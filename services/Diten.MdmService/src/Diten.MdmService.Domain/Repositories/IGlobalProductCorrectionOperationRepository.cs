using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public interface IGlobalProductCorrectionOperationRepository
{
    // BL-552 flow 3 FIX3 item 5: tenants holding a STUCK request (what the gauges count), for a worker switched off.
    Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverStuckTenantPartitionsAsync(Guid? afterTenantId,
        int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult(new GlobalProductIdentityWorkflowTenantPartitionPage([], null));

    Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverTenantPartitionsAsync(
        Guid? afterTenantId,
        int limit,
        CancellationToken cancellationToken = default);
    Task<GlobalProductCorrectionReserveResult> ReserveAsync(
        GlobalProductCorrectionOperation operation,
        CancellationToken cancellationToken = default);
    Task<GlobalProductCorrectionOperation?> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);
    Task<GlobalProductCorrectionClaim?> TryClaimAsync(
        Guid operationId,
        string fingerprint,
        IReadOnlyCollection<GlobalProductCorrectionCheckpoint> checkpoints,
        string leaseOwner,
        long nowUtcTicks,
        long leaseUntilUtcTicks,
        CancellationToken cancellationToken = default);
    // BL-552: a claim the store did not answer within its bound is given back if (and only if) this owner holds it.
    Task ReleaseIfOwnedAsync(Guid operationId, string leaseOwner, long leaseUntilUtcTicks,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
    // BL-552: this tenant's stuck requests (lock held), and those of them only a person can settle (BL-530).
    Task<long> CountManualReconciliationAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
    Task<long> CountNeedingAPersonAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
    Task<bool> AdvanceAsync(
        GlobalProductCorrectionClaim claim,
        GlobalProductCorrectionMutation mutation,
        CancellationToken cancellationToken = default);
    Task<GlobalProductCorrectionRecoverablePage> DiscoverRecoverableAsync(
        long nowUtcTicks,
        int limit,
        Guid? afterOperationId = null,
        CancellationToken cancellationToken = default);
}
