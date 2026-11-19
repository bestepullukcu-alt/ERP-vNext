using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Application.Features.Warehouse.Ports;

/// <summary>The bucket a serial read is about (warehouse + location + SKU + level + lot).</summary>
public sealed record WarehouseSerialBucket(Guid ItemId, Guid SkuId, SkuLevel SkuLevel, string? LotId, Guid WarehouseId, Guid LocationId);

/// <summary>
/// The serials a putaway moves (pack §5 seam row, §8.16; MOD-0175 precedent): the Active registered serials at the location MINUS the serials an
/// open 0175 hold or blocked record of the same bucket names (ASSUMPTION-0175-Q4-02). Serial NUMBERS, in intake order. Unreadable →
/// <see cref="WarehouseInventoryUnavailableException"/> (503 INVENTORY_UNAVAILABLE).
/// </summary>
public interface IWarehouseSerialReader
{
    Task<IReadOnlyList<string>> AvailableSerialsAsync(WarehouseSerialBucket bucket, CancellationToken cancellationToken = default);
}

/// <summary>An in-process stock read failed; nothing was written (503 INVENTORY_UNAVAILABLE).</summary>
public sealed class WarehouseInventoryUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
