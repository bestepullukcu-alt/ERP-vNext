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

    Task<bool> AdvanceAsync(
        GlobalProductIdentityWorkflowClaim claim,
        GlobalProductIdentityWorkflowCheckpointMutation mutation,
        CancellationToken cancellationToken = default);
}
