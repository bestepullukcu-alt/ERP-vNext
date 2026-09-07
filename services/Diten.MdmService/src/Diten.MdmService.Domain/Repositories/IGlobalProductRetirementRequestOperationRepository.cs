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
    Task<bool> AdvanceAsync(GlobalProductRetirementRequestClaim claim,
        GlobalProductRetirementRequestMutation mutation, CancellationToken cancellationToken = default);
    Task<GlobalProductRetirementRequestRecoverablePage> DiscoverRecoverableAsync(long nowUtcTicks, int limit,
        Guid? afterOperationId = null, CancellationToken cancellationToken = default);
}
