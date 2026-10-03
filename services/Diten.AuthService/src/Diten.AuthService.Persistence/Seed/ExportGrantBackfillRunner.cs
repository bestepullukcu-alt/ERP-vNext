using System.Text.Json;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Seed;

/// <summary>
/// The Mongo side of <see cref="ExportGrantBackfill"/>, run on every start.
///
/// <para>⚠ AN AUTHORITY WRITER. Every grant it makes leaves a <c>role_permission_granted</c> row in
/// <c>authAuditLogs</c> with the system actor — a permission nobody clicked must still answer "who gave this?".</para>
///
/// <para><b>Reads are bounded by what is left to do.</b> The marks are read first. Then ONE indexed
/// <c>distinct</c> asks the roles collection for the tenants that are not yet settled for every key; when there is
/// none — every start after the first — no role and no grant document is read at all. Otherwise only the unsettled
/// tenants' roles are read (the fields the decision needs) and only their grants of the read / export permissions.</para>
///
/// <para><b>One tenant's failure is that tenant's.</b> Each tenant is settled on its own: an error (a document that
/// does not deserialize, a write that fails) is logged with the tenant id, the key and the exception TYPE — never the
/// message or a document — the tenant stays unmarked and is tried again on the next start, and every other tenant and
/// the other key go on. Cancellation is not an error and stops the run.</para>
///
/// <para><b>The audit row is the decision record: whoever writes it, and only they, writes the grant.</b> The audit
/// row of a (tenant, role, key) has a deterministic id and is written BEFORE the grant. A run that cannot write it —
/// the id is already there — does NOT write the grant: an earlier run, or an instance starting at the same time, has
/// already decided this role. That is what makes a revoked grant stay revoked. A revoke is a hard delete, so "the
/// grant was never written" and "it was written and an administrator took it away" look the same in the database;
/// without a transaction (the deployment's Mongo topology is not something this code may assume) one of the two must
/// be chosen, and the choice is: NEVER grant twice.</para>
///
/// <para>⚠ THE PRICE, taken on purpose (CT decision, WP-ROLES-CLOSE-01 FIX2): a run that stops between a role's audit
/// row and its grant leaves that role WITHOUT export for good — the next start sees the audit row and does not repeat
/// the grant. It says so (a Warning with the tenant, the role and the key, and a count in the run's summary) and an
/// administrator gives the permission on the Role Permissions screen. No "missing" audit row is written for it: the
/// database cannot tell an interrupted run from an administrator's revoke, and the second must not be recorded as a
/// system event. In return no start, and no overlapping instance, ever hands back what was taken away.</para>
///
/// <para>Grants are unique per (role, permission, tenant) and marks have a deterministic id: a duplicate key there
/// means "already done" and never fails the tenant. The role-assignment version is bumped as soon as the tenant's
/// grants are written, before its mark; the mark is written once every role of the tenant has been through the rule,
/// whether its grant was written or not repeated — so a settled tenant is not scanned again.</para>
/// </summary>
public static class ExportGrantBackfillRunner
{
    private const string SystemUser = "system";

    /// <summary>Where a run is, for the fault-injection tests (a hook that throws is a failure at that point).</summary>
    public enum Stage
    {
        /// <summary>The audit row of this grant is written, the grant is not.</summary>
        AuditWritten,
        /// <summary>The grant is written.</summary>
        GrantWritten,
        /// <summary>All grants of this tenant are written, its version is not bumped and it is not marked.</summary>
        TenantGrantsWritten
    }

    /// <summary>What one run did. <paramref name="FailedTenants"/> stay unmarked and are retried on the next start.</summary>
    /// <param name="GrantsNotRepeated">Roles whose audit row already existed, so their grant was NOT written again
    /// (an interrupted earlier run, or an administrator's revoke — the database cannot tell which).</param>
    public sealed record Result(
        int GrantsWritten, int TenantsMarkedBorn, int TenantsMarkedBackfilled, int SourcesCorrected,
        IReadOnlyList<Guid> FailedTenants, IReadOnlyList<(Guid TenantId, Guid RoleId, string ExportKey)> GrantsNotRepeated)
    {
        public static readonly Result Nothing = new(0, 0, 0, 0, [], []);
    }

    public static async Task<Result> RunAsync(
        IMongoDatabase database,
        ILogger logger,
        Func<Stage, ExportGrantBackfill.KeyPair, Guid, Task>? hook = null,
        CancellationToken ct = default)
    {
        var permissions = await LoadPermissionsAsync(database, ct);
        var pairs = ExportGrantBackfill.Keys
            .Where(k => permissions.ContainsKey(k.ReadKey) && permissions.ContainsKey(k.ExportKey))
            .ToList(); // a key that is not in the catalog (yet): nothing to decide, nothing to mark
        if (pairs.Count == 0) return Result.Nothing;

        var markCol = database.GetCollection<PermissionReconciliationMark>(PermissionReconciliationMark.CollectionName);
        var exportKeys = pairs.Select(p => p.ExportKey).ToList();
        var marks = await markCol.Find(m => exportKeys.Contains(m.Key)).ToListAsync(ct);

        var failed = new HashSet<Guid>();
        var corrected = await CorrectLegacySourcesAsync(database, logger, pairs, permissions, marks, failed, ct);

        // Settled for EVERY key → never read again. One indexed distinct finds whoever is not.
        var settled = marks.GroupBy(m => m.TenantId)
            .Where(g => exportKeys.All(k => g.Any(m => m.Key == k)))
            .Select(g => g.Key)
            .Append(Guid.Empty) // a role without a tenant is never processed and never marked
            .ToList();
        var roleCol = database.GetCollection<Role>("roles");
        var unsettled = await (await roleCol.DistinctAsync(r => r.TenantId, Builders<Role>.Filter.Nin(r => r.TenantId, settled), cancellationToken: ct)).ToListAsync(ct);
        if (unsettled.Count == 0) return Result.Nothing with { SourcesCorrected = corrected, FailedTenants = failed.ToList() };


        // Deleted roles are read too: they receive nothing, but the oldest one dates the tenant.
        // (A role document that stores no CreatedAt reads as the moment it was loaded — newer than any key — so it can
        // only ever make its tenant look younger, never older: no separate check is needed on this side.)
        var roles = (await ReadRolesAsync(roleCol, unsettled, ct))
            .Select(r => new ExportGrantBackfill.RoleState(r.Id, r.Name, r.TenantId, r.IsSystem, r.CreatedAt, r.IsDeleted))
            .ToList();
        var permissionIds = pairs.SelectMany(p => new[] { permissions[p.ReadKey].Id, permissions[p.ExportKey].Id }).ToList();
        var rpCol = database.GetCollection<RolePermission>("rolePermissions");
        // A revoked grant is a hard delete, so "live" is the only state a grant document has; IsDeleted is honoured anyway.
        var grants = (await rpCol
                .Find(rp => unsettled.Contains(rp.TenantId) && permissionIds.Contains(rp.PermissionId) && rp.IsDeleted == false)
                .Project<RolePermission>(Builders<RolePermission>.Projection.Include(rp => rp.RoleId).Include(rp => rp.PermissionId))
                .ToListAsync(ct))
            .Select(rp => (rp.RoleId, rp.PermissionId))
            .ToHashSet();

        var datedKeys = await StoredCreatedAtAsync(database, "permissions", "Key", exportKeys, ct);
        var auditCol = database.GetCollection<AuthAuditLog>("authAuditLogs");
        var versions = new RoleAssignmentVersionRepository(database);
        int written = 0, born = 0, backfilled = 0;
        var notRepeated = new List<(Guid TenantId, Guid RoleId, string ExportKey)>();

        foreach (var pair in pairs)
        {
            var export = permissions[pair.ExportKey];
            var marked = marks.Where(m => m.Key == pair.ExportKey).Select(m => m.TenantId).ToHashSet();
            // The key's age must be a STORED value. An entity read from a document without CreatedAt carries the
            // property's initializer (the moment it was read), which would make every tenant look older than the key.
            var keyCreatedAt = datedKeys.Contains(pair.ExportKey) ? export.CreatedAt : default;
            var plan = ExportGrantBackfill.Plan(roles, grants, permissions[pair.ReadKey].Id, export.Id, keyCreatedAt, marked,
                role => DefaultRolePermissionTemplate.SelectFor(role.Name, [export]).Count > 0);

            foreach (var tenant in plan)
            {
                try
                {
                    if (tenant.Origin == ExportGrantBackfill.OriginBorn)
                    {
                        await WriteMarkAsync(markCol, tenant.TenantId, pair.ExportKey, ExportGrantBackfill.OriginBorn, ct);
                        born++;
                        continue;
                    }

                    foreach (var grant in tenant.Grants)
                    {
                        // The decision record. Not ours to write → not ours to grant (see the type's remarks).
                        if (!await InsertOnceAsync(auditCol, GrantAuditRow(pair, grant), ct))
                        {
                            notRepeated.Add((grant.TenantId, grant.RoleId, pair.ExportKey));
                            logger.LogWarning(
                                "Export grant not repeated: tenant {TenantId}, role {RoleId}, key {ExportKey} already has its audit row and holds no grant "
                                + "(an earlier run was interrupted, or the grant was revoked). The role is left without it; grant it on the Role Permissions screen if it is wanted.",
                                grant.TenantId, grant.RoleId, pair.ExportKey);
                            continue;
                        }

                        if (hook is not null) await hook(Stage.AuditWritten, pair, grant.RoleId);

                        var row = grant.Source == GrantSource.System
                            ? RolePermission.SystemGrant(grant.RoleId, grant.PermissionId, grant.TenantId, SystemUser)
                            : RolePermission.ManualGrant(grant.RoleId, grant.PermissionId, grant.TenantId, SystemUser);
                        if (await InsertOnceAsync(rpCol, row, ct)) written++;
                        grants.Add((grant.RoleId, grant.PermissionId));
                        if (hook is not null) await hook(Stage.GrantWritten, pair, grant.RoleId);
                    }

                    if (hook is not null) await hook(Stage.TenantGrantsWritten, pair, tenant.TenantId);

                    // As soon as the tenant's grants are written — not at the end of the run: cached authorization
                    // snapshots of THIS tenant are invalidated (FU13) even if a later tenant fails. Always, also when
                    // this run wrote none: an earlier, interrupted run may have written them and never got here.
                    await versions.IncrementAsync(tenant.TenantId, ct);

                    // Marked last: a tenant that failed above stays unmarked and the next start finishes it.
                    await WriteMarkAsync(markCol, tenant.TenantId, pair.ExportKey, ExportGrantBackfill.OriginBackfilled, ct);
                    backfilled++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed.Add(tenant.TenantId);
                    LogTenantFailure(logger, "backfill", tenant.TenantId, pair.ExportKey, ex);
                }
            }
        }

        return new Result(written, born, backfilled, corrected, failed.ToList(), notRepeated);
    }

    /// <summary>
    /// The one-time SOURCE correction of what an earlier build's backfill wrote. That build filed every backfilled
    /// export grant as <see cref="GrantSource.System"/> — also on Viewer and on custom roles, where nothing
    /// re-provisions it — so the Role Permissions screen answers 409 and an administrator can never take it away. Its
    /// marks carry no <c>Origin</c>; those tenants are visited once: each such grant becomes Manual and leaves one
    /// audit row (deterministic id), and the mark gets its Origin. ⚠ It GRANTS NOTHING AND REMOVES NOTHING — the set of
    /// permissions every role holds is the same before and after.
    /// </summary>
    private static async Task<int> CorrectLegacySourcesAsync(
        IMongoDatabase database,
        ILogger logger,
        IReadOnlyList<ExportGrantBackfill.KeyPair> pairs,
        IReadOnlyDictionary<string, Permission> permissions,
        IReadOnlyList<PermissionReconciliationMark> marks,
        HashSet<Guid> failed,
        CancellationToken ct)
    {
        var legacy = marks.Where(m => m.Origin is null && m.TenantId != Guid.Empty).ToList();
        if (legacy.Count == 0) return 0;

        var roleCol = database.GetCollection<Role>("roles");
        var rpCol = database.GetCollection<RolePermission>("rolePermissions");
        var auditCol = database.GetCollection<AuthAuditLog>("authAuditLogs");
        var markCol = database.GetCollection<PermissionReconciliationMark>(PermissionReconciliationMark.CollectionName);
        var corrected = 0;

        foreach (var mark in legacy)
        {
            var pair = pairs.FirstOrDefault(p => p.ExportKey == mark.Key);
            if (pair is null) continue;
            var export = permissions[pair.ExportKey];

            try
            {
                var tenantRoles = (await ReadRolesAsync(roleCol, [mark.TenantId], ct))
                    .Where(r => !r.IsDeleted)
                    .ToDictionary(r => r.Id, r => new ExportGrantBackfill.RoleState(r.Id, r.Name, r.TenantId, r.IsSystem, r.CreatedAt));
                var exportGrants = await rpCol
                    .Find(rp => rp.TenantId == mark.TenantId && rp.PermissionId == export.Id && rp.IsDeleted == false)
                    .Project<RolePermission>(Builders<RolePermission>.Projection.Include(rp => rp.RoleId).Include(rp => rp.GrantSource))
                    .ToListAsync(ct);

                foreach (var grant in exportGrants)
                {
                    if (!tenantRoles.TryGetValue(grant.RoleId, out var role)) continue;
                    if (!ExportGrantBackfill.NeedsSourceCorrection(role, grant.GrantSource,
                            r => DefaultRolePermissionTemplate.SelectFor(r.Name, [export]).Count > 0))
                    {
                        continue;
                    }

                    await InsertOnceAsync(auditCol, CorrectionAuditRow(pair, mark.TenantId, role, export.Id), ct);
                    // Only a grant that is STILL System is changed: the filter makes a second writer's update a no-op.
                    var changed = await rpCol.UpdateOneAsync(
                        rp => rp.Id == grant.Id && rp.GrantSource == GrantSource.System,
                        Builders<RolePermission>.Update.Set(rp => rp.GrantSource, GrantSource.Manual),
                        cancellationToken: ct);
                    if (changed.ModifiedCount > 0) corrected++;
                }

                await markCol.UpdateOneAsync(
                    m => m.Id == mark.Id,
                    Builders<PermissionReconciliationMark>.Update.Set(m => m.Origin, ExportGrantBackfill.OriginBackfilled),
                    cancellationToken: ct);
                mark.Origin = ExportGrantBackfill.OriginBackfilled;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed.Add(mark.TenantId);
                LogTenantFailure(logger, "source correction", mark.TenantId, pair.ExportKey, ex);
            }
        }

        return corrected;
    }

    private static async Task<Dictionary<string, Permission>> LoadPermissionsAsync(IMongoDatabase database, CancellationToken ct)
    {
        var keys = ExportGrantBackfill.Keys.SelectMany(k => new[] { k.ReadKey, k.ExportKey }).ToList();
        return (await database.GetCollection<Permission>("permissions").Find(p => keys.Contains(p.Key) && p.IsDeleted == false).ToListAsync(ct))
            .ToDictionary(p => p.Key, StringComparer.Ordinal);
    }

    /// <summary>The values of <paramref name="keyField"/> whose document really STORES a CreatedAt.</summary>
    private static async Task<HashSet<string>> StoredCreatedAtAsync(IMongoDatabase database, string collection, string keyField, IReadOnlyCollection<string> keys, CancellationToken ct)
    {
        var filter = Builders<MongoDB.Bson.BsonDocument>.Filter.In(keyField, keys) & Builders<MongoDB.Bson.BsonDocument>.Filter.Exists("CreatedAt");
        var docs = await database.GetCollection<MongoDB.Bson.BsonDocument>(collection).Find(filter).Project(Builders<MongoDB.Bson.BsonDocument>.Projection.Include(keyField)).ToListAsync(ct);
        return docs.Select(d => d[keyField].AsString).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>The roles of these tenants — live and deleted — with only the fields the decision reads.</summary>
    private static Task<List<Role>> ReadRolesAsync(IMongoCollection<Role> roleCol, IReadOnlyCollection<Guid> tenants, CancellationToken ct)
        => roleCol
            .Find(r => tenants.Contains(r.TenantId))
            .Project<Role>(Builders<Role>.Projection
                .Include(r => r.Name).Include(r => r.TenantId).Include(r => r.IsSystem).Include(r => r.IsDeleted).Include(r => r.CreatedAt))
            .ToListAsync(ct);

    private static AuthAuditLog GrantAuditRow(ExportGrantBackfill.KeyPair pair, ExportGrantBackfill.PlannedGrant grant)
        => new(ExportGrantBackfill.AuditEventName, Guid.Empty, grant.TenantId, JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["roleId"] = grant.RoleId,
            ["roleName"] = grant.RoleName,
            ["permissionId"] = grant.PermissionId,
            ["permissionKey"] = pair.ExportKey,
            ["grantSource"] = grant.Source.ToString(),
            ["source"] = pair.AuditSource,
            ["actor"] = SystemUser,
            ["actorId"] = null
        }))
        {
            Id = ExportGrantBackfill.AuditId(grant.TenantId, grant.RoleId, pair.ExportKey)
        };

    private static AuthAuditLog CorrectionAuditRow(ExportGrantBackfill.KeyPair pair, Guid tenantId, ExportGrantBackfill.RoleState role, Guid permissionId)
        => new(ExportGrantBackfill.SourceCorrectedEventName, Guid.Empty, tenantId, JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["roleId"] = role.RoleId,
            ["roleName"] = role.Name,
            ["permissionId"] = permissionId,
            ["permissionKey"] = pair.ExportKey,
            ["before"] = new Dictionary<string, object?> { ["grantSource"] = GrantSource.System.ToString() },
            ["after"] = new Dictionary<string, object?> { ["grantSource"] = GrantSource.Manual.ToString() },
            ["source"] = pair.CorrectionAuditSource,
            ["actor"] = SystemUser,
            ["actorId"] = null
        }))
        {
            Id = ExportGrantBackfill.CorrectionAuditId(tenantId, role.RoleId, pair.ExportKey)
        };

    private static Task<bool> WriteMarkAsync(IMongoCollection<PermissionReconciliationMark> markCol, Guid tenantId, string exportKey, string origin, CancellationToken ct)
        => InsertOnceAsync(
            markCol,
            new PermissionReconciliationMark
            {
                Id = ExportGrantBackfill.MarkId(tenantId, exportKey),
                TenantId = tenantId,
                Key = exportKey,
                ReconciledAtUtc = DateTime.UtcNow,
                Origin = origin
            },
            ct);

    /// <summary>Inserts the document; a DUPLICATE KEY (same id, or the collection's unique index) means another writer
    /// — an earlier run, or an instance starting at the same time — already did, and returns false. Every other write
    /// error is the caller's to see: it fails the tenant, it is never swallowed here.</summary>
    private static async Task<bool> InsertOnceAsync<T>(IMongoCollection<T> collection, T document, CancellationToken ct)
    {
        try
        {
            await collection.InsertOneAsync(document, cancellationToken: ct);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    // The tenant, the key and the exception's TYPE. Not its message and not a document: a serializer's message quotes
    // field values, and the documents here are roles and grants.
    private static void LogTenantFailure(ILogger logger, string step, Guid tenantId, string exportKey, Exception ex)
        => logger.LogError(
            "Export grant {Step} failed for tenant {TenantId}, key {ExportKey}: {ExceptionType}. The tenant is left unmarked and is retried on the next start.",
            step, tenantId, exportKey, ex.GetType().FullName);
}

/// <summary>
/// The seeder runs during service registration, before any logging provider exists. This <see cref="ILogger"/> writes
/// one line per entry to the console (which the host captures), so code that logs through <see cref="ILogger"/> — and
/// is tested through it — has somewhere to write at that point of the start-up.
/// </summary>
public sealed class SeedConsoleLogger : ILogger
{
    public static readonly SeedConsoleLogger Instance = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel)) Console.WriteLine($"[seed:{logLevel}] {formatter(state, null)}");
    }
}
