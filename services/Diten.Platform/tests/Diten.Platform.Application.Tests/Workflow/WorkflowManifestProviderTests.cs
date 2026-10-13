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

        foreach (var api in Manifest.ApiPermissions ?? [])
        {
            Assert.Contains(api.PermissionKey, KnownPermissions);
        }
    }

    // WP-0023-FX — the RUNTIME keys (start, view instances/tasks, act on a task) are declared ONLY as Tenant API
    // permissions. A runtime key on any /Platform page or action would make the catalog sync keep it PlatformAdmin
    // (most restrictive wins), and a QA role could again not be granted it.
    private static readonly string[] RuntimeKeys =
    [
        WorkflowPermissions.InstancesStart, WorkflowPermissions.InstancesView, WorkflowPermissions.TasksApprove,
        WorkflowPermissions.TasksReject, WorkflowPermissions.TasksDelegate, WorkflowPermissions.TasksRequestInfo,
        WorkflowPermissions.TasksCancel
    ];

    [Fact]
    public void Fx_runtime_keys_are_exactly_the_Tenant_api_permissions_and_on_no_page_or_action()
    {
        var api = (Manifest.ApiPermissions ?? []).ToList();
        Assert.Equal(RuntimeKeys.OrderBy(k => k, StringComparer.Ordinal), api.Select(p => p.PermissionKey).OrderBy(k => k, StringComparer.Ordinal));
        Assert.All(api, p => Assert.Equal("Tenant", p.Scope));

        var onPages = Manifest.Pages
            .SelectMany(p => p.Actions.Select(a => a.PermissionKey).Append(p.RequiredPermission))
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(RuntimeKeys, key => Assert.DoesNotContain(key, onPages));
    }

    [Fact]
    public void Fx_every_WorkflowPermissions_key_is_declared_once_as_a_page_key_or_an_api_permission()
    {
        var declared = Manifest.Pages
            .SelectMany(p => p.Actions.Select(a => a.PermissionKey).Append(p.RequiredPermission))
            .Concat((Manifest.ApiPermissions ?? []).Select(p => p.PermissionKey))
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(declared.SetEquals(KnownPermissions),
            "Every WorkflowPermissions key must be declared by the manifest (page, action or API permission) and nothing else.");
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
    public void Task_page_under_the_designer_is_a_design_page_and_its_task_actions_are_api_permissions()
    {
        // WP-0023-FX — the approve/reject/delegate/request-info/cancel keys moved to ApiPermissions (Tenant); the
        // /Platform Tasks page is gated by the design read key and carries no action.
        var tasks = Assert.Single(Manifest.Pages, p => p.PageCode == "TASKS");
        Assert.Equal(WorkflowPermissions.DefinitionsView, tasks.RequiredPermission);
        Assert.Empty(tasks.Actions);

        var api = (Manifest.ApiPermissions ?? []).Select(p => p.PermissionKey).ToHashSet();
        Assert.Contains(WorkflowPermissions.TasksApprove, api);
        Assert.Contains(WorkflowPermissions.TasksReject, api);
        Assert.Contains(WorkflowPermissions.TasksDelegate, api);
        Assert.Contains(WorkflowPermissions.TasksRequestInfo, api);
        Assert.Contains(WorkflowPermissions.TasksCancel, api);
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
        // The real Definitions row/toolbar buttons that invoke a DESIGN action: Create (manage), Publish (publish).
        // The Start Instance button is gated by the runtime key instances.start, an API permission (WP-0023-FX).
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            WorkflowPermissions.DefinitionsManage,
            WorkflowPermissions.DefinitionsPublish
        };
        var definitions = Assert.Single(Manifest.Pages, p => p.PageCode == "DEFINITIONS");
        var actionKeys = definitions.Actions.Select(a => a.PermissionKey).ToHashSet(StringComparer.Ordinal);

        Assert.True(actionKeys.SetEquals(expected),
            "Definitions actions must mirror the real design row/toolbar menu (Create, Publish).");
    }

    [Fact]
    public void Start_instance_is_a_Tenant_api_permission_and_the_instances_page_is_a_view_only_design_page()
    {
        var start = Assert.Single(Manifest.ApiPermissions ?? [], p => p.PermissionKey == WorkflowPermissions.InstancesStart);
        Assert.Equal("Tenant", start.Scope);

        var instances = Assert.Single(Manifest.Pages, p => p.PageCode == "INSTANCES");
        Assert.Equal(WorkflowPermissions.DefinitionsView, instances.RequiredPermission);
        Assert.Empty(instances.Actions);
    }
}
