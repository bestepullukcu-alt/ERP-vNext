using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Diten.InventoryService.Api.ModuleRegistration;
using Diten.InventoryService.Infrastructure.Authorization;

namespace Diten.InventoryService.Api.Recall;

/// <summary>
/// MOD-0177 Recall Readiness self-registration manifest (pack §14.4): module code <c>RECALL-READINESS</c>, SortOrder 177, namespace
/// <c>recall.*</c> ONLY (FU-0174-CAT-01: a foreign key in a manifest moves that key to this module in the catalog).
/// <para><b>WP-0177-R1: NO PAGE YET</b> (the 0176 F1 "sayfasız ilk dilim" pattern). The screens come with R6 / R7; a page registered now
/// would put a screen that does not exist into the live tenant's menu. So the R1 keys — exactly the ones the R1 controller enforces — are
/// declared as <c>ApiPermissions</c> (Scope Tenant, module-self-registration standard §2d) until their pages and buttons replace them.
/// The Platform reconcile accepts a page-less manifest (measured by 0176 F1).</para>
/// <para><b>WP-0177-R6:</b> the first three pages (pack §11.1) — Hazırlık Paneli, İzleme (+ "CSV indir" / "Vaka aç" buttons, §11.3) and Vakalar;
/// their keys leave <c>ApiPermissions</c> (the page / action declares them). The other keys stay API-only until their R7 pages.</para>
/// </summary>
public sealed class RecallReadinessManifestProvider : IModuleManifestProvider
{
    public const string ModuleCode = "RECALL-READINESS";

    public ModuleManifestDocument GetManifest() =>
        new(
            ModuleCode: ModuleCode,
            ModuleName: "Recall Readiness",
            DisplayName: "Recall",
            Domain: "SUPPLYCHAINEXECUTION",
            Service: "Inventory",
            ModuleVersion: "1.0.0",
            IsTenantAssignable: true,
            SortOrder: 177,
            Pages:
            [
                new ModuleManifestPage(
                    PageCode: "RECALLDASHBOARD",
                    DisplayName: "Recall readiness dashboard",
                    RoutePath: "/Recall",
                    RequiredPermission: RecallPermissions.DashboardRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 10,
                    Actions: []),
                new ModuleManifestPage(
                    PageCode: "RECALLTRACE",
                    DisplayName: "Recall trace",
                    RoutePath: "/Recall/Trace",
                    RequiredPermission: RecallPermissions.TraceRun,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "Tree",
                    SortOrder: 20,
                    Actions:
                    [
                        new ModuleManifestAction("RECALL_TRACE_EXPORT", "Download CSV", RecallPermissions.TraceExport, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("RECALL_CASE_OPEN", "Open recall case", RecallPermissions.CasesCreate, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "RECALLCASES",
                    DisplayName: "Recall cases",
                    RoutePath: "/Recall/Cases",
                    RequiredPermission: RecallPermissions.CasesRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 30,
                    Actions: [])
            ],
            Icon: "bx-revision",
            ApiPermissions:
            [
                new ModuleManifestPermission(RecallPermissions.CasesUpdate, "Edit and submit a recall scope", "Tenant"),
                new ModuleManifestPermission(RecallPermissions.CasesApprove, "Approve or return a recall scope", "Tenant"),
                new ModuleManifestPermission(RecallPermissions.CasesCancel, "Cancel a recall case before approval", "Tenant"),
                new ModuleManifestPermission(RecallPermissions.CasesClose, "Close a recall case", "Tenant"),
                new ModuleManifestPermission(RecallPermissions.EffectivenessComplete, "Run the recall effectiveness check", "Tenant"),
                new ModuleManifestPermission(RecallPermissions.SegregationsConfirm, "Confirm physical segregation of recalled stock", "Tenant"),
                new ModuleManifestPermission(RecallPermissions.NotificationsCreate, "Record a customer notification", "Tenant"),
                new ModuleManifestPermission(RecallPermissions.ConsumptionsCreate, "Record a consumption declaration", "Tenant"),
                new ModuleManifestPermission(RecallPermissions.SettingsRead, "Read the recall settings", "Tenant"),
                new ModuleManifestPermission(RecallPermissions.SettingsManage, "Change the recall settings", "Tenant"),
                // API-only by design (pack §14.2): the lot / movement screens of 0174 / 0173 read it; it has no page of its own
                new ModuleManifestPermission(RecallPermissions.LotsStatusRead, "Read whether a lot is under recall", "Tenant"),
                // WP-0177-R3: KR-B "Karantina isteği gönder / Yeniden dene", KR-H "İadeyi bağla" — API-only until the R7 case detail page
                new ModuleManifestPermission(RecallPermissions.QuarantineRequestsCreate, "Send / retry a recall quarantine request", "Tenant"),
                new ModuleManifestPermission(RecallPermissions.ReturnsLink, "Link a customer return to a recall case", "Tenant"),
                // WP-0177-R5: the drills (pack §8.10, KR-G) — API-only until the R6 "Tatbikat Planlayıcı" page
                new ModuleManifestPermission(RecallPermissions.DrillsRead, "Read recall drill plans and runs", "Tenant"),
                new ModuleManifestPermission(RecallPermissions.DrillsManage, "Create and edit recall drill plans", "Tenant"),
                new ModuleManifestPermission(RecallPermissions.DrillsRun, "Run, sign or cancel a recall drill", "Tenant")
            ]);
}
