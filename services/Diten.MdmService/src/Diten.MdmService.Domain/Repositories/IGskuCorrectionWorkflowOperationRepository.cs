using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public interface IGskuCorrectionWorkflowOperationRepository
{
    Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverTenantPartitionsAsync(
        Guid? afterTenantId, int limit, CancellationToken cancellationToken = default);
    Task<GskuCorrectionWorkflowReserveResult> ReserveAsync(
        GskuCorrectionWorkflowOperation operation, CancellationToken cancellationToken = default);
    Task<GskuCorrectionWorkflowOperation?> GetByOperationIdAsync(
        Guid operationId, CancellationToken cancellationToken = default);
    Task<GskuCorrectionWorkflowClaim?> TryClaimAsync(
        Guid operationId, string fingerprint,
        IReadOnlyCollection<GskuCorrectionWorkflowCheckpoint> checkpoints,
        string leaseOwner, long nowUtcTicks, long leaseUntilUtcTicks,
        CancellationToken cancellationToken = default);
    Task<bool> AdvanceAsync(
        GskuCorrectionWorkflowClaim claim, GskuCorrectionWorkflowMutation mutation,
        CancellationToken cancellationToken = default);
    Task<GskuCorrectionWorkflowRecoverablePage> DiscoverRecoverableAsync(
        long nowUtcTicks, int limit, Guid? afterOperationId = null,
        CancellationToken cancellationToken = default);
}
