using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.S2S;
using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class PermissionCatalogManifestRegistrar : IPermissionCatalogManifestRegistrar
{
    internal const string ManifestsCollection = "s2sPermissionCatalogManifests";
    internal const string OperationsCollection = "s2sPermissionCatalogOperations";
    internal const string OwnersCollection = "s2sPermissionCatalogPermissionOwners";
    internal const string AuditsCollection = "s2sPermissionCatalogRegistrationAudits";

    private readonly IMongoClient _client;
    private readonly IMongoCollection<ManifestDocument> _manifests;
    private readonly IMongoCollection<OperationDocument> _operations;
    private readonly IMongoCollection<PermissionOwnerDocument> _owners;
    private readonly IMongoCollection<RegistrationAuditDocument> _audits;
    private readonly IMongoCollection<Permission> _permissions;
    private readonly IPermissionCatalogTransactionProbe _probe;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private volatile bool _indexesReady;

    public PermissionCatalogManifestRegistrar(IMongoClient client, IMongoDatabase database)
        : this(client, database, NoOpPermissionCatalogTransactionProbe.Instance) { }

    public PermissionCatalogManifestRegistrar(IMongoClient client, IMongoDatabase database, IPermissionCatalogTransactionProbe probe)
    {
        _client = client;
        _manifests = database.GetCollection<ManifestDocument>(ManifestsCollection);
        _operations = database.GetCollection<OperationDocument>(OperationsCollection);
        _owners = database.GetCollection<PermissionOwnerDocument>(OwnersCollection);
        _audits = database.GetCollection<RegistrationAuditDocument>(AuditsCollection);
        _permissions = database.GetCollection<Permission>("permissions");
        _probe = probe;
    }

    public async Task<PermissionCatalogRegistrationResult> RegisterAsync(PermissionCatalogManifestV1 manifest, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PermissionCatalogManifestValidator.ValidateShape(manifest);
        var canonical = GateIPermissionCatalogManifests.All.SingleOrDefault(x =>
            string.Equals(x.OwnerModuleId, manifest.OwnerModuleId, StringComparison.Ordinal));
        if (canonical is not null &&
            string.Equals(canonical.ManifestVersion, manifest.ManifestVersion, StringComparison.Ordinal) &&
            !PermissionCatalogManifestValidator.SamePayload(canonical, manifest))
            return new(PermissionCatalogRegistrationStatus.Conflict, Guid.Empty, manifest.CanonicalPayloadHash);
        PermissionCatalogManifestValidator.ValidateCanonical(manifest);
        await EnsureIndexesAsync(cancellationToken);

        var existing = await FindManifestAsync(manifest.OwnerModuleId, manifest.ManifestVersion, cancellationToken);
        if (existing is not null) return ExistingResult(existing, manifest.CanonicalPayloadHash);

        using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority));
        try
        {
            _probe.BodyStarted();
            var registrationId = Guid.NewGuid();
            await ValidatePersistentCollisionsAsync(session, manifest, cancellationToken);
            await _manifests.InsertOneAsync(session, ManifestDocument.From(registrationId, manifest), cancellationToken: cancellationToken);
            await _probe.AfterParticipantAsync(PermissionCatalogTransactionParticipant.ManifestHeader, cancellationToken);

            foreach (var entry in manifest.Entries)
                await _operations.InsertOneAsync(session, OperationDocument.From(registrationId, manifest, entry), cancellationToken: cancellationToken);
            await _probe.AfterParticipantAsync(PermissionCatalogTransactionParticipant.OperationMappings, cancellationToken);

            var boundPermissions = new List<(string Key, Permission Permission)>();
            foreach (var key in manifest.Entries.Select(x => x.PermissionKey).Distinct(StringComparer.Ordinal))
            {
                var permission = await BindPermissionAsync(session, manifest, key, cancellationToken);
                boundPermissions.Add((key, permission));
            }
            await _probe.AfterParticipantAsync(PermissionCatalogTransactionParticipant.PermissionsCatalog, cancellationToken);

            foreach (var binding in boundPermissions)
                await _owners.InsertOneAsync(session, PermissionOwnerDocument.From(registrationId, manifest, binding.Permission.Id, binding.Key), cancellationToken: cancellationToken);
            await _probe.AfterParticipantAsync(PermissionCatalogTransactionParticipant.PermissionOwnershipBindings, cancellationToken);

            await _audits.InsertOneAsync(session, RegistrationAuditDocument.From(registrationId, manifest), cancellationToken: cancellationToken);
            await _probe.AfterParticipantAsync(PermissionCatalogTransactionParticipant.RegistrationAudit, cancellationToken);
            await CommitOnlyAsync(session, _probe, cancellationToken);
            return new(PermissionCatalogRegistrationStatus.Registered, registrationId, manifest.CanonicalPayloadHash);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await AbortAsync(session);
            throw;
        }
        catch (PermissionCatalogConflictException)
        {
            await AbortAsync(session);
            return new(PermissionCatalogRegistrationStatus.Conflict, Guid.Empty, manifest.CanonicalPayloadHash);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            await AbortAsync(session);
            return await ReconcileAsync(manifest, cancellationToken);
        }
        catch (MongoException ex) when (ex.HasErrorLabel("UnknownTransactionCommitResult"))
        {
            await AbortAsync(session);
            return await ReconcileAsync(manifest, cancellationToken, PermissionCatalogRegistrationStatus.Registered);
        }
        catch (MongoException ex) when (ex.HasErrorLabel("TransientTransactionError"))
        {
            await AbortAsync(session);
            return await ReconcileAsync(manifest, cancellationToken);
        }
        catch (MongoException)
        {
            await AbortAsync(session);
            return new(PermissionCatalogRegistrationStatus.Unavailable, Guid.Empty, manifest.CanonicalPayloadHash);
        }
        catch (TimeoutException)
        {
            await AbortAsync(session);
            return new(PermissionCatalogRegistrationStatus.Unavailable, Guid.Empty, manifest.CanonicalPayloadHash);
        }
        catch (PermissionCatalogInjectedFailureException)
        {
            await AbortAsync(session);
            return new(PermissionCatalogRegistrationStatus.Unavailable, Guid.Empty, manifest.CanonicalPayloadHash);
        }
        catch (PermissionCatalogUnknownCommitException)
        {
            await AbortAsync(session);
            return await ReconcileAsync(manifest, cancellationToken, PermissionCatalogRegistrationStatus.Registered);
        }
    }

    private async Task ValidatePersistentCollisionsAsync(IClientSessionHandle session, PermissionCatalogManifestV1 manifest, CancellationToken ct)
    {
        var header = await _manifests.Find(session, Builders<ManifestDocument>.Filter.And(
            Builders<ManifestDocument>.Filter.Eq(x => x.OwnerModuleId, manifest.OwnerModuleId),
            Builders<ManifestDocument>.Filter.Eq(x => x.ManifestVersion, manifest.ManifestVersion))).FirstOrDefaultAsync(ct);
        if (header is not null) throw new PermissionCatalogConflictException();

        var operationIds = manifest.Entries.Select(x => x.OperationId).ToArray();
        var operations = await _operations.Find(session, Builders<OperationDocument>.Filter.In(x => x.OperationId, operationIds)).ToListAsync(ct);
        if (operations.Any(x => !string.Equals(x.OwnerModuleId, manifest.OwnerModuleId, StringComparison.Ordinal))) throw new PermissionCatalogConflictException();

        var keys = manifest.Entries.Select(x => x.PermissionKey).Distinct(StringComparer.Ordinal).ToArray();
        var owners = await _owners.Find(session, Builders<PermissionOwnerDocument>.Filter.In(x => x.PermissionKey, keys)).ToListAsync(ct);
        if (owners.Any(x => !string.Equals(x.OwnerModuleId, manifest.OwnerModuleId, StringComparison.Ordinal))) throw new PermissionCatalogConflictException();
    }

    private async Task<Permission> BindPermissionAsync(IClientSessionHandle session, PermissionCatalogManifestV1 manifest, string key, CancellationToken ct)
    {
        var existing = await _permissions.Find(session, Builders<Permission>.Filter.Eq(x => x.Key, key)).FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            if (existing.IsDeleted || existing.Scope != PermissionScope.Tenant ||
                !string.Equals(existing.Module, manifest.ModuleEntitlementCode, StringComparison.Ordinal) ||
                !string.Equals(existing.Key, key, StringComparison.Ordinal)) throw new PermissionCatalogConflictException();
            return existing;
        }

        var segments = key.Split('.');
        var permission = new Permission(segments[0], string.Join('.', segments[1..^1]), segments[^1], key, null,
            manifest.ModuleEntitlementCode, PermissionScope.Tenant);
        if (!string.Equals(permission.Key, key, StringComparison.Ordinal)) throw new PermissionCatalogConflictException();
        await _permissions.InsertOneAsync(session, permission, cancellationToken: ct);
        return permission;
    }

    private async Task<PermissionCatalogRegistrationResult> ReconcileAsync(PermissionCatalogManifestV1 manifest, CancellationToken ct,
        PermissionCatalogRegistrationStatus matchingStatus = PermissionCatalogRegistrationStatus.NoOp)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var existing = await FindManifestAsync(manifest.OwnerModuleId, manifest.ManifestVersion, ct);
            if (existing is not null)
            {
                var result = ExistingResult(existing, manifest.CanonicalPayloadHash);
                return result.Status == PermissionCatalogRegistrationStatus.NoOp
                    ? result with { Status = matchingStatus }
                    : result;
            }
            if (attempt < 4) await Task.Delay(TimeSpan.FromMilliseconds(25), ct);
        }
        return new(PermissionCatalogRegistrationStatus.Unavailable, Guid.Empty, manifest.CanonicalPayloadHash);
    }

    private async Task<ManifestDocument?> FindManifestAsync(string owner, string version, CancellationToken ct) =>
        await _manifests.WithReadConcern(ReadConcern.Majority).Find(Builders<ManifestDocument>.Filter.And(
            Builders<ManifestDocument>.Filter.Eq(x => x.OwnerModuleId, owner), Builders<ManifestDocument>.Filter.Eq(x => x.ManifestVersion, version))).FirstOrDefaultAsync(ct);

    private static PermissionCatalogRegistrationResult ExistingResult(ManifestDocument existing, string requestedHash) =>
        string.Equals(existing.PayloadHash, requestedHash, StringComparison.Ordinal)
            ? new(PermissionCatalogRegistrationStatus.NoOp, existing.RegistrationId, existing.PayloadHash)
            : new(PermissionCatalogRegistrationStatus.Conflict, existing.RegistrationId, requestedHash);

    private static async Task CommitOnlyAsync(IClientSessionHandle session, IPermissionCatalogTransactionProbe probe, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                if (probe.BeforeCommit(attempt) == PermissionCatalogCommitDirective.SimulateUnknownBeforeSend)
                    throw new PermissionCatalogUnknownCommitException();
                await session.CommitTransactionAsync(ct);
                probe.AfterCommit(attempt);
                return;
            }
            catch (MongoException ex) when (ex.HasErrorLabel("UnknownTransactionCommitResult") && attempt < 2) { }
            catch (PermissionCatalogUnknownCommitException ex) when (!ex.DurableCommitPossible && attempt < 3) { }
        }
        throw new PermissionCatalogUnknownCommitException();
    }

    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        if (_indexesReady) return;
        await _indexLock.WaitAsync(ct);
        try
        {
            if (_indexesReady) return;
            await _manifests.Indexes.CreateOneAsync(new CreateIndexModel<ManifestDocument>(Builders<ManifestDocument>.IndexKeys.Ascending(x => x.OwnerModuleId).Ascending(x => x.ManifestVersion), new() { Unique = true, Name = "ux_owner_version" }), cancellationToken: ct);
            await _operations.Indexes.CreateOneAsync(new CreateIndexModel<OperationDocument>(Builders<OperationDocument>.IndexKeys.Ascending(x => x.OperationId), new() { Unique = true, Name = "ux_operation" }), cancellationToken: ct);
            await _owners.Indexes.CreateOneAsync(new CreateIndexModel<PermissionOwnerDocument>(Builders<PermissionOwnerDocument>.IndexKeys.Ascending(x => x.PermissionKey), new() { Unique = true, Name = "ux_permission_owner" }), cancellationToken: ct);
            await _audits.Indexes.CreateOneAsync(new CreateIndexModel<RegistrationAuditDocument>(Builders<RegistrationAuditDocument>.IndexKeys.Ascending(x => x.OwnerModuleId).Ascending(x => x.ManifestVersion), new() { Unique = true, Name = "ux_audit_owner_version" }), cancellationToken: ct);
            _indexesReady = true;
        }
        finally { _indexLock.Release(); }
    }

    private static async Task AbortAsync(IClientSessionHandle session)
    { if (!session.IsInTransaction) return; try { await session.AbortTransactionAsync(CancellationToken.None); } catch (MongoException) { } }

    private sealed class PermissionCatalogConflictException : Exception;
}

public enum PermissionCatalogTransactionParticipant
{
    ManifestHeader,
    OperationMappings,
    PermissionsCatalog,
    PermissionOwnershipBindings,
    RegistrationAudit
}

public enum PermissionCatalogCommitDirective { Send, SimulateUnknownBeforeSend }

public interface IPermissionCatalogTransactionProbe
{
    void BodyStarted();
    Task AfterParticipantAsync(PermissionCatalogTransactionParticipant participant, CancellationToken cancellationToken);
    PermissionCatalogCommitDirective BeforeCommit(int attempt);
    void AfterCommit(int attempt);
}

internal sealed class NoOpPermissionCatalogTransactionProbe : IPermissionCatalogTransactionProbe
{
    internal static readonly NoOpPermissionCatalogTransactionProbe Instance = new();
    public void BodyStarted() { }
    public Task AfterParticipantAsync(PermissionCatalogTransactionParticipant participant, CancellationToken cancellationToken) => Task.CompletedTask;
    public PermissionCatalogCommitDirective BeforeCommit(int attempt) => PermissionCatalogCommitDirective.Send;
    public void AfterCommit(int attempt) { }
}

public sealed class PermissionCatalogInjectedFailureException : Exception;
public sealed class PermissionCatalogUnknownCommitException(bool durableCommitPossible = false) : Exception
{
    public bool DurableCommitPossible { get; } = durableCommitPossible;
}

public sealed class ManifestDocument
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid RegistrationId { get; set; }
    public string OwnerModuleId { get; set; } = ""; public string ModuleEntitlementCode { get; set; } = "";
    public string ServiceIdentity { get; set; } = ""; public string ClientId { get; set; } = ""; public string Audience { get; set; } = "";
    public string ProtocolScope { get; set; } = ""; public string ManifestVersion { get; set; } = ""; public string PayloadHash { get; set; } = "";
    public string Provenance { get; set; } = ""; public DateTimeOffset RegisteredAtUtc { get; set; }
    public static ManifestDocument From(Guid id, PermissionCatalogManifestV1 m) => new() { RegistrationId = id, OwnerModuleId = m.OwnerModuleId, ModuleEntitlementCode = m.ModuleEntitlementCode, ServiceIdentity = m.ServiceIdentity, ClientId = m.ClientId, Audience = m.Audience, ProtocolScope = m.ProtocolScope, ManifestVersion = m.ManifestVersion, PayloadHash = m.CanonicalPayloadHash, Provenance = m.RegistrationProvenance, RegisteredAtUtc = m.RegisteredAtUtc };
}
public sealed class OperationDocument
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid RegistrationId { get; set; } public string OwnerModuleId { get; set; } = ""; public string ManifestVersion { get; set; } = ""; public string OperationId { get; set; } = ""; public string PermissionKey { get; set; } = "";
    public static OperationDocument From(Guid id, PermissionCatalogManifestV1 m, PermissionCatalogOperationV1 e) => new() { RegistrationId = id, OwnerModuleId = m.OwnerModuleId, ManifestVersion = m.ManifestVersion, OperationId = e.OperationId, PermissionKey = e.PermissionKey };
}
public sealed class PermissionOwnerDocument
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid RegistrationId { get; set; } public Guid PermissionId { get; set; } public string OwnerModuleId { get; set; } = ""; public string ModuleEntitlementCode { get; set; } = ""; public string PermissionKey { get; set; } = "";
    public static PermissionOwnerDocument From(Guid id, PermissionCatalogManifestV1 m, Guid permissionId, string key) => new() { RegistrationId = id, PermissionId = permissionId, OwnerModuleId = m.OwnerModuleId, ModuleEntitlementCode = m.ModuleEntitlementCode, PermissionKey = key };
}
public sealed class RegistrationAuditDocument
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid RegistrationId { get; set; } public string OwnerModuleId { get; set; } = ""; public string ManifestVersion { get; set; } = ""; public string PayloadHash { get; set; } = ""; public string EventType { get; set; } = "permission-catalog-manifest-registered"; public DateTimeOffset RegisteredAtUtc { get; set; }
    public static RegistrationAuditDocument From(Guid id, PermissionCatalogManifestV1 m) => new() { RegistrationId = id, OwnerModuleId = m.OwnerModuleId, ManifestVersion = m.ManifestVersion, PayloadHash = m.CanonicalPayloadHash, RegisteredAtUtc = m.RegisteredAtUtc };
}
