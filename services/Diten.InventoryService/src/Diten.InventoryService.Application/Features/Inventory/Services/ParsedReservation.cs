using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Application.Features.Inventory.Services;

/// <summary>
/// A structurally valid reservation request. <see cref="Quantity"/> is in the SKU's base UoM (ASSUMPTION-S2-01). MOD-0173-FU03:
/// <see cref="WarehouseId"/> / <see cref="LotId"/> are the trimmed texts as sent (null = SKU level); whether they name a warehouse / a
/// registered lot is a 422 of the handler, not a structural rule (pack §12).
/// </summary>
public sealed record ParsedReservation(
    string IdempotencyKey,
    Guid ItemId,
    Guid SkuId,
    SkuLevel SkuLevel,
    decimal Quantity,
    string ReservationType,
    string SourceModule,
    string? SourceDocumentId,
    string? SourceLineId,
    int? Priority,
    DateTimeOffset? ExpiresAt,
    string? WarehouseId = null,
    string? LotId = null);
