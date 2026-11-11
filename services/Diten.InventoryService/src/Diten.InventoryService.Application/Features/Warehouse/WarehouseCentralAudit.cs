using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common;

namespace Diten.InventoryService.Application.Features.Warehouse;

/// <summary>
/// BL-501 — what every MOD-0178 write command tells Platform's central audit log (pack §21; the <c>InventoryCentralAudit.For*</c> pattern,
/// that file unchanged). Category is the service's <see cref="InventoryCentralAudit.Category"/> (Platform has no inventory / warehouse
/// category; owner decision Beste 2026-10-26); the source module is the manifest's module code. The record carries no name and no body:
/// the before / after values of a role command live only in <c>wms_location_role_events</c> (pack §4.3, §8.8).
/// </summary>
public static class WarehouseCentralAudit
{
    /// <summary>= <c>PutawayPickingPackingManifestProvider.ModuleCode</c> (pack §14.4).</summary>
    public const string Module = "PUTAWAY-PICKING-PACKING";

    public static CentralAuditMetadata For(string operation, string entityType) => new(InventoryCentralAudit.Category, operation, entityType, Module);
}
