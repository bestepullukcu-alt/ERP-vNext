namespace Diten.SupplyChainService.Domain.Features.Claims;
public sealed record ClaimReferenceSnapshot(Guid ShipmentId, string ShipmentStatus, Guid? ShipmentCarrierId, string? CarrierStatus, Guid Root, DateTimeOffset ObservedAt);
