using Diten.PlanningService.Application.Features.DemandPlanning.Contracts;

namespace Diten.PlanningService.Infrastructure.Features.DemandPlanning;

// Explicit fail-closed adapter. Tests may replace it with fixtures from frozen contracts.
// It never calls another service and never presents mock data as a live producer.
public sealed class UnconfiguredFrozenContractReaders :
    IProductMasterV1Reader, ILocationV1Reader, IDispatchHistoryReader
{
    public Task<string?> GetBaseUomIdAsync(Guid tenantId, Guid legalEntityId, Guid skuId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Frozen Product Master v1 fixture is not configured.");

    public Task<bool?> IsWarehouseAsync(Guid tenantId, string locationId, string legalEntityId,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Frozen LOCATION v1 fixture is not configured.");

    public Task<bool?> HasVerifiedCoverageAsync(Guid tenantId, Guid legalEntityId, string warehouseId, DateOnly from, DateOnly through,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Verified dispatch history source is not configured.");

}