using System.Text.Json;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
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
/// <para><b>One tenant's failure is that tenant's.</b> Role and grant documents are read raw and deserialized ONE BY
/// ONE: a document that does not deserialize fails only its own tenant (logged with the tenant id, the document id and
/// the exception TYPE), and a broken catalog row of a key fails only that key. Each tenant is then settled on its own:
/// a write that fails is logged with the tenant id, the key and the exception type — never the message or a
/// document — the tenant stays unmarked and is tried again on the next start, and every other tenant and the other
/// key go on. Cancellation is not an error and stops the run.</para>
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
        TenantGrantsWritten,
        /// <summary>Source correction: this grant was read as System, its source is not changed yet.</summary>
        SourceCorrectionRead
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
        CancellationToken ct = default,
        Func<IMongoCollection<RolePermission>, RolePermission, CancellationToken, Task<bool>>? grantWriter = null)
    {
        // Test seam: how a grant document is inserted (a test makes it fail AFTER the write, the way a lost
        // acknowledgement does). Production uses InsertOnceAsync.
        grantWriter ??= InsertOnceAsync;
        var failed = new HashSet<Guid>();
        var permissions = await LoadPermissionsAsync(database, logger, ct);
        var pairs = ExportGrantBackfill.Keys
            .Where(k => permissions.ContainsKey(k.ReadKey) && permissions.ContainsKey(k.ExportKey))
            .ToList(); // a key that is not in the catalog (yet): nothing to decide, nothing to mark
        if (pairs.Count == 0) return Result.Nothing;

        var markCol = database.GetCollection<PermissionReconciliationMark>(PermissionReconciliationMark.CollectionName);
        var exportKeys = pairs.Select(p => p.ExportKey).ToList();
        // Read raw, one by one: a mark that does not deserialize fails ITS tenant (left out of this run, logged), it
        // does not stop the run — and its tenant is not treated as unmarked either.
        var marks = await ReadDocumentsAsync<PermissionReconciliationMark>(database, PermissionReconciliationMark.CollectionName,
            Builders<PermissionReconciliationMark>.Filter.In(m => m.Key, exportKeys), null, logger, failed, ct);

        var corrected = await CorrectLegacySourcesAsync(database, logger, pairs, permissions, marks, failed, hook, ct);

        // Settled for EVERY key → never read again. One indexed distinct finds whoever is not.
        var settled = marks.GroupBy(m => m.TenantId)
            .Where(g => exportKeys.All(k => g.Any(m => m.Key == k)))
            .Select(g => g.Key)
            .Concat(failed) // a tenant whose mark could not be read is left alone on this start
            .Append(Guid.Empty) // a role without a tenant is never processed and never marked
            .ToList();
        var unsettled = await UnsettledTenantsAsync(database, settled, logger, ct);
        if (unsettled.Count == 0) return Result.Nothing with { SourcesCorrected = corrected, FailedTenants = failed.ToList() };


        // Deleted roles are read too: they receive nothing, but the oldest one dates the tenant. (A role document that
        // stores no CreatedAt reads as the moment it was loaded — newer than any key — so it can only make its tenant
        // look younger, and it is never a candidate itself.) Read raw, deserialized one by one: a broken document fails
        // its own tenant only.
        var roleDocs = await ReadDocumentsAsync<Role>(database, "roles",
            Builders<Role>.Filter.In(r => r.TenantId, unsettled),
            ["Name", "TenantId", "IsSystem", "IsDeleted", "CreatedAt"], logger, failed, ct);
        var roles = roleDocs
            .Select(r => new ExportGrantBackfill.RoleState(r.Id, r.Name, r.TenantId, r.IsSystem, r.CreatedAt, r.IsDeleted))
            .ToList();
        var permissionIds = pairs.SelectMany(p => new[] { permissions[p.ReadKey].Id, permissions[p.ExportKey].Id }).ToList();
        var rpCol = database.GetCollection<RolePermission>("rolePermissions");
        // A revoked grant is a hard delete, so "live" is the only state a grant document has; IsDeleted is honoured anyway.
        // A grant document that STORES no CreatedAt would read as "now" (the property's initializer): its date is
        // "not stored", and the decision is left to the role's own age — never a date the database does not hold.
        var undatedGrants = new HashSet<Guid>();
        var grantDocs = await ReadDocumentsAsync<RolePermission>(database, "rolePermissions",
            Builders<RolePermission>.Filter.In(rp => rp.TenantId, unsettled)
            & Builders<RolePermission>.Filter.In(rp => rp.PermissionId, permissionIds)
            & Builders<RolePermission>.Filter.Eq(rp => rp.IsDeleted, false),
            ["RoleId", "PermissionId", "TenantId", "CreatedAt"], logger, failed, ct,
            (grant, raw) => { if (!raw.Contains("CreatedAt")) undatedGrants.Add(grant.Id); });
        var grants = grantDocs.Select(rp => (rp.RoleId, rp.PermissionId)).ToHashSet();
        var grantCreatedAt = grantDocs.Where(rp => !undatedGrants.Contains(rp.Id))
            .GroupBy(rp => (rp.RoleId, rp.PermissionId)).ToDictionary(g => g.Key, g => g.Min(rp => rp.CreatedAt));
        // A tenant one of whose documents could not be read is not decided on a partial picture.
        roles = roles.Where(r => !failed.Contains(r.TenantId)).ToList();

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
                role => DefaultRolePermissionTemplate.SelectFor(role.Name, [export]).Count > 0, grantCreatedAt);

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
                            // An instance that started at the same time may have written the row AND the grant since
                            // this run read its picture: then nothing is missing, and nothing is reported.
                            // The owner of the row writes its grant right after it: allow it that moment (a bounded re-read,
                            // 3 × 100 ms) before calling a grant missing.
                            if (await HoldsSoonAsync(rpCol, grant, ct))
                            {
                                grants.Add((grant.RoleId, grant.PermissionId));
                                continue;
                            }

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
                        try
                        {
                            if (await grantWriter(rpCol, row, ct)) written++;
                        }
                        catch (MongoWriteException ex) when (ex.WriteError is not null)
                        {
                            // The SERVER refused the insert (a write error, not a lost acknowledgement): the outcome is
                            // known. If the grant is indeed not there, withdraw the audit row's "granted" with a row of its
                            // own (deterministic id — written once). The role stays without the grant (the audit row is the
                            // decision record) and the tenant fails below, logged.
                            if (!await HoldsAsync(rpCol, grant, ct)) await InsertOnceAsync(auditCol, NotAppliedAuditRow(pair, grant, ex), ct);
                            throw;
                        }
                        // Every other exception — a write concern error, a network failure after the send — leaves the
                        // outcome UNKNOWN: the grant may well be in. Nothing is withdrawn; the tenant fails and the next
                        // start sees what is there.

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
        Func<Stage, ExportGrantBackfill.KeyPair, Guid, Task>? hook,
        CancellationToken ct)
    {
        var legacy = marks.Where(m => m.Origin is null && m.TenantId != Guid.Empty).ToList();
        if (legacy.Count == 0) return 0;

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
                var unreadable = new HashSet<Guid>();
                var tenantRoles = (await ReadDocumentsAsync<Role>(database, "roles",
                        Builders<Role>.Filter.Eq(r => r.TenantId, mark.TenantId),
                        ["Name", "TenantId", "IsSystem", "IsDeleted", "CreatedAt"], logger, unreadable, ct))
                    .Where(r => !r.IsDeleted)
                    .ToDictionary(r => r.Id, r => new ExportGrantBackfill.RoleState(r.Id, r.Name, r.TenantId, r.IsSystem, r.CreatedAt));
                var exportGrants = await ReadDocumentsAsync<RolePermission>(database, "rolePermissions",
                    Builders<RolePermission>.Filter.Eq(rp => rp.TenantId, mark.TenantId)
                    & Builders<RolePermission>.Filter.Eq(rp => rp.PermissionId, export.Id)
                    & Builders<RolePermission>.Filter.Eq(rp => rp.IsDeleted, false),
                    ["RoleId", "GrantSource", "TenantId"], logger, unreadable, ct);
                if (unreadable.Count > 0)
                {
                    // Already logged with the document id; the tenant keeps its legacy mark and is tried next start.
                    failed.Add(mark.TenantId);
                    continue;
                }

                foreach (var grant in exportGrants)
                {
                    if (!tenantRoles.TryGetValue(grant.RoleId, out var role)) continue;
                    if (!ExportGrantBackfill.NeedsSourceCorrection(role, grant.GrantSource,
                            r => DefaultRolePermissionTemplate.SelectFor(r.Name, [export]).Count > 0))
                    {
                        continue;
                    }

                    if (hook is not null) await hook(Stage.SourceCorrectionRead, pair, grant.RoleId);
                    // Only a grant that is STILL there and STILL System is changed (a second writer, or an administrator
                    // who revoked it meanwhile, makes this a no-op) — and only a change that happened is recorded.
                    // (A stop between the two writes leaves a corrected grant without its row: a missing record of a
                    // change that grants nothing, never a record of a change that did not happen.)
                    var changed = await rpCol.UpdateOneAsync(
                        rp => rp.Id == grant.Id && rp.GrantSource == GrantSource.System,
                        Builders<RolePermission>.Update.Set(rp => rp.GrantSource, GrantSource.Manual),
                        cancellationToken: ct);
                    if (changed.ModifiedCount == 0) continue;
                    await InsertOnceAsync(auditCol, CorrectionAuditRow(pair, mark.TenantId, role, export.Id), ct);
                    corrected++;
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

    /// <summary>The four catalog rows, live only. A row that does not deserialize is left out (its key is then "not in
    /// the catalog" for this run — nothing decided, nothing marked) and logged with its id and the exception type.</summary>
    private static async Task<Dictionary<string, Permission>> LoadPermissionsAsync(IMongoDatabase database, ILogger logger, CancellationToken ct)
    {
        var keys = ExportGrantBackfill.Keys.SelectMany(k => new[] { k.ReadKey, k.ExportKey }).ToList();
        var raw = database.GetCollection<BsonDocument>("permissions");
        var filter = Builders<Permission>.Filter.In(p => p.Key, keys) & Builders<Permission>.Filter.Eq(p => p.IsDeleted, false);
        var found = new Dictionary<string, Permission>(StringComparer.Ordinal);
        foreach (var doc in await raw.Find(Render(database, "permissions", filter)).ToListAsync(ct))
        {
            try
            {
                var permission = BsonSerializer.Deserialize<Permission>(doc);
                found[permission.Key] = permission;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError("Export grant backfill could not read catalog row {DocumentId}: {ExceptionType}. Its key is skipped on this start.",
                    IdOf(doc), ex.GetType().FullName);
            }
        }

        return found;
    }

    /// <summary>
    /// Reads the matching documents RAW (only <paramref name="fields"/>) and deserializes each on its own. A document
    /// that does not deserialize adds its tenant to <paramref name="failed"/> and is logged with the tenant id, the
    /// document id and the exception type — never its content.
    /// </summary>
    private static async Task<List<T>> ReadDocumentsAsync<T>(
        IMongoDatabase database, string collection, FilterDefinition<T> filter, IReadOnlyList<string>? fields,
        ILogger logger, HashSet<Guid> failed, CancellationToken ct, Action<T, BsonDocument>? inspect = null)
    {
        var find = database.GetCollection<BsonDocument>(collection).Find(Render(database, collection, filter));
        var docs = fields is null
            ? await find.ToListAsync(ct)
            : await find.Project(new BsonDocument(fields.Select(f => new BsonElement(f, 1)))).ToListAsync(ct);
        var read = new List<T>(docs.Count);
        foreach (var doc in docs)
        {
            try
            {
                var item = BsonSerializer.Deserialize<T>(doc);
                inspect?.Invoke(item, doc);
                read.Add(item);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var tenant = TenantOf(doc);
                if (tenant is { } t) failed.Add(t);
                logger.LogError(
                    "Export grant backfill could not read {Collection} document {DocumentId} of tenant {TenantId}: {ExceptionType}. The tenant is left unmarked and is retried on the next start.",
                    collection, IdOf(doc), tenant?.ToString() ?? "unknown", ex.GetType().FullName);
            }
        }

        return read;
    }

    private static BsonDocument Render<T>(IMongoDatabase database, string collection, FilterDefinition<T> filter)
    {
        var typed = database.GetCollection<T>(collection);
        return filter.Render(typed.DocumentSerializer, typed.Settings.SerializerRegistry);
    }

    /// <summary>The document's id as a reader can search for it (a Guid when it is one), never its content.</summary>
    private static string IdOf(BsonDocument doc)
    {
        // Only an identifier is printed: a Guid, an ObjectId or a string id. Any other _id (a document, an array …) could
        // carry content, so only its type is named.
        var id = doc.GetValue("_id", BsonNull.Value);
        try
        {
            return id.BsonType switch
            {
                BsonType.Binary when id.AsBsonBinaryData.SubType is BsonBinarySubType.UuidStandard or BsonBinarySubType.UuidLegacy
                    => id.AsBsonBinaryData.ToGuid().ToString(),
                BsonType.ObjectId => id.AsObjectId.ToString(),
                BsonType.String => id.AsString,
                _ => $"<{id.BsonType}>"
            };
        }
        catch (Exception)
        {
            return "<unreadable>";
        }
    }

    private static Guid? TenantOf(BsonDocument doc)
    {
        try
        {
            var value = doc.GetValue("TenantId", BsonNull.Value);
            return value.IsBsonBinaryData ? value.AsBsonBinaryData.ToGuid() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// The tenants with a role that are not settled — one RAW distinct over <c>TenantId</c>. A value that is not a Guid
    /// (an empty or a text TenantId, which <c>$nin</c> also matches) belongs to no tenant: it is logged with the ids of
    /// the role documents that carry it, and skipped — it never stops the run.
    /// </summary>
    private static async Task<List<Guid>> UnsettledTenantsAsync(IMongoDatabase database, IReadOnlyCollection<Guid> settled, ILogger logger, CancellationToken ct)
    {
        var raw = database.GetCollection<BsonDocument>("roles");
        var filter = Render(database, "roles", Builders<Role>.Filter.Nin(r => r.TenantId, settled));
        var values = await (await raw.DistinctAsync<BsonValue>("TenantId", filter, cancellationToken: ct)).ToListAsync(ct);
        var tenants = new List<Guid>();
        foreach (var value in values)
        {
            if (value.IsBsonBinaryData && value.AsBsonBinaryData.SubType is BsonBinarySubType.UuidStandard or BsonBinarySubType.UuidLegacy)
            {
                tenants.Add(value.AsBsonBinaryData.ToGuid());
                continue;
            }

            var ids = await raw.Find(new BsonDocument("TenantId", value)).Project(new BsonDocument("_id", 1)).Limit(20).ToListAsync(ct);
            logger.LogError(
                "Export grant backfill skips role document(s) {DocumentIds} whose TenantId is not a tenant id ({BsonType}).",
                string.Join(", ", ids.Select(IdOf)), value.BsonType);
        }

        return tenants;
    }

    private static async Task<bool> HoldsSoonAsync(IMongoCollection<RolePermission> rpCol, ExportGrantBackfill.PlannedGrant grant, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (await HoldsAsync(rpCol, grant, ct)) return true;
            await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
        }

        return await HoldsAsync(rpCol, grant, ct);
    }

    private static async Task<bool> HoldsAsync(IMongoCollection<RolePermission> rpCol, ExportGrantBackfill.PlannedGrant grant, CancellationToken ct)
        => await rpCol.Find(rp => rp.RoleId == grant.RoleId && rp.PermissionId == grant.PermissionId && rp.TenantId == grant.TenantId && rp.IsDeleted == false)
            .Limit(1).AnyAsync(ct);

    /// <summary>The values of <paramref name="keyField"/> whose document really STORES a CreatedAt.</summary>
    private static async Task<HashSet<string>> StoredCreatedAtAsync(IMongoDatabase database, string collection, string keyField, IReadOnlyCollection<string> keys, CancellationToken ct)
    {
        var filter = Builders<MongoDB.Bson.BsonDocument>.Filter.In(keyField, keys) & Builders<MongoDB.Bson.BsonDocument>.Filter.Exists("CreatedAt");
        var docs = await database.GetCollection<MongoDB.Bson.BsonDocument>(collection).Find(filter).Project(Builders<MongoDB.Bson.BsonDocument>.Projection.Include(keyField)).ToListAsync(ct);
        return docs.Select(d => d[keyField].AsString).ToHashSet(StringComparer.Ordinal);
    }


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

    private static AuthAuditLog NotAppliedAuditRow(ExportGrantBackfill.KeyPair pair, ExportGrantBackfill.PlannedGrant grant, Exception ex)
        => new(ExportGrantBackfill.GrantNotAppliedEventName, Guid.Empty, grant.TenantId, JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["roleId"] = grant.RoleId,
            ["roleName"] = grant.RoleName,
            ["permissionId"] = grant.PermissionId,
            ["permissionKey"] = pair.ExportKey,
            ["withdraws"] = ExportGrantBackfill.AuditId(grant.TenantId, grant.RoleId, pair.ExportKey),
            ["reason"] = ex.GetType().FullName,
            ["source"] = pair.AuditSource,
            ["actor"] = SystemUser,
            ["actorId"] = null
        }))
        {
            Id = ExportGrantBackfill.NotAppliedAuditId(grant.TenantId, grant.RoleId, pair.ExportKey)
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
