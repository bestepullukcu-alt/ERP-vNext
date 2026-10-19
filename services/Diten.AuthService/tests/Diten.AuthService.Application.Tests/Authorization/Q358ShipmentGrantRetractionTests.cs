using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Exceptions;
using Diten.AuthService.Application.Tests.Persistence;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using Diten.AuthService.Persistence.Seed;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Action = Diten.AuthService.Domain.Authorization.Q358ShipmentGrantRetraction.Action;
using Mode = Diten.AuthService.Domain.Authorization.Q358ShipmentGrantRetraction.Mode;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// Q358 clean-up (C-13 follow-up): the pure decision, and the production runner on an owned replica set — DryRun writes
/// nothing; Apply converts an entitled tenant's System Shipment grants to Module, soft-deletes a non-entitled tenant's,
/// leaves an unknown tenant, Module/Manual grants and non-Admin roles alone; audit first; a second run is a no-op; a
/// soft-deleted grant comes back when granted again.
/// </summary>
public sealed class Q358ShipmentGrantRetractionPlanTests
{
    private static Q358ShipmentGrantRetraction.Grant G(string key, GrantSource source)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), key, source);

    [Theory]
    [InlineData(null, Mode.Off)]
    [InlineData("", Mode.Off)]
    [InlineData("yes", Mode.Off)]
    [InlineData("true", Mode.Off)]
    [InlineData("DryRun", Mode.DryRun)]
    [InlineData(" dry-run ", Mode.DryRun)]
    [InlineData("APPLY", Mode.Apply)]
    public void Only_DryRun_and_Apply_switch_it_on(string? value, Mode expected)
        => Assert.Equal(expected, Q358ShipmentGrantRetraction.ParseMode(value));

    [Fact]
    public void Only_System_grants_of_the_five_keys_are_candidates()
    {
        var grants = new[]
        {
            G("supplychain.shipments.read", GrantSource.System),
            G("supplychain.shipments.create", GrantSource.Module),
            G("supplychain.shipments.dispatch", GrantSource.Manual),
            G("supplychain.carriers.read", GrantSource.System),
            G("supplychain.shipments.pod.capture", GrantSource.System)
        };

        var plan = Q358ShipmentGrantRetraction.Plan(grants, ["finance"]);

        Assert.Equal(["supplychain.shipments.pod.capture", "supplychain.shipments.read"], plan.Select(p => p.Grant.PermissionKey));
        Assert.All(plan, p => Assert.Equal(Action.SoftDelete, p.Action));
    }

    [Fact]
    public void Entitled_converts_a_normalized_code_counts_unknown_skips()
    {
        var grants = Q358ShipmentGrantRetraction.ShipmentKeys.Select(k => G(k, GrantSource.System)).ToList();

        Assert.All(Q358ShipmentGrantRetraction.Plan(grants, [" Shipment-Tracking-POD "]), p => Assert.Equal(Action.ConvertToModule, p.Action));
        Assert.All(Q358ShipmentGrantRetraction.Plan(grants, []), p => Assert.Equal(Action.SoftDelete, p.Action));
        var unknown = Q358ShipmentGrantRetraction.Plan(grants, null);
        Assert.Equal(5, unknown.Count);
        Assert.All(unknown, p => Assert.Equal(Action.Skip, p.Action));
    }
}

[Collection(AuthReconciliationMongoCollection.Name)]
public sealed class Q358ShipmentGrantRetractionRunnerTests(DisposableAuthMongoReplicaSet ownedMongo)
{
    private IMongoDatabase Db => ownedMongo.Database;
    private IMongoCollection<RolePermission> Grants => Db.GetCollection<RolePermission>("rolePermissions");
    private IMongoCollection<AuthAuditLog> Audit => Db.GetCollection<AuthAuditLog>("authAuditLogs");

    private sealed record Tenant(Guid Id, Guid Admin, Guid Viewer);

    private async Task<Dictionary<string, Guid>> CatalogAsync()
    {
        var col = Db.GetCollection<Permission>("permissions");
        var ids = new Dictionary<string, Guid>();
        foreach (var key in Q358ShipmentGrantRetraction.ShipmentKeys.Append("supplychain.carriers.read"))
        {
            var dot = key.LastIndexOf('.');
            var p = new Permission("supplychain", key["supplychain.".Length..dot], key[(dot + 1)..], key, null,
                moduleOverride: Q358ShipmentGrantRetraction.ModuleCode, scope: PermissionScope.Tenant);
            Assert.Equal(key, p.Key);
            await col.InsertOneAsync(p);
            ids[key] = p.Id;
        }

        return ids;
    }

    /// <summary>A tenant as C-13 left it: Admin holds the five keys as System (plus one unrelated key), Viewer holds read.</summary>
    private async Task<Tenant> TenantAsync(Dictionary<string, Guid> catalog, GrantSource adminSource = GrantSource.System)
    {
        var id = Guid.NewGuid();
        var roles = Db.GetCollection<Role>("roles");
        var admin = new Role("Admin", "Admin", "q358 fixture", id); admin.MarkAsSystem();
        var viewer = new Role("Viewer", "Viewer", "q358 fixture", id); viewer.MarkAsSystem();
        await roles.InsertManyAsync([admin, viewer]);
        foreach (var key in Q358ShipmentGrantRetraction.ShipmentKeys.Append("supplychain.carriers.read"))
        {
            await Grants.InsertOneAsync(adminSource == GrantSource.Module
                ? RolePermission.ModuleGrant(admin.Id, catalog[key], id, "system", Q358ShipmentGrantRetraction.ModuleCode)
                : RolePermission.SystemGrant(admin.Id, catalog[key], id, "system"));
        }

        await Grants.InsertOneAsync(RolePermission.SystemGrant(viewer.Id, catalog["supplychain.shipments.read"], id, "system"));
        return new Tenant(id, admin.Id, viewer.Id);
    }

    private async Task<List<RolePermission>> ShipmentGrantsAsync(Guid roleId, Dictionary<string, Guid> catalog)
    {
        var ids = Q358ShipmentGrantRetraction.ShipmentKeys.Select(k => catalog[k]).ToList();
        return await Grants.Find(g => g.RoleId == roleId && ids.Contains(g.PermissionId)).ToListAsync();
    }

    private static Func<Guid, CancellationToken, Task<IReadOnlyCollection<string>?>> Entitlement(
        Guid entitled, Guid notEntitled, Guid? unknown = null)
        => (tenant, _) => Task.FromResult<IReadOnlyCollection<string>?>(
            tenant == entitled ? ["shipment-tracking-pod", "finance"]
            : tenant == notEntitled ? ["finance"]
            : tenant == unknown ? null
            : []);

    private Task<Q358ShipmentGrantRetractionRunner.Result> RunAsync(Mode mode,
        Func<Guid, CancellationToken, Task<IReadOnlyCollection<string>?>> entitlement,
        Func<Q358ShipmentGrantRetraction.Planned, Task>? hook = null)
        => Q358ShipmentGrantRetractionRunner.RunAsync(Db, mode, entitlement, NullLogger.Instance, CancellationToken.None, hook);

    [OwnedMongoFact]
    public async Task DryRun_plans_every_tenant_and_writes_nothing()
    {
        await ownedMongo.ResetAsync();
        var catalog = await CatalogAsync();
        var e = await TenantAsync(catalog);
        var n = await TenantAsync(catalog);
        var before = await Grants.Find(FilterDefinition<RolePermission>.Empty).ToListAsync();

        var result = await RunAsync(Mode.DryRun, Entitlement(e.Id, n.Id));

        Assert.Equal(10, result.Planned.Count);
        Assert.Equal(5, result.Planned.Count(p => p.Grant.TenantId == e.Id && p.Action == Action.ConvertToModule));
        Assert.Equal(5, result.Planned.Count(p => p.Grant.TenantId == n.Id && p.Action == Action.SoftDelete));
        Assert.Equal(0, result.Converted + result.SoftDeleted);
        Assert.Equal(0, await Audit.CountDocumentsAsync(FilterDefinition<AuthAuditLog>.Empty));
        var after = await Grants.Find(FilterDefinition<RolePermission>.Empty).ToListAsync();
        Assert.Equal(before.Select(g => (g.Id, g.GrantSource, g.IsDeleted)).OrderBy(x => x.Id),
            after.Select(g => (g.Id, g.GrantSource, g.IsDeleted)).OrderBy(x => x.Id));
    }

    [OwnedMongoFact]
    public async Task Off_reads_nothing_and_changes_nothing()
    {
        await ownedMongo.ResetAsync();
        var catalog = await CatalogAsync();
        var n = await TenantAsync(catalog);
        var reads = 0;

        var result = await RunAsync(Mode.Off, (_, _) => { reads++; return Task.FromResult<IReadOnlyCollection<string>?>([]); });

        Assert.Equal(0, reads);
        Assert.Empty(result.Planned);
        Assert.All(await ShipmentGrantsAsync(n.Admin, catalog), g => Assert.False(g.IsDeleted));
    }

    [OwnedMongoFact]
    public async Task Apply_converts_entitled_soft_deletes_not_entitled_skips_unknown_and_touches_nothing_else()
    {
        await ownedMongo.ResetAsync();
        var catalog = await CatalogAsync();
        var e = await TenantAsync(catalog);
        var n = await TenantAsync(catalog);
        var u = await TenantAsync(catalog);
        var m = await TenantAsync(catalog, GrantSource.Module);
        var versions = new RoleAssignmentVersionRepository(Db);

        var result = await RunAsync(Mode.Apply, Entitlement(e.Id, n.Id, u.Id));

        Assert.Equal(5, result.Converted);
        Assert.Equal(5, result.SoftDeleted);
        Assert.Equal([u.Id], result.UnknownTenants);
        Assert.Empty(result.FailedTenants);
        Assert.False(result.Clean);

        Assert.All(await ShipmentGrantsAsync(e.Admin, catalog), g =>
        {
            Assert.False(g.IsDeleted);
            Assert.Equal(GrantSource.Module, g.GrantSource);
            Assert.Equal(Q358ShipmentGrantRetraction.ModuleCode, g.SourceModuleCode);
        });
        Assert.All(await ShipmentGrantsAsync(n.Admin, catalog), g =>
        {
            Assert.True(g.IsDeleted);
            Assert.Equal("system", g.UpdatedBy);
            Assert.NotNull(g.UpdatedAt);
        });
        Assert.All(await ShipmentGrantsAsync(u.Admin, catalog), g => Assert.Equal((GrantSource.System, false), (g.GrantSource, g.IsDeleted)));
        Assert.All(await ShipmentGrantsAsync(m.Admin, catalog), g => Assert.Equal((GrantSource.Module, false), (g.GrantSource, g.IsDeleted)));
        // Viewer's baseline read and the unrelated key: untouched everywhere.
        foreach (var t in new[] { e, n, u, m })
        {
            Assert.All(await ShipmentGrantsAsync(t.Viewer, catalog), g => Assert.Equal((GrantSource.System, false), (g.GrantSource, g.IsDeleted)));
            var other = await Grants.Find(g => g.RoleId == t.Admin && g.PermissionId == catalog["supplychain.carriers.read"]).SingleAsync();
            Assert.False(other.IsDeleted);
        }

        // Audit: one row per change, written by the system, naming the decision.
        Assert.Equal(5, await Audit.CountDocumentsAsync(a => a.TenantId == e.Id && a.EventName == Q358ShipmentGrantRetraction.ConvertedEventName));
        Assert.Equal(5, await Audit.CountDocumentsAsync(a => a.TenantId == n.Id && a.EventName == Q358ShipmentGrantRetraction.RevokedEventName));
        Assert.Equal(0, await Audit.CountDocumentsAsync(a => a.TenantId == u.Id || a.TenantId == m.Id));
        var row = await Audit.Find(a => a.TenantId == n.Id).FirstAsync();
        using (var meta = JsonDocument.Parse(row.Metadata))
        {
            Assert.Equal(Q358ShipmentGrantRetraction.DecisionRef, meta.RootElement.GetProperty("decision").GetString());
            Assert.Equal("system", meta.RootElement.GetProperty("actor").GetString());
            Assert.Equal(n.Admin, meta.RootElement.GetProperty("roleId").GetGuid());
        }

        Assert.Equal(1, await versions.GetAsync(e.Id, CancellationToken.None));
        Assert.Equal(1, await versions.GetAsync(n.Id, CancellationToken.None));
        Assert.Equal(0, await versions.GetAsync(u.Id, CancellationToken.None));
        Assert.Equal(0, await versions.GetAsync(m.Id, CancellationToken.None));

        // A second run: nothing left to do, nothing written.
        var audit = await Audit.CountDocumentsAsync(FilterDefinition<AuthAuditLog>.Empty);
        var again = await RunAsync(Mode.Apply, Entitlement(e.Id, n.Id, u.Id));
        Assert.Equal(0, again.Converted + again.SoftDeleted + again.NotApplied);
        Assert.Equal(audit, await Audit.CountDocumentsAsync(FilterDefinition<AuthAuditLog>.Empty));
        Assert.Equal(1, await versions.GetAsync(n.Id, CancellationToken.None));
    }

    [OwnedMongoFact]
    public async Task A_soft_deleted_grant_comes_back_when_granted_again_a_live_one_stays_a_duplicate()
    {
        await ownedMongo.ResetAsync();
        var catalog = await CatalogAsync();
        var n = await TenantAsync(catalog);
        await RunAsync(Mode.Apply, Entitlement(Guid.Empty, n.Id));
        var context = new TenantContext();
        context.SetTenant(n.Id);
        var repository = new RolePermissionRepository(Db, context);
        var read = catalog["supplychain.shipments.read"];

        // The tenant buys the module: the entitlement sync's grant revives the soft-deleted document as Module.
        await repository.AssignAsync(RolePermission.ModuleGrant(n.Admin, read, n.Id, "system", Q358ShipmentGrantRetraction.ModuleCode), CancellationToken.None);

        var revived = await Grants.Find(g => g.RoleId == n.Admin && g.PermissionId == read).SingleAsync();
        Assert.False(revived.IsDeleted);
        Assert.Equal(GrantSource.Module, revived.GrantSource);
        Assert.Contains(read, (await repository.GetByRoleAsync(n.Admin, n.Id, CancellationToken.None)).Select(g => g.PermissionId));

        await Assert.ThrowsAsync<DuplicateRolePermissionException>(() => repository.AssignAsync(
            RolePermission.ManualGrant(n.Admin, read, n.Id, "someone"), CancellationToken.None));
        Assert.Equal(GrantSource.Module, (await Grants.Find(g => g.Id == revived.Id).SingleAsync()).GrantSource);
    }

    [OwnedMongoFact]
    public async Task A_run_stopped_after_the_audit_row_touches_no_grant_and_the_next_run_finishes_with_one_row()
    {
        await ownedMongo.ResetAsync();
        var catalog = await CatalogAsync();
        var n = await TenantAsync(catalog);

        var first = await RunAsync(Mode.Apply, Entitlement(Guid.Empty, n.Id),
            _ => throw new InvalidOperationException("stopped here"));

        Assert.Equal([n.Id], first.FailedTenants);
        Assert.All(await ShipmentGrantsAsync(n.Admin, catalog), g => Assert.False(g.IsDeleted));
        Assert.Equal(1, await Audit.CountDocumentsAsync(a => a.TenantId == n.Id));

        var second = await RunAsync(Mode.Apply, Entitlement(Guid.Empty, n.Id));

        Assert.Equal(5, second.SoftDeleted);
        Assert.Empty(second.FailedTenants);
        Assert.Equal(5, await Audit.CountDocumentsAsync(a => a.TenantId == n.Id && a.EventName == Q358ShipmentGrantRetraction.RevokedEventName));
        Assert.Equal(0, await Audit.CountDocumentsAsync(a => a.TenantId == n.Id && a.EventName == Q358ShipmentGrantRetractionRunner.NotAppliedEventName));
    }

    [OwnedMongoFact]
    public async Task A_grant_changed_between_the_audit_row_and_the_write_is_left_as_it_is_and_the_row_withdrawn()
    {
        await ownedMongo.ResetAsync();
        var catalog = await CatalogAsync();
        var n = await TenantAsync(catalog);
        var read = catalog["supplychain.shipments.read"];

        var result = await RunAsync(Mode.Apply, Entitlement(Guid.Empty, n.Id), item =>
            item.Grant.PermissionKey == "supplychain.shipments.read"
                // An administrator makes it a deliberate (Manual) grant in between.
                ? Grants.UpdateOneAsync(g => g.Id == item.Grant.GrantId, Builders<RolePermission>.Update.Set(g => g.GrantSource, GrantSource.Manual))
                : Task.CompletedTask);

        Assert.Equal(4, result.SoftDeleted);
        Assert.Equal(1, result.NotApplied);
        var kept = await Grants.Find(g => g.RoleId == n.Admin && g.PermissionId == read).SingleAsync();
        Assert.Equal((GrantSource.Manual, false), (kept.GrantSource, kept.IsDeleted));
        var withdrawn = await Audit.Find(a => a.TenantId == n.Id && a.EventName == Q358ShipmentGrantRetractionRunner.NotAppliedEventName).SingleAsync();
        using var meta = JsonDocument.Parse(withdrawn.Metadata);
        Assert.Equal(Q358ShipmentGrantRetraction.AuditId(n.Id, n.Admin, "supplychain.shipments.read", Action.SoftDelete),
            meta.RootElement.GetProperty("withdraws").GetGuid());
    }
}
