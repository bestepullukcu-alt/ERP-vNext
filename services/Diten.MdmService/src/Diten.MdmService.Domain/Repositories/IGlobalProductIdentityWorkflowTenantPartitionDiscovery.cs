namespace Diten.MdmService.Domain.Repositories;

public interface IGlobalProductIdentityWorkflowTenantPartitionDiscovery
{
    Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverAsync(
        Guid? afterTenantId,
        long nowUtcTicks,
        int limit,
        CancellationToken cancellationToken = default);

    // BL-552 flow 3: the tenants that hold a submit stuck for a person. The work discovery above skips manual
    // reconciliation, so without this a tenant whose only submit is stuck would never be counted by the gauges.
    Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverStuckAsync(
        Guid? afterTenantId,
        int limit,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new GlobalProductIdentityWorkflowTenantPartitionPage([], null));
}
