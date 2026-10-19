using Diten.Platform.Application.Features.Crm.SelfRegistration;
using Xunit;

namespace Diten.Platform.Application.Tests.Crm;

// WP-VP-FIX-1 (D7) — the CRM manifest registers the two visit screens that had no sidebar entry: Visit Planning (sort 64,
// right before Planned Visits 65) gated by crm.visit-plan.read, and Visit Execution (sort 66) gated by the canonical
// crm.visit-report.read. Both are nav-visible List pages with their write actions; the module identity is unchanged.
public sealed class CrmManifestVisitPlanningTests
{
    private static readonly Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument Manifest =
        new CrmManifestProvider().GetManifest();

    [Fact]
    public void Visit_planning_is_registered_before_planned_visits_with_generate_and_apply()
    {
        var page = Assert.Single(Manifest.Pages, p => p.PageCode == "VISIT_PLANNING");
        Assert.Equal("/CRM/VisitPlanning", page.RoutePath);
        Assert.Equal("crm.visit-plan.read", page.RequiredPermission);
        Assert.True(page.IsNavigationVisible);
        Assert.Equal("List", page.PageType);
        Assert.Equal(64, page.SortOrder);
        Assert.True(page.SortOrder < Assert.Single(Manifest.Pages, p => p.PageCode == "PLANNED_VISITS").SortOrder);

        Assert.Equal(
            new[] { "crm.visit-plan.generate", "crm.visit-plan.apply", "crm.visit-plan.read-all" },
            page.Actions.OrderBy(a => a.SortOrder).Select(a => a.PermissionKey).ToArray());
    }

    [Fact]
    public void Visit_workspace_is_registered_above_visit_planning()
    {
        // WP-VW-W2 (CT) — the one-calendar workspace sits right above Visit Planning, gated by crm.visit-report.read.
        var page = Assert.Single(Manifest.Pages, p => p.PageCode == "VISIT_WORKSPACE");
        Assert.Equal("/CRM/VisitWorkspace", page.RoutePath);
        Assert.Equal("crm.visit-report.read", page.RequiredPermission);
        Assert.True(page.IsNavigationVisible);
        Assert.Equal(63, page.SortOrder);
        Assert.True(page.SortOrder < Assert.Single(Manifest.Pages, p => p.PageCode == "VISIT_PLANNING").SortOrder);

        // WP-VW-W3 (BE-a) — the visit execution + evidence policy keys live on the workspace page.
        // WP-VW-W3-AUTH — followed by the document repository's three scope-bound visit-evidence keys.
        Assert.Equal(
            new[]
            {
                "crm.visit-execution.read", "crm.visit-execution.record", "crm.visit-evidence-policy.manage",
                "platform.document-repository.visit-evidence.write", "platform.document-repository.visit-evidence.read",
                "platform.document-repository.visit-evidence.read-all"
            },
            page.Actions.OrderBy(a => a.SortOrder).Select(a => a.PermissionKey).ToArray());
    }

    // WP-VW-W3-AUTH — the three visit-evidence keys have their page home on the tenant route /CRM/VisitWorkspace, so the
    // manifest sync carries Scope=Tenant for them (Platform's own route rule). They are the VERBATIM keys the document
    // repository controller enforces (reflected exactly like A1 auto-registration does), and the general
    // platform.document-repository.read / .manage are declared nowhere in the CRM manifest.
    [Fact]
    public void The_visit_evidence_keys_are_declared_on_the_visit_workspace_tenant_route_and_the_general_keys_nowhere()
    {
        var page = Assert.Single(Manifest.Pages, p => p.PageCode == "VISIT_WORKSPACE");
        Assert.Equal("Tenant", Diten.Platform.Application.Features.ModulePages.ModulePageDescriptorNormalizer.ScopeFromRoute(page.RoutePath));

        var expected = new Dictionary<string, string>
        {
            ["EVIDENCE_WRITE"] = Diten.Platform.Application.Features.DocumentRepository.DocumentRepositoryPermissions.VisitEvidenceWrite,
            ["EVIDENCE_READ"] = Diten.Platform.Application.Features.DocumentRepository.DocumentRepositoryPermissions.VisitEvidenceRead,
            ["EVIDENCE_READ_ALL"] = Diten.Platform.Application.Features.DocumentRepository.DocumentRepositoryPermissions.VisitEvidenceReadAll
        };
        var enforced = Diten.Platform.API.Security.HasPermissionReflector.CollectPermissionKeys(
            typeof(Diten.Platform.API.Security.HasPermissionAttribute).Assembly);

        foreach (var (actionCode, key) in expected)
        {
            var action = Assert.Single(page.Actions, a => a.ActionCode == actionCode);
            Assert.Equal(key, action.PermissionKey);
            Assert.False(action.IsDangerous);
            Assert.Contains(key, enforced);
            // Declared on exactly one page: a second (platform-routed) home would trip Auth's most-restrictive tie-break.
            Assert.Single(Manifest.Pages.SelectMany(p => p.Actions), a => a.PermissionKey == key);
        }

        var everyKey = Manifest.Pages.SelectMany(p => p.Actions.Select(a => a.PermissionKey).Append(p.RequiredPermission)).ToList();
        Assert.DoesNotContain("platform.document-repository.read", everyKey);
        Assert.DoesNotContain("platform.document-repository.manage", everyKey);
    }

    [Fact]
    public void Visit_execution_is_registered_after_planned_visits_with_record_and_amend()
    {
        var page = Assert.Single(Manifest.Pages, p => p.PageCode == "VISIT_EXECUTION");
        Assert.Equal("/CRM/VisitExecution", page.RoutePath);
        Assert.Equal("crm.visit-report.read", page.RequiredPermission);
        Assert.True(page.IsNavigationVisible);
        Assert.Equal("List", page.PageType);
        Assert.Equal(66, page.SortOrder);

        Assert.Equal(
            // WP-VW-W4 (W4-BE-a) — + the competitor usage counts (read-aggregate).
            new[] { "crm.visit-report.record", "crm.visit-report.amend", "crm.visit-report.read-aggregate" },
            page.Actions.OrderBy(a => a.SortOrder).Select(a => a.PermissionKey).ToArray());
        Assert.All(page.Actions, a => Assert.True(a.IsRowAction));
    }

    // WP-VP-2 (B-1) — the two explicit-grant-only tenant-wide reads have a manifest home (Toolbar authority).
    [Theory]
    [InlineData("VISIT_PLANNING", "crm.visit-plan.read-all")]
    [InlineData("PLANNED_VISITS", "crm.planned-visit.read-all")]
    public void The_read_all_keys_are_declared_as_page_actions(string pageCode, string key)
    {
        var page = Assert.Single(Manifest.Pages, p => p.PageCode == pageCode);
        var action = Assert.Single(page.Actions, a => a.ActionCode == "READ_ALL");
        Assert.Equal(key, action.PermissionKey);
        Assert.True(action.IsToolbarAction);
        Assert.False(action.IsDangerous);
    }

    [Fact]
    public void The_module_identity_is_unchanged_and_page_codes_and_sort_orders_stay_unique()
    {
        Assert.Equal("CRM", Manifest.ModuleCode);
        Assert.Equal(Manifest.Pages.Count, Manifest.Pages.Select(p => p.PageCode).Distinct().Count());
        Assert.Equal(Manifest.Pages.Count, Manifest.Pages.Select(p => p.SortOrder).Distinct().Count());
    }
}
