using System.Text.Json;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Seed;

/// <summary>
/// The Mongo side of <see cref="ExportGrantBackfill"/>: reads roles, grants and marks ONCE for every export key, writes
/// the planned grants tenant by tenant and marks each tenant when it is finished.
///
/// <para>⚠ AN AUTHORITY WRITER. Every grant it makes leaves a <c>role_permission_granted</c> row in
/// <c>authAuditLogs</c> with the system actor — a permission nobody clicked must still answer "who gave this?".</para>
///
/// <para><b>Order inside one grant: audit row, then grant.</b> The audit row has a deterministic id, so writing it
/// again is a no-op. Stopped between the two, the next run finds the grant missing, repeats the (no-op) audit write and
/// writes the grant: one grant, exactly one audit row. The reverse order would leave a grant that no later run could
/// tell apart from one a person made, and so a grant without its audit row.</para>
///
/// <para><b>Two instances at once.</b> Grants are unique per (role, permission, tenant), audit rows and marks per
/// deterministic id. The losing writer gets a duplicate-key error, which here means "already done" — it never fails
/// the seeding.</para>
/// </summary>
public static class ExportGrantBackfillRunner
{
    private const string SystemUser = "system";

    /// <summary>Where a run is, for the fault-injection tests (a hook that throws is a process that died there).</summary>
    public enum Stage
    {
        /// <summary>The audit row of this grant is written, the grant is not.</summary>
        AuditWritten,
        /// <summary>The grant is written.</summary>
        GrantWritten,
        /// <summary>All grants of this tenant are written, its mark is not.</summary>
        TenantGrantsWritten
    }

    /// <summary>
    /// Before a build that knows the export keys creates the FIRST role of a tenant: mark the tenant "born" for every
    /// key, so it is never treated as an old tenant. No-op when the tenant already has a role (it is then either old —
    /// the backfill decides — or already marked) or has no tenant id. Written BEFORE the role, so a process that dies
    /// in between leaves a marked tenant without roles (harmless) and never an unmarked tenant with roles.
    /// </summary>
    public static async Task MarkBornIfTenantHasNoRolesAsync(IMongoDatabase database, Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) return;

        var hasRole = await database.GetCollection<Role>("roles").Find(r => r.TenantId == tenantId).Limit(1).AnyAsync(ct);
        if (hasRole) return;

        foreach (var pair in ExportGrantBackfill.Keys)
        {
            await WriteMarkAsync(database, tenantId, pair.ExportKey, ExportGrantBackfill.OriginBorn, ct);
        }
    }

    /// <summary>Runs the backfill for every export key. Returns the number of grants written by THIS run.</summary>
    public static async Task<int> RunAsync(
        IMongoDatabase database,
        Func<Stage, ExportGrantBackfill.KeyPair, Guid, Task>? hook = null,
        CancellationToken ct = default)
    {
        var permCol = database.GetCollection<Permission>("permissions");
        var keys = ExportGrantBackfill.Keys.SelectMany(k => new[] { k.ReadKey, k.ExportKey }).ToList();
        var permissions = (await permCol.Find(p => keys.Contains(p.Key) && p.IsDeleted == false).ToListAsync(ct))
            .ToDictionary(p => p.Key, StringComparer.Ordinal);

        // ONE read of the roles, the grants and the marks, shared by every key.
        var roles = (await database.GetCollection<Role>("roles").Find(r => r.IsDeleted == false).ToListAsync(ct))
            .Select(r => new ExportGrantBackfill.RoleState(r.Id, r.Name, r.TenantId, r.IsSystem))
            .ToList();
        var rpCol = database.GetCollection<RolePermission>("rolePermissions");
        var grants = (await rpCol.Find(rp => rp.IsDeleted == false).ToListAsync(ct))
            .Select(rp => (rp.RoleId, rp.PermissionId))
            .ToHashSet();
        var marks = await database.GetCollection<PermissionReconciliationMark>(PermissionReconciliationMark.CollectionName)
            .Find(_ => true).ToListAsync(ct);

        var auditCol = database.GetCollection<AuthAuditLog>("authAuditLogs");
        var written = 0;
        var bumped = new HashSet<Guid>();

        foreach (var pair in ExportGrantBackfill.Keys)
        {
            if (!permissions.TryGetValue(pair.ReadKey, out var read) || !permissions.TryGetValue(pair.ExportKey, out var export))
            {
                continue; // the key is not in the catalog (yet): nothing to decide, nothing to mark
            }

            var marked = marks.Where(m => m.Key == pair.ExportKey).Select(m => m.TenantId).ToHashSet();
            var plan = ExportGrantBackfill.Plan(roles, grants, read.Id, export.Id, marked,
                role => DefaultRolePermissionTemplate.SelectFor(role.Name, [export]).Count > 0);

            foreach (var tenant in plan)
            {
                foreach (var grant in tenant.Grants)
                {
                    var metadata = JsonSerializer.Serialize(new Dictionary<string, object?>
                    {
                        ["roleId"] = grant.RoleId,
                        ["roleName"] = grant.RoleName,
                        ["permissionId"] = grant.PermissionId,
                        ["permissionKey"] = pair.ExportKey,
                        ["grantSource"] = grant.Source.ToString(),
                        ["source"] = pair.AuditSource,
                        ["actor"] = SystemUser,
                        ["actorId"] = null
                    });
                    await InsertOnceAsync(auditCol,
                        new AuthAuditLog(ExportGrantBackfill.AuditEventName, Guid.Empty, grant.TenantId, metadata)
                        {
                            Id = ExportGrantBackfill.AuditId(grant.TenantId, grant.RoleId, pair.ExportKey)
                        }, ct);
                    if (hook is not null) await hook(Stage.AuditWritten, pair, grant.RoleId);

                    var row = grant.Source == GrantSource.System
                        ? RolePermission.SystemGrant(grant.RoleId, grant.PermissionId, grant.TenantId, SystemUser)
                        : RolePermission.ManualGrant(grant.RoleId, grant.PermissionId, grant.TenantId, SystemUser);
                    if (await InsertOnceAsync(rpCol, row, ct))
                    {
                        written++;
                        bumped.Add(grant.TenantId);
                    }

                    grants.Add((grant.RoleId, grant.PermissionId));
                    if (hook is not null) await hook(Stage.GrantWritten, pair, grant.RoleId);
                }

                if (hook is not null) await hook(Stage.TenantGrantsWritten, pair, tenant.TenantId);

                // Marked only now: a run that stopped above leaves the tenant unmarked and the next run finishes it.
                await WriteMarkAsync(database, tenant.TenantId, pair.ExportKey, ExportGrantBackfill.OriginBackfilled, ct);
            }
        }

        // Cached authorization snapshots of every tenant that received a grant are invalidated (FU13).
        var versionService = new RoleAssignmentVersionRepository(database);
        foreach (var tenantId in bumped)
        {
            await versionService.IncrementAsync(tenantId, CancellationToken.None);
        }

        return written;
    }

    private static Task WriteMarkAsync(IMongoDatabase database, Guid tenantId, string exportKey, string origin, CancellationToken ct)
        => InsertOnceAsync(
            database.GetCollection<PermissionReconciliationMark>(PermissionReconciliationMark.CollectionName),
            new PermissionReconciliationMark
            {
                Id = ExportGrantBackfill.MarkId(tenantId, exportKey),
                TenantId = tenantId,
                Key = exportKey,
                ReconciledAtUtc = DateTime.UtcNow,
                Origin = origin
            },
            ct);

    /// <summary>Inserts the document; a duplicate key (same id, or the collection's unique index) means another writer
    /// — an earlier run, or an instance starting at the same time — already did. Returns whether THIS call wrote it.</summary>
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
}
