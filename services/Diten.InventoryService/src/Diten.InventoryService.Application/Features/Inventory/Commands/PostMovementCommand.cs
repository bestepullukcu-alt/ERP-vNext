using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Inventory.Commands;

/// <summary>
/// POST /api/inventory/movements (contract <c>postMovement</c>) — the ONLY API that changes stock. Every field is
/// carried as the raw request string; <see cref="Services.MovementRequestParser"/> owns parsing so a wrongly
/// typed value is a contract 400, not a model-binding accident. There is no TenantId / LegalEntityId member:
/// scope comes from the signed JWT (tenant-audit DTO rule).
/// </summary>
public sealed record PostMovementCommand : IRequest<Response<MovementResponseDto>>
{
    /// <summary>The <c>Idempotency-Key</c> header; must equal <see cref="IdempotencyKey"/>.</summary>
    public string? HeaderIdempotencyKey { get; init; }

    public string? IdempotencyKey { get; init; }
    public string? DocumentGroupId { get; init; }
    public string? MovementType { get; init; }
    public string? MovementReason { get; init; }
    public string? ItemId { get; init; }
    public string? SkuId { get; init; }
    public string? SkuLevel { get; init; }
    public string? FromWarehouseId { get; init; }
    public string? FromLocationId { get; init; }
    public string? ToWarehouseId { get; init; }
    public string? ToLocationId { get; init; }
    public string? Quantity { get; init; }
    public string? UomId { get; init; }
    public string? LotId { get; init; }
    public IReadOnlyList<string>? SerialIds { get; init; }
    public string? FromStockStatus { get; init; }
    public string? ToStockStatus { get; init; }
    public string? SourceModule { get; init; }
    public string? SourceType { get; init; }
    public string? SourceDocumentId { get; init; }
    public string? SourceLineId { get; init; }
    public string? SourceSystem { get; init; }
    public string? TransactionDate { get; init; }
    public string? PostingDate { get; init; }
}
