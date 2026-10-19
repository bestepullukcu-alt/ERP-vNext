namespace Diten.PlanningService.Application.Features.DemandPlanning.Contracts;

// Consumer port for the frozen PRODUCT-MASTER-BUNDLE v1 SKU/UoM seam.
public interface IProductMasterV1Reader
{
    Task<string?> GetBaseUomIdAsync(Guid tenantId, Guid legalEntityId, Guid skuId, CancellationToken cancellationToken);
}