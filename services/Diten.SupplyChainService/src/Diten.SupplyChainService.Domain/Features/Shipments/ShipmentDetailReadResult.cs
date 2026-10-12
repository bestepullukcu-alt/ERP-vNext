namespace Diten.SupplyChainService.Domain.Features.Shipments;

public enum RawRootState { Missing, ExplicitNull, InvalidStoredValue, PresentStoredUuid }

public sealed record ShipmentDetailReadResult(Shipment Shipment, RawRootState RootState, Guid? LifecycleCorrelationId);
