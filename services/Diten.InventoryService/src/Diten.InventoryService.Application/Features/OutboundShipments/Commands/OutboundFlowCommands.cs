using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Warehouse;
using MediatR;

namespace Diten.InventoryService.Application.Features.OutboundShipments.Commands;

// MOD-0178 DL-5 — the S2 outbound flow commands (pack §3, §8.21, §8.22; WMS draft §11). One central record each (pack §21): 1 only when the
// command wrote; a command that answers without a write (the dispatch sync seeing no dispatch) leaves none.

/// <summary><c>cancelOutboundShipment</c> — Allocated / Picked / Packed only, reason 10–1000, If-Match (§8.22).</summary>
public sealed record CancelOutboundShipmentCommand(Guid OutboundId, string? Reason, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<OutboundShipmentViewDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("LifecycleTransition", "OutboundShipment");
    public Guid? CentralAuditEntityId(object? response) => OutboundId;
}

/// <summary><c>releaseOutboundShipment</c> — Packed → ReadyToShip with the <c>OutboundShipmentReady</c> outbox row in the same transaction (§8.21).</summary>
public sealed record ReleaseOutboundShipmentCommand(Guid OutboundId, IReadOnlyList<ReleaseReasonInput> FefoBypassReasons, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<OutboundShipmentViewDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Execute", "OutboundShipment");
    public Guid? CentralAuditEntityId(object? response) => OutboundId;
}

/// <summary><c>syncOutboundDispatch</c> — "Sevkiyatı kontrol et": the MVP-6 shipment of the outbound → Shipped (§8.21, v1 user-triggered).</summary>
public sealed record SyncOutboundDispatchCommand(Guid OutboundId, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<SyncDispatchResultDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Update", "OutboundShipment");
    public Guid? CentralAuditEntityId(object? response) => OutboundId;
}

/// <summary>
/// The maintenance service's internal command (§8.17 step 6, §8.22): the <c>Pending</c> reservation releases of one outbound, in its own scope
/// with the outbound's creator as actor. Nothing pending → success without a write (no central row).
/// </summary>
public sealed record ReleaseOutboundReservationsCommand(Guid OutboundId)
    : IRequest<Response<bool>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => WarehouseCentralAudit.For("Execute", "OutboundShipment");
    public Guid? CentralAuditEntityId(object? response) => OutboundId;
}
