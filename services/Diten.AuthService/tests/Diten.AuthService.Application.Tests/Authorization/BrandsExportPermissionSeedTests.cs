using System.Reflection;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Seed;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 (M-4) — the new <c>mdm.brands.export</c> key on the REAL seeder, against the test-owned mongod:
/// it is in the catalog as a seeded (system) Tenant key of module <c>brand-product-master</c>, tenant-97c5's brand grant
/// list carries it, roles that read brands receive it once (and only once — a revoke sticks), and its gate is its OWN
/// mark key. Mirrors UsersExportPermissionSeedTests. Disposable tenants only.
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class BrandsExportPermissionSeedTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private readonly AccountKindAcceptance.AuthTestHost _host;

    public BrandsExportPermissionSeedTests(AccountKindAcceptance.AuthTestHost host)
    {
        _host = host;
    }

    private IMongoCollection<Permission> Permissions => _host.Database.GetCollection<Permission>("permissions");
    private IMongoCollection<Role> Roles => _host.Database.GetCollection<Role>("roles");
    private IMongoCollection<RolePermission> Grants => _host.Database.GetCollection<RolePermission>("rolePermissions");
    private IMongoCollection<PermissionReconciliationMark> Marks =>
        _host.Database.GetCollection<PermissionReconciliationMark>(PermissionReconciliationMark.CollectionName);

    [Fact]
    public void The_canonical_catalog_carries_the_export_key_as_a_brand_product_master_tenant_key()
    {
        var export = Assert.Single(DataSeeder.BuildCanonicalPermissions(), p => p.Key == BrandsExportGrantBackfill.ExportKey);

        Assert.Equal("brand-product-master", export.Module);
        Assert.Equal(PermissionScope.Tenant, export.Scope);
    }

    [Fact]
    public void Tenant97c5s_brand_grant_list_carries_the_export_key()
    {
        var field = typeof(DataSeeder).GetField("BrandProductPermissionKeys", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("DataSeeder.BrandProductPermissionKeys not found.");
        var keys = Assert.IsType<string[]>(field.GetValue(null));

        Assert.Contains(BrandsExportGrantBackfill.ExportKey, keys);
        Assert.Contains(BrandsExportGrantBackfill.ReadKey, keys);
    }

    [Fact]
    public async Task The_seeded_export_key_is_a_system_tenant_key_of_brand_product_master()
    {
        var export = await Permissions.Find(p => p.Key == BrandsExportGrantBackfill.ExportKey).SingleAsync();

        Assert.True(export.IsSystem);
        Assert.False(export.IsDeleted);
        Assert.Equal(PermissionScope.Tenant, export.Scope);
        Assert.Equal("brand-product-master", export.Module);
    }

    [Fact]
    public async Task Roles_that_read_brands_receive_export_once_and_a_second_run_changes_nothing()
    {
        var tenant = Guid.NewGuid();
        var readers = await NewRoleAsync(tenant, "Readers", BrandsExportGrantBackfill.ReadKey);
        var auditors = await NewRoleAsync(tenant, "Auditors", BrandsExportGrantBackfill.ReadKey);
        var writers = await NewRoleAsync(tenant, "Writers", "mdm.brands.create");

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
        var readers = await NewRoleAsync(tenant, "Readers", BrandsExportGrantBackfill.ReadKey);
        var auditors = await NewRoleAsync(tenant, "Auditors", BrandsExportGrantBackfill.ReadKey);
        await DataSeeder.SeedAsync(_host.Database);
        Assert.True(await HoldsExportAsync(readers));

        // An administrator takes export away from one role (revokes are hard deletes, as in RolePermissionRepository).
        var exportId = (await Permissions.Find(p => p.Key == BrandsExportGrantBackfill.ExportKey).SingleAsync()).Id;
        await Grants.DeleteOneAsync(g => g.RoleId == readers && g.PermissionId == exportId);

        await DataSeeder.SeedAsync(_host.Database);

        Assert.False(await HoldsExportAsync(readers)); // not handed back
        Assert.True(await HoldsExportAsync(auditors));
    }

    // The gate is a persistent mark, not the role state: even when the administrator takes export from EVERY role of the
    // tenant, the second start does not hand it back.
    [Fact]
    public async Task Export_taken_from_every_role_does_not_come_back_on_the_second_start()
    {
        var tenant = Guid.NewGuid();
        var readers = await NewRoleAsync(tenant, "Readers", BrandsExportGrantBackfill.ReadKey);

        await DataSeeder.SeedAsync(_host.Database);
        Assert.True(await HoldsExportAsync(readers));
        Assert.Single(await Marks.Find(m => m.TenantId == tenant && m.Key == BrandsExportGrantBackfill.ExportKey).ToListAsync());

        var exportId = (await Permissions.Find(p => p.Key == BrandsExportGrantBackfill.ExportKey).SingleAsync()).Id;
        await Grants.DeleteOneAsync(g => g.RoleId == readers && g.PermissionId == exportId); // the only holder

        await DataSeeder.SeedAsync(_host.Database);

        Assert.False(await HoldsExportAsync(readers));
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == BrandsExportGrantBackfill.ExportKey)); // marked once
    }

    // A tenant marked BEFORE the run (e.g. an earlier start already backfilled it) is never looked at again.
    [Fact]
    public async Task A_tenant_already_marked_is_not_granted()
    {
        var tenant = Guid.NewGuid();
        var readers = await NewRoleAsync(tenant, "Readers", BrandsExportGrantBackfill.ReadKey);
        await Marks.InsertOneAsync(new PermissionReconciliationMark
        {
            TenantId = tenant, Key = BrandsExportGrantBackfill.ExportKey, ReconciledAtUtc = DateTime.UtcNow
        });

        await DataSeeder.SeedAsync(_host.Database);

        Assert.False(await HoldsExportAsync(readers));
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == BrandsExportGrantBackfill.ExportKey));
    }

    // The brands gate is its OWN mark key: a tenant that already went through the Users export backfill is still
    // backfilled for brands.
    [Fact]
    public async Task The_users_export_mark_does_not_gate_the_brands_backfill()
    {
        var tenant = Guid.NewGuid();
        var readers = await NewRoleAsync(tenant, "Readers", BrandsExportGrantBackfill.ReadKey);
        await Marks.InsertOneAsync(new PermissionReconciliationMark
        {
            TenantId = tenant, Key = UsersExportGrantBackfill.ExportKey, ReconciledAtUtc = DateTime.UtcNow
        });

        await DataSeeder.SeedAsync(_host.Database);

        Assert.True(await HoldsExportAsync(readers));
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == BrandsExportGrantBackfill.ExportKey));
    }

    [Fact]
    public async Task A_tenant_already_on_the_export_key_is_not_backfilled()
    {
        var tenant = Guid.NewGuid();
        var exporters = await NewRoleAsync(tenant, "Exporters", BrandsExportGrantBackfill.ReadKey, BrandsExportGrantBackfill.ExportKey);
        var readers = await NewRoleAsync(tenant, "Readers", BrandsExportGrantBackfill.ReadKey);

        await DataSeeder.SeedAsync(_host.Database);

        Assert.True(await HoldsExportAsync(exporters));
        Assert.False(await HoldsExportAsync(readers));
    }

    private async Task<Guid> NewRoleAsync(Guid tenant, string name, params string[] keys)
    {
        var role = new Role(name, name, "brands export backfill fixture", tenant);
        await Roles.InsertOneAsync(role);
        foreach (var key in keys)
        {
            var permission = await Permissions.Find(p => p.Key == key).SingleAsync();
            await Grants.InsertOneAsync(RolePermission.ManualGrant(role.Id, permission.Id, tenant, "brands-export-backfill-fixture"));
        }

        return role.Id;
    }

    private async Task<bool> HoldsExportAsync(Guid roleId)
    {
        var exportId = (await Permissions.Find(p => p.Key == BrandsExportGrantBackfill.ExportKey).SingleAsync()).Id;
        return await Grants.CountDocumentsAsync(g => g.RoleId == roleId && g.PermissionId == exportId && g.IsDeleted == false) == 1;
    }

    private Task<long> TenantGrantCountAsync(Guid tenant) => Grants.CountDocumentsAsync(g => g.TenantId == tenant);
}
