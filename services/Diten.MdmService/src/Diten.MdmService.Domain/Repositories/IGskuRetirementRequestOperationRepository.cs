using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public interface IGskuRetirementRequestOperationRepository
{
    Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverTenantPartitionsAsync(
        Guid? afterTenantId, int limit, CancellationToken cancellationToken = default);
    Task<GskuRetirementRequestWorkflowReserveResult> ReserveAsync(
        GskuRetirementRequestOperation operation, CancellationToken cancellationToken = default);
    Task<GskuRetirementRequestOperation?> GetByOperationIdAsync(
        Guid operationId, CancellationToken cancellationToken = default);
    Task<GskuRetirementRequestWorkflowClaim?> TryClaimAsync(
        Guid operationId, string fingerprint,
        IReadOnlyCollection<GskuRetirementRequestCheckpoint> checkpoints,
        string leaseOwner, long nowUtcTicks, long leaseUntilUtcTicks,
        CancellationToken cancellationToken = default);
    // Shared item 13 (A3), extended to this flow by WP-MDM-GSKU-ACCEPT-01 FIX2 item 9: a claim the store did not answer
    // within its bound is given back if (and only if) this owner holds it with exactly that lease.
    // FIX3 item 1 (B13): this tenant's operations waiting for a person (manual reconciliation), for the cycle report.
    Task<long> CountManualReconciliationAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
    Task ReleaseIfOwnedAsync(Guid operationId, string leaseOwner, long leaseUntilUtcTicks,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task<bool> AdvanceAsync(
        GskuRetirementRequestWorkflowClaim claim, GskuRetirementRequestWorkflowMutation mutation,
        CancellationToken cancellationToken = default);
    Task<GskuRetirementRequestWorkflowRecoverablePage> DiscoverRecoverableAsync(
        long nowUtcTicks, int limit, Guid? afterOperationId = null,
        CancellationToken cancellationToken = default);
}
