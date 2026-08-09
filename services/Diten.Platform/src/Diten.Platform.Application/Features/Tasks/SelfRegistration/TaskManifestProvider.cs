using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.Tasks.SelfRegistration;

/// <summary>
/// MOD-0024 — the Task Engine self-registration manifest.
///
/// <para><b>Why the permissions MUST be declared here.</b> A permission key first created by the A1 reflection
/// worker is stamped <c>Module="platform"</c> + <c>Scope=PlatformAdmin</c>, and AuthService has no scope-downgrade
/// path — the key could then never be granted to a tenant role. Declaring the keys on manifest pages/actions makes
/// the manifest sync the attribution author (<c>Module="tasks"</c>, and <c>Scope=Tenant</c> because these routes
/// are not under <c>/Platform/*</c>). The startup ordering gate guarantees the manifest runs first.</para>
///
/// <para><b>Nav visibility.</b> Every <b>personal work surface</b> is <c>IsNavigationVisible: false</c> on
/// purpose: Görev Merkezi is the single answer to "where is my work", and a competing "Görevler" sidebar item
/// would fragment it. Those pages exist for permission attribution and are reached from the Task Center's
/// "+ Yeni" (precedent: the nav-invisible PERMISSIONS page in the Access Governance manifest).</para>
///
/// <para>That rule is about work surfaces, not about pages in general. <c>TASK_FIELD_DEFINITIONS</c> is visible
/// because it answers a different question — it configures the tenant's field schema rather than showing anyone
/// their work — so it cannot fragment an entry point it does not compete with. A settings screen reachable only
/// by typing its URL is a defect, not a policy. The distinction is enforced by
/// <c>TaskPermissions.PersonalWorkSurfaceScoped</c>, so a future personal page inherits the rule automatically.</para>
///
/// <para><b>Notification events.</b> Declared here so the Notification Event Catalog can materialize them. Two
/// verified constraints shape the values below: an event only reaches <c>Active</c> when it has zero validation
/// issues AND declares <c>Status: "Active"</c>; and <c>TargetPageCode</c> / <c>RequiredPermissionKey</c> are
/// validated against THIS manifest's own page codes and permission keys — hence the self-consistent references.</para>
/// </summary>
public sealed class TaskManifestProvider : IModuleManifestProvider
{
    // Page codes are referenced by the notification events below, so they are constants rather than literals.
    private const string PageTasks = "TASKS";
    private const string PageTaskCreate = "TASK_CREATE";
    private const string PageTaskDetail = "TASK_DETAIL";
    private const string PageTaskEdit = "TASK_EDIT";
    private const string PageTaskFieldDefinitions = "TASK_FIELD_DEFINITIONS";
    private const string PageTaskRecurrenceRules = "TASK_RECURRENCE_RULES";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "tasks",
            ModuleName: "Task Engine",
            DisplayName: "Görevler / Tasks",
            Domain: "Workspace",
            Service: "DitenPlatform",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 20,
            Icon: "bx-task",
            IsBaseline: false,
            Pages:
            [
                // The list surface. Its actions are what give every write permission a manifest home.
                new ModuleManifestPage(
                    PageCode: PageTasks,
                    DisplayName: "Tasks",
                    RoutePath: "/Tasks",
                    RequiredPermission: TaskPermissions.Read,
                    ParentPageCode: null,
                    IsNavigationVisible: false,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create Task", TaskPermissions.Create,
                            "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("UPDATE", "Edit Task", TaskPermissions.Update,
                            "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("ASSIGN", "Assign Task", TaskPermissions.Assign,
                            "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("CLAIM", "Claim Task", TaskPermissions.Claim,
                            "RowAction", 40, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("COMPLETE", "Complete Task", TaskPermissions.Complete,
                            "RowAction", 50, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("CANCEL", "Cancel Task", TaskPermissions.Cancel,
                            "RowAction", 60, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DELETE", "Delete Task", TaskPermissions.Delete,
                            "RowAction", 70, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("BULK_DELETE", "Delete Selected", TaskPermissions.BulkDelete,
                            "Toolbar", 80, IsDangerous: true, IsToolbarAction: true, IsRowAction: false),
                        // Gives the field-definition key manifest attribution; its UI lands in Phase 5.
                        new ModuleManifestAction("FIELD_DEFINITIONS", "Manage Task Fields",
                            TaskPermissions.FieldDefinitionsManage,
                            "Toolbar", 90, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: PageTaskCreate,
                    DisplayName: "Create Task",
                    RoutePath: "/Tasks/Create",
                    RequiredPermission: TaskPermissions.Create,
                    ParentPageCode: PageTasks,
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 11,
                    Actions: []),

                new ModuleManifestPage(
                    PageCode: PageTaskDetail,
                    DisplayName: "Task Detail",
                    RoutePath: "/Tasks/{id}",
                    RequiredPermission: TaskPermissions.Read,
                    ParentPageCode: PageTasks,
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 12,
                    Actions: []),

                new ModuleManifestPage(
                    PageCode: PageTaskEdit,
                    DisplayName: "Edit Task",
                    RoutePath: "/Tasks/{id}/Edit",
                    RequiredPermission: TaskPermissions.Update,
                    ParentPageCode: PageTasks,
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 13,
                    Actions: []),

                /*
                 * The configurable-field admin surface, registered HERE rather than as a hand-written <li> in
                 * _LayoutTenantShell.cshtml. That shell renders this area's menu DATA-DRIVEN from the module
                 * catalog (see the WC-1b note beside the Task Center entry); a hard-coded link would be a second,
                 * unmanaged entry that Menu Settings could neither reorder nor hide.
                 *
                 * The ONE nav-visible page in this manifest. It requires FieldDefinitionsManage, which is not
                 * in TaskPermissions.PersonalWorkSurfaceScoped, so it is not a "where is my work" surface and
                 * cannot fragment the Task Center. Every page that IS one stays invisible, and the test derives
                 * that from the permission rather than from a list of page codes.
                 */
                new ModuleManifestPage(
                    PageCode: PageTaskFieldDefinitions,
                    DisplayName: "Field Definitions",
                    RoutePath: "/Tasks/FieldDefinitions",
                    RequiredPermission: TaskPermissions.FieldDefinitionsManage,
                    ParentPageCode: PageTasks,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 20,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create Field Definition",
                            TaskPermissions.FieldDefinitionsManage, "Toolbar", 10,
                            IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("EDIT", "Edit Field Definition",
                            TaskPermissions.FieldDefinitionsManage, "Row", 20,
                            IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DELETE", "Delete Field Definition",
                            TaskPermissions.FieldDefinitionsManage, "Row", 30,
                            IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),

                /*
                 * The recurring-rule admin surface (BL-052). Registered here for the same reason the field
                 * definitions are: this area's menu is rendered from the module catalog, so a hard-coded <li>
                 * would be a second, unmanaged entry Menu Settings could neither reorder nor hide.
                 *
                 * Nav-visible, and it may be: RecurrenceManage is deliberately NOT in PersonalWorkSurfaceScoped,
                 * because defining WHEN work is created is a configuration authority rather than an answer to
                 * "where is my work". The screen also works by direct URL without this entry — the menu makes it
                 * FINDABLE, not reachable.
                 */
                new ModuleManifestPage(
                    PageCode: PageTaskRecurrenceRules,
                    DisplayName: "Recurring Task Rules",
                    RoutePath: "/Tasks/RecurrenceRules",
                    RequiredPermission: TaskPermissions.RecurrenceManage,
                    ParentPageCode: PageTasks,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 30,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create Recurrence Rule",
                            TaskPermissions.RecurrenceManage, "Toolbar", 10,
                            IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("EDIT", "Edit Recurrence Rule",
                            TaskPermissions.RecurrenceManage, "Row", 20,
                            IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DELETE", "Delete Recurrence Rule",
                            TaskPermissions.RecurrenceManage, "Row", 30,
                            IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ])
            ],
            NotificationEvents:
            [
                // Email only — there is no in-app channel (NotificationChannelCode { Email = 0 }); the header
                // bell is BL-025. Variable names satisfy ^[A-Za-z][A-Za-z0-9_.]*$ and use catalog types.
                new ModuleManifestNotificationEvent(
                    EventCode: TaskNotificationEvents.Assigned,
                    Channel: "Email",
                    DefaultTemplateKey: "platform.tasks.assigned",
                    DisplayNameKey: "NotificationEvent_TaskAssigned",
                    FallbackDisplayName: "Task assigned",
                    Description: "Sent when a task is assigned to a person or offered to a position pool.",
                    RequiredVariables:
                    [
                        new ModuleManifestNotificationVariable("TaskTitle"),
                        new ModuleManifestNotificationVariable("TaskId")
                    ],
                    OptionalVariables: [new ModuleManifestNotificationVariable("DueAt", "Date", false)],
                    TargetPageCode: PageTaskDetail,
                    RequiredPermissionKey: TaskPermissions.Read,
                    CanTenantOverride: true,
                    UsageType: "SystemEvent",
                    SeverityDefault: "Info",
                    LinkPolicy: "TargetPage",
                    Status: "Active"),

                new ModuleManifestNotificationEvent(
                    EventCode: TaskNotificationEvents.Claimed,
                    Channel: "Email",
                    DefaultTemplateKey: "platform.tasks.claimed",
                    DisplayNameKey: "NotificationEvent_TaskClaimed",
                    FallbackDisplayName: "Task claimed",
                    Description: "Sent to the task creator when a pooled task is claimed.",
                    RequiredVariables:
                    [
                        new ModuleManifestNotificationVariable("TaskTitle"),
                        new ModuleManifestNotificationVariable("TaskId")
                    ],
                    OptionalVariables: null,
                    TargetPageCode: PageTaskDetail,
                    RequiredPermissionKey: TaskPermissions.Read,
                    CanTenantOverride: true,
                    UsageType: "SystemEvent",
                    SeverityDefault: "Info",
                    LinkPolicy: "TargetPage",
                    Status: "Active"),

                new ModuleManifestNotificationEvent(
                    EventCode: TaskNotificationEvents.DueSoon,
                    Channel: "Email",
                    DefaultTemplateKey: "platform.tasks.duesoon",
                    DisplayNameKey: "NotificationEvent_TaskDueSoon",
                    FallbackDisplayName: "Task due soon",
                    Description: "Reminder that a task's due date is approaching.",
                    RequiredVariables:
                    [
                        new ModuleManifestNotificationVariable("TaskTitle"),
                        new ModuleManifestNotificationVariable("TaskId")
                    ],
                    OptionalVariables: [new ModuleManifestNotificationVariable("DueAt", "Date", false)],
                    TargetPageCode: PageTaskDetail,
                    RequiredPermissionKey: TaskPermissions.Read,
                    CanTenantOverride: true,
                    UsageType: "SystemEvent",
                    SeverityDefault: "Warning",
                    LinkPolicy: "TargetPage",
                    Status: "Active"),

                new ModuleManifestNotificationEvent(
                    EventCode: TaskNotificationEvents.Completed,
                    Channel: "Email",
                    DefaultTemplateKey: "platform.tasks.completed",
                    DisplayNameKey: "NotificationEvent_TaskCompleted",
                    FallbackDisplayName: "Task completed",
                    Description: "Sent to the task creator when the task is completed.",
                    RequiredVariables:
                    [
                        new ModuleManifestNotificationVariable("TaskTitle"),
                        new ModuleManifestNotificationVariable("TaskId")
                    ],
                    OptionalVariables: null,
                    TargetPageCode: PageTaskDetail,
                    RequiredPermissionKey: TaskPermissions.Read,
                    CanTenantOverride: true,
                    UsageType: "SystemEvent",
                    SeverityDefault: "Success",
                    LinkPolicy: "TargetPage",
                    Status: "Active"),

                // Declared now, dispatched in Phase 3 when the MOD-0023 handoff lands.
                new ModuleManifestNotificationEvent(
                    EventCode: TaskNotificationEvents.ApprovalRequested,
                    Channel: "Email",
                    DefaultTemplateKey: "platform.tasks.approvalrequested",
                    DisplayNameKey: "NotificationEvent_TaskApprovalRequested",
                    FallbackDisplayName: "Task approval requested",
                    Description: "Sent when a task requires manager approval before work may start.",
                    RequiredVariables:
                    [
                        new ModuleManifestNotificationVariable("TaskTitle"),
                        new ModuleManifestNotificationVariable("TaskId")
                    ],
                    OptionalVariables: null,
                    TargetPageCode: PageTaskDetail,
                    RequiredPermissionKey: TaskPermissions.Read,
                    CanTenantOverride: true,
                    UsageType: "SystemEvent",
                    SeverityDefault: "Info",
                    LinkPolicy: "TargetPage",
                    Status: "Active")
            ]);
}
