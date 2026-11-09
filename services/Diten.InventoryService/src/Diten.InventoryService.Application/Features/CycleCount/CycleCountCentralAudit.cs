using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common;

namespace Diten.InventoryService.Application.Features.CycleCounting;

/// <summary>
/// BL-501 — what every MOD-0181 write command tells Platform's central audit log (pack §8.11, §21; the <c>WarehouseCentralAudit</c> pattern).
/// Category is the service's <see cref="InventoryCentralAudit.Category"/>; the source module is the manifest's module code. The record carries
/// no name and no body: the before / after values live only in <c>wms_count_events.Data</c> (same transaction).
/// </summary>
public static class CycleCountCentralAudit
{
    /// <summary>= <c>CycleCountingManifestProvider.ModuleCode</c> (pack §14, Q-16 = A).</summary>
    public const string Module = "CYCLE-COUNTING";

    public static CentralAuditMetadata For(string operation, string entityType) => new(InventoryCentralAudit.Category, operation, entityType, Module);
}
