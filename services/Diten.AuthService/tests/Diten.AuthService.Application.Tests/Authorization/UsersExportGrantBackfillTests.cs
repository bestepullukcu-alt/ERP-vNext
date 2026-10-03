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

    /// <summary>When the export key entered the catalog.</summary>
    private static readonly DateTimeOffset KeyCreated = new(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Before = KeyCreated.AddDays(-30);
    private static readonly DateTimeOffset After = KeyCreated.AddDays(3);

    // What the default template does for the export keys: Admin gets them, nobody else.
    private static bool TemplateGrants(RoleState role) => role.Name is "Admin" or "SuperAdmin";

    private static RoleState Old(string name, Guid tenant, bool system = false) => new(Guid.NewGuid(), name, tenant, system, Before);

    private static IReadOnlyList<ExportGrantBackfill.TenantPlan> Plan(IEnumerable<RoleState> roles, ISet<(Guid, Guid)> grants, IReadOnlySet<Guid>? marked = null)
        => ExportGrantBackfill.Plan(roles, grants, Read, Export, KeyCreated, marked ?? NoMarks, TemplateGrants);

    // ── old or born: the stored fact ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_tenant_whose_oldest_role_is_older_than_the_key_is_old()
        => Assert.Equal(ExportGrantBackfill.OriginBackfilled, ExportGrantBackfill.Classify(Before, KeyCreated));

    [Fact]
    public void A_tenant_whose_oldest_role_is_newer_than_the_key_is_born()
        => Assert.Equal(ExportGrantBackfill.OriginBorn, ExportGrantBackfill.Classify(After, KeyCreated));

    // Every doubt resolves to NOT granting.
    [Fact]
    public void The_same_instant_is_born()
        => Assert.Equal(ExportGrantBackfill.OriginBorn, ExportGrantBackfill.Classify(KeyCreated, KeyCreated));

    // FIX3 item 2 — the clock-skew allowance: "older" means older than (key − allowance). Both sides of the boundary.
    [Fact]
    public void Older_than_the_key_by_more_than_the_allowance_is_old_and_the_comparison_is_in_UTC()
    {
        var cutoff = KeyCreated - ExportGrantBackfill.ClockSkewAllowance;
        Assert.Equal(ExportGrantBackfill.OriginBackfilled, ExportGrantBackfill.Classify(cutoff.AddTicks(-1), KeyCreated));
        // The same instant written with another offset is still the same instant.
        Assert.Equal(ExportGrantBackfill.OriginBackfilled, ExportGrantBackfill.Classify(cutoff.AddTicks(-1).ToOffset(TimeSpan.FromHours(3)), KeyCreated));
    }

    [Fact]
    public void Inside_the_allowance_is_born()
    {
        var cutoff = KeyCreated - ExportGrantBackfill.ClockSkewAllowance;
        Assert.Equal(ExportGrantBackfill.OriginBorn, ExportGrantBackfill.Classify(cutoff, KeyCreated));                 // exactly at the edge
        Assert.Equal(ExportGrantBackfill.OriginBorn, ExportGrantBackfill.Classify(KeyCreated.AddMinutes(-4), KeyCreated)); // inside
        Assert.Equal(ExportGrantBackfill.OriginBorn, ExportGrantBackfill.Classify(KeyCreated.AddTicks(-1), KeyCreated));   // a tick before the key
        Assert.Equal(TimeSpan.FromMinutes(5), ExportGrantBackfill.ClockSkewAllowance);
    }

    [Fact]
    public void A_missing_timestamp_on_either_side_is_born()
    {
        Assert.Equal(ExportGrantBackfill.OriginBorn, ExportGrantBackfill.Classify(default, KeyCreated));
        Assert.Equal(ExportGrantBackfill.OriginBorn, ExportGrantBackfill.Classify(Before, default));
        Assert.Equal(ExportGrantBackfill.OriginBorn, ExportGrantBackfill.Classify(DateTimeOffset.MinValue, KeyCreated));
    }

    [Fact]
    public void A_role_without_a_date_says_nothing_the_dated_roles_decide_and_with_none_the_tenant_is_born()
    {
        var mixed = Guid.NewGuid();
        var oldRole = new RoleState(Guid.NewGuid(), "Readers", mixed, false, Before);
        var undated = new RoleState(Guid.NewGuid(), "Undated", mixed, false);
        var onlyUndated = new RoleState(Guid.NewGuid(), "Undated", Guid.NewGuid(), false);
        var grants = new HashSet<(Guid, Guid)> { (oldRole.RoleId, Read), (undated.RoleId, Read), (onlyUndated.RoleId, Read) };

        var plans = Plan([oldRole, undated, onlyUndated], grants).ToDictionary(p => p.TenantId);

        Assert.Equal(ExportGrantBackfill.OriginBackfilled, plans[mixed].Origin);            // the dated role proves the tenant is old
        Assert.Equal(ExportGrantBackfill.OriginBorn, plans[onlyUndated.TenantId].Origin);   // nothing dates this one → no grant
        Assert.Empty(plans[onlyUndated.TenantId].Grants);
    }

    [Fact]
    public void A_born_tenant_is_planned_with_no_grant_so_that_it_gets_its_born_mark()
    {
        var tenant = Guid.NewGuid();
        var admin = new RoleState(Guid.NewGuid(), "Admin", tenant, true, After);
        var viewer = new RoleState(Guid.NewGuid(), "Viewer", tenant, true, After);
        var grants = new HashSet<(Guid, Guid)> { (admin.RoleId, Read), (admin.RoleId, Export), (viewer.RoleId, Read) };

        var plan = Assert.Single(Plan([admin, viewer], grants));

        Assert.Equal(ExportGrantBackfill.OriginBorn, plan.Origin);
        Assert.Empty(plan.Grants);
    }

    // The tenant's age is its OLDEST role document — also one that has since been deleted.
    [Fact]
    public void A_deleted_role_dates_the_tenant_but_receives_nothing()
    {
        var tenant = Guid.NewGuid();
        var deletedOld = new RoleState(Guid.NewGuid(), "Gone", tenant, false, Before, IsDeleted: true);
        var liveOld = new RoleState(Guid.NewGuid(), "Readers", tenant, false, Before);
        var grants = new HashSet<(Guid, Guid)> { (deletedOld.RoleId, Read), (liveOld.RoleId, Read) };

        var plan = Assert.Single(Plan([deletedOld, liveOld], grants));

        Assert.Equal(ExportGrantBackfill.OriginBackfilled, plan.Origin);
        Assert.Equal(liveOld.RoleId, Assert.Single(plan.Grants).RoleId); // only the live reader is granted
    }

    // FIX3 item 1 — the ROLE must predate the key too: in an old tenant, a role opened after the key is no candidate.
    [Fact]
    public void In_an_old_tenant_a_role_opened_after_the_key_is_no_candidate()
    {
        var tenant = Guid.NewGuid();
        var oldRole = new RoleState(Guid.NewGuid(), "OldReaders", tenant, false, Before);
        var newRole = new RoleState(Guid.NewGuid(), "NewReaders", tenant, false, After);
        var undated = new RoleState(Guid.NewGuid(), "Undated", tenant, false);
        var grants = new HashSet<(Guid, Guid)> { (oldRole.RoleId, Read), (newRole.RoleId, Read), (undated.RoleId, Read) };

        var plan = Assert.Single(Plan([oldRole, newRole, undated], grants));

        Assert.Equal(ExportGrantBackfill.OriginBackfilled, plan.Origin);
        Assert.Equal(oldRole.RoleId, Assert.Single(plan.Grants).RoleId);
    }

    // … and so must its read grant, when its date is stored.
    [Fact]
    public void An_old_role_whose_read_grant_was_given_after_the_key_is_no_candidate()
    {
        var tenant = Guid.NewGuid();
        var lateRead = new RoleState(Guid.NewGuid(), "LateReaders", tenant, false, Before);
        var earlyRead = new RoleState(Guid.NewGuid(), "EarlyReaders", tenant, false, Before);
        var undatedRead = new RoleState(Guid.NewGuid(), "UndatedRead", tenant, false, Before);
        var defaultRead = new RoleState(Guid.NewGuid(), "DefaultRead", tenant, false, Before);
        var grants = new HashSet<(Guid, Guid)> { (lateRead.RoleId, Read), (earlyRead.RoleId, Read), (undatedRead.RoleId, Read), (defaultRead.RoleId, Read) };
        var readDates = new Dictionary<(Guid, Guid), DateTimeOffset>
        {
            [(lateRead.RoleId, Read)] = After,
            [(earlyRead.RoleId, Read)] = Before,
            [(defaultRead.RoleId, Read)] = default // a stored 0001-01-01 is no date either
            // undatedRead: no stored date → the role's own age decides
        };

        var plan = Assert.Single(ExportGrantBackfill.Plan([lateRead, earlyRead, undatedRead, defaultRead], grants, Read, Export, KeyCreated, NoMarks, TemplateGrants, readDates));

        Assert.Equal(new[] { earlyRead.RoleId, undatedRead.RoleId, defaultRead.RoleId }.OrderBy(x => x), plan.Grants.Select(g => g.RoleId).OrderBy(x => x));
    }

    [Fact]
    public void A_tenant_with_only_deleted_roles_is_still_planned_and_so_marked()
    {
        var tenant = Guid.NewGuid();
        var deleted = new RoleState(Guid.NewGuid(), "Gone", tenant, false, Before, IsDeleted: true);

        var plan = Assert.Single(Plan([deleted], new HashSet<(Guid, Guid)> { (deleted.RoleId, Read) }));

        Assert.Equal(tenant, plan.TenantId);
        Assert.Empty(plan.Grants);
    }

    // ── what an old tenant receives ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_reading_role_gets_export_and_nothing_else_does()
    {
        var tenant = Guid.NewGuid();
        var reader = Old("Readers", tenant);
        var writer = Old("Writers", tenant);
        var grants = new HashSet<(Guid, Guid)> { (reader.RoleId, Read), (writer.RoleId, Create) };

        var plan = Assert.Single(Plan([reader, writer], grants));

        Assert.Equal(tenant, plan.TenantId);
        var grant = Assert.Single(plan.Grants);
        Assert.Equal((reader.RoleId, Export, tenant), (grant.RoleId, grant.PermissionId, grant.TenantId));
    }

    [Fact]
    public void Feeding_the_plan_back_plans_no_grant_but_the_tenant_is_still_to_be_marked()
    {
        var tenant = Guid.NewGuid();
        var reader = Old("Readers", tenant);
        var grants = new HashSet<(Guid, Guid)> { (reader.RoleId, Read) };

        foreach (var g in Plan([reader], grants).SelectMany(p => p.Grants)) grants.Add((g.RoleId, g.PermissionId));

        Assert.Empty(Assert.Single(Plan([reader], grants)).Grants);
    }

    [Fact]
    public void A_marked_tenant_is_not_looked_at_whatever_its_roles_hold()
    {
        var tenant = Guid.NewGuid();
        var reader = Old("Readers", tenant); // export was taken from it on purpose

        Assert.Empty(Plan([reader], new HashSet<(Guid, Guid)> { (reader.RoleId, Read) }, new HashSet<Guid> { tenant }));
    }

    // The heuristic this replaced ("a role of the tenant already holds export, so the tenant is done") skipped exactly
    // these OLD tenants: an Admin that got the key from the template or an entitlement sync before the first run.
    [Fact]
    public void In_an_old_tenant_a_role_that_already_holds_export_does_not_make_the_tenant_done()
    {
        var tenant = Guid.NewGuid();
        var admin = Old("Admin", tenant, system: true);
        var viewer = Old("Viewer", tenant, system: true);
        var readers = Old("Readers", tenant);
        var grants = new HashSet<(Guid, Guid)>
        {
            (admin.RoleId, Read), (admin.RoleId, Export), (viewer.RoleId, Read), (readers.RoleId, Read)
        };

        var plan = Assert.Single(Plan([admin, viewer, readers], grants));

        Assert.Equal(new[] { viewer.RoleId, readers.RoleId }.OrderBy(x => x), plan.Grants.Select(g => g.RoleId).OrderBy(x => x));
    }

    [Fact]
    public void A_role_without_a_tenant_is_never_planned_and_its_empty_tenant_is_never_marked()
    {
        var orphan = Old("Orphans", Guid.Empty);

        Assert.Empty(Plan([orphan], new HashSet<(Guid, Guid)> { (orphan.RoleId, Read) }));
    }

    [Fact]
    public void An_old_tenant_with_no_reading_role_is_still_planned_so_that_it_gets_marked()
    {
        var writer = Old("Writers", Guid.NewGuid());

        var plan = Assert.Single(Plan([writer], new HashSet<(Guid, Guid)> { (writer.RoleId, Create) }));

        Assert.Equal(ExportGrantBackfill.OriginBackfilled, plan.Origin);
        Assert.Empty(plan.Grants);
    }

    // ── the source of a backfilled grant ─────────────────────────────────────────────────────────────────

    [Fact]
    public void The_grant_is_template_managed_only_for_a_system_role_the_template_gives_export_to()
    {
        var tenant = Guid.NewGuid();
        var admin = Old("Admin", tenant, system: true);
        var viewer = Old("Viewer", tenant, system: true);
        var custom = Old("Readers", tenant);
        var customNamedAdmin = Old("Admin", Guid.NewGuid());

        Assert.Equal(GrantSource.System, ExportGrantBackfill.SourceFor(admin, TemplateGrants));
        Assert.Equal(GrantSource.Manual, ExportGrantBackfill.SourceFor(viewer, TemplateGrants));          // nothing re-provisions it
        Assert.Equal(GrantSource.Manual, ExportGrantBackfill.SourceFor(custom, TemplateGrants));          // the administrator's to take away
        Assert.Equal(GrantSource.Manual, ExportGrantBackfill.SourceFor(customNamedAdmin, TemplateGrants));
    }

    [Fact]
    public void Only_a_System_grant_on_a_role_the_template_does_not_serve_needs_its_source_corrected()
    {
        var tenant = Guid.NewGuid();
        var admin = Old("Admin", tenant, system: true);
        var viewer = Old("Viewer", tenant, system: true);
        var custom = Old("Readers", tenant);

        Assert.True(ExportGrantBackfill.NeedsSourceCorrection(viewer, GrantSource.System, TemplateGrants));
        Assert.True(ExportGrantBackfill.NeedsSourceCorrection(custom, GrantSource.System, TemplateGrants));
        Assert.False(ExportGrantBackfill.NeedsSourceCorrection(admin, GrantSource.System, TemplateGrants));  // the template's own
        Assert.False(ExportGrantBackfill.NeedsSourceCorrection(custom, GrantSource.Manual, TemplateGrants)); // already right
        Assert.False(ExportGrantBackfill.NeedsSourceCorrection(custom, GrantSource.Module, TemplateGrants)); // an entitlement's, not ours
    }

    // ── identities ───────────────────────────────────────────────────────────────────────────────────────

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
        Assert.NotEqual(ExportGrantBackfill.AuditId(tenant, role, "auth.roles.export"), ExportGrantBackfill.CorrectionAuditId(tenant, role, "auth.roles.export"));
        Assert.NotEqual(ExportGrantBackfill.AuditId(tenant, role, "auth.roles.export"), ExportGrantBackfill.NotAppliedAuditId(tenant, role, "auth.roles.export"));
        Assert.Equal(ExportGrantBackfill.NotAppliedAuditId(tenant, role, "auth.roles.export"), ExportGrantBackfill.NotAppliedAuditId(tenant, role, "auth.roles.export"));
    }

    [Fact]
    public void Both_export_keys_are_covered_and_name_the_keys_the_endpoints_and_screens_use()
    {
        Assert.Equal(
            [(UsersExportGrantBackfill.ReadKey, UsersExportGrantBackfill.ExportKey), (RolesExportGrantBackfill.ReadKey, RolesExportGrantBackfill.ExportKey)],
            ExportGrantBackfill.Keys.Select(k => (k.ReadKey, k.ExportKey)));
        Assert.Equal(RolesExportGrantBackfill.AuditSource, ExportGrantBackfill.Roles.AuditSource);
        Assert.NotEqual(ExportGrantBackfill.Users.AuditSource, ExportGrantBackfill.Users.CorrectionAuditSource);
    }
}
