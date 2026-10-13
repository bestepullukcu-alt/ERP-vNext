using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.ValueObjects;

/// <summary>
/// Canonical uniqueness key of a balance row (pack §4) — minus TenantId / LegalEntityId, which are not values the
/// caller or the handler chooses: the repository adds them from the resolved scope on every read and write.
/// Serial is tracked on transactions, not in the key. <see cref="OwnerId"/> / <see cref="ProjectId"/> are reserved
/// for P2 and always null in S1.
/// </summary>
public sealed record InventoryDimension(
    Guid ItemId,
    Guid SkuId,
    SkuLevel SkuLevel,
    string? WarehouseId,
    string? LocationId,
    string? LotId,
    StockStatus StockStatus,
    string? OwnerId = null,
    string? ProjectId = null);
