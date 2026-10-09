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
    // Gives back a lease this owner wrote at this exact expiry — for a claim whose answer never arrived.
    // FIX3 (CT decision 1): the note of this GSKU's latest identity operation, when it completed with the submit-time
    // proof because a reference retired after the approval; else null.
    Task<string?> LatestApprovalNoteAsync(Guid gskuId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
    // FIX3 item 1 (B13): this tenant's operations waiting for a person (manual reconciliation), for the cycle report.
    Task<long> CountManualReconciliationAsync(CancellationToken cancellationToken = default) => Task.FromResult(0L);
    Task ReleaseIfOwnedAsync(
        Guid operationId, string leaseOwner, long leaseUntilUtcTicks, CancellationToken cancellationToken = default);
    // The reason of this GSKU's latest identity operation when that one ended "approved, not applicable"; else null.
    Task<string?> LatestNotApplicableReasonAsync(Guid gskuId, CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(null);
    // Where this GSKU's latest identity submit stands (FIX2 item 1: FirstGskuIdentitySubmitState.Of is the rule).
    Task<FirstGskuIdentitySubmitState> GetSubmitStateAsync(Guid gskuId, int currentGskuVersion,
        CancellationToken cancellationToken = default);
    // The engine approved and the approval cannot be applied: revision and GSKU back to draft with their audit
    // intents, and the operation final with the reason — one transaction.
    Task<FirstGskuIdentityDecisionNotApplicableWriteResult> ApplyDecisionNotApplicableAsync(
        FirstGskuIdentityWorkflowClaim claim,
        FirstGskuIdentityWorkflowOperation operation,
        string reasonCode,
        LocalAuditIntent revisionAuditIntent,
        LocalAuditIntent gskuAuditIntent,
        long updatedAtUtcTicks,
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
