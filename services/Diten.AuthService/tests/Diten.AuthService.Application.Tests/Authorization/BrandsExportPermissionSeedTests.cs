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
/// list carries it, and the Admin template does NOT (brand-product-master is not an Admin module).
/// <para>WP-CT-LANE-TKO-MERGE-01 — what the one-way backfill does with it is no longer a second rule of its own: the key is
/// one pair of <see cref="ExportGrantBackfill"/> (<see cref="ExportGrantBackfill.Brands"/>), and every backfill scenario —
/// readers receive export once, a marked tenant is not looked at again, a revoke sticks, its own mark key — runs for it in
/// <c>ExportGrantBackfillScenarioTests</c> ("brands").</para>
/// </summary>
public sealed class BrandsExportPermissionSeedTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private readonly AccountKindAcceptance.AuthTestHost _host;

    public BrandsExportPermissionSeedTests(AccountKindAcceptance.AuthTestHost host)
    {
        _host = host;
    }

    private IMongoCollection<Permission> Permissions => _host.Database.GetCollection<Permission>("permissions");

    [Fact]
    public void The_canonical_catalog_carries_the_export_key_as_a_brand_product_master_tenant_key()
    {
        var export = Assert.Single(DataSeeder.BuildCanonicalPermissions(), p => p.Key == ExportGrantBackfill.Brands.ExportKey);

        Assert.Equal("brand-product-master", export.Module);
        Assert.Equal(PermissionScope.Tenant, export.Scope);
    }

    [Fact]
    public void Tenant97c5s_brand_grant_list_carries_the_export_key()
    {
        var field = typeof(DataSeeder).GetField("BrandProductPermissionKeys", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("DataSeeder.BrandProductPermissionKeys not found.");
        var keys = Assert.IsType<string[]>(field.GetValue(null));

        Assert.Contains(ExportGrantBackfill.Brands.ExportKey, keys);
        Assert.Contains(ExportGrantBackfill.Brands.ReadKey, keys);
    }

    [Fact]
    public async Task The_seeded_export_key_is_a_system_tenant_key_of_brand_product_master_the_Admin_template_does_not_grant()
    {
        var export = await Permissions.Find(p => p.Key == ExportGrantBackfill.Brands.ExportKey).SingleAsync();

        Assert.True(export.IsSystem);
        Assert.False(export.IsDeleted);
        Assert.Equal(PermissionScope.Tenant, export.Scope);
        Assert.Equal("brand-product-master", export.Module);
        Assert.NotEqual(default, export.CreatedAt); // the backfill ages tenants against this stored date
        var catalog = await Permissions.Find(_ => true).ToListAsync();
        Assert.DoesNotContain(DefaultRolePermissionTemplate.SelectFor(DefaultRolePermissionTemplate.AdminRole, catalog),
            p => p.Key == export.Key); // so every backfilled brands grant is Manual: an administrator can take it away
    }

    [Fact]
    public void The_brands_key_is_one_pair_of_the_one_export_backfill()
    {
        Assert.Contains(ExportGrantBackfill.Brands, ExportGrantBackfill.Keys);
        Assert.Equal(("mdm.brands.read", "mdm.brands.export", "brands-export-backfill"),
            (ExportGrantBackfill.Brands.ReadKey, ExportGrantBackfill.Brands.ExportKey, ExportGrantBackfill.Brands.AuditSource));
    }
}
