using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.SelfRegistration;
using Diten.Platform.Application.Features.WorkAggregation.SelfRegistration;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

// MOD-0024 / İş Raporu — the Work Report moved OUT of the Task Engine manifest into its own module, because the
// tenant sidebar groups by MODULE: no ParentPageCode could take a report out of the "Görev Tanımları / Task
// Settings" group while it was declared there. These assertions pin the two halves of that move — the new module
// identity that makes the menu right, and the identifiers that had to survive it unchanged.
public sealed class WorkReportManifestProviderTests
{
    private static readonly ModuleManifestDocument Manifest = new WorkReportManifestProvider().GetManifest();
    private static readonly ModuleManifestDocument TaskManifest = new TaskManifestProvider().GetManifest();

    [Fact]
    public void Declares_its_own_module_between_the_task_center_and_the_task_settings()
    {
        Assert.Equal("work-report", Manifest.ModuleCode);
        Assert.Equal("Workspace", Manifest.Domain);
        Assert.Equal("DitenPlatform", Manifest.Service);
        Assert.True(Manifest.IsTenantAssignable);
        Assert.False(Manifest.IsBaseline);   // entitlement-gated, like Task Engine and Görev Merkezi

        // Do the work (10) → measure the work (15) → configure the work (20). The three read in that order in
        // the Çalışma Alanı domain group, and this number is the whole reason the middle one is a module.
        Assert.Equal(15, Manifest.SortOrder);
        Assert.True(Manifest.SortOrder > new WorkAggregationManifestProvider().GetManifest().SortOrder);
        Assert.True(Manifest.SortOrder < TaskManifest.SortOrder);
    }

    // Default.cshtml renders a ONE-page module as a flat sidebar link labelled by the MODULE name (IsSinglePage).
    // A second nav-visible page here would turn "İş Raporu" into a collapsible group wrapping a single child —
    // the shape this move exists to avoid.
    [Fact]
    public void Publishes_exactly_one_nav_visible_page_so_the_sidebar_stays_a_flat_link()
    {
        var page = Assert.Single(Manifest.Pages);
        Assert.True(page.IsNavigationVisible);
        Assert.Null(page.ParentPageCode);
    }

    // Page codes key the nav l10n bridge (Nav.Page.TASKWORKREPORT, seven languages), the route is what the tenant
    // has bookmarked, and permission keys are immutable identities in AuthService. A menu move must change none
    // of them — only permission.Module follows the page, and that is a grouping label, not an authority.
    [Fact]
    public void Carries_the_page_code_route_and_permission_keys_over_unchanged()
    {
        var page = Assert.Single(Manifest.Pages);

        Assert.Equal("TASK_WORK_REPORT", page.PageCode);
        Assert.Equal("/Tasks/WorkReport", page.RoutePath);
        Assert.Equal(TaskPermissions.WorkReportRead, page.RequiredPermission);

        var action = Assert.Single(page.Actions!);
        Assert.Equal("VIEW_TENANT_WIDE", action.ActionCode);
        Assert.Equal(TaskPermissions.WorkReportReadTenantWide, action.PermissionKey);
    }

    // The other half of the move: the Task Engine manifest must NOT still declare the report. Two manifests
    // declaring the same page would publish it into both sidebar groups, and the reconciler's orphan prune
    // (which is module-scoped) could never take it out of the settings group.
    [Fact]
    public void The_task_engine_manifest_no_longer_declares_the_report()
    {
        Assert.DoesNotContain(TaskManifest.Pages, p =>
            p.PageCode == "TASK_WORK_REPORT" || p.RoutePath == "/Tasks/WorkReport");
    }
}
