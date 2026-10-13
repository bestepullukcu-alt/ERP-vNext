using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.Inventory.Services;

/// <summary>
/// A structurally valid movement request. <see cref="Direction"/> is the normalised pack §12 direction; it is
/// null only for a REVERSAL, whose sides are derived from the original transaction.
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
    DateTimeOffset PostingDate);
