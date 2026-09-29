using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.TimeEntry.SelfRegistration;

/// <summary>
/// MOD-0280-FU01 (pack §9, §14, §19.3; A4) — the time-entry module's self-registration manifest: a catalog module,
/// tenant-assignable, NOT baseline (entitlement-gated), in the existing "Human Capital" domain.
///
/// <para><b>Why pages exist before any screen (T1a).</b> Self-registration derives each permission's scope from a page
/// route (§2c): a key first minted by the reflection worker would be stamped platform-admin and could never be granted
/// to a tenant role. So the six T1a keys ride on the pages the pack names (§9) now — the MOD-0357 S2 / MOD-0024
/// precedent.</para>
///
/// <para><b><c>IsNavigationVisible: false</c> on every page until T2.</b> The screens (<c>/TimeEntry</c>,
/// <c>/TimeEntry/Approvals</c>, …) are T2's; a nav entry today would be a dead link. T2 flips
/// <c>MY_TIMESHEET</c> and <c>TIME_APPROVALS</c> to visible (pack §9) together with their <c>Nav.Page.*</c> keys in seven
/// languages.</para>
///
/// <para><b>Known gap, reported (the same one MOD-0357 S2 reported).</b>
/// <c>NavManifestL10nGuardTests</c> derives a required <c>Nav.Module.TIMEENTRY</c> key from this file's module code,
/// unconditionally, in all seven <c>SharedResource</c> files — and <c>frontend/**</c> is outside this slice. Left to
/// Control Tower, not silently resolved.</para>
///
/// <para>The two T4 keys (<c>time-entry.team-totals.read</c>, <c>time-entry.person-reports.read</c>) are NOT declared
/// here: they are minted with their endpoints in T4 (pack §14), after their explicit-grant enrolment.</para>
/// </summary>
public sealed class TimeEntryManifestProvider : IModuleManifestProvider
{
    public const string PageMyTimesheet = "MY_TIMESHEET";
    public const string PageApprovals = "TIME_APPROVALS";
    public const string PageApprovalDetail = "TIME_APPROVAL_DETAIL";
    public const string PageCategories = "TIME_CATEGORIES";
    public const string PageSettings = "TIME_SETTINGS";

    public ModuleManifestDocument GetManifest() =>
        new(
            // A literal on purpose: NavManifestL10nGuardTests reads module codes from this file's TEXT.
            ModuleCode: "time-entry",
            ModuleName: "Time Entry",
            DisplayName: "Zaman Çizelgem / My Timesheet",
            Domain: "Human Capital", // SOFT: seeds once; → existing Nav.Domain.HUMANCAPITAL
            Service: "DitenPlatform",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 60,
            Icon: "bx-time-five",
            IsBaseline: false,
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: PageMyTimesheet,
                    DisplayName: "My Timesheet",
                    RoutePath: "/TimeEntry",
                    RequiredPermission: TimeEntryPermissions.TimesheetsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("SAVE", "Save Draft", TimeEntryPermissions.TimesheetsUpdate,
                            "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("SUBMIT", "Submit Week", TimeEntryPermissions.TimesheetsUpdate,
                            "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("WITHDRAW", "Withdraw Week", TimeEntryPermissions.TimesheetsUpdate,
                            "Toolbar", 30, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("REQUEST_CORRECTION", "Request Correction", TimeEntryPermissions.TimesheetsUpdate,
                            "Toolbar", 40, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("DISCARD_CORRECTION", "Discard Correction", TimeEntryPermissions.TimesheetsUpdate,
                            "Toolbar", 50, IsDangerous: true, IsToolbarAction: true, IsRowAction: false),
                        // T1b — capture. No new key: the timer and the suggestions are the person's own sheet.
                        new ModuleManifestAction("START_TIMER", "Start Timer", TimeEntryPermissions.TimesheetsUpdate,
                            "Toolbar", 60, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("STOP_TIMER", "Stop Timer", TimeEntryPermissions.TimesheetsUpdate,
                            "Toolbar", 70, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("UNDO_TIMER_SWITCH", "Undo Timer Switch", TimeEntryPermissions.TimesheetsUpdate,
                            "Toolbar", 80, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("ACCEPT_SUGGESTION", "Accept Time Suggestion", TimeEntryPermissions.TimesheetsUpdate,
                            "RowAction", 90, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DISMISS_SUGGESTION", "Dismiss Time Suggestion", TimeEntryPermissions.TimesheetsUpdate,
                            "RowAction", 100, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("FILL_FROM_PLAN", "Fill From Plan", TimeEntryPermissions.TimesheetsRead,
                            "Toolbar", 110, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),

                new ModuleManifestPage(
                    PageCode: PageApprovals,
                    DisplayName: "Timesheet Approvals",
                    RoutePath: "/TimeEntry/Approvals",
                    RequiredPermission: TimeEntryPermissions.ApprovalsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: false,
                    PageType: "List",
                    SortOrder: 20,
                    Actions:
                    [
                        new ModuleManifestAction("REOPEN", "Reopen Week", TimeEntryPermissions.WeeksReopen,
                            "RowAction", 10, IsDangerous: false, IsToolbarAction: false, IsRowAction: true)
                    ]),

                new ModuleManifestPage(
                    PageCode: PageApprovalDetail,
                    DisplayName: "Timesheet Approval Detail",
                    RoutePath: "/TimeEntry/Approvals/{id}",
                    RequiredPermission: TimeEntryPermissions.ApprovalsRead,
                    ParentPageCode: PageApprovals,
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 21,
                    Actions: []),

                new ModuleManifestPage(
                    PageCode: PageCategories,
                    DisplayName: "Work Categories",
                    RoutePath: "/TimeEntry/Categories",
                    RequiredPermission: TimeEntryPermissions.CategoriesManage,
                    ParentPageCode: null,
                    IsNavigationVisible: false,
                    PageType: "List",
                    SortOrder: 30,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create Category", TimeEntryPermissions.CategoriesManage,
                            "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("INSTALL_RECOMMENDED", "Install Recommended", TimeEntryPermissions.CategoriesManage,
                            "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("UPDATE", "Edit Category", TimeEntryPermissions.CategoriesManage,
                            "RowAction", 30, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("ACTIVATE", "Activate Category", TimeEntryPermissions.CategoriesManage,
                            "RowAction", 40, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DEACTIVATE", "Deactivate Category", TimeEntryPermissions.CategoriesManage,
                            "RowAction", 50, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),

                new ModuleManifestPage(
                    PageCode: PageSettings,
                    DisplayName: "Timesheet Settings",
                    RoutePath: "/TimeEntry/Settings",
                    RequiredPermission: TimeEntryPermissions.SettingsManage,
                    ParentPageCode: null,
                    IsNavigationVisible: false,
                    PageType: "Form",
                    SortOrder: 40,
                    Actions:
                    [
                        new ModuleManifestAction("UPDATE", "Save Settings", TimeEntryPermissions.SettingsManage,
                            "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("SET_TIMER_SWITCH", "Set Legal Entity Timer Switch", TimeEntryPermissions.SettingsManage,
                            "RowAction", 20, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ])
            ],
            NotificationEvents:
            [
                Event(TimeEntryNotificationEvents.WeekSubmitted, "Timesheet submitted",
                    "Sent to the approver when a week is submitted.", PageApprovals, TimeEntryPermissions.ApprovalsRead),
                Event(TimeEntryNotificationEvents.WeekApproved, "Timesheet approved",
                    "Sent to the person when their week is approved.", PageMyTimesheet, TimeEntryPermissions.TimesheetsRead),
                Event(TimeEntryNotificationEvents.WeekRejected, "Timesheet returned",
                    "Sent to the person when their week is rejected, with the approver's reason.", PageMyTimesheet,
                    TimeEntryPermissions.TimesheetsRead),
                Event(TimeEntryNotificationEvents.WeekWithdrawn, "Timesheet withdrawn",
                    "Sent to the approver when the person withdraws a submitted week.", PageApprovals,
                    TimeEntryPermissions.ApprovalsRead),
                Event(TimeEntryNotificationEvents.TimerAutoClosed, "Timer closed at midnight",
                    "Sent to the person the morning after their timer was closed at local midnight.", PageMyTimesheet,
                    TimeEntryPermissions.TimesheetsRead)
            ]);

    // Declared, not yet dispatched: templates (7 languages) are T3, so every event stays Draft until then.
    private static ModuleManifestNotificationEvent Event(
        string code, string fallbackName, string description, string pageCode, string permission) =>
        new(
            EventCode: code,
            Channel: "Email",
            DefaultTemplateKey: code,
            DisplayNameKey: null,
            FallbackDisplayName: fallbackName,
            Description: description,
            RequiredVariables: [new ModuleManifestNotificationVariable("WeekKey")],
            OptionalVariables: null,
            TargetPageCode: pageCode,
            RequiredPermissionKey: permission,
            CanTenantOverride: false,
            UsageType: "SystemEvent",
            Status: "Draft");
}
