using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Repositories;

/// <summary>GET /availability filters (contract <c>getAvailability</c>).</summary>
public sealed record AvailabilityFilter(
    Guid? ItemId,
    Guid? SkuId,
    SkuLevel? SkuLevel,
    string? WarehouseId,
    string? LocationId);
