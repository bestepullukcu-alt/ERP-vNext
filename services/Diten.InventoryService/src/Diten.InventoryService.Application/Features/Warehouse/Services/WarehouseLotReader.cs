using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Domain.Traceability.Repositories;

namespace Diten.InventoryService.Application.Features.Warehouse.Services;

/// <summary><see cref="IWarehouseLotReader"/> over the MOD-0174 lot store (tenant-bound, read-only). A typed (non-UUID) lot has no entry.</summary>
public sealed class WarehouseLotReader : IWarehouseLotReader
{
    private readonly ILotRepository _lots;

    public WarehouseLotReader(ILotRepository lots) => _lots = lots;

    public async Task<IReadOnlyDictionary<string, WarehouseLotInfo>> LotsAsync(IReadOnlyCollection<string> lotIds, CancellationToken cancellationToken = default)
    {
        var ids = lotIds.Select(l => Guid.TryParseExact(l, "D", out var id) ? id : (Guid?)null).OfType<Guid>().Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<string, WarehouseLotInfo>(StringComparer.Ordinal);
        var lots = await _lots.FindManyAsync(ids, cancellationToken);
        return lots.ToDictionary(l => l.Id.ToString("D"), l => new WarehouseLotInfo(l.Id.ToString("D"), l.LotNumber, l.Status), StringComparer.Ordinal);
    }
}
