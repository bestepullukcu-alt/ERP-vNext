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
/// <para><b>WP-0177-R7a:</b> the case detail page RECALLCASEDETAILS (not in the menu, parent RECALLCASES) with one action per §11.3 button; the case
/// keys leave <c>ApiPermissions</c>. Settings and drill keys stay API-only until their R7b pages.</para>
/// <para><b>WP-0177-R7b:</b> Tatbikat Planlayıcı, Kanıt Paketi and Ayarlar (pack §11.1) with their §11.3 buttons; only <c>recall.lots.status.read</c>
/// stays an API permission (by design, pack §14.2).</para>
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
                    Actions: []),
                new ModuleManifestPage(
                    PageCode: "RECALLCASEDETAILS",
                    DisplayName: "Recall case",
                    RoutePath: "/Recall/Cases/Details/{id}",
                    RequiredPermission: RecallPermissions.CasesRead,
                    ParentPageCode: "RECALLCASES",
                    IsNavigationVisible: false,
                    PageType: "Detail",
                    SortOrder: 31,
                    Actions:
                    [
                        new ModuleManifestAction("RECALL_SCOPE_MARK", "Include / exclude a scope item", RecallPermissions.CasesUpdate, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("RECALL_SCOPE_SUBMIT", "Submit the scope for approval", RecallPermissions.CasesUpdate, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("RECALL_SCOPE_RETURN", "Return the scope to draft", RecallPermissions.CasesApprove, "Toolbar", 30, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("RECALL_SCOPE_APPROVE", "Approve the scope", RecallPermissions.CasesApprove, "Toolbar", 40, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("RECALL_SCOPE_REVISE", "Revise the scope", RecallPermissions.CasesUpdate, "Toolbar", 50, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("RECALL_QUARANTINE_REQUEST", "Send / retry a quarantine request", RecallPermissions.QuarantineRequestsCreate, "Toolbar", 60, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("RECALL_SEGREGATION_CONFIRM", "Confirm physical segregation", RecallPermissions.SegregationsConfirm, "Toolbar", 70, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("RECALL_CUSTOMER_NOTIFY", "Record a customer notification", RecallPermissions.NotificationsCreate, "Toolbar", 80, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("RECALL_CONSUMPTION_DECLARE", "Add a consumption declaration", RecallPermissions.ConsumptionsCreate, "Toolbar", 90, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("RECALL_RETURN_LINK", "Link a customer return", RecallPermissions.ReturnsLink, "Toolbar", 100, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("RECALL_EFFECTIVENESS_START", "Start the effectiveness check", RecallPermissions.EffectivenessComplete, "Toolbar", 110, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("RECALL_EFFECTIVENESS_COMPLETE", "Complete the effectiveness check", RecallPermissions.EffectivenessComplete, "Toolbar", 120, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("RECALL_CASE_CLOSE", "Close the recall case", RecallPermissions.CasesClose, "Toolbar", 130, IsDangerous: true, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("RECALL_CASE_CANCEL", "Cancel the recall case", RecallPermissions.CasesCancel, "Toolbar", 140, IsDangerous: true, IsToolbarAction: true, IsRowAction: false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "RECALLDRILLS",
                    DisplayName: "Recall drills",
                    RoutePath: "/Recall/Drills",
                    RequiredPermission: RecallPermissions.DrillsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "List",
                    SortOrder: 40,
                    Actions:
                    [
                        new ModuleManifestAction("RECALL_DRILL_PLAN_SAVE", "Add / edit a drill plan", RecallPermissions.DrillsManage, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("RECALL_DRILL_RUN", "Start / step / cancel a drill", RecallPermissions.DrillsRun, "Toolbar", 20, IsDangerous: false, IsToolbarAction: true, IsRowAction: false),
                        new ModuleManifestAction("RECALL_DRILL_SIGN", "Sign the drill result", RecallPermissions.DrillsRun, "Toolbar", 30, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "RECALLEVIDENCE",
                    DisplayName: "Recall evidence pack",
                    RoutePath: "/Recall/Evidence",
                    RequiredPermission: RecallPermissions.EvidenceExport,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "Detail",
                    SortOrder: 50,
                    Actions:
                    [
                        new ModuleManifestAction("RECALL_EVIDENCE_EXPORT", "Download the evidence pack (PDF / CSV)", RecallPermissions.EvidenceExport, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ]),
                new ModuleManifestPage(
                    PageCode: "RECALLSETTINGS",
                    DisplayName: "Recall settings",
                    RoutePath: "/Recall/Settings",
                    RequiredPermission: RecallPermissions.SettingsRead,
                    ParentPageCode: null,
                    IsNavigationVisible: true,
                    PageType: "Form",
                    SortOrder: 60,
                    Actions:
                    [
                        new ModuleManifestAction("RECALL_SETTINGS_SAVE", "Save the recall settings", RecallPermissions.SettingsManage, "Toolbar", 10, IsDangerous: false, IsToolbarAction: true, IsRowAction: false)
                    ])
            ],
            Icon: "bx-revision",
            ApiPermissions:
            [
                // WP-0177-R7a: the case keys (update / approve / cancel / close / effectiveness / segregation / notification / consumption /
                // quarantine request / return link) moved to the RECALLCASEDETAILS buttons (pack §11.3)
                // WP-0177-R7b: the drill, evidence and settings keys moved to the RECALLDRILLS / RECALLEVIDENCE / RECALLSETTINGS pages and buttons
                // API-only by design (pack §14.2): the lot / movement screens of 0174 / 0173 read it; it has no page of its own
                new ModuleManifestPermission(RecallPermissions.LotsStatusRead, "Read whether a lot is under recall", "Tenant")
            ]);
}
