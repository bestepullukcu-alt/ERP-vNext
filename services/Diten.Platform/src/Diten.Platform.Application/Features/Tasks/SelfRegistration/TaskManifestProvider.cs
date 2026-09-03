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
    private const string PageTaskTypes = "TASK_TYPES";
    private const string PageDocumentList = "TASK_DOCUMENT_LIST";
    private const string PageTaskRecurrenceRules = "TASK_RECURRENCE_RULES";
    private const string PageChecklistTemplates = "TASK_CHECKLIST_TEMPLATES";
    private const string PageTaskTemplates = "TASK_TEMPLATES";
    private const string PageWorkReport = "TASK_WORK_REPORT";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "tasks",
            ModuleName: "Task Engine",
            /*
             * ⚠ "GÖREV TANIMLARI", NOT "GÖREVLER". Every page this manifest publishes to the sidebar
             * (TASK_FIELD_DEFINITIONS, TASK_TYPES, TASK_DOCUMENT_LIST, TASK_RECURRENCE_RULES) is a
             * definition/settings screen. The four work surfaces — TASKS, TASK_CREATE, TASK_DETAIL, TASK_EDIT —
             * are IsNavigationVisible: false on purpose (see the Nav visibility note above), so "Görevler"
             * promised the user a task LIST from the menu and handed them a configuration screen instead.
             *
             * It also collided with the neighbouring module "Görev Merkezi / Task Center", which IS where a
             * person's work lives. Two near-identical names for two different things is not a labelling nit: the
             * menu could not tell the user which one answered "where is my work". Renaming this one separates them.
             *
             * NOTE — this string is the FALLBACK, not what the sidebar prints. The menu localizes the module by
             * its stable CODE (Nav.Module.TASKS, all seven tenant languages); this is what renders only when that
             * key is missing, plus what the operator sees in the module catalog. Both were changed together.
             */
            DisplayName: "Görev Tanımları / Task Settings",
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
                 * The TASK TYPE catalogue (DCP-005 slice 1), registered here for the same reason the field
                 * definitions above are: this area's menu is rendered from the module catalog, so a hard-coded
                 * <li> would be a second, unmanaged entry Menu Settings could neither reorder nor hide.
                 *
                 * ⚠ NO DELETE ACTION, unlike its sibling. A type that has been used is part of the identity of
                 * every task opened under it, so it is retired and never removed — the manifest says so too,
                 * because an action declared here is an action the catalogue will offer.
                 */
                new ModuleManifestPage(
                    PageCode: PageTaskTypes,
                    DisplayName: "Task Types",
                    RoutePath: "/Tasks/TaskTypes",
                    RequiredPermission: TaskPermissions.TaskTypesManage,
                    ParentPageCode: PageTasks,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 25,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create Task Type",
                            TaskPermissions.TaskTypesManage, "Toolbar", 10,
                            IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("EDIT", "Edit Task Type",
                            TaskPermissions.TaskTypesManage, "Row", 20,
                            IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DEACTIVATE", "Deactivate Task Type",
                            TaskPermissions.TaskTypesManage, "Row", 30,
                            IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),

                /*
                 * The controlled-document reference LIST (DCP-005 slice 2).
                 *
                 * ⚠ ONE ACTION, AND IT IS AN IMPORT. There is no create, no edit and no delete — the list is a
                 * lookup, not a table, and a row that could be edited here would be the second authority over a
                 * document that §6.1 exists to prevent. The manifest says so too, because an action declared
                 * here is an action the catalogue will offer.
                 */
                new ModuleManifestPage(
                    PageCode: PageDocumentList,
                    DisplayName: "Controlled Documents",
                    RoutePath: "/Tasks/DocumentList",
                    // ⚠ READ, NOT IMPORT. Measured: the page was published behind the import permission while the search it
                    // exists for asks only Read — so a QA reader who could see every row could not open the screen
                    // showing them. The WRITE surfaces inside are gated separately, in the view.
                    RequiredPermission: TaskPermissions.DocumentListRead,
                    ParentPageCode: PageTasks,
                    // ⚠ PUBLISHED VISIBLE ONLY NOW, in the round the screen was measured open. It shipped `true` once with no
                    // view and no route: the sidebar would have grown an entry pointing at a 404 on the next
                    // reconciliation. A manifest page is a promise the menu keeps — it is made when it can be kept.
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 27,
                    Actions:
                    [
                        new ModuleManifestAction("IMPORT", "Import Document List",
                            TaskPermissions.DocumentListImport, "Toolbar", 10,
                            IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
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
                    ]),

                /*
                 * BL-054 — the two template surfaces that make a recurrence rule produce a task worth receiving.
                 *
                 * ⚠ THE CHECKLIST SCREEN SORTS BEFORE THE TASK-TEMPLATE ONE, and that is not decoration. The
                 * task-template form carries a checklist picker; an administrator who meets these menu entries in
                 * the other order fills that picker's source in AFTERWARDS, having already saved a template with
                 * no gate. The menu is the only instruction most people will ever read, so it states the order
                 * the work actually has.
                 *
                 * Both nav-visible, and both may be: these keys configure what work LOOKS LIKE rather than
                 * showing anybody their own work, so neither is in PersonalWorkSurfaceScoped and neither
                 * fragments the Task Center. TaskManifestProviderTests derives that from the permission.
                 */
                new ModuleManifestPage(
                    PageCode: PageChecklistTemplates,
                    DisplayName: "Checklist Templates",
                    RoutePath: "/Tasks/ChecklistTemplates",
                    RequiredPermission: TaskPermissions.ChecklistTemplatesManage,
                    ParentPageCode: PageTasks,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 31,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create Checklist Template",
                            TaskPermissions.ChecklistTemplatesManage, "Toolbar", 10,
                            IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("EDIT", "Edit Checklist Template",
                            TaskPermissions.ChecklistTemplatesManage, "Row", 20,
                            IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DELETE", "Delete Checklist Template",
                            TaskPermissions.ChecklistTemplatesManage, "Row", 30,
                            IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),

                new ModuleManifestPage(
                    PageCode: PageTaskTemplates,
                    DisplayName: "Task Templates",
                    RoutePath: "/Tasks/Templates",
                    RequiredPermission: TaskPermissions.TemplatesManage,
                    ParentPageCode: PageTasks,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 32,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create Task Template",
                            TaskPermissions.TemplatesManage, "Toolbar", 10,
                            IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("EDIT", "Edit Task Template",
                            TaskPermissions.TemplatesManage, "Row", 20,
                            IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DELETE", "Delete Task Template",
                            TaskPermissions.TemplatesManage, "Row", 30,
                            IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),

                /*
                 * The WORK REPORT (Faz 5a) — how work is flowing, for the work the reader may see.
                 *
                 * ⚠ NAV-INVISIBLE, AND THAT IS THE POINT OF DECLARING IT NOW ANYWAY. Faz 5a ships the QUERY;
                 * the screen is 5b. A manifest page is a promise the menu keeps, and publishing this visible
                 * would grow a sidebar entry pointing at a 404 on the next reconciliation — exactly what the
                 * document-list page shipped once and had to be corrected for.
                 *
                 * It is declared regardless because the permission keys need a manifest HOME: a key the
                 * manifest never mentions is created by the A1 reflection worker as Module="platform" +
                 * Scope=PlatformAdmin, which AuthService cannot downgrade — permanently unassignable to a
                 * tenant role. 5b flips the flag; it does not invent the keys.
                 *
                 * ⚠ NOT behind `TaskPermissions.Read`. That key is in `PersonalWorkSurfaceScoped`, so a
                 * nav-visible page behind it would become a second answer to "where is my work". A report is not
                 * personal work — it says how the ORGANISATION's work is flowing — so it carries its own key,
                 * the same separation `DocumentListRead` was given.
                 */
                new ModuleManifestPage(
                    PageCode: PageWorkReport,
                    DisplayName: "Work Report",
                    RoutePath: "/Tasks/WorkReport",
                    RequiredPermission: TaskPermissions.WorkReportRead,
                    ParentPageCode: PageTasks,
                    /*
                     * ⚠ VISIBLE NOW (Faz 5b), and it was `false` for exactly one slice.
                     *
                     * 5a shipped the query with no screen behind this route, and publishing it visible then
                     * would have grown a sidebar entry pointing at a 404 on the next reconciliation — the
                     * mistake the document-list page made once and had to be corrected for. The page exists as
                     * of this slice, so the promise the menu makes can now be kept.
                     */
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 33,
                    Actions:
                    [
                        /*
                         * Widening the report to every row in the tenant, as its own declared authority rather
                         * than a flag on the request — a flag would let anyone who can reach the endpoint set
                         * it. Held by far fewer people than the report itself: Oracle frames worklist report
                         * scope as the user's groups or their reportees' groups, not the company.
                         */
                        new ModuleManifestAction("VIEW_TENANT_WIDE", "View Work Report Tenant-Wide",
                            TaskPermissions.WorkReportReadTenantWide, "Toolbar", 10,
                            IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
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

                /*
                 * Somebody said something on the task (2026-08-14). Declared HERE rather than through a second
                 * notification path: the module already owns a dispatcher, a policy and a recipient resolver, and
                 * a parallel road would need its own copy of the master switch, the per-event preference and the
                 * actor exclusion.
                 *
                 * Variables match its five siblings exactly, and the comment TEXT is deliberately not among them
                 * — see NotificationTemplateSeed for why quoting a retractable sentence into an unrecallable
                 * email is the one thing this event must not do.
                 */
                new ModuleManifestNotificationEvent(
                    EventCode: TaskNotificationEvents.Commented,
                    Channel: "Email",
                    DefaultTemplateKey: "platform.tasks.commented",
                    DisplayNameKey: "NotificationEvent_TaskCommented",
                    FallbackDisplayName: "Task commented",
                    Description: "Sent when somebody comments on a task. Edits and withdrawals send nothing.",
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
