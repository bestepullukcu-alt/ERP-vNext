using Diten.BuildingBlocks.CentralAudit;

namespace Diten.ProcurementService.Application.Features.Supplier;

/// <summary>
/// MOD-0140 §21 "Audited Events" (BL-501, path b): the central record each marked supplier command files. Category
/// <c>MasterData</c> (Platform's <c>AuditCategory</c> has no purchasing value — the Inventory / MVP-3 choice), source module
/// <c>procurement-suppliers</c> (the manifest's <c>ModuleCode</c>). Operations are Platform <c>AuditOperation</c> names. The
/// record never carries the request body (AUD-001 §3). The entity id is given by the store when it enlists the event in
/// the write's own transaction (the relationship's or the supplier's id, §21 "Nesne"), so the response is not read.
/// </summary>
internal static class SupplierCentralAudit
{
    public const string Category = "MasterData";
    public const string SourceModule = "procurement-suppliers";
    public const string SupplierEntity = "Supplier";
    public const string RelationshipEntity = "SupplierCompanyRelationship";

    public static CentralAuditMetadata Of(string operation, string entityType) => new(Category, operation, entityType, SourceModule);
}
