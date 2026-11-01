using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;

namespace Diten.InventoryService.Application.Features.Warehouse.Services;

/// <summary>
/// <see cref="IWarehouseSerialReader"/> on the MOD-0175 reads (pack §5 seam row: the <c>SerialsAtAsync</c> read is reused, §8.16): the Active
/// serials at the location minus the serials of an open hold / open blocked record of the same bucket — the same exclusion as MOD-0175's
/// <c>StockActionHandlers.SerialsForAsync</c>. No new repository query, no MOD-0174 / 0175 file changed.
/// </summary>
public sealed class WarehouseSerialReader : IWarehouseSerialReader
{
    private readonly IQuarantineLedgerReader _ledgerReader;
    private readonly IQuarantineRepository _quarantine;

    public WarehouseSerialReader(IQuarantineLedgerReader ledgerReader, IQuarantineRepository quarantine)
    {
        _ledgerReader = ledgerReader;
        _quarantine = quarantine;
    }

    public async Task<IReadOnlyList<string>> AvailableSerialsAsync(WarehouseSerialBucket bucket, CancellationToken cancellationToken = default)
    {
        try
        {
            var warehouse = bucket.WarehouseId.ToString("D");
            var location = bucket.LocationId.ToString("D");
            var registered = await _ledgerReader.SerialsAtAsync(bucket.ItemId, bucket.SkuId, bucket.SkuLevel, bucket.LotId, warehouse, location, cancellationToken);
            if (registered.Count == 0) return [];

            var taken = new HashSet<string>(StringComparer.Ordinal);
            var holds = await _quarantine.ListHoldsAsync(new HoldQuery(null, warehouse, null, null, null, null, bucket.LotId, null, 1, 500), cancellationToken);
            foreach (var hold in holds.Items.Where(h => h.Status != HoldStatus.Decided && Same(h.ItemId, h.SkuId, h.SkuLevel, h.LotId, h.LocationId, bucket)))
            {
                taken.UnionWith(hold.SerialIds ?? []);
            }

            var blocked = await _quarantine.ListBlockedAsync(new BlockedQuery(null, null, false, null, BlockedStatus.Open, warehouse, 1, 500), cancellationToken);
            foreach (var record in blocked.Items.Where(r => Same(r.ItemId, r.SkuId, r.SkuLevel, r.LotId, r.LocationId, bucket)))
            {
                taken.UnionWith(record.SerialIds ?? []);
            }

            return registered.Where(s => !taken.Contains(s)).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new WarehouseInventoryUnavailableException("The serials of the bucket could not be read.", ex);
        }
    }

    private static bool Same(Guid itemId, Guid skuId, SkuLevel level, string? lotId, string locationId, WarehouseSerialBucket b)
        => itemId == b.ItemId && skuId == b.SkuId && level == b.SkuLevel && string.Equals(lotId, b.LotId, StringComparison.Ordinal)
           && string.Equals(locationId, b.LocationId.ToString("D"), StringComparison.Ordinal);
}
