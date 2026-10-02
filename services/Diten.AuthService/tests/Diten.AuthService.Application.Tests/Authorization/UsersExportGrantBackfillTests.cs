using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using RoleState = Diten.AuthService.Domain.Authorization.ExportGrantBackfill.RoleState;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// BL-452 package 3 / WP-ROLES-CLOSE-01 — the decision core of the one-way export backfill, in isolation (the scenario
/// tests drive it on Mongo). The core is the same for every export key, so these run with bare permission ids.
/// </summary>
public sealed class UsersExportGrantBackfillTests
{
    private static readonly Guid Read = Guid.NewGuid();
    private static readonly Guid Export = Guid.NewGuid();
    private static readonly Guid Create = Guid.NewGuid();
    private static readonly IReadOnlySet<Guid> NoMarks = new HashSet<Guid>();

    // What the default template does for the export keys: Admin gets them, nobody else.
    private static bool TemplateGrants(RoleState role) => role.Name is "Admin" or "SuperAdmin";

    [Fact]
    public void A_reading_role_gets_export_and_nothing_else_does()
    {
        var tenant = Guid.NewGuid();
        var reader = new RoleState(Guid.NewGuid(), "Readers", tenant, IsSystem: false);
        var writer = new RoleState(Guid.NewGuid(), "Writers", tenant, IsSystem: false);
        var grants = new HashSet<(Guid, Guid)> { (reader.RoleId, Read), (writer.RoleId, Create) };

        var plan = Assert.Single(ExportGrantBackfill.Plan([reader, writer], grants, Read, Export, NoMarks, TemplateGrants));

        Assert.Equal(tenant, plan.TenantId);
        var grant = Assert.Single(plan.Grants);
        Assert.Equal((reader.RoleId, Export, tenant), (grant.RoleId, grant.PermissionId, grant.TenantId));
    }

    [Fact]
    public void Feeding_the_plan_back_plans_no_grant_but_the_tenant_is_still_to_be_marked()
    {
        var tenant = Guid.NewGuid();
        var reader = new RoleState(Guid.NewGuid(), "Readers", tenant, IsSystem: false);
        var grants = new HashSet<(Guid, Guid)> { (reader.RoleId, Read) };

        foreach (var g in ExportGrantBackfill.Plan([reader], grants, Read, Export, NoMarks, TemplateGrants).SelectMany(p => p.Grants))
        {
            grants.Add((g.RoleId, g.PermissionId));
        }

        var again = Assert.Single(ExportGrantBackfill.Plan([reader], grants, Read, Export, NoMarks, TemplateGrants));
        Assert.Empty(again.Grants);
    }

    [Fact]
    public void A_marked_tenant_is_not_looked_at_whatever_its_roles_hold()
    {
        var tenant = Guid.NewGuid();
        var reader = new RoleState(Guid.NewGuid(), "Readers", tenant, IsSystem: false); // export was taken from it on purpose
        var grants = new HashSet<(Guid, Guid)> { (reader.RoleId, Read) };

        Assert.Empty(ExportGrantBackfill.Plan([reader], grants, Read, Export, new HashSet<Guid> { tenant }, TemplateGrants));
    }

    // The heuristic this replaced ("a role of the tenant already holds export, so the tenant is done") skipped exactly
    // these tenants: an Admin that got the key from the template or an entitlement sync before the first run.
    [Fact]
    public void A_role_that_already_holds_export_does_not_make_the_tenant_done()
    {
        var tenant = Guid.NewGuid();
        var admin = new RoleState(Guid.NewGuid(), "Admin", tenant, IsSystem: true);
        var viewer = new RoleState(Guid.NewGuid(), "Viewer", tenant, IsSystem: true);
        var readers = new RoleState(Guid.NewGuid(), "Readers", tenant, IsSystem: false);
        var grants = new HashSet<(Guid, Guid)>
        {
            (admin.RoleId, Read), (admin.RoleId, Export), (viewer.RoleId, Read), (readers.RoleId, Read)
        };

        var plan = Assert.Single(ExportGrantBackfill.Plan([admin, viewer, readers], grants, Read, Export, NoMarks, TemplateGrants));

        Assert.Equal(new[] { viewer.RoleId, readers.RoleId }.OrderBy(x => x), plan.Grants.Select(g => g.RoleId).OrderBy(x => x));
    }

    [Fact]
    public void A_role_without_a_tenant_is_never_planned_and_its_empty_tenant_is_never_marked()
    {
        var orphan = new RoleState(Guid.NewGuid(), "Orphans", Guid.Empty, IsSystem: false);
        var grants = new HashSet<(Guid, Guid)> { (orphan.RoleId, Read) };

        Assert.Empty(ExportGrantBackfill.Plan([orphan], grants, Read, Export, NoMarks, TemplateGrants));
    }

    [Fact]
    public void A_tenant_with_no_reading_role_is_still_planned_so_that_it_gets_marked()
    {
        var writer = new RoleState(Guid.NewGuid(), "Writers", Guid.NewGuid(), IsSystem: false);

        var plan = Assert.Single(ExportGrantBackfill.Plan([writer], new HashSet<(Guid, Guid)> { (writer.RoleId, Create) }, Read, Export, NoMarks, TemplateGrants));

        Assert.Empty(plan.Grants);
    }

    [Fact]
    public void The_grant_is_template_managed_only_for_a_system_role_the_template_gives_export_to()
    {
        var tenant = Guid.NewGuid();
        var admin = new RoleState(Guid.NewGuid(), "Admin", tenant, IsSystem: true);
        var viewer = new RoleState(Guid.NewGuid(), "Viewer", tenant, IsSystem: true);
        var custom = new RoleState(Guid.NewGuid(), "Readers", tenant, IsSystem: false);
        var customNamedAdmin = new RoleState(Guid.NewGuid(), "Admin", Guid.NewGuid(), IsSystem: false);

        Assert.Equal(GrantSource.System, ExportGrantBackfill.SourceFor(admin, TemplateGrants));
        Assert.Equal(GrantSource.Manual, ExportGrantBackfill.SourceFor(viewer, TemplateGrants));          // nothing re-provisions it
        Assert.Equal(GrantSource.Manual, ExportGrantBackfill.SourceFor(custom, TemplateGrants));          // the administrator's to take away
        Assert.Equal(GrantSource.Manual, ExportGrantBackfill.SourceFor(customNamedAdmin, TemplateGrants));
    }

    [Fact]
    public void Mark_and_audit_ids_are_the_same_for_every_writer_and_differ_per_tenant_role_and_key()
    {
        var tenant = Guid.NewGuid();
        var role = Guid.NewGuid();

        Assert.Equal(ExportGrantBackfill.MarkId(tenant, "auth.roles.export"), ExportGrantBackfill.MarkId(tenant, "auth.roles.export"));
        Assert.NotEqual(ExportGrantBackfill.MarkId(tenant, "auth.roles.export"), ExportGrantBackfill.MarkId(tenant, "auth.users.export"));
        Assert.NotEqual(ExportGrantBackfill.MarkId(tenant, "auth.roles.export"), ExportGrantBackfill.MarkId(Guid.NewGuid(), "auth.roles.export"));
        Assert.Equal(ExportGrantBackfill.AuditId(tenant, role, "auth.roles.export"), ExportGrantBackfill.AuditId(tenant, role, "auth.roles.export"));
        Assert.NotEqual(ExportGrantBackfill.AuditId(tenant, role, "auth.roles.export"), ExportGrantBackfill.AuditId(tenant, Guid.NewGuid(), "auth.roles.export"));
        Assert.NotEqual(ExportGrantBackfill.AuditId(tenant, role, "auth.roles.export"), ExportGrantBackfill.AuditId(tenant, role, "auth.users.export"));
    }

    [Fact]
    public void Both_export_keys_are_covered_and_name_the_keys_the_endpoints_and_screens_use()
    {
        Assert.Equal(
            [(UsersExportGrantBackfill.ReadKey, UsersExportGrantBackfill.ExportKey), (RolesExportGrantBackfill.ReadKey, RolesExportGrantBackfill.ExportKey)],
            ExportGrantBackfill.Keys.Select(k => (k.ReadKey, k.ExportKey)));
        Assert.Equal(RolesExportGrantBackfill.AuditSource, ExportGrantBackfill.Roles.AuditSource);
    }
}
