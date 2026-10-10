namespace Diten.SupplyChainService.Domain.Features.Shipments;
public sealed record ShipmentScope(Guid TenantId, Guid LegalEntityId, Guid ActorId);
