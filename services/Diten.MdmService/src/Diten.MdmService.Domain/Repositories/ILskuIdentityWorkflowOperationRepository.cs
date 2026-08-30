using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface ILskuIdentityWorkflowOperationRepository
{
    Task<LskuIdentityWorkflowReserveResult> ReserveAsync(
        LskuIdentityWorkflowOperation operation,
        CancellationToken cancellationToken = default);

    Task<LskuIdentityWorkflowOperation?> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<LskuIdentityWorkflowOperation?> GetByStartIdempotencyKeyAsync(
        string startIdempotencyKey,
        CancellationToken cancellationToken = default);

    Task<LskuIdentityWorkflowClaim?> TryClaimAsync(
        LskuIdentityWorkflowClaimRequest request,
        CancellationToken cancellationToken = default);

    Task<LskuIdentityWorkflowRecoverablePage> DiscoverRecoverableAsync(
        long nowUtcTicks,
        int limit,
        LskuIdentityWorkflowRecoveryCursor? after = null,
        CancellationToken cancellationToken = default);

    Task<bool> AdvanceAsync(
        LskuIdentityWorkflowClaim claim,
        LskuIdentityWorkflowCheckpointMutation mutation,
        CancellationToken cancellationToken = default);
}
