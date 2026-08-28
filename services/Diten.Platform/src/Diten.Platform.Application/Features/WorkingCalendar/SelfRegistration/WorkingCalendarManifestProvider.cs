using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;
using Perms = Diten.Platform.Application.Features.WorkingCalendar.WorkingCalendarPermissions;

namespace Diten.Platform.Application.Features.WorkingCalendar.SelfRegistration;

/// <summary>
/// Working Calendar &amp; Public Holidays self-registration manifest.
/// <para>
/// <b>RoutePath is security-load-bearing here, not just navigation.</b> Permission scope is derived from each page's
/// route at sync time: <c>/Platform/…</c> becomes PlatformAdmin (not tenant-assignable), anything else becomes Tenant.
/// That is exactly the boundary this module needs, and it falls out automatically:
/// </para>
/// <list type="bullet">
/// <item><c>/Platform/WorkingCalendars</c> → PlatformAdmin — the country layer stays platform-only.</item>
/// <item><c>/WorkingCalendar/Overrides</c> → Tenant — the override keys become assignable to tenant roles even though
/// they sit in the <c>platform.</c> namespace, so no self-service allow-list entry is needed.</item>
/// </list>
/// <para>
/// Moving either route would silently mis-scope its permissions — the tenant page under <c>/Platform/…</c> would lock
/// tenants out of their own calendars, and the admin page outside it would offer the country layer to tenants.
/// </para>
/// <para><b>Actions mirror the UI, not the API.</b> Each action is placed on the page whose toolbar or row menu
/// actually shows the button, and every permission key below is a real constant the controllers enforce.</para>
/// </summary>
public sealed class WorkingCalendarManifestProvider : IModuleManifestProvider
{
    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "working-calendar",
            ModuleName: "Working Calendar",
            DisplayName: "Working Calendar & Public Holidays",
            Domain: "PlatformSharedServices",
            Service: "DitenPlatform",
            ModuleVersion: "1.0.0",
            // Shared foundation: every tenant needs working-day answers, and the per-user permission gate still
            // applies. Gating it behind an entitlement would let a tenant own overrides it could not read back.
            IsTenantAssignable: true,
            SortOrder: 90,
            Icon: "bx-calendar",
            IsBaseline: true,
            Pages:
            [
                // Country layer — platform-admin shell. The /Platform/ prefix is what makes these keys PlatformAdmin.
                new ModuleManifestPage(
                    PageCode: "WORKING_CALENDARS",
                    DisplayName: "Working Calendars",
                    RoutePath: "/Platform/WorkingCalendars",
                    RequiredPermission: Perms.Read,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions:
                    [
                        new ModuleManifestAction("WC_CREATE", "Add Calendar", Perms.Manage, "Create", 10, false, true, false),
                        new ModuleManifestAction("WC_EDIT", "Edit", Perms.Manage, "Update", 20, false, false, true),
                        new ModuleManifestAction("WC_ACTIVATE", "Activate", Perms.Activate, "Activate", 30, false, false, true),
                        new ModuleManifestAction("WC_ARCHIVE", "Archive", Perms.Manage, "Archive", 40, true, false, true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "WORKING_CALENDAR_CREATE",
                    DisplayName: "Add Working Calendar",
                    RoutePath: "/Platform/WorkingCalendars/Create",
                    RequiredPermission: Perms.Manage,
                    ParentPageCode: "WORKING_CALENDARS",
                    IsNavigationVisible: false,
                    PageType: "Form",
                    SortOrder: 20,
                    Actions: []),
                new ModuleManifestPage(
                    PageCode: "WORKING_CALENDAR_DETAILS",
                    DisplayName: "Working Calendar Details",
                    RoutePath: "/Platform/WorkingCalendars/Details",
                    RequiredPermission: Perms.Read,
                    ParentPageCode: "WORKING_CALENDARS",
                    IsNavigationVisible: false,
                    PageType: "Details",
                    SortOrder: 30,
                    Actions:
                    [
                        new ModuleManifestAction("WC_DAY_UPSERT", "Add/Edit Day", Perms.Manage, "Update", 10, false, true, true),
                        new ModuleManifestAction("WC_DAY_ARCHIVE", "Archive Day", Perms.Manage, "Archive", 20, true, false, true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "WORKING_CALENDAR_EDIT",
                    DisplayName: "Edit Working Calendar",
                    RoutePath: "/Platform/WorkingCalendars/Edit",
                    RequiredPermission: Perms.Manage,
                    ParentPageCode: "WORKING_CALENDARS",
                    IsNavigationVisible: false,
                    PageType: "Form",
                    SortOrder: 40,
                    Actions: []),

                // Tenant override layer — tenant shell. NOT under /Platform/, which is what derives the Tenant scope.
                new ModuleManifestPage(
                    PageCode: "WORKING_CALENDAR_OVERRIDES",
                    DisplayName: "Working Calendar Overrides",
                    RoutePath: "/WorkingCalendar/Overrides",
                    RequiredPermission: Perms.OverrideRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 50,
                    Actions:
                    [
                        new ModuleManifestAction("WCO_CREATE", "Add Override", Perms.OverrideManage, "Create", 10, false, true, false),
                        new ModuleManifestAction("WCO_EDIT", "Edit", Perms.OverrideManage, "Update", 20, false, false, true),
                        new ModuleManifestAction("WCO_ACTIVATE", "Activate", Perms.OverrideManage, "Activate", 30, false, false, true),
                        new ModuleManifestAction("WCO_ARCHIVE", "Archive", Perms.OverrideManage, "Archive", 40, true, false, true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "WORKING_CALENDAR_OVERRIDE_CREATE",
                    DisplayName: "Add Working Calendar Override",
                    RoutePath: "/WorkingCalendar/Overrides/Create",
                    RequiredPermission: Perms.OverrideManage,
                    ParentPageCode: "WORKING_CALENDAR_OVERRIDES",
                    IsNavigationVisible: false,
                    PageType: "Form",
                    SortOrder: 60,
                    Actions: []),
                new ModuleManifestPage(
                    PageCode: "WORKING_CALENDAR_OVERRIDE_DETAILS",
                    DisplayName: "Working Calendar Override Details",
                    RoutePath: "/WorkingCalendar/Overrides/Details",
                    RequiredPermission: Perms.OverrideRead,
                    ParentPageCode: "WORKING_CALENDAR_OVERRIDES",
                    IsNavigationVisible: false,
                    PageType: "Details",
                    SortOrder: 70,
                    Actions:
                    [
                        new ModuleManifestAction("WCO_DAY_UPSERT", "Add/Edit Day", Perms.OverrideManage, "Update", 10, false, true, true),
                        new ModuleManifestAction("WCO_DAY_ARCHIVE", "Archive Day", Perms.OverrideManage, "Archive", 20, true, false, true)
                    ]),
                new ModuleManifestPage(
                    PageCode: "WORKING_CALENDAR_OVERRIDE_EDIT",
                    DisplayName: "Edit Working Calendar Override",
                    RoutePath: "/WorkingCalendar/Overrides/Edit",
                    RequiredPermission: Perms.OverrideManage,
                    ParentPageCode: "WORKING_CALENDAR_OVERRIDES",
                    IsNavigationVisible: false,
                    PageType: "Form",
                    SortOrder: 80,
                    Actions: [])
            ]);
}
