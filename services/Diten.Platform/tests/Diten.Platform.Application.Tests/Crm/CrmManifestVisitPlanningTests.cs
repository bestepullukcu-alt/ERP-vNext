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
            new[] { "crm.visit-plan.generate", "crm.visit-plan.apply" },
            page.Actions.OrderBy(a => a.SortOrder).Select(a => a.PermissionKey).ToArray());
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
            new[] { "crm.visit-report.record", "crm.visit-report.amend" },
            page.Actions.OrderBy(a => a.SortOrder).Select(a => a.PermissionKey).ToArray());
        Assert.All(page.Actions, a => Assert.True(a.IsRowAction));
    }

    [Fact]
    public void The_module_identity_is_unchanged_and_page_codes_and_sort_orders_stay_unique()
    {
        Assert.Equal("CRM", Manifest.ModuleCode);
        Assert.Equal(Manifest.Pages.Count, Manifest.Pages.Select(p => p.PageCode).Distinct().Count());
        Assert.Equal(Manifest.Pages.Count, Manifest.Pages.Select(p => p.SortOrder).Distinct().Count());
    }
}
