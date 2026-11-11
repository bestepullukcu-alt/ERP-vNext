using Diten.InventoryService.Domain.Traceability.Enums;

namespace Diten.InventoryService.Application.Features.Warehouse.Ports;

/// <summary>A registered lot as MOD-0178 shows and checks it (TRACE, read in-process).</summary>
public sealed record WarehouseLotInfo(string LotId, string LotNumber, LotStatus Status);

/// <summary>TRACE lot reads (pack §7: lot status / number). A ledger lot that is not a registered lot (typed text) has no entry.</summary>
public interface IWarehouseLotReader
{
    Task<IReadOnlyDictionary<string, WarehouseLotInfo>> LotsAsync(IReadOnlyCollection<string> lotIds, CancellationToken cancellationToken = default);
}
