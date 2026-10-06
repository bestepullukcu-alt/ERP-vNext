using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.ValueObjects;

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

    Task<LskuIdentityWithdrawalWriteResult> ApplyWithdrawalAsync(
        LskuIdentityWorkflowClaim claim,
        LskuIdentityWorkflowOperation operation,
        ProductIdentityWorkflowCancellationEvidence cancellationEvidence,
        LocalAuditIntent auditIntent,
        long updatedAtUtcTicks,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new LskuIdentityWithdrawalWriteResult(
            false, false, null, "LSKU_IDENTITY_WITHDRAWAL_NOT_IMPLEMENTED"));

    // LSKU FIX2 item 10: the unfinished submits of a draft, for the cancel to settle or refuse one by one.
    Task<IReadOnlyList<LskuIdentityWorkflowOperation>> GetUnfinishedForLskuAsync(Guid lskuId,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("LSKU_IDENTITY_WORKFLOW_UNFINISHED_READ_NOT_IMPLEMENTED");

    // LSKU FIX1 B3: a draft is cancelled only while no submit of it is unfinished (the engine may hold its start).
    Task<bool> HasUnfinishedForLskuAsync(Guid lskuId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("LSKU_IDENTITY_WORKFLOW_UNFINISHED_READ_NOT_IMPLEMENTED");

    // LSKU FIX1 B3: the engine refused the submit before processing it. In one transaction the draft's version moves
    // (with the intent that says why) and the operation ends AbandonedBeforeWorkflowStart, so the maker's resubmit
    // is a new operation on the new version.
    Task<LskuIdentityWithdrawalWriteResult> AbandonBeforeStartAsync(
        LskuIdentityWorkflowClaim claim,
        LskuIdentityWorkflowOperation operation,
        string reasonCode,
        LocalAuditIntent auditIntent,
        long updatedAtUtcTicks,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new LskuIdentityWithdrawalWriteResult(
            false, false, null, "LSKU_IDENTITY_ABANDON_NOT_IMPLEMENTED"));

    // LSKU FIX1 B5: the engine decided and MDM cannot apply it. In one transaction the LSKU goes back to Draft (with
    // the intent) and the operation ends DecisionNotApplicable with the reason.
    Task<LskuIdentityWithdrawalWriteResult> ApplyDecisionNotApplicableAsync(
        LskuIdentityWorkflowClaim claim,
        LskuIdentityWorkflowOperation operation,
        string reasonCode,
        LocalAuditIntent auditIntent,
        long updatedAtUtcTicks,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new LskuIdentityWithdrawalWriteResult(
            false, false, null, "LSKU_IDENTITY_DECISION_NOT_APPLICABLE_NOT_IMPLEMENTED"));
}
