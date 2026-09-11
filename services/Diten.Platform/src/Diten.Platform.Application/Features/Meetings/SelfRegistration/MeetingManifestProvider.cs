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
                    Actions: [])
            ],
            NotificationEvents: []);
}
