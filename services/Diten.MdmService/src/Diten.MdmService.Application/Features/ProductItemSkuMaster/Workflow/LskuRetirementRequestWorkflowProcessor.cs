using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record LskuRetirementRequestProcessingResult(bool Succeeded,
    LskuRetirementRequestOperation? Operation, string? ErrorCode, int StatusCode, bool IsReplay);

// LSKU FIX1 B2: the GSKU retirement request's execution rules (streak limit, back-off ceiling, settle window, the
// maker's own request not counted) — the first two members keep their place for existing callers.
public sealed record LskuRetirementRequestExecutionConfiguration(TimeSpan LeaseDuration, TimeSpan RetryDelay,
    int QuarantineAfterFailedAttempts = ProductIdentityRecoveryStreak.DefaultQuarantineAfterFailedAttempts,
    TimeSpan? MaxRetryDelay = null,
    TimeSpan? StartSettleWindow = null)
{
    public TimeSpan SettleWindow => StartSettleWindow is { } window && window > TimeSpan.Zero ? window : TimeSpan.FromMinutes(2);

    public bool Interactive { get; init; }
}

/*
 * LSKU FIX1 B2 / S2 / S3 / B5 — the accepted GSKU retirement request pattern (GskuRetirementRequestWorkflowProcessor),
 * applied to the LSKU's own request:
 *   - the lock is given back only when the engine is known to hold nothing for this request (principle A): a start
 *     refused before anything was processed, the lookup's "nothing under this key" after the settle window, an
 *     approval closed without a decision, an approval its own requester decided; the lock and the "one active request"
 *     flag go in the same step (ENDED = manual reconciliation with nothing held);
 *   - nothing stays stuck without a named exit (principle B): a STUCK request keeps the lock, is counted, and its
 *     requester's same command reads the engine again; the reason is kept at the limit (*_STUCK_ON_RECORD_ANSWER /
 *     *_STUCK_ON_RELEASE_CONFLICT, a person's — BL-530); only a service fault reaches RETRY_LIMIT_REACHED and keeps
 *     retrying;
 *   - a refused write: the cap / the store / a lease held elsewhere wait, a concurrent write is retried (counted),
 *     anything else is stuck at once (S3: the decision's write no longer retries for ever).
 */
public sealed class LskuRetirementRequestWorkflowProcessor(
    ILskuRetirementRequestOperationRepository operations, ILskuRepository lskus,
    IProductIdentityWorkflowClient workflow, LskuRetirementRequestWorkflowStartRequestFactory factory,
    TimeProvider clock, ILogger<LskuRetirementRequestWorkflowProcessor>? logger = null)
{
    private readonly ILogger log = logger ?? NullLogger<LskuRetirementRequestWorkflowProcessor>.Instance;

    public const string SelfApprovalIgnored = "LSKU_RETIREMENT_SELF_APPROVAL_IGNORED";
    public const string ManualReconciliationNeeded = "LSKU_RETIREMENT_MANUAL_RECONCILIATION_NEEDED";
    public const string StartSettling = "LSKU_RETIREMENT_START_SETTLING";
    public const string StartNotReceived = "LSKU_RETIREMENT_START_NOT_RECEIVED";
    public const string ClosedWithoutDecision = "LSKU_RETIREMENT_CLOSED_WITHOUT_DECISION";
    public const string RetryLimitReached = "LSKU_RETIREMENT_RETRY_LIMIT_REACHED";
    public const string StuckOnRecordAnswer = "LSKU_RETIREMENT_STUCK_ON_RECORD_ANSWER";
    public const string StuckOnReleaseConflict = "LSKU_RETIREMENT_STUCK_ON_RELEASE_CONFLICT";
    public const string EvidenceConflict = "LSKU_RETIREMENT_EVIDENCE_CONFLICT";
    public const string StartEvidenceInvalid = "LSKU_RETIREMENT_START_EVIDENCE_INVALID";
    public const string LskuDrift = "LSKU_RETIREMENT_LSKU_DRIFT";
    public const string ReleaseConflict = "LSKU_LIFECYCLE_RELEASE_CONFLICT";

    public async Task<LskuRetirementRequestProcessingResult> StartInteractiveAsync(Guid tenantId, Guid lskuId,
        int expectedVersion, Guid operationId, Guid makerId, string requestReason, string delegatedToken,
        LskuRetirementRequestExecutionConfiguration execution, CancellationToken ct)
    {
        if (tenantId == Guid.Empty || lskuId == Guid.Empty || operationId == Guid.Empty || makerId == Guid.Empty
            || expectedVersion < 0 || !LskuRetirementRequestWorkflowStartRequestFactory.HasValidReason(requestReason)
            || string.IsNullOrWhiteSpace(delegatedToken)) return Fail("LSKU_RETIREMENT_REQUEST_INVALID", 400);
        var operation = await operations.GetByOperationIdAsync(operationId, ct);
        var replay = operation is not null;
        if (operation is null)
        {
            var lsku = await lskus.GetByIdAsync(lskuId, ct);
            if (lsku is null || lsku.IsDeleted || lsku.TenantId != tenantId) return Fail("LSKU_NOT_FOUND", 404);
            if (lsku.Version != expectedVersion) return Fail("LSKU_CONCURRENCY_CONFLICT", 409);
            LskuRetirementRequestStartPlan plan;
            try { plan = factory.Create(lsku, operationId, makerId, requestReason); }
            catch (InvalidOperationException e)
            {
                return Fail(e.Message, e.Message.Contains("CONFIGURATION", StringComparison.Ordinal) ? 503 : 409);
            }
            var reserved = await operations.ReserveAsync(plan.Operation, ct);
            if (!reserved.Succeeded || reserved.Operation is null)
                return Fail(reserved.ErrorCode ?? "LSKU_RETIREMENT_OPERATION_CONFLICT", 409);
            operation = reserved.Operation; replay = reserved.IsReplay;
        }
        if (operation.TenantId != tenantId || operation.LskuId != lskuId
            || operation.BaseLskuVersion != expectedVersion || operation.MakerSubjectId != makerId
            || !string.Equals(operation.RequestReason,
                LskuRetirementRequestWorkflowStartRequestFactory.NormalizeReason(requestReason), StringComparison.Ordinal)
            || !string.Equals(operation.OperationFingerprint,
                LskuRetirementRequestWorkflowStartRequestFactory.ComputeFingerprint(operation), StringComparison.Ordinal))
            return Fail("LSKU_RETIREMENT_OPERATION_CONFLICT", 409);
        var wasStuck = IsStuck(operation);
        var cause = operation.LastFailureCode;
        var result = await ProcessAsync(operation, delegatedToken, $"interactive-{makerId:N}", execution, true, ct);
        // One Error line when a request becomes stuck in its maker's own request (no runner sees it).
        if (!wasStuck && result.Operation is { } after && IsStuck(after))
            log.LogError("LSKU retirement request {OperationId} of tenant {TenantId} is stuck ({ResultCode}); the cause before: {CauseCode}.",
                after.OperationId, after.TenantId, after.LastFailureCode, cause ?? "none");
        return result with { IsReplay = replay || result.IsReplay };
    }

    public Task<LskuRetirementRequestProcessingResult> RecoverAsync(LskuRetirementRequestOperation operation,
        string leaseOwner, LskuRetirementRequestExecutionConfiguration execution, CancellationToken ct) =>
        ProcessAsync(operation, null, leaseOwner, execution, false, ct);

    private async Task<LskuRetirementRequestProcessingResult> ProcessAsync(LskuRetirementRequestOperation operation,
        string? token, string leaseOwner, LskuRetirementRequestExecutionConfiguration execution,
        bool stopAtPending, CancellationToken ct)
    {
        execution = execution with { Interactive = token is not null };
        var reconciling = false;
        var signal = new ReconcileSignal();
        for (var step = 0; step < 10; step++)
        {
            ct.ThrowIfCancellationRequested();
            if (operation.Checkpoint == LskuRetirementRequestCheckpoint.Completed) return Success(operation, true);
            var reconcile = false;
            if (operation.Checkpoint == LskuRetirementRequestCheckpoint.ManualReconciliationRequired)
            {
                // A STUCK request's way out is its requester's same command; an engine that could not be asked is a
                // service fault (503), not "a person".
                if (signal.EngineSilent)
                    return Fail(operation, ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, 503);
                if (token is null || step > 0
                    || operation.RecoveryDisposition != ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
                    return Fail(operation, reconciling && operation.RecoveryDisposition
                            == ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired
                        ? ManualReconciliationNeeded
                        : operation.LastFailureCode ?? "LSKU_RETIREMENT_RECONCILIATION_REQUIRED", 409);
                reconcile = reconciling = true;
            }
            if (stopAtPending && !reconciling && operation.Checkpoint == LskuRetirementRequestCheckpoint.AwaitingDecision)
                return Success(operation, false);
            var now = clock.GetUtcNow();
            // A requester's repeat before the worker's next attempt is due is answered without a claim.
            if (token is not null && !reconcile
                && operation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable
                && operation.NextAttemptAtUtcTicksV1 > now.UtcTicks)
                return Fail(operation, operation.LastFailureCode ?? "LSKU_RETIREMENT_RETRY_SCHEDULED", 503);
            var leaseUntil = now.Add(execution.LeaseDuration).UtcTicks;
            var (claim, storeSilent) = await ProductIdentityWorkflowClaims.ClaimAsync(
                bound => operations.TryClaimAsync(operation.OperationId, operation.OperationFingerprint,
                    [operation.Checkpoint], leaseOwner, now.UtcTicks, leaseUntil, bound),
                bound => operations.ReleaseIfOwnedAsync(operation.OperationId, leaseOwner, leaseUntil, bound),
                execution.LeaseDuration,
                exception => log.LogWarning("{Flow} {OperationId}: a claim the store did not answer could not be given back ({ExceptionType}).",
                    "LSKU_RETIREMENT_REQUEST", operation.OperationId, exception.GetType().Name));
            if (storeSilent) return Fail(operation, ProductIdentityWorkflowClaims.StoreUnavailable, 503);
            if (claim is null) return Fail(operation, "LSKU_RETIREMENT_BUSY", 409);
            bool advanced;
            try
            {
                advanced = operation.Checkpoint switch
                {
                    LskuRetirementRequestCheckpoint.Prepared => await PrepareAsync(operation, claim, token, now, execution, ct),
                    LskuRetirementRequestCheckpoint.ManualReconciliationRequired when reconcile
                        => await ReconcileAsync(operation, claim, now, execution, signal, ct),
                    // Requests parked here before FIX1 hold the LSKU's lock: the worker settles them like a Prepared one.
                    LskuRetirementRequestCheckpoint.AwaitingMakerReplay when token is null
                        => await PreparedWithoutMakerAsync(operation, claim, now, execution, ct),
                    LskuRetirementRequestCheckpoint.StartOutcomeUnknown or LskuRetirementRequestCheckpoint.AwaitingMakerReplay
                        => await RecoverStartAsync(operation, claim, token, now, execution, ct),
                    LskuRetirementRequestCheckpoint.WorkflowStarted => await Advance(claim,
                        LskuRetirementRequestCheckpoint.AwaitingDecision, now, ct),
                    LskuRetirementRequestCheckpoint.AwaitingDecision => await ObserveAsync(operation, claim, now, execution, ct),
                    LskuRetirementRequestCheckpoint.DecisionObserved => await ApplyAsync(operation, claim, now, execution, ct),
                    LskuRetirementRequestCheckpoint.DecisionApplied => await CompleteAsync(operation, claim, now, ct),
                    _ => false
                };
            }
            catch
            {
                // S2: a step that throws gives its lease back (bounded, not under the caller's token).
                await ReleaseAsync(operation, leaseOwner, leaseUntil, execution);
                throw;
            }
            if (!advanced)
            {
                // S2: progress the store refused (a lost lease, a refused transition) gives the lease back.
                await ReleaseAsync(operation, leaseOwner, leaseUntil, execution);
                return Fail(operation, "LSKU_RETIREMENT_CONCURRENCY_CONFLICT", 409);
            }
            operation = await operations.GetByOperationIdAsync(operation.OperationId, ct)
                ?? throw new InvalidOperationException("LSKU_RETIREMENT_OPERATION_LOST");
            if (reconciling && operation.Checkpoint == LskuRetirementRequestCheckpoint.AwaitingDecision
                && operation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.None
                && operation.NextAttemptAtUtcTicksV1 > clock.GetUtcNow().UtcTicks)
                return Success(operation, false);
            // A reconciling repeat that left the request WAITING (its lock release has to wait) stops here: the worker
            // releases it; going on would treat "nothing arrived" as a start never sent and send it again.
            if (reconciling && operation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable)
                return Fail(operation, ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, 503);
            if (operation.NextAttemptAtUtcTicksV1 > clock.GetUtcNow().UtcTicks)
                return Fail(operation, operation.LastFailureCode ?? "LSKU_RETIREMENT_RETRY_SCHEDULED", 503);
        }
        return Fail(operation, "LSKU_RETIREMENT_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<bool> PrepareAsync(LskuRetirementRequestOperation op, LskuRetirementRequestClaim claim,
        string? token, DateTimeOffset now, LskuRetirementRequestExecutionConfiguration execution, CancellationToken ct)
    {
        if (token is null)
            // The worker never starts a request, nor parks it holding the lock (B2: FIX0 parked it with the lock).
            return await PreparedWithoutMakerAsync(op, claim, now, execution, ct);
        var lsku = await lskus.GetByIdAsync(op.LskuId, ct);
        if (lsku is null || lsku.IsDeleted || lsku.TenantId != op.TenantId || op.CreatedAtUtcTicksV1 <= 0)
            return await Ended(claim, "LSKU_NOT_FOUND", now, ct);
        var admitted = await AcquireAsync(op, lsku, ct);
        // Nothing was taken: the request ends and the reason is kept (never a lock without a request behind it).
        if (!admitted.Succeeded) return await Ended(claim, admitted.ErrorCode, now, ct);
        return await RecordStart(op, claim, await workflow.StartAsync(op.TenantId, factory.Rehydrate(op), token, ct),
            fromStart: true, now, execution, ct);
    }

    private Task<LskuLifecycleWriteResult> AcquireAsync(LskuRetirementRequestOperation op, Lsku lsku,
        CancellationToken ct) =>
        lskus.AcquireLifecycleOperationAsync(lsku.Id, op.BaseLskuVersion, Binding(op),
            LskuIdentityLifecycleAuditIntentFactory.CreateRetirementOperation(lsku, op.BaseLskuVersion,
                op.OperationId, op.MakerSubjectId, ProductAuditOperation.LskuRetirementRequested, op.RequestReason,
                new(op.CreatedAtUtcTicksV1, TimeSpan.Zero)), ct);

    private async Task<bool> PreparedWithoutMakerAsync(LskuRetirementRequestOperation op,
        LskuRetirementRequestClaim claim, DateTimeOffset now,
        LskuRetirementRequestExecutionConfiguration execution, CancellationToken ct)
    {
        var lsku = await lskus.GetByIdAsync(op.LskuId, ct);
        if (lsku is null || lsku.IsDeleted || lsku.TenantId != op.TenantId)
            return await Ended(claim, "LSKU_NOT_FOUND", now, ct);
        var found = await workflow.GetStartResultAsync(op.TenantId,
            new(op.ObjectType, op.ObjectId, op.MakerSubjectId, op.StartIdempotencyKey), ct);
        if (found.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
        {
            var admitted = await AcquireAsync(op, lsku, ct);
            if (!admitted.Succeeded) return await Stuck(claim, admitted.ErrorCode, now, ct);
            return await RecordStart(op, claim, found, fromStart: false, now, execution, ct);
        }
        if (found.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound)
            // B2: a start PROVEN never created (no start under this key after the settle window) is released.
            return Settling(op, now, execution)
                ? await RetryAsync(op, claim, op.Checkpoint, StartSettling, now, execution, ct, transient: true)
                : await EndAndReleaseAsync(op, claim, StartNotReceived, now, execution, ct);
        return await RetryAsync(op, claim, op.Checkpoint,
            found.ErrorCode ?? "LSKU_RETIREMENT_START_LOOKUP_FAILED", now, execution, ct,
            transient: Waits(ProductIdentityWorkflowCall.StartResult, found.Outcome, found.ErrorCode));
    }

    // The requester's same command on a STUCK request: read the engine again and put the request back.
    private async Task<bool> ReconcileAsync(LskuRetirementRequestOperation op, LskuRetirementRequestClaim claim,
        DateTimeOffset now, LskuRetirementRequestExecutionConfiguration execution, ReconcileSignal signal,
        CancellationToken ct)
    {
        if (op.DecisionKind.HasValue && op.DecisionActorSubjectId.HasValue)
            return await Advance(claim, LskuRetirementRequestCheckpoint.DecisionObserved, now, ct);
        if (op.WorkflowInstanceId.HasValue)
            return await Advance(claim, LskuRetirementRequestCheckpoint.AwaitingDecision, now, ct);
        var found = await workflow.GetStartResultAsync(op.TenantId,
            new(op.ObjectType, op.ObjectId, op.MakerSubjectId, op.StartIdempotencyKey), ct);
        if (found.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
            return await RecordStart(op, claim, found, fromStart: false, now, execution, ct);
        if (found.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound && !Settling(op, now, execution))
            return await EndAndReleaseAsync(op, claim, StartNotReceived, now, execution, ct);
        // Not settled now: in the settle window, or a service fault — ask again (503); an answer about the record will
        // not change by asking again — 409, a person (BL-530).
        if (found.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound
            || Waits(ProductIdentityWorkflowCall.StartResult, found.Outcome, found.ErrorCode))
            signal.EngineSilent = true;
        return await operations.AdvanceAsync(claim, new(op.Checkpoint, op.RecoveryDisposition, now.UtcTicks,
            LastFailureCode: op.LastFailureCode, ReleaseLease: true, Streak: RecoveryStreakWrite.Keep), ct);
    }

    private static bool IsTransient(ProductIdentityWorkflowTransportOutcome outcome) =>
        outcome is ProductIdentityWorkflowTransportOutcome.Timeout or ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Incomplete;

    private static bool Waits(ProductIdentityWorkflowCall call, ProductIdentityWorkflowTransportOutcome outcome, string? code) =>
        IsTransient(outcome)
        || outcome is not (ProductIdentityWorkflowTransportOutcome.Success or ProductIdentityWorkflowTransportOutcome.NotFound
            or ProductIdentityWorkflowTransportOutcome.NonTerminal)
           && ProductIdentityWorkflowFaultPolicy.Classify(call, outcome, code).Kind == ProductIdentityWorkflowFaultKind.ServiceWait;

    private sealed class ReconcileSignal
    {
        public bool EngineSilent { get; set; }
    }

    // A start sent less than the settle window ago may still be in the engine's hands.
    private static bool Settling(LskuRetirementRequestOperation op, DateTimeOffset now,
        LskuRetirementRequestExecutionConfiguration execution) =>
        now.UtcTicks - (op.LastStartSentAtUtcTicksV1 ?? op.UpdatedAtUtcTicksV1) < execution.SettleWindow.Ticks;

    private async Task<bool> RecoverStartAsync(LskuRetirementRequestOperation op, LskuRetirementRequestClaim claim,
        string? token, DateTimeOffset now, LskuRetirementRequestExecutionConfiguration execution, CancellationToken ct)
    {
        var found = await workflow.GetStartResultAsync(op.TenantId,
            new(op.ObjectType, op.ObjectId, op.MakerSubjectId, op.StartIdempotencyKey), ct);
        var fromStart = false;
        if (found.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound && token is not null)
        {
            found = await workflow.StartAsync(op.TenantId, factory.Rehydrate(op), token, ct);
            fromStart = true;
        }
        return await RecordStart(op, claim, found, fromStart, now, execution, ct);
    }

    private async Task<bool> RecordStart(LskuRetirementRequestOperation op, LskuRetirementRequestClaim claim,
        ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult> result, bool fromStart,
        DateTimeOffset now, LskuRetirementRequestExecutionConfiguration execution, CancellationToken ct)
    {
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.Success && result.Value is { } v
            && v.WorkflowInstanceId != Guid.Empty && v.TemplateId != Guid.Empty && v.TemplateVersionId != Guid.Empty
            && v.ApprovalTaskId != Guid.Empty && v.AssignmentSnapshotId != Guid.Empty
            && v.StartTransitionLogId != Guid.Empty && v.ObjectRef == op.ObjectRef
            && (!op.WorkflowTemplateId.HasValue || v.TemplateId == op.WorkflowTemplateId))
            return await operations.AdvanceAsync(claim, new(LskuRetirementRequestCheckpoint.WorkflowStarted,
                ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks, WorkflowInstanceId: v.WorkflowInstanceId,
                WorkflowTemplateId: v.TemplateId, WorkflowTemplateVersionId: v.TemplateVersionId,
                ApprovalTaskId: v.ApprovalTaskId, AssignmentSnapshotId: v.AssignmentSnapshotId,
                StartTransitionLogId: v.StartTransitionLogId,
                WorkflowStartedAtUtcTicksV1: (v.StartedAt ?? now).UtcTicks, ReleaseLease: true), ct);
        // Principle A: the lock goes back only when the engine is known to hold nothing for this request.
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound && !fromStart && Settling(op, now, execution))
            return await RetryAsync(op, claim, LskuRetirementRequestCheckpoint.StartOutcomeUnknown, StartSettling,
                now, execution, ct, transient: true);
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound)
            // From the start: no such definition — nothing was started. From the lookup after the window: nothing
            // arrived. Either way the LSKU is free (B2: the former endless StartOutcomeUnknown loop).
            return await EndAndReleaseAsync(op, claim, fromStart
                ? ProductIdentityWorkflowMakerCodes.DefinitionNotReady
                : StartNotReceived, now, execution, ct);
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.Timeout or ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Incomplete or ProductIdentityWorkflowTransportOutcome.Success)
            // The answer did not arrive, or cannot be used: the engine may have started an approval — the lock stays.
            return await StartUnknownAsync(op, claim, result.ErrorCode
                ?? (result.Outcome == ProductIdentityWorkflowTransportOutcome.Success ? StartEvidenceInvalid : null),
                now, execution, ct, fromStart, transient: Waits(fromStart ? ProductIdentityWorkflowCall.Start
                    : ProductIdentityWorkflowCall.StartResult, result.Outcome, result.ErrorCode));
        if (!fromStart)
            // The LOOKUP's refusal says nothing about what the lost start did: the lock is kept and the engine asked
            // again; only the START call's own "nothing was processed" answer may give the LSKU back.
            return await StartUnknownAsync(op, claim, result.ErrorCode ?? "LSKU_RETIREMENT_START_LOOKUP_FAILED", now,
                execution, ct, startSent: false,
                transient: Waits(ProductIdentityWorkflowCall.StartResult, result.Outcome, result.ErrorCode));
        var fault = ProductIdentityWorkflowFaultPolicy.Classify(
            ProductIdentityWorkflowCall.Start, result.Outcome, result.ErrorCode);
        if (!fault.StartedNothing)
            return await StartUnknownAsync(op, claim, result.ErrorCode, now, execution, ct, fromStart,
                transient: fault.Kind == ProductIdentityWorkflowFaultKind.ServiceWait);
        // B2: refused before anything was processed (403 and its kind): nothing exists at the engine — the LSKU is
        // given back, the request ends with the reason, and a second request is possible at once.
        return await EndAndReleaseAsync(op, claim, fault.MakerCode ?? result.ErrorCode ?? "LSKU_RETIREMENT_START_REFUSED",
            now, execution, ct);
    }

    private Task<bool> StartUnknownAsync(LskuRetirementRequestOperation op, LskuRetirementRequestClaim claim,
        string? code, DateTimeOffset now, LskuRetirementRequestExecutionConfiguration execution, CancellationToken ct,
        bool startSent = false, bool transient = false)
    {
        if (code is not null && code.EndsWith("_INVALID", StringComparison.Ordinal) && op.LastFailureCode != code)
            log.LogError("LSKU retirement request {OperationId} of tenant {TenantId}: the engine's start answer cannot be used ({ResultCode}); the lock is kept and the start is looked up again.",
                op.OperationId, op.TenantId, code);
        return RetryAsync(op, claim, LskuRetirementRequestCheckpoint.StartOutcomeUnknown, code, now, execution, ct,
            transient, startSentAt: startSent ? now.UtcTicks : null);
    }

    // B2: the request ends without its decision applied; the LSKU's lock goes in the same write as the intent that
    // says why, and the store clears the request's "active" flag with the ENDED write.
    private async Task<bool> EndAndReleaseAsync(LskuRetirementRequestOperation op, LskuRetirementRequestClaim claim,
        string reason, DateTimeOffset now, LskuRetirementRequestExecutionConfiguration execution, CancellationToken ct)
    {
        var lsku = await lskus.GetByIdAsync(op.LskuId, ct);
        if (lsku?.ActiveLifecycleOperation is { } held && held == Binding(op))
        {
            var audit = LskuIdentityLifecycleAuditIntentFactory.CreateRetirementRelease(lsku,
                checked(op.BaseLskuVersion + 1), op.OperationId, op.MakerSubjectId, reason, now);
            var released = await lskus.ReleaseLifecycleOperationAsync(lsku.Id, checked(op.BaseLskuVersion + 1),
                Binding(op), audit, ct);
            if (!released.Succeeded)
                return await WriteRefusedAsync(op, claim, released.ErrorCode, now, execution, ct);
        }
        log.LogInformation("LSKU retirement request {OperationId} of tenant {TenantId} ended without its decision applied ({ResultCode}); the LSKU is free again.",
            op.OperationId, op.TenantId, reason);
        return await Ended(claim, reason, now, ct);
    }

    private async Task<bool> ObserveAsync(LskuRetirementRequestOperation op, LskuRetirementRequestClaim claim,
        DateTimeOffset now, LskuRetirementRequestExecutionConfiguration execution, CancellationToken ct)
    {
        if (!op.WorkflowInstanceId.HasValue) return await Stuck(claim, StartEvidenceInvalid, now, ct);
        var result = await workflow.GetTerminalEvidenceAsync(op.TenantId,
            new(op.WorkflowInstanceId.Value, op.ObjectType, op.ObjectId), ct);
        // NOT_TERMINAL (escalated or not) waits; it is not a failure — the streak is over.
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.NonTerminal)
            return await operations.AdvanceAsync(claim, new(op.Checkpoint, op.RecoveryDisposition, now.UtcTicks,
                now.Add(execution.RetryDelay).UtcTicks, result.ErrorCode ?? "LSKU_RETIREMENT_DECISION_PENDING",
                ReleaseLease: true), ct);
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.NotFound
            or ProductIdentityWorkflowTransportOutcome.Incomplete or ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Timeout)
            return await RetryAsync(op, claim, op.Checkpoint,
                result.ErrorCode ?? "LSKU_RETIREMENT_DECISION_PENDING", now, execution, ct,
                transient: IsTransient(result.Outcome));
        if (result.Outcome != ProductIdentityWorkflowTransportOutcome.Success
            && ProductIdentityWorkflowFaultPolicy.Classify(ProductIdentityWorkflowCall.TerminalEvidence, result.Outcome,
                result.ErrorCode).Kind == ProductIdentityWorkflowFaultKind.ServiceWait)
            return await RetryAsync(op, claim, op.Checkpoint, result.ErrorCode, now, execution, ct, transient: true);
        var e = result.Value;
        if (e is not null && Guid.TryParse(e.ActorUserId, out var self) && self == op.MakerSubjectId)
        {
            // B5: the requester decided their own request and the engine has closed it — no other decision will
            // come. The request ends without being applied and the LSKU is given back (formerly: quarantined, locked).
            log.LogWarning("LSKU retirement request {OperationId} of tenant {TenantId} was decided by its own requester; the decision is not applied ({ResultCode}).",
                op.OperationId, op.TenantId, SelfApprovalIgnored);
            return await EndAndReleaseAsync(op, claim, SelfApprovalIgnored, now, execution, ct);
        }
        var approved = e?.TerminalAction == "Approve" && e.TaskStatus == "Approved" && e.InstanceStatus == "Completed";
        var rejected = e?.TerminalAction == "Reject" && e.TaskStatus == "Rejected" && e.InstanceStatus == "Rejected";
        if ((!approved && !rejected) || e is null || e.WorkflowInstanceId != op.WorkflowInstanceId
            || e.ApprovalTaskId != op.ApprovalTaskId || e.TemplateId != op.WorkflowTemplateId
            || e.TemplateVersionId != op.WorkflowTemplateVersionId || e.ObjectType != op.ObjectType
            || e.ObjectId != op.ObjectId || e.ObjectRef != op.ObjectRef
            || !Guid.TryParse(e.ActorUserId, out var actor) || actor == Guid.Empty
            || e.TransitionSequence <= 0 || e.DecisionAt.Offset != TimeSpan.Zero)
            // BL-547: CLOSED_WITHOUT_DECISION ends + releases; INCONSISTENT (or decided evidence this request cannot
            // apply) is stuck with the lock held — the requester's same command re-reads; an answer that is not
            // evidence is asked again (counted).
            return ProductIdentityWorkflowFaultPolicy.IsClosedWithoutDecision(result)
                ? await EndAndReleaseAsync(op, claim, ClosedWithoutDecision, now, execution, ct)
                : e is null && result.Outcome != ProductIdentityWorkflowTransportOutcome.Conflict
                    ? await RetryAsync(op, claim, op.Checkpoint,
                        result.ErrorCode ?? "LSKU_RETIREMENT_EVIDENCE_UNREADABLE", now, execution, ct)
                    : await Stuck(claim, EvidenceConflict, now, ct);
        return await operations.AdvanceAsync(claim, new(LskuRetirementRequestCheckpoint.DecisionObserved,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
            DecisionKind: approved ? ProductIdentityDecisionKind.Approved : ProductIdentityDecisionKind.Rejected,
            DecisionActorSubjectId: actor, DecisionReasonCode: e.ReasonCode, DecisionAtUtcTicksV1: e.DecisionAt.UtcTicks,
            DecisionTransitionSequence: e.TransitionSequence, DecisionTaskStatus: e.TaskStatus,
            DecisionInstanceStatus: e.InstanceStatus, ReleaseLease: true), ct);
    }

    private async Task<bool> ApplyAsync(LskuRetirementRequestOperation op, LskuRetirementRequestClaim claim,
        DateTimeOffset now, LskuRetirementRequestExecutionConfiguration execution, CancellationToken ct)
    {
        var lsku = await lskus.GetByIdAsync(op.LskuId, ct);
        // A gone LSKU does not come back by waiting; recorded facts that cannot be applied are stuck (a person's).
        if (lsku is null || lsku.IsDeleted || lsku.TenantId != op.TenantId
            || !op.DecisionActorSubjectId.HasValue || op.DecisionAtUtcTicksV1 is not > 0
            || op.DecisionKind is not (ProductIdentityDecisionKind.Approved or ProductIdentityDecisionKind.Rejected))
            return await Stuck(claim, LskuDrift, now, ct);
        var approved = op.DecisionKind == ProductIdentityDecisionKind.Approved;
        var expected = checked(op.BaseLskuVersion + 1);
        var audit = LskuIdentityLifecycleAuditIntentFactory.CreateRetirementOperation(lsku, expected,
            op.OperationId, op.DecisionActorSubjectId.Value,
            approved ? ProductAuditOperation.LskuIdentityRetired : ProductAuditOperation.LskuRetirementRejected,
            op.RequestReason, new(op.DecisionAtUtcTicksV1.Value, TimeSpan.Zero));
        var result = await lskus.ApplyRetirementDecisionAsync(lsku.Id, expected, Binding(op), approved, audit, ct);
        // S3: the refusal is classified (formerly every refusal was retried for ever).
        if (!result.Succeeded) return await WriteRefusedAsync(op, claim, Normalize(result.ErrorCode), now, execution, ct);
        return await Advance(claim, LskuRetirementRequestCheckpoint.DecisionApplied, now, ct);
    }

    // The LSKU's own readback (kept from FIX0, stricter than the GSKU's): the decision is on the LSKU exactly once, at
    // the version it was written at. Anything else is the record's answer — stuck, a person's (BL-530).
    private async Task<bool> CompleteAsync(LskuRetirementRequestOperation op, LskuRetirementRequestClaim claim,
        DateTimeOffset now, CancellationToken ct)
    {
        var lsku = await lskus.GetByIdAsync(op.LskuId, ct);
        if (lsku is null || lsku.IsDeleted || lsku.TenantId != op.TenantId
            || lsku.Version != (long)op.BaseLskuVersion + 2 || lsku.ActiveLifecycleOperation is not null
            || op.DecisionAtUtcTicksV1 is not > 0 || op.DecisionActorSubjectId is not { } actor || actor == Guid.Empty
            || op.DecisionKind is not (ProductIdentityDecisionKind.Approved or ProductIdentityDecisionKind.Rejected)
            || lsku.LifecycleStatus != (op.DecisionKind == ProductIdentityDecisionKind.Approved
                ? ProductIdentityLifecycleStatus.Retired : ProductIdentityLifecycleStatus.IdentityApproved))
            return await Stuck(claim, ReadbackConflict, now, ct);
        var audit = LskuIdentityLifecycleAuditIntentFactory.CreateRetirementOperation(lsku,
            checked(op.BaseLskuVersion + 1), op.OperationId, actor,
            op.DecisionKind == ProductIdentityDecisionKind.Approved ? ProductAuditOperation.LskuIdentityRetired
                : ProductAuditOperation.LskuRetirementRejected, op.RequestReason,
            new(op.DecisionAtUtcTicksV1.Value, TimeSpan.Zero));
        var intent = lsku.AuditIntents.Count(x => x.IntentId == audit.IntentId
            && x.TenantId == op.TenantId && x.AggregateId == op.LskuId && x.Operation == audit.Operation
            && x.PreVersion == audit.PreVersion && x.PostVersion == audit.PostVersion
            && x.IdempotencyKey == audit.IdempotencyKey && x.EvidenceHash == audit.EvidenceHash);
        var receipt = lsku.AuditIntentReceipts.Count(x => x.IntentId == audit.IntentId
            && x.TenantId == op.TenantId && x.SourceService == audit.SourceService
            && x.IdempotencyKey == audit.IdempotencyKey && x.EvidenceHash == audit.EvidenceHash
            && !string.IsNullOrWhiteSpace(x.CentralAcknowledgement)
            && !string.IsNullOrWhiteSpace(x.CompactReceiptReference));
        return intent + receipt == 1
            ? await Advance(claim, LskuRetirementRequestCheckpoint.Completed, now, ct)
            : await Stuck(claim, ReadbackConflict, now, ct);
    }

    public const string ReadbackConflict = "LSKU_RETIREMENT_READBACK_CONFLICT";

    // The retire write keeps its older codes; the shared classification reads them as the GSKU's.
    private static string? Normalize(string? code) => code switch
    {
        "AUDIT_INTENT_CAPACITY_EXCEEDED" => ProductIdentityWriteFaults.AuditCapacityReached,
        "LSKU_IDENTITY_VERSION_CONFLICT" => "LSKU_CONCURRENCY_CONFLICT",
        _ => code
    };

    // S3 — the GSKU rule: capacity / store / a lease held elsewhere wait (never stuck), a concurrent write or the
    // lock release's conflict is retried (counted), anything else is stuck at once with the lock held (BL-530).
    private Task<bool> WriteRefusedAsync(LskuRetirementRequestOperation op, LskuRetirementRequestClaim claim,
        string? code, DateTimeOffset now, LskuRetirementRequestExecutionConfiguration execution, CancellationToken ct) =>
        ProductIdentityWriteFaults.MayPassByItself(code)
            ? RetryAsync(op, claim, op.Checkpoint, code, now, execution, ct, transient: true)
            : ProductIdentityWriteFaults.MetAConcurrentWrite(code) || code == ReleaseConflict
                ? RetryAsync(op, claim, op.Checkpoint, code, now, execution, ct)
                : Stuck(claim, code, now, ct);

    private async Task<bool> RetryAsync(LskuRetirementRequestOperation op, LskuRetirementRequestClaim claim,
        LskuRetirementRequestCheckpoint checkpoint, string? code, DateTimeOffset now,
        LskuRetirementRequestExecutionConfiguration execution, CancellationToken ct, bool transient = false,
        long? startSentAt = null)
    {
        // A stuck request that has to WAIT goes back where the worker looks again (never "manual + retryable").
        if (checkpoint == LskuRetirementRequestCheckpoint.ManualReconciliationRequired)
            checkpoint = op.WorkflowInstanceId.HasValue
                ? LskuRetirementRequestCheckpoint.AwaitingDecision
                : LskuRetirementRequestCheckpoint.StartOutcomeUnknown;
        var counted = !execution.Interactive;
        var (exhausted, _, next) = ProductIdentityRecoveryStreak.Next(
            counted ? op.FailedAttemptCount : Math.Max(op.FailedAttemptCount - 1, 0), now,
            execution.RetryDelay, execution.QuarantineAfterFailedAttempts, execution.MaxRetryDelay);
        if (counted && exhausted && !transient)
            // The cause is kept: a limit reached on an answer about the record is a person's (needing_a_person).
            return await Stuck(claim, code == ReleaseConflict ? StuckOnReleaseConflict : StuckOnRecordAnswer, now, ct);
        if (counted && exhausted)
            code = RetryLimitReached;
        return await operations.AdvanceAsync(claim, new(checkpoint, ProductIdentityWorkflowRecoveryDisposition.Retryable,
            now.UtcTicks, next, code, ReleaseLease: true,
            Streak: counted ? RecoveryStreakWrite.Increment : RecoveryStreakWrite.Keep,
            LastStartSentAtUtcTicksV1: startSentAt), ct);
    }

    public static bool IsStuck(LskuRetirementRequestOperation op) =>
        op.Checkpoint == LskuRetirementRequestCheckpoint.ManualReconciliationRequired
            ? op.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired
            : op.LastFailureCode == RetryLimitReached;

    private async Task ReleaseAsync(LskuRetirementRequestOperation op, string leaseOwner, long leaseUntil,
        LskuRetirementRequestExecutionConfiguration execution)
    {
        using var bound = new CancellationTokenSource(ProductIdentityWorkflowClaims.Bound(execution.LeaseDuration));
        try { await operations.ReleaseIfOwnedAsync(op.OperationId, leaseOwner, leaseUntil, bound.Token); }
        catch (Exception exception)
        {
            log.LogWarning("{Flow} {OperationId}: the lease of a step that did not finish could not be given back ({ExceptionType}).",
                "LSKU_RETIREMENT_REQUEST", op.OperationId, exception.GetType().Name);
        }
    }

    private static LskuActiveLifecycleOperationBinding Binding(LskuRetirementRequestOperation op) =>
        new(LskuLifecycleOperationKind.Retirement, op.OperationId, op.BaseLskuVersion);
    private Task<bool> Advance(LskuRetirementRequestClaim claim, LskuRetirementRequestCheckpoint next,
        DateTimeOffset now, CancellationToken ct) => operations.AdvanceAsync(claim, new(next,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks, ReleaseLease: true), ct);
    // STUCK (lock held, counted, the requester's same command is the way out) vs ENDED (nothing held, not active).
    private Task<bool> Stuck(LskuRetirementRequestClaim claim, string? code, DateTimeOffset now,
        CancellationToken ct) => operations.AdvanceAsync(claim, new(
            LskuRetirementRequestCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, now.UtcTicks,
            LastFailureCode: code ?? "LSKU_RETIREMENT_RECONCILIATION_REQUIRED", ReleaseLease: true), ct);
    private Task<bool> Ended(LskuRetirementRequestClaim claim, string? code, DateTimeOffset now,
        CancellationToken ct) => operations.AdvanceAsync(claim, new(
            LskuRetirementRequestCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
            LastFailureCode: code ?? "LSKU_RETIREMENT_RECONCILIATION_REQUIRED", ReleaseLease: true), ct);
    private static LskuRetirementRequestProcessingResult Success(LskuRetirementRequestOperation op, bool replay) =>
        new(true, op, null, op.Checkpoint == LskuRetirementRequestCheckpoint.Completed ? 200 : 202, replay);
    private static LskuRetirementRequestProcessingResult Fail(string code, int status) => new(false, null, code, status, false);
    private static LskuRetirementRequestProcessingResult Fail(LskuRetirementRequestOperation op, string code, int status) =>
        new(false, op, code, status, false);
}
