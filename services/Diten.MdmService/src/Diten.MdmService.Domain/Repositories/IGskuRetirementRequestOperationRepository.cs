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
    Task<bool> AdvanceAsync(
        GskuRetirementRequestWorkflowClaim claim, GskuRetirementRequestWorkflowMutation mutation,
        CancellationToken cancellationToken = default);
    Task<GskuRetirementRequestWorkflowRecoverablePage> DiscoverRecoverableAsync(
        long nowUtcTicks, int limit, Guid? afterOperationId = null,
        CancellationToken cancellationToken = default);
}
