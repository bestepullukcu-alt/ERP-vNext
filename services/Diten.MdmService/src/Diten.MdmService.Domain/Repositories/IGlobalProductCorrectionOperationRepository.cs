using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public interface IGlobalProductCorrectionOperationRepository
{
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
