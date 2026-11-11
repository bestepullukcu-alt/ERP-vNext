using System.Text.Json;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Seed;

/// <summary>
/// The Mongo side of <see cref="BaselineGrantRetraction"/>: a one-time, audited clean-up of ONE widening
/// (<see cref="BaselineGrantRetraction.Definition"/> — Q358: <see cref="Q358ShipmentGrantRetraction"/>, G7:
/// <see cref="G7SandopCapacityGrantRetraction"/>), run only when that definition's <c>…Retraction:Mode</c> says so (default
/// Off). (The file keeps its Q358 name: the closed writer inventory lists its calls by path.)
///
/// <para>⚠ AN AUTHORITY WRITER. <see cref="BaselineGrantRetraction.Mode.DryRun"/> writes NOTHING and reports the plan;
/// <see cref="BaselineGrantRetraction.Mode.Apply"/> changes only System-sourced grants of the definition's keys on tenant Admin roles.</para>
///
/// <para><b>Audit first (AUD-001, K2 fail-closed).</b> Each change writes its audit row (deterministic id) BEFORE the grant
/// is touched: a write of the row that fails (other than "already there") fails the tenant and the grant is not touched;
/// a row that is already there is the record of this same decision by an earlier, interrupted run. The grant change itself
/// is conditional — only a
/// grant that is STILL live and STILL System changes — so a repeated run, or a second instance, changes nothing twice. A
/// row written for a change that then did not happen (an administrator, or another writer, changed the grant in between)
/// is withdrawn by a <c>…_not_applied</c> row of its own.</para>
///
/// <para><b>Soft delete.</b> A revoked grant is marked <c>IsDeleted</c> with <c>DeletedAt</c> (and
/// <c>UpdatedAt</c>/<c>UpdatedBy</c>). Granting it again later — the tenant buys the module — revives that document and
/// clears <c>DeletedAt</c> (<see cref="RolePermissionRepository.AssignAsync"/>), because the unique (role, permission,
/// tenant) index still holds it.</para>
///
/// <para><b>Unknown entitlement → nothing.</b> A tenant whose entitlement cannot be read authoritatively is skipped and
/// reported; it is looked at again on the next run.</para>
/// </summary>
public static class Q358ShipmentGrantRetractionRunner
{
    private const string SystemUser = "system";
    public const string NotAppliedEventName = "role_permission_retraction_not_applied";

    /// <summary>What one run decided and did. <paramref name="Planned"/> is the full plan (also in DryRun).</summary>
    public sealed record Result(
        BaselineGrantRetraction.Mode Mode,
        int AdminRolesScanned,
        int Converted,
        int SoftDeleted,
        int NotApplied,
        IReadOnlyList<BaselineGrantRetraction.Planned> Planned,
        IReadOnlyList<Guid> UnknownTenants,
        IReadOnlyList<Guid> FailedTenants)
    {
        public bool Clean => UnknownTenants.Count == 0 && FailedTenants.Count == 0;
    }

    /// <param name="readEntitlement">The tenant's entitled module codes, or null when they cannot be read authoritatively.</param>
    /// <param name="hook">Test seam: called after a change's audit row is written and before the grant is touched.</param>
    public static async Task<Result> RunAsync(
        IMongoDatabase database,
        BaselineGrantRetraction.Definition definition,
        BaselineGrantRetraction.Mode mode,
        Func<Guid, CancellationToken, Task<IReadOnlyCollection<string>?>> readEntitlement,
        ILogger logger,
        CancellationToken ct = default,
        Func<BaselineGrantRetraction.Planned, Task>? hook = null)
    {
        if (mode == BaselineGrantRetraction.Mode.Off)
            return new Result(mode, 0, 0, 0, 0, [], [], []);

        var permissions = (await database.GetCollection<Permission>("permissions")
                .Find(Builders<Permission>.Filter.In(p => p.Key, definition.AllKeys)
                      & Builders<Permission>.Filter.Eq(p => p.IsDeleted, false))
                .ToListAsync(ct))
            .ToDictionary(p => p.Id, p => p.Key);

        // Tenant Admin roles only: the system role named Admin of a real tenant. Viewer, SuperAdmin, custom roles: never.
        var adminRoles = await database.GetCollection<Role>("roles")
            .Find(Builders<Role>.Filter.Eq(r => r.Name, BaselineGrantRetraction.AdminRoleName)
                  & Builders<Role>.Filter.Eq(r => r.IsSystem, true)
                  & Builders<Role>.Filter.Eq(r => r.IsDeleted, false)
                  & Builders<Role>.Filter.Ne(r => r.TenantId, Guid.Empty))
            .ToListAsync(ct);
        if (permissions.Count == 0 || adminRoles.Count == 0)
            return new Result(mode, adminRoles.Count, 0, 0, 0, [], [], []);

        var rpCol = database.GetCollection<RolePermission>("rolePermissions");
        // Source is filtered in memory: a legacy document without GrantSource reads as System (the entity's default).
        var grants = (await rpCol
                .Find(Builders<RolePermission>.Filter.In(rp => rp.RoleId, adminRoles.Select(r => r.Id))
                      & Builders<RolePermission>.Filter.In(rp => rp.PermissionId, permissions.Keys)
                      & Builders<RolePermission>.Filter.Eq(rp => rp.IsDeleted, false))
                .ToListAsync(ct))
            .Select(rp => new BaselineGrantRetraction.Grant(rp.Id, rp.TenantId, rp.RoleId, rp.PermissionId, permissions[rp.PermissionId], rp.GrantSource))
            .ToList();

        var roleTenant = adminRoles.ToDictionary(r => r.Id, r => r.TenantId);
        var auditCol = database.GetCollection<AuthAuditLog>("authAuditLogs");
        var versions = new RoleAssignmentVersionRepository(database);
        var planned = new List<BaselineGrantRetraction.Planned>();
        var unknown = new List<Guid>();
        var failed = new List<Guid>();
        int converted = 0, softDeleted = 0, notApplied = 0;

        // A grant whose tenant differs from its role's is not this rule's to decide (and is reported, not touched).
        foreach (var stray in grants.Where(g => roleTenant[g.RoleId] != g.TenantId))
        {
            logger.LogError("{Retraction} retraction skips grant {GrantId}: its tenant {TenantId} is not its role's.", definition.Name, stray.GrantId, stray.TenantId);
        }

        foreach (var tenant in grants.Where(g => roleTenant[g.RoleId] == g.TenantId)
                     .GroupBy(g => g.TenantId).OrderBy(g => g.Key))
        {
            if (!tenant.Any(g => g.Source == GrantSource.System)) continue; // nothing to decide: no entitlement read
            try
            {
                var entitled = await readEntitlement(tenant.Key, ct);
                var plan = BaselineGrantRetraction.Plan(definition, tenant, entitled);
                planned.AddRange(plan);
                if (entitled is null)
                {
                    unknown.Add(tenant.Key);
                    logger.LogWarning("{Retraction} retraction skips tenant {TenantId}: its entitlement could not be read; {Count} grant(s) left as they are.",
                        definition.Name, tenant.Key, plan.Count);
                    continue;
                }

                foreach (var item in plan)
                {
                    logger.LogInformation("{Retraction} retraction {Mode}: tenant {TenantId}, role {RoleId}, key {PermissionKey} → {Action}.",
                        definition.Name, mode, item.Grant.TenantId, item.Grant.RoleId, item.Grant.PermissionKey, item.Action);
                }

                if (mode != BaselineGrantRetraction.Mode.Apply) continue;

                var changedAny = false;
                foreach (var item in plan)
                {
                    var auditId = BaselineGrantRetraction.AuditId(definition, item.Grant.TenantId, item.Grant.RoleId, item.Grant.PermissionKey, item.Action);
                    var ownRow = await InsertOnceAsync(auditCol, AuditRow(definition, item, auditId), ct);
                    if (hook is not null) await hook(item);

                    var filter = Builders<RolePermission>.Filter.Eq(rp => rp.Id, item.Grant.GrantId)
                                 & Builders<RolePermission>.Filter.Eq(rp => rp.IsDeleted, false)
                                 & Builders<RolePermission>.Filter.Or(
                                     Builders<RolePermission>.Filter.Eq(rp => rp.GrantSource, GrantSource.System),
                                     Builders<RolePermission>.Filter.Exists(rp => rp.GrantSource, false));
                    var now = DateTimeOffset.UtcNow;
                    var update = item.Action == BaselineGrantRetraction.Action.ConvertToModule
                        ? Builders<RolePermission>.Update
                            .Set(rp => rp.GrantSource, GrantSource.Module)
                            .Set(rp => rp.SourceModuleCode, item.ModuleCode)
                        : Builders<RolePermission>.Update.Set(rp => rp.IsDeleted, true).Set(rp => rp.DeletedAt, now);
                    update = update.Set(rp => rp.UpdatedAt, now).Set(rp => rp.UpdatedBy, SystemUser);

                    var changed = await rpCol.UpdateOneAsync(filter, update, cancellationToken: ct);
                    if (changed.ModifiedCount == 1)
                    {
                        changedAny = true;
                        if (item.Action == BaselineGrantRetraction.Action.ConvertToModule) converted++; else softDeleted++;
                        continue;
                    }

                    // Not changed: someone else did it first (a repeated run, a second instance) or an administrator
                    // changed the grant since it was read. Only a row THIS run wrote claims a change; withdraw it.
                    if (ownRow)
                    {
                        await InsertOnceAsync(auditCol, NotAppliedRow(definition, item, auditId), ct);
                        notApplied++;
                    }
                }

                // Cached authorization snapshots of this tenant are invalidated (FU13) once its grants changed.
                if (changedAny) await versions.IncrementAsync(tenant.Key, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed.Add(tenant.Key);
                logger.LogError("{Retraction} retraction failed for tenant {TenantId}: {ExceptionType}. It is looked at again on the next run.",
                    definition.Name, tenant.Key, ex.GetType().FullName);
            }
        }

        return new Result(mode, adminRoles.Count, converted, softDeleted, notApplied, planned, unknown, failed);
    }

    private static AuthAuditLog AuditRow(BaselineGrantRetraction.Definition definition, BaselineGrantRetraction.Planned item, Guid auditId)
    {
        var convert = item.Action == BaselineGrantRetraction.Action.ConvertToModule;
        return new AuthAuditLog(
            convert ? BaselineGrantRetraction.ConvertedEventName : BaselineGrantRetraction.RevokedEventName,
            Guid.Empty, item.Grant.TenantId, JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["roleId"] = item.Grant.RoleId,
                ["roleName"] = BaselineGrantRetraction.AdminRoleName,
                ["permissionId"] = item.Grant.PermissionId,
                ["permissionKey"] = item.Grant.PermissionKey,
                ["grantId"] = item.Grant.GrantId,
                ["before"] = new Dictionary<string, object?> { ["grantSource"] = GrantSource.System.ToString(), ["isDeleted"] = false },
                ["after"] = convert
                    ? new Dictionary<string, object?> { ["grantSource"] = GrantSource.Module.ToString(), ["sourceModuleCode"] = item.ModuleCode }
                    : new Dictionary<string, object?> { ["isDeleted"] = true },
                ["decision"] = definition.DecisionRef,
                ["source"] = definition.AuditSource,
                ["actor"] = SystemUser,
                ["actorId"] = null
            }))
        {
            Id = auditId
        };
    }

    private static AuthAuditLog NotAppliedRow(BaselineGrantRetraction.Definition definition, BaselineGrantRetraction.Planned item, Guid withdraws)
        => new(NotAppliedEventName, Guid.Empty, item.Grant.TenantId, JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["roleId"] = item.Grant.RoleId,
            ["permissionKey"] = item.Grant.PermissionKey,
            ["grantId"] = item.Grant.GrantId,
            ["withdraws"] = withdraws,
            ["source"] = definition.AuditSource,
            ["actor"] = SystemUser,
            ["actorId"] = null
        }))
        {
            Id = BaselineGrantRetraction.NotAppliedAuditId(definition, withdraws)
        };

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
