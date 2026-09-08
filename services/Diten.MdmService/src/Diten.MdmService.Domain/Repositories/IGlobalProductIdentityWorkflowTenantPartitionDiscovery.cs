namespace Diten.MdmService.Domain.Repositories;

public interface IGlobalProductIdentityWorkflowTenantPartitionDiscovery
{
    Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverAsync(
        Guid? afterTenantId,
        long nowUtcTicks,
        int limit,
        CancellationToken cancellationToken = default);
}
