using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface IFirstGskuIdentityRetirementOperationRepository
{
    Task<FirstGskuIdentityRetirementReserveResult> ReserveAsync(
        FirstGskuIdentityRetirementOperation operation, CancellationToken cancellationToken = default);
    Task<FirstGskuIdentityRetirementOperation?> GetByOperationIdAsync(
        Guid operationId, CancellationToken cancellationToken = default);
    Task<FirstGskuIdentityRetirementClaim?> TryClaimAsync(
        FirstGskuIdentityRetirementClaimRequest request, CancellationToken cancellationToken = default);
    // Shared item 13 (A3), extended to this flow by WP-MDM-GSKU-ACCEPT-01 FIX2 item 9: a claim the store did not answer
    // within its bound is given back if (and only if) this owner holds it with exactly that lease.
    // FIX3 item 1 (B13): this tenant's operations waiting for a person (manual reconciliation), for the cycle report.
    Task<long> CountManualReconciliationAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
    Task ReleaseIfOwnedAsync(Guid operationId, string leaseOwner, long leaseUntilUtcTicks,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
    Task<bool> AdvanceAsync(
        FirstGskuIdentityRetirementClaim claim, FirstGskuIdentityRetirementMutation mutation,
        CancellationToken cancellationToken = default);
    Task<FirstGskuIdentityRetirementRecoverablePage> DiscoverRecoverableAsync(
        long nowUtcTicks, int limit, FirstGskuIdentityRetirementRecoveryCursor? after = null,
        CancellationToken cancellationToken = default);
}
