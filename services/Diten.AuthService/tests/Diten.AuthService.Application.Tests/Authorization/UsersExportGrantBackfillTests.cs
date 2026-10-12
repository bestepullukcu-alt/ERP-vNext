using Diten.AuthService.Domain.Authorization;
using RoleRef = Diten.AuthService.Domain.Authorization.TenantAdminSelfServiceReconciler.RoleRef;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>BL-452 package 3 — the decision core of the one-way export backfill, in isolation (see the seed tests for Mongo).</summary>
public sealed class UsersExportGrantBackfillTests
{
    private static readonly Guid Read = Guid.NewGuid();
    private static readonly Guid Export = Guid.NewGuid();
    private static readonly Guid Create = Guid.NewGuid();

    [Fact]
    public void A_reading_role_gets_export_and_nothing_else_does()
    {
        var tenant = Guid.NewGuid();
        var reader = new RoleRef(Guid.NewGuid(), "Readers", tenant);
        var writer = new RoleRef(Guid.NewGuid(), "Writers", tenant);
        var grants = new HashSet<(Guid, Guid)> { (reader.RoleId, Read), (writer.RoleId, Create) };

        var planned = UsersExportGrantBackfill.PlanMissingGrants([reader, writer], grants, Read, Export, new HashSet<Guid>());

        var grant = Assert.Single(planned);
        Assert.Equal((reader.RoleId, Export, tenant), (grant.RoleId, grant.PermissionId, grant.TenantId));
    }

    [Fact]
    public void Feeding_the_plan_back_plans_nothing()
    {
        var tenant = Guid.NewGuid();
        var reader = new RoleRef(Guid.NewGuid(), "Readers", tenant);
        var grants = new HashSet<(Guid, Guid)> { (reader.RoleId, Read) };

        foreach (var g in UsersExportGrantBackfill.PlanMissingGrants([reader], grants, Read, Export, new HashSet<Guid>()))
        {
            grants.Add((g.RoleId, g.PermissionId));
        }

        Assert.Empty(UsersExportGrantBackfill.PlanMissingGrants([reader], grants, Read, Export, new HashSet<Guid>()));
    }

    [Fact]
    public void A_tenant_already_on_the_export_key_is_left_alone()
    {
        var tenant = Guid.NewGuid();
        var exporter = new RoleRef(Guid.NewGuid(), "Exporters", tenant);
        var reader = new RoleRef(Guid.NewGuid(), "Readers", tenant); // export was taken from it on purpose
        var grants = new HashSet<(Guid, Guid)> { (exporter.RoleId, Read), (exporter.RoleId, Export), (reader.RoleId, Read) };

        var onKey = UsersExportGrantBackfill.TenantsAlreadyOnExport([exporter, reader], grants, Export);

        Assert.Contains(tenant, onKey);
        Assert.Empty(UsersExportGrantBackfill.PlanMissingGrants([exporter, reader], grants, Read, Export, onKey));
    }

    [Fact]
    public void SuperAdmins_automatic_grant_does_not_count_as_a_decision()
    {
        var tenant = Guid.NewGuid();
        var super = new RoleRef(Guid.NewGuid(), "SuperAdmin", tenant);
        var viewer = new RoleRef(Guid.NewGuid(), "Viewer", tenant);
        var grants = new HashSet<(Guid, Guid)> { (super.RoleId, Read), (super.RoleId, Export), (viewer.RoleId, Read) };

        var onKey = UsersExportGrantBackfill.TenantsAlreadyOnExport([super, viewer], grants, Export);

        Assert.DoesNotContain(tenant, onKey);
        var grant = Assert.Single(UsersExportGrantBackfill.PlanMissingGrants([super, viewer], grants, Read, Export, onKey));
        Assert.Equal(viewer.RoleId, grant.RoleId);
    }

    [Fact]
    public void Without_the_export_key_in_the_catalog_no_tenant_is_on_it()
    {
        var role = new RoleRef(Guid.NewGuid(), "Readers", Guid.NewGuid());
        Assert.Empty(UsersExportGrantBackfill.TenantsAlreadyOnExport([role], new HashSet<(Guid, Guid)> { (role.RoleId, Read) }, null));
    }
}
