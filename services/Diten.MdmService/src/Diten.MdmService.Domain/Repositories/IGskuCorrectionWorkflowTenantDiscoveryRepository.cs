namespace Diten.MdmService.Domain.Repositories;

public interface IGskuCorrectionWorkflowTenantDiscoveryRepository
{
    Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverAsync(Guid? afterTenantId, int limit,
        CancellationToken cancellationToken = default);
}
