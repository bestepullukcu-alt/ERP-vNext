namespace Diten.MdmService.Domain.Repositories;

public interface IFinishedGoodIdentityWorkflowTenantPartitionDiscovery
{
    Task<FinishedGoodIdentityWorkflowTenantPartitionPage> DiscoverAsync(
        Guid? afterTenantId,
        int limit,
        CancellationToken cancellationToken = default);
}
