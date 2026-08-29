using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface IFirstGskuIdentityWorkflowOperationRepository
{
    Task<FirstGskuIdentityWorkflowReserveResult> ReserveAsync(
        FirstGskuIdentityWorkflowOperation operation, CancellationToken cancellationToken = default);
    Task<FirstGskuIdentityWorkflowOperation?> GetByOperationIdAsync(
        Guid operationId, CancellationToken cancellationToken = default);
    Task<FirstGskuIdentityWorkflowOperation?> GetByStartIdempotencyKeyAsync(
        string startIdempotencyKey, CancellationToken cancellationToken = default);
    Task<FirstGskuIdentityWorkflowClaim?> TryClaimAsync(
        FirstGskuIdentityWorkflowClaimRequest request, CancellationToken cancellationToken = default);
    Task<FirstGskuIdentityWorkflowRecoverablePage> DiscoverRecoverableAsync(
        long nowUtcTicks, int limit, FirstGskuIdentityWorkflowRecoveryCursor? after = null,
        CancellationToken cancellationToken = default);
    Task<bool> AdvanceAsync(
        FirstGskuIdentityWorkflowClaim claim, FirstGskuIdentityWorkflowCheckpointMutation mutation,
        CancellationToken cancellationToken = default);
}
