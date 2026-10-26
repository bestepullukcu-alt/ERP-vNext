using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Diten.AuthService.Application.Tests.Persistence;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Seed;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Action = Diten.AuthService.Domain.Authorization.BaselineGrantRetraction.Action;
using Mode = Diten.AuthService.Domain.Authorization.BaselineGrantRetraction.Mode;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// G7 clean-up (S&amp;OP MOD-0190 + Capacity MOD-0192): each key is decided by the entitlement of ITS OWN module; the
/// Viewer's two .read keys (the read rule, not G7) are never touched; the Q358 and G7 definitions never decide each
/// other's keys; and the Q358 audit ids keep the format a Q358 run already wrote.
/// </summary>
public sealed class G7SandopCapacityGrantRetractionPlanTests
{
    private static readonly BaselineGrantRetraction.Definition G7 = G7SandopCapacityGrantRetraction.Definition;

    private static BaselineGrantRetraction.Grant G(string key, GrantSource source = GrantSource.System)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), key, source);

    [Fact]
    public void Each_key_follows_the_entitlement_of_its_own_module()
    {
        var plan = BaselineGrantRetraction.Plan(G7, G7.AllKeys.Select(k => G(k)), ["Capacity-Planning"]);

        Assert.Equal(8, plan.Count);
        Assert.All(plan.Where(p => p.Grant.PermissionKey.StartsWith("supplychain.capacity-plans.", StringComparison.Ordinal)),
            p => Assert.Equal((Action.ConvertToModule, "capacity-planning"), (p.Action, p.ModuleCode)));
        Assert.All(plan.Where(p => p.Grant.PermissionKey.StartsWith("supplychain.sandop-plans.", StringComparison.Ordinal)),
            p => Assert.Equal((Action.SoftDelete, "sop-workflow-signoffs"), (p.Action, p.ModuleCode)));
    }

    [Fact]
    public void The_two_definitions_never_decide_each_others_keys()
    {
        var shipment = G("supplychain.shipments.read");
        var capacity = G("supplychain.capacity-plans.read");

        Assert.Equal([capacity], BaselineGrantRetraction.Plan(G7, [shipment, capacity], []).Select(p => p.Grant));
        Assert.Equal([shipment], BaselineGrantRetraction.Plan(Q358ShipmentGrantRetraction.Definition, [shipment, capacity], []).Select(p => p.Grant));
        Assert.Empty(G7.AllKeys.Intersect(Q358ShipmentGrantRetraction.Definition.AllKeys));
        Assert.NotEqual(G7.ConfigurationKey, Q358ShipmentGrantRetraction.Definition.ConfigurationKey);
    }

    [Fact]
    public void The_Q358_audit_id_keeps_the_format_a_Q358_run_already_wrote_and_G7_ids_differ()
    {
        var (tenant, role) = (Guid.NewGuid(), Guid.NewGuid());
        const string key = "supplychain.shipments.read";
        var pinned = new Guid(MD5.HashData(Encoding.UTF8.GetBytes($"q358-retraction|SoftDelete|{tenant:N}|{role:N}|{key}")));

        Assert.Equal(pinned, BaselineGrantRetraction.AuditId(Q358ShipmentGrantRetraction.Definition, tenant, role, key, Action.SoftDelete));
        Assert.NotEqual(pinned, BaselineGrantRetraction.AuditId(G7, tenant, role, key, Action.SoftDelete));
    }

    [Fact]
    public void G7_names_the_eight_keys_of_the_owner_decision()
        => Assert.Equal(
            [
                "supplychain.capacity-plans.create", "supplychain.capacity-plans.evaluate", "supplychain.capacity-plans.read",
                "supplychain.capacity-plans.scenario.create", "supplychain.sandop-plans.create", "supplychain.sandop-plans.read",
                "supplychain.sandop-plans.sign-off.record", "supplychain.sandop-plans.snapshot.capture"
            ],
            G7.AllKeys.Order(StringComparer.Ordinal));
}

[Collection(AuthReconciliationMongoCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class G7SandopCapacityGrantRetractionRunnerTests(DisposableAuthMongoReplicaSet ownedMongo)
{
    private static readonly BaselineGrantRetraction.Definition G7 = G7SandopCapacityGrantRetraction.Definition;
    private IMongoDatabase Db => ownedMongo.Database;
    private IMongoCollection<RolePermission> Grants => Db.GetCollection<RolePermission>("rolePermissions");
    private IMongoCollection<AuthAuditLog> Audit => Db.GetCollection<AuthAuditLog>("authAuditLogs");

    private async Task<Dictionary<string, Guid>> CatalogAsync()
    {
        var col = Db.GetCollection<Permission>("permissions");
        var ids = new Dictionary<string, Guid>();
        foreach (var module in G7.Modules)
        foreach (var key in module.Keys.Append("supplychain.shipments.read").Distinct())
        {
            if (ids.ContainsKey(key)) continue;
            var dot = key.LastIndexOf('.');
            var p = new Permission("supplychain", key["supplychain.".Length..dot], key[(dot + 1)..], key, null,
                moduleOverride: G7.ModuleOf(key) ?? Q358ShipmentGrantRetraction.ModuleCode, scope: PermissionScope.Tenant);
            Assert.Equal(key, p.Key);
            await col.InsertOneAsync(p);
            ids[key] = p.Id;
        }

        return ids;
    }

    [OwnedMongoFact]
    public async Task Apply_converts_the_entitled_module_soft_deletes_the_other_and_leaves_Viewer_and_Q358_keys()
    {
        await ownedMongo.ResetAsync();
        var catalog = await CatalogAsync();
        var tenant = Guid.NewGuid();
        var roles = Db.GetCollection<Role>("roles");
        var admin = new Role("Admin", "Admin", "g7 fixture", tenant); admin.MarkAsSystem();
        var viewer = new Role("Viewer", "Viewer", "g7 fixture", tenant); viewer.MarkAsSystem();
        await roles.InsertManyAsync([admin, viewer]);
        // As G7 left it: Admin all eight + a Q358 key as System; Viewer the two .read keys (the read rule) as System.
        foreach (var key in G7.AllKeys.Append("supplychain.shipments.read"))
            await Grants.InsertOneAsync(RolePermission.SystemGrant(admin.Id, catalog[key], tenant, "system"));
        foreach (var key in new[] { "supplychain.sandop-plans.read", "supplychain.capacity-plans.read" })
            await Grants.InsertOneAsync(RolePermission.SystemGrant(viewer.Id, catalog[key], tenant, "system"));

        var result = await Q358ShipmentGrantRetractionRunner.RunAsync(Db, G7, Mode.Apply,
            (_, _) => Task.FromResult<IReadOnlyCollection<string>?>(["CAPACITY-PLANNING"]), NullLogger.Instance);

        Assert.Equal((4, 4, 0), (result.Converted, result.SoftDeleted, result.NotApplied));
        var adminGrants = await Grants.Find(g => g.RoleId == admin.Id).ToListAsync();
        var byKey = adminGrants.ToDictionary(g => catalog.Single(c => c.Value == g.PermissionId).Key);
        foreach (var module in G7.Modules)
        foreach (var key in module.Keys)
        {
            var grant = byKey[key];
            if (module.ModuleCode == G7SandopCapacityGrantRetraction.CapacityModuleCode)
                Assert.Equal((GrantSource.Module, "capacity-planning", false), (grant.GrantSource, grant.SourceModuleCode, grant.IsDeleted));
            else
                Assert.True(grant.IsDeleted);
        }

        Assert.Equal((GrantSource.System, false), (byKey["supplychain.shipments.read"].GrantSource, byKey["supplychain.shipments.read"].IsDeleted));
        Assert.All(await Grants.Find(g => g.RoleId == viewer.Id).ToListAsync(),
            g => Assert.Equal((GrantSource.System, false), (g.GrantSource, g.IsDeleted)));

        var rows = await Audit.Find(a => a.TenantId == tenant).ToListAsync();
        Assert.Equal(8, rows.Count);
        Assert.All(rows, row =>
        {
            using var meta = JsonDocument.Parse(row.Metadata);
            Assert.Equal(G7.DecisionRef, meta.RootElement.GetProperty("decision").GetString());
            Assert.Equal(G7.AuditSource, meta.RootElement.GetProperty("source").GetString());
        });
    }
}
