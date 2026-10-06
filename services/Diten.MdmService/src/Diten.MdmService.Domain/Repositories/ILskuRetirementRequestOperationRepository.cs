using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public interface ILskuRetirementRequestOperationRepository
{
    Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverTenantPartitionsAsync(Guid? afterTenantId,
        int limit, CancellationToken cancellationToken = default);
    Task<LskuRetirementRequestReserveResult> ReserveAsync(LskuRetirementRequestOperation operation,
        CancellationToken cancellationToken = default);
    Task<LskuRetirementRequestOperation?> GetByOperationIdAsync(Guid operationId,
        CancellationToken cancellationToken = default);
    Task<LskuRetirementRequestClaim?> TryClaimAsync(Guid operationId, string fingerprint,
        IReadOnlyCollection<LskuRetirementRequestCheckpoint> checkpoints, string owner,
        long nowUtcTicks, long untilUtcTicks, CancellationToken cancellationToken = default);
    Task<bool> AdvanceAsync(LskuRetirementRequestClaim claim, LskuRetirementRequestMutation mutation,
        CancellationToken cancellationToken = default);
    Task<LskuRetirementRequestPage> DiscoverRecoverableAsync(long nowUtcTicks, int limit, Guid? afterOperationId = null,
        CancellationToken cancellationToken = default);
    // LSKU FIX1 B2 / S2 (the GSKU pattern): a step that did not finish gives its lease back.
    Task ReleaseIfOwnedAsync(Guid operationId, string leaseOwner, long leaseUntilUtcTicks,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
    // LSKU FIX1 S4: this tenant's stuck requests, and those of them only a person can settle (BL-530).
    Task<long> CountManualReconciliationAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
    Task<long> CountNeedingAPersonAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
}
