using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.Workflow.SelfRegistration;

/// <summary>
/// MC-3b PILOT — the Workflow module's self-registration manifest. Mirrors the REAL WorkflowDefinitionsController
/// routes and <see cref="WorkflowPermissions"/> keys (zero drift): every RequiredPermission / PermissionKey below
/// is a WorkflowPermissions constant, so the permission grammar (platform.workflow.*) is declared verbatim and the
/// ModuleCode stays a clean slug ("workflow") — the reconcile does NOT force ModuleCode↔permission mapping.
/// SOFT fields (Domain/Service/DisplayName/SortOrder/IsTenantAssignable) are operator-owned after first seed.
/// </summary>
public sealed class WorkflowManifestProvider : IModuleManifestProvider
{
    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "workflow",
            ModuleName: "Workflow",
            DisplayName: "Workflow",
            Domain: "PlatformSharedServices",
            Service: "DitenPlatform",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 200,
            Icon: "bx-git-merge", // FIX-MODULE-ICON — default sidebar icon (SOFT; operator can override in catalog).
            Pages:
            [
                // Definitions — the navigable entry (gated like the tenant nav: definitions.view).
                new ModuleManifestPage(
                    PageCode: "DEFINITIONS",
                    DisplayName: "Workflow Definitions",
                    RoutePath: "/Platform/Workflow",
                    RequiredPermission: WorkflowPermissions.DefinitionsView,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    // Mirror the Definitions row menu: Create (toolbar), Publish + Start Instance (row actions).
                    // Start Instance is a ROW action on the Definitions list (UI), even though the API is POST /instances.
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create Definition", WorkflowPermissions.DefinitionsManage, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("PUBLISH", "Publish", WorkflowPermissions.DefinitionsPublish, "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("START_INSTANCE", "Start Instance", WorkflowPermissions.InstancesStart, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),

                // UI-only sub-pages reached from a Definitions row (Designer / Visual Designer / Versions).
                new ModuleManifestPage(
                    PageCode: "DESIGNER",
                    DisplayName: "Designer",
                    RoutePath: "/Platform/Workflow/Definitions/{id}/Designer",
                    RequiredPermission: WorkflowPermissions.DefinitionsManage,
                    ParentPageCode: "DEFINITIONS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 11,
                    Actions: []),

                new ModuleManifestPage(
                    PageCode: "VISUAL_DESIGNER",
                    DisplayName: "Visual Designer",
                    RoutePath: "/Platform/Workflow/Definitions/{id}/VisualDesigner",
                    RequiredPermission: WorkflowPermissions.DefinitionsManage,
                    ParentPageCode: "DEFINITIONS",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 12,
                    Actions: []),

                new ModuleManifestPage(
                    PageCode: "VERSIONS",
                    DisplayName: "Versions",
                    RoutePath: "/Platform/Workflow/Definitions/{id}/Versions",
                    RequiredPermission: WorkflowPermissions.DefinitionsView,
                    ParentPageCode: "DEFINITIONS",
                    IsNavigationVisible: false,
                    PageType: "List",
                    SortOrder: 13,
                    Actions: []),

                // Instances list — view-only (Start Instance lives on the Definitions row, above).
                new ModuleManifestPage(
                    PageCode: "INSTANCES",
                    DisplayName: "Workflow Instances",
                    RoutePath: "/Platform/Workflow/Definitions/{id}/Instances",
                    RequiredPermission: WorkflowPermissions.InstancesView,
                    ParentPageCode: "DEFINITIONS",
                    IsNavigationVisible: false,
                    PageType: "List",
                    SortOrder: 20,
                    Actions: []),

                // Task inbox — the actionable approvals (approve/reject/delegate/request-info/cancel).
                new ModuleManifestPage(
                    PageCode: "TASKS",
                    DisplayName: "My Tasks",
                    RoutePath: "/Platform/Workflow/Definitions/{id}/Tasks",
                    RequiredPermission: WorkflowPermissions.InstancesView,
                    ParentPageCode: "DEFINITIONS",
                    IsNavigationVisible: false,
                    PageType: "List",
                    SortOrder: 30,
                    Actions:
                    [
                        new ModuleManifestAction("APPROVE", "Approve", WorkflowPermissions.TasksApprove, "RowAction", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("REJECT", "Reject", WorkflowPermissions.TasksReject, "RowAction", 20, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DELEGATE", "Delegate", WorkflowPermissions.TasksDelegate, "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("REQUEST_INFO", "Request Info", WorkflowPermissions.TasksRequestInfo, "RowAction", 40, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("CANCEL", "Cancel", WorkflowPermissions.TasksCancel, "RowAction", 50, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),

                new ModuleManifestPage(
                    PageCode: "SLA_RULES",
                    DisplayName: "SLA Escalation Rules",
                    RoutePath: "/Platform/Workflow/Definitions/{id}/SlaRules",
                    RequiredPermission: WorkflowPermissions.EscalationsManage,
                    ParentPageCode: "DEFINITIONS",
                    IsNavigationVisible: false,
                    PageType: "List",
                    SortOrder: 40,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE_SLA_RULE", "Create SLA Rule", WorkflowPermissions.EscalationsManage, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: "ESCALATIONS",
                    DisplayName: "Escalations",
                    RoutePath: "/Platform/Workflow/Escalations",
                    RequiredPermission: WorkflowPermissions.EscalationsRun,
                    ParentPageCode: null,
                    IsNavigationVisible: false,
                    PageType: "List",
                    SortOrder: 50,
                    Actions:
                    [
                        new ModuleManifestAction("RUN_ESCALATIONS", "Run Escalations", WorkflowPermissions.EscalationsRun, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: "TRANSITION_GATE",
                    DisplayName: "Transition Gate",
                    RoutePath: "/Platform/Workflow/TransitionGate",
                    RequiredPermission: WorkflowPermissions.TransitionsEvaluate,
                    ParentPageCode: null,
                    IsNavigationVisible: false,
                    PageType: "List",
                    SortOrder: 60,
                    Actions:
                    [
                        new ModuleManifestAction("EVALUATE", "Evaluate", WorkflowPermissions.TransitionsEvaluate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ])
            ]);
}
