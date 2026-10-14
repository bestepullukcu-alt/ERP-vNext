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
            Pages: [],
            Icon: "bx-revision",
            ApiPermissions:
            [
                new ModuleManifestPermission(RecallPermissions.CasesRead, "Read recall cases", "Tenant"),
                new ModuleManifestPermission(RecallPermissions.CasesCreate, "Open a recall case", "Tenant"),
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
                // WP-0177-R2: the trace (pack §8.2) — API-only until the R6 "İzleme" page
                new ModuleManifestPermission(RecallPermissions.TraceRun, "Run a recall trace", "Tenant"),
                new ModuleManifestPermission(RecallPermissions.TraceExport, "Export a recall trace as CSV", "Tenant"),
                // WP-0177-R3: KR-B "Karantina isteği gönder / Yeniden dene", KR-H "İadeyi bağla" — API-only until the R7 case detail page
                new ModuleManifestPermission(RecallPermissions.QuarantineRequestsCreate, "Send / retry a recall quarantine request", "Tenant"),
                new ModuleManifestPermission(RecallPermissions.ReturnsLink, "Link a customer return to a recall case", "Tenant"),
                // WP-0177-R5: the drills (pack §8.10, KR-G) — API-only until the R6 "Tatbikat Planlayıcı" page
                new ModuleManifestPermission(RecallPermissions.DrillsRead, "Read recall drill plans and runs", "Tenant"),
                new ModuleManifestPermission(RecallPermissions.DrillsManage, "Create and edit recall drill plans", "Tenant"),
                new ModuleManifestPermission(RecallPermissions.DrillsRun, "Run, sign or cancel a recall drill", "Tenant")
            ]);
}
