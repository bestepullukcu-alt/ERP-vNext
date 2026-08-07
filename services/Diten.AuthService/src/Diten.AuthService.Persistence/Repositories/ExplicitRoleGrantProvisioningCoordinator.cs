using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.S2S;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class ExplicitRoleGrantProvisioningCoordinator : IExplicitRoleGrantProvisioningCoordinator
{
    private const int MaximumTransactionBodyAttempts = 3;
    private const string ReceiptIdentityIndexName = "ux_tenant_actor_operation_idempotency";
    public const string ReceiptsCollection = "explicitRoleGrantProvisioningReceipts";
    public const string AuditsCollection = "explicitRoleGrantProvisioningAudits";
    public const string VersionsCollection = "auth_role_assignment_versions";
    private readonly IMongoClient _client;
    private readonly IExplicitRoleGrantProvisioningAuthorizer _authorizer;
    private readonly IExplicitRoleGrantTransactionProbe _probe;
    private readonly IMongoCollection<Role> _roles;
    private readonly IMongoCollection<Permission> _permissions;
    private readonly IMongoCollection<PermissionOwnerDocument> _owners;
    private readonly IMongoCollection<RolePermission> _grants;
    private readonly IMongoCollection<ExplicitRoleGrantReceiptDocument> _receipts;
    private readonly IMongoCollection<ExplicitRoleGrantVersionDocument> _versions;
    private readonly IMongoCollection<ExplicitRoleGrantAuditDocument> _audits;
    private readonly AuthCommonUuidCompatibilityGuard _uuidCompatibilityGuard;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private volatile bool _indexesReady;

    public ExplicitRoleGrantProvisioningCoordinator(IMongoClient client, IMongoDatabase database,
        IExplicitRoleGrantProvisioningAuthorizer authorizer, IExplicitRoleGrantTransactionProbe probe)
    {
        _client = client; _authorizer = authorizer; _probe = probe;
        _roles = database.GetCollection<Role>("roles");
        _permissions = database.GetCollection<Permission>("permissions");
        _owners = database.GetCollection<PermissionOwnerDocument>(PermissionCatalogManifestRegistrar.OwnersCollection);
        _grants = database.GetCollection<RolePermission>("rolePermissions");
        _receipts = database.GetCollection<ExplicitRoleGrantReceiptDocument>(ReceiptsCollection);
        _versions = database.GetCollection<ExplicitRoleGrantVersionDocument>(VersionsCollection);
        _audits = database.GetCollection<ExplicitRoleGrantAuditDocument>(AuditsCollection);
        _uuidCompatibilityGuard = new AuthCommonUuidCompatibilityGuard(database);
    }

    public Task<ExplicitRoleGrantProvisioningResult> ExecuteAsync(ExplicitRoleGrantProvisioningV1 request, CancellationToken cancellationToken) =>
        ExecuteAsync(request, cancellationToken, transactionBodyAttempt: 1);

    private async Task<ExplicitRoleGrantProvisioningResult> ExecuteAsync(
        ExplicitRoleGrantProvisioningV1 request,
        CancellationToken cancellationToken,
        int transactionBodyAttempt)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.AuthenticatedActorId == Guid.Empty) return Result(ExplicitRoleGrantProvisioningStatus.Unauthorized, request);
        request.Validate();
        var authorization = await _authorizer.AuthorizeAsync(request.TenantId, request.AuthenticatedActorId,
            request.Mutation, request.TrustedAuthorizationProvenance, cancellationToken);
        if (authorization != ExplicitRoleGrantAuthorizationDecision.Allowed)
            return Result(authorization switch
            {
                ExplicitRoleGrantAuthorizationDecision.Denied => ExplicitRoleGrantProvisioningStatus.Forbidden,
                ExplicitRoleGrantAuthorizationDecision.NotReferenceable => ExplicitRoleGrantProvisioningStatus.NotFound,
                _ => ExplicitRoleGrantProvisioningStatus.Unavailable
            }, request);

        try
        {
            await _uuidCompatibilityGuard.EnsureAuthorizationDocumentsCompatibleAsync(
                request.TenantId, request.RoleId, request.PermissionId, cancellationToken);
            await _uuidCompatibilityGuard.EnsureRoleAssignmentVersionCompatibleAsync(request.TenantId, cancellationToken);
        }
        catch (AuthUuidMigrationRequiredException)
        {
            return Result(ExplicitRoleGrantProvisioningStatus.Unavailable, request);
        }

        await EnsureIndexesAsync(cancellationToken);
        var existing = await FindReceiptAsync(request, cancellationToken);
        if (existing is not null) return ReceiptResult(existing, request.CanonicalPayloadHash);

        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority));
        try
        {
            _probe.BodyStarted();
            var roleFence = await _roles.UpdateOneAsync(session, new BsonDocument
            {
                { "TenantId", CommonGuid(request.TenantId) }, { "_id", CommonGuid(request.RoleId) }, { "IsDeleted", false }
            },
                Builders<Role>.Update.Inc(x => x.ExplicitGrantValidationFence, 1), cancellationToken: cancellationToken);
            if (roleFence.MatchedCount != 1) return await AbortResultAsync(session, ExplicitRoleGrantProvisioningStatus.NotFound, request);
            await _probe.AfterParticipantAsync(ExplicitRoleGrantTransactionParticipant.RoleFence, cancellationToken);

            var permission = await _permissions.Find(session, new BsonDocument("_id", CommonGuid(request.PermissionId))).FirstOrDefaultAsync(cancellationToken);
            if (permission is null || permission.IsDeleted || !DefaultRolePermissionTemplate.IsTenantAssignable(permission))
                return await AbortResultAsync(session, ExplicitRoleGrantProvisioningStatus.Forbidden, request);
            var owner = await _owners.Find(session, Builders<PermissionOwnerDocument>.Filter.And(
                new BsonDocument("PermissionId", CommonGuid(permission.Id)),
                Builders<PermissionOwnerDocument>.Filter.Eq(x => x.PermissionKey, permission.Key),
                Builders<PermissionOwnerDocument>.Filter.Eq(x => x.ModuleEntitlementCode, permission.Module))).FirstOrDefaultAsync(cancellationToken);
            if (owner is null) return await AbortResultAsync(session, ExplicitRoleGrantProvisioningStatus.Conflict, request);
            var permissionFence = await _permissions.UpdateOneAsync(session, Builders<Permission>.Filter.And(
                new BsonDocument("_id", CommonGuid(permission.Id)), Builders<Permission>.Filter.Eq(x => x.Key, permission.Key),
                Builders<Permission>.Filter.Eq(x => x.Module, owner.ModuleEntitlementCode), Builders<Permission>.Filter.Eq(x => x.Scope, PermissionScope.Tenant),
                Builders<Permission>.Filter.Eq(x => x.IsDeleted, false)), Builders<Permission>.Update.Inc(x => x.ExplicitGrantValidationFence, 1), cancellationToken: cancellationToken);
            if (permissionFence.MatchedCount != 1) return await AbortResultAsync(session, ExplicitRoleGrantProvisioningStatus.Conflict, request);
            await _probe.AfterParticipantAsync(ExplicitRoleGrantTransactionParticipant.PermissionFence, cancellationToken);

            var receiptId = Guid.NewGuid();
            var receipt = ExplicitRoleGrantReceiptDocument.Create(receiptId, request);
            await _receipts.InsertOneAsync(session, receipt, cancellationToken: cancellationToken);
            await _probe.AfterParticipantAsync(ExplicitRoleGrantTransactionParticipant.IdempotencyReceipt, cancellationToken);

            var grantFilter = new BsonDocument
            {
                { "TenantId", CommonGuid(request.TenantId) }, { "RoleId", CommonGuid(request.RoleId) },
                { "PermissionId", CommonGuid(request.PermissionId) }, { "IsDeleted", false }
            };
            var grant = await _grants.Find(session, grantFilter).FirstOrDefaultAsync(cancellationToken);
            var changed = false;
            if (request.Mutation == ExplicitRoleGrantMutationV1.Grant && grant is null)
            {
                await _grants.InsertOneAsync(session, RolePermission.ManualGrant(request.RoleId, request.PermissionId,
                    request.TenantId, request.AuthenticatedActorId.ToString("D")), cancellationToken: cancellationToken);
                changed = true;
            }
            else if (request.Mutation == ExplicitRoleGrantMutationV1.Revoke && grant is not null)
            {
                if (grant.GrantSource != GrantSource.Manual)
                    return await AbortResultAsync(session, ExplicitRoleGrantProvisioningStatus.Conflict, request);
                await _grants.DeleteOneAsync(session, grantFilter, cancellationToken: cancellationToken);
                changed = true;
            }
            await _probe.AfterParticipantAsync(ExplicitRoleGrantTransactionParticipant.RolePermissionMutation, cancellationToken);

            var version = changed
                ? await IncrementVersionAsync(session, request.TenantId, cancellationToken)
                : await ReadVersionAsync(session, request.TenantId, cancellationToken);
            await _probe.AfterParticipantAsync(ExplicitRoleGrantTransactionParticipant.AuthorizationVersion, cancellationToken);

            receipt.AuthorizationStateChanged = changed; receipt.AuthorizationVersion = version;
            await _receipts.ReplaceOneAsync(session, new BsonDocument("_id", CommonGuid(receipt.Id)), receipt, cancellationToken: cancellationToken);
            await _audits.InsertOneAsync(session, ExplicitRoleGrantAuditDocument.Create(receiptId, request, changed, version), cancellationToken: cancellationToken);
            await _probe.AfterParticipantAsync(ExplicitRoleGrantTransactionParticipant.ImmutableAudit, cancellationToken);

            await CommitOnlyAsync(session, _probe, cancellationToken);
            return new(changed ? ExplicitRoleGrantProvisioningStatus.Applied : ExplicitRoleGrantProvisioningStatus.NoOp,
                receiptId, changed, version, request.CanonicalPayloadHash);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { await AbortAsync(session); throw; }
        catch (MongoWriteException ex) when (IsReceiptIdentityDuplicate(ex))
        { await AbortAsync(session); return await ReconcileAsync(request, cancellationToken); }
        catch (MongoWriteException ex) when (IsExactAssignmentDuplicate(ex, request) && transactionBodyAttempt < MaximumTransactionBodyAttempts)
        {
            await AbortAsync(session);
            await _probe.BeforeRetryBarrierAsync(cancellationToken);
            var barrier = await SynchronizeRoleFenceAsync(request, cancellationToken);
            if (barrier != RoleFenceBarrierResult.Synchronized)
                return Result(barrier == RoleFenceBarrierResult.NotFound
                    ? ExplicitRoleGrantProvisioningStatus.NotFound
                    : ExplicitRoleGrantProvisioningStatus.Unavailable, request);
            return await ExecuteAsync(request, cancellationToken, transactionBodyAttempt + 1);
        }
        catch (ExplicitRoleGrantInjectedFailureException) { await AbortAsync(session); return Result(ExplicitRoleGrantProvisioningStatus.Unavailable, request); }
        catch (ExplicitRoleGrantUnknownCommitException) { await AbortAsync(session); return await ReconcileAsync(request, cancellationToken, true); }
        catch (MongoException ex) when (ex.HasErrorLabel("UnknownTransactionCommitResult"))
        { await AbortAsync(session); return await ReconcileAsync(request, cancellationToken, true); }
        catch (MongoException ex) when (ex.HasErrorLabel("TransientTransactionError") && transactionBodyAttempt < MaximumTransactionBodyAttempts)
        {
            await AbortAsync(session);
            await _probe.BeforeRetryBarrierAsync(cancellationToken);
            var barrier = await SynchronizeRoleFenceAsync(request, cancellationToken);
            if (barrier != RoleFenceBarrierResult.Synchronized)
                return Result(barrier == RoleFenceBarrierResult.NotFound
                    ? ExplicitRoleGrantProvisioningStatus.NotFound
                    : ExplicitRoleGrantProvisioningStatus.Unavailable, request);
            return await ExecuteAsync(request, cancellationToken, transactionBodyAttempt + 1);
        }
        catch (MongoException) { await AbortAsync(session); return Result(ExplicitRoleGrantProvisioningStatus.Unavailable, request); }
        catch (TimeoutException) { await AbortAsync(session); return Result(ExplicitRoleGrantProvisioningStatus.Unavailable, request); }
    }

    private async Task<long> IncrementVersionAsync(IClientSessionHandle session, Guid tenantId, CancellationToken ct)
    {
        var options = new FindOneAndUpdateOptions<ExplicitRoleGrantVersionDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After };
        var doc = await _versions.FindOneAndUpdateAsync(session, new BsonDocument("_id", CommonGuid(tenantId)),
            Builders<ExplicitRoleGrantVersionDocument>.Update.Inc(x => x.Version, 1).Set(x => x.UpdatedAt, DateTimeOffset.UtcNow), options, ct);
        return doc.Version;
    }
    private async Task<long> ReadVersionAsync(IClientSessionHandle session, Guid tenantId, CancellationToken ct) =>
        (await _versions.Find(session, new BsonDocument("_id", CommonGuid(tenantId))).FirstOrDefaultAsync(ct))?.Version ?? 0;

    private async Task<ExplicitRoleGrantProvisioningResult> ReconcileAsync(ExplicitRoleGrantProvisioningV1 request, CancellationToken ct, bool committedMatch = false)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var receipt = await FindReceiptAsync(request, ct);
            if (receipt is not null)
            {
                var result = ReceiptResult(receipt, request.CanonicalPayloadHash);
                return committedMatch && result.Status != ExplicitRoleGrantProvisioningStatus.Conflict
                    ? result with { Status = receipt.AuthorizationStateChanged ? ExplicitRoleGrantProvisioningStatus.Applied : ExplicitRoleGrantProvisioningStatus.NoOp }
                    : result;
            }
            if (attempt < 4) await Task.Delay(25, ct);
        }
        return Result(ExplicitRoleGrantProvisioningStatus.Unavailable, request);
    }

    private async Task<ExplicitRoleGrantReceiptDocument?> FindReceiptAsync(ExplicitRoleGrantProvisioningV1 request, CancellationToken ct) =>
        await _receipts.WithReadConcern(ReadConcern.Majority).Find(Builders<ExplicitRoleGrantReceiptDocument>.Filter.And(
            new BsonDocument("TenantId", CommonGuid(request.TenantId)),
            new BsonDocument("AuthenticatedActorId", CommonGuid(request.AuthenticatedActorId)),
            Builders<ExplicitRoleGrantReceiptDocument>.Filter.Eq(x => x.Operation, request.Mutation),
            Builders<ExplicitRoleGrantReceiptDocument>.Filter.Eq(x => x.IdempotencyKey, request.IdempotencyKey))).FirstOrDefaultAsync(ct);

    private async Task<RoleFenceBarrierResult> SynchronizeRoleFenceAsync(ExplicitRoleGrantProvisioningV1 request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var result = await _roles.WithWriteConcern(WriteConcern.WMajority).UpdateOneAsync(new BsonDocument
            {
                { "TenantId", CommonGuid(request.TenantId) },
                { "_id", CommonGuid(request.RoleId) },
                { "IsDeleted", false }
            }, Builders<Role>.Update.Inc(x => x.ExplicitGrantValidationFence, 1), cancellationToken: ct);
            return result.MatchedCount == 1 ? RoleFenceBarrierResult.Synchronized : RoleFenceBarrierResult.NotFound;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (MongoException) { return RoleFenceBarrierResult.Unavailable; }
    }

    private static bool IsReceiptIdentityDuplicate(MongoWriteException exception) =>
        IsDuplicateForIndex(exception, ReceiptIdentityIndexName);

    private static bool IsExactAssignmentDuplicate(MongoWriteException exception, ExplicitRoleGrantProvisioningV1 request) =>
        request.Mutation == ExplicitRoleGrantMutationV1.Grant &&
        IsDuplicateForIndex(exception, RolePermissionRepository.AssignmentUniqueIndexName);

    private static bool IsDuplicateForIndex(MongoWriteException exception, string indexName) =>
        exception.WriteError?.Category == ServerErrorCategory.DuplicateKey &&
        exception.WriteError.Message.Contains(indexName, StringComparison.Ordinal);

    private enum RoleFenceBarrierResult { Synchronized, NotFound, Unavailable }

    private static ExplicitRoleGrantProvisioningResult ReceiptResult(ExplicitRoleGrantReceiptDocument receipt, string hash) =>
        string.Equals(receipt.PayloadHash, hash, StringComparison.Ordinal)
            ? new(receipt.AuthorizationStateChanged ? ExplicitRoleGrantProvisioningStatus.Applied : ExplicitRoleGrantProvisioningStatus.NoOp,
                receipt.Id, receipt.AuthorizationStateChanged, receipt.AuthorizationVersion, receipt.PayloadHash)
            : new(ExplicitRoleGrantProvisioningStatus.Conflict, receipt.Id, false, receipt.AuthorizationVersion, hash);
    private static ExplicitRoleGrantProvisioningResult Result(ExplicitRoleGrantProvisioningStatus status, ExplicitRoleGrantProvisioningV1 r) => new(status, Guid.Empty, false, 0, r.CanonicalPayloadHash);
    private static BsonBinaryData CommonGuid(Guid value) => MongoGuidRepresentationPolicy.ToBson(value);
    private static async Task<ExplicitRoleGrantProvisioningResult> AbortResultAsync(IClientSessionHandle s, ExplicitRoleGrantProvisioningStatus status, ExplicitRoleGrantProvisioningV1 r)
    { await AbortAsync(s); return Result(status, r); }

    private static async Task CommitOnlyAsync(IClientSessionHandle session, IExplicitRoleGrantTransactionProbe probe, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                if (probe.BeforeCommit(attempt) == ExplicitRoleGrantCommitDirective.SimulateUnknownBeforeSend) throw new ExplicitRoleGrantUnknownCommitException();
                await session.CommitTransactionAsync(ct); probe.AfterCommit(attempt); return;
            }
            catch (MongoException ex) when (ex.HasErrorLabel("UnknownTransactionCommitResult") && attempt < 3) { }
            catch (ExplicitRoleGrantUnknownCommitException ex) when (!ex.DurableCommitPossible && attempt < 3) { }
        }
        throw new ExplicitRoleGrantUnknownCommitException();
    }
    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        if (_indexesReady) return; await _indexLock.WaitAsync(ct);
        try
        {
            if (_indexesReady) return;
            await _receipts.Indexes.CreateOneAsync(new CreateIndexModel<ExplicitRoleGrantReceiptDocument>(
                Builders<ExplicitRoleGrantReceiptDocument>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.AuthenticatedActorId).Ascending(x => x.Operation).Ascending(x => x.IdempotencyKey),
                new() { Unique = true, Name = ReceiptIdentityIndexName }), cancellationToken: ct);
            await _grants.Indexes.CreateOneAsync(new CreateIndexModel<RolePermission>(
                Builders<RolePermission>.IndexKeys.Ascending(x => x.RoleId).Ascending(x => x.PermissionId).Ascending(x => x.TenantId),
                new() { Unique = true, Name = RolePermissionRepository.AssignmentUniqueIndexName }), cancellationToken: ct);
            await _audits.Indexes.CreateOneAsync(new CreateIndexModel<ExplicitRoleGrantAuditDocument>(
                Builders<ExplicitRoleGrantAuditDocument>.IndexKeys.Ascending(x => x.ReceiptId), new() { Unique = true, Name = "ux_receipt_audit" }), cancellationToken: ct);
            _indexesReady = true;
        }
        finally { _indexLock.Release(); }
    }
    private static async Task AbortAsync(IClientSessionHandle s) { if (!s.IsInTransaction) return; try { await s.AbortTransactionAsync(CancellationToken.None); } catch (MongoException) { } }
}

public sealed class ExplicitRoleGrantReceiptDocument
{
    public Guid Id { get; set; } public Guid TenantId { get; set; } public Guid AuthenticatedActorId { get; set; }
    public ExplicitRoleGrantMutationV1 Operation { get; set; } public string IdempotencyKey { get; set; } = "";
    public Guid RoleId { get; set; } public Guid PermissionId { get; set; } public string PayloadHash { get; set; } = "";
    public string TrustedAuthorizationProvenance { get; set; } = ""; public bool AuthorizationStateChanged { get; set; }
    public long AuthorizationVersion { get; set; } public DateTimeOffset CreatedAtUtc { get; set; }
    public static ExplicitRoleGrantReceiptDocument Create(Guid id, ExplicitRoleGrantProvisioningV1 r) => new() { Id = id, TenantId = r.TenantId, AuthenticatedActorId = r.AuthenticatedActorId, Operation = r.Mutation, IdempotencyKey = r.IdempotencyKey, RoleId = r.RoleId, PermissionId = r.PermissionId, PayloadHash = r.CanonicalPayloadHash, TrustedAuthorizationProvenance = r.TrustedAuthorizationProvenance, CreatedAtUtc = DateTimeOffset.UtcNow };
}
public sealed class ExplicitRoleGrantAuditDocument
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid ReceiptId { get; set; } public Guid TenantId { get; set; }
    public Guid AuthenticatedActorId { get; set; } public Guid RoleId { get; set; } public Guid PermissionId { get; set; }
    public ExplicitRoleGrantMutationV1 Operation { get; set; } public bool AuthorizationStateChanged { get; set; }
    public long AuthorizationVersion { get; set; } public string PayloadHash { get; set; } = ""; public DateTimeOffset OccurredAtUtc { get; set; }
    public static ExplicitRoleGrantAuditDocument Create(Guid receiptId, ExplicitRoleGrantProvisioningV1 r, bool changed, long version) => new() { ReceiptId = receiptId, TenantId = r.TenantId, AuthenticatedActorId = r.AuthenticatedActorId, RoleId = r.RoleId, PermissionId = r.PermissionId, Operation = r.Mutation, AuthorizationStateChanged = changed, AuthorizationVersion = version, PayloadHash = r.CanonicalPayloadHash, OccurredAtUtc = DateTimeOffset.UtcNow };
}
public sealed class ExplicitRoleGrantVersionDocument
{
    [BsonId] public Guid TenantId { get; set; } public long Version { get; set; } public DateTimeOffset UpdatedAt { get; set; }
}
public enum ExplicitRoleGrantTransactionParticipant { RoleFence, PermissionFence, IdempotencyReceipt, RolePermissionMutation, AuthorizationVersion, ImmutableAudit }
public enum ExplicitRoleGrantCommitDirective { Send, SimulateUnknownBeforeSend }
public interface IExplicitRoleGrantTransactionProbe
{
    void BodyStarted(); Task AfterParticipantAsync(ExplicitRoleGrantTransactionParticipant participant, CancellationToken cancellationToken);
    Task BeforeRetryBarrierAsync(CancellationToken cancellationToken);
    ExplicitRoleGrantCommitDirective BeforeCommit(int attempt); void AfterCommit(int attempt);
}
internal sealed class NoOpExplicitRoleGrantTransactionProbe : IExplicitRoleGrantTransactionProbe
{
    internal static readonly NoOpExplicitRoleGrantTransactionProbe Instance = new(); public void BodyStarted() { }
    public Task AfterParticipantAsync(ExplicitRoleGrantTransactionParticipant participant, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task BeforeRetryBarrierAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public ExplicitRoleGrantCommitDirective BeforeCommit(int attempt) => ExplicitRoleGrantCommitDirective.Send; public void AfterCommit(int attempt) { }
}
internal sealed class UnboundExplicitRoleGrantProvisioningAuthorizer : IExplicitRoleGrantProvisioningAuthorizer
{
    internal static readonly UnboundExplicitRoleGrantProvisioningAuthorizer Instance = new();
    public Task<ExplicitRoleGrantAuthorizationDecision> AuthorizeAsync(Guid tenantId, Guid authenticatedActorId, ExplicitRoleGrantMutationV1 mutation, string trustedProvenance, CancellationToken cancellationToken) => Task.FromResult(ExplicitRoleGrantAuthorizationDecision.Unavailable);
}
public sealed class ExplicitRoleGrantInjectedFailureException : Exception;
public sealed class ExplicitRoleGrantUnknownCommitException(bool durableCommitPossible = false) : Exception { public bool DurableCommitPossible { get; } = durableCommitPossible; }
