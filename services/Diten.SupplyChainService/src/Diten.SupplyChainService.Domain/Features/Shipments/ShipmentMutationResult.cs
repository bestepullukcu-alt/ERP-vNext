namespace Diten.SupplyChainService.Domain.Features.Shipments;
public sealed record ShipmentMutationResult(Shipment? Shipment, bool Replay, int StatusCode, string? ErrorCode);
