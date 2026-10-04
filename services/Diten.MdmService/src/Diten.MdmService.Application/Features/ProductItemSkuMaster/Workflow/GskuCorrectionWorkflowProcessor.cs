using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Validators;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record GskuCorrectionProcessingResult(bool Succeeded, GskuCorrectionWorkflowOperation? Operation,
    string? ErrorCode, int StatusCode, bool IsReplay);

public sealed class GskuCorrectionWorkflowProcessor(
    IGskuCorrectionWorkflowOperationRepository operations,
    IGskuRepository gskus,
    IProductDefinitionRevisionRepository revisions,
    IProductIdentityWorkflowClient workflow,
    IWorkflowVerifiedGskuReferenceResolver references,
    GskuCorrectionWorkflowStartRequestFactory factory,
    TimeProvider clock,
    ILogger<GskuCorrectionWorkflowProcessor>? logger = null)
{
    private readonly ILogger log = logger ?? NullLogger<GskuCorrectionWorkflowProcessor>.Instance;
    public const string SelfApprovalIgnored = "GSKU_CORRECTION_SELF_APPROVAL_IGNORED";
    public const string NotApplicable = "GSKU_CORRECTION_NOT_APPLICABLE";

    public async Task<GskuCorrectionProcessingResult> StartInteractiveAsync(Guid tenantId, Guid gskuId,
        int expectedVersion, Guid operationId, Guid makerId, decimal quantity, string uomCode,
        string delegatedToken, GskuCorrectionExecutionConfiguration execution, CancellationToken ct)
    {
        if (tenantId == Guid.Empty || gskuId == Guid.Empty || operationId == Guid.Empty || makerId == Guid.Empty
            || expectedVersion < 0 || quantity <= 0 || string.IsNullOrWhiteSpace(uomCode)
            || string.IsNullOrWhiteSpace(delegatedToken)) return Fail("GSKU_CORRECTION_REQUEST_INVALID", 400);
        if (!CreateFirstGskuDraftValidator.IsValidPack(quantity, uomCode)) return Fail("GSKU_CORRECTION_FACTS_INVALID", 409);
        var existing = await operations.GetByOperationIdAsync(operationId, ct);
        GskuCorrectionWorkflowOperation operation;
        var replay = existing is not null;
        if (existing is null)
        {
            var gsku = await gskus.GetByIdAsync(gskuId, ct);
            if (gsku is null) return Fail("GSKU_NOT_FOUND", 404);
            if (gsku.Version != expectedVersion) return Fail("GSKU_CONCURRENCY_CONFLICT", 409);
            if (await gskus.FindRetirementBlockerAsync(gskuId, ct) is { } blocker)
                return Fail(blocker, 409);
            var revision = await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, ct);
            if (revision is null) return Fail("GSKU_REVISION_NOT_FOUND", 409);
            VerifiedGskuReferenceResolveResult resolved;
            try { resolved = await references.ResolveLatestAsync(tenantId, "SCALAR_QUANTITY_APPLIES", uomCode, ct); }
            catch (OperationCanceledException) { throw; }
            catch { return Fail("REFERENCE_DATA_CONTRACT_UNAVAILABLE", 503); }
            // An outage of the reference service is not "the facts are invalid": the maker asks again (503).
            if (!resolved.IsSuccessful && resolved.StatusCode is 503 or 504)
                return Fail(resolved.FailureCode ?? "REFERENCE_DATA_CONTRACT_UNAVAILABLE", 503);
            GskuCorrectionStartPlan plan;
            try { plan = factory.Create(gsku, revision.GlobalProductId, operationId, makerId, quantity, uomCode, resolved); }
            catch (InvalidOperationException e) { return Fail(e.Message, e.Message.Contains("CONFIGURATION") ? 503 : 409); }
            var reserve = await operations.ReserveAsync(plan.Operation, ct);
            if (!reserve.Succeeded || reserve.Operation is null)
                return Fail(reserve.ErrorCode ?? "GSKU_CORRECTION_OPERATION_CONFLICT", 409);
            operation = reserve.Operation; replay = reserve.IsReplay;
        }
        else operation = existing;
        if (operation.TenantId != tenantId || operation.GskuId != gskuId
            || operation.BaseGskuVersion != expectedVersion || operation.MakerSubjectId != makerId
            || operation.ProposedPackQuantity != quantity || operation.ProposedPackUomCode != uomCode
            || !factory.MatchesConfiguration(operation)) return Fail("GSKU_CORRECTION_OPERATION_CONFLICT", 409);
        var result = await ProcessAsync(operation, delegatedToken, $"interactive-{makerId:N}", execution, true, ct);
        return result with { IsReplay = replay || result.IsReplay };
    }

    public Task<GskuCorrectionProcessingResult> RecoverAsync(GskuCorrectionWorkflowOperation operation,
        string leaseOwner, GskuCorrectionExecutionConfiguration execution, CancellationToken ct) =>
        ProcessAsync(operation, null, leaseOwner, execution, false, ct);

    private async Task<GskuCorrectionProcessingResult> ProcessAsync(GskuCorrectionWorkflowOperation operation,
        string? token, string leaseOwner, GskuCorrectionExecutionConfiguration execution,
        bool stopAtPending, CancellationToken ct)
    {
        execution = execution with { Interactive = token is not null };
        var reconciling = false;
        for (var step = 0; step < 9; step++)
        {
            ct.ThrowIfCancellationRequested();
            if (operation.Checkpoint == GskuCorrectionWorkflowCheckpoint.Completed) return Success(operation, true);
            var reconcile = false;
            if (operation.Checkpoint == GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired)
            {
                // FIX4 item 1 (b): a request STUCK in manual reconciliation (the GSKU's lock held) has a way out — its
                // requester's same command reads the engine again (ReconcileAsync). One that ENDED (lock given back)
                // only answers why it ended. A repeat that could not settle it says so: a person has to (BL-530).
                if (token is null || step > 0
                    || operation.RecoveryDisposition != ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
                    return Fail(operation, reconciling && operation.RecoveryDisposition
                            == ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired
                        ? ManualReconciliationNeeded
                        : operation.LastFailureCode ?? "GSKU_CORRECTION_RECONCILIATION_REQUIRED", 409);
                reconcile = reconciling = true;
            }
            if (stopAtPending && !reconciling && operation.Checkpoint == GskuCorrectionWorkflowCheckpoint.AwaitingDecision)
                return Success(operation, false);
            var now = clock.GetUtcNow();
            // FIX4 item 2: a maker's repeat before the worker's next attempt is due is answered without a claim — it
            // neither jumps the back-off nor adds to the streak.
            if (token is not null && !reconcile
                && operation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable
                && operation.NextAttemptAtUtcTicksV1 > now.UtcTicks)
                return Fail(operation, operation.LastFailureCode ?? "GSKU_CORRECTION_RETRY_SCHEDULED", 503);
            var leaseUntil = now.Add(execution.LeaseDuration).UtcTicks;
            // FIX2 item 9: the shared bounded claim (lease/2, give back a claim the store may have written, 503).
            var (claim, storeSilent) = await ProductIdentityWorkflowClaims.ClaimAsync(
                bound => operations.TryClaimAsync(operation.OperationId, operation.OperationFingerprint,
                    [operation.Checkpoint], leaseOwner, now.UtcTicks, leaseUntil, bound),
                bound => operations.ReleaseIfOwnedAsync(operation.OperationId, leaseOwner, leaseUntil, bound),
                execution.LeaseDuration,
                exception => log.LogWarning("{Flow} {OperationId}: a claim the store did not answer could not be given back ({ExceptionType}).",
                    "GSKU_CORRECTION", operation.OperationId, exception.GetType().Name));
            if (storeSilent) return Fail(operation, ProductIdentityWorkflowClaims.StoreUnavailable, 503);
            if (claim is null) return Fail(operation, "GSKU_CORRECTION_BUSY", 409);
            bool advanced;
            try
            {
            advanced = operation.Checkpoint switch
            {
                GskuCorrectionWorkflowCheckpoint.Prepared => await PrepareAsync(operation, claim, token, now, execution, ct),
                GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired when reconcile
                    => await ReconcileAsync(operation, claim, now, execution, ct),
                // FIX4 item 10: FIX2 parked a Prepared request the worker picked up in AwaitingMakerReplay, holding the
                // GSKU's lock; the worker now settles it as it does a Prepared one (FIX3 S3).
                GskuCorrectionWorkflowCheckpoint.AwaitingMakerReplay when token is null
                    => await PreparedWithoutMakerAsync(operation, claim, now, execution, ct),
                GskuCorrectionWorkflowCheckpoint.StartOutcomeUnknown or GskuCorrectionWorkflowCheckpoint.AwaitingMakerReplay
                    => await RecoverStartAsync(operation, claim, token, now, execution, ct),
                GskuCorrectionWorkflowCheckpoint.WorkflowStarted => await Advance(claim,
                    GskuCorrectionWorkflowCheckpoint.AwaitingDecision, now, release: true, ct: ct),
                GskuCorrectionWorkflowCheckpoint.AwaitingDecision => await ObserveAsync(operation, claim, now, execution, ct),
                GskuCorrectionWorkflowCheckpoint.DecisionObserved => await ApplyAsync(operation, claim, now, execution, ct),
                GskuCorrectionWorkflowCheckpoint.DecisionApplied => await Advance(claim,
                    GskuCorrectionWorkflowCheckpoint.Completed, now, release: true, ct: ct),
                _ => false
            };
            }
            catch
            {
                // FIX3 item 2 (B14): a step that throws (the caller cancelled, the store or the engine failed half-way)
                // gives its lease back — bounded, not under the caller's token — or the next attempt is "busy" until it runs out.
                await ReleaseAsync(operation, leaseOwner, leaseUntil, execution);
                throw;
            }
            if (!advanced)
            {
                await ReleaseAsync(operation, leaseOwner, leaseUntil, execution);
                return Fail(operation, "GSKU_CORRECTION_CONCURRENCY_CONFLICT", 409);
            }
            operation = await operations.GetByOperationIdAsync(operation.OperationId, ct)
                ?? throw new InvalidOperationException("GSKU_CORRECTION_OPERATION_LOST");
            // The reconciling repeat found the approval still open: it waits there, and says so (202).
            // (An open approval is looked at again later without a fault — disposition None, a next look set — whatever
            // code the engine gave for "not yet", e.g. WORKFLOW_DECISION_NOT_TERMINAL.)
            if (reconciling && operation.Checkpoint == GskuCorrectionWorkflowCheckpoint.AwaitingDecision
                && operation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.None
                && operation.NextAttemptAtUtcTicksV1 > clock.GetUtcNow().UtcTicks)
                return Success(operation, false);
            if (operation.NextAttemptAtUtcTicksV1 > clock.GetUtcNow().UtcTicks)
                return Fail(operation, operation.LastFailureCode ?? "GSKU_CORRECTION_RETRY_SCHEDULED", 503);
        }
        return Fail(operation, "GSKU_CORRECTION_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<bool> PrepareAsync(GskuCorrectionWorkflowOperation op, GskuCorrectionWorkflowClaim claim,
        string? token, DateTimeOffset now, GskuCorrectionExecutionConfiguration execution, CancellationToken ct)
    {
        if (token is null)
            // FIX3 S3: the worker never starts a correction (only the maker's token can), and FIX2 parked a Prepared
            // one it picked up in AwaitingMakerReplay holding the GSKU's lock — a state the worker skipped and the screen
            // hid every action for. Now: did a start of the maker's own request arrive? Then it goes on as started;
            // if nothing arrived (and the settle window has passed) the GSKU is given back and the request ends.
            return await PreparedWithoutMakerAsync(op, claim, now, execution, ct);
        var gsku = await gskus.GetByIdAsync(op.GskuId, ct);
        if (gsku is null) return await Ended(claim, "GSKU_NOT_FOUND", now, ct);
        var admitted = await AcquireAsync(op, gsku, now, ct);
        // Not admitted (the GSKU busy with another request, its version moved): nothing was taken — the request ends.
        if (!admitted.Succeeded) return await Ended(claim, admitted.ErrorCode, now, ct);
        var started = await workflow.StartAsync(op.TenantId, factory.Rehydrate(op), token, ct);
        return await RecordStart(op, claim, started, fromStart: true, now, execution, ct);
    }

    private Task<GskuCorrectionWriteResult> AcquireAsync(GskuCorrectionWorkflowOperation op, Gsku gsku,
        DateTimeOffset now, CancellationToken ct) =>
        gskus.AcquireCorrectionAsync(gsku.Id, op.BaseGskuVersion, Binding(op),
            GskuCorrectionAuditIntentFactory.Create(gsku, op.BaseGskuVersion, op.OperationId,
                op.MakerSubjectId, ProductAuditOperation.GskuCorrectionRequested,
                op.ProposedPackQuantity, op.ProposedPackUomCode,
                op.ProposedPackApplicabilitySelection, op.ProposedPackUomSelection, now), ct);

    private async Task<bool> PreparedWithoutMakerAsync(GskuCorrectionWorkflowOperation op,
        GskuCorrectionWorkflowClaim claim, DateTimeOffset now, GskuCorrectionExecutionConfiguration execution,
        CancellationToken ct)
    {
        var gsku = await gskus.GetByIdAsync(op.GskuId, ct);
        if (gsku is null) return await Ended(claim, "GSKU_NOT_FOUND", now, ct);
        var found = await workflow.GetStartResultAsync(op.TenantId,
            new(op.ObjectType, op.ObjectId, op.MakerSubjectId, op.StartIdempotencyKey), ct);
        if (found.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
        {
            // The maker's start went through before its request was cut off: the lock is taken (an exact repeat if
            // it already was) and the correction goes on as started.
            var admitted = await AcquireAsync(op, gsku, now, ct);
            if (!admitted.Succeeded) return await Stuck(claim, admitted.ErrorCode, now, ct);
            return await RecordStart(op, claim, found, fromStart: false, now, execution, ct);
        }
        if (found.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound)
            return Settling(op, now, execution)
                ? await RetryAsync(op, claim, op.Checkpoint, StartSettling, now, execution, ct, transient: true)
                : await EndAndReleaseAsync(op, claim, "GSKU_CORRECTION_START_NOT_RECEIVED", now, execution, ct);
        return await RetryAsync(op, claim, op.Checkpoint, found.ErrorCode ?? "GSKU_CORRECTION_START_LOOKUP_FAILED",
            now, execution, ct, transient: IsTransient(found.Outcome));
    }

    /*
     * FIX4 item 1 (b) — the requester's same command on a request STUCK in manual reconciliation (the GSKU's lock held).
     * It reads the engine again and puts the request back where the engine and the GSKU say it stands; the ordinary
     * steps take it from there:
     *   decision on record       → its application is tried again;
     *   an approval on record    → back to waiting for its decision — read in this same request (decided → applied;
     *                              closed without a decision / evidence unusable → the request ends, the GSKU is freed;
     *                              still open → it waits, 202);
     *   no approval on record    → was the start received? yes → started; no → the request ends, the GSKU is freed;
     *   the engine cannot answer → said as such (503); nothing changes.
     */
    private async Task<bool> ReconcileAsync(GskuCorrectionWorkflowOperation op, GskuCorrectionWorkflowClaim claim,
        DateTimeOffset now, GskuCorrectionExecutionConfiguration execution, CancellationToken ct)
    {
        if (op.DecisionKind.HasValue && op.DecisionActorSubjectId.HasValue)
            return await Advance(claim, GskuCorrectionWorkflowCheckpoint.DecisionObserved, now, release: true, ct: ct);
        if (op.WorkflowInstanceId.HasValue)
            return await Advance(claim, GskuCorrectionWorkflowCheckpoint.AwaitingDecision, now, release: true, ct: ct);
        var found = await workflow.GetStartResultAsync(op.TenantId,
            new(op.ObjectType, op.ObjectId, op.MakerSubjectId, op.StartIdempotencyKey), ct);
        if (found.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
            return await RecordStart(op, claim, found, fromStart: false, now, execution, ct);
        if (found.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound)
            return await EndAndReleaseAsync(op, claim, "GSKU_CORRECTION_START_NOT_RECEIVED", now, execution, ct);
        // The engine could not be asked: the request stays as it is; the next repeat (or a person) asks again.
        return await operations.AdvanceAsync(claim, new(op.Checkpoint, op.RecoveryDisposition, now.UtcTicks,
            LastFailureCode: op.LastFailureCode, ReleaseLease: true, Streak: RecoveryStreakWrite.Keep), ct);
    }

    public const string ManualReconciliationNeeded = "GSKU_CORRECTION_MANUAL_RECONCILIATION_NEEDED";

    // FIX4 item 1 (a): an engine or Platform that cannot be reached, a timeout, a 5xx — never quarantined.
    private static bool IsTransient(ProductIdentityWorkflowTransportOutcome outcome) =>
        outcome is ProductIdentityWorkflowTransportOutcome.Timeout or ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Incomplete;

    // FIX3 S8: a start sent less than the settle window ago may still be in the engine's hands.
    private static bool Settling(GskuCorrectionWorkflowOperation op, DateTimeOffset now,
        GskuCorrectionExecutionConfiguration execution) =>
        now.UtcTicks - (op.LastStartSentAtUtcTicksV1 ?? op.UpdatedAtUtcTicksV1) < execution.SettleWindow.Ticks;

    public const string StartSettling = "GSKU_CORRECTION_START_SETTLING";

    private async Task<bool> RecoverStartAsync(GskuCorrectionWorkflowOperation op, GskuCorrectionWorkflowClaim claim,
        string? token, DateTimeOffset now, GskuCorrectionExecutionConfiguration execution, CancellationToken ct)
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

    private async Task<bool> RecordStart(GskuCorrectionWorkflowOperation op, GskuCorrectionWorkflowClaim claim,
        ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult> result, bool fromStart,
        DateTimeOffset now, GskuCorrectionExecutionConfiguration execution, CancellationToken ct)
    {
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.Success && result.Value is { } v
            && v.WorkflowInstanceId != Guid.Empty && v.TemplateId != Guid.Empty && v.TemplateVersionId != Guid.Empty
            && v.ApprovalTaskId != Guid.Empty && v.AssignmentSnapshotId != Guid.Empty
            && v.StartTransitionLogId != Guid.Empty && v.ObjectRef == op.ObjectRef)
            return await operations.AdvanceAsync(claim, new(GskuCorrectionWorkflowCheckpoint.WorkflowStarted,
                ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks, WorkflowInstanceId: v.WorkflowInstanceId,
                WorkflowTemplateId: v.TemplateId, WorkflowTemplateVersionId: v.TemplateVersionId,
                ApprovalTaskId: v.ApprovalTaskId, AssignmentSnapshotId: v.AssignmentSnapshotId,
                StartTransitionLogId: v.StartTransitionLogId,
                WorkflowStartedAtUtcTicksV1: (v.StartedAt ?? now).UtcTicks, ReleaseLease: true), ct);
        // FIX2 items 4 and 5: the lock is given back only when the engine is known to hold nothing for this request.
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound && !fromStart && Settling(op, now, execution))
            // FIX3 S8: "nothing under this key" right after a start timed out may only mean the engine is still on it:
            // the lock stays until the settle window has passed.
            return await RetryAsync(op, claim, GskuCorrectionWorkflowCheckpoint.StartOutcomeUnknown, StartSettling,
                now, execution, ct, transient: true);
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound)
            // From the start itself: no such definition — nothing was started (the set-up is missing). From the
            // lookup: the engine has nothing under this request's key — nothing arrived. Either way the GSKU is free.
            return await EndAndReleaseAsync(op, claim, fromStart
                ? ProductIdentityWorkflowMakerCodes.DefinitionNotReady
                : "GSKU_CORRECTION_START_NOT_RECEIVED", now, execution, ct);
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.Timeout or ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Incomplete or ProductIdentityWorkflowTransportOutcome.Success)
            // The answer did not arrive, or arrived and cannot be used: the engine may have started an approval —
            // the GSKU keeps its lock and the worker asks the engine again.
            return await StartUnknownAsync(op, claim, result.ErrorCode
                ?? (result.Outcome == ProductIdentityWorkflowTransportOutcome.Success ? "GSKU_CORRECTION_START_EVIDENCE_INVALID" : null),
                now, execution, ct, fromStart, transient: IsTransient(result.Outcome));
        var fault = ProductIdentityWorkflowFaultPolicy.Classify(
            ProductIdentityWorkflowCall.Start, result.Outcome, result.ErrorCode);
        if (!fault.StartedNothing)
            // The engine may have done it (an unreadable or invalid answer): the lock stays, the start is looked up.
            return await StartUnknownAsync(op, claim, result.ErrorCode, now, execution, ct, fromStart);
        // Refused before anything was processed (the shared classification): nothing exists at the engine, so the
        // GSKU is given back and the reason recorded; the maker learns it from the answer.
        return await EndAndReleaseAsync(op, claim, fault.MakerCode ?? result.ErrorCode ?? "GSKU_CORRECTION_START_REFUSED", now, execution, ct);
    }

    private Task<bool> StartUnknownAsync(GskuCorrectionWorkflowOperation op, GskuCorrectionWorkflowClaim claim,
        string? code, DateTimeOffset now, GskuCorrectionExecutionConfiguration execution, CancellationToken ct,
        bool startSent = false, bool transient = false)
    {
        if (code is not null && code.EndsWith("_INVALID", StringComparison.Ordinal))
            log.LogError("GSKU correction {OperationId} of tenant {TenantId}: the engine's start answer cannot be used ({ResultCode}); the lock is kept and the start is looked up again.",
                op.OperationId, op.TenantId, code);
        return RetryAsync(op, claim, GskuCorrectionWorkflowCheckpoint.StartOutcomeUnknown, code, now, execution, ct,
            transient, startSentAt: startSent ? now.UtcTicks : null);
    }

    // B3: the correction ends without its decision applied; the GSKU's lock goes in the same write as the intent
    // that says why. If giving it back fails for a reason that may pass (the cap, the store), the operation waits.
    private async Task<bool> EndAndReleaseAsync(GskuCorrectionWorkflowOperation op, GskuCorrectionWorkflowClaim claim,
        string reason, DateTimeOffset now, GskuCorrectionExecutionConfiguration execution, CancellationToken ct)
    {
        var gsku = await gskus.GetByIdAsync(op.GskuId, ct);
        if (gsku?.ActiveLifecycleOperation is { } held && held == Binding(op))
        {
            var audit = GskuCorrectionAuditIntentFactory.Create(gsku, checked(op.BaseGskuVersion + 1), op.OperationId,
                op.MakerSubjectId, ProductAuditOperation.GskuCorrectionManualReconciliationRequired,
                op.ProposedPackQuantity, op.ProposedPackUomCode, op.ProposedPackApplicabilitySelection,
                op.ProposedPackUomSelection, now);
            var released = await gskus.ReleaseLifecycleOperationAsync(gsku.Id, checked(op.BaseGskuVersion + 1),
                Binding(op), audit, ct);
            if (!released.Succeeded)
                // The cap, the store: giving the lock back waits, it is never quarantined (FIX4 item 1 (a)).
                return await RetryAsync(op, claim, op.Checkpoint, released.ErrorCode, now, execution, ct, transient: true);
        }
        // FIX4 item 4: a request that ended, its lock given back, is not "stuck": information, not an error.
        log.LogInformation("GSKU correction {OperationId} of tenant {TenantId} ended without its decision applied ({ResultCode}); the GSKU is free again.",
            op.OperationId, op.TenantId, reason);
        return await Ended(claim, reason, now, ct);
    }

    private async Task<bool> ObserveAsync(GskuCorrectionWorkflowOperation op, GskuCorrectionWorkflowClaim claim,
        DateTimeOffset now, GskuCorrectionExecutionConfiguration execution, CancellationToken ct)
    {
        if (!op.WorkflowInstanceId.HasValue) return await Stuck(claim, "GSKU_CORRECTION_START_EVIDENCE_INVALID", now, ct);
        var result = await workflow.GetTerminalEvidenceAsync(op.TenantId,
            new(op.WorkflowInstanceId.Value, op.ObjectType, op.ObjectId), ct);
        // An approval still open is not a failure: the streak is over (FIX3 item 1).
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.NonTerminal)
            return await operations.AdvanceAsync(claim, new(op.Checkpoint, op.RecoveryDisposition, now.UtcTicks,
                now.Add(execution.RetryDelay).UtcTicks, result.ErrorCode ?? "GSKU_CORRECTION_DECISION_PENDING", ReleaseLease: true), ct);
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.NotFound
            or ProductIdentityWorkflowTransportOutcome.Incomplete or ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Timeout)
            return await RetryAsync(op, claim, op.Checkpoint, result.ErrorCode ?? "GSKU_CORRECTION_DECISION_PENDING", now, execution, ct,
                transient: IsTransient(result.Outcome));
        if (result.Outcome != ProductIdentityWorkflowTransportOutcome.Success
            && ProductIdentityWorkflowFaultPolicy.Classify(ProductIdentityWorkflowCall.TerminalEvidence, result.Outcome,
                result.ErrorCode).Kind == ProductIdentityWorkflowFaultKind.ServiceWait)
            // The service's identity or reachability: the chain's fault, not the record's — it waits, never quarantined.
            return await RetryAsync(op, claim, op.Checkpoint, result.ErrorCode, now, execution, ct, transient: true);
        var e = result.Value;
        if (e is not null && Guid.TryParse(e.ActorUserId, out var self) && self == op.MakerSubjectId)
        {
            // B8, FIX2 item 3: the maker decided their own request and the engine has closed it — no other decision
            // will come. The request ends without being applied and the GSKU is given back in the same write as the
            // intent that says why (FIX1 kept waiting on a closed approval, with the GSKU locked for good).
            log.LogWarning("GSKU correction {OperationId} of tenant {TenantId} was decided by its own maker; the decision is not applied ({ResultCode}).",
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
            // The approval is decided (terminal evidence) and its evidence cannot be used: nothing more can come of
            // it — the GSKU is given back (B3).
            // FIX2 item 5 (a): closed without a decision this service can apply (cancelled by an administrator,
            // evidence that contradicts the request): nothing more can come of it — the GSKU is given back. An answer
            // that is not evidence at all is the engine's fault and is asked again (the lock stays).
            // FIX4 (CT decision 3 corrected): Platform does not name "closed without a decision" yet (BL-547) — it
            // answers WORKFLOW_TERMINAL_EVIDENCE_INCONSISTENT for a cancellation in the Task Center and for a timeout
            // alike. Quarantining that turned every such cancellation into a GSKU locked for good. Back to FIX2: any
            // terminal evidence this service cannot apply ends the request and frees the GSKU (the identity flow keeps
            // its quarantine, FIX3). An answer that is not evidence at all is asked again.
            return e is null && result.Outcome != ProductIdentityWorkflowTransportOutcome.Conflict
                ? await RetryAsync(op, claim, op.Checkpoint,
                    result.ErrorCode ?? "GSKU_CORRECTION_EVIDENCE_UNREADABLE", now, execution, ct)
                : await EndAndReleaseAsync(op, claim, e is null
                    ? "GSKU_CORRECTION_CLOSED_WITHOUT_DECISION" : "GSKU_CORRECTION_EVIDENCE_CONFLICT", now, execution, ct);
        return await operations.AdvanceAsync(claim, new(GskuCorrectionWorkflowCheckpoint.DecisionObserved,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
            DecisionKind: approved ? ProductIdentityDecisionKind.Approved : ProductIdentityDecisionKind.Rejected,
            DecisionActorSubjectId: actor, DecisionReasonCode: e.ReasonCode, DecisionAtUtcTicksV1: e.DecisionAt.UtcTicks,
            DecisionTransitionSequence: e.TransitionSequence, DecisionTaskStatus: e.TaskStatus,
            DecisionInstanceStatus: e.InstanceStatus, ReleaseLease: true), ct);
    }

    private async Task<bool> ApplyAsync(GskuCorrectionWorkflowOperation op, GskuCorrectionWorkflowClaim claim,
        DateTimeOffset now, GskuCorrectionExecutionConfiguration execution, CancellationToken ct)
    {
        var gsku = await gskus.GetByIdAsync(op.GskuId, ct);
        if (gsku is null || !op.DecisionActorSubjectId.HasValue)
            return await Stuck(claim, "GSKU_CORRECTION_GSKU_DRIFT", now, ct);
        var approved = op.DecisionKind == ProductIdentityDecisionKind.Approved;
        ReferenceCatalogSelection? applicability = null, uom = null;
        // FIX2 item 11: a repeat after the decision was already written uses what was written (the resolution stored on
        // the GSKU), not a new resolution — a catalog version that moved in between would change the evidence and the
        // repeat would conflict with its own earlier write for ever.
        var appliedKey = $"gsku-correction:{op.OperationId:D}:{ProductAuditOperation.GskuCorrectionApplied}";
        var alreadyApplied = approved && gsku.ActiveLifecycleOperation is null
            && (gsku.AuditIntents.Any(x => x.IdempotencyKey == appliedKey)
                || gsku.AuditIntentReceipts.Any(x => x.IdempotencyKey == appliedKey));
        if (alreadyApplied)
        {
            applicability = gsku.PackApplicabilitySelection;
            uom = gsku.PackUomSelection;
        }
        else if (approved)
        {
            if (await gskus.FindRetirementBlockerAsync(op.GskuId, ct) is { } blocker)
                return await EndAndReleaseAsync(op, claim, blocker, now, execution, ct);
            VerifiedGskuReferenceResolveResult resolved;
            try
            {
                resolved = await references.ResolveLatestAsync(op.TenantId,
                    "SCALAR_QUANTITY_APPLIES", op.ProposedPackUomCode, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch { resolved = VerifiedGskuReferenceResolveResult.Fail(503, "REFERENCE_DATA_CONTRACT_UNAVAILABLE"); }
            (applicability, uom) = GskuCorrectionWorkflowStartRequestFactory.ResolveSelections(resolved, op.ProposedPackUomCode);
            if (applicability is null || uom is null)
                // An outage (503 / 504) or a refused service identity waits; a value that is no longer selectable
                // will not come back by waiting: the correction is not applicable and the GSKU is given back.
                // FIX3 S6: only a value that is no longer selectable (or retired) ends the correction unapplied, as the
                // first gate of the identity flow does (FIX2 item 2). Anything else — an outage, a refused service
                // identity, a contract mismatch between the two services — says nothing about the approved values: it
                // waits (and after the streak's limit is handed to manual reconciliation, B13), the approval kept.
                return !resolved.IsSuccessful && resolved.FailureCode == VerifiedGskuReferenceResolveResult.NotSelectable
                    ? await EndAndReleaseAsync(op, claim, NotApplicable, now, execution, ct)
                    : await RetryAsync(op, claim, op.Checkpoint,
                        resolved.FailureCode ?? "REFERENCE_DATA_CONTRACT_UNAVAILABLE", now, execution, ct,
                        transient: resolved.StatusCode is 503 or 504 or 401 or 403);
        }
        var expected = checked(op.BaseGskuVersion + 1);
        var audit = GskuCorrectionAuditIntentFactory.Create(gsku, expected, op.OperationId,
            op.DecisionActorSubjectId.Value, approved ? ProductAuditOperation.GskuCorrectionApplied
                : ProductAuditOperation.GskuCorrectionRejected, op.ProposedPackQuantity, op.ProposedPackUomCode,
            applicability ?? op.ProposedPackApplicabilitySelection, uom ?? op.ProposedPackUomSelection,
            op.DecisionAtUtcTicksV1.HasValue ? new(op.DecisionAtUtcTicksV1.Value, TimeSpan.Zero) : now);
        var result = await gskus.ApplyCorrectionDecisionAsync(gsku.Id, expected, Binding(op),
            approved ? op.ProposedPackQuantity : null, approved ? op.ProposedPackUomCode : null,
            applicability, uom, audit, ct);
        if (!result.Succeeded)
            // A blocker that appeared after the check above ends it like the check would have; anything else is a
            // write that may still go through (the cap, the store, a concurrent write) and is tried again.
            return result.ErrorCode is "DEPENDENT_IDENTITIES_EXIST" or "GSKU_CHILD_CREATION_IN_PROGRESS"
                ? await EndAndReleaseAsync(op, claim, result.ErrorCode, now, execution, ct)
                : await RetryAsync(op, claim, op.Checkpoint, result.ErrorCode, now, execution, ct, transient: true);
        return await Advance(claim, GskuCorrectionWorkflowCheckpoint.DecisionApplied, now, release: true, ct: ct);
    }

    /*
     * FIX3 item 1 (B13) + FIX4 items 1 (a), 2, 3: every retry goes through here. Only the WORKER's attempts count (a
     * maker's repeat leaves the streak as it is), in the store ($inc). After the configured number in a row:
     *   a transient cause (the engine or Platform unreachable, a timeout, a 5xx, the store) is NEVER quarantined — it
     *   is said (an error line, RETRY_LIMIT_REACHED on the record, counted by the gauge) and goes on retrying at the
     *   ceiling, and heals by itself when the service is back;
     *   any other cause (an answer that cannot be used, a contract mismatch) is handed to manual reconciliation with
     *   the lock held — and its requester's same command is its way out (ReconcileAsync).
     */
    private async Task<bool> RetryAsync(GskuCorrectionWorkflowOperation op, GskuCorrectionWorkflowClaim claim,
        GskuCorrectionWorkflowCheckpoint checkpoint, string? code, DateTimeOffset now,
        GskuCorrectionExecutionConfiguration execution, CancellationToken ct, bool transient = false,
        long? startSentAt = null)
    {
        var counted = !execution.Interactive;
        var (exhausted, attempts, next) = ProductIdentityRecoveryStreak.Next(
            counted ? op.FailedAttemptCount : Math.Max(op.FailedAttemptCount - 1, 0), now,
            execution.RetryDelay, execution.QuarantineAfterFailedAttempts, execution.MaxRetryDelay);
        if (counted && exhausted && !transient)
        {
            log.LogError("GSKU correction {OperationId} of tenant {TenantId} failed {Attempts} attempts in a row (last {ResultCode}); it is handed to manual reconciliation, the GSKU's lock kept.",
                op.OperationId, op.TenantId, attempts, code);
            return await Stuck(claim, RetryLimitReached, now, ct);
        }
        if (counted && exhausted)
        {
            if (attempts == execution.QuarantineAfterFailedAttempts)
                log.LogError("GSKU correction {OperationId} of tenant {TenantId} failed {Attempts} attempts in a row (last {ResultCode}); a service it needs does not answer — it keeps retrying at the ceiling.",
                    op.OperationId, op.TenantId, attempts, code);
            code = RetryLimitReached;
        }
        return await operations.AdvanceAsync(claim, new(checkpoint, ProductIdentityWorkflowRecoveryDisposition.Retryable,
            now.UtcTicks, next, code, ReleaseLease: true,
            Streak: counted ? RecoveryStreakWrite.Increment : RecoveryStreakWrite.Keep,
            LastStartSentAtUtcTicksV1: startSentAt), ct);
    }

    public const string RetryLimitReached = "GSKU_CORRECTION_RETRY_LIMIT_REACHED";

    private async Task ReleaseAsync(GskuCorrectionWorkflowOperation op, string leaseOwner, long leaseUntil,
        GskuCorrectionExecutionConfiguration execution)
    {
        using var bound = new CancellationTokenSource(ProductIdentityWorkflowClaims.Bound(execution.LeaseDuration));
        try { await operations.ReleaseIfOwnedAsync(op.OperationId, leaseOwner, leaseUntil, bound.Token); }
        catch (Exception exception)
        {
            log.LogWarning("{Flow} {OperationId}: the lease of a step that did not finish could not be given back ({ExceptionType}).",
                "GSKU_CORRECTION", op.OperationId, exception.GetType().Name);
        }
    }

    private Task<bool> Advance(GskuCorrectionWorkflowClaim claim, GskuCorrectionWorkflowCheckpoint next,
        DateTimeOffset now, string? error = null, bool release = false, CancellationToken ct = default) =>
        operations.AdvanceAsync(claim, new(next, ProductIdentityWorkflowRecoveryDisposition.None,
            now.UtcTicks, LastFailureCode: error, ReleaseLease: release), ct);
    // FIX4 item 4: STUCK — handed to a person with the GSKU's lock held (counted by the gauge; its requester's same
    // command is its way out).
    private Task<bool> Stuck(GskuCorrectionWorkflowClaim claim, string? code, DateTimeOffset now, CancellationToken ct) =>
        operations.AdvanceAsync(claim, new(GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, now.UtcTicks,
            LastFailureCode: code ?? "GSKU_CORRECTION_RECONCILIATION_REQUIRED", ReleaseLease: true), ct);
    // ENDED — the request is over, nothing held (the record keeps why): not counted, not an error.
    private Task<bool> Ended(GskuCorrectionWorkflowClaim claim, string? code, DateTimeOffset now, CancellationToken ct) =>
        operations.AdvanceAsync(claim, new(GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
            LastFailureCode: code ?? "GSKU_CORRECTION_RECONCILIATION_REQUIRED", ReleaseLease: true), ct);
    private static GskuActiveLifecycleOperationBinding Binding(GskuCorrectionWorkflowOperation op) =>
        new(GskuLifecycleOperationKind.Correction, op.OperationId, op.BaseGskuVersion);
    private static GskuCorrectionProcessingResult Success(GskuCorrectionWorkflowOperation op, bool replay) =>
        new(true, op, null, op.Checkpoint == GskuCorrectionWorkflowCheckpoint.AwaitingDecision ? 202 : 200, replay);
    private static GskuCorrectionProcessingResult Fail(string code, int status) => new(false, null, code, status, false);
    private static GskuCorrectionProcessingResult Fail(GskuCorrectionWorkflowOperation op, string code, int status) =>
        new(false, op, code, status, false);
}
