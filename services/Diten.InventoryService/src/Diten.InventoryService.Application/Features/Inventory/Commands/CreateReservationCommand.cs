using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Inventory.Commands;

/// <summary>
/// POST /api/inventory/reservations (contract <c>createReservation</c>) — physical hold at SKU level (pack
/// ASSUMPTION-0173-12). Raw request strings; <see cref="Services.ReservationRequestParser"/> owns parsing. No
/// TenantId / LegalEntityId member: scope comes from the signed JWT.
/// </summary>
public sealed record CreateReservationCommand : IRequest<Response<ReservationResponseDto>>
{
    /// <summary>The <c>Idempotency-Key</c> header; must equal <see cref="IdempotencyKey"/>.</summary>
    public string? HeaderIdempotencyKey { get; init; }

    public string? IdempotencyKey { get; init; }
    public string? ItemId { get; init; }
    public string? SkuId { get; init; }
    public string? SkuLevel { get; init; }
    public string? Quantity { get; init; }
    public string? BaseQuantity { get; init; }
    public string? ReservationType { get; init; }
    public string? SourceModule { get; init; }
    public string? SourceDocumentId { get; init; }
    public string? SourceLineId { get; init; }

    /// <summary>The JSON number as sent (raw text), parsed as a 32-bit integer.</summary>
    public string? Priority { get; init; }

    public string? ExpiresAt { get; init; }
}
