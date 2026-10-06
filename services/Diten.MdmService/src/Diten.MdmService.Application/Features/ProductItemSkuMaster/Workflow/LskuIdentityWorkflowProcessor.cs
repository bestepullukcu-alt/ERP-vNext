using System.Globalization;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record LskuIdentityWorkflowProcessingResult(
    bool Succeeded,
    LskuIdentityWorkflowOperation? Operation,
    string? ErrorCode,
    int StatusCode,
    bool IsReplay);

public sealed record LskuIdentityWorkflowExecutionConfiguration(
    TimeSpan LeaseDuration,
    TimeSpan RetryDelay,
    // LSKU FIX2 item 10: how long after a start was sent the engine's "nothing under this key" is not trusted.
    TimeSpan? StartSettleWindow = null)
{
    public TimeSpan SettleWindow => StartSettleWindow is { } window && window > TimeSpan.Zero ? window : TimeSpan.FromMinutes(2);
}

public sealed class LskuIdentityWorkflowProcessor(
    ILskuIdentityWorkflowOperationRepository operations,
    ILskuRepository lskus,
    IGskuRepository gskus,
    IProductDefinitionRevisionRepository revisions,
    IWorkflowVerifiedMarketReferenceResolver marketResolver,
    IProductIdentityWorkflowClient workflowClient,
    LskuIdentityWorkflowStartRequestFactory requestFactory,
    TimeProvider timeProvider,
    ILogger<LskuIdentityWorkflowProcessor>? logger = null,
    LskuIdentityWorkflowExecutionConfiguration? execution = null)
{
    private readonly ILogger log = logger ?? NullLogger<LskuIdentityWorkflowProcessor>.Instance;
    private TimeSpan SettleWindow => execution?.SettleWindow ?? TimeSpan.FromMinutes(2);

    // A start sent less than the settle window ago may still be in the engine's hands (never sent: nothing to wait for).
    // FIX3 M2: a legacy document (no mark) may have sent its start at its last update — the window runs from there,
    // as in the retirement flow (?? UpdatedAtUtcTicksV1); only the explicit 0 has nothing to wait for.
    private bool Settling(LskuIdentityWorkflowOperation operation, DateTimeOffset now) =>
        (operation.LastStartSentAtUtcTicksV1 ?? operation.UpdatedAtUtcTicksV1) is var sent && sent > 0
        && now.UtcTicks - sent < SettleWindow.Ticks;

    // FIX2 item 13: only 0 proves "never sent" — written at reservation. A legacy operation (the field missing, read as
    // null) may have sent its start before the field existed.
    private static bool NeverSent(LskuIdentityWorkflowOperation operation) => operation.LastStartSentAtUtcTicksV1 == 0;

    // LSKU FIX1 B3 / B5 — the named ends and stuck reasons of the GSKU identity pattern.
    public const string StartRefused = "LSKU_IDENTITY_WORKFLOW_START_REFUSED";
    public const string ReconciledNothingStarted = "LSKU_IDENTITY_RECONCILED_NOTHING_STARTED";
    public const string SelfApprovalIgnored = "LSKU_IDENTITY_SELF_APPROVAL_IGNORED";
    public const string ClosedWithoutDecision = "LSKU_IDENTITY_CLOSED_WITHOUT_DECISION";
    public const string DecisionNotApplicableCode = "LSKU_IDENTITY_DECISION_NOT_APPLICABLE";
    public const string ManualReconciliationNeeded = "LSKU_IDENTITY_WORKFLOW_MANUAL_RECONCILIATION_NEEDED";
    public const string StuckOnRecordAnswer = "LSKU_IDENTITY_WORKFLOW_STUCK_ON_RECORD_ANSWER";
    public const string SubmitTargetChanged = "LSKU_IDENTITY_SUBMIT_TARGET_CHANGED";

    private static readonly LskuIdentityWorkflowCheckpoint[] RecoverableCheckpoints =
    [
        LskuIdentityWorkflowCheckpoint.Prepared,
        LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown,
        LskuIdentityWorkflowCheckpoint.WorkflowStarted,
        LskuIdentityWorkflowCheckpoint.LocalPendingApplied,
        LskuIdentityWorkflowCheckpoint.AwaitingDecision,
        LskuIdentityWorkflowCheckpoint.DecisionObserved,
        LskuIdentityWorkflowCheckpoint.ApprovalValidated,
        LskuIdentityWorkflowCheckpoint.DecisionApplied,
        LskuIdentityWorkflowCheckpoint.WithdrawalRequested,
        LskuIdentityWorkflowCheckpoint.WithdrawalPreflightObserved,
        LskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown,
        LskuIdentityWorkflowCheckpoint.WithdrawalObserved,
        LskuIdentityWorkflowCheckpoint.WithdrawalApplied
    ];

    public async Task<LskuIdentityWorkflowProcessingResult> StartInteractiveAsync(
        Guid tenantId,
        Guid lskuId,
        int expectedVersion,
        Guid operationId,
        Guid makerSubjectId,
        string delegatedUserToken,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || lskuId == Guid.Empty || operationId == Guid.Empty
            || makerSubjectId == Guid.Empty || expectedVersion < 0
            || string.IsNullOrWhiteSpace(delegatedUserToken))
        {
            return Fail("LSKU_IDENTITY_WORKFLOW_START_INVALID", 400);
        }

        var existing = await operations.GetByOperationIdAsync(operationId, cancellationToken);
        LskuIdentityWorkflowOperation operation;
        var replay = existing is not null;
        if (existing is null)
        {
            var lsku = await lskus.GetByIdAsync(lskuId, cancellationToken);
            if (lsku is null)
            {
                return Fail("LSKU_IDENTITY_NOT_FOUND", 404);
            }

            var gsku = await gskus.GetByIdAsync(lsku.GskuId, cancellationToken);
            var revision = gsku is null
                ? null
                : await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
            if (gsku is null || revision is null)
            {
                return Fail("LSKU_IDENTITY_PARENT_NOT_REFERENCEABLE", 409);
            }
            LskuIdentityWorkflowStartPlan plan;
            try
            {
                plan = requestFactory.Create(
                    tenantId, lsku, gsku, revision, operationId, expectedVersion, makerSubjectId);
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message, exception.Message == "LSKU_IDENTITY_WORKFLOW_CONFIGURATION_INVALID"
                    ? 503 : 409);
            }

            var reserved = await operations.ReserveAsync(plan.Operation, cancellationToken);
            if (!reserved.Succeeded || reserved.Operation is null)
            {
                return Fail(reserved.ErrorCode ?? "LSKU_IDENTITY_WORKFLOW_OPERATION_CONFLICT", 409);
            }

            operation = reserved.Operation;
            replay = reserved.IsReplay;
        }
        else
        {
            operation = existing;
        }

        if (!ExactInteractiveFacts(operation, tenantId, lskuId, expectedVersion, makerSubjectId))
        {
            return Fail("LSKU_IDENTITY_WORKFLOW_OPERATION_CONFLICT", 409);
        }

        // LSKU FIX2 item 2 (found by the transition census): a submit whose WITHDRAWAL is under way belongs to the
        // withdrawal path — a repeat of the original submit must not read a withdrawal's AwaitingMakerReplay as a start
        // waiting for its replay (the store's table alone cannot tell the two apart: AMR → WorkflowStarted is allowed).
        if (InWithdrawal(operation))
        {
            return Fail(operation, WithdrawalUnderWay, 409);
        }

        var result = await ProcessAsync(
            operation,
            delegatedUserToken,
            $"interactive-{makerSubjectId:N}",
            leaseDuration,
            retryDelay,
            stopWhenAwaitingDecision: true,
            cancellationToken);
        return result with { IsReplay = replay || result.IsReplay };
    }

    public Task<LskuIdentityWorkflowProcessingResult> RecoverAsync(
        LskuIdentityWorkflowOperation operation,
        string leaseOwner,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default) =>
        InWithdrawal(operation)
            ? ProcessWithdrawalAsync(operation, null, leaseOwner, leaseDuration, retryDelay, cancellationToken)
            : ProcessAsync(operation, null, leaseOwner, leaseDuration, retryDelay,
                stopWhenAwaitingDecision: false, cancellationToken);

    public async Task<LskuIdentityWorkflowProcessingResult> WithdrawInteractiveAsync(
        Guid tenantId, Guid lskuId, int expectedVersion, Guid commandId, Guid requesterSubjectId,
        string reasonCode, string? comment, string delegatedUserToken, TimeSpan leaseDuration,
        TimeSpan retryDelay, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || lskuId == Guid.Empty || commandId == Guid.Empty
            || requesterSubjectId == Guid.Empty || expectedVersion < 1
            || !ExactText(reasonCode, 128) || !OptionalExactText(comment, 2000)
            || string.IsNullOrWhiteSpace(delegatedUserToken))
            return Fail("LSKU_IDENTITY_WITHDRAWAL_INVALID", 400);
        var lsku = await lskus.GetByIdAsync(lskuId, cancellationToken);
        if (lsku?.IdentityWorkflowBinding is not { } binding)
            return Fail(lsku is null ? "LSKU_IDENTITY_NOT_FOUND" : "LSKU_IDENTITY_STATE_CONFLICT", lsku is null ? 404 : 409);
        if (binding.SubmitterSubjectId != requesterSubjectId)
            return Fail("LSKU_IDENTITY_WITHDRAWAL_NOT_REQUESTER", 403);
        var operation = await operations.GetByStartIdempotencyKeyAsync(binding.StartIdempotencyKey, cancellationToken);
        if (operation is null || operation.TenantId != tenantId || operation.LskuId != lskuId
            || operation.MakerSubjectId != requesterSubjectId || operation.WorkflowInstanceId != binding.WorkflowInstanceId
            || operation.ApprovalTaskId != binding.ApprovalTaskId)
            return Fail("LSKU_IDENTITY_WORKFLOW_BINDING_CONFLICT", 409);
        var fingerprint = WithdrawalFingerprint(tenantId, lskuId, expectedVersion, commandId,
            requesterSubjectId, reasonCode, comment, binding);
        if (operation.WithdrawalCommandId.HasValue && !ExactWithdrawalFacts(operation, commandId,
                fingerprint, requesterSubjectId, expectedVersion, reasonCode, comment))
            return Fail("LSKU_IDENTITY_WITHDRAWAL_OPERATION_CONFLICT", 409);
        if (!operation.WithdrawalCommandId.HasValue && (lsku.Version != expectedVersion
            || lsku.LifecycleStatus != ProductIdentityLifecycleStatus.PendingIdentityApproval
            || operation.Checkpoint != LskuIdentityWorkflowCheckpoint.AwaitingDecision))
            return Fail("LSKU_IDENTITY_STATE_CONFLICT", 409);
        var result = await ProcessWithdrawalAsync(operation, delegatedUserToken,
            $"withdraw-{requesterSubjectId:N}", leaseDuration, retryDelay, cancellationToken,
            commandId, fingerprint, requesterSubjectId, expectedVersion, reasonCode, comment);
        return result with { IsReplay = operation.WithdrawalCommandId.HasValue || result.IsReplay };
    }

    private async Task<LskuIdentityWorkflowProcessingResult> ProcessWithdrawalAsync(
        LskuIdentityWorkflowOperation initial, string? token, string leaseOwner, TimeSpan leaseDuration,
        TimeSpan retryDelay, CancellationToken cancellationToken, Guid? commandId = null,
        string? fingerprint = null, Guid? requester = null, int? expectedVersion = null,
        string? reasonCode = null, string? comment = null)
    {
        var operation = initial;
        for (var step = 0; step < 10; step++)
        {
            if (operation.Checkpoint == LskuIdentityWorkflowCheckpoint.Completed)
                return operation.WithdrawalCommandId.HasValue ? Success(operation, true)
                    : Fail(operation, "LSKU_IDENTITY_WORKFLOW_STATE_CONFLICT", 409);
            // FIX3 M3: a withdrawal parked for its maker is the worker's end — only the requester's same withdrawal can
            // send the cancel; the worker neither claims nor rewrites it.
            if (string.IsNullOrWhiteSpace(token) && operation.Checkpoint == LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay)
                return Fail(operation, "LSKU_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED", 409);
            var now = timeProvider.GetUtcNow();
            var claim = await operations.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
                [operation.Checkpoint], leaseOwner, now.UtcTicks, now.Add(leaseDuration).UtcTicks), cancellationToken);
            if (claim is null) return Fail(operation, "LSKU_IDENTITY_WORKFLOW_BUSY", 409);
            // FIX3 item 4: every decision of this step is made on the document as it is UNDER the claim — a copy read
            // before it may be stale within the same checkpoint (the claim compares the checkpoint, not the facts).
            if (await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken) is not { } underClaim
                || underClaim.Checkpoint != claim.Checkpoint)
                return Fail(operation, "LSKU_IDENTITY_WORKFLOW_BUSY", 409);
            operation = underClaim;
            bool advanced;
            switch (operation.Checkpoint)
            {
                case LskuIdentityWorkflowCheckpoint.AwaitingDecision:
                    if (!commandId.HasValue || fingerprint is null || !requester.HasValue || !expectedVersion.HasValue || reasonCode is null)
                        return Fail(operation, "LSKU_IDENTITY_WORKFLOW_DECISION_PENDING", 409);
                    advanced = await operations.AdvanceAsync(claim, new(
                        LskuIdentityWorkflowCheckpoint.WithdrawalRequested,
                        ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
                        WithdrawalCommandId: commandId, WithdrawalFingerprint: fingerprint,
                        WithdrawalRequesterSubjectId: requester, WithdrawalExpectedLskuVersion: expectedVersion,
                        WithdrawalReasonCode: reasonCode, WithdrawalComment: comment, ReleaseLease: true), cancellationToken);
                    break;
                case LskuIdentityWorkflowCheckpoint.WithdrawalRequested:
                {
                    var preflight = await workflowClient.GetCancellationPreflightAsync(operation.TenantId,
                        new(operation.WorkflowInstanceId!.Value, operation.ApprovalTaskId!.Value,
                            operation.ObjectType, operation.ObjectId, operation.MakerSubjectId), cancellationToken);
                    if (preflight.Outcome != ProductIdentityWorkflowTransportOutcome.Success || preflight.Value is not { } p)
                    {
                        // LSKU FIX1 B5: the engine refused the withdrawal (409 = the approver has already decided):
                        // nothing changed there — the operation goes back to awaiting the decision, every withdrawal
                        // field goes, and the decision is applied by the ordinary observation.
                        advanced = Retryable(preflight.Outcome)
                            ? await ScheduleRetryAsync(operation, claim, operation.Checkpoint, preflight.ErrorCode, now, retryDelay,
                                string.IsNullOrWhiteSpace(token), cancellationToken)
                            : await ClosedBeforeWithdrawalAsync(operation, claim, preflight.ErrorCode, now, cancellationToken);
                        break;
                    }
                    advanced = await operations.AdvanceAsync(claim, new(
                        LskuIdentityWorkflowCheckpoint.WithdrawalPreflightObserved,
                        ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
                        WithdrawalExpectedWorkflowInstanceVersion: p.WorkflowInstanceVersion,
                        WithdrawalExpectedApprovalTaskVersion: p.ApprovalTaskVersion,
                        WithdrawalObjectRef: p.ObjectRef, ReleaseLease: true), cancellationToken);
                    break;
                }
                case LskuIdentityWorkflowCheckpoint.WithdrawalPreflightObserved:
                case LskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown:
                case LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay:
                {
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        // FIX3 item 2 / M3: parked for its maker (only the requester's same withdrawal can send the
                        // cancel). The worker's pass ENDS here — it does not re-claim and rewrite AMR until its step
                        // budget — and the time the cancel was first left unanswered is kept for the gauge.
                        if (!await operations.AdvanceAsync(claim, new(LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                                ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, now.UtcTicks,
                                LastFailureCode: "LSKU_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED", ReleaseLease: true,
                                WithdrawalUnansweredSinceUtcTicksV1: operation.WithdrawalUnansweredSinceUtcTicksV1 ?? now.UtcTicks),
                                cancellationToken))
                            return Fail(operation, "LSKU_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT", 409);
                        var parked = await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken) ?? operation;
                        return Fail(parked, "LSKU_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED", 409);
                    }
                    var cancelled = await workflowClient.CancelAsync(operation.TenantId, new(
                        operation.WorkflowInstanceId!.Value, operation.ApprovalTaskId!.Value,
                        operation.ObjectType, operation.ObjectId, operation.MakerSubjectId,
                        operation.WithdrawalExpectedWorkflowInstanceVersion!.Value,
                        operation.WithdrawalExpectedApprovalTaskVersion!.Value,
                        operation.WithdrawalReasonCode!, operation.WithdrawalComment,
                        operation.WithdrawalCommandId!.Value.ToString("D")), token, cancellationToken);
                    if (cancelled.Outcome != ProductIdentityWorkflowTransportOutcome.Success || cancelled.Value is not { } e)
                    {
                        // An unanswered cancel may have happened: asked again. A refused one did not: B5 as above.
                        advanced = Retryable(cancelled.Outcome)
                                   || cancelled.Outcome == ProductIdentityWorkflowTransportOutcome.Incomplete
                            ? await ScheduleRetryAsync(operation, claim, LskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown,
                                cancelled.ErrorCode, now, retryDelay, string.IsNullOrWhiteSpace(token), cancellationToken,
                                withdrawalUnansweredSince: operation.WithdrawalUnansweredSinceUtcTicksV1 ?? now.UtcTicks)
                            : await ClosedBeforeWithdrawalAsync(operation, claim, cancelled.ErrorCode, now, cancellationToken);
                        break;
                    }
                    advanced = await operations.AdvanceAsync(claim, new(
                        LskuIdentityWorkflowCheckpoint.WithdrawalObserved,
                        ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
                        WithdrawalTransitionLogId: e.TransitionLogId,
                        WithdrawalObservedAtUtcTicksV1: e.DecisionAt.UtcTicks,
                        WithdrawalTransitionSequence: e.TransitionSequence,
                        WithdrawalResultWorkflowInstanceVersion: e.WorkflowInstanceVersion,
                        WithdrawalResultApprovalTaskVersion: e.ApprovalTaskVersion,
                        WithdrawalTaskStatus: e.TaskStatus, WithdrawalInstanceStatus: e.InstanceStatus,
                        WithdrawalObjectRef: e.ObjectRef, ReleaseLease: true), cancellationToken);
                    break;
                }
                case LskuIdentityWorkflowCheckpoint.WithdrawalObserved:
                {
                    if (!TryCancellationEvidence(operation, out var evidence))
                    { advanced = await QuarantineAsync(claim, "LSKU_IDENTITY_WITHDRAWAL_EVIDENCE_INVALID", now, cancellationToken); break; }
                    var lsku = await lskus.GetByIdAsync(operation.LskuId, cancellationToken);
                    if (lsku is null) { advanced = await QuarantineAsync(claim, "LSKU_IDENTITY_NOT_FOUND", now, cancellationToken); break; }
                    var audit = LskuIdentityLifecycleAuditIntentFactory.CreateWithdrawal(lsku,
                        operation.WithdrawalExpectedLskuVersion!.Value, evidence);
                    var written = await operations.ApplyWithdrawalAsync(claim, operation, evidence, audit, now.UtcTicks, cancellationToken);
                    advanced = written.Succeeded;
                    break;
                }
                case LskuIdentityWorkflowCheckpoint.WithdrawalApplied:
                    advanced = await AdvanceAsync(claim, LskuIdentityWorkflowCheckpoint.Completed,
                        ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true,
                        cancellationToken: cancellationToken);
                    break;
                default: return Fail(operation, "LSKU_IDENTITY_WORKFLOW_STATE_CONFLICT", 409);
            }
            if (!advanced) return Fail(operation, "LSKU_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT", 409);
            operation = await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken)
                ?? throw new InvalidOperationException("LSKU_IDENTITY_WORKFLOW_OPERATION_LOST");
            // B5: the engine refused the withdrawal — it ended here (the approval stays); the requester learns why.
            if (operation.Checkpoint == LskuIdentityWorkflowCheckpoint.AwaitingDecision && !operation.WithdrawalCommandId.HasValue)
                return Fail(operation, operation.LastFailureCode ?? "LSKU_IDENTITY_WITHDRAWAL_REFUSED", 409);
            if (operation.NextAttemptAtUtcTicksV1 > timeProvider.GetUtcNow().UtcTicks)
                return Fail(operation, operation.LastFailureCode ?? "LSKU_IDENTITY_WORKFLOW_RETRY_SCHEDULED", 503);
        }
        return Fail(operation, "LSKU_IDENTITY_WORKFLOW_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<LskuIdentityWorkflowProcessingResult> ProcessAsync(
        LskuIdentityWorkflowOperation initial,
        string? delegatedUserToken,
        string leaseOwner,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        bool stopWhenAwaitingDecision,
        CancellationToken cancellationToken)
    {
        var operation = initial;
        var manualResumeAttempted = false;
        // Only a REPEAT reconciles: an operation that became stuck in this very request is answered, not re-read
        // (the GSKU identity rule) — the engine may still be on the start this request sent.
        var startedStuck = initial.Checkpoint == LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired;
        for (var step = 0; step < 12; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation.Checkpoint == LskuIdentityWorkflowCheckpoint.Completed)
            {
                return Success(operation, true);
            }
            // LSKU FIX1 B3 / B5: an operation that ended with its LSKU a draft is final here; the submit handler says
            // why to the maker (a stable code + the recorded reason); the resubmit is a new operation.
            if (operation.Checkpoint is LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart
                or LskuIdentityWorkflowCheckpoint.Superseded
                or LskuIdentityWorkflowCheckpoint.DecisionNotApplicable)
            {
                return Success(operation, true);
            }
            if (stopWhenAwaitingDecision
                && operation.Checkpoint == LskuIdentityWorkflowCheckpoint.AwaitingDecision)
            {
                return Success(operation, false);
            }
            if (operation.Checkpoint == LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
                && string.IsNullOrWhiteSpace(delegatedUserToken))
            {
                return Fail(operation, "LSKU_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED", 409);
            }
            if (operation.Checkpoint == LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired)
            {
                // LSKU FIX1 B5: a STUCK operation's way out is its requester's same command (it reads the engine
                // again); the worker never touches it, and a second pass in the same request is a person's (BL-530).
                if (manualResumeAttempted || !startedStuck || string.IsNullOrWhiteSpace(delegatedUserToken)
                    || operation.RecoveryDisposition != ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
                {
                    return Fail(operation, manualResumeAttempted ? ManualReconciliationNeeded
                        : operation.LastFailureCode ?? "LSKU_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED", 409);
                }
            }
            var counted = string.IsNullOrWhiteSpace(delegatedUserToken);

            var now = timeProvider.GetUtcNow();
            var claim = await operations.TryClaimAsync(
                new(
                    operation.OperationId,
                    operation.OperationFingerprint,
                    [operation.Checkpoint],
                    leaseOwner,
                    now.UtcTicks,
                    now.Add(leaseDuration).UtcTicks),
                cancellationToken);
            if (claim is null)
            {
                return Fail(operation, "LSKU_IDENTITY_WORKFLOW_BUSY", 409);
            }
            // FIX3 item 4: decide on the document as it is UNDER the claim (see ProcessWithdrawalAsync).
            if (await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken) is not { } underClaim
                || underClaim.Checkpoint != claim.Checkpoint)
            {
                return Fail(operation, "LSKU_IDENTITY_WORKFLOW_BUSY", 409);
            }
            operation = underClaim;

            var advanced = operation.Checkpoint switch
            {
                LskuIdentityWorkflowCheckpoint.Prepared =>
                    await AdvanceAsync(claim, LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                        ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true,
                        cancellationToken: cancellationToken),
                LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown or
                    LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay =>
                    await ResolveStartAsync(operation, claim, delegatedUserToken, now, retryDelay, cancellationToken),
                LskuIdentityWorkflowCheckpoint.WorkflowStarted =>
                    await ApplyLocalPendingAsync(operation, claim, now, retryDelay, counted, cancellationToken),
                LskuIdentityWorkflowCheckpoint.LocalPendingApplied =>
                    await AdvanceAsync(claim, LskuIdentityWorkflowCheckpoint.AwaitingDecision,
                        ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true,
                        cancellationToken: cancellationToken),
                LskuIdentityWorkflowCheckpoint.AwaitingDecision =>
                    await ObserveDecisionAsync(operation, claim, now, retryDelay, counted, cancellationToken),
                LskuIdentityWorkflowCheckpoint.DecisionObserved =>
                    await ValidateApprovalOrApplyRejectAsync(
                        operation, claim, now, retryDelay, cancellationToken),
                LskuIdentityWorkflowCheckpoint.ApprovalValidated =>
                    await ApplyDecisionAsync(operation, claim, now, retryDelay, counted, cancellationToken),
                LskuIdentityWorkflowCheckpoint.DecisionApplied =>
                    await VerifyDecisionAppliedAndCompleteAsync(
                        operation, claim, now, retryDelay, cancellationToken),
                LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired =>
                    await ReconcileAsync(operation, claim, now, cancellationToken),
                _ => false
            };
            if (!advanced)
            {
                return Fail(operation, "LSKU_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT", 409);
            }

            if (operation.Checkpoint == LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired)
            {
                manualResumeAttempted = true;
            }

            operation = await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken)
                ?? throw new InvalidOperationException("LSKU_IDENTITY_WORKFLOW_OPERATION_LOST");
            // FIX2 (the GSKU identity rule, FirstGskuIdentityWorkflowProcessor.MakerAnswer): a start the engine sent
            // back to the maker who is present is answered now — the refused call is not repeated in this request.
            if (operation.Checkpoint == LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
                && !string.IsNullOrWhiteSpace(delegatedUserToken))
            {
                var code = ProductIdentityWorkflowMakerCodes.IsMakerCode(operation.LastFailureCode)
                    ? operation.LastFailureCode!
                    : ProductIdentityWorkflowMakerCodes.MakerReplayRequired;
                return Fail(operation, code, ProductIdentityWorkflowMakerCodes.StatusFor(code));
            }
            if (operation.NextAttemptAtUtcTicksV1 > timeProvider.GetUtcNow().UtcTicks)
            {
                return Fail(operation, operation.LastFailureCode ?? "LSKU_IDENTITY_WORKFLOW_RETRY_SCHEDULED", 503);
            }
        }

        return Fail(operation, "LSKU_IDENTITY_WORKFLOW_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<bool> ResolveStartAsync(
        LskuIdentityWorkflowOperation operation,
        LskuIdentityWorkflowClaim claim,
        string? delegatedUserToken,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult> outcome;
        var startMayBeSent = !string.IsNullOrWhiteSpace(delegatedUserToken);
        if (startMayBeSent)
        {
            // LSKU FIX2 item 8: a start goes only to the draft it was reserved for, and only while that draft carries
            // this submit's fence — taken atomically, so a draft cancel can no longer slip between the check and the
            // send (the cancel is refused while the fence is held). Without the fence: if nothing was ever sent, the
            // engine holds nothing — the submit ends; if a start was sent, it is not sent again — the engine is asked.
            var fence = await lskus.FenceIdentitySubmitAsync(operation.LskuId, operation.ExpectedLskuVersion,
                operation.OperationId, cancellationToken);
            if (!fence.Succeeded)
            {
                if (NeverSent(operation))
                    return await AdvanceAsync(claim, LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart,
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
                // FIX3 item 4: the copy this step decides on carries the mark it just wrote (an end written after the
                // send compares it — AbandonBeforeStartAsync).
                operation.LastStartSentAtUtcTicksV1 = now.UtcTicks;
            }
        }
        if (startMayBeSent)
        {
            outcome = await workflowClient.StartAsync(
                operation.TenantId,
                requestFactory.Rehydrate(operation),
                delegatedUserToken,
                cancellationToken);
            if (outcome.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
            {
                return await PersistStartedAsync(operation, claim, outcome.Value, now, cancellationToken);
            }
            if (outcome.Outcome != ProductIdentityWorkflowTransportOutcome.Incomplete)
            {
                // LSKU FIX1 B3 — the GSKU identity rule: the shared classification of the START's answer.
                var fault = ProductIdentityWorkflowFaultPolicy.Classify(
                    ProductIdentityWorkflowCall.Start, outcome.Outcome, outcome.ErrorCode);
                switch (fault.Kind)
                {
                    case ProductIdentityWorkflowFaultKind.Quarantine when RefusedBeforeProcessing(outcome.Outcome, outcome.ErrorCode):
                        // Refused before anything was processed: nothing exists at the engine. The submit ends, the
                        // LSKU stays a draft (its version moves), and the maker can submit again.
                        return await AbandonAsync(operation, claim, BoundedCode(outcome.ErrorCode, StartRefused), now,
                            cancellationToken);
                    case ProductIdentityWorkflowFaultKind.Quarantine:
                        return await QuarantineAsync(claim, outcome.ErrorCode, now, cancellationToken);
                    case ProductIdentityWorkflowFaultKind.MakerReplay:
                        return await AdvanceAsync(claim, LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                            ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, now,
                            BoundedCode(fault.MakerCode, "LSKU_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED"),
                            releaseLease: true, cancellationToken: cancellationToken);
                    case ProductIdentityWorkflowFaultKind.ServiceWait when fault.StartedNothing:
                        return await AdvanceAsync(claim, LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                            ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, now,
                            ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit,
                            releaseLease: true, cancellationToken: cancellationToken);
                }
                // No answer, or one nobody could accept: the start may have arrived — ask.
            }
        }

        var lookup = await workflowClient.GetStartResultAsync(
            operation.TenantId,
            new(operation.ObjectType, operation.ObjectId, operation.MakerSubjectId,
                operation.StartIdempotencyKey),
            cancellationToken);
        if (lookup.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
        {
            return await PersistStartedAsync(operation, claim, lookup.Value, now, cancellationToken);
        }

        if (lookup.Outcome is ProductIdentityWorkflowTransportOutcome.NotFound
            or ProductIdentityWorkflowTransportOutcome.Incomplete)
        {
            return await AdvanceAsync(
                claim,
                LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay,
                now,
                lookup.ErrorCode ?? "LSKU_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED",
                releaseLease: true,
                cancellationToken: cancellationToken);
        }

        if (Retryable(lookup.Outcome) || ProductIdentityWorkflowFaultPolicy.Classify(
                ProductIdentityWorkflowCall.StartResult, lookup.Outcome, lookup.ErrorCode).Kind
            == ProductIdentityWorkflowFaultKind.ServiceWait)
        {
            return await ScheduleRetryAsync(
                operation, claim, LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                lookup.ErrorCode, now, retryDelay, string.IsNullOrWhiteSpace(delegatedUserToken), cancellationToken);
        }

        // The LOOKUP's refusal says nothing about what the start did: stuck, the requester's repeat reads again.
        return await QuarantineAsync(claim, lookup.ErrorCode, now, cancellationToken);
    }

    private static bool InWithdrawal(LskuIdentityWorkflowOperation operation) =>
        operation.WithdrawalCommandId.HasValue && operation.Checkpoint is
            LskuIdentityWorkflowCheckpoint.WithdrawalRequested
            or LskuIdentityWorkflowCheckpoint.WithdrawalPreflightObserved
            or LskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown
            or LskuIdentityWorkflowCheckpoint.WithdrawalObserved
            or LskuIdentityWorkflowCheckpoint.WithdrawalApplied
            or LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay;

    public const string WithdrawalUnderWay = "LSKU_IDENTITY_WITHDRAWAL_UNDER_WAY";

    public const string ClosedByDraftCancel = "LSKU_IDENTITY_SUBMIT_CLOSED_BY_DRAFT_CANCEL";

    /*
     * LSKU FIX2 item 10: a draft cancel meets a submit waiting for its maker's replay. If the engine is KNOWN to hold
     * nothing for it — no start was ever sent, or the engine has nothing under its key after the settle window — the
     * cancel closes it (a named end; its fence goes). Otherwise the cancel is refused: a start that may have arrived
     * is never closed behind the engine's back (principle A).
     * FIX3 items 3 / 4 / 15: the decision is made on the document UNDER the claim (the maker's replay may have sent a
     * start since the cancel first read it), and the closure is ONE atomic write — the submit abandoned with its own
     * audit intent, the draft's version moved, its fence released — guarded by the start-sent mark it was decided on
     * (AbandonBeforeStartAsync compares it). The caller then cancels at the moved version.
     */
    public async Task<(bool Closed, string? Code, int Status)> CloseForDraftCancelAsync(
        LskuIdentityWorkflowOperation operation, string leaseOwner, TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (operation.Checkpoint != LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay)
            return (false, "LSKU_SUBMIT_IN_FLIGHT", 409);
        var now = timeProvider.GetUtcNow();
        var claim = await operations.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [operation.Checkpoint], leaseOwner, now.UtcTicks, now.Add(leaseDuration).UtcTicks), cancellationToken);
        if (claim is null) return (false, "LSKU_IDENTITY_WORKFLOW_BUSY", 409);
        if (await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken) is not { } fresh
            || fresh.Checkpoint != LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
            || InWithdrawal(fresh))
            return (false, "LSKU_SUBMIT_IN_FLIGHT", 409);
        if (!NeverSent(fresh))
        {
            if (Settling(fresh, now)) return await KeepForMakerAsync(fresh, claim, now, "LSKU_SUBMIT_IN_FLIGHT", 409, cancellationToken);
            var lookup = await workflowClient.GetStartResultAsync(fresh.TenantId,
                new(fresh.ObjectType, fresh.ObjectId, fresh.MakerSubjectId, fresh.StartIdempotencyKey),
                cancellationToken);
            if (lookup.Outcome != ProductIdentityWorkflowTransportOutcome.NotFound)
                return await KeepForMakerAsync(fresh, claim, now,
                    lookup.Outcome == ProductIdentityWorkflowTransportOutcome.Success
                        ? "LSKU_SUBMIT_IN_FLIGHT" : ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit,
                    lookup.Outcome == ProductIdentityWorkflowTransportOutcome.Success ? 409 : 503, cancellationToken);
        }
        var lsku = await lskus.GetByIdAsync(fresh.LskuId, cancellationToken);
        if (lsku is null) return await KeepForMakerAsync(fresh, claim, now, "LSKU_NOT_FOUND", 404, cancellationToken);
        var audit = LskuIdentityLifecycleAuditIntentFactory.CreateSubmitAbandoned(lsku, fresh.ExpectedLskuVersion,
            fresh.OperationId, fresh.MakerSubjectId, ClosedByDraftCancel, now);
        var written = await operations.AbandonBeforeStartAsync(claim, fresh, ClosedByDraftCancel, audit, now.UtcTicks,
            cancellationToken);
        if (!written.Succeeded)
            return await KeepForMakerAsync(fresh, claim, now, written.ErrorCode ?? "LSKU_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT",
                written.ErrorCode == "AUDIT_INTENT_CAPACITY_REACHED" ? 503 : 409, cancellationToken);
        log.LogInformation("LSKU identity operation {OperationId} of tenant {TenantId} was closed by its draft's cancel ({ResultCode}).",
            fresh.OperationId, fresh.TenantId, ClosedByDraftCancel);
        return (true, null, 200);
    }

    // The cancel did not close the submit: it stays parked for its maker, unchanged, and the lease goes back now.
    private async Task<(bool Closed, string? Code, int Status)> KeepForMakerAsync(LskuIdentityWorkflowOperation operation,
        LskuIdentityWorkflowClaim claim, DateTimeOffset now, string code, int status, CancellationToken cancellationToken)
    {
        await operations.AdvanceAsync(claim, new(operation.Checkpoint, operation.RecoveryDisposition, now.UtcTicks,
            NextAttemptAtUtcTicksV1: operation.NextAttemptAtUtcTicksV1, LastFailureCode: operation.LastFailureCode,
            ReleaseLease: true, Streak: RecoveryStreakWrite.Keep), cancellationToken);
        return (false, code, status);
    }

    private static bool RefusedBeforeProcessing(ProductIdentityWorkflowTransportOutcome outcome, string? code) =>
        outcome == ProductIdentityWorkflowTransportOutcome.Invalid
            && code is not (ProductIdentityWorkflowFaultPolicy.ResponseUnreadable or ProductIdentityWorkflowFaultPolicy.ResponseInvalid)
        || outcome == ProductIdentityWorkflowTransportOutcome.Forbidden;

    // LSKU FIX1 B3: the submit ends before anything started; the draft's version moves in the same transaction so the
    // maker's resubmit (the Web derives the operation id from the version) is a new operation.
    private async Task<bool> AbandonAsync(LskuIdentityWorkflowOperation operation, LskuIdentityWorkflowClaim claim,
        string code, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var lsku = await lskus.GetByIdAsync(operation.LskuId, cancellationToken);
        if (lsku is null) return await QuarantineAsync(claim, "LSKU_IDENTITY_NOT_FOUND", now, cancellationToken);
        var audit = LskuIdentityLifecycleAuditIntentFactory.CreateSubmitAbandoned(lsku, operation.ExpectedLskuVersion,
            operation.OperationId, operation.MakerSubjectId, code, now);
        var written = await operations.AbandonBeforeStartAsync(claim, operation, code, audit, now.UtcTicks,
            cancellationToken);
        if (written.Succeeded)
        {
            log.LogWarning("LSKU identity operation {OperationId} of tenant {TenantId} ended before the approval engine started anything ({ResultCode}); the draft can be submitted again.",
                operation.OperationId, operation.TenantId, code);
            return true;
        }
        return await WriteRefusedAsync(operation, claim, written.ErrorCode, now, TimeSpan.FromSeconds(30),
            counted: true, cancellationToken);
    }

    // LSKU FIX1 B5: the engine decided and MDM cannot apply it — the LSKU goes back to Draft with the reason.
    private async Task<bool> DecisionNotApplicableAsync(LskuIdentityWorkflowOperation operation,
        LskuIdentityWorkflowClaim claim, string code, DateTimeOffset now, TimeSpan retryDelay, bool counted,
        CancellationToken cancellationToken)
    {
        var lsku = await lskus.GetByIdAsync(operation.LskuId, cancellationToken);
        if (lsku is null) return await QuarantineAsync(claim, "LSKU_IDENTITY_NOT_FOUND", now, cancellationToken);
        var reason = BoundedCode(code, DecisionNotApplicableCode);
        var audit = LskuIdentityLifecycleAuditIntentFactory.CreateDecisionNotApplicable(lsku,
            checked(operation.ExpectedLskuVersion + 1), operation.OperationId, operation.MakerSubjectId, reason, now);
        var written = await operations.ApplyDecisionNotApplicableAsync(claim, operation, reason, audit, now.UtcTicks,
            cancellationToken);
        if (written.Succeeded)
        {
            log.LogWarning("LSKU identity operation {OperationId} of tenant {TenantId}: the engine's decision cannot be applied ({ResultCode}); the LSKU is a draft again.",
                operation.OperationId, operation.TenantId, reason);
            return true;
        }
        return await WriteRefusedAsync(operation, claim, written.ErrorCode, now, retryDelay, counted, cancellationToken);
    }

    /*
     * LSKU FIX1 B5 — the requester's same command on a STUCK operation reads where things stand and puts the
     * operation back; the ordinary steps take it from there:
     *   a contract mismatch on an approval (the earlier rule)   → decision observed (its validation is tried again);
     *   no start on record                                       → the engine's start lookup: found → started,
     *                                                              nothing under the key → abandoned (draft again);
     *   the LSKU still pending on this approval                  → awaiting the decision (the recorded one is read again);
     *   the LSKU still a draft with this start's approval        → started (the pending write is made again);
     *   the LSKU already shows this operation's decision         → decision applied (verified, completed);
     *   anything else                                            → stays, 409 "a person" (BL-530).
     */
    private async Task<bool> ReconcileAsync(LskuIdentityWorkflowOperation operation, LskuIdentityWorkflowClaim claim,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (CanResumeApprovalContract(operation, "requester"))
            return await AdvanceAsync(claim, LskuIdentityWorkflowCheckpoint.DecisionObserved,
                ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true,
                cancellationToken: cancellationToken);
        if (!operation.WorkflowInstanceId.HasValue)
        {
            var lookup = await workflowClient.GetStartResultAsync(operation.TenantId,
                new(operation.ObjectType, operation.ObjectId, operation.MakerSubjectId, operation.StartIdempotencyKey),
                cancellationToken);
            if (lookup.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
                return await PersistStartedAsync(operation, claim, lookup.Value, now, cancellationToken);
            // LSKU FIX2 item 10: "nothing under this key" is trusted only after the settle window (the retirement rule).
            if (lookup.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound && !Settling(operation, now))
                return await AbandonAsync(operation, claim, ReconciledNothingStarted, now, cancellationToken);
            return await StayStuckAsync(operation, claim, now, cancellationToken);
        }
        var lsku = await lskus.GetByIdAsync(operation.LskuId, cancellationToken);
        var binding = lsku?.IdentityWorkflowBinding;
        var ours = binding is not null && binding.WorkflowInstanceId == operation.WorkflowInstanceId
            && binding.StartIdempotencyKey == operation.StartIdempotencyKey;
        if (lsku is not null && ours && lsku.LifecycleStatus == ProductIdentityLifecycleStatus.PendingIdentityApproval
            && binding!.TerminalDecision is null)
            return await operations.AdvanceAsync(claim, new(LskuIdentityWorkflowCheckpoint.AwaitingDecision,
                ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks, ReleaseLease: true, ClearDecision: true),
                cancellationToken);
        if (lsku is not null && !ours && lsku.LifecycleStatus == ProductIdentityLifecycleStatus.Draft
            && lsku.Version == operation.ExpectedLskuVersion && CompleteStartProof(operation))
            return await operations.AdvanceAsync(claim, new(LskuIdentityWorkflowCheckpoint.WorkflowStarted,
                ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
                WorkflowInstanceId: operation.WorkflowInstanceId, WorkflowTemplateId: operation.WorkflowTemplateId,
                WorkflowTemplateVersionId: operation.WorkflowTemplateVersionId, ApprovalTaskId: operation.ApprovalTaskId,
                AssignmentSnapshotId: operation.AssignmentSnapshotId, StartTransitionLogId: operation.StartTransitionLogId,
                WorkflowStartedAtUtcTicksV1: operation.WorkflowStartedAtUtcTicksV1,
                ReleaseLease: true, ClearDecision: true), cancellationToken);
        if (lsku is not null && ours && binding!.TerminalDecision is { } decided
            && decided.WorkflowInstanceId == operation.WorkflowInstanceId
            && TryRehydrateEvidence(operation, out var recorded) && SameRecordedDecision(decided, recorded))
            return await AdvanceAsync(claim, LskuIdentityWorkflowCheckpoint.DecisionApplied,
                ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true,
                cancellationToken: cancellationToken);
        return await StayStuckAsync(operation, claim, now, cancellationToken);
    }

    private static bool SameRecordedDecision(ProductIdentityWorkflowDecisionEvidence stored,
        ProductIdentityWorkflowDecisionEvidence recorded) =>
        stored.Decision == recorded.Decision && stored.ApprovalTaskId == recorded.ApprovalTaskId
        && stored.TransitionSequence == recorded.TransitionSequence
        && stored.DecisionActorSubjectId == recorded.DecisionActorSubjectId;

    private Task<bool> StayStuckAsync(LskuIdentityWorkflowOperation operation, LskuIdentityWorkflowClaim claim,
        DateTimeOffset now, CancellationToken cancellationToken) =>
        operations.AdvanceAsync(claim, new(operation.Checkpoint, operation.RecoveryDisposition, now.UtcTicks,
            LastFailureCode: operation.LastFailureCode, ReleaseLease: true, Streak: RecoveryStreakWrite.Keep),
            cancellationToken);

    private Task<bool> PersistStartedAsync(
        LskuIdentityWorkflowOperation operation,
        LskuIdentityWorkflowClaim claim,
        ProductIdentityWorkflowStartResult? result,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (result is null || result.WorkflowInstanceId == Guid.Empty || result.TemplateId == Guid.Empty
            || result.TemplateVersionId == Guid.Empty || result.ApprovalTaskId == Guid.Empty
            || result.AssignmentSnapshotId == Guid.Empty || result.StartTransitionLogId == Guid.Empty
            || result.StartedAt is not { } startedAt || startedAt == default
            || startedAt.Offset != TimeSpan.Zero
            || !string.Equals(result.ObjectRef, operation.ObjectRef, StringComparison.Ordinal)
            || operation.WorkflowTemplateId.HasValue && result.TemplateId != operation.WorkflowTemplateId.Value)
        {
            return QuarantineAsync(claim, "LSKU_IDENTITY_WORKFLOW_START_PROOF_INVALID", now, cancellationToken);
        }

        return operations.AdvanceAsync(
            claim,
            new(
                LskuIdentityWorkflowCheckpoint.WorkflowStarted,
                ProductIdentityWorkflowRecoveryDisposition.None,
                now.UtcTicks,
                WorkflowInstanceId: result.WorkflowInstanceId,
                WorkflowTemplateId: result.TemplateId,
                WorkflowTemplateVersionId: result.TemplateVersionId,
                ApprovalTaskId: result.ApprovalTaskId,
                AssignmentSnapshotId: result.AssignmentSnapshotId,
                StartTransitionLogId: result.StartTransitionLogId,
                WorkflowStartedAtUtcTicksV1: startedAt.UtcTicks,
                ReleaseLease: true),
            cancellationToken);
    }

    private async Task<bool> ApplyLocalPendingAsync(
        LskuIdentityWorkflowOperation operation,
        LskuIdentityWorkflowClaim claim,
        DateTimeOffset now,
        TimeSpan retryDelay,
        bool counted,
        CancellationToken cancellationToken)
    {
        var lsku = await lskus.GetByIdAsync(operation.LskuId, cancellationToken);
        if (lsku is null)
        {
            return await QuarantineAsync(claim, "LSKU_IDENTITY_NOT_FOUND", now, cancellationToken);
        }
        if (!CompleteStartProof(operation))
        {
            return await QuarantineAsync(claim, "LSKU_IDENTITY_WORKFLOW_START_PROOF_INVALID", now, cancellationToken);
        }

        var submittedAt = new DateTimeOffset(operation.WorkflowStartedAtUtcTicksV1!.Value, TimeSpan.Zero);
        var binding = new ProductIdentityWorkflowBinding
        {
            WorkflowInstanceId = operation.WorkflowInstanceId!.Value,
            WorkflowTemplateId = operation.WorkflowTemplateId!.Value,
            WorkflowTemplateVersionId = operation.WorkflowTemplateVersionId!.Value,
            ApprovalTaskId = operation.ApprovalTaskId!.Value,
            AssignmentSnapshotId = operation.AssignmentSnapshotId!.Value,
            StartTransitionLogId = operation.StartTransitionLogId!.Value,
            ObjectType = operation.ObjectType,
            ObjectId = operation.LskuId,
            ObjectRef = operation.ObjectRef,
            SubmitterSubjectId = operation.MakerSubjectId,
            StartIdempotencyKey = operation.StartIdempotencyKey,
            StartRequestFingerprint = operation.OperationFingerprint,
            SubmittedAtUtc = submittedAt,
            DueAtUtc = operation.DueAtUtcTicksV1.HasValue
                ? new DateTimeOffset(operation.DueAtUtcTicksV1.Value, TimeSpan.Zero)
                : null
        };
        var audit = LskuIdentityLifecycleAuditIntentFactory.CreateSubmit(
            lsku, operation.ExpectedLskuVersion, binding);
        var result = await lskus.SubmitIdentityAsync(
            operation.LskuId,
            operation.ExpectedLskuVersion,
            binding,
            audit,
            cancellationToken);
        if (!result.Succeeded)
        {
            return await WriteRefusedAsync(operation, claim, result.ErrorCode, now, retryDelay, counted,
                cancellationToken);
        }
        if (result.Lsku is null
            || result.Lsku.Version != operation.ExpectedLskuVersion + 1
            || result.Lsku.LifecycleStatus != ProductIdentityLifecycleStatus.PendingIdentityApproval
            || !ExactBinding(result.Lsku.IdentityWorkflowBinding, binding)
            || !ExactAuditResult(result.Lsku, audit))
        {
            return await QuarantineAsync(claim,
                "LSKU_IDENTITY_WORKFLOW_LOCAL_PENDING_INCONSISTENT", now, cancellationToken);
        }

        return await AdvanceAsync(
            claim,
            LskuIdentityWorkflowCheckpoint.LocalPendingApplied,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now,
            releaseLease: true,
            cancellationToken: cancellationToken);
    }

    private async Task<bool> ObserveDecisionAsync(
        LskuIdentityWorkflowOperation operation,
        LskuIdentityWorkflowClaim claim,
        DateTimeOffset now,
        TimeSpan retryDelay,
        bool counted,
        CancellationToken cancellationToken)
    {
        if (!operation.WorkflowInstanceId.HasValue)
        {
            return await QuarantineAsync(claim, "LSKU_IDENTITY_WORKFLOW_START_PROOF_INVALID", now, cancellationToken);
        }

        var result = await workflowClient.GetTerminalEvidenceAsync(
            operation.TenantId,
            new(operation.WorkflowInstanceId.Value, operation.ObjectType, operation.ObjectId),
            cancellationToken);
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.NonTerminal
            or ProductIdentityWorkflowTransportOutcome.NotFound
            or ProductIdentityWorkflowTransportOutcome.Incomplete
            || Retryable(result.Outcome)
            || result.Outcome != ProductIdentityWorkflowTransportOutcome.Success
            && ProductIdentityWorkflowFaultPolicy.Classify(ProductIdentityWorkflowCall.TerminalEvidence,
                result.Outcome, result.ErrorCode).Kind == ProductIdentityWorkflowFaultKind.ServiceWait)
        {
            return await ScheduleRetryAsync(
                operation,
                claim,
                LskuIdentityWorkflowCheckpoint.AwaitingDecision,
                result.ErrorCode ?? "LSKU_IDENTITY_WORKFLOW_DECISION_PENDING",
                now,
                retryDelay,
                counted,
                cancellationToken);
        }
        // BL-547: CLOSED_WITHOUT_DECISION — the approval ended with no decision: the submit is not applicable and the
        // LSKU is a draft again (formerly: quarantined, the LSKU pending for good).
        if (ProductIdentityWorkflowFaultPolicy.IsClosedWithoutDecision(result))
        {
            return await DecisionNotApplicableAsync(operation, claim, ClosedWithoutDecision, now, retryDelay,
                counted: true, cancellationToken);
        }
        if (result.Outcome != ProductIdentityWorkflowTransportOutcome.Success || result.Value is null)
        {
            // TERMINAL_EVIDENCE_INCONSISTENT or a refusal about the record: stuck, the requester's repeat reads again.
            return await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
        }

        var evidence = result.Value;
        if (Guid.TryParse(evidence.ActorUserId, out var self) && self == operation.MakerSubjectId)
        {
            // B5 — the GSKU identity rule: the maker decided their own submit and the engine has closed it; no other
            // decision will come. Not applied: the LSKU is a draft again, the reason is kept.
            log.LogWarning("LSKU identity operation {OperationId} of tenant {TenantId} was decided by its own maker; the decision is not applied ({ResultCode}).",
                operation.OperationId, operation.TenantId, SelfApprovalIgnored);
            return await DecisionNotApplicableAsync(operation, claim, SelfApprovalIgnored, now, retryDelay,
                counted: true, cancellationToken);
        }
        if (!TryMapDecision(evidence.TerminalAction, out var decision)
            || !ExactTerminalStatuses(decision, evidence.TaskStatus, evidence.InstanceStatus)
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
                && (string.IsNullOrWhiteSpace(evidence.ReasonCode)
                    || evidence.ReasonCode.Length > 128
                    || evidence.ReasonCode.Any(char.IsControl)
                    || evidence.ReasonCode != evidence.ReasonCode.Trim()))
        {
            return await QuarantineAsync(claim, "LSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        }

        return await operations.AdvanceAsync(
            claim,
            new(
                LskuIdentityWorkflowCheckpoint.DecisionObserved,
                ProductIdentityWorkflowRecoveryDisposition.None,
                now.UtcTicks,
                DecisionKind: decision,
                DecisionObservedAtUtcTicksV1: now.UtcTicks,
                DecisionActorSubjectId: actorId,
                DecisionReasonCode: evidence.ReasonCode,
                DecisionObjectType: evidence.ObjectType,
                DecisionObjectId: evidence.ObjectId,
                DecisionObjectRef: evidence.ObjectRef,
                DecisionWorkflowTemplateId: evidence.TemplateId,
                DecisionWorkflowTemplateVersionId: evidence.TemplateVersionId,
                DecisionTaskStatus: evidence.TaskStatus,
                DecisionInstanceStatus: evidence.InstanceStatus,
                DecisionTransitionSequence: evidence.TransitionSequence,
                DecisionAtUtcTicksV1: evidence.DecisionAt.UtcTicks,
                ReleaseLease: true),
            cancellationToken);
    }

    private async Task<bool> ApplyDecisionAsync(
        LskuIdentityWorkflowOperation operation,
        LskuIdentityWorkflowClaim claim,
        DateTimeOffset now,
        TimeSpan retryDelay,
        bool counted,
        CancellationToken cancellationToken)
    {
        if (!TryRehydrateEvidence(operation, out var evidence))
        {
            return await QuarantineAsync(claim, "LSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        }
        if (evidence.Decision == ProductIdentityDecisionKind.Approved)
        {
            if (!ExactApprovalProof(operation))
            {
                return await QuarantineAsync(
                    claim, "LSKU_IDENTITY_WORKFLOW_APPROVAL_PROOF_INVALID", now, cancellationToken);
            }
            var validation = await ValidateApprovalDependenciesAsync(operation, cancellationToken);
            if (!validation.Succeeded)
            {
                return await ApprovalNotValidAsync(operation, claim, validation, now, retryDelay, counted,
                    cancellationToken);
            }
        }
        var lsku = await lskus.GetByIdAsync(operation.LskuId, cancellationToken);
        if (lsku is null)
        {
            return await QuarantineAsync(claim, "LSKU_IDENTITY_NOT_FOUND", now, cancellationToken);
        }

        var expectedVersion = checked(operation.ExpectedLskuVersion + 1);
        var audit = LskuIdentityLifecycleAuditIntentFactory.CreateDecision(lsku, expectedVersion, evidence);
        var result = await lskus.ReconcileIdentityDecisionAsync(
            operation.LskuId, expectedVersion, evidence, audit, cancellationToken);
        if (!result.Succeeded)
        {
            return await WriteRefusedAsync(operation, claim, result.ErrorCode, now, retryDelay, counted,
                cancellationToken);
        }
        if (!ExactDecisionResult(result.Lsku, operation, evidence))
        {
            return await QuarantineAsync(claim,
                "LSKU_IDENTITY_WORKFLOW_DECISION_INCONSISTENT", now, cancellationToken);
        }

        return await AdvanceAsync(
            claim,
            LskuIdentityWorkflowCheckpoint.DecisionApplied,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now,
            releaseLease: true,
            cancellationToken: cancellationToken);
    }

    private async Task<bool> VerifyDecisionAppliedAndCompleteAsync(
        LskuIdentityWorkflowOperation operation,
        LskuIdentityWorkflowClaim claim,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!TryRehydrateEvidence(operation, out var evidence))
        {
            return await QuarantineAsync(claim,
                "LSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        }
        // LSKU FIX1 B5: the decision is already on the LSKU — what it depended on is not asked again here (a market
        // retired since then would otherwise freeze an applied decision for good); only the result is verified.
        if (evidence.Decision == ProductIdentityDecisionKind.Approved && !ExactApprovalProof(operation))
        {
            return await QuarantineAsync(
                claim, "LSKU_IDENTITY_WORKFLOW_APPROVAL_PROOF_INVALID", now, cancellationToken);
        }
        var lsku = await lskus.GetByIdAsync(operation.LskuId, cancellationToken);
        if (!ExactDecisionResult(lsku, operation, evidence))
        {
            return await QuarantineAsync(claim,
                "LSKU_IDENTITY_WORKFLOW_DECISION_INCONSISTENT", now, cancellationToken);
        }
        return await AdvanceAsync(claim, LskuIdentityWorkflowCheckpoint.Completed,
            ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true,
            cancellationToken: cancellationToken);
    }

    private async Task<bool> ValidateApprovalOrApplyRejectAsync(
        LskuIdentityWorkflowOperation operation,
        LskuIdentityWorkflowClaim claim,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!TryRehydrateEvidence(operation, out var evidence))
        {
            return await QuarantineAsync(
                claim, "LSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        }

        // Rejection is deliberately provider- and parent-independent.
        if (evidence.Decision == ProductIdentityDecisionKind.Rejected)
        {
            return await ApplyDecisionAsync(operation, claim, now, retryDelay, counted: true, cancellationToken);
        }

        var validation = await ValidateApprovalDependenciesAsync(operation, cancellationToken);
        if (!validation.Succeeded || validation.Selection is null)
        {
            return await ApprovalNotValidAsync(operation, claim, validation, now, retryDelay, counted: true,
                cancellationToken);
        }

        var proof = ApprovalProofFingerprint(
            operation.TenantId,
            operation.OperationId,
            operation.LskuId,
            operation.MarketCode,
            operation.DecisionTransitionSequence,
            validation.Selection,
            now.UtcTicks);
        return await operations.AdvanceAsync(
            claim,
            new(
                LskuIdentityWorkflowCheckpoint.ApprovalValidated,
                ProductIdentityWorkflowRecoveryDisposition.None,
                now.UtcTicks,
                ApprovalMarketSelection: validation.Selection,
                MarketValidatedAtUtcTicksV1: now.UtcTicks,
                ApprovalMarketProofFingerprint: proof,
                ReleaseLease: true),
            cancellationToken);
    }

    private async Task<ApprovalDependencyValidation> ValidateApprovalDependenciesAsync(
        LskuIdentityWorkflowOperation operation,
        CancellationToken cancellationToken)
    {
        var gsku = await gskus.GetByIdAsync(operation.GskuId, cancellationToken);
        if (gsku is null || gsku.IsDeleted || gsku.Id != operation.GskuId
            || gsku.ProductDefinitionRevisionId != operation.ProductDefinitionRevisionId
            || gsku.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved)
        {
            return new(false, false, "LSKU_IDENTITY_PARENT_NOT_REFERENCEABLE", MakerFixable: true);
        }
        var revision = await revisions.GetByIdAsync(operation.ProductDefinitionRevisionId, cancellationToken);
        if (revision is null || revision.IsDeleted || revision.Id != operation.ProductDefinitionRevisionId
            || revision.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved)
        {
            return new(false, false, "LSKU_IDENTITY_PARENT_NOT_REFERENCEABLE", MakerFixable: true);
        }

        var resolved = await marketResolver.ResolveLatestAsync(
            operation.TenantId, operation.MarketCode, cancellationToken);
        if (!resolved.IsSuccessful || resolved.Selection is null)
        {
            // LSKU FIX2 item 7 — the GSKU rule (FirstGskuIdentityWorkflowProcessor: 503/504 transient, 401/403 the
            // service's identity): the provider that cannot answer now, or refuses THIS SERVICE, waits — the
            // approver's decision is kept. Only a market the provider no longer offers (404) is the maker's to put
            // right (not applicable). Anything else (a contract mismatch of the provider) stays stuck with its cause.
            return new(
                false,
                resolved.StatusCode is 401 or 403 or 503 or 504,
                BoundedCode(resolved.FailureCode,
                    resolved.StatusCode == 504
                        ? "REFERENCE_PROVIDER_TIMEOUT"
                        : "REFERENCE_MARKET_NOT_FOUND"),
                // FIX3 item 6: only the provider's own "not offered" (its exact reason), not any 404.
                MakerFixable: resolved.StatusCode == 404 && resolved.FailureCode == "REFERENCE_MARKET_NOT_FOUND");
        }
        var value = resolved.Selection;
        if (value.SetCode != "market" || value.ValueCode != operation.MarketCode
            || value.CatalogVersionId == Guid.Empty || value.CatalogVersionNumber <= 0
            || value.ResolutionMode != "LATEST" || value.ResolvedAtUtc == default
            || value.ResolvedAtUtc.Offset != TimeSpan.Zero)
        {
            return new(false, false, "REFERENCE_MARKET_CONTRACT_CONFLICT");
        }
        return new(true, false, null, new ReferenceCatalogSelection
        {
            SetCode = value.SetCode,
            ValueCode = value.ValueCode,
            CatalogVersionId = value.CatalogVersionId,
            CatalogVersionNumber = value.CatalogVersionNumber,
            ResolutionMode = ReferenceCatalogResolutionMode.Latest,
            ResolvedAtUtc = value.ResolvedAtUtc
        });
    }

    /*
     * LSKU FIX1 B5 — the GSKU identity items 2(b) and 8: a provider that cannot answer now waits (never stuck); a
     * reference the maker can put right (the market retired or not served to the tenant, the parent no longer
     * referenceable) makes the engine's approval NOT APPLICABLE — the LSKU is a draft again with the reason, and the
     * maker submits again when it is put right (formerly: quarantined, the decision never applied). A contract mismatch
     * of the provider itself stays stuck; the requester's repeat validates it again (the earlier way out).
     */
    private Task<bool> ApprovalNotValidAsync(LskuIdentityWorkflowOperation operation, LskuIdentityWorkflowClaim claim,
        ApprovalDependencyValidation validation, DateTimeOffset now, TimeSpan retryDelay, bool counted,
        CancellationToken cancellationToken) =>
        validation.Retryable
            ? ScheduleRetryAsync(operation, claim, operation.Checkpoint, validation.ErrorCode, now, retryDelay, counted, cancellationToken)
            : validation.MakerFixable
                ? DecisionNotApplicableAsync(operation, claim, BoundedCode(validation.ErrorCode, DecisionNotApplicableCode),
                    now, retryDelay, counted, cancellationToken)
                : QuarantineAsync(claim, validation.ErrorCode, now, cancellationToken);

    // LSKU FIX1 B5 / S3 — the shared write-fault rule: the cap, the store and a lease held elsewhere wait (never
    // stuck); a concurrent write is retried and counted (stuck with its cause at the limit); anything else is stuck at
    // once and its requester's repeat is the way out (a person when that cannot settle it, BL-530).
    private Task<bool> WriteRefusedAsync(LskuIdentityWorkflowOperation operation, LskuIdentityWorkflowClaim claim,
        string? code, DateTimeOffset now, TimeSpan retryDelay, bool counted, CancellationToken cancellationToken)
    {
        var normalized = code switch
        {
            "AUDIT_INTENT_CAPACITY_EXCEEDED" => ProductIdentityWriteFaults.AuditCapacityReached,
            "LSKU_IDENTITY_VERSION_CONFLICT" => "LSKU_CONCURRENCY_CONFLICT",
            _ => code
        };
        // A stuck operation's write that has to wait stays stuck (the worker does not scan it); the repeat asks again.
        if (operation.Checkpoint == LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired)
            return QuarantineAsync(claim, normalized, now, cancellationToken);
        if (ProductIdentityWriteFaults.MayPassByItself(normalized))
            return ScheduleRetryAsync(operation, claim, operation.Checkpoint, normalized, now, retryDelay, counted, cancellationToken);
        if (!ProductIdentityWriteFaults.MetAConcurrentWrite(normalized))
            return QuarantineAsync(claim, normalized, now, cancellationToken);
        var (exhausted, _, next) = ProductIdentityRecoveryStreak.Next(
            counted ? operation.FailedAttemptCount : Math.Max(operation.FailedAttemptCount - 1, 0), now, retryDelay,
            ProductIdentityRecoveryStreak.DefaultQuarantineAfterFailedAttempts, null);
        if (counted && exhausted)
            return QuarantineAsync(claim, StuckOnRecordAnswer, now, cancellationToken);
        return operations.AdvanceAsync(claim, new(operation.Checkpoint,
            ProductIdentityWorkflowRecoveryDisposition.Retryable, now.UtcTicks, next,
            BoundedCode(normalized, "LSKU_CONCURRENCY_CONFLICT"), ReleaseLease: true,
            Streak: counted ? RecoveryStreakWrite.Increment : RecoveryStreakWrite.Keep), cancellationToken);
    }

    private Task<bool> ClosedBeforeWithdrawalAsync(LskuIdentityWorkflowOperation operation,
        LskuIdentityWorkflowClaim claim, string? code, DateTimeOffset now, CancellationToken cancellationToken)
    {
        log.LogInformation("LSKU identity operation {OperationId} of tenant {TenantId}: the withdrawal was refused by the engine ({ResultCode}); the approval stays and its decision is applied.",
            operation.OperationId, operation.TenantId, code ?? "none");
        return operations.AdvanceAsync(claim, new(LskuIdentityWorkflowCheckpoint.AwaitingDecision,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
            LastFailureCode: BoundedCode(code, "LSKU_IDENTITY_WITHDRAWAL_REFUSED"), ReleaseLease: true,
            ClearWithdrawal: true), cancellationToken);
    }

    public const string RetryLimitReached = "LSKU_IDENTITY_WORKFLOW_RETRY_LIMIT_REACHED";

    /*
     * FIX3 item 2 (the retirement flow's RetryAsync): a wait is bounded and counted. Only the worker's attempts count
     * (an interactive repeat keeps the streak); the delay backs off to the ceiling. At the limit the operation STAYS
     * where it waits and Retryable — never "manual + retryable" — with RETRY_LIMIT_REACHED as its cause, so the
     * LSKU_IDENTITY gauge counts it (manual_reconciliation) and the error line is said once. A later success resets it.
     */
    private async Task<bool> ScheduleRetryAsync(
        LskuIdentityWorkflowOperation operation,
        LskuIdentityWorkflowClaim claim,
        LskuIdentityWorkflowCheckpoint checkpoint,
        string? errorCode,
        DateTimeOffset now,
        TimeSpan retryDelay,
        bool counted,
        CancellationToken cancellationToken,
        long? withdrawalUnansweredSince = null)
    {
        var (exhausted, _, next) = ProductIdentityRecoveryStreak.Next(
            counted ? operation.FailedAttemptCount : Math.Max(operation.FailedAttemptCount - 1, 0), now, retryDelay,
            ProductIdentityRecoveryStreak.DefaultQuarantineAfterFailedAttempts,
            ProductIdentityRecoveryStreak.DefaultMaxRetryDelay);
        var code = BoundedCode(errorCode, "LSKU_IDENTITY_WORKFLOW_PROVIDER_UNAVAILABLE");
        if (counted && exhausted)
        {
            if (operation.LastFailureCode != RetryLimitReached)
                log.LogError("LSKU identity operation {OperationId} of tenant {TenantId} has waited {Attempts} worker attempts at {Checkpoint} ({ResultCode}); it keeps waiting and is counted until the cause is removed.",
                    operation.OperationId, operation.TenantId, operation.FailedAttemptCount + 1, checkpoint, code);
            code = RetryLimitReached;
        }
        return await operations.AdvanceAsync(
            claim,
            new(
                checkpoint,
                ProductIdentityWorkflowRecoveryDisposition.Retryable,
                now.UtcTicks,
                next,
                code,
                ReleaseLease: true,
                Streak: counted ? RecoveryStreakWrite.Increment : RecoveryStreakWrite.Keep,
                WithdrawalUnansweredSinceUtcTicksV1: withdrawalUnansweredSince),
            cancellationToken);
    }

    private Task<bool> QuarantineAsync(
        LskuIdentityWorkflowClaim claim,
        string? errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        AdvanceAsync(
            claim,
            LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired,
            now,
            BoundedCode(errorCode, "LSKU_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED"),
            releaseLease: true,
            cancellationToken: cancellationToken);

    private Task<bool> AdvanceAsync(
        LskuIdentityWorkflowClaim claim,
        LskuIdentityWorkflowCheckpoint checkpoint,
        ProductIdentityWorkflowRecoveryDisposition disposition,
        DateTimeOffset now,
        string? errorCode = null,
        bool releaseLease = false,
        CancellationToken cancellationToken = default) =>
        operations.AdvanceAsync(
            claim,
            new(checkpoint, disposition, now.UtcTicks, LastFailureCode: errorCode,
                ReleaseLease: releaseLease),
            cancellationToken);

    private static bool CompleteStartProof(LskuIdentityWorkflowOperation operation) =>
        operation.WorkflowInstanceId is { } instanceId && instanceId != Guid.Empty
        && operation.WorkflowTemplateId is { } templateId && templateId != Guid.Empty
        && operation.WorkflowTemplateVersionId is { } versionId && versionId != Guid.Empty
        && operation.ApprovalTaskId is { } taskId && taskId != Guid.Empty
        && operation.AssignmentSnapshotId is { } snapshotId && snapshotId != Guid.Empty
        && operation.StartTransitionLogId is { } logId && logId != Guid.Empty
        && operation.WorkflowStartedAtUtcTicksV1 is > 0;

    private static bool CanResumeApprovalContract(
        LskuIdentityWorkflowOperation operation,
        string? delegatedUserToken) =>
        !string.IsNullOrWhiteSpace(delegatedUserToken)
        && operation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired
        && operation.LastFailureCode == "REFERENCE_CONTRACT_MISMATCH"
        && operation.DecisionKind == ProductIdentityDecisionKind.Approved
        && operation.ApprovalMarketSelection is null
        && !operation.MarketValidatedAtUtcTicksV1.HasValue
        && operation.ApprovalMarketProofFingerprint is null
        && CompleteStartProof(operation)
        && TryRehydrateEvidence(operation, out var evidence)
        && evidence.Decision == ProductIdentityDecisionKind.Approved
        && evidence.WorkflowInstanceId == operation.WorkflowInstanceId
        && evidence.ApprovalTaskId == operation.ApprovalTaskId
        && evidence.WorkflowTemplateId == operation.WorkflowTemplateId
        && evidence.WorkflowTemplateVersionId == operation.WorkflowTemplateVersionId
        && evidence.ObjectType == operation.ObjectType
        && evidence.ObjectId == operation.LskuId
        && evidence.ObjectId.ToString("D") == operation.ObjectId
        && evidence.ObjectRef == operation.ObjectRef
        && evidence.DecisionActorSubjectId != Guid.Empty
        && evidence.DecisionActorSubjectId != operation.MakerSubjectId
        && evidence.DecisionAtUtc.UtcTicks > 0
        && evidence.TransitionSequence > 0
        && ExactTerminalStatuses(
            ProductIdentityDecisionKind.Approved,
            evidence.TaskStatus,
            evidence.InstanceStatus);

    private static bool ExactBinding(
        ProductIdentityWorkflowBinding? actual,
        ProductIdentityWorkflowBinding expected) =>
        actual is not null
        && actual.WorkflowInstanceId == expected.WorkflowInstanceId
        && actual.WorkflowTemplateId == expected.WorkflowTemplateId
        && actual.WorkflowTemplateVersionId == expected.WorkflowTemplateVersionId
        && actual.ApprovalTaskId == expected.ApprovalTaskId
        && actual.AssignmentSnapshotId == expected.AssignmentSnapshotId
        && actual.StartTransitionLogId == expected.StartTransitionLogId
        && actual.ObjectId == expected.ObjectId
        && actual.SubmitterSubjectId == expected.SubmitterSubjectId
        && actual.SubmittedAtUtc == expected.SubmittedAtUtc
        && actual.DueAtUtc == expected.DueAtUtc
        && actual.TerminalDecision is null
        && string.Equals(actual.ObjectType, expected.ObjectType, StringComparison.Ordinal)
        && string.Equals(actual.ObjectRef, expected.ObjectRef, StringComparison.Ordinal)
        && string.Equals(actual.StartIdempotencyKey, expected.StartIdempotencyKey, StringComparison.Ordinal)
        && string.Equals(actual.StartRequestFingerprint, expected.StartRequestFingerprint, StringComparison.Ordinal);

    private static bool ExactDecisionResult(
        Lsku? lsku,
        LskuIdentityWorkflowOperation operation,
        ProductIdentityWorkflowDecisionEvidence evidence)
    {
        if (lsku is null || lsku.Version != operation.ExpectedLskuVersion + 2
            || lsku.IdentityWorkflowBinding is not { } binding
            || !ExactOperationBinding(binding, operation)
            || binding.TerminalDecision is not { } persisted
            || !ExactTerminalDecision(persisted, evidence)
            || lsku.LifecycleStatus != (evidence.Decision == ProductIdentityDecisionKind.Approved
                ? ProductIdentityLifecycleStatus.IdentityApproved
                : ProductIdentityLifecycleStatus.Draft))
        {
            return false;
        }
        var expected = LskuIdentityLifecycleAuditIntentFactory.CreateDecision(
            lsku, operation.ExpectedLskuVersion + 1, evidence);
        return ExactAuditResult(lsku, expected);
    }

    private static bool ExactAuditResult(Lsku lsku, LocalAuditIntent expected)
    {
        var intents = lsku.AuditIntents.Where(intent => intent.IntentId == expected.IntentId).Take(2).ToArray();
        var receipts = lsku.AuditIntentReceipts
            .Where(receipt => receipt.IntentId == expected.IntentId).Take(2).ToArray();
        var exactIntent = intents.Length == 1 && receipts.Length == 0
            && SameImmutableAudit(intents[0], expected);
        var exactReceipt = intents.Length == 0 && receipts.Length == 1
            && receipts[0].TenantId == expected.TenantId
            && receipts[0].SourceService == expected.SourceService
            && receipts[0].IdempotencyKey == expected.IdempotencyKey
            && receipts[0].EvidenceHash == expected.EvidenceHash;
        return exactIntent || exactReceipt;
    }

    private static bool ExactOperationBinding(
        ProductIdentityWorkflowBinding binding,
        LskuIdentityWorkflowOperation operation) =>
        binding.WorkflowInstanceId == operation.WorkflowInstanceId
        && binding.WorkflowTemplateId == operation.WorkflowTemplateId
        && binding.WorkflowTemplateVersionId == operation.WorkflowTemplateVersionId
        && binding.ApprovalTaskId == operation.ApprovalTaskId
        && binding.AssignmentSnapshotId == operation.AssignmentSnapshotId
        && binding.StartTransitionLogId == operation.StartTransitionLogId
        && binding.ObjectType == operation.ObjectType
        && binding.ObjectId == operation.LskuId
        && binding.ObjectRef == operation.ObjectRef
        && binding.SubmitterSubjectId == operation.MakerSubjectId
        && binding.StartIdempotencyKey == operation.StartIdempotencyKey
        && binding.StartRequestFingerprint == operation.OperationFingerprint
        && binding.SubmittedAtUtc.UtcTicks == operation.WorkflowStartedAtUtcTicksV1
        && binding.DueAtUtc?.UtcTicks == operation.DueAtUtcTicksV1;

    private static bool ExactTerminalDecision(
        ProductIdentityWorkflowDecisionEvidence actual,
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

    private static bool TryMapDecision(string action, out ProductIdentityDecisionKind decision)
    {
        if (string.Equals(action, "Approve", StringComparison.Ordinal))
        {
            decision = ProductIdentityDecisionKind.Approved;
            return true;
        }
        if (string.Equals(action, "Reject", StringComparison.Ordinal))
        {
            decision = ProductIdentityDecisionKind.Rejected;
            return true;
        }
        decision = default;
        return false;
    }

    private static bool ExactTerminalStatuses(
        ProductIdentityDecisionKind decision,
        string taskStatus,
        string instanceStatus) =>
        decision switch
        {
            ProductIdentityDecisionKind.Approved =>
                string.Equals(taskStatus, "Approved", StringComparison.Ordinal)
                && string.Equals(instanceStatus, "Completed", StringComparison.Ordinal),
            ProductIdentityDecisionKind.Rejected =>
                string.Equals(taskStatus, "Rejected", StringComparison.Ordinal)
                && string.Equals(instanceStatus, "Rejected", StringComparison.Ordinal),
            _ => false
        };

    private static bool TryRehydrateEvidence(
        LskuIdentityWorkflowOperation operation,
        out ProductIdentityWorkflowDecisionEvidence evidence)
    {
        evidence = null!;
        if (!operation.DecisionKind.HasValue || !operation.WorkflowInstanceId.HasValue
            || !operation.ApprovalTaskId.HasValue || !operation.DecisionWorkflowTemplateId.HasValue
            || !operation.DecisionWorkflowTemplateVersionId.HasValue
            || !operation.DecisionActorSubjectId.HasValue || !operation.DecisionTransitionSequence.HasValue
            || !operation.DecisionAtUtcTicksV1.HasValue
            || !Guid.TryParse(operation.DecisionObjectId, out var objectId))
        {
            return false;
        }
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

    private static bool ExactInteractiveFacts(
        LskuIdentityWorkflowOperation operation,
        Guid tenantId,
        Guid lskuId,
        int expectedVersion,
        Guid makerSubjectId) =>
        !operation.IsDeleted && operation.TenantId == tenantId
        && operation.LskuId == lskuId
        && operation.ExpectedLskuVersion == expectedVersion
        && operation.MakerSubjectId == makerSubjectId
        && string.Equals(operation.ObjectType, LskuIdentityWorkflowStartRequestFactory.LskuObjectType,
            StringComparison.Ordinal)
        && string.Equals(operation.ObjectId, lskuId.ToString("D"), StringComparison.Ordinal);

    private static bool Retryable(ProductIdentityWorkflowTransportOutcome outcome) =>
        outcome is ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Timeout;

    private static bool ExactApprovalProof(LskuIdentityWorkflowOperation operation) =>
        operation.ApprovalMarketSelection is { } selection
        && selection.SetCode == "market"
        && selection.ValueCode == operation.MarketCode
        && selection.CatalogVersionId != Guid.Empty
        && selection.CatalogVersionNumber > 0
        && selection.ResolutionMode == ReferenceCatalogResolutionMode.Latest
        && selection.ResolvedAtUtc != default
        && selection.ResolvedAtUtc.Offset == TimeSpan.Zero
        && operation.MarketValidatedAtUtcTicksV1 is > 0
        && operation.ApprovalMarketProofFingerprint is { } fingerprint
        && fingerprint == ApprovalProofFingerprint(
            operation.TenantId,
            operation.OperationId,
            operation.LskuId,
            operation.MarketCode,
            operation.DecisionTransitionSequence,
            selection,
            operation.MarketValidatedAtUtcTicksV1.Value);

    private static string ApprovalProofFingerprint(
        Guid tenantId,
        Guid operationId,
        Guid lskuId,
        string marketCode,
        long? decisionTransitionSequence,
        ReferenceCatalogSelection selection,
        long validatedAtUtcTicks)
    {
        var facts = string.Join('|',
            "lsku-approval-market-v1",
            tenantId.ToString("D"),
            operationId.ToString("D"),
            lskuId.ToString("D"),
            marketCode,
            decisionTransitionSequence?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            selection.SetCode,
            selection.ValueCode,
            selection.CatalogVersionId.ToString("D"),
            selection.CatalogVersionNumber.ToString(CultureInfo.InvariantCulture),
            ((int)selection.ResolutionMode).ToString(CultureInfo.InvariantCulture),
            selection.ResolvedAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture),
            validatedAtUtcTicks.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(facts))).ToLowerInvariant();
    }

    private static bool SameImmutableAudit(LocalAuditIntent left, LocalAuditIntent right) =>
        left.SourceService == right.SourceService
        && left.SchemaVersion == right.SchemaVersion
        && left.ContractVersion == right.ContractVersion
        && left.IntentId == right.IntentId
        && left.TenantId == right.TenantId
        && left.AggregateType == right.AggregateType
        && left.AggregateId == right.AggregateId
        && left.PreVersion == right.PreVersion
        && left.PostVersion == right.PostVersion
        && left.Operation == right.Operation
        && left.ActorId == right.ActorId
        && left.CorrelationId == right.CorrelationId
        && left.CausationId == right.CausationId
        && left.CommandId == right.CommandId
        && left.Sequence == right.Sequence
        && left.TimestampUtc == right.TimestampUtc
        && left.TimestampUtcTicksV1 == right.TimestampUtcTicksV1
        && left.TemporalStorageVersion == right.TemporalStorageVersion
        && left.EvidenceHash == right.EvidenceHash
        && left.SnapshotReference == right.SnapshotReference
        && left.IdempotencyKey == right.IdempotencyKey;

    private static string BoundedCode(string? code, string fallback) =>
        !string.IsNullOrWhiteSpace(code) && code.Length <= 128 && code.All(character => !char.IsControl(character))
            ? code
            : fallback;

    private static bool ExactText(string? value, int max) => value is { Length: > 0 }
        && value.Length <= max && value == value.Trim() && !value.Any(char.IsControl);

    private static bool OptionalExactText(string? value, int max) => value is null
        || value.Length <= max && value == value.Trim() && !value.Any(char.IsControl);

    private static string WithdrawalFingerprint(Guid tenantId, Guid lskuId, int expectedVersion,
        Guid commandId, Guid requester, string reason, string? comment, ProductIdentityWorkflowBinding binding)
    {
        var facts = string.Join('|', "lsku-withdrawal-v1", tenantId.ToString("D"), lskuId.ToString("D"),
            expectedVersion.ToString(CultureInfo.InvariantCulture), commandId.ToString("D"), requester.ToString("D"),
            reason, comment ?? string.Empty, binding.WorkflowInstanceId.ToString("D"),
            binding.ApprovalTaskId.ToString("D"), binding.StartIdempotencyKey);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(facts))).ToLowerInvariant();
    }

    private static bool ExactWithdrawalFacts(LskuIdentityWorkflowOperation operation, Guid commandId,
        string fingerprint, Guid requester, int expectedVersion, string reason, string? comment) =>
        operation.WithdrawalCommandId == commandId && operation.WithdrawalFingerprint == fingerprint
        && operation.WithdrawalRequesterSubjectId == requester
        && operation.WithdrawalExpectedLskuVersion == expectedVersion
        && operation.WithdrawalReasonCode == reason && operation.WithdrawalComment == comment;

    private static bool TryCancellationEvidence(LskuIdentityWorkflowOperation operation,
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
            WorkflowTemplateVersionId = templateVersionId, ObjectType = operation.ObjectType,
            ObjectId = operation.LskuId, ObjectRef = operation.WithdrawalObjectRef ?? operation.ObjectRef,
            RequesterSubjectId = requester, ReasonCode = operation.WithdrawalReasonCode!,
            Comment = operation.WithdrawalComment,
            CancelledAtUtc = new DateTimeOffset(operation.WithdrawalObservedAtUtcTicksV1.Value, TimeSpan.Zero),
            TransitionSequence = operation.WithdrawalTransitionSequence.Value, TransitionLogId = logId,
            TaskStatus = operation.WithdrawalTaskStatus!, InstanceStatus = operation.WithdrawalInstanceStatus!,
            WorkflowInstanceVersion = operation.WithdrawalResultWorkflowInstanceVersion.Value,
            ApprovalTaskVersion = operation.WithdrawalResultApprovalTaskVersion.Value,
            IdempotencyKey = commandId.ToString("D")
        };
        return evidence.TaskStatus == "Cancelled" && evidence.InstanceStatus == "Cancelled";
    }

    private static LskuIdentityWorkflowProcessingResult Success(
        LskuIdentityWorkflowOperation operation,
        bool replay) => new(true, operation, null, 200, replay);

    private static LskuIdentityWorkflowProcessingResult Fail(string errorCode, int statusCode) =>
        new(false, null, errorCode, statusCode, false);

    private static LskuIdentityWorkflowProcessingResult Fail(
        LskuIdentityWorkflowOperation operation,
        string errorCode,
        int statusCode) => new(false, operation, errorCode, statusCode, false);

    private sealed record ApprovalDependencyValidation(
        bool Succeeded,
        bool Retryable,
        string? ErrorCode,
        ReferenceCatalogSelection? Selection = null,
        // LSKU FIX2 item 7: a reference the maker can put right (market no longer offered, parent not referenceable).
        bool MakerFixable = false);
}
