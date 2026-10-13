using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Repositories;

/// <summary>GET /balance filters (contract <c>getBalance</c>). Tenant / LE are never part of a filter — they are the scope.</summary>
public sealed record BalanceQuery(
    Guid? ItemId,
    Guid? SkuId,
    SkuLevel? SkuLevel,
    string? WarehouseId,
    string? LocationId,
    string? LotId,
    StockStatus? StockStatus,
    int Page,
    int PageSize);
