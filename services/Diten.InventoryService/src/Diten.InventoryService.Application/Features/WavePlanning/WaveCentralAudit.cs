using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common;

namespace Diten.InventoryService.Application.Features.WavePlanning;

/// <summary>
/// BL-501 — what every MOD-0180 write command tells Platform's central audit log (pack §8.13, §21; the <c>WarehouseCentralAudit</c> pattern).
/// Category is the service's <see cref="InventoryCentralAudit.Category"/>; the source module is the manifest's module code. The record carries
/// no name and no body: the before / after values and the reasons live only in <c>wms_wave_events.Data</c> (same transaction).
/// </summary>
public static class WaveCentralAudit
{
    /// <summary>= <c>WavePlanningManifestProvider.ModuleCode</c> (pack §14).</summary>
    public const string Module = "WAVE-PLANNING";

    public static CentralAuditMetadata For(string operation, string entityType) => new(InventoryCentralAudit.Category, operation, entityType, Module);
}
