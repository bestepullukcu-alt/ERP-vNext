namespace Diten.MdmService.Domain.Repositories;

public interface ILskuIdentityWorkflowTenantPartitionDiscovery
{
    Task<LskuIdentityWorkflowTenantPartitionPage> DiscoverAsync(
        Guid? afterTenantId,
        long nowUtcTicks,
        int limit,
        CancellationToken cancellationToken = default);
}
