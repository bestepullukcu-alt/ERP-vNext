namespace Diten.SupplyChainService.Domain.Features.Shipments;
// DetailsJson: the contract Error's `details` of a stored outcome (3.4.0 dispatch refusals), replayed unchanged.
public sealed record ShipmentMutationResult(Shipment? Shipment, bool Replay, int StatusCode, string? ErrorCode, string? DetailsJson = null);
