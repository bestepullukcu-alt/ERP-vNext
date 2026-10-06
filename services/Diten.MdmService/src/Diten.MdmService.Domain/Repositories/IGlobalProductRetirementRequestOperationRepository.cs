using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public interface IGlobalProductRetirementRequestOperationRepository
{
    Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverTenantPartitionsAsync(Guid? afterTenantId,
        int limit, CancellationToken cancellationToken = default);
    Task<GlobalProductRetirementRequestReserveResult> ReserveAsync(
        GlobalProductRetirementRequestOperation operation, CancellationToken cancellationToken = default);
    Task<GlobalProductRetirementRequestOperation?> GetByOperationIdAsync(Guid operationId,
        CancellationToken cancellationToken = default);
    Task<GlobalProductRetirementRequestClaim?> TryClaimAsync(Guid operationId, string fingerprint,
        IReadOnlyCollection<GlobalProductRetirementRequestCheckpoint> checkpoints, string leaseOwner,
        long nowUtcTicks, long leaseUntilUtcTicks, CancellationToken cancellationToken = default);
    // BL-552: a claim the store did not answer within its bound is given back if (and only if) this owner holds it.
    Task ReleaseIfOwnedAsync(Guid operationId, string leaseOwner, long leaseUntilUtcTicks,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
    // BL-552: this tenant's stuck requests (lock held), and those of them only a person can settle (BL-530).
    Task<long> CountManualReconciliationAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
    Task<long> CountNeedingAPersonAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
    Task<bool> AdvanceAsync(GlobalProductRetirementRequestClaim claim,
        GlobalProductRetirementRequestMutation mutation, CancellationToken cancellationToken = default);
    Task<GlobalProductRetirementRequestRecoverablePage> DiscoverRecoverableAsync(long nowUtcTicks, int limit,
        Guid? afterOperationId = null, CancellationToken cancellationToken = default);
}
