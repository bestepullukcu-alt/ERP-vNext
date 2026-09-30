using System.Text.Json;
using Diten.AuthService.Application.Common.Entitlements;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

/// <summary>
/// The bounded operational command's only write owner. Existing storage only, session-bound atomic local
/// changes, and append-only receipts. Observed quiescence is not an all-writer exclusion guarantee.
/// </summary>
public sealed class EntitlementReconciliationOperationStore : IEntitlementReconciliationOperationStore
{
    public const string ApplicationName = "Diten.AuthService.EntitlementReconciliation";
    private const string Pending = "LOCAL_COMMITTED_AUTHORITY_REVALIDATION_PENDING";
    private const string Manual = "COMMITTED_MANUAL_RECONCILIATION_REQUIRED";
    private const string LocalEvent = "product_identity_entitlement_reconciliation_local_commit_recorded";
    private const string TerminalEvent = "product_identity_entitlement_reconciliation_authority_revalidated";
    private const string HoldEvent = "product_identity_entitlement_reconciliation_manual_hold_recorded";
    private static readonly string[] Collections =
    ["users", "roles", "permissions", "userRoles", "rolePermissions", "refreshTokens",
        "tenant_user_memberships", "auth_role_assignment_versions", "authAuditLogs"];
    private static readonly string[] Reasons =
    ["EXTERNAL_AUTHORITY_DRIFT_AFTER_COMMIT", "POST_COMMIT_AUTHORITY_UNAVAILABLE",
        "OPERATOR_AUTHORITY_DRIFT_AFTER_COMMIT", "LOCAL_CONCURRENCY_DRIFT_AFTER_COMMIT", "POST_COMMIT_FINALIZATION_MISSING"];
    private readonly IMongoDatabase _database;
    private readonly Dictionary<string, BsonBinaryData> _uuids = new(StringComparer.Ordinal);

    public EntitlementReconciliationOperationStore(IMongoDatabase database)
    {
        _database = database.WithReadPreference(ReadPreference.Primary).WithReadConcern(ReadConcern.Majority)
            .WithWriteConcern(WriteConcern.WMajority);
    }

    public async Task VerifyStorageAsync(CancellationToken ct)
    {
        _uuids.Clear();
        var hello = await _database.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: ct);
        if (!hello.GetValue("isWritablePrimary", false).ToBoolean()
            || string.IsNullOrWhiteSpace(hello.GetValue("setName", "").AsString)
            || !hello.Contains("logicalSessionTimeoutMinutes") || hello.GetValue("maxWireVersion", 0).ToInt32() < 17)
            throw Failure("EXISTING_REPLICA_PRIMARY_REQUIRED");
        var observed = new Dictionary<string, BsonBinaryData>(StringComparer.Ordinal);
        foreach (var name in Collections)
        {
            observed[name] = await CollectionUuidAsync(name, ct);
            await RequireIndexAsync(name, "_id_", new("_id", 1), null, null, null, ct);
        }
        // These are the existing source specifications, not a schema bootstrap or replacement index design.
        await RequireIndexAsync("users", "Email_1_TenantId_1", new() { { "Email", 1 }, { "TenantId", 1 } }, true, null, null, ct);
        await RequireIndexAsync("roles", "uq_roles_tenant_name_active", new() { { "TenantId", 1 }, { "Name", 1 } },
            true, new("IsDeleted", false), null, ct);
        await RequireIndexAsync("permissions", "Key_1", new("Key", 1), true, null, null, ct);
        await RequireIndexAsync("permissions", "Module_1_Resource_1_Action_1",
            new() { { "Module", 1 }, { "Resource", 1 }, { "Action", 1 } }, true, null, null, ct);
        await RequireIndexAsync("userRoles", "UserId_1_RoleId_1_TenantId_1",
            new() { { "UserId", 1 }, { "RoleId", 1 }, { "TenantId", 1 } }, true, null, null, ct);
        await RequireIndexAsync("userRoles", "UserId_1_TenantId_1", new() { { "UserId", 1 }, { "TenantId", 1 } }, false, null, null, ct);
        await RequireIndexAsync("rolePermissions", "RoleId_1_PermissionId_1_TenantId_1",
            new() { { "RoleId", 1 }, { "PermissionId", 1 }, { "TenantId", 1 } }, true, null, null, ct);
        await RequireIndexAsync("rolePermissions", "RoleId_1_TenantId_1", new() { { "RoleId", 1 }, { "TenantId", 1 } }, false, null, null, ct);
        await RequireIndexAsync("refreshTokens", "Token_1", new("Token", 1), true, null, null, ct);
        await RequireIndexAsync("refreshTokens", "UserId_1_TenantId_1", new() { { "UserId", 1 }, { "TenantId", 1 } }, false, null, null, ct);
        await RequireIndexAsync("refreshTokens", "ExpiresAt_1", new("ExpiresAt", 1), false, null, 0, ct);
        await RequireIndexAsync("authAuditLogs", "TenantId_1_OccurredAt_1", new() { { "TenantId", 1 }, { "OccurredAt", 1 } }, false, null, null, ct);
        await RequireIndexAsync("authAuditLogs", "EventName_1_OccurredAt_1", new() { { "EventName", 1 }, { "OccurredAt", 1 } }, false, null, null, ct);
        foreach (var item in observed)
        {
            if (!item.Value.Equals(await CollectionUuidAsync(item.Key, ct))) throw Failure("STORAGE_REPLACED");
            _uuids.Add(item.Key, item.Value);
        }
    }

    public async Task<EntitlementLocalSnapshot> ReadAsync(Guid tenantId, Guid operatorId, CancellationToken ct)
    {
        ValidateTarget(tenantId, operatorId);
        var quiet = await ObserveQuiescenceAsync(ct);
        using var session = await _database.Client.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction(TransactionOptions());
        try { return await SnapshotAsync(session, tenantId, operatorId, quiet, ct); }
        finally { await AbortSafelyAsync(session); }
    }

    public async Task<EntitlementOperationReceipt?> ReadReceiptAsync(Guid operationId, CancellationToken ct)
    {
        if (operationId == Guid.Empty) throw Failure("OPERATION_ID_REQUIRED");
        var receipts = await ReceiptsAsync(operationId, null, ct);
        return receipts.Hold?.Receipt ?? receipts.Terminal?.Receipt ?? receipts.Local?.Receipt;
    }

    public async Task<EntitlementLocalCommitResult> CommitAsync(EntitlementReconciliationPlan plan, CancellationToken ct)
    {
        var planHash = plan.Sha256();
        ValidateTarget(plan.TenantId, plan.ActorId);
        await VerifyStorageAsync(ct);
        var prior = await ReadReceiptAsync(plan.OperationId, ct);
        if (prior is not null)
        {
            if (prior.PlanSha256 != planHash) throw Failure("OPERATION_PLAN_CONFLICT");
            // CommitAsync is never an alternate terminal-success replay entry point.
            return new(prior.State, prior);
        }
        var quiet = await ObserveQuiescenceAsync(ct);
        if (quiet != plan.QuiescenceFingerprint) throw Failure("QUIESCENCE_DRIFT");
        using var session = await _database.Client.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction(TransactionOptions());
        var commitAttempted = false;
        try
        {
            var before = await SnapshotAsync(session, plan.TenantId, plan.ActorId, quiet, ct);
            ValidatePreconditions(plan, before);
            if ((await ReceiptsAsync(plan.OperationId, session, ct)).Local is not null)
                throw Failure("OPERATION_ALREADY_RECORDED");
            if (await ObserveQuiescenceAsync(ct) != quiet) throw Failure("QUIESCENCE_DRIFT");
            var now = DateTimeOffset.UtcNow;
            var versionResult = await Raw("auth_role_assignment_versions").UpdateOneAsync(session,
                new BsonDocument { { "_id", Uuid(plan.TenantId) }, { "Version", plan.ExpectedRoleAssignmentVersion } },
                new BsonDocument { { "$inc", new BsonDocument("Version", 1L) }, { "$set", new BsonDocument("UpdatedAt", Stamp(now)) } },
                new UpdateOptions { IsUpsert = false }, ct);
            if (!versionResult.IsAcknowledged || versionResult.ModifiedCount != 1) throw Failure("VERSION_CAS_CONFLICT");

            var additions = plan.Rows.Where(x => x.Action == "add").Select(row =>
            {
                var grant = new RolePermission(row.RoleId, row.PermissionId, plan.TenantId,
                    plan.ActorId.ToString("D"), GrantSource.Module, plan.ModuleCode)
                { Id = row.GrantId, CreatedAt = now, CreatedBy = plan.ActorId.ToString("D") };
                return grant.ToBsonDocument();
            }).ToArray();
            await InsertExistingAsync("rolePermissions", additions, session, ct);
            var removal = plan.Rows.Single(x => x.Action == "remove");
            var deleteResult = await Raw("rolePermissions").DeleteOneAsync(session, new BsonDocument
            {
                { "_id", Uuid(removal.GrantId) }, { "TenantId", Uuid(plan.TenantId) },
                { "RoleId", Uuid(removal.RoleId) }, { "PermissionId", Uuid(removal.PermissionId) },
                { "GrantSource", (int)GrantSource.Module }, { "SourceModuleCode", plan.ModuleCode }, { "IsDeleted", false }
            }, cancellationToken: ct);
            if (!deleteResult.IsAcknowledged || deleteResult.DeletedCount != 1) throw Failure("REMOVAL_CAS_CONFLICT");
            if (plan.ActiveRefreshTokenIds.Count > 0)
            {
                var revokeResult = await Raw("refreshTokens").UpdateManyAsync(session, new BsonDocument
                {
                    { "TenantId", Uuid(plan.TenantId) }, { "_id", new BsonDocument("$in", Ids(plan.ActiveRefreshTokenIds)) },
                    { "UserId", new BsonDocument("$in", Ids(plan.AffectedHolderIds)) },
                    { "IsDeleted", false }, { "RevokedAt", BsonNull.Value },
                    { "ExpiresAt", new BsonDocument("$gt", new BsonDateTime(now.UtcDateTime)) }
                }, new BsonDocument("$set", new BsonDocument("RevokedAt", new BsonDateTime(now.UtcDateTime))),
                    new UpdateOptions { IsUpsert = false }, ct);
                if (!revokeResult.IsAcknowledged || revokeResult.ModifiedCount != plan.ActiveRefreshTokenIds.Count)
                    throw Failure("TOKEN_SET_DRIFT");
            }

            // This snapshot includes our writes and the complete preserved/negative rows, not just the delta.
            var after = await SnapshotAsync(session, plan.TenantId, plan.ActorId, quiet, ct);
            if (after.RoleAssignmentVersion != checked(plan.ExpectedRoleAssignmentVersion + 1)
                || after.ActiveRefreshTokenIds.Count != 0 || after.Operator.Fingerprint != plan.OperatorFingerprint)
                throw Failure("TRANSACTION_POSTSTATE_INVALID");
            var receipt = new EntitlementOperationReceipt(plan.OperationId, planHash, Pending, null,
                after.Fingerprint, after.RoleAssignmentVersion, false);
            var audits = plan.Rows.Select((row, ordinal) => RowAudit(plan, row, ordinal, now)).ToList();
            audits.Add(ReceiptAudit(plan, receipt, LocalEvent, "receipt:local", now));
            await InsertExistingAsync("authAuditLogs", audits, session, ct);
            commitAttempted = true;
            await session.CommitTransactionAsync(ct); // One attempt only: never WithTransaction or retry loops.
            var persisted = await ReadReceiptAsync(plan.OperationId, ct);
            if (persisted != receipt) return new("MANUAL_RECONCILIATION_REQUIRED", null);
            return new(Pending, persisted);
        }
        catch (Exception) when (commitAttempted)
        {
            // Acknowledgement uncertainty is resolved with majority reads, never by another commit/mutation.
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                var receipt = await ReadReceiptAsync(plan.OperationId, budget.Token);
                var current = await ReadAsync(plan.TenantId, plan.ActorId, budget.Token);
                if (receipt is not null && receipt.PlanSha256 == planHash
                    && current.Fingerprint == receipt.PostStateFingerprint
                    && current.RoleAssignmentVersion == checked(plan.ExpectedRoleAssignmentVersion + 1))
                    return new(Pending, receipt);
                if (receipt is null && current.Fingerprint == plan.LocalFingerprint
                    && current.Operator.Fingerprint == plan.OperatorFingerprint
                    && current.QuiescenceFingerprint == plan.QuiescenceFingerprint)
                    return new("NOT_APPLIED", null);
            }
            catch { /* No proof is ambiguous, never success. */ }
            return new("MANUAL_RECONCILIATION_REQUIRED", null);
        }
        finally { if (!commitAttempted) await AbortSafelyAsync(session); }
    }

    public async Task<EntitlementOperationReceipt> FinalizeAsync(EntitlementReconciliationPlan plan,
        bool success, string? reason, string postStateFingerprint, CancellationToken ct)
    {
        var planHash = plan.Sha256();
        await VerifyStorageAsync(ct);
        var receipts = await ReceiptsAsync(plan.OperationId, null, ct);
        var local = receipts.Local?.Receipt ?? throw Failure("LOCAL_RECEIPT_REQUIRED");
        if (local.PlanSha256 != planHash) throw Failure("OPERATION_PLAN_CONFLICT");
        if (receipts.Hold is not null) return receipts.Hold.Receipt;
        if (receipts.Terminal?.Receipt.State == Manual) return receipts.Terminal.Receipt;
        try
        {
            var current = await ReadAsync(plan.TenantId, plan.ActorId, ct);
            if (!current.Operator.IsAuthorized || current.Operator.Fingerprint != plan.OperatorFingerprint)
            { success = false; reason ??= "OPERATOR_AUTHORITY_DRIFT_AFTER_COMMIT"; }
            if (current.Fingerprint != local.PostStateFingerprint || postStateFingerprint != local.PostStateFingerprint
                || current.RoleAssignmentVersion != checked(plan.ExpectedRoleAssignmentVersion + 1)
                || current.QuiescenceFingerprint != plan.QuiescenceFingerprint)
            { success = false; reason ??= "LOCAL_CONCURRENCY_DRIFT_AFTER_COMMIT"; }
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            success = false;
            reason ??= "POST_COMMIT_AUTHORITY_UNAVAILABLE";
        }
        if (!success && !Reasons.Contains(reason, StringComparer.Ordinal)) throw Failure("MANUAL_REASON_REQUIRED");
        if (success && reason is not null) throw Failure("SUCCESS_REASON_INVALID");
        if (success && receipts.Terminal?.Receipt.State == "SUCCESS") return receipts.Terminal.Receipt;
        var hold = receipts.Terminal?.Receipt.State == "SUCCESS";
        var terminal = new EntitlementOperationReceipt(plan.OperationId, planHash, success ? "SUCCESS" : Manual,
            reason, local.PostStateFingerprint, local.RoleAssignmentVersion, hold);
        var audit = ReceiptAudit(plan, terminal, hold ? HoldEvent : TerminalEvent,
            hold ? "receipt:hold" : "receipt:terminal", DateTimeOffset.UtcNow);
        try { await InsertExistingAsync("authAuditLogs", [audit], null, ct); }
        catch (Exception ex) when (ex is MongoException || ex is InvalidOperationException { Message: "WRITE_ACKNOWLEDGEMENT_UNCERTAIN" })
        {
            // Duplicate or ambiguous append: read the immutable winner. Never overwrite it.
            var winner = await ReadReceiptAsync(plan.OperationId, ct);
            if (winner is null || winner.PlanSha256 != planHash || winner.State == Pending) throw;
            if (!success && winner.State == "SUCCESS")
            {
                var manual = terminal with { State = Manual, ManualHold = true };
                await InsertExistingAsync("authAuditLogs",
                    [ReceiptAudit(plan, manual, HoldEvent, "receipt:hold", DateTimeOffset.UtcNow)], null, ct);
            }
        }
        var persisted = await ReadReceiptAsync(plan.OperationId, ct) ?? throw Failure("FINALIZATION_UNCERTAIN");
        if (persisted.PlanSha256 != planHash || persisted.State == Pending) throw Failure("FINALIZATION_UNCERTAIN");
        return persisted;
    }

    private async Task<EntitlementLocalSnapshot> SnapshotAsync(IClientSessionHandle session, Guid tenantId,
        Guid actorId, string quiet, CancellationToken ct)
    {
        var target = new BsonDocument("TenantId", Uuid(tenantId));
        var admin = new BsonDocument("TenantId", Uuid(EntitlementReconciliationPlan.AdminTenant));
        var permissions = await ReadRowsAsync("permissions", new(), session, ct);
        var roles = await ReadRowsAsync("roles", target, session, ct);
        var grants = await ReadRowsAsync("rolePermissions", target, session, ct);
        var users = await ReadRowsAsync("users", target, session, ct);
        var holders = await ReadRowsAsync("userRoles", target, session, ct);
        var tokens = await ReadRowsAsync("refreshTokens", target, session, ct);
        var memberships = await ReadRowsAsync("tenant_user_memberships", target, session, ct);
        var versions = await ReadRowsAsync("auth_role_assignment_versions", new("_id", Uuid(tenantId)), session, ct);
        if (versions.Count != 1 || !versions[0].TryGetValue("Version", out var versionValue)
            || !versionValue.IsInt64 || versionValue.AsInt64 < 0 || versionValue.AsInt64 == long.MaxValue)
            throw Failure("EXISTING_VERSION_ROW_REQUIRED");
        var version = versionValue.AsInt64;
        var actorRows = await ReadRowsAsync("users", new() { { "_id", Uuid(actorId) }, { "TenantId", Uuid(EntitlementReconciliationPlan.AdminTenant) } }, session, ct);
        var actorLinks = await ReadRowsAsync("userRoles", new() { { "UserId", Uuid(actorId) }, { "TenantId", Uuid(EntitlementReconciliationPlan.AdminTenant) } }, session, ct);
        var adminRoles = await ReadRowsAsync("roles", admin, session, ct);
        var adminGrants = await ReadRowsAsync("rolePermissions", admin, session, ct);
        var actor = Operator(actorId, actorRows, actorLinks, adminRoles, adminGrants, permissions);
        var typedRoles = roles.Select(x => BsonSerializer.Deserialize<Role>(x)).ToArray();
        var retirementRoles = typedRoles.Where(x => x.Name == ProductIdentityLifecycleEntitlementGrantProfile.RetirementStewardRole && !x.IsDeleted).ToArray();
        if (retirementRoles.Length != 1) throw Failure("RETIREMENT_ROLE_AMBIGUOUS");
        var affected = holders.Where(x => !Deleted(x) && x.GetValue("RoleId", BsonNull.Value).Equals(Uuid(retirementRoles[0].Id)))
            .Select(x => Id(x, "UserId")).Distinct().Order().ToArray();
        var now = DateTime.UtcNow;
        var active = tokens.Where(x => !Deleted(x) && affected.Contains(Id(x, "UserId"))
            && x.GetValue("RevokedAt", BsonNull.Value).IsBsonNull
            && x.GetValue("ExpiresAt", BsonNull.Value).IsBsonDateTime && x["ExpiresAt"].ToUniversalTime() > now)
            .Select(x => Id(x)).Order().ToArray();
        var state = new BsonDocument
        {
            { "permissions", Ordered(permissions) }, { "roles", Ordered(roles) }, { "grants", Ordered(grants) },
            { "users", Ordered(users) }, { "userRoles", Ordered(holders) }, { "tokens", Ordered(tokens) },
            { "memberships", Ordered(memberships) }, { "version", Ordered(versions) },
            { "affectedHolders", Ids(affected) }, { "activeTokens", Ids(active) }
        };
        // Secret-bearing documents are hashed in memory only; no raw rows/hash values enter artifacts or audits.
        var fingerprint = Digest(state);
        return new(tenantId, version, permissions.Select(x => BsonSerializer.Deserialize<Permission>(x)).ToArray(),
            typedRoles, grants.Select(x => BsonSerializer.Deserialize<RolePermission>(x)).ToArray(), affected, active, actor, fingerprint, quiet);
    }

    private static EntitlementOperatorSnapshot Operator(Guid actorId, List<BsonDocument> actors,
        List<BsonDocument> links, List<BsonDocument> roles, List<BsonDocument> grants, List<BsonDocument> permissions)
    {
        var user = actors.Count == 1 ? BsonSerializer.Deserialize<User>(actors[0]) : null;
        var roleIds = links.Where(x => !Deleted(x)).Select(x => Id(x, "RoleId")).ToHashSet();
        var actorRoles = roles.Where(x => roleIds.Contains(Id(x))).ToList();
        var activeRoles = actorRoles.Where(x => !Deleted(x)).Select(x => Id(x)).ToHashSet();
        var actorGrants = grants.Where(x => roleIds.Contains(Id(x, "RoleId"))).ToList();
        var permissionIds = actorGrants.Select(x => Id(x, "PermissionId")).ToHashSet();
        var actorPermissions = permissions.Where(x => permissionIds.Contains(Id(x))
            || x.GetValue("Key", "") == EntitlementReconciliationPlan.RequiredPermission).ToList();
        var required = actorPermissions.Where(x => x.GetValue("Key", "") == EntitlementReconciliationPlan.RequiredPermission).ToArray();
        var entitled = required.Length == 1 && !Deleted(required[0])
            && required[0].GetValue("Scope", -1).ToInt32() == (int)PermissionScope.PlatformAdmin
            && actorGrants.Any(x => !Deleted(x) && activeRoles.Contains(Id(x, "RoleId"))
                && Id(x, "PermissionId") == Id(required[0]));
        var authorized = user is not null && !user.IsDeleted && user.IsActive && user.EmailConfirmed
            && !user.MustChangePassword && user.PlatformActorType == "platform_admin" && entitled
            && (user.LockoutEnd is null || user.LockoutEnd <= DateTime.UtcNow)
            && !string.IsNullOrWhiteSpace(user.Email);
        return new(actorId, EntitlementReconciliationPlan.AdminTenant, user?.Email.Trim().ToLowerInvariant() ?? "",
            authorized, Digest(new BsonDocument
            {
                { "user", Ordered(actors) }, { "userRoles", Ordered(links) }, { "roles", Ordered(actorRoles) },
                { "grants", Ordered(actorGrants) }, { "permissions", Ordered(actorPermissions) }
            }));
    }

    private static void ValidatePreconditions(EntitlementReconciliationPlan plan, EntitlementLocalSnapshot before)
    {
        if (!before.Operator.IsAuthorized || before.Operator.UserId != plan.ActorId
            || before.Operator.Fingerprint != plan.OperatorFingerprint || before.Fingerprint != plan.LocalFingerprint
            || before.QuiescenceFingerprint != plan.QuiescenceFingerprint
            || before.RoleAssignmentVersion != plan.ExpectedRoleAssignmentVersion
            || !before.AffectedHolderIds.SequenceEqual(plan.AffectedHolderIds)
            || !before.ActiveRefreshTokenIds.SequenceEqual(plan.ActiveRefreshTokenIds)) throw Failure("PLAN_PRECONDITION_DRIFT");
        var exactAdds = new (string Role, string Key)[]
        {
            (ProductIdentityLifecycleEntitlementGrantProfile.StewardRole, ProductIdentityLifecycleEntitlementGrantProfile.GskusRequestCorrection),
            (ProductIdentityLifecycleEntitlementGrantProfile.StewardRole, ProductIdentityLifecycleEntitlementGrantProfile.GskusUpdate),
            (ProductIdentityLifecycleEntitlementGrantProfile.StewardRole, ProductIdentityLifecycleEntitlementGrantProfile.GskusWithdraw),
            (ProductIdentityLifecycleEntitlementGrantProfile.StewardRole, ProductIdentityLifecycleEntitlementGrantProfile.LskusWithdraw),
            (ProductIdentityLifecycleEntitlementGrantProfile.RetirementStewardRole, ProductIdentityLifecycleEntitlementGrantProfile.GskusRequestRetirement),
            (ProductIdentityLifecycleEntitlementGrantProfile.RetirementStewardRole, ProductIdentityLifecycleEntitlementGrantProfile.LskusRequestRetirement)
        };
        if (!plan.Rows.Where(x => x.Action == "add").Select(x => (x.RoleName, x.PermissionKey)).ToHashSet().SetEquals(exactAdds))
            throw Failure("EXACT_DELTA_REQUIRED");
        foreach (var row in plan.Rows)
        {
            var role = before.Roles.SingleOrDefault(x => x.Id == row.RoleId && !x.IsDeleted);
            var permission = before.Permissions.SingleOrDefault(x => x.Id == row.PermissionId && !x.IsDeleted);
            if (role is null || !role.IsSystem || role.Name != row.RoleName || role.TenantId != plan.TenantId
                || permission is null || permission.Key != row.PermissionKey || permission.Module != row.PermissionModule
                || permission.Scope != row.PermissionScope || row.PermissionScope != PermissionScope.Tenant)
                throw Failure("ROW_IDENTITY_DRIFT");
            if (row.Action == "add")
            {
                if (row.GrantId != EntitlementReconciliationPlan.DeterministicId(plan.OperationId, "grant:" + row.RoleName + ":" + row.PermissionKey)
                    || before.Grants.Any(x => x.Id == row.GrantId || (x.RoleId == row.RoleId && x.PermissionId == row.PermissionId)))
                    throw Failure("ADD_ALREADY_PRESENT_OR_TOMBSTONED");
            }
            else
            {
                var grant = before.Grants.SingleOrDefault(x => x.Id == row.GrantId);
                if (row.GrantId != EntitlementReconciliationPlan.RemovedGrant || row.PermissionKey != ProductIdentityLifecycleEntitlementGrantProfile.LskusRetire
                    || row.RoleName != ProductIdentityLifecycleEntitlementGrantProfile.RetirementStewardRole
                    || grant is null || grant.IsDeleted || grant.TenantId != plan.TenantId || grant.RoleId != row.RoleId
                    || grant.PermissionId != row.PermissionId || grant.GrantSource != GrantSource.Module || grant.SourceModuleCode != plan.ModuleCode)
                    throw Failure("EXACT_REMOVAL_PRECONDITION_FAILED");
            }
        }
    }

    private sealed record ReceiptEnvelope(Guid OperationId, EntitlementOperationReceipt Receipt, Guid TenantId,
        Guid ActorId, string ActorType, string ModuleCode, string LocalFingerprint, string AuthorityFingerprint,
        string OperatorFingerprint, string QuiescenceFingerprint, long VersionBefore, long VersionAfter,
        int AddCount, int RemoveCount, string ProvenanceSha256, string BinarySha256);
    private sealed record ReceiptSet(ReceiptEnvelope? Local, ReceiptEnvelope? Terminal, ReceiptEnvelope? Hold);

    private async Task<ReceiptSet> ReceiptsAsync(Guid operationId, IClientSessionHandle? session, CancellationToken ct)
    {
        var known = new Dictionary<Guid, string>
        {
            [EntitlementReconciliationPlan.DeterministicId(operationId, "receipt:local")] = LocalEvent,
            [EntitlementReconciliationPlan.DeterministicId(operationId, "receipt:terminal")] = TerminalEvent,
            [EntitlementReconciliationPlan.DeterministicId(operationId, "receipt:hold")] = HoldEvent
        };
        var filter = new BsonDocument("$or", new BsonArray
        {
            new BsonDocument("_id", new BsonDocument("$in", Ids(known.Keys))),
            new BsonDocument { { "TenantId", Uuid(EntitlementReconciliationPlan.TargetTenant) },
                { "EventName", new BsonDocument("$in", new BsonArray { LocalEvent, TerminalEvent, HoldEvent }) } }
        });
        var rows = await ReadRowsAsync("authAuditLogs", filter, session, ct);
        var found = new Dictionary<string, ReceiptEnvelope>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            ReceiptEnvelope envelope;
            try { envelope = JsonSerializer.Deserialize<ReceiptEnvelope>(row["Metadata"].AsString) ?? throw Failure("RECEIPT_INVALID"); }
            catch (Exception) { throw Failure("RECEIPT_INVALID"); }
            if (envelope.OperationId != operationId && !known.ContainsKey(Id(row))) continue;
            var name = row.GetValue("EventName", "").AsString;
            if (Deleted(row) || !known.TryGetValue(Id(row), out var expectedName) || name != expectedName
                || envelope.OperationId != operationId || envelope.Receipt.OperationId != operationId
                || envelope.TenantId != EntitlementReconciliationPlan.TargetTenant
                || row.GetValue("TenantId", BsonNull.Value) != Uuid(envelope.TenantId)
                || row.GetValue("UserId", BsonNull.Value) != Uuid(envelope.ActorId)
                || envelope.ActorId == Guid.Empty || envelope.ActorType != "PlatformAdministrator"
                || envelope.ModuleCode != EntitlementReconciliationPlan.Module || envelope.AddCount != 6 || envelope.RemoveCount != 1
                || envelope.VersionBefore < 0 || envelope.VersionBefore == long.MaxValue
                || envelope.VersionAfter != envelope.VersionBefore + 1
                || envelope.Receipt.RoleAssignmentVersion != envelope.VersionAfter
                || !HashText(envelope.Receipt.PlanSha256) || !HashText(envelope.Receipt.PostStateFingerprint)
                || !found.TryAdd(name, envelope)) throw Failure("RECEIPT_CONFLICT_OR_TOMBSTONE");
            var receipt = envelope.Receipt;
            if ((name == LocalEvent && (receipt.State != Pending || receipt.Reason is not null || receipt.ManualHold))
                || (name == TerminalEvent && (receipt.State != "SUCCESS" && receipt.State != Manual || receipt.ManualHold))
                || (name == HoldEvent && (receipt.State != Manual || !receipt.ManualHold))
                || (receipt.State == "SUCCESS" && receipt.Reason is not null)
                || (receipt.State == Manual && !Reasons.Contains(receipt.Reason, StringComparer.Ordinal)))
                throw Failure("RECEIPT_STATE_INVALID");
        }
        found.TryGetValue(LocalEvent, out var local);
        found.TryGetValue(TerminalEvent, out var terminal);
        found.TryGetValue(HoldEvent, out var hold);
        if (local is null && found.Count != 0 || hold is not null && terminal?.Receipt.State != "SUCCESS") throw Failure("RECEIPT_CHAIN_INVALID");
        if (local is not null && found.Values.Any(x => x with { Receipt = local.Receipt } != local
            || x.Receipt.PlanSha256 != local.Receipt.PlanSha256 || x.Receipt.PostStateFingerprint != local.Receipt.PostStateFingerprint))
            throw Failure("RECEIPT_BINDING_CONFLICT");
        return new(local, terminal, hold);
    }

    private static BsonDocument ReceiptAudit(EntitlementReconciliationPlan plan, EntitlementOperationReceipt receipt,
        string eventName, string discriminator, DateTimeOffset now) => Audit(plan, eventName, discriminator,
        JsonSerializer.Serialize(new ReceiptEnvelope(plan.OperationId, receipt, plan.TenantId, plan.ActorId, plan.ActorType,
            plan.ModuleCode, plan.LocalFingerprint, plan.AuthorityFingerprint, plan.OperatorFingerprint, plan.QuiescenceFingerprint,
            plan.ExpectedRoleAssignmentVersion, checked(plan.ExpectedRoleAssignmentVersion + 1), 6, 1,
            plan.ProvenanceSha256, plan.BinarySha256)), now);

    private static BsonDocument RowAudit(EntitlementReconciliationPlan plan, EntitlementReconciliationRow row, int ordinal, DateTimeOffset now) =>
        Audit(plan, row.Action == "add" ? "role_permission_granted" : "role_permission_revoked", $"row:{ordinal}:{row.Action}",
            JsonSerializer.Serialize(new
            {
                plan.OperationId, CorrelationId = plan.OperationId, PlanSha256 = plan.Sha256(), plan.TenantId,
                plan.ActorId, plan.ActorType, row.RoleId, row.RoleName, row.PermissionId, row.PermissionKey,
                row.GrantId, row.GrantSource, row.SourceModuleCode, Before = row.Action == "remove" ? "present" : "absent",
                After = row.Action == "remove" ? "absent" : "present"
            }), now);

    private static BsonDocument Audit(EntitlementReconciliationPlan plan, string name, string discriminator, string metadata, DateTimeOffset now)
    {
        var document = new AuthAuditLog(name, plan.ActorId, plan.TenantId, metadata)
        { Id = EntitlementReconciliationPlan.DeterministicId(plan.OperationId, discriminator), CreatedAt = now,
            CreatedBy = plan.ActorId.ToString("D") }.ToBsonDocument();
        document["OccurredAt"] = Stamp(now);
        return document;
    }

    private async Task InsertExistingAsync(string collection, IEnumerable<BsonDocument> documents,
        IClientSessionHandle? session, CancellationToken ct)
    {
        if (!_uuids.TryGetValue(collection, out var uuid)) throw Failure("STORAGE_PREFLIGHT_REQUIRED");
        var batch = new BsonArray(documents);
        var command = new BsonDocument { { "insert", collection }, { "collectionUUID", uuid }, { "documents", batch }, { "ordered", true } };
        if (session is null) command.Add("writeConcern", new BsonDocument("w", "majority"));
        var reply = session is null
            ? await _database.RunCommandAsync<BsonDocument>(command, cancellationToken: ct)
            : await _database.RunCommandAsync<BsonDocument>(session, command, cancellationToken: ct);
        if (reply.Contains("writeConcernError") || reply.GetValue("writeErrors", new BsonArray()).AsBsonArray.Count != 0
            || reply.GetValue("n", 0).ToInt32() != batch.Count) throw Failure("WRITE_ACKNOWLEDGEMENT_UNCERTAIN");
    }

    private async Task<string> ObserveQuiescenceAsync(CancellationToken ct)
    {
        if (_database.Client.Settings.ApplicationName != ApplicationName) throw Failure("OPERATIONAL_CLIENT_IDENTITY_REQUIRED");
        // $currentOp is an instantaneous server observation and supports only local read concern.
        // Do not change the majority data/receipt handle or snapshot transaction read concern.
        var admin = _database.Client.GetDatabase("admin").WithReadConcern(ReadConcern.Local);
        var pipeline = new[]
        {
            new BsonDocument("$currentOp", new BsonDocument
                { { "allUsers", true }, { "idleConnections", true }, { "idleSessions", true }, { "localOps", true } }),
            // MongoDB 7 may repeat this unused top-level field for idle transactions. Exclude it server-side
            // before BSON deserialization; retain client, command, transaction, namespace and operation facts.
            new BsonDocument("$project", new BsonDocument("waitingForLock", 0))
        };
        using var cursor = await admin.AggregateAsync<BsonDocument>(pipeline, cancellationToken: ct);
        var rows = await cursor.ToListAsync(ct);
        var observations = new List<string>();
        foreach (var row in rows)
        {
            var metadata = row.GetValue("clientMetadata", new BsonDocument()).AsBsonDocument;
            var app = metadata.GetValue("application", new BsonDocument()).AsBsonDocument.GetValue("name", "").AsString;
            if (app == ApplicationName || !row.Contains("client")) continue;
            var command = row.GetValue("command", new BsonDocument());
            var transaction = row.GetValue("transaction", new BsonDocument());
            var ns = row.GetValue("ns", "").AsString;
            var attributable = ns.StartsWith(_database.DatabaseNamespace.DatabaseName + ".", StringComparison.Ordinal)
                || TargetsDatabase(command) || TargetsDatabase(transaction);
            var mutating = new[] { "insert", "update", "remove" }.Contains(row.GetValue("op", "").AsString, StringComparer.Ordinal)
                || ContainsMutation(command) || ContainsMutation(transaction);
            var explicitDatabase = FindDatabase(command) ?? FindDatabase(transaction);
            if (attributable || mutating && explicitDatabase is null) throw Failure("UNKNOWN_AUTH_WRITER_OBSERVED");
            var driver = metadata.GetValue("driver", new BsonDocument()).AsBsonDocument.GetValue("name", "").AsString;
            // Do not infer an Auth database from an idle/unattributable connection. Count the observable set
            // (including shared Platform clients) without storing commands, endpoints, usernames or tokens.
            observations.Add(EntitlementReconciliationPlan.Hash(app + "\n" + driver + "\n" + (explicitDatabase ?? "unattributed")));
        }
        return EntitlementReconciliationPlan.Hash(string.Join("\n", observations.Order(StringComparer.Ordinal)));
    }

    private bool TargetsDatabase(BsonValue value)
    {
        if (value is BsonDocument document)
            return document.Any(e => e.Name == "$db" && e.Value == _database.DatabaseNamespace.DatabaseName
                || e.Value.IsString && e.Value.AsString.StartsWith(_database.DatabaseNamespace.DatabaseName + ".", StringComparison.Ordinal)
                || TargetsDatabase(e.Value));
        return value is BsonArray array && array.Any(TargetsDatabase);
    }
    private static string? FindDatabase(BsonValue value)
    {
        if (value is BsonDocument document)
        {
            if (document.TryGetValue("$db", out var db) && db.IsString) return db.AsString;
            return document.Select(e => FindDatabase(e.Value)).FirstOrDefault(x => x is not null);
        }
        return value is BsonArray array ? array.Select(FindDatabase).FirstOrDefault(x => x is not null) : null;
    }
    private static bool ContainsMutation(BsonValue value)
    {
        if (value is BsonDocument document)
            return document.Any(e => new[] { "insert", "update", "delete", "findAndModify", "bulkWrite", "applyOps", "create", "drop", "dropDatabase", "renameCollection", "createIndexes", "dropIndexes", "collMod", "$merge", "$out" }.Contains(e.Name, StringComparer.Ordinal)
                || ContainsMutation(e.Value));
        return value is BsonArray array && array.Any(ContainsMutation);
    }

    private async Task<BsonBinaryData> CollectionUuidAsync(string name, CancellationToken ct)
    {
        using var cursor = await _database.ListCollectionsAsync(new ListCollectionsOptions { Filter = new BsonDocument("name", name) }, ct);
        var rows = await cursor.ToListAsync(ct);
        if (rows.Count != 1 || rows[0].GetValue("type", "") != "collection"
            || rows[0].GetValue("info", new BsonDocument()).AsBsonDocument.GetValue("uuid", BsonNull.Value) is not BsonBinaryData uuid
            || uuid.SubType != BsonBinarySubType.UuidStandard) throw Failure("EXISTING_COLLECTION_REQUIRED");
        return uuid;
    }
    private async Task RequireIndexAsync(string name, string indexName, BsonDocument keys,
        bool? unique, BsonDocument? partial, long? ttl, CancellationToken ct)
    {
        using var cursor = await Raw(name).Indexes.ListAsync(ct);
        var matches = (await cursor.ToListAsync(ct)).Where(x => x.GetValue("name", "") == indexName).ToArray();
        if (matches.Length != 1) throw Failure("EXISTING_INDEX_REQUIRED");
        var index = matches[0];
        if (!index.GetValue("key", BsonNull.Value).Equals(keys)
            || unique.HasValue && index.GetValue("unique", false).ToBoolean() != unique.Value
            || index.GetValue("sparse", false).ToBoolean() || index.GetValue("hidden", false).ToBoolean() || index.Contains("collation")
            || (partial is null ? index.Contains("partialFilterExpression") : !index.GetValue("partialFilterExpression", BsonNull.Value).Equals(partial))
            || (ttl is null ? index.Contains("expireAfterSeconds") : !index.Contains("expireAfterSeconds") || index["expireAfterSeconds"].ToInt64() != ttl))
            throw Failure("EXISTING_INDEX_MISMATCH");
    }
    private async Task<List<BsonDocument>> ReadRowsAsync(string name, BsonDocument filter, IClientSessionHandle? session, CancellationToken ct)
        => session is null ? await Raw(name).Find(filter).ToListAsync(ct) : await Raw(name).Find(session, filter).ToListAsync(ct);
    private IMongoCollection<BsonDocument> Raw(string name) => _database.GetCollection<BsonDocument>(name);
    private static TransactionOptions TransactionOptions() => new(ReadConcern.Snapshot, ReadPreference.Primary, WriteConcern.WMajority,
        maxCommitTime: TimeSpan.FromSeconds(15));
    private static async Task AbortSafelyAsync(IClientSessionHandle session)
    {
        if (!session.IsInTransaction) return;
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await session.AbortTransactionAsync(budget.Token); } catch { /* Original error remains authoritative. */ }
    }
    private static void ValidateTarget(Guid tenant, Guid actor)
    {
        if (tenant != EntitlementReconciliationPlan.TargetTenant || actor == Guid.Empty) throw Failure("TARGET_INVALID");
    }
    private static BsonBinaryData Uuid(Guid value) => new(value, GuidRepresentation.Standard);
    private static BsonArray Ids(IEnumerable<Guid> values) => new(values.Select(x => (BsonValue)Uuid(x)));
    private static Guid Id(BsonDocument row, string field = "_id") => row[field].AsBsonBinaryData.ToGuid(GuidRepresentation.Standard);
    private static bool Deleted(BsonDocument row) => !row.TryGetValue("IsDeleted", out var deleted) || !deleted.IsBoolean || deleted.AsBoolean;
    private static BsonArray Stamp(DateTimeOffset value) => new() { value.Ticks, (int)value.Offset.TotalMinutes };
    private static BsonArray Ordered(IEnumerable<BsonDocument> rows) => new(rows.OrderBy(x => x["_id"].ToJson(), StringComparer.Ordinal).Select(Canonical));
    private static BsonValue Canonical(BsonValue value) => value switch
    {
        BsonDocument document => new BsonDocument(document.Elements.OrderBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => new BsonElement(x.Name, Canonical(x.Value)))),
        BsonArray array => new BsonArray(array.Select(Canonical)),
        _ => value
    };
    private static string Digest(BsonDocument document) => EntitlementReconciliationPlan.Hash(
        Canonical(document).ToJson(new JsonWriterSettings { OutputMode = JsonOutputMode.CanonicalExtendedJson }));
    private static bool HashText(string value) => value.Length == 64 && value.All(Uri.IsHexDigit);
    private static InvalidOperationException Failure(string code) => new(code);
}
