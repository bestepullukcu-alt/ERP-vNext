namespace Diten.MdmService.Domain.Repositories;

public interface IFirstGskuIdentityWorkflowTenantPartitionDiscovery
{
    Task<FirstGskuIdentityWorkflowTenantPartitionPage> DiscoverAsync(
        Guid? afterTenantId, long nowUtcTicks, int limit, CancellationToken cancellationToken = default);

    // FIX3 item 1 (B13): how many identity operations wait for a person, over all tenants (a number only — no tenant
    // data). The partition discovery leaves those tenants out (nothing is due there), so the count is taken here.
    Task<long> CountManualReconciliationAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
}
