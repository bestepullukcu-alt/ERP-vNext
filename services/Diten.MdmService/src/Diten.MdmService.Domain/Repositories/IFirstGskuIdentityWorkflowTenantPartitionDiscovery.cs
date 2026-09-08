namespace Diten.MdmService.Domain.Repositories;

public interface IFirstGskuIdentityWorkflowTenantPartitionDiscovery
{
    Task<FirstGskuIdentityWorkflowTenantPartitionPage> DiscoverAsync(
        Guid? afterTenantId, long nowUtcTicks, int limit, CancellationToken cancellationToken = default);
}
