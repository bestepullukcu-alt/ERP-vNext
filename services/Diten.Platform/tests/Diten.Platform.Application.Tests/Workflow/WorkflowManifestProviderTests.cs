using System.Reflection;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.SelfRegistration;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

// MC-3b PILOT — the workflow manifest must mirror the real controller permissions verbatim (zero drift) and be
// internally consistent (unique routes/page-codes/action-codes) so the reconcile upserts every page/action.
public sealed class WorkflowManifestProviderTests
{
    private static readonly HashSet<string> KnownPermissions = typeof(WorkflowPermissions)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    private static readonly Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument Manifest =
        new WorkflowManifestProvider().GetManifest();

    [Fact]
    public void Declares_clean_slug_module_identity()
    {
        Assert.Equal("workflow", Manifest.ModuleCode);
        Assert.Equal("Workflow", Manifest.DisplayName);
        Assert.True(Manifest.IsTenantAssignable);
        Assert.NotEmpty(Manifest.Pages);
    }

    [Fact]
    public void Every_permission_is_a_real_WorkflowPermissions_constant()
    {
        Assert.NotEmpty(KnownPermissions);
        foreach (var page in Manifest.Pages)
        {
            Assert.Contains(page.RequiredPermission, KnownPermissions);
            foreach (var action in page.Actions)
            {
                Assert.Contains(action.PermissionKey, KnownPermissions);
            }
        }
    }

    [Fact]
    public void Page_routes_and_codes_are_unique_so_reconcile_does_not_skip()
    {
        var routes = Manifest.Pages.Select(p => p.RoutePath).ToList();
        Assert.Equal(routes.Count, routes.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        var codes = Manifest.Pages.Select(p => p.PageCode).ToList();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var page in Manifest.Pages)
        {
            var actionCodes = page.Actions.Select(a => a.ActionCode).ToList();
            Assert.Equal(actionCodes.Count, actionCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    [Fact]
    public void Definitions_is_the_navigable_entry_at_the_real_route()
    {
        var definitions = Assert.Single(Manifest.Pages, p => p.PageCode == "DEFINITIONS");
        Assert.True(definitions.IsNavigationVisible);
        Assert.Equal("/Platform/Workflow", definitions.RoutePath);
        Assert.Equal(WorkflowPermissions.DefinitionsView, definitions.RequiredPermission);
    }

    [Fact]
    public void Task_inbox_exposes_the_real_task_actions()
    {
        var tasks = Assert.Single(Manifest.Pages, p => p.PageCode == "TASKS");
        var keys = tasks.Actions.Select(a => a.PermissionKey).ToHashSet();
        Assert.Contains(WorkflowPermissions.TasksApprove, keys);
        Assert.Contains(WorkflowPermissions.TasksReject, keys);
        Assert.Contains(WorkflowPermissions.TasksDelegate, keys);
        Assert.Contains(WorkflowPermissions.TasksRequestInfo, keys);
        Assert.Contains(WorkflowPermissions.TasksCancel, keys);
    }

    // ── §3 completeness (bidirectional) ───────────────────────────────────────
    // The REAL frontend WorkflowController view-routes (Diten.Web). Kept verbatim here because the Platform
    // test assembly cannot reference the frontend project. Deleting a page from the manifest (or adding a real
    // route without a page) breaks the SetEquals below — proving the mirror stays complete in BOTH directions.
    private static readonly HashSet<string> FrontendViewRoutes = new(StringComparer.Ordinal)
    {
        "/Platform/Workflow",
        "/Platform/Workflow/Definitions/{id}/Designer",
        "/Platform/Workflow/Definitions/{id}/VisualDesigner",
        "/Platform/Workflow/Definitions/{id}/Versions",
        "/Platform/Workflow/Definitions/{id}/Instances",
        "/Platform/Workflow/Definitions/{id}/Tasks",
        "/Platform/Workflow/Definitions/{id}/SlaRules",
        "/Platform/Workflow/Escalations",
        "/Platform/Workflow/TransitionGate"
    };

    [Fact]
    public void Every_frontend_view_route_has_exactly_one_manifest_page_and_vice_versa()
    {
        var manifestRoutes = Manifest.Pages.Select(p => p.RoutePath).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(FrontendViewRoutes.Count, Manifest.Pages.Count); // no extra/duplicate pages
        Assert.True(manifestRoutes.SetEquals(FrontendViewRoutes),
            "Manifest pages must mirror the frontend view-routes exactly (both directions).");
    }

    [Fact]
    public void Definitions_row_menu_buttons_each_have_a_manifest_action()
    {
        // The real Definitions row/toolbar buttons that invoke an action (navigation links are pages, not actions):
        // Create (manage), Publish (publish), Start Instance (instances.start).
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            WorkflowPermissions.DefinitionsManage,
            WorkflowPermissions.DefinitionsPublish,
            WorkflowPermissions.InstancesStart
        };
        var definitions = Assert.Single(Manifest.Pages, p => p.PageCode == "DEFINITIONS");
        var actionKeys = definitions.Actions.Select(a => a.PermissionKey).ToHashSet(StringComparer.Ordinal);

        Assert.True(actionKeys.SetEquals(expected),
            "Definitions actions must mirror the real row/toolbar menu (Create, Publish, Start Instance).");
    }

    [Fact]
    public void Start_instance_is_a_definitions_row_action_not_on_instances()
    {
        var definitions = Assert.Single(Manifest.Pages, p => p.PageCode == "DEFINITIONS");
        var start = Assert.Single(definitions.Actions, a => a.PermissionKey == WorkflowPermissions.InstancesStart);
        Assert.True(start.IsRowAction);

        // Instances list is view-only.
        var instances = Assert.Single(Manifest.Pages, p => p.PageCode == "INSTANCES");
        Assert.Empty(instances.Actions);
    }
}
