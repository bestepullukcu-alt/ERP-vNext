namespace Diten.PlanningService.Application.Features.DemandPlanning.Contracts;

// LOCATION v1 uses string location IDs; a Warehouse is not inferred from another location type.
public interface ILocationV1Reader
{
    Task<bool?> IsWarehouseAsync(Guid tenantId, string locationId, string legalEntityId, CancellationToken cancellationToken);
}