namespace Diten.MdmService.Domain.Repositories;

public interface ILskuIdentityWorkflowTenantPartitionDiscovery
{
    Task<LskuIdentityWorkflowTenantPartitionPage> DiscoverAsync(
        Guid? afterTenantId,
        long nowUtcTicks,
        int limit,
        CancellationToken cancellationToken = default);

    // LSKU FIX2 item 3 (the GSKU identity runner's gauge): over all tenants, numbers only — the partition discovery
    // leaves the tenants out whose operations are stuck (nothing is due there), so they are counted here.
    // Stuck = manual reconciliation held for the requester or a person, or waiting for the maker's replay.
    Task<long> CountManualReconciliationAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
    // Of those, the ones the requester's repeat cannot settle — a person's (BL-530).
    Task<long> CountNeedingAPersonAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
}
