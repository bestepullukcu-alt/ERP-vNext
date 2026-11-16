using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Expiry.Enums;
using Diten.InventoryService.Domain.Expiry.Rules;

namespace Diten.InventoryService.Application.Features.Warehouse.Ports;

/// <summary>
/// MOD-0178 S2's FEFO read (pack §5 S2 row, §7 MOD-0176 row): the order of one company + warehouse + SKU by the SAME rule as
/// <c>getFefoOrder</c> (<c>GetFefoOrderHandler</c>: <see cref="Expiry.Ports.IExpiryReadModel.FefoCandidatesAsync"/> + <see cref="FefoOrderRule"/>,
/// <c>basis: onHandAvailable</c> — FU03 §8.5 known difference; the free base is the caller's, §8.17 step 2) and the company's FEFO outbound
/// mode (no policy row → the defaults, <c>RequireReason</c>). MOD-0176 files are read, never changed.
/// </summary>
public interface IWarehouseFefoReader
{
    Task<FefoOrder> OrderAsync(Guid warehouseId, Guid itemId, Guid skuId, SkuLevel skuLevel, int? minRemainingShelfLifeDays, DateTimeOffset asOf,
        CancellationToken cancellationToken = default);

    Task<FefoOutboundMode> OutboundModeAsync(CancellationToken cancellationToken = default);
}
