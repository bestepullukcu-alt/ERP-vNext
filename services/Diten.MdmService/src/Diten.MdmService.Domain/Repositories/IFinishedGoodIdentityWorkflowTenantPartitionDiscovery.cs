namespace Diten.MdmService.Domain.Repositories;

public interface IFinishedGoodIdentityWorkflowTenantPartitionDiscovery
{
    Task<FinishedGoodIdentityWorkflowTenantPartitionPage> DiscoverAsync(
        Guid? afterTenantId,
        long nowUtcTicks,
        int limit,
        CancellationToken cancellationToken = default);
}
