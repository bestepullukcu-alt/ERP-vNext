using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.Inventory.Commands;

/// <summary>
/// POST /api/inventory/movements (contract <c>postMovement</c>) — the ONLY API that changes stock. Every field is
/// carried as the raw request string; <see cref="Services.MovementRequestParser"/> owns parsing so a wrongly
/// typed value is a contract 400, not a model-binding accident. There is no TenantId / LegalEntityId member:
/// scope comes from the signed JWT (tenant-audit DTO rule).
/// </summary>
public sealed record PostMovementCommand : IRequest<Response<MovementResponseDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForLedger("Create", "InventoryTransaction");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<MovementResponseDto>(response)?.TransactionId);

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

    /// <summary>INVENTORY 1.3.0 (MOD-0176 KF-B): the reason of an issue that is not from the first eligible FEFO lot (10–1000).</summary>
    public string? FefoBypassReason { get; init; }

    /// <summary>INVENTORY 1.7.0 (MOD-0173-FU03 §8.3): the reservation this issue consumes (or a LOCATION_TRANSFER moves); raw text.</summary>
    public string? ReservationId { get; init; }

    /// <summary>INVENTORY 1.7.0 (§8.3.1, CT-08 r3 boundary): REVERSAL only — give the consumed quantity back to its reservation. Absent = false.</summary>
    public bool? RestoreReservation { get; init; }
}
