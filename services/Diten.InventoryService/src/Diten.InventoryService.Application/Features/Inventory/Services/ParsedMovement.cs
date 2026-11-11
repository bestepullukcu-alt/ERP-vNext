using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.Inventory.Services;

/// <summary>
/// A structurally valid movement request. <see cref="Direction"/> is the normalised pack §12 direction; it is
/// null only for a REVERSAL, whose sides are derived from the original transaction. MOD-0173-FU03 (1.7.0): <see cref="ReservationId"/> is
/// the trimmed text as sent (whether it names a reservation is a 422 of the posting path, never a 400); <see cref="RestoreReservation"/> is
/// true only on a REVERSAL that asked for it.
/// </summary>
public sealed record ParsedMovement(
    string IdempotencyKey,
    string? DocumentGroupId,
    MovementType MovementType,
    string? MovementReason,
    Guid ItemId,
    Guid SkuId,
    SkuLevel SkuLevel,
    decimal Quantity,
    string UomId,
    string? LotId,
    IReadOnlyList<string>? SerialIds,
    MovementDirection? Direction,
    string SourceModule,
    string SourceType,
    string? SourceDocumentId,
    string? SourceLineId,
    string? SourceSystem,
    DateTimeOffset TransactionDate,
    DateTimeOffset PostingDate,
    string? FefoBypassReason = null,
    string? ReservationId = null,
    bool RestoreReservation = false);
