using System.Globalization;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.MdmService.Application.Features.ItemMaster.Activation;

public sealed record ItemActivationWorkflowProcessingResult(
    bool Succeeded,
    ItemWorkflowOperation? Operation,
    string? ErrorCode,
    int StatusCode,
    bool IsReplay);

public sealed record ItemActivationWorkflowExecutionConfiguration(
    TimeSpan LeaseDuration,
    TimeSpan RetryDelay,
    // How long after a start was sent the engine's "nothing under this key" is not trusted (§8.4 A).
    TimeSpan? StartSettleWindow = null)
{
    public TimeSpan SettleWindow => StartSettleWindow is { } window && window > TimeSpan.Zero ? window : TimeSpan.FromMinutes(2);
}

/// <summary>
/// The decision's item write (approved → Active; rejected / not applicable → Draft, or Deactivated for a reactivation).
/// The production writer sends <c>ApplyItemActivationDecisionCommand</c> through the pipeline, so the move is audited on
/// path b (§21); it then calls the store.
/// </summary>
public interface IItemActivationDecisionWriter
{
    Task<ItemWorkflowWriteResult> ApplyDecisionAsync(ItemWorkflowClaim claim, ItemWorkflowOperation operation,
        ProductIdentityWorkflowDecisionEvidence evidence, long updatedAtUtcTicks, CancellationToken cancellationToken);

    Task<ItemWorkflowWriteResult> ApplyNotApplicableAsync(ItemWorkflowClaim claim, ItemWorkflowOperation operation,
        string reasonCode, long updatedAtUtcTicks, CancellationToken cancellationToken);

    // S2 FIX1 item 5: the worker's two other item moves, audited the same way (a user's request records its own move).
    Task<ItemWorkflowWriteResult> ApplyLocalPendingAsync(ItemWorkflowClaim claim, ItemWorkflowOperation operation,
        ProductIdentityWorkflowBinding binding, long updatedAtUtcTicks, CancellationToken cancellationToken);

    Task<ItemWorkflowWriteResult> ApplyWithdrawalAsync(ItemWorkflowClaim claim, ItemWorkflowOperation operation,
        Diten.MdmService.Domain.ValueObjects.ProductIdentityWorkflowCancellationEvidence evidence, long updatedAtUtcTicks, CancellationToken cancellationToken);
}

/// <summary>
/// MOD-0290-FU04 S2 — the item activation / reactivation approval (flow ITEM_ACTIVATION, MOD-0023, the QA Manager
/// position). The accepted GSKU pattern (§8.4), as the LSKU identity flow carries it:
/// <list type="bullet">
/// <item>(A) the item goes back / gives its fence up only when the engine is KNOWN to hold nothing for the request — the
/// start lookup and the start's answer are separate; "nothing" is trusted only after the settle window; the engine's
/// three answers: open → wait, closed without a decision → back, contradicting evidence → stuck + reconciliation. No
/// approval without its item.</item>
/// <item>(B) no state stays stuck unseen without a named exit: the exit is the requester's same command or a person
/// (BL-530); the two gauges count them; the store refuses "manual + retryable"; write classes (passes by itself → wait,
/// concurrent → counted retry, other → stuck).</item>
/// <item>A stuck approval HOLDS the item: it stays PendingActivation (not editable) until its exit.</item>
/// <item>Maker ≠ approver: the maker's own decision is not applied — the item goes back with the reason.</item>
/// <item>A reactivation (Deactivated → PendingActivation) that is rejected, withdrawn or not applicable returns to
/// Deactivated, never to Draft.</item>
/// </list>
/// </summary>
public sealed class ItemActivationWorkflowProcessor(
    IItemWorkflowOperationRepository operations,
    IItemRepository items,
    IProductIdentityWorkflowClient workflowClient,
    ItemActivationWorkflowStartRequestFactory requestFactory,
    IItemActivationDecisionWriter decisionWriter,
    // S2 FIX1 item 1: the worker has no user request — the lists are read with MDM's service identity for the tenant.
    Diten.MdmService.Application.Contracts.ReferenceData.IWorkflowItemReferenceResolver references,
    TimeProvider timeProvider,
    ILogger<ItemActivationWorkflowProcessor>? logger = null,
    ItemActivationWorkflowExecutionConfiguration? execution = null)
{
    public const string Flow = "ITEM_ACTIVATION";

    private readonly ILogger log = logger ?? NullLogger<ItemActivationWorkflowProcessor>.Instance;
    private TimeSpan SettleWindow => execution?.SettleWindow ?? TimeSpan.FromMinutes(2);

    public const string StartRefused = "ITEM_ACTIVATION_WORKFLOW_START_REFUSED";
    public const string ReconciledNothingStarted = "ITEM_ACTIVATION_RECONCILED_NOTHING_STARTED";
    public const string SelfApprovalIgnored = "ITEM_ACTIVATION_SELF_APPROVAL_IGNORED";
    public const string ClosedWithoutDecision = "ITEM_ACTIVATION_CLOSED_WITHOUT_DECISION";
    public const string DecisionNotApplicableCode = "ITEM_ACTIVATION_DECISION_NOT_APPLICABLE";
    public const string ManualReconciliationNeeded = "ITEM_ACTIVATION_WORKFLOW_MANUAL_RECONCILIATION_NEEDED";
    public const string StuckOnRecordAnswer = "ITEM_ACTIVATION_WORKFLOW_STUCK_ON_RECORD_ANSWER";
    public const string SubmitTargetChanged = "ITEM_ACTIVATION_SUBMIT_TARGET_CHANGED";
    public const string WithdrawalUnderWay = "ITEM_ACTIVATION_WITHDRAWAL_UNDER_WAY";
    public const string RetryLimitReached = "ITEM_ACTIVATION_WORKFLOW_RETRY_LIMIT_REACHED";
    public const string MakerReplayRequired = "ITEM_ACTIVATION_WORKFLOW_MAKER_REPLAY_REQUIRED";
    public const string DecisionPendingCode = "ITEM_ACTIVATION_WORKFLOW_DECISION_PENDING";
    public const string IdempotencyConflict = "ITEM_ACTIVATION_WORKFLOW_IDEMPOTENCY_CONFLICT";
    // The engine cancel's reason code for a maker's withdrawal (the free text, if any, travels as the comment).
    public const string WithdrawalReasonCode = "ITEM_ACTIVATION_WITHDRAWN_BY_MAKER";

    // A start sent less than the settle window ago may still be in the engine's hands (never sent: nothing to wait for).
    // A legacy document (no mark) may have sent its start at its last update read BEFORE the claim (the claim itself
    // writes UpdatedAt = now).
    private bool Settling(ItemWorkflowOperation operation, long preClaimUpdatedAtUtcTicks, DateTimeOffset now) =>
        (operation.LastStartSentAtUtcTicksV1 ?? preClaimUpdatedAtUtcTicks) is var sent && sent > 0
        && now.UtcTicks - sent < SettleWindow.Ticks;

    // Only 0 proves "never sent" — written at reservation.
    private static bool NeverSent(ItemWorkflowOperation operation) => operation.LastStartSentAtUtcTicksV1 == 0;

    public static ItemLifecycleStatus SourceStatus(ItemActivationKind kind) =>
        kind == ItemActivationKind.Reactivation ? ItemLifecycleStatus.Deactivated : ItemLifecycleStatus.Draft;

    public async Task<ItemActivationWorkflowProcessingResult> StartInteractiveAsync(
        Guid tenantId, Guid itemId, ItemActivationKind kind, int expectedVersion, Guid operationId, Guid makerSubjectId,
        string delegatedUserToken, TimeSpan leaseDuration, TimeSpan retryDelay, CancellationToken cancellationToken = default,
        string? requestReason = null, List<ItemReferenceEvidenceEntry>? referenceEvidence = null)
    {
        if (tenantId == Guid.Empty || itemId == Guid.Empty || operationId == Guid.Empty || makerSubjectId == Guid.Empty
            || expectedVersion < 0 || kind is not (ItemActivationKind.Activation or ItemActivationKind.Reactivation)
            || string.IsNullOrWhiteSpace(delegatedUserToken))
            return Fail("ITEM_ACTIVATION_WORKFLOW_START_INVALID", 400);

        var existing = await operations.GetByOperationIdAsync(operationId, cancellationToken);
        ItemWorkflowOperation operation;
        var replay = existing is not null;
        if (existing is null)
        {
            var item = await items.GetByIdAsync(itemId, cancellationToken);
            if (item is null) return Fail(ItemErrors.NotFound, 404);
            ItemActivationWorkflowStartPlan plan;
            try
            {
                plan = requestFactory.Create(tenantId, item, kind, operationId, expectedVersion, makerSubjectId, requestReason);
                // S2 FIX1 (minor): the evidence the approval will be held to, frozen once with the reservation.
                plan.Operation.ReferenceEvidence = referenceEvidence;
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message, exception.Message == ItemActivationWorkflowStartRequestFactory.ConfigurationInvalid
                    ? 503 : 409);
            }
            var reserved = await operations.ReserveAsync(plan.Operation, cancellationToken);
            if (!reserved.Succeeded || reserved.Operation is null)
                return Fail(reserved.ErrorCode ?? "ITEM_ACTIVATION_WORKFLOW_OPERATION_CONFLICT", 409);
            operation = reserved.Operation;
            replay = reserved.IsReplay;
        }
        else
        {
            operation = existing;
        }

        if (!ExactInteractiveFacts(operation, tenantId, itemId, kind, expectedVersion, makerSubjectId))
            return Fail("ITEM_ACTIVATION_WORKFLOW_OPERATION_CONFLICT", 409);
        // S2 FIX1 item 6: the same request carries the same reason; another reason under its key is an idempotency
        // conflict (CT decision A).
        if (!string.Equals(operation.RequestReason, requestReason, StringComparison.Ordinal))
            return Fail(IdempotencyConflict, 409);
        // A submit whose withdrawal is under way belongs to the withdrawal path.
        if (InWithdrawal(operation)) return Fail(operation, WithdrawalUnderWay, 409);

        var result = await ProcessAsync(operation, delegatedUserToken, $"interactive-{makerSubjectId:N}", leaseDuration,
            retryDelay, stopWhenAwaitingDecision: true, cancellationToken);
        return result with { IsReplay = replay || result.IsReplay };
    }

    public Task<ItemActivationWorkflowProcessingResult> RecoverAsync(ItemWorkflowOperation operation, string leaseOwner,
        TimeSpan leaseDuration, TimeSpan retryDelay, CancellationToken cancellationToken = default) =>
        InWithdrawal(operation)
            ? ProcessWithdrawalAsync(operation, null, leaseOwner, leaseDuration, retryDelay, cancellationToken)
            : ProcessAsync(operation, null, leaseOwner, leaseDuration, retryDelay, stopWhenAwaitingDecision: false,
                cancellationToken);

    public async Task<ItemActivationWorkflowProcessingResult> WithdrawInteractiveAsync(
        Guid tenantId, Guid itemId, int expectedVersion, Guid commandId, Guid requesterSubjectId, string? comment,
        string delegatedUserToken, TimeSpan leaseDuration, TimeSpan retryDelay, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || itemId == Guid.Empty || commandId == Guid.Empty || requesterSubjectId == Guid.Empty
            || expectedVersion < 1 || !OptionalExactText(comment, 2000) || string.IsNullOrWhiteSpace(delegatedUserToken))
            return Fail("ITEM_ACTIVATION_WITHDRAWAL_INVALID", 400);
        var item = await items.GetByIdAsync(itemId, cancellationToken);
        if (item?.ActivationWorkflowBinding is not { } binding)
            return Fail(item is null ? ItemErrors.NotFound : "ITEM_ACTIVATION_STATE_CONFLICT", item is null ? 404 : 409);
        if (binding.SubmitterSubjectId != requesterSubjectId)
            return Fail("ITEM_ACTIVATION_WITHDRAWAL_NOT_REQUESTER", 403);
        var operation = await operations.GetByStartIdempotencyKeyAsync(binding.StartIdempotencyKey, cancellationToken);
        if (operation is null || operation.TenantId != tenantId || operation.ItemId != itemId
            || operation.MakerSubjectId != requesterSubjectId || operation.WorkflowInstanceId != binding.WorkflowInstanceId
            || operation.ApprovalTaskId != binding.ApprovalTaskId)
            return Fail("ITEM_ACTIVATION_WORKFLOW_BINDING_CONFLICT", 409);
        var fingerprint = WithdrawalFingerprint(tenantId, itemId, expectedVersion, commandId, requesterSubjectId,
            WithdrawalReasonCode, comment, binding);
        if (operation.WithdrawalCommandId.HasValue && !ExactWithdrawalFacts(operation, commandId, fingerprint,
                requesterSubjectId, expectedVersion, WithdrawalReasonCode, comment))
            return Fail("ITEM_ACTIVATION_WITHDRAWAL_OPERATION_CONFLICT", 409);
        if (!operation.WithdrawalCommandId.HasValue && (item.Version != expectedVersion
            || item.LifecycleStatus != ItemLifecycleStatus.PendingActivation
            || operation.Checkpoint != ItemWorkflowCheckpoint.AwaitingDecision))
            return Fail("ITEM_ACTIVATION_STATE_CONFLICT", 409);
        var result = await ProcessWithdrawalAsync(operation, delegatedUserToken, $"withdraw-{requesterSubjectId:N}",
            leaseDuration, retryDelay, cancellationToken, commandId, fingerprint, requesterSubjectId, expectedVersion,
            WithdrawalReasonCode, comment);
        return result with { IsReplay = operation.WithdrawalCommandId.HasValue || result.IsReplay };
    }

    private async Task<ItemActivationWorkflowProcessingResult> ProcessWithdrawalAsync(
        ItemWorkflowOperation initial, string? token, string leaseOwner, TimeSpan leaseDuration, TimeSpan retryDelay,
        CancellationToken cancellationToken, Guid? commandId = null, string? fingerprint = null, Guid? requester = null,
        int? expectedVersion = null, string? reasonCode = null, string? comment = null)
    {
        var operation = initial;
        for (var step = 0; step < 10; step++)
        {
            if (operation.Checkpoint == ItemWorkflowCheckpoint.Completed)
                return operation.WithdrawalCommandId.HasValue ? Success(operation, true)
                    : Fail(operation, "ITEM_ACTIVATION_WORKFLOW_STATE_CONFLICT", 409);
            // A withdrawal parked for its maker is the worker's end — only the requester's same withdrawal sends the cancel.
            if (string.IsNullOrWhiteSpace(token) && operation.Checkpoint == ItemWorkflowCheckpoint.AwaitingMakerReplay)
                return Fail(operation, MakerReplayRequired, 409);
            var now = timeProvider.GetUtcNow();
            var claim = await operations.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
                [operation.Checkpoint], leaseOwner, now.UtcTicks, now.Add(leaseDuration).UtcTicks), cancellationToken);
            if (claim is null) return Fail(operation, "ITEM_ACTIVATION_WORKFLOW_BUSY", 409);
            // Every decision of this step is made on the document as it is UNDER the claim.
            if (await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken) is not { } underClaim
                || underClaim.Checkpoint != claim.Checkpoint)
                return Fail(operation, "ITEM_ACTIVATION_WORKFLOW_BUSY", 409);
            operation = underClaim;
            bool advanced;
            switch (operation.Checkpoint)
            {
                case ItemWorkflowCheckpoint.AwaitingDecision:
                    if (!commandId.HasValue || fingerprint is null || !requester.HasValue || !expectedVersion.HasValue
                        || reasonCode is null)
                        return Fail(operation, "ITEM_ACTIVATION_WORKFLOW_DECISION_PENDING", 409);
                    advanced = await operations.AdvanceAsync(claim, new(ItemWorkflowCheckpoint.WithdrawalRequested,
                        ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks, WithdrawalCommandId: commandId,
                        WithdrawalFingerprint: fingerprint, WithdrawalRequesterSubjectId: requester,
                        WithdrawalExpectedItemVersion: expectedVersion, WithdrawalReasonCode: reasonCode,
                        WithdrawalComment: comment, ReleaseLease: true), cancellationToken);
                    break;
                case ItemWorkflowCheckpoint.WithdrawalRequested:
                {
                    var preflight = await workflowClient.GetCancellationPreflightAsync(operation.TenantId,
                        new(operation.WorkflowInstanceId!.Value, operation.ApprovalTaskId!.Value, operation.ObjectType,
                            operation.ObjectId, operation.MakerSubjectId), cancellationToken);
                    if (preflight.Outcome != ProductIdentityWorkflowTransportOutcome.Success || preflight.Value is not { } p)
                    {
                        // The engine refused the withdrawal (the approver has already decided): nothing changed there —
                        // the operation awaits the decision again and the decision is applied by the ordinary observation.
                        advanced = Retryable(preflight.Outcome)
                            ? await ScheduleRetryAsync(operation, claim, operation.Checkpoint, preflight.ErrorCode, now,
                                retryDelay, string.IsNullOrWhiteSpace(token), cancellationToken)
                            : await ClosedBeforeWithdrawalAsync(operation, claim, preflight.ErrorCode, now, cancellationToken);
                        break;
                    }
                    advanced = await operations.AdvanceAsync(claim, new(ItemWorkflowCheckpoint.WithdrawalPreflightObserved,
                        ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
                        WithdrawalExpectedWorkflowInstanceVersion: p.WorkflowInstanceVersion,
                        WithdrawalExpectedApprovalTaskVersion: p.ApprovalTaskVersion, WithdrawalObjectRef: p.ObjectRef,
                        ReleaseLease: true), cancellationToken);
                    break;
                }
                case ItemWorkflowCheckpoint.WithdrawalPreflightObserved:
                case ItemWorkflowCheckpoint.WithdrawalOutcomeUnknown:
                case ItemWorkflowCheckpoint.AwaitingMakerReplay:
                {
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        // Parked for its maker (only the requester's same withdrawal can send the cancel); the time the
                        // cancel was first left unanswered is kept for the gauge.
                        if (!await operations.AdvanceAsync(claim, new(ItemWorkflowCheckpoint.AwaitingMakerReplay,
                                ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, now.UtcTicks,
                                LastFailureCode: MakerReplayRequired, ReleaseLease: true,
                                WithdrawalUnansweredSinceUtcTicksV1: operation.WithdrawalUnansweredSinceUtcTicksV1 ?? now.UtcTicks),
                                cancellationToken))
                            return Fail(operation, "ITEM_ACTIVATION_WORKFLOW_CONCURRENCY_CONFLICT", 409);
                        var parked = await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken) ?? operation;
                        return Fail(parked, MakerReplayRequired, 409);
                    }
                    var cancelled = await workflowClient.CancelAsync(operation.TenantId, new(
                        operation.WorkflowInstanceId!.Value, operation.ApprovalTaskId!.Value, operation.ObjectType,
                        operation.ObjectId, operation.MakerSubjectId, operation.WithdrawalExpectedWorkflowInstanceVersion!.Value,
                        operation.WithdrawalExpectedApprovalTaskVersion!.Value, operation.WithdrawalReasonCode!,
                        operation.WithdrawalComment, operation.WithdrawalCommandId!.Value.ToString("D")), token, cancellationToken);
                    if (cancelled.Outcome != ProductIdentityWorkflowTransportOutcome.Success || cancelled.Value is not { } e)
                    {
                        // An unanswered cancel may have happened: asked again. A refused one did not.
                        advanced = Retryable(cancelled.Outcome) || cancelled.Outcome == ProductIdentityWorkflowTransportOutcome.Incomplete
                            ? await ScheduleRetryAsync(operation, claim, ItemWorkflowCheckpoint.WithdrawalOutcomeUnknown,
                                cancelled.ErrorCode, now, retryDelay, string.IsNullOrWhiteSpace(token), cancellationToken,
                                withdrawalUnansweredSince: operation.WithdrawalUnansweredSinceUtcTicksV1 ?? now.UtcTicks)
                            : await ClosedBeforeWithdrawalAsync(operation, claim, cancelled.ErrorCode, now, cancellationToken);
                        break;
                    }
                    advanced = await operations.AdvanceAsync(claim, new(ItemWorkflowCheckpoint.WithdrawalObserved,
                        ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
                        WithdrawalTransitionLogId: e.TransitionLogId, WithdrawalObservedAtUtcTicksV1: e.DecisionAt.UtcTicks,
                        WithdrawalTransitionSequence: e.TransitionSequence,
                        WithdrawalResultWorkflowInstanceVersion: e.WorkflowInstanceVersion,
                        WithdrawalResultApprovalTaskVersion: e.ApprovalTaskVersion, WithdrawalTaskStatus: e.TaskStatus,
                        WithdrawalInstanceStatus: e.InstanceStatus, WithdrawalObjectRef: e.ObjectRef, ReleaseLease: true),
                        cancellationToken);
                    break;
                }
                case ItemWorkflowCheckpoint.WithdrawalObserved:
                {
                    if (!TryCancellationEvidence(operation, out var evidence))
                    {
                        advanced = await QuarantineAsync(claim, "ITEM_ACTIVATION_WITHDRAWAL_EVIDENCE_INVALID", now, cancellationToken);
                        break;
                    }
                    // S2 FIX1 item 5: finished by the worker → its own audited command; in the requester's withdrawal
                    // request the request's own record carries the move.
                    var written = string.IsNullOrWhiteSpace(token)
                        ? await decisionWriter.ApplyWithdrawalAsync(claim, operation, evidence, now.UtcTicks, cancellationToken)
                        : await operations.ApplyWithdrawalAsync(claim, operation, evidence, now.UtcTicks, cancellationToken);
                    advanced = written.Succeeded || await WriteRefusedAsync(operation, claim, written.ErrorCode, now,
                        retryDelay, string.IsNullOrWhiteSpace(token), cancellationToken);
                    break;
                }
                case ItemWorkflowCheckpoint.WithdrawalApplied:
                    advanced = await AdvanceAsync(claim, ItemWorkflowCheckpoint.Completed,
                        ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true, cancellationToken: cancellationToken);
                    break;
                default:
                    return Fail(operation, "ITEM_ACTIVATION_WORKFLOW_STATE_CONFLICT", 409);
            }
            if (!advanced) return Fail(operation, "ITEM_ACTIVATION_WORKFLOW_CONCURRENCY_CONFLICT", 409);
            operation = await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken)
                ?? throw new InvalidOperationException("ITEM_ACTIVATION_WORKFLOW_OPERATION_LOST");
            // The engine refused the withdrawal — it ended here (the approval stays); the requester learns why.
            if (operation.Checkpoint == ItemWorkflowCheckpoint.AwaitingDecision && !operation.WithdrawalCommandId.HasValue)
                return Fail(operation, operation.LastFailureCode ?? "ITEM_ACTIVATION_WITHDRAWAL_REFUSED", 409);
            if (operation.NextAttemptAtUtcTicksV1 > timeProvider.GetUtcNow().UtcTicks)
                return Fail(operation, operation.LastFailureCode ?? "ITEM_ACTIVATION_WORKFLOW_RETRY_SCHEDULED", 503);
        }
        return Fail(operation, "ITEM_ACTIVATION_WORKFLOW_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<ItemActivationWorkflowProcessingResult> ProcessAsync(
        ItemWorkflowOperation initial, string? delegatedUserToken, string leaseOwner, TimeSpan leaseDuration,
        TimeSpan retryDelay, bool stopWhenAwaitingDecision, CancellationToken cancellationToken)
    {
        var operation = initial;
        var manualResumeAttempted = false;
        // Only a REPEAT reconciles: an operation that became stuck in this very request is answered, not re-read.
        var startedStuck = initial.Checkpoint == ItemWorkflowCheckpoint.ManualReconciliationRequired;
        for (var step = 0; step < 12; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation.Checkpoint == ItemWorkflowCheckpoint.Completed) return Success(operation, true);
            // Ended with the item back where the request found it: final here; the handler says why.
            if (operation.Checkpoint is ItemWorkflowCheckpoint.AbandonedBeforeWorkflowStart
                or ItemWorkflowCheckpoint.Superseded or ItemWorkflowCheckpoint.DecisionNotApplicable)
                return Success(operation, true);
            if (stopWhenAwaitingDecision && operation.Checkpoint == ItemWorkflowCheckpoint.AwaitingDecision)
                return Success(operation, false);
            if (operation.Checkpoint == ItemWorkflowCheckpoint.AwaitingMakerReplay && string.IsNullOrWhiteSpace(delegatedUserToken))
                return Fail(operation, MakerReplayRequired, 409);
            if (operation.Checkpoint == ItemWorkflowCheckpoint.ManualReconciliationRequired)
            {
                // A STUCK operation's way out is its requester's same command (it reads the engine again); the worker
                // never touches it, and a second pass in the same request is a person's (BL-530).
                if (manualResumeAttempted || !startedStuck || string.IsNullOrWhiteSpace(delegatedUserToken)
                    || operation.RecoveryDisposition != ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
                    return Fail(operation, manualResumeAttempted ? ManualReconciliationNeeded
                        : operation.LastFailureCode ?? "ITEM_ACTIVATION_WORKFLOW_RECONCILIATION_REQUIRED", 409);
            }
            var counted = string.IsNullOrWhiteSpace(delegatedUserToken);
            var now = timeProvider.GetUtcNow();
            var preClaimUpdatedAtUtcTicks = operation.UpdatedAtUtcTicksV1;
            var claim = await operations.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
                [operation.Checkpoint], leaseOwner, now.UtcTicks, now.Add(leaseDuration).UtcTicks), cancellationToken);
            if (claim is null) return Fail(operation, "ITEM_ACTIVATION_WORKFLOW_BUSY", 409);
            if (await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken) is not { } underClaim
                || underClaim.Checkpoint != claim.Checkpoint)
                return Fail(operation, "ITEM_ACTIVATION_WORKFLOW_BUSY", 409);
            operation = underClaim;

            var advanced = operation.Checkpoint switch
            {
                ItemWorkflowCheckpoint.Prepared =>
                    await AdvanceAsync(claim, ItemWorkflowCheckpoint.StartOutcomeUnknown,
                        ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true, cancellationToken: cancellationToken),
                ItemWorkflowCheckpoint.StartOutcomeUnknown or ItemWorkflowCheckpoint.AwaitingMakerReplay =>
                    await ResolveStartAsync(operation, claim, delegatedUserToken, now, retryDelay, counted, cancellationToken),
                ItemWorkflowCheckpoint.WorkflowStarted =>
                    await ApplyLocalPendingAsync(operation, claim, now, retryDelay, counted, cancellationToken),
                ItemWorkflowCheckpoint.LocalPendingApplied =>
                    await AdvanceAsync(claim, ItemWorkflowCheckpoint.AwaitingDecision,
                        ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true, cancellationToken: cancellationToken),
                ItemWorkflowCheckpoint.AwaitingDecision =>
                    await ObserveDecisionAsync(operation, claim, now, retryDelay, counted, cancellationToken),
                ItemWorkflowCheckpoint.DecisionObserved =>
                    await ValidateApprovalOrApplyRejectAsync(operation, claim, now, retryDelay, counted, cancellationToken),
                ItemWorkflowCheckpoint.ApprovalValidated =>
                    await ApplyDecisionAsync(operation, claim, now, retryDelay, counted, cancellationToken),
                ItemWorkflowCheckpoint.DecisionApplied =>
                    await VerifyDecisionAppliedAndCompleteAsync(operation, claim, now, cancellationToken),
                ItemWorkflowCheckpoint.ManualReconciliationRequired =>
                    await ReconcileAsync(operation, claim, preClaimUpdatedAtUtcTicks, now, counted, cancellationToken),
                _ => false
            };
            if (!advanced) return Fail(operation, "ITEM_ACTIVATION_WORKFLOW_CONCURRENCY_CONFLICT", 409);
            if (operation.Checkpoint == ItemWorkflowCheckpoint.ManualReconciliationRequired) manualResumeAttempted = true;

            operation = await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken)
                ?? throw new InvalidOperationException("ITEM_ACTIVATION_WORKFLOW_OPERATION_LOST");
            // A start the engine sent back to the maker who is present is answered now — not repeated in this request.
            if (operation.Checkpoint == ItemWorkflowCheckpoint.AwaitingMakerReplay && !string.IsNullOrWhiteSpace(delegatedUserToken))
            {
                var code = ProductIdentityWorkflowMakerCodes.IsMakerCode(operation.LastFailureCode)
                    ? operation.LastFailureCode!
                    : ProductIdentityWorkflowMakerCodes.MakerReplayRequired;
                return Fail(operation, code, ProductIdentityWorkflowMakerCodes.StatusFor(code));
            }
            if (operation.NextAttemptAtUtcTicksV1 > timeProvider.GetUtcNow().UtcTicks)
                return Fail(operation, operation.LastFailureCode ?? "ITEM_ACTIVATION_WORKFLOW_RETRY_SCHEDULED", 503);
        }
        return Fail(operation, "ITEM_ACTIVATION_WORKFLOW_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<bool> ResolveStartAsync(ItemWorkflowOperation operation, ItemWorkflowClaim claim,
        string? delegatedUserToken, DateTimeOffset now, TimeSpan retryDelay, bool counted, CancellationToken cancellationToken)
    {
        var startMayBeSent = !string.IsNullOrWhiteSpace(delegatedUserToken);
        if (startMayBeSent)
        {
            // A start goes only to the item it was reserved for, and only while the item carries this submit's fence —
            // taken atomically, so no edit slips between the check and the send. Without the fence: if nothing was ever
            // sent, the engine holds nothing — the submit ends; if a start was sent, it is not sent again — ask.
            var fenced = await items.FenceActivationSubmitAsync(operation.ItemId, operation.ExpectedItemVersion,
                SourceStatus(operation.ActivationKind), operation.OperationId, cancellationToken);
            if (!fenced)
            {
                if (NeverSent(operation))
                    return await AdvanceAsync(claim, ItemWorkflowCheckpoint.AbandonedBeforeWorkflowStart,
                        ProductIdentityWorkflowRecoveryDisposition.None, now, SubmitTargetChanged, releaseLease: true,
                        cancellationToken: cancellationToken);
                startMayBeSent = false;
            }
            // Written BEFORE the start leaves: a request cut off after sending can never pass for "never sent".
            else if (!await operations.AdvanceAsync(claim, new(operation.Checkpoint, operation.RecoveryDisposition,
                         now.UtcTicks, LastFailureCode: operation.LastFailureCode, ReleaseLease: false,
                         Streak: RecoveryStreakWrite.Keep, LastStartSentAtUtcTicksV1: now.UtcTicks), cancellationToken))
            {
                return false;
            }
            else
            {
                operation.LastStartSentAtUtcTicksV1 = now.UtcTicks;
            }
        }
        if (startMayBeSent)
        {
            var outcome = await workflowClient.StartAsync(operation.TenantId, requestFactory.Rehydrate(operation),
                delegatedUserToken!, cancellationToken);
            if (outcome.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
                return await PersistStartedAsync(operation, claim, outcome.Value, now, cancellationToken);
            if (outcome.Outcome != ProductIdentityWorkflowTransportOutcome.Incomplete)
            {
                var fault = ProductIdentityWorkflowFaultPolicy.Classify(ProductIdentityWorkflowCall.Start, outcome.Outcome,
                    outcome.ErrorCode);
                switch (fault.Kind)
                {
                    case ProductIdentityWorkflowFaultKind.Quarantine
                        when IdentityApprovalBinding.RefusedBeforeProcessing(outcome.Outcome, outcome.ErrorCode):
                        // Refused before anything was processed: nothing exists at the engine. The submit ends, the item
                        // stays where it was (its version moves), and the maker can submit again.
                        return await AbandonAsync(operation, claim, BoundedCode(outcome.ErrorCode, StartRefused), now,
                            counted, cancellationToken);
                    case ProductIdentityWorkflowFaultKind.Quarantine:
                        return await QuarantineAsync(claim, outcome.ErrorCode, now, cancellationToken);
                    case ProductIdentityWorkflowFaultKind.MakerReplay:
                        return await AdvanceAsync(claim, ItemWorkflowCheckpoint.AwaitingMakerReplay,
                            ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, now,
                            BoundedCode(fault.MakerCode, MakerReplayRequired), releaseLease: true,
                            cancellationToken: cancellationToken);
                    case ProductIdentityWorkflowFaultKind.ServiceWait when fault.StartedNothing:
                        return await AdvanceAsync(claim, ItemWorkflowCheckpoint.AwaitingMakerReplay,
                            ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, now,
                            ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, releaseLease: true,
                            cancellationToken: cancellationToken);
                }
                // No answer, or one nobody could accept: the start may have arrived — ask.
            }
        }

        var lookup = await workflowClient.GetStartResultAsync(operation.TenantId,
            new(operation.ObjectType, operation.ObjectId, operation.MakerSubjectId, operation.StartIdempotencyKey),
            cancellationToken);
        if (lookup.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
            return await PersistStartedAsync(operation, claim, lookup.Value, now, cancellationToken);
        if (lookup.Outcome is ProductIdentityWorkflowTransportOutcome.NotFound or ProductIdentityWorkflowTransportOutcome.Incomplete)
            return await AdvanceAsync(claim, ItemWorkflowCheckpoint.AwaitingMakerReplay,
                ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, now,
                lookup.ErrorCode ?? MakerReplayRequired, releaseLease: true, cancellationToken: cancellationToken);
        if (Retryable(lookup.Outcome) || ProductIdentityWorkflowFaultPolicy.Classify(ProductIdentityWorkflowCall.StartResult,
                lookup.Outcome, lookup.ErrorCode).Kind == ProductIdentityWorkflowFaultKind.ServiceWait)
            return await ScheduleRetryAsync(operation, claim, ItemWorkflowCheckpoint.StartOutcomeUnknown, lookup.ErrorCode,
                now, retryDelay, string.IsNullOrWhiteSpace(delegatedUserToken), cancellationToken);
        // The LOOKUP's refusal says nothing about what the start did: stuck, the requester's repeat reads again.
        return await QuarantineAsync(claim, lookup.ErrorCode, now, cancellationToken);
    }

    private static bool InWithdrawal(ItemWorkflowOperation operation) =>
        operation.WithdrawalCommandId.HasValue && operation.Checkpoint is
            ItemWorkflowCheckpoint.WithdrawalRequested
            or ItemWorkflowCheckpoint.WithdrawalPreflightObserved
            or ItemWorkflowCheckpoint.WithdrawalOutcomeUnknown
            or ItemWorkflowCheckpoint.WithdrawalObserved
            or ItemWorkflowCheckpoint.WithdrawalApplied
            or ItemWorkflowCheckpoint.AwaitingMakerReplay;

    // The submit ends before anything started; the item's version moves (and its fence goes) in the same transaction so
    // the maker's resubmit is a new operation.
    private async Task<bool> AbandonAsync(ItemWorkflowOperation operation, ItemWorkflowClaim claim, string code,
        DateTimeOffset now, bool counted, CancellationToken cancellationToken)
    {
        var written = await operations.AbandonBeforeStartAsync(claim, operation, code, now.UtcTicks, cancellationToken);
        if (written.Succeeded)
        {
            log.LogWarning("Item activation operation {OperationId} of tenant {TenantId} ended before the approval engine started anything ({ResultCode}); the item can be submitted again.",
                operation.OperationId, operation.TenantId, code);
            return true;
        }
        return await WriteRefusedAsync(operation, claim, written.ErrorCode, now, TimeSpan.FromSeconds(30), counted,
            cancellationToken);
    }

    // The engine decided and MDM does not apply it — the item goes back (Draft / Deactivated) with the reason.
    private async Task<bool> DecisionNotApplicableAsync(ItemWorkflowOperation operation, ItemWorkflowClaim claim,
        string code, DateTimeOffset now, TimeSpan retryDelay, bool counted, CancellationToken cancellationToken)
    {
        var reason = BoundedCode(code, DecisionNotApplicableCode);
        var written = await decisionWriter.ApplyNotApplicableAsync(claim, operation, reason, now.UtcTicks, cancellationToken);
        if (written.Succeeded)
        {
            log.LogWarning("Item activation operation {OperationId} of tenant {TenantId}: the engine's decision is not applied ({ResultCode}); the item is back where the request found it.",
                operation.OperationId, operation.TenantId, reason);
            return true;
        }
        return await WriteRefusedAsync(operation, claim, written.ErrorCode, now, retryDelay, counted, cancellationToken);
    }

    /*
     * The requester's same command on a STUCK operation reads where things stand and puts the operation back; the
     * ordinary steps take it from there:
     *   no start on record                               → the engine's start lookup: found → started,
     *                                                      nothing under the key (after the settle window) → abandoned;
     *   the item still pending on this approval          → awaiting the decision (the recorded one is read again);
     *   the item still at its submit version, unbound    → started (the pending write is made again);
     *   the item already shows this operation's decision → decision applied (verified, completed);
     *   anything else                                    → stays, 409 "a person" (BL-530).
     */
    private async Task<bool> ReconcileAsync(ItemWorkflowOperation operation, ItemWorkflowClaim claim,
        long preClaimUpdatedAtUtcTicks, DateTimeOffset now, bool counted, CancellationToken cancellationToken)
    {
        if (!operation.WorkflowInstanceId.HasValue)
        {
            var lookup = await workflowClient.GetStartResultAsync(operation.TenantId,
                new(operation.ObjectType, operation.ObjectId, operation.MakerSubjectId, operation.StartIdempotencyKey),
                cancellationToken);
            if (lookup.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
                return await PersistStartedAsync(operation, claim, lookup.Value, now, cancellationToken);
            if (lookup.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound
                && !Settling(operation, preClaimUpdatedAtUtcTicks, now))
                return await AbandonAsync(operation, claim, ReconciledNothingStarted, now, counted, cancellationToken);
            return await StayStuckAsync(operation, claim, now, cancellationToken);
        }
        var item = await items.GetByIdAsync(operation.ItemId, cancellationToken);
        var binding = item?.ActivationWorkflowBinding;
        var ours = binding is not null && binding.WorkflowInstanceId == operation.WorkflowInstanceId
                   && binding.StartIdempotencyKey == operation.StartIdempotencyKey;
        if (item is not null && ours && item.LifecycleStatus == ItemLifecycleStatus.PendingActivation
            && binding!.TerminalDecision is null && binding.CancellationEvidence is null)
            return await operations.AdvanceAsync(claim, new(ItemWorkflowCheckpoint.AwaitingDecision,
                ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks, ReleaseLease: true, ClearDecision: true),
                cancellationToken);
        if (item is not null && !ours && item.LifecycleStatus == SourceStatus(operation.ActivationKind)
            && item.Version == operation.ExpectedItemVersion && CompleteStartProof(operation))
            return await operations.AdvanceAsync(claim, new(ItemWorkflowCheckpoint.WorkflowStarted,
                ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
                WorkflowInstanceId: operation.WorkflowInstanceId, WorkflowTemplateId: operation.WorkflowTemplateId,
                WorkflowTemplateVersionId: operation.WorkflowTemplateVersionId, ApprovalTaskId: operation.ApprovalTaskId,
                AssignmentSnapshotId: operation.AssignmentSnapshotId, StartTransitionLogId: operation.StartTransitionLogId,
                WorkflowStartedAtUtcTicksV1: operation.WorkflowStartedAtUtcTicksV1, ReleaseLease: true, ClearDecision: true),
                cancellationToken);
        if (item is not null && ours && binding!.TerminalDecision is { } decided
            && decided.WorkflowInstanceId == operation.WorkflowInstanceId
            && TryRehydrateEvidence(operation, out var recorded) && SameRecordedDecision(decided, recorded))
            return await AdvanceAsync(claim, ItemWorkflowCheckpoint.DecisionApplied,
                ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true, cancellationToken: cancellationToken);
        return await StayStuckAsync(operation, claim, now, cancellationToken);
    }

    private static bool SameRecordedDecision(ProductIdentityWorkflowDecisionEvidence stored,
        ProductIdentityWorkflowDecisionEvidence recorded) =>
        stored.Decision == recorded.Decision && stored.ApprovalTaskId == recorded.ApprovalTaskId
        && stored.TransitionSequence == recorded.TransitionSequence
        && stored.DecisionActorSubjectId == recorded.DecisionActorSubjectId;

    private Task<bool> StayStuckAsync(ItemWorkflowOperation operation, ItemWorkflowClaim claim, DateTimeOffset now,
        CancellationToken cancellationToken) =>
        operations.AdvanceAsync(claim, new(operation.Checkpoint, operation.RecoveryDisposition, now.UtcTicks,
            LastFailureCode: operation.LastFailureCode, ReleaseLease: true, Streak: RecoveryStreakWrite.Keep), cancellationToken);

    private Task<bool> PersistStartedAsync(ItemWorkflowOperation operation, ItemWorkflowClaim claim,
        ProductIdentityWorkflowStartResult? result, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (result is null || result.WorkflowInstanceId == Guid.Empty || result.TemplateId == Guid.Empty
            || result.TemplateVersionId == Guid.Empty || result.ApprovalTaskId == Guid.Empty
            || result.AssignmentSnapshotId == Guid.Empty || result.StartTransitionLogId == Guid.Empty
            || result.StartedAt is not { } startedAt || startedAt == default || startedAt.Offset != TimeSpan.Zero
            || !string.Equals(result.ObjectRef, operation.ObjectRef, StringComparison.Ordinal)
            || operation.WorkflowTemplateId.HasValue && result.TemplateId != operation.WorkflowTemplateId.Value)
            return QuarantineAsync(claim, "ITEM_ACTIVATION_WORKFLOW_START_PROOF_INVALID", now, cancellationToken);
        return operations.AdvanceAsync(claim, new(ItemWorkflowCheckpoint.WorkflowStarted,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks, WorkflowInstanceId: result.WorkflowInstanceId,
            WorkflowTemplateId: result.TemplateId, WorkflowTemplateVersionId: result.TemplateVersionId,
            ApprovalTaskId: result.ApprovalTaskId, AssignmentSnapshotId: result.AssignmentSnapshotId,
            StartTransitionLogId: result.StartTransitionLogId, WorkflowStartedAtUtcTicksV1: startedAt.UtcTicks,
            ReleaseLease: true), cancellationToken);
    }

    public static ProductIdentityWorkflowBinding BindingFor(ItemWorkflowOperation operation) => new()
    {
        WorkflowInstanceId = operation.WorkflowInstanceId!.Value,
        WorkflowTemplateId = operation.WorkflowTemplateId!.Value,
        WorkflowTemplateVersionId = operation.WorkflowTemplateVersionId!.Value,
        ApprovalTaskId = operation.ApprovalTaskId!.Value,
        AssignmentSnapshotId = operation.AssignmentSnapshotId!.Value,
        StartTransitionLogId = operation.StartTransitionLogId!.Value,
        ObjectType = operation.ObjectType,
        ObjectId = operation.ItemId,
        ObjectRef = operation.ObjectRef,
        SubmitterSubjectId = operation.MakerSubjectId,
        StartIdempotencyKey = operation.StartIdempotencyKey,
        StartRequestFingerprint = operation.OperationFingerprint,
        SubmittedAtUtc = new DateTimeOffset(operation.WorkflowStartedAtUtcTicksV1!.Value, TimeSpan.Zero),
        DueAtUtc = operation.DueAtUtcTicksV1.HasValue ? new DateTimeOffset(operation.DueAtUtcTicksV1.Value, TimeSpan.Zero) : null
    };

    private async Task<bool> ApplyLocalPendingAsync(ItemWorkflowOperation operation, ItemWorkflowClaim claim,
        DateTimeOffset now, TimeSpan retryDelay, bool counted, CancellationToken cancellationToken)
    {
        if (!CompleteStartProof(operation))
            return await QuarantineAsync(claim, "ITEM_ACTIVATION_WORKFLOW_START_PROOF_INVALID", now, cancellationToken);
        var binding = BindingFor(operation);
        // S2 FIX1 item 5: made by the worker → its own audited command; in the maker's submit the submit's record carries it.
        var result = counted
            ? await decisionWriter.ApplyLocalPendingAsync(claim, operation, binding, now.UtcTicks, cancellationToken)
            : await operations.ApplyLocalPendingAsync(claim, operation, binding, now.UtcTicks, cancellationToken);
        if (!result.Succeeded)
            return await WriteRefusedAsync(operation, claim, result.ErrorCode, now, retryDelay, counted, cancellationToken);
        if (result.Item is null || result.Item.Version != operation.ExpectedItemVersion + 1
            || result.Item.LifecycleStatus != ItemLifecycleStatus.PendingActivation
            || result.Item.ActivationKind != operation.ActivationKind
            || !ExactOperationBinding(result.Item.ActivationWorkflowBinding, operation)
            || result.Item.ActivationWorkflowBinding!.TerminalDecision is not null)
            return await QuarantineAsync(claim, "ITEM_ACTIVATION_WORKFLOW_LOCAL_PENDING_INCONSISTENT", now, cancellationToken);
        return await AdvanceAsync(claim, ItemWorkflowCheckpoint.LocalPendingApplied,
            ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true, cancellationToken: cancellationToken);
    }

    private async Task<bool> ObserveDecisionAsync(ItemWorkflowOperation operation, ItemWorkflowClaim claim,
        DateTimeOffset now, TimeSpan retryDelay, bool counted, CancellationToken cancellationToken)
    {
        if (!operation.WorkflowInstanceId.HasValue)
            return await QuarantineAsync(claim, "ITEM_ACTIVATION_WORKFLOW_START_PROOF_INVALID", now, cancellationToken);
        var result = await workflowClient.GetTerminalEvidenceAsync(operation.TenantId,
            new(operation.WorkflowInstanceId.Value, operation.ObjectType, operation.ObjectId), cancellationToken);
        // The engine's first answer — open: wait. S2 FIX1 (minor, the GSKU correction's accepted rule): an approval still
        // open is not a failure — the person has not decided yet. It is asked again after the retry delay, its streak
        // reset; it never counts toward the retry limit (counted, a day's wait used to reach RETRY_LIMIT_REACHED in ~6.5 h
        // of 15-minute polls and the gauge called it stuck).
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.NonTerminal)
            return await operations.AdvanceAsync(claim, new(ItemWorkflowCheckpoint.AwaitingDecision,
                ProductIdentityWorkflowRecoveryDisposition.Retryable, now.UtcTicks, now.Add(retryDelay).UtcTicks,
                BoundedCode(result.ErrorCode, DecisionPendingCode), ReleaseLease: true), cancellationToken);
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.NotFound
                or ProductIdentityWorkflowTransportOutcome.Incomplete
            || Retryable(result.Outcome)
            || result.Outcome != ProductIdentityWorkflowTransportOutcome.Success
            && ProductIdentityWorkflowFaultPolicy.Classify(ProductIdentityWorkflowCall.TerminalEvidence, result.Outcome,
                result.ErrorCode).Kind == ProductIdentityWorkflowFaultKind.ServiceWait)
            return await ScheduleRetryAsync(operation, claim, ItemWorkflowCheckpoint.AwaitingDecision,
                result.ErrorCode ?? DecisionPendingCode, now, retryDelay, counted, cancellationToken);
        // The second — closed without a decision: the item goes back.
        if (ProductIdentityWorkflowFaultPolicy.IsClosedWithoutDecision(result))
            return await DecisionNotApplicableAsync(operation, claim, ClosedWithoutDecision, now, retryDelay, counted,
                cancellationToken);
        // The third — evidence that contradicts the record: stuck, the requester's repeat reads again.
        if (result.Outcome != ProductIdentityWorkflowTransportOutcome.Success || result.Value is null)
            return await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);

        var evidence = result.Value;
        if (Guid.TryParse(evidence.ActorUserId, out var self) && self == operation.MakerSubjectId)
        {
            // Maker ≠ approver: the maker decided their own request and the engine has closed it; no other decision will
            // come. Not applied: the item goes back, the reason is kept.
            log.LogWarning("Item activation operation {OperationId} of tenant {TenantId} was decided by its own maker; the decision is not applied ({ResultCode}).",
                operation.OperationId, operation.TenantId, SelfApprovalIgnored);
            return await DecisionNotApplicableAsync(operation, claim, SelfApprovalIgnored, now, retryDelay, counted,
                cancellationToken);
        }
        if (!IdentityApprovalBinding.TryMapDecision(evidence.TerminalAction, out var decision)
            || !IdentityApprovalBinding.ExactTerminalStatuses(decision, evidence.TaskStatus, evidence.InstanceStatus)
            || evidence.WorkflowInstanceId != operation.WorkflowInstanceId
            || evidence.ApprovalTaskId != operation.ApprovalTaskId
            || evidence.TemplateId != operation.WorkflowTemplateId
            || evidence.TemplateVersionId != operation.WorkflowTemplateVersionId
            || !string.Equals(evidence.ObjectType, operation.ObjectType, StringComparison.Ordinal)
            || !string.Equals(evidence.ObjectId, operation.ObjectId, StringComparison.Ordinal)
            || !string.Equals(evidence.ObjectRef, operation.ObjectRef, StringComparison.Ordinal)
            || !Guid.TryParse(evidence.ActorUserId, out var actorId) || actorId == Guid.Empty
            || evidence.TransitionSequence <= 0
            || evidence.DecisionAt.Offset != TimeSpan.Zero
            || decision == ProductIdentityDecisionKind.Rejected
               && (string.IsNullOrWhiteSpace(evidence.ReasonCode) || evidence.ReasonCode.Length > 128
                   || evidence.ReasonCode.Any(char.IsControl) || evidence.ReasonCode != evidence.ReasonCode.Trim()))
            return await QuarantineAsync(claim, "ITEM_ACTIVATION_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);

        return await operations.AdvanceAsync(claim, new(ItemWorkflowCheckpoint.DecisionObserved,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks, DecisionKind: decision,
            DecisionObservedAtUtcTicksV1: now.UtcTicks, DecisionActorSubjectId: actorId, DecisionReasonCode: evidence.ReasonCode,
            DecisionObjectType: evidence.ObjectType, DecisionObjectId: evidence.ObjectId, DecisionObjectRef: evidence.ObjectRef,
            DecisionWorkflowTemplateId: evidence.TemplateId, DecisionWorkflowTemplateVersionId: evidence.TemplateVersionId,
            DecisionTaskStatus: evidence.TaskStatus, DecisionInstanceStatus: evidence.InstanceStatus,
            DecisionTransitionSequence: evidence.TransitionSequence, DecisionAtUtcTicksV1: evidence.DecisionAt.UtcTicks,
            ReleaseLease: true), cancellationToken);
    }

    // A rejection is applied as it is; an approval passes the activation gate first (R-04): a gate no longer met makes
    // the approval NOT APPLICABLE — the item goes back with ITEM_STOCK_FIELDS_REQUIRED_FOR_ACTIVATION.
    private async Task<bool> ValidateApprovalOrApplyRejectAsync(ItemWorkflowOperation operation, ItemWorkflowClaim claim,
        DateTimeOffset now, TimeSpan retryDelay, bool counted, CancellationToken cancellationToken)
    {
        if (!TryRehydrateEvidence(operation, out var evidence))
            return await QuarantineAsync(claim, "ITEM_ACTIVATION_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        if (evidence.Decision == ProductIdentityDecisionKind.Rejected)
            return await ApplyDecisionAsync(operation, claim, now, retryDelay, counted, cancellationToken);
        var gate = await GateAsync(operation, cancellationToken);
        if (gate is not null) return await GateNotMetAsync(operation, claim, gate, now, retryDelay, counted, cancellationToken);
        // "S2 kanıt karşılaştırma kuralı": the codes the item holds are compared with their frozen evidence (code still in
        // its list, same kind), never with a fresh version number.
        var item = await items.GetByIdAsync(operation.ItemId, cancellationToken);
        if (item is null) return await QuarantineAsync(claim, ItemErrors.NotFound, now, cancellationToken);
        var held = await ItemReferenceEvidence.CheckAsync(
            new Diten.MdmService.Application.Contracts.ReferenceData.TenantBoundItemReferenceResolver(references,
                operation.TenantId), item, cancellationToken, operation.ReferenceEvidence);
        switch (held.Verdict)
        {
            case ItemReferenceEvidence.Verdict.Wait:
                return await ScheduleRetryAsync(operation, claim, operation.Checkpoint, held.Code, now, retryDelay, counted,
                    cancellationToken);
            case ItemReferenceEvidence.Verdict.NotApplicable:
                return await DecisionNotApplicableAsync(operation, claim, held.Code!, now, retryDelay, counted,
                    cancellationToken);
            case ItemReferenceEvidence.Verdict.Stuck:
                if (held.Code == ItemReferenceEvidence.ProviderConfigurationError)
                    log.LogError("Item activation operation {OperationId} of tenant {TenantId}: the item list provider refused MDM's service identity or configuration ({ResultCode}); the approved item stays pending until this is fixed and the requester submits again.",
                        operation.OperationId, operation.TenantId, held.Code);
                return await QuarantineAsync(claim, held.Code, now, cancellationToken);
        }
        return await operations.AdvanceAsync(claim, new(ItemWorkflowCheckpoint.ApprovalValidated,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks, ApprovalGateValidatedAtUtcTicksV1: now.UtcTicks,
            ReleaseLease: true), cancellationToken);
    }

    // Null: the gate is met. Otherwise the code (the item gone is the store's, not the maker's).
    private async Task<string?> GateAsync(ItemWorkflowOperation operation, CancellationToken cancellationToken)
    {
        var item = await items.GetByIdAsync(operation.ItemId, cancellationToken);
        if (item is null) return ItemErrors.NotFound;
        return ItemActivationGate.MissingFields(item).Count == 0 ? null : ItemActivationGate.StockFieldsRequired;
    }

    private Task<bool> GateNotMetAsync(ItemWorkflowOperation operation, ItemWorkflowClaim claim, string code,
        DateTimeOffset now, TimeSpan retryDelay, bool counted, CancellationToken cancellationToken) =>
        code == ItemActivationGate.StockFieldsRequired
            ? DecisionNotApplicableAsync(operation, claim, code, now, retryDelay, counted, cancellationToken)
            : QuarantineAsync(claim, code, now, cancellationToken);

    private async Task<bool> ApplyDecisionAsync(ItemWorkflowOperation operation, ItemWorkflowClaim claim,
        DateTimeOffset now, TimeSpan retryDelay, bool counted, CancellationToken cancellationToken)
    {
        if (!TryRehydrateEvidence(operation, out var evidence))
            return await QuarantineAsync(claim, "ITEM_ACTIVATION_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        if (evidence.Decision == ProductIdentityDecisionKind.Approved)
        {
            if (operation.ApprovalGateValidatedAtUtcTicksV1 is not > 0)
                return await QuarantineAsync(claim, "ITEM_ACTIVATION_WORKFLOW_APPROVAL_PROOF_INVALID", now, cancellationToken);
            // The gate again, as the decision is applied (the item is locked, so only a write outside MDM's own rules —
            // a later binary, a hand edit — can have moved it).
            var gate = await GateAsync(operation, cancellationToken);
            if (gate is not null) return await GateNotMetAsync(operation, claim, gate, now, retryDelay, counted, cancellationToken);
        }
        var result = await decisionWriter.ApplyDecisionAsync(claim, operation, evidence, now.UtcTicks, cancellationToken);
        if (!result.Succeeded)
            return await WriteRefusedAsync(operation, claim, result.ErrorCode, now, retryDelay, counted, cancellationToken);
        if (!ExactDecisionResult(result.Item, operation, evidence))
            return await QuarantineAsync(claim, "ITEM_ACTIVATION_WORKFLOW_DECISION_INCONSISTENT", now, cancellationToken);
        return await AdvanceAsync(claim, ItemWorkflowCheckpoint.DecisionApplied, ProductIdentityWorkflowRecoveryDisposition.None,
            now, releaseLease: true, cancellationToken: cancellationToken);
    }

    private async Task<bool> VerifyDecisionAppliedAndCompleteAsync(ItemWorkflowOperation operation, ItemWorkflowClaim claim,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!TryRehydrateEvidence(operation, out var evidence))
            return await QuarantineAsync(claim, "ITEM_ACTIVATION_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        var item = await items.GetByIdAsync(operation.ItemId, cancellationToken);
        if (!ExactDecisionResult(item, operation, evidence))
            return await QuarantineAsync(claim, "ITEM_ACTIVATION_WORKFLOW_DECISION_INCONSISTENT", now, cancellationToken);
        return await AdvanceAsync(claim, ItemWorkflowCheckpoint.Completed, ProductIdentityWorkflowRecoveryDisposition.None,
            now, releaseLease: true, cancellationToken: cancellationToken);
    }

    // Where the decision lands (§8.1): approved → Active; rejected → Draft, or Deactivated for a reactivation.
    public static ItemLifecycleStatus DecisionTarget(ItemActivationKind kind, ProductIdentityDecisionKind decision) =>
        decision == ProductIdentityDecisionKind.Approved
            ? ItemLifecycle.Target(ItemLifecycleStatus.PendingActivation, ItemTransition.ApproveActivation, kind)
            : ItemLifecycle.Target(ItemLifecycleStatus.PendingActivation, ItemTransition.RejectActivation, kind);

    // S2 FIX1 item 3: the decision is recognised by what it wrote — this approval's binding and its terminal decision
    // (instance, task, transition sequence …) with the target status — never by an exact version: an Active item may take
    // a name / description edit between the decision's write and this check (the audit POST alone may take 100 s), and
    // an exact version would then fail every replay and the reconciliation after it.
    private static bool ExactDecisionResult(Item? item, ItemWorkflowOperation operation,
        ProductIdentityWorkflowDecisionEvidence evidence) =>
        item is not null && item.Version >= operation.ExpectedItemVersion + 2
        && item.ActivationWorkflowBinding is { } binding && ExactOperationBinding(binding, operation)
        && binding.TerminalDecision is { } persisted && ExactTerminalDecision(persisted, evidence)
        && item.LifecycleStatus == DecisionTarget(operation.ActivationKind, evidence.Decision);

    // Write classes (§8.4 B): the store and a lease held elsewhere wait (never stuck); a concurrent write is retried and
    // counted (stuck with its cause at the limit); anything else is stuck at once — its requester's repeat is the exit.
    private Task<bool> WriteRefusedAsync(ItemWorkflowOperation operation, ItemWorkflowClaim claim, string? code,
        DateTimeOffset now, TimeSpan retryDelay, bool counted, CancellationToken cancellationToken)
    {
        var normalized = code switch
        {
            "ITEM_VERSION_CONFLICT" => "ITEM_CONCURRENCY_CONFLICT",
            _ => code
        };
        if (operation.Checkpoint == ItemWorkflowCheckpoint.ManualReconciliationRequired)
            return QuarantineAsync(claim, normalized, now, cancellationToken);
        if (ProductIdentityWriteFaults.MayPassByItself(normalized))
            return ScheduleRetryAsync(operation, claim, operation.Checkpoint, normalized, now, retryDelay, counted, cancellationToken);
        if (!ProductIdentityWriteFaults.MetAConcurrentWrite(normalized) && normalized != "ITEM_CONCURRENCY_CONFLICT")
            return QuarantineAsync(claim, normalized, now, cancellationToken);
        var (exhausted, _, next) = ProductIdentityRecoveryStreak.Next(
            counted ? operation.FailedAttemptCount : Math.Max(operation.FailedAttemptCount - 1, 0), now, retryDelay,
            ProductIdentityRecoveryStreak.DefaultQuarantineAfterFailedAttempts, null);
        if (counted && exhausted) return QuarantineAsync(claim, StuckOnRecordAnswer, now, cancellationToken);
        return operations.AdvanceAsync(claim, new(operation.Checkpoint, ProductIdentityWorkflowRecoveryDisposition.Retryable,
            now.UtcTicks, next, BoundedCode(normalized, "ITEM_CONCURRENCY_CONFLICT"), ReleaseLease: true,
            Streak: counted ? RecoveryStreakWrite.Increment : RecoveryStreakWrite.Keep), cancellationToken);
    }

    private Task<bool> ClosedBeforeWithdrawalAsync(ItemWorkflowOperation operation, ItemWorkflowClaim claim, string? code,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        log.LogInformation("Item activation operation {OperationId} of tenant {TenantId}: the withdrawal was refused by the engine ({ResultCode}); the approval stays and its decision is applied.",
            operation.OperationId, operation.TenantId, code ?? "none");
        return operations.AdvanceAsync(claim, new(ItemWorkflowCheckpoint.AwaitingDecision,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
            LastFailureCode: BoundedCode(code, "ITEM_ACTIVATION_WITHDRAWAL_REFUSED"), ReleaseLease: true,
            ClearWithdrawal: true), cancellationToken);
    }

    /*
     * A wait is bounded and counted. Only the worker's attempts count (an interactive repeat keeps the streak). At the
     * limit the operation STAYS where it waits and Retryable — never "manual + retryable" — with RETRY_LIMIT_REACHED as
     * its cause, so the gauge counts it (manual_reconciliation) and the error line is said once.
     */
    private async Task<bool> ScheduleRetryAsync(ItemWorkflowOperation operation, ItemWorkflowClaim claim,
        ItemWorkflowCheckpoint checkpoint, string? errorCode, DateTimeOffset now, TimeSpan retryDelay, bool counted,
        CancellationToken cancellationToken, long? withdrawalUnansweredSince = null)
    {
        var (exhausted, _, next) = ProductIdentityRecoveryStreak.Next(
            counted ? operation.FailedAttemptCount : Math.Max(operation.FailedAttemptCount - 1, 0), now, retryDelay,
            ProductIdentityRecoveryStreak.DefaultQuarantineAfterFailedAttempts, ProductIdentityRecoveryStreak.DefaultMaxRetryDelay);
        var code = BoundedCode(errorCode, "ITEM_ACTIVATION_WORKFLOW_PROVIDER_UNAVAILABLE");
        string? cause = null;
        if (counted && exhausted || !counted && operation.LastFailureCode == RetryLimitReached)
        {
            cause = code;
            code = RetryLimitReached;
        }
        return await operations.AdvanceAsync(claim, new(checkpoint, ProductIdentityWorkflowRecoveryDisposition.Retryable,
            now.UtcTicks, next, code, ReleaseLease: true,
            Streak: counted ? RecoveryStreakWrite.Increment : RecoveryStreakWrite.Keep,
            WithdrawalUnansweredSinceUtcTicksV1: withdrawalUnansweredSince, RetryLimitCauseCode: cause), cancellationToken);
    }

    private Task<bool> QuarantineAsync(ItemWorkflowClaim claim, string? errorCode, DateTimeOffset now,
        CancellationToken cancellationToken) =>
        AdvanceAsync(claim, ItemWorkflowCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, now,
            BoundedCode(errorCode, "ITEM_ACTIVATION_WORKFLOW_RECONCILIATION_REQUIRED"), releaseLease: true,
            cancellationToken: cancellationToken);

    private Task<bool> AdvanceAsync(ItemWorkflowClaim claim, ItemWorkflowCheckpoint checkpoint,
        ProductIdentityWorkflowRecoveryDisposition disposition, DateTimeOffset now, string? errorCode = null,
        bool releaseLease = false, CancellationToken cancellationToken = default) =>
        operations.AdvanceAsync(claim, new(checkpoint, disposition, now.UtcTicks, LastFailureCode: errorCode,
            ReleaseLease: releaseLease), cancellationToken);

    private static bool CompleteStartProof(ItemWorkflowOperation operation) =>
        operation.WorkflowInstanceId is { } instanceId && instanceId != Guid.Empty
        && operation.WorkflowTemplateId is { } templateId && templateId != Guid.Empty
        && operation.WorkflowTemplateVersionId is { } versionId && versionId != Guid.Empty
        && operation.ApprovalTaskId is { } taskId && taskId != Guid.Empty
        && operation.AssignmentSnapshotId is { } snapshotId && snapshotId != Guid.Empty
        && operation.StartTransitionLogId is { } logId && logId != Guid.Empty
        && operation.WorkflowStartedAtUtcTicksV1 is > 0;

    private static bool ExactOperationBinding(ProductIdentityWorkflowBinding? binding, ItemWorkflowOperation operation) =>
        binding is not null
        && binding.WorkflowInstanceId == operation.WorkflowInstanceId
        && binding.WorkflowTemplateId == operation.WorkflowTemplateId
        && binding.WorkflowTemplateVersionId == operation.WorkflowTemplateVersionId
        && binding.ApprovalTaskId == operation.ApprovalTaskId
        && binding.AssignmentSnapshotId == operation.AssignmentSnapshotId
        && binding.StartTransitionLogId == operation.StartTransitionLogId
        && binding.ObjectType == operation.ObjectType
        && binding.ObjectId == operation.ItemId
        && binding.ObjectRef == operation.ObjectRef
        && binding.SubmitterSubjectId == operation.MakerSubjectId
        && binding.StartIdempotencyKey == operation.StartIdempotencyKey
        && binding.StartRequestFingerprint == operation.OperationFingerprint
        && binding.SubmittedAtUtc.UtcTicks == operation.WorkflowStartedAtUtcTicksV1
        && binding.DueAtUtc?.UtcTicks == operation.DueAtUtcTicksV1;

    private static bool ExactTerminalDecision(ProductIdentityWorkflowDecisionEvidence actual,
        ProductIdentityWorkflowDecisionEvidence expected) =>
        actual.Decision == expected.Decision
        && actual.WorkflowInstanceId == expected.WorkflowInstanceId
        && actual.ApprovalTaskId == expected.ApprovalTaskId
        && actual.WorkflowTemplateId == expected.WorkflowTemplateId
        && actual.WorkflowTemplateVersionId == expected.WorkflowTemplateVersionId
        && actual.ObjectType == expected.ObjectType
        && actual.ObjectId == expected.ObjectId
        && actual.ObjectRef == expected.ObjectRef
        && actual.DecisionActorSubjectId == expected.DecisionActorSubjectId
        && actual.ReasonCode == expected.ReasonCode
        && actual.DecisionAtUtc == expected.DecisionAtUtc
        && actual.TransitionSequence == expected.TransitionSequence
        && actual.TaskStatus == expected.TaskStatus
        && actual.InstanceStatus == expected.InstanceStatus;

    public static bool TryRehydrateEvidence(ItemWorkflowOperation operation, out ProductIdentityWorkflowDecisionEvidence evidence)
    {
        evidence = null!;
        if (!operation.DecisionKind.HasValue || !operation.WorkflowInstanceId.HasValue
            || !operation.ApprovalTaskId.HasValue || !operation.DecisionWorkflowTemplateId.HasValue
            || !operation.DecisionWorkflowTemplateVersionId.HasValue || !operation.DecisionActorSubjectId.HasValue
            || !operation.DecisionTransitionSequence.HasValue || !operation.DecisionAtUtcTicksV1.HasValue
            || !Guid.TryParse(operation.DecisionObjectId, out var objectId))
            return false;
        evidence = new()
        {
            Decision = operation.DecisionKind.Value,
            WorkflowInstanceId = operation.WorkflowInstanceId.Value,
            ApprovalTaskId = operation.ApprovalTaskId.Value,
            WorkflowTemplateId = operation.DecisionWorkflowTemplateId.Value,
            WorkflowTemplateVersionId = operation.DecisionWorkflowTemplateVersionId.Value,
            ObjectType = operation.DecisionObjectType ?? string.Empty,
            ObjectId = objectId,
            ObjectRef = operation.DecisionObjectRef ?? string.Empty,
            DecisionActorSubjectId = operation.DecisionActorSubjectId.Value,
            ReasonCode = operation.DecisionReasonCode,
            DecisionAtUtc = new DateTimeOffset(operation.DecisionAtUtcTicksV1.Value, TimeSpan.Zero),
            TransitionSequence = operation.DecisionTransitionSequence.Value,
            TaskStatus = operation.DecisionTaskStatus ?? string.Empty,
            InstanceStatus = operation.DecisionInstanceStatus ?? string.Empty
        };
        return true;
    }

    private static bool ExactInteractiveFacts(ItemWorkflowOperation operation, Guid tenantId, Guid itemId,
        ItemActivationKind kind, int expectedVersion, Guid makerSubjectId) =>
        !operation.IsDeleted && operation.TenantId == tenantId && operation.ItemId == itemId
        && operation.ActivationKind == kind && operation.ExpectedItemVersion == expectedVersion
        && operation.MakerSubjectId == makerSubjectId
        && string.Equals(operation.ObjectType, ItemActivationWorkflowStartRequestFactory.ItemObjectType, StringComparison.Ordinal)
        && string.Equals(operation.ObjectId, itemId.ToString("D"), StringComparison.Ordinal);

    private static bool Retryable(ProductIdentityWorkflowTransportOutcome outcome) =>
        outcome is ProductIdentityWorkflowTransportOutcome.Retryable or ProductIdentityWorkflowTransportOutcome.Timeout;

    private static string BoundedCode(string? code, string fallback) => IdentityApprovalBinding.BoundedCode(code, fallback);

    private static bool OptionalExactText(string? value, int max) => value is null
        || value.Length <= max && value == value.Trim() && !value.Any(char.IsControl);

    private static string WithdrawalFingerprint(Guid tenantId, Guid itemId, int expectedVersion, Guid commandId,
        Guid requester, string reason, string? comment, ProductIdentityWorkflowBinding binding)
    {
        var facts = string.Join('|', "item-activation-withdrawal-v1", tenantId.ToString("D"), itemId.ToString("D"),
            expectedVersion.ToString(CultureInfo.InvariantCulture), commandId.ToString("D"), requester.ToString("D"),
            reason, comment ?? string.Empty, binding.WorkflowInstanceId.ToString("D"), binding.ApprovalTaskId.ToString("D"),
            binding.StartIdempotencyKey);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(facts))).ToLowerInvariant();
    }

    private static bool ExactWithdrawalFacts(ItemWorkflowOperation operation, Guid commandId, string fingerprint,
        Guid requester, int expectedVersion, string reason, string? comment) =>
        operation.WithdrawalCommandId == commandId && operation.WithdrawalFingerprint == fingerprint
        && operation.WithdrawalRequesterSubjectId == requester && operation.WithdrawalExpectedItemVersion == expectedVersion
        && operation.WithdrawalReasonCode == reason && operation.WithdrawalComment == comment;

    private static bool TryCancellationEvidence(ItemWorkflowOperation operation,
        out Diten.MdmService.Domain.ValueObjects.ProductIdentityWorkflowCancellationEvidence evidence)
    {
        evidence = null!;
        if (operation.WorkflowInstanceId is not { } workflowId || operation.ApprovalTaskId is not { } taskId
            || operation.WorkflowTemplateId is not { } templateId || operation.WorkflowTemplateVersionId is not { } templateVersionId
            || operation.WithdrawalRequesterSubjectId is not { } requester
            || operation.WithdrawalTransitionLogId is not { } logId || operation.WithdrawalObservedAtUtcTicksV1 is not > 0
            || operation.WithdrawalTransitionSequence is not > 0 || operation.WithdrawalResultWorkflowInstanceVersion is not > 0
            || operation.WithdrawalResultApprovalTaskVersion is not > 0 || operation.WithdrawalCommandId is not { } commandId)
            return false;
        evidence = new()
        {
            WorkflowInstanceId = workflowId, ApprovalTaskId = taskId, WorkflowTemplateId = templateId,
            WorkflowTemplateVersionId = templateVersionId, ObjectType = operation.ObjectType, ObjectId = operation.ItemId,
            ObjectRef = operation.WithdrawalObjectRef ?? operation.ObjectRef, RequesterSubjectId = requester,
            ReasonCode = operation.WithdrawalReasonCode!, Comment = operation.WithdrawalComment,
            CancelledAtUtc = new DateTimeOffset(operation.WithdrawalObservedAtUtcTicksV1.Value, TimeSpan.Zero),
            TransitionSequence = operation.WithdrawalTransitionSequence.Value, TransitionLogId = logId,
            TaskStatus = operation.WithdrawalTaskStatus!, InstanceStatus = operation.WithdrawalInstanceStatus!,
            WorkflowInstanceVersion = operation.WithdrawalResultWorkflowInstanceVersion.Value,
            ApprovalTaskVersion = operation.WithdrawalResultApprovalTaskVersion.Value,
            IdempotencyKey = commandId.ToString("D")
        };
        return evidence.TaskStatus == "Cancelled" && evidence.InstanceStatus == "Cancelled";
    }

    private static ItemActivationWorkflowProcessingResult Success(ItemWorkflowOperation operation, bool replay) =>
        new(true, operation, null, 200, replay);

    private static ItemActivationWorkflowProcessingResult Fail(string errorCode, int statusCode) =>
        new(false, null, errorCode, statusCode, false);

    private static ItemActivationWorkflowProcessingResult Fail(ItemWorkflowOperation operation, string errorCode,
        int statusCode) => new(false, operation, errorCode, statusCode, false);
}
