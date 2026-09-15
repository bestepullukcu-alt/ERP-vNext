using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.Meetings.SelfRegistration;

/// <summary>
/// MOD-0357 S2 — the Meetings module self-registration manifest (pack §14; ADR-001 §3 pattern).
///
/// <para><b>Why a page exists at all when S3 builds no screen yet.</b> A permission key first created by the A1
/// reflection worker is stamped <c>Module="platform"</c> + <c>Scope=PlatformAdmin</c>, and AuthService has no
/// scope-downgrade path — the key could then never be granted to a tenant role (the exact reason
/// <c>TaskManifestProvider</c> declares its own permissions on manifest pages instead). All nine
/// <see cref="MeetingPermissions"/> keys need <c>Module=meetings</c>/<c>Scope=Tenant</c> attribution NOW (this
/// WP's own AC10), which the self-registration mechanism only derives from a page's route (§2c) — so ONE page
/// carries them, exactly the way <c>TaskManifestProvider</c>'s own <c>TASKS</c> page carries
/// <see cref="TaskPermissions"/> Read/Create/Update/… before any of its four work-surface pages had a live view.</para>
///
/// <para><b><c>IsNavigationVisible: false</c>, deliberately — this WP's own NE #6.</b> S3 builds the real
/// <c>/Meetings</c> screen; until then a nav-visible entry would be a dead menu link. This mirrors the same
/// pattern <c>TaskManifestProvider</c> uses for its own <c>TASKS</c>/<c>TASK_CREATE</c>/<c>TASK_DETAIL</c>/
/// <c>TASK_EDIT</c> pages.</para>
///
/// <para><b>Known gap, reported per this WP's own stop-condition list ("yeni resx gereği").</b> The mere
/// existence of any <c>*ManifestProvider.cs</c> file under <c>services/</c> makes
/// <c>frontend/Diten.Web.Tests/Navigation/NavManifestL10nGuardTests.Every_manifest_module_has_a_Nav_Module_key_in_all_seven_languages</c>
/// require a <c>Nav.Module.meetings</c> resx key in all 7 tenant languages — UNCONDITIONALLY, regardless of
/// whether any page is nav-visible (read and confirmed against that test's own source: the module-key check
/// does not gate on <c>IsNavigationVisible</c> at all). This WP explicitly forbids new resx ("YENİ RESX YOK —
/// ön yüz S3'te eşler") and `frontend/**` is a protected path for this WP. Both cannot be satisfied at once;
/// see this WP's own final report for the decision this is left to Control Tower to make (add the one key, or
/// defer this manifest's DI registration to S3) — not silently resolved either way here.</para>
/// </summary>
public sealed class MeetingManifestProvider : IModuleManifestProvider
{
    private const string PageMeetings = "MEETINGS";
    private const string PageMeetingCreate = "MEETING_CREATE";
    private const string PageMeetingDetail = "MEETING_DETAIL";
    private const string PageMeetingEdit = "MEETING_EDIT";
    private const string PageMeetingTypes = "MEETING_TYPES";
    private const string PageMeetingSeries = "MEETING_SERIES";
    private const string PageMeetingReport = "MEETING_REPORT";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "meetings",
            ModuleName: "Meetings",
            DisplayName: "Toplantılar / Meetings",
            Domain: "ManagementGovernance",
            Service: "DitenPlatform",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 40,
            Icon: "bx-calendar-event",
            IsBaseline: false,
            Pages:
            [
                // S3 — pack §9: the top-level list IS nav-visible (unlike MOD-0024's work surfaces, Meetings is
                // a first-class tenant screen, not a personal-work aggregator hidden behind the Task Center).
                new ModuleManifestPage(
                    PageCode: PageMeetings,
                    DisplayName: "Meetings",
                    RoutePath: "/Meetings",
                    RequiredPermission: MeetingPermissions.Read,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create Meeting", MeetingPermissions.Create,
                            "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("UPDATE", "Edit Meeting", MeetingPermissions.Update,
                            "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DELETE", "Delete Meeting", MeetingPermissions.Delete,
                            "RowAction", 30, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("BULK_DELETE", "Delete Selected", MeetingPermissions.BulkDelete,
                            "Toolbar", 40, IsDangerous: true, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("MINUTES_WRITE", "Edit Minutes", MeetingPermissions.MinutesWrite,
                            "RowAction", 50, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("MINUTES_PUBLISH", "Publish Minutes", MeetingPermissions.MinutesPublish,
                            "RowAction", 60, IsDangerous: true, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("TYPES_MANAGE", "Manage Meeting Types", MeetingPermissions.TypesManage,
                            "Toolbar", 70, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("SERIES_MANAGE", "Manage Meeting Series", MeetingPermissions.SeriesManage,
                            "Toolbar", 75, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("READ_ALL", "View All Meetings", MeetingPermissions.ReadAll,
                            "Toolbar", 80, IsDangerous: false, IsToolbarAction: false, IsRowAction: false)
                    ]),

                // The three work-surface routes — nav-hidden (§2a), reached only from the list, mirroring
                // TaskManifestProvider's own TASK_CREATE/TASK_DETAIL/TASK_EDIT pages exactly.
                new ModuleManifestPage(
                    PageCode: PageMeetingCreate,
                    DisplayName: "Create Meeting",
                    RoutePath: "/Meetings/Create",
                    RequiredPermission: MeetingPermissions.Create,
                    ParentPageCode: PageMeetings,
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 11,
                    Actions: []),

                new ModuleManifestPage(
                    PageCode: PageMeetingDetail,
                    DisplayName: "Meeting Detail",
                    RoutePath: "/Meetings/{id}",
                    RequiredPermission: MeetingPermissions.Read,
                    ParentPageCode: PageMeetings,
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 12,
                    Actions: []),

                new ModuleManifestPage(
                    PageCode: PageMeetingEdit,
                    DisplayName: "Edit Meeting",
                    RoutePath: "/Meetings/{id}/Edit",
                    RequiredPermission: MeetingPermissions.Update,
                    ParentPageCode: PageMeetings,
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 13,
                    Actions: []),

                // S8 — the meeting-type setting screen (pack §5 :323, §K8). Nav-visible under MEETINGS, same
                // shape TaskManifestProvider's own TASK_TYPES page takes under TASKS. The MEETINGS page's own
                // TYPES_MANAGE toolbar action stays — it now links here instead of being a dead placeholder.
                new ModuleManifestPage(
                    PageCode: PageMeetingTypes,
                    DisplayName: "Meeting Types",
                    RoutePath: "/Meetings/MeetingTypes",
                    RequiredPermission: MeetingPermissions.TypesManage,
                    ParentPageCode: PageMeetings,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 20,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create Meeting Type", MeetingPermissions.TypesManage,
                            "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("EDIT", "Edit Meeting Type", MeetingPermissions.TypesManage,
                            "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DELETE", "Delete Meeting Type", MeetingPermissions.TypesManage,
                            "RowAction", 30, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),

                // S11 — the recurring cadence rule catalogue (pack §19). Nav-visible under MEETINGS, same shape
                // the MEETING_TYPES page above takes.
                new ModuleManifestPage(
                    PageCode: PageMeetingSeries,
                    DisplayName: "Meeting Series",
                    RoutePath: "/Meetings/Series",
                    RequiredPermission: MeetingPermissions.SeriesManage,
                    ParentPageCode: PageMeetings,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 21,
                    Actions:
                    [
                        new ModuleManifestAction("CREATE", "Create Meeting Series", MeetingPermissions.SeriesManage,
                            "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("EDIT", "Edit Meeting Series", MeetingPermissions.SeriesManage,
                            "RowAction", 20, IsDangerous: false, IsToolbarAction: false, IsRowAction: true),
                        new ModuleManifestAction("DELETE", "Delete Meeting Series", MeetingPermissions.SeriesManage,
                            "RowAction", 30, IsDangerous: true, IsToolbarAction: false, IsRowAction: true)
                    ]),

                // S12 (pack §23) — the meeting report & action register. Nav-visible under MEETINGS, same shape
                // MEETING_TYPES/MEETING_SERIES take. Gated on Read (owner decision, pack §23.13/1: reuse
                // platform.meetings.read/.read-all — no new key); read-all's own widening happens INSIDE the
                // handler (§23.6), same as every other Meetings read.
                new ModuleManifestPage(
                    PageCode: PageMeetingReport,
                    DisplayName: "Meeting Report",
                    RoutePath: "/Meetings/Report",
                    RequiredPermission: MeetingPermissions.Read,
                    ParentPageCode: PageMeetings,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 22,
                    Actions: [])
            ],
            NotificationEvents:
            [
                // S5 — invite/change/cancel (pack §3 IMeetingInviteMailer, K12). Email only, same posture
                // TaskManifestProvider's own events take: there is no in-app channel for these yet.
                new ModuleManifestNotificationEvent(
                    EventCode: "platform.meetings.invite",
                    Channel: "Email",
                    DefaultTemplateKey: "platform.meetings.invite",
                    DisplayNameKey: "NotificationEvent_MeetingInvite",
                    FallbackDisplayName: "Meeting invitation",
                    Description: "Sent to attendees when a meeting is created.",
                    RequiredVariables:
                    [
                        new ModuleManifestNotificationVariable("MeetingTitle"),
                        new ModuleManifestNotificationVariable("MeetingType"),
                        new ModuleManifestNotificationVariable("StartAt"),
                        new ModuleManifestNotificationVariable("EndAt"),
                        new ModuleManifestNotificationVariable("Organizer"),
                        new ModuleManifestNotificationVariable("MeetingUrl")
                    ],
                    OptionalVariables: [new ModuleManifestNotificationVariable("Location", IsRequired: false)],
                    TargetPageCode: PageMeetingDetail,
                    RequiredPermissionKey: MeetingPermissions.Read,
                    CanTenantOverride: true,
                    UsageType: "SystemEvent",
                    SeverityDefault: "Info",
                    LinkPolicy: "TargetPage",
                    Status: "Active"),

                new ModuleManifestNotificationEvent(
                    EventCode: "platform.meetings.change",
                    Channel: "Email",
                    DefaultTemplateKey: "platform.meetings.change",
                    DisplayNameKey: "NotificationEvent_MeetingChange",
                    FallbackDisplayName: "Meeting changed",
                    Description: "Sent to attendees when a meeting's date, time or location changes.",
                    RequiredVariables:
                    [
                        new ModuleManifestNotificationVariable("MeetingTitle"),
                        new ModuleManifestNotificationVariable("MeetingType"),
                        new ModuleManifestNotificationVariable("StartAt"),
                        new ModuleManifestNotificationVariable("EndAt"),
                        new ModuleManifestNotificationVariable("Organizer"),
                        new ModuleManifestNotificationVariable("MeetingUrl")
                    ],
                    OptionalVariables: [new ModuleManifestNotificationVariable("Location", IsRequired: false)],
                    TargetPageCode: PageMeetingDetail,
                    RequiredPermissionKey: MeetingPermissions.Read,
                    CanTenantOverride: true,
                    UsageType: "SystemEvent",
                    SeverityDefault: "Info",
                    LinkPolicy: "TargetPage",
                    Status: "Active"),

                new ModuleManifestNotificationEvent(
                    EventCode: "platform.meetings.cancel",
                    Channel: "Email",
                    DefaultTemplateKey: "platform.meetings.cancel",
                    DisplayNameKey: "NotificationEvent_MeetingCancel",
                    FallbackDisplayName: "Meeting cancelled",
                    Description: "Sent to attendees when a meeting is cancelled.",
                    RequiredVariables:
                    [
                        new ModuleManifestNotificationVariable("MeetingTitle"),
                        new ModuleManifestNotificationVariable("MeetingType"),
                        new ModuleManifestNotificationVariable("StartAt"),
                        new ModuleManifestNotificationVariable("EndAt"),
                        new ModuleManifestNotificationVariable("Organizer"),
                        new ModuleManifestNotificationVariable("MeetingUrl")
                    ],
                    OptionalVariables: [new ModuleManifestNotificationVariable("Location", IsRequired: false)],
                    TargetPageCode: PageMeetingDetail,
                    RequiredPermissionKey: MeetingPermissions.Read,
                    CanTenantOverride: true,
                    UsageType: "SystemEvent",
                    SeverityDefault: "Info",
                    LinkPolicy: "TargetPage",
                    Status: "Active"),

                // BL-387/BL-373 (owner, 2026-09-14) — the ORGANIZER's own variant of invite/change/cancel, used
                // whenever the organizer did not perform the action themselves (series sweep, a delegate acting
                // on their behalf, or a reassignment). No "Organizer" variable: the reader IS the organizer, so
                // naming them back to themselves would be redundant, unlike the three sibling events above.
                new ModuleManifestNotificationEvent(
                    EventCode: "platform.meetings.organizer-added",
                    Channel: "Email",
                    DefaultTemplateKey: "platform.meetings.organizer-added",
                    DisplayNameKey: "NotificationEvent_MeetingOrganizerAdded",
                    FallbackDisplayName: "Meeting added to your calendar",
                    Description: "Sent to the organizer when a meeting they did not personally create is scheduled on their behalf.",
                    RequiredVariables:
                    [
                        new ModuleManifestNotificationVariable("MeetingTitle"),
                        new ModuleManifestNotificationVariable("MeetingType"),
                        new ModuleManifestNotificationVariable("StartAt"),
                        new ModuleManifestNotificationVariable("EndAt"),
                        new ModuleManifestNotificationVariable("MeetingUrl")
                    ],
                    OptionalVariables: [new ModuleManifestNotificationVariable("Location", IsRequired: false)],
                    TargetPageCode: PageMeetingDetail,
                    RequiredPermissionKey: MeetingPermissions.Read,
                    CanTenantOverride: true,
                    UsageType: "SystemEvent",
                    SeverityDefault: "Info",
                    LinkPolicy: "TargetPage",
                    Status: "Active"),

                new ModuleManifestNotificationEvent(
                    EventCode: "platform.meetings.organizer-updated",
                    Channel: "Email",
                    DefaultTemplateKey: "platform.meetings.organizer-updated",
                    DisplayNameKey: "NotificationEvent_MeetingOrganizerUpdated",
                    FallbackDisplayName: "Your meeting was updated",
                    Description: "Sent to the organizer when a meeting they organize changes and they did not make the change themselves.",
                    RequiredVariables:
                    [
                        new ModuleManifestNotificationVariable("MeetingTitle"),
                        new ModuleManifestNotificationVariable("MeetingType"),
                        new ModuleManifestNotificationVariable("StartAt"),
                        new ModuleManifestNotificationVariable("EndAt"),
                        new ModuleManifestNotificationVariable("MeetingUrl")
                    ],
                    OptionalVariables: [new ModuleManifestNotificationVariable("Location", IsRequired: false)],
                    TargetPageCode: PageMeetingDetail,
                    RequiredPermissionKey: MeetingPermissions.Read,
                    CanTenantOverride: true,
                    UsageType: "SystemEvent",
                    SeverityDefault: "Info",
                    LinkPolicy: "TargetPage",
                    Status: "Active"),

                new ModuleManifestNotificationEvent(
                    EventCode: "platform.meetings.organizer-cancelled",
                    Channel: "Email",
                    DefaultTemplateKey: "platform.meetings.organizer-cancelled",
                    DisplayNameKey: "NotificationEvent_MeetingOrganizerCancelled",
                    FallbackDisplayName: "Your meeting was cancelled",
                    Description: "Sent to the organizer when a meeting they organize is cancelled and they did not cancel it themselves.",
                    RequiredVariables:
                    [
                        new ModuleManifestNotificationVariable("MeetingTitle"),
                        new ModuleManifestNotificationVariable("MeetingType"),
                        new ModuleManifestNotificationVariable("StartAt"),
                        new ModuleManifestNotificationVariable("EndAt"),
                        new ModuleManifestNotificationVariable("MeetingUrl")
                    ],
                    OptionalVariables: [new ModuleManifestNotificationVariable("Location", IsRequired: false)],
                    TargetPageCode: PageMeetingDetail,
                    RequiredPermissionKey: MeetingPermissions.Read,
                    CanTenantOverride: true,
                    UsageType: "SystemEvent",
                    SeverityDefault: "Info",
                    LinkPolicy: "TargetPage",
                    Status: "Active"),

                // BL-386 — the removed attendee only, never the rest of the meeting; deliberately LINKLESS
                // (`LinkPolicy: "None"`, no TargetPageCode): the meeting detail page 404s for someone no longer
                // on the meeting (D3's own visibility rule), so a link here would point straight at that dead end.
                new ModuleManifestNotificationEvent(
                    EventCode: "platform.meetings.removed",
                    Channel: "Email",
                    DefaultTemplateKey: "platform.meetings.removed",
                    DisplayNameKey: "NotificationEvent_MeetingRemoved",
                    FallbackDisplayName: "Removed from meeting",
                    Description: "Sent to a single attendee when they are removed from a meeting.",
                    RequiredVariables:
                    [
                        new ModuleManifestNotificationVariable("MeetingTitle"),
                        new ModuleManifestNotificationVariable("MeetingType"),
                        new ModuleManifestNotificationVariable("StartAt"),
                        new ModuleManifestNotificationVariable("EndAt"),
                        new ModuleManifestNotificationVariable("Organizer")
                    ],
                    OptionalVariables: [new ModuleManifestNotificationVariable("Location", IsRequired: false)],
                    TargetPageCode: null,
                    CanTenantOverride: true,
                    UsageType: "SystemEvent",
                    SeverityDefault: "Info",
                    LinkPolicy: "None",
                    Status: "Active")
            ]);
}
