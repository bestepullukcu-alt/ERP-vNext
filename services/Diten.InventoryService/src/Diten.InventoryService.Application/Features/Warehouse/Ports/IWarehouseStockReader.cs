using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Application.Features.Warehouse.Ports;

/// <summary>One INVENTORY balance row as MOD-0178 reads it (a copy for the request; never stored — pack §2 boundary rule 1).</summary>
public sealed record WarehouseStockRow(
    Guid ItemId,
    Guid SkuId,
    SkuLevel SkuLevel,
    Guid WarehouseId,
    Guid LocationId,
    string? LotId,
    StockStatus StockStatus,
    decimal OnHand,
    string BaseUomId,
    DateTimeOffset? LastPostingDate);

/// <summary>
/// The in-process READ of INVENTORY balances MOD-0178 needs (pack §8.2 candidates, §12 source bucket, §8.6 suggestion, §8.16 mixed status),
/// bound to the caller's Tenant + company. Read-only: MOD-0178 changes stock only through <see cref="IWarehouseMovementPoster"/>.
/// </summary>
public interface IWarehouseStockReader
{
    /// <summary>The company's rows with OnHand &gt; 0 at these locations, every stock status.</summary>
    Task<IReadOnlyList<WarehouseStockRow>> RowsAtAsync(IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken = default);

    /// <summary>The locations of a warehouse where this SKU (and lot, if given) is stocked, most recent posting first (§8.6).</summary>
    Task<IReadOnlyList<Guid>> LastStockedLocationsAsync(Guid warehouseId, Guid skuId, SkuLevel skuLevel, string? lotId, CancellationToken cancellationToken = default);
}
