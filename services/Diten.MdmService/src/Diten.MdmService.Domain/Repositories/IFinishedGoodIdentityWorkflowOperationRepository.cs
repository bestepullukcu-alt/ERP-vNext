using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface IFinishedGoodIdentityWorkflowOperationRepository
{
    Task<FinishedGoodIdentityWorkflowReserveResult> ReserveAsync(
        FinishedGoodIdentityWorkflowReservation reservation,
        CancellationToken cancellationToken = default);

    Task<FinishedGoodIdentityWorkflowOperation?> GetByOperationIdAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);

    Task<FinishedGoodIdentityWorkflowOperation?> GetByStartIdempotencyKeyAsync(
        string startIdempotencyKey,
        CancellationToken cancellationToken = default);

    Task<FinishedGoodIdentityWorkflowClaim?> TryClaimAsync(
        FinishedGoodIdentityWorkflowClaimRequest request,
        CancellationToken cancellationToken = default);

    Task<FinishedGoodIdentityWorkflowRecoverablePage> DiscoverRecoverableAsync(
        int limit,
        FinishedGoodIdentityWorkflowRecoveryCursor? after = null,
        CancellationToken cancellationToken = default);

    Task<FinishedGoodIdentityWorkflowAdvanceResult> AdvanceAsync(
        FinishedGoodIdentityWorkflowClaim claim,
        FinishedGoodIdentityWorkflowCheckpointMutation mutation,
        CancellationToken cancellationToken = default);
}
