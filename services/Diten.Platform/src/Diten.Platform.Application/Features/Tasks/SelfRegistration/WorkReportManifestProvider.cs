using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.Tasks.SelfRegistration;

/// <summary>
/// MOD-0024 / İş Raporu — the Work Report's own self-registration manifest.
///
/// <para><b>Why a MODULE and not a page under "Görev Tanımları".</b> The tenant sidebar groups by MODULE, not by
/// page hierarchy: <c>GetTenantNavigationMenuQueryHandler</c> returns one group per entitled module, and
/// <c>DynamicModuleMenuViewComponent.BuildTree</c> already treats a page whose <c>ParentPageCode</c> names a
/// nav-INVISIBLE page (here: <c>TASKS</c>) as top level. So while this page lived in the Task Engine manifest,
/// no value of <c>ParentPageCode</c> could move it out of the "Görev Tanımları / Task Settings" group — the
/// group is the module. Measured consequence of leaving it there: the permissions diverge (the definition pages
/// are behind <c>*Manage</c> keys, the report behind <c>WorkReportRead</c>), and
/// <c>Default.cshtml</c> shows a module group as soon as ANY of its pages is permitted — so a manager who may
/// read the report but configure nothing saw a "Görev Tanımları" heading containing no definition at all.
/// A report is not a definition; it gets its own identity.</para>
///
/// <para><b>Single page on purpose.</b> <c>Default.cshtml</c> renders a one-page module as a flat sidebar link
/// labelled by the MODULE name (<c>IsSinglePage</c>), which is exactly the shape wanted here — "İş Raporu" as a
/// sibling of "Görev Merkezi", not a collapsible group wrapping a single child. That is also why the sidebar
/// label comes from <c>Nav.Module.WORKREPORT</c>; <c>Nav.Page.TASKWORKREPORT</c> stays as the Ctrl+K / search
/// label and is unchanged.</para>
///
/// <para><b>What deliberately did NOT change.</b> The page code (<c>TASK_WORK_REPORT</c>), the route
/// (<c>/Tasks/WorkReport</c>) and both permission keys are carried over verbatim. Page codes key the nav l10n
/// bridge in seven languages and permission keys are immutable identities in AuthService — renaming either would
/// break translations or role grants for a menu move. Only <c>permission.Module</c> follows the page
/// (<c>tasks</c> → <c>work-report</c>), and that field is a Role Assignment GROUPING label, not an authority.</para>
///
/// <para><b>Entitlement posture.</b> <c>IsBaseline = false</c> + <c>IsTenantAssignable = true</c>, mirroring
/// Task Engine and Görev Merkezi. ⚠ MIGRATION: a tenant that reaches the report today rides the TASKS
/// entitlement; it now needs a <c>WORK-REPORT</c> entitlement row (the platform system tenant is exempt via the
/// BL-059 bypass, so dev is unaffected).</para>
/// </summary>
public sealed class WorkReportManifestProvider : IModuleManifestProvider
{
    // Unchanged from the Task Engine manifest — see the "what did NOT change" note above.
    private const string PageWorkReport = "TASK_WORK_REPORT";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: "work-report",
            ModuleName: "Work Report",
            // FALLBACK only — the sidebar prints Nav.Module.WORKREPORT (all seven tenant languages). This is what
            // the operator sees in the module catalog, and what renders if that key is ever missing.
            DisplayName: "İş Raporu / Work Report",
            Domain: "Workspace",
            Service: "DitenPlatform",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            // Between Görev Merkezi (10) and Görev Tanımları (20): do the work → measure the work → configure the
            // work. The three read in that order in the Çalışma Alanı domain group.
            SortOrder: 15,
            Icon: "bx-bar-chart-alt-2",
            IsBaseline: false,
            Pages:
            [
                /*
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
                    // Top level: this module's only page. A non-null parent here would name a page that does not
                    // exist in this module.
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
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
            ]);
}
