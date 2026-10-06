using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface IGlobalProductIdentityWorkflowOperationRepository
{
    Task<GlobalProductIdentityWorkflowReserveResult> ReserveAsync(
        GlobalProductIdentityWorkflowOperation operation,
        CancellationToken cancellationToken = default);

    Task<GlobalProductIdentityWorkflowOperation?> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<GlobalProductIdentityWorkflowOperation?> GetByStartIdempotencyKeyAsync(
        string startIdempotencyKey,
        CancellationToken cancellationToken = default);

    Task<GlobalProductIdentityWorkflowClaim?> TryClaimAsync(
        GlobalProductIdentityWorkflowClaimRequest request,
        CancellationToken cancellationToken = default);

    Task<GlobalProductIdentityWorkflowRecoverablePage> DiscoverRecoverableAsync(
        long nowUtcTicks,
        int limit,
        GlobalProductIdentityWorkflowRecoveryCursor? after = null,
        CancellationToken cancellationToken = default);

    // Gives back a lease this owner wrote at this exact expiry — for a claim whose answer never arrived.
    Task ReleaseIfOwnedAsync(
        Guid operationId,
        string leaseOwner,
        long leaseUntilUtcTicks,
        CancellationToken cancellationToken = default);

    // BL-552: this tenant's stuck submits (manual reconciliation, or past the retry limit), and those of them only a
    // person can settle (BL-530).
    Task<long> CountManualReconciliationAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
    Task<long> CountNeedingAPersonAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);

    Task<bool> AdvanceAsync(
        GlobalProductIdentityWorkflowClaim claim,
        GlobalProductIdentityWorkflowCheckpointMutation mutation,
        CancellationToken cancellationToken = default);
}
