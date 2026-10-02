using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Settings;
using Diten.AuthService.Persistence.Seed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// BL-452 package 3 — the new <c>auth.users.export</c> key on the REAL seeder, against the test-owned mongod: it is in the
/// catalog as a seeded (system) Tenant key, the tenant Admin template carries it, roles that read users receive it once
/// (and only once — a revoke sticks), and the catalog→Auth sync that the Access Governance manifest now drives refreshes
/// it without taking away its seeded protection. Disposable tenants only.
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class UsersExportPermissionSeedTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private readonly AccountKindAcceptance.AuthTestHost _host;

    public UsersExportPermissionSeedTests(AccountKindAcceptance.AuthTestHost host)
    {
        _host = host;
    }

    private IMongoCollection<Permission> Permissions => _host.Database.GetCollection<Permission>("permissions");
    private IMongoCollection<Role> Roles => _host.Database.GetCollection<Role>("roles");
    private IMongoCollection<RolePermission> Grants => _host.Database.GetCollection<RolePermission>("rolePermissions");

    [Fact]
    public async Task The_export_key_is_a_seeded_tenant_key_the_Admin_template_grants_and_Viewer_does_not()
    {
        var export = await Permissions.Find(p => p.Key == UsersExportGrantBackfill.ExportKey).SingleAsync();

        Assert.True(export.IsSystem);
        Assert.Equal(PermissionScope.Tenant, export.Scope);
        Assert.Equal("access-governance", export.Module);

        var catalog = await Permissions.Find(_ => true).ToListAsync();
        Assert.Contains(DefaultRolePermissionTemplate.SelectFor(DefaultRolePermissionTemplate.AdminRole, catalog), p => p.Key == export.Key);
        Assert.DoesNotContain(DefaultRolePermissionTemplate.SelectFor(DefaultRolePermissionTemplate.ViewerRole, catalog), p => p.Key == export.Key);
    }

    [Fact]
    public async Task Roles_that_read_users_receive_export_once_and_a_second_run_changes_nothing()
    {
        var tenant = Guid.NewGuid();
        var readers = await NewRoleAsync(tenant, "Readers", UsersExportGrantBackfill.ReadKey);
        var auditors = await NewRoleAsync(tenant, "Auditors", UsersExportGrantBackfill.ReadKey);
        var writers = await NewRoleAsync(tenant, "Writers", "auth.users.create");

        await DataSeeder.SeedAsync(_host.Database);

        Assert.True(await HoldsExportAsync(readers));
        Assert.True(await HoldsExportAsync(auditors));
        Assert.False(await HoldsExportAsync(writers));
        var afterFirst = await TenantGrantCountAsync(tenant);

        await DataSeeder.SeedAsync(_host.Database);

        Assert.Equal(afterFirst, await TenantGrantCountAsync(tenant)); // idempotent: no duplicate, no new grant
    }

    [Fact]
    public async Task A_revoke_after_the_backfill_sticks()
    {
        var tenant = Guid.NewGuid();
        var readers = await NewRoleAsync(tenant, "Readers", UsersExportGrantBackfill.ReadKey);
        var auditors = await NewRoleAsync(tenant, "Auditors", UsersExportGrantBackfill.ReadKey);
        await DataSeeder.SeedAsync(_host.Database);
        Assert.True(await HoldsExportAsync(readers));

        // An administrator takes export away from one role (revokes are hard deletes, as in RolePermissionRepository).
        var exportId = (await Permissions.Find(p => p.Key == UsersExportGrantBackfill.ExportKey).SingleAsync()).Id;
        await Grants.DeleteOneAsync(g => g.RoleId == readers && g.PermissionId == exportId);

        await DataSeeder.SeedAsync(_host.Database);

        Assert.False(await HoldsExportAsync(readers)); // not handed back
        Assert.True(await HoldsExportAsync(auditors));
    }

    // v2 — the gate is a persistent mark, not the role state: even when the administrator takes export from EVERY role of
    // the tenant (the v1 edge that re-granted on the next start), the second start does not hand it back.
    [Fact]
    public async Task Export_taken_from_every_role_does_not_come_back_on_the_second_start()
    {
        var tenant = Guid.NewGuid();
        var readers = await NewRoleAsync(tenant, "Readers", UsersExportGrantBackfill.ReadKey);

        await DataSeeder.SeedAsync(_host.Database);
        Assert.True(await HoldsExportAsync(readers));
        var mark = await _host.Database.GetCollection<PermissionReconciliationMark>(PermissionReconciliationMark.CollectionName)
            .Find(m => m.TenantId == tenant && m.Key == UsersExportGrantBackfill.ExportKey).ToListAsync();
        Assert.Single(mark);

        var exportId = (await Permissions.Find(p => p.Key == UsersExportGrantBackfill.ExportKey).SingleAsync()).Id;
        await Grants.DeleteOneAsync(g => g.RoleId == readers && g.PermissionId == exportId); // the only holder

        await DataSeeder.SeedAsync(_host.Database);

        Assert.False(await HoldsExportAsync(readers));
        Assert.Equal(1, await _host.Database.GetCollection<PermissionReconciliationMark>(PermissionReconciliationMark.CollectionName)
            .CountDocumentsAsync(m => m.TenantId == tenant && m.Key == UsersExportGrantBackfill.ExportKey)); // marked once
    }

    // CT acceptance, WP-ROLES-CLOSE-01 correction round — this used to assert the OPPOSITE ("a tenant one of whose roles
    // already holds export is not backfilled"). That guess is what lost roles their export after an interrupted first
    // start, or when Platform's entitlement sync had already handed the key to Admin. Old-or-new is now a persistent
    // mark (ExportGrantBackfill): an unmarked tenant with roles is old and is processed in full. The intent the old
    // test protected — a tenant set up AFTER the key existed is left alone — is S-D in ExportGrantBackfillScenarioTests.
    [Fact]
    public async Task An_old_tenant_one_of_whose_roles_already_exports_still_has_its_other_readers_backfilled()
    {
        var tenant = Guid.NewGuid();
        var exporters = await NewRoleAsync(tenant, "Exporters", UsersExportGrantBackfill.ReadKey, UsersExportGrantBackfill.ExportKey);
        var readers = await NewRoleAsync(tenant, "Readers", UsersExportGrantBackfill.ReadKey);

        await DataSeeder.SeedAsync(_host.Database);

        Assert.True(await HoldsExportAsync(exporters));
        Assert.True(await HoldsExportAsync(readers));
    }

    [Fact]
    public async Task The_catalog_sync_refreshes_the_seeded_key_without_removing_its_protection()
    {
        var key = _host.Factory.Services.GetRequiredService<IOptions<InternalEventAuthSettings>>().Value.ApiKey;
        using var client = _host.Client();
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", key);

        // What Platform's manifest reconcile sends for the USERS page's EXPORT action (route /Users → Tenant).
        var sync = await client.PostAsJsonAsync("internal/permissions/sync", new
        {
            permissionKey = UsersExportGrantBackfill.ExportKey,
            displayName = "Export",
            moduleCode = "ACCESS-GOVERNANCE",
            scope = "Tenant"
        });
        Assert.Equal(HttpStatusCode.OK, sync.StatusCode);
        using (var body = JsonDocument.Parse(await sync.Content.ReadAsStringAsync()))
        {
            Assert.Equal("updated", body.RootElement.GetProperty("status").GetString());
        }

        var after = await Permissions.Find(p => p.Key == UsersExportGrantBackfill.ExportKey).SingleAsync();
        Assert.True(after.IsSystem);                      // still seeded
        Assert.False(after.IsDeleted);
        Assert.Equal(PermissionScope.Tenant, after.Scope);

        // And the catalog can never delete it: a seeded key answers 409 and stays.
        var delete = await client.DeleteAsync($"internal/permissions/{UsersExportGrantBackfill.ExportKey}");
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.False((await Permissions.Find(p => p.Key == UsersExportGrantBackfill.ExportKey).SingleAsync()).IsDeleted);
    }

    private async Task<Guid> NewRoleAsync(Guid tenant, string name, params string[] keys)
    {
        var role = new Role(name, name, "export backfill fixture", tenant);
        await Roles.InsertOneAsync(role);
        foreach (var key in keys)
        {
            var permission = await Permissions.Find(p => p.Key == key).SingleAsync();
            await Grants.InsertOneAsync(RolePermission.ManualGrant(role.Id, permission.Id, tenant, "export-backfill-fixture"));
        }

        return role.Id;
    }

    private async Task<bool> HoldsExportAsync(Guid roleId)
    {
        var exportId = (await Permissions.Find(p => p.Key == UsersExportGrantBackfill.ExportKey).SingleAsync()).Id;
        return await Grants.CountDocumentsAsync(g => g.RoleId == roleId && g.PermissionId == exportId && g.IsDeleted == false) == 1;
    }

    private Task<long> TenantGrantCountAsync(Guid tenant) => Grants.CountDocumentsAsync(g => g.TenantId == tenant);
}
