using System.Text.Json;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Seed;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Roles;

/// <summary>
/// WP-ROLES-CLOSE-01 — the new <c>auth.roles.export</c> key on the REAL seeder, against the test-owned mongod: it is in
/// the catalog as a seeded (system) Tenant key, the tenant Admin template carries it, roles that read roles receive it
/// ONCE per tenant (a second run writes nothing, a revoke sticks), a role without read never receives it, and every
/// grant the backfill makes is an RBAC audit row with the system actor. Disposable tenants only.
/// <para>⚠ The backfill is an AUTHORITY WRITER (role → permission grants made by the system, not by a person).</para>
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class RolesExportPermissionSeedTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private readonly AccountKindAcceptance.AuthTestHost _host;

    public RolesExportPermissionSeedTests(AccountKindAcceptance.AuthTestHost host) => _host = host;

    private IMongoCollection<Permission> Permissions => _host.Database.GetCollection<Permission>("permissions");
    private IMongoCollection<Role> Roles => _host.Database.GetCollection<Role>("roles");
    private IMongoCollection<RolePermission> Grants => _host.Database.GetCollection<RolePermission>("rolePermissions");
    private IMongoCollection<AuthAuditLog> Audit => _host.Database.GetCollection<AuthAuditLog>("authAuditLogs");
    private IMongoCollection<PermissionReconciliationMark> Marks => _host.Database.GetCollection<PermissionReconciliationMark>(PermissionReconciliationMark.CollectionName);

    [Fact]
    public async Task The_export_key_is_a_seeded_tenant_key_the_Admin_template_grants_and_Viewer_does_not()
    {
        var export = await Permissions.Find(p => p.Key == RolesExportGrantBackfill.ExportKey).SingleAsync();

        Assert.True(export.IsSystem);
        Assert.Equal(PermissionScope.Tenant, export.Scope);
        Assert.Equal("access-governance", export.Module);
        Assert.True(DefaultRolePermissionTemplate.IsTenantAssignable(export)); // a tenant administrator can hand it out

        var catalog = await Permissions.Find(_ => true).ToListAsync();
        Assert.Contains(DefaultRolePermissionTemplate.SelectFor(DefaultRolePermissionTemplate.AdminRole, catalog), p => p.Key == export.Key);
        Assert.DoesNotContain(DefaultRolePermissionTemplate.SelectFor(DefaultRolePermissionTemplate.ViewerRole, catalog), p => p.Key == export.Key);
    }

    [Fact]
    public async Task Roles_that_read_roles_receive_export_once_a_role_without_read_does_not_and_a_second_run_writes_nothing()
    {
        var tenant = Guid.NewGuid();
        var readers = await NewRoleAsync(tenant, "Readers", RolesExportGrantBackfill.ReadKey);
        var auditors = await NewRoleAsync(tenant, "Auditors", RolesExportGrantBackfill.ReadKey);
        var writers = await NewRoleAsync(tenant, "Writers", "auth.roles.create"); // no read → no export

        await DataSeeder.SeedAsync(_host.Database);

        Assert.True(await HoldsExportAsync(readers));
        Assert.True(await HoldsExportAsync(auditors));
        Assert.False(await HoldsExportAsync(writers));
        Assert.Single(await Marks.Find(m => m.TenantId == tenant && m.Key == RolesExportGrantBackfill.ExportKey).ToListAsync());
        var grantsAfterFirst = await Grants.CountDocumentsAsync(g => g.TenantId == tenant);
        var auditAfterFirst = await Audit.CountDocumentsAsync(a => a.TenantId == tenant);

        await DataSeeder.SeedAsync(_host.Database);

        Assert.Equal(grantsAfterFirst, await Grants.CountDocumentsAsync(g => g.TenantId == tenant)); // no duplicate, no new grant
        Assert.Equal(auditAfterFirst, await Audit.CountDocumentsAsync(a => a.TenantId == tenant));   // and no new audit row
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == RolesExportGrantBackfill.ExportKey));
    }

    [Fact]
    public async Task Every_backfilled_grant_is_a_system_sourced_grant_and_an_audit_row_with_the_system_actor()
    {
        var tenant = Guid.NewGuid();
        var readers = await NewRoleAsync(tenant, "Readers", RolesExportGrantBackfill.ReadKey);
        var writers = await NewRoleAsync(tenant, "Writers", "auth.roles.create");

        await DataSeeder.SeedAsync(_host.Database);

        var exportId = (await Permissions.Find(p => p.Key == RolesExportGrantBackfill.ExportKey).SingleAsync()).Id;
        var grant = await Grants.Find(g => g.RoleId == readers && g.PermissionId == exportId).SingleAsync();
        Assert.Equal(GrantSource.System, grant.GrantSource);
        Assert.Equal("system", grant.AssignedBy);

        var rows = await Audit.Find(a => a.TenantId == tenant && a.EventName == "role_permission_granted").ToListAsync();
        var row = Assert.Single(rows); // one grant → one row; the role without read left none
        Assert.Equal(Guid.Empty, row.UserId); // no person
        using var metadata = JsonDocument.Parse(row.Metadata);
        Assert.Equal(readers, metadata.RootElement.GetProperty("roleId").GetGuid());
        Assert.Equal("Readers", metadata.RootElement.GetProperty("roleName").GetString());
        Assert.Equal(RolesExportGrantBackfill.ExportKey, metadata.RootElement.GetProperty("permissionKey").GetString());
        Assert.Equal("system", metadata.RootElement.GetProperty("actor").GetString());
        Assert.Equal(RolesExportGrantBackfill.AuditSource, metadata.RootElement.GetProperty("source").GetString());
        Assert.DoesNotContain(writers.ToString(), row.Metadata);
    }

    [Fact]
    public async Task A_revoke_after_the_backfill_sticks_even_when_it_takes_export_from_every_role()
    {
        var tenant = Guid.NewGuid();
        var readers = await NewRoleAsync(tenant, "Readers", RolesExportGrantBackfill.ReadKey);
        await DataSeeder.SeedAsync(_host.Database);
        Assert.True(await HoldsExportAsync(readers));

        // An administrator takes export away (revokes are hard deletes, as in RolePermissionRepository) — the only holder.
        var exportId = (await Permissions.Find(p => p.Key == RolesExportGrantBackfill.ExportKey).SingleAsync()).Id;
        await Grants.DeleteOneAsync(g => g.RoleId == readers && g.PermissionId == exportId);

        await DataSeeder.SeedAsync(_host.Database);

        Assert.False(await HoldsExportAsync(readers)); // not handed back
    }

    [Fact]
    public async Task A_tenant_already_on_the_export_key_is_marked_without_a_grant()
    {
        var tenant = Guid.NewGuid();
        var exporters = await NewRoleAsync(tenant, "Exporters", RolesExportGrantBackfill.ReadKey, RolesExportGrantBackfill.ExportKey);
        var readers = await NewRoleAsync(tenant, "Readers", RolesExportGrantBackfill.ReadKey);

        await DataSeeder.SeedAsync(_host.Database);

        Assert.True(await HoldsExportAsync(exporters));
        Assert.False(await HoldsExportAsync(readers));
        Assert.Empty(await Audit.Find(a => a.TenantId == tenant && a.EventName == "role_permission_granted").ToListAsync());
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == RolesExportGrantBackfill.ExportKey));
    }

    [Fact]
    public async Task One_tenants_backfill_writes_nothing_into_another_tenant()
    {
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        var myReaders = await NewRoleAsync(mine, "Readers", RolesExportGrantBackfill.ReadKey);
        var theirWriters = await NewRoleAsync(theirs, "Writers", "auth.roles.create");

        await DataSeeder.SeedAsync(_host.Database);

        Assert.True(await HoldsExportAsync(myReaders));
        Assert.False(await HoldsExportAsync(theirWriters));
        var exportId = (await Permissions.Find(p => p.Key == RolesExportGrantBackfill.ExportKey).SingleAsync()).Id;
        Assert.Equal(0, await Grants.CountDocumentsAsync(g => g.TenantId == theirs && g.PermissionId == exportId));
        Assert.Equal(0, await Audit.CountDocumentsAsync(a => a.TenantId == theirs && a.EventName == "role_permission_granted"));
    }

    private async Task<Guid> NewRoleAsync(Guid tenant, string name, params string[] keys)
    {
        var role = new Role(name, name, "roles export backfill fixture", tenant);
        await Roles.InsertOneAsync(role);
        foreach (var key in keys)
        {
            var permission = await Permissions.Find(p => p.Key == key).SingleAsync();
            await Grants.InsertOneAsync(RolePermission.ManualGrant(role.Id, permission.Id, tenant, "roles-export-backfill-fixture"));
        }

        return role.Id;
    }

    private async Task<bool> HoldsExportAsync(Guid roleId)
    {
        var exportId = (await Permissions.Find(p => p.Key == RolesExportGrantBackfill.ExportKey).SingleAsync()).Id;
        return await Grants.CountDocumentsAsync(g => g.RoleId == roleId && g.PermissionId == exportId && g.IsDeleted == false) == 1;
    }
}
