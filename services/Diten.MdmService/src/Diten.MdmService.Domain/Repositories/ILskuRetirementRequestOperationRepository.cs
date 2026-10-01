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
}
