using Diten.AuthService.Domain.Authorization;
using RoleRef = Diten.AuthService.Domain.Authorization.TenantAdminSelfServiceReconciler.RoleRef;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 (M-4) — the decision core of the one-way <c>mdm.brands.export</c> backfill, in isolation
/// (see <see cref="BrandsExportPermissionSeedTests"/> for the real seeder against Mongo). Mirrors UsersExportGrantBackfillTests.
/// </summary>
public sealed class BrandsExportGrantBackfillTests
{
    private static readonly Guid Read = Guid.NewGuid();
    private static readonly Guid Export = Guid.NewGuid();
    private static readonly Guid Create = Guid.NewGuid();

    [Fact]
    public void The_keys_are_the_brand_read_and_export_keys()
    {
        Assert.Equal("mdm.brands.read", BrandsExportGrantBackfill.ReadKey);
        Assert.Equal("mdm.brands.export", BrandsExportGrantBackfill.ExportKey);
    }

    [Fact]
    public void A_reading_role_gets_export_and_nothing_else_does()
    {
        var tenant = Guid.NewGuid();
        var reader = new RoleRef(Guid.NewGuid(), "Readers", tenant);
        var writer = new RoleRef(Guid.NewGuid(), "Writers", tenant);
        var grants = new HashSet<(Guid, Guid)> { (reader.RoleId, Read), (writer.RoleId, Create) };

        var planned = BrandsExportGrantBackfill.PlanMissingGrants([reader, writer], grants, Read, Export, new HashSet<Guid>());

        var grant = Assert.Single(planned);
        Assert.Equal((reader.RoleId, Export, tenant), (grant.RoleId, grant.PermissionId, grant.TenantId));
    }

    [Fact]
    public void Feeding_the_plan_back_plans_nothing()
    {
        var tenant = Guid.NewGuid();
        var reader = new RoleRef(Guid.NewGuid(), "Readers", tenant);
        var grants = new HashSet<(Guid, Guid)> { (reader.RoleId, Read) };

        foreach (var g in BrandsExportGrantBackfill.PlanMissingGrants([reader], grants, Read, Export, new HashSet<Guid>()))
        {
            grants.Add((g.RoleId, g.PermissionId));
        }

        Assert.Empty(BrandsExportGrantBackfill.PlanMissingGrants([reader], grants, Read, Export, new HashSet<Guid>()));
    }

    [Fact]
    public void A_tenant_already_on_the_export_key_is_left_alone()
    {
        var tenant = Guid.NewGuid();
        var exporter = new RoleRef(Guid.NewGuid(), "Exporters", tenant);
        var reader = new RoleRef(Guid.NewGuid(), "Readers", tenant); // export was taken from it on purpose
        var grants = new HashSet<(Guid, Guid)> { (exporter.RoleId, Read), (exporter.RoleId, Export), (reader.RoleId, Read) };

        var onKey = BrandsExportGrantBackfill.TenantsAlreadyOnExport([exporter, reader], grants, Export);

        Assert.Contains(tenant, onKey);
        Assert.Empty(BrandsExportGrantBackfill.PlanMissingGrants([exporter, reader], grants, Read, Export, onKey));
    }

    [Fact]
    public void SuperAdmins_automatic_grant_does_not_count_as_a_decision()
    {
        var tenant = Guid.NewGuid();
        var super = new RoleRef(Guid.NewGuid(), "SuperAdmin", tenant);
        var viewer = new RoleRef(Guid.NewGuid(), "Viewer", tenant);
        var grants = new HashSet<(Guid, Guid)> { (super.RoleId, Read), (super.RoleId, Export), (viewer.RoleId, Read) };

        var onKey = BrandsExportGrantBackfill.TenantsAlreadyOnExport([super, viewer], grants, Export);

        Assert.DoesNotContain(tenant, onKey);
        var grant = Assert.Single(BrandsExportGrantBackfill.PlanMissingGrants([super, viewer], grants, Read, Export, onKey));
        Assert.Equal(viewer.RoleId, grant.RoleId);
    }

    [Fact]
    public void Without_the_export_key_in_the_catalog_no_tenant_is_on_it()
    {
        var role = new RoleRef(Guid.NewGuid(), "Readers", Guid.NewGuid());
        Assert.Empty(BrandsExportGrantBackfill.TenantsAlreadyOnExport([role], new HashSet<(Guid, Guid)> { (role.RoleId, Read) }, null));
    }

    [Fact]
    public void Only_tenants_not_yet_marked_are_marked()
    {
        var marked = Guid.NewGuid();
        var fresh = Guid.NewGuid();
        var roles = new[]
        {
            new RoleRef(Guid.NewGuid(), "Readers", marked),
            new RoleRef(Guid.NewGuid(), "Readers", fresh),
            new RoleRef(Guid.NewGuid(), "Writers", fresh)
        };

        var toMark = BrandsExportGrantBackfill.TenantsToMark(roles, new HashSet<Guid> { marked });

        Assert.Equal(new HashSet<Guid> { fresh }, toMark);
    }
}
