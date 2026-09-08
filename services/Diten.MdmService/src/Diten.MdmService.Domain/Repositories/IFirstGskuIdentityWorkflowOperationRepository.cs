using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.ValueObjects;

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
    Task<FirstGskuIdentityWithdrawalWriteResult> ApplyWithdrawalAsync(
        FirstGskuIdentityWorkflowClaim claim,
        FirstGskuIdentityWorkflowOperation operation,
        ProductIdentityWorkflowCancellationEvidence cancellationEvidence,
        LocalAuditIntent revisionAuditIntent,
        LocalAuditIntent gskuAuditIntent,
        long updatedAtUtcTicks,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new FirstGskuIdentityWithdrawalWriteResult(
            false, false, null, null, "FIRST_GSKU_IDENTITY_WITHDRAWAL_NOT_IMPLEMENTED"));
}
