using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Application.Features.Inventory.Services;

/// <summary>A structurally valid reservation request. <see cref="Quantity"/> is in the SKU's base UoM (ASSUMPTION-S2-01).</summary>
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
    DateTimeOffset? ExpiresAt);
