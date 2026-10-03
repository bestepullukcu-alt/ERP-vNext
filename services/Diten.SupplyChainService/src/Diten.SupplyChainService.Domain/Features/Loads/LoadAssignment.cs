namespace Diten.SupplyChainService.Domain.Features.Loads;
public sealed record LoadAssignment(Guid TenantId, Guid LegalEntityId, Guid ShipmentId, Guid LoadId);
