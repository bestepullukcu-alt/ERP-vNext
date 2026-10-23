using Diten.BuildingBlocks.CentralAudit;
namespace Diten.SupplyChainService.Application.Common;

/// <summary>
/// BL-501 (WP-A1, E-2) — what every SupplyChain write command tells Platform's central audit log (AUD-001 path b, the
/// shared forwarder). The in-product trails (shipment history and audit, carrier / load / return / claim / capacity /
/// S&amp;OP audit collections, the module outboxes) are unchanged: the central record is written NEXT to them, in the same
/// Mongo transaction (<c>CentralAuditTransaction.EnlistAsync</c> before each repository's commit), with the id of the
/// aggregate the transaction wrote.
///
/// <para><b>Category.</b> Platform's <c>AuditCategory</c> has no supply-chain value; every record is filed under
/// <see cref="Category"/> as Inventory (MVP-1) and the MVP-3 BOMs are. The module code and the entity type say what it
/// is. A dedicated category is a Platform enum change, left to Control Tower.</para>
///
/// <para><b>Source module</b> = the module code each manifest registers with Platform (a test compares them).</para>
/// </summary>
public static class SupplyChainCentralAudit
{
    public const string Category = "MasterData";

    public const string ShipmentModule = "shipment-tracking-pod";
    public const string CarrierModule = "carrier-management";
    public const string LoadModule = "routing-load-planning";
    public const string ReturnModule = "reverse-logistics";
    public const string ClaimModule = "claims-management";
    public const string CapacityModule = "capacity-planning";
    public const string SandopModule = "sop-workflow-signoffs";

    public const string Create = "Create";
    public const string Update = "Update";
    public const string Transition = "LifecycleTransition";
    public const string Execute = "Execute";

    public static CentralAuditMetadata For(string module, string operation, string entityType) => new(Category, operation, entityType, module);
}
