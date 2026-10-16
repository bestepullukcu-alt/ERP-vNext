namespace Diten.MdmService.Domain.Repositories;

public interface IGskuRetirementRequestWorkflowTenantDiscoveryRepository
{
    Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverAsync(Guid? afterTenantId, int limit,
        CancellationToken cancellationToken = default);
}
