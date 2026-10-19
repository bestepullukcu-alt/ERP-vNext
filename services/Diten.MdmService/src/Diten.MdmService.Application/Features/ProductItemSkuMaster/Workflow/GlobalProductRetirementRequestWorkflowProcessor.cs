using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record GlobalProductRetirementRequestProcessingResult(bool Succeeded,
    GlobalProductRetirementRequestOperation? Operation, string? ErrorCode, int StatusCode, bool IsReplay);
public sealed record GlobalProductRetirementRequestExecutionConfiguration(TimeSpan LeaseDuration, TimeSpan RetryDelay,
    // BL-552 (the GSKU FIX3 rules): worker retries in a row before a cause that is not a service fault is handed to a
    // person, the ceiling of the doubling delay, and how long after a start "nothing arrived" is still not trusted.
    int QuarantineAfterFailedAttempts = ProductIdentityRecoveryStreak.DefaultQuarantineAfterFailedAttempts,
    TimeSpan? MaxRetryDelay = null,
    TimeSpan? StartSettleWindow = null)
{
    public TimeSpan SettleWindow => StartSettleWindow is { } window && window > TimeSpan.Zero ? window : TimeSpan.FromMinutes(2);

    // Set by the processor for a maker's request — its retries do not count in the worker's streak.
    public bool Interactive { get; init; }
}

public sealed class GlobalProductRetirementRequestWorkflowProcessor(
    IGlobalProductRetirementRequestOperationRepository operations,
    IGlobalProductRepository products,
    IProductIdentityWorkflowClient workflowClient,
    GlobalProductRetirementRequestWorkflowStartRequestFactory requestFactory,
    TimeProvider timeProvider,
    ILogger<GlobalProductRetirementRequestWorkflowProcessor>? logger = null)
{
    private readonly ILogger log = logger ?? NullLogger<GlobalProductRetirementRequestWorkflowProcessor>.Instance;
    public async Task<GlobalProductRetirementRequestProcessingResult> StartInteractiveAsync(Guid tenantId,
        Guid productId, int expectedVersion, Guid operationId, Guid makerSubjectId, string requestReason,
        string delegatedToken,
        GlobalProductRetirementRequestExecutionConfiguration execution, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || productId == Guid.Empty || operationId == Guid.Empty
            || makerSubjectId == Guid.Empty || expectedVersion < 0
            || !GlobalProductRetirementRequestWorkflowStartRequestFactory.HasValidReason(requestReason)
            || string.IsNullOrWhiteSpace(delegatedToken))
            return Fail("GLOBAL_PRODUCT_RETIREMENT_REQUEST_INVALID", 400);
        var operation = await operations.GetByOperationIdAsync(operationId, cancellationToken);
        var replay = operation is not null;
        if (operation is null)
        {
            var product = await products.GetByIdAsync(productId, cancellationToken);
            if (product is null) return Fail("PRODUCT_IDENTITY_NOT_FOUND", 404);
            if (product.Version != expectedVersion) return Fail("GLOBAL_PRODUCT_CONCURRENCY_CONFLICT", 409);
            GlobalProductRetirementRequestStartPlan plan;
            try { plan = requestFactory.Create(product, operationId, makerSubjectId, requestReason); }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message, exception.Message.Contains("CONFIGURATION", StringComparison.Ordinal)
                    ? 503 : 409);
            }
            var reserve = await operations.ReserveAsync(plan.Operation, cancellationToken);
            if (!reserve.Succeeded || reserve.Operation is null)
                return Fail(reserve.ErrorCode ?? "GLOBAL_PRODUCT_RETIREMENT_OPERATION_CONFLICT", 409);
            operation = reserve.Operation; replay = reserve.IsReplay;
        }
        if (operation.TenantId != tenantId || operation.GlobalProductId != productId
            || operation.BaseProductVersion != expectedVersion || operation.MakerSubjectId != makerSubjectId)
            return Fail("GLOBAL_PRODUCT_RETIREMENT_OPERATION_CONFLICT", 409);
        if (!string.Equals(operation.RequestReason,
                GlobalProductRetirementRequestWorkflowStartRequestFactory.NormalizeReason(requestReason),
                StringComparison.Ordinal))
            return Fail("GLOBAL_PRODUCT_RETIREMENT_OPERATION_CONFLICT", 409);
        var wasStuck = IsStuck(operation);
        var cause = operation.LastFailureCode;
        var result = await ProcessAsync(operation, delegatedToken, $"interactive-{makerSubjectId:N}",
            execution, true, cancellationToken);
        // BL-552: ONE Error line when a request becomes stuck in its maker's own request (no runner sees it).
        if (!wasStuck && result.Operation is { } after && IsStuck(after))
            log.LogError("Global Product retirement request {OperationId} of tenant {TenantId} is stuck ({ResultCode}); the cause before: {CauseCode}.",
                after.OperationId, after.TenantId, after.LastFailureCode, cause ?? "none");
        return result with { IsReplay = replay || result.IsReplay };
    }

    public Task<GlobalProductRetirementRequestProcessingResult> RecoverAsync(
        GlobalProductRetirementRequestOperation operation,
        string leaseOwner,
        GlobalProductRetirementRequestExecutionConfiguration execution,
        CancellationToken cancellationToken) =>
        ProcessAsync(operation, null, leaseOwner, execution, false, cancellationToken);

    /*
     * BL-552 — the accepted GSKU pattern (WP-MDM-GSKU-ACCEPT-01 FIX1–FIX7) on the Global Product retirement request:
     *   (A) the product's lock is given back only when the engine is known to hold nothing for this request — no
     *       approval without an owner;
     *   (B) no state stays stuck for ever, unseen, without a named way out — every stuck state has its exit (the
     *       requester's same command, or a person, BL-530) and a gauge that counts it.
     * The state ⇔ exit table is in the runbook (product-identity-workflow-recovery).
     */
    private async Task<GlobalProductRetirementRequestProcessingResult> ProcessAsync(
        GlobalProductRetirementRequestOperation initial,
        string? delegatedToken,
        string leaseOwner,
        GlobalProductRetirementRequestExecutionConfiguration execution,
        bool stopAtAwaitingDecision,
        CancellationToken cancellationToken)
    {
        var operation = initial;
        var token = string.IsNullOrWhiteSpace(delegatedToken) ? null : delegatedToken;
        execution = execution with { Interactive = token is not null };
        var reconciling = false;
        var signal = new ReconcileSignal();
        for (var step = 0; step < 9; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation.Checkpoint == GlobalProductRetirementRequestCheckpoint.Completed)
                return Success(operation, true);
            var reconcile = false;
            if (operation.Checkpoint == GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired)
            {
                // A request STUCK (the product's lock held) has a way out — its requester's same command reads the
                // engine again (ReconcileAsync). One that ENDED (lock given back) only answers why it ended. A repeat
                // that could not settle it says so: a person has to (BL-530). The engine could not be asked: 503.
                if (signal.EngineSilent)
                    return Fail(operation, ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, 503);
                if (token is null || step > 0
                    || operation.RecoveryDisposition != ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired)
                {
                    var code = reconciling && operation.RecoveryDisposition
                            == ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired
                        ? ManualReconciliationNeeded
                        : operation.LastFailureCode ?? "GLOBAL_PRODUCT_RETIREMENT_RECONCILIATION_REQUIRED";
                    // BL-552 FIX1 item 11: an ended or stuck request answers with the status its reason has.
                    return Fail(operation, code, StatusOf(code));
                }
                reconcile = reconciling = true;
            }
            if (stopAtAwaitingDecision && !reconciling
                && operation.Checkpoint == GlobalProductRetirementRequestCheckpoint.AwaitingDecision)
                return Success(operation, false);
            var now = timeProvider.GetUtcNow();
            // A maker's repeat before the worker's next attempt is due is answered without a claim — it neither jumps
            // the back-off nor adds to the streak.
            if (token is not null && !reconcile
                && operation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable
                && operation.NextAttemptAtUtcTicksV1 > now.UtcTicks)
                return Fail(operation, operation.LastFailureCode ?? "GLOBAL_PRODUCT_RETIREMENT_RETRY_SCHEDULED", 503);
            var leaseUntil = now.Add(execution.LeaseDuration).UtcTicks;
            var (claim, storeSilent) = await ProductIdentityWorkflowClaims.ClaimAsync(
                bound => operations.TryClaimAsync(operation.OperationId, operation.OperationFingerprint,
                    [operation.Checkpoint], leaseOwner, now.UtcTicks, leaseUntil, bound),
                bound => operations.ReleaseIfOwnedAsync(operation.OperationId, leaseOwner, leaseUntil, bound),
                execution.LeaseDuration,
                exception => log.LogWarning("{Flow} {OperationId}: a claim the store did not answer could not be given back ({ExceptionType}).",
                    "GLOBAL_PRODUCT_RETIREMENT", operation.OperationId, exception.GetType().Name));
            if (storeSilent) return Fail(operation, ProductIdentityWorkflowClaims.StoreUnavailable, 503);
            if (claim is null) return Fail(operation, "GLOBAL_PRODUCT_RETIREMENT_BUSY", 409);
            bool advanced;
            try
            {
                advanced = operation.Checkpoint switch
                {
                    GlobalProductRetirementRequestCheckpoint.Prepared => await PrepareAsync(
                        operation, claim, token, now, execution, cancellationToken),
                    GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired when reconcile => await ReconcileAsync(
                        operation, claim, now, execution, signal, cancellationToken),
                    // A request parked in AwaitingMakerReplay HOLDS the product's lock: the worker settles it as it
                    // does a Prepared one (did the maker's start arrive?).
                    GlobalProductRetirementRequestCheckpoint.AwaitingMakerReplay when token is null => await PreparedWithoutMakerAsync(
                        operation, claim, now, execution, cancellationToken),
                    GlobalProductRetirementRequestCheckpoint.StartOutcomeUnknown
                        or GlobalProductRetirementRequestCheckpoint.AwaitingMakerReplay => await RecoverStartAsync(
                            operation, claim, token, now, execution, cancellationToken),
                    GlobalProductRetirementRequestCheckpoint.WorkflowStarted => await AdvanceAsync(claim,
                        GlobalProductRetirementRequestCheckpoint.AwaitingDecision, null, now, true, cancellationToken),
                    GlobalProductRetirementRequestCheckpoint.AwaitingDecision => await ObserveDecisionAsync(
                        operation, claim, now, execution, cancellationToken),
                    GlobalProductRetirementRequestCheckpoint.DecisionObserved => await ApplyDecisionAsync(
                        operation, claim, now, execution, cancellationToken),
                    GlobalProductRetirementRequestCheckpoint.DecisionApplied => await AdvanceAsync(claim,
                        GlobalProductRetirementRequestCheckpoint.Completed, null, now, true, cancellationToken),
                    _ => false
                };
            }
            catch
            {
                // A step that throws gives its lease back — bounded, not under the caller's token.
                await ReleaseLeaseAsync(operation, leaseOwner, leaseUntil, execution);
                throw;
            }
            if (!advanced)
            {
                await ReleaseLeaseAsync(operation, leaseOwner, leaseUntil, execution);
                return Fail(operation, "GLOBAL_PRODUCT_RETIREMENT_CONCURRENCY_CONFLICT", 409);
            }
            operation = await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken)
                ?? throw new InvalidOperationException("GLOBAL_PRODUCT_RETIREMENT_OPERATION_LOST");
            // The reconciling repeat found the approval still open: it waits there, and says so (202).
            if (reconciling && operation.Checkpoint == GlobalProductRetirementRequestCheckpoint.AwaitingDecision
                && operation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.None
                && operation.NextAttemptAtUtcTicksV1 > timeProvider.GetUtcNow().UtcTicks)
                return Success(operation, false);
            // The reconciling repeat that left the request WAITING (its lock release refused by the cap, the store, a
            // concurrent write — back where the worker looks) stops here: the worker releases it.
            if (reconciling && operation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable)
                return Fail(operation, ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, 503);
            if (operation.NextAttemptAtUtcTicksV1 > timeProvider.GetUtcNow().UtcTicks)
                return Fail(operation, operation.LastFailureCode ?? "GLOBAL_PRODUCT_RETIREMENT_RETRY_SCHEDULED", 503);
        }
        return Fail(operation, "GLOBAL_PRODUCT_RETIREMENT_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<bool> PrepareAsync(
        GlobalProductRetirementRequestOperation operation,
        GlobalProductRetirementRequestClaim claim,
        string? token,
        DateTimeOffset now,
        GlobalProductRetirementRequestExecutionConfiguration execution,
        CancellationToken cancellationToken)
    {
        if (token is null)
            // The worker never starts a retirement request (only the maker's token can). Did a start of the maker's own
            // request arrive? Then it goes on as started; if nothing arrived the request ends.
            return await PreparedWithoutMakerAsync(operation, claim, now, execution, cancellationToken);
        var product = await products.GetByIdAsync(operation.GlobalProductId, cancellationToken);
        if (product is null) return await Ended(claim, "PRODUCT_IDENTITY_NOT_FOUND", now, cancellationToken);
        var admitted = await AcquireAsync(operation, product, now, cancellationToken);
        // BL-552 FIX1 item 1 (b): the product held by THIS request is its own lock — never "ended"; the request goes
        // on. Not admitted otherwise (another request holds it, its version moved): nothing was taken — it ends.
        if (!admitted.Succeeded && !HeldByThis(admitted.GlobalProduct ?? product, operation))
            // BL-552 flow 2 FIX1 item 8: a refusal that passes by itself (the audit cap, the store) took nothing and
            // ends nothing — the request waits and the maker's same key gets through once it passes.
            return ProductIdentityWriteFaults.MayPassByItself(admitted.ErrorCode)
                ? await RetryAsync(operation, claim, operation.Checkpoint, admitted.ErrorCode, now, execution,
                    cancellationToken, transient: true)
                : await Ended(claim, admitted.ErrorCode, now, cancellationToken);
        // BL-552 FIX1 item 9: the settle window starts here, in a field nothing else refreshes — written (under this
        // claim, kept) once the lock is held and before the start is sent, so a request cut off after either is still
        // read as "a start may be on its way" for exactly the window, and no longer.
        if (!await operations.AdvanceAsync(claim, new(operation.Checkpoint, operation.RecoveryDisposition, now.UtcTicks,
                operation.NextAttemptAtUtcTicksV1, operation.LastFailureCode, Streak: RecoveryStreakWrite.Keep,
                LastStartSentAtUtcTicksV1: now.UtcTicks), cancellationToken))
            return false;
        var started = await workflowClient.StartAsync(operation.TenantId,
            requestFactory.Rehydrate(operation), token, cancellationToken);
        return await RecordStartAsync(operation, claim, started, fromStart: true, now, execution, cancellationToken);
    }

    private Task<GlobalProductLifecycleWriteResult> AcquireAsync(GlobalProductRetirementRequestOperation operation,
        GlobalProduct product, DateTimeOffset now, CancellationToken cancellationToken) =>
        products.AcquireLifecycleOperationAsync(product.Id, operation.BaseProductVersion, Binding(operation),
            GlobalProductRetirementRequestAuditIntentFactory.Create(product, operation.BaseProductVersion,
                operation.OperationId, operation.MakerSubjectId,
                ProductAuditOperation.GlobalProductRetirementRequested, operation.RequestReason, now),
            cancellationToken);

    private async Task<bool> PreparedWithoutMakerAsync(
        GlobalProductRetirementRequestOperation operation,
        GlobalProductRetirementRequestClaim claim,
        DateTimeOffset now,
        GlobalProductRetirementRequestExecutionConfiguration execution,
        CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(operation.GlobalProductId, cancellationToken);
        if (product is null) return await Ended(claim, "PRODUCT_IDENTITY_NOT_FOUND", now, cancellationToken);
        var found = await LookupStartAsync(operation, cancellationToken);
        if (found.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
        {
            // The maker's start went through before its request was cut off: the lock is taken (an exact repeat if it
            // already was) and the request goes on as started.
            var admitted = await AcquireAsync(operation, product, now, cancellationToken);
            if (!admitted.Succeeded && !HeldByThis(admitted.GlobalProduct ?? product, operation))
                return ProductIdentityWriteFaults.MayPassByItself(admitted.ErrorCode)
                    ? await RetryAsync(operation, claim, operation.Checkpoint, admitted.ErrorCode, now, execution,
                        cancellationToken, transient: true)
                    : await Stuck(claim, admitted.ErrorCode, now, cancellationToken);
            return await RecordStartAsync(operation, claim, found, fromStart: false, now, execution, cancellationToken);
        }
        if (found.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound)
            return Settling(operation, now, execution)
                ? await RetryAsync(operation, claim, operation.Checkpoint, StartSettling, now, execution,
                    cancellationToken, transient: true)
                : HeldByThis(product, operation)
                    ? await EndAndReleaseAsync(operation, claim, StartNotReceived, now, execution, cancellationToken)
                    // BL-552 flow 2 FIX1 item 8: it never took the product, so nothing was started — it holds
                    // nothing and waits for its maker's same key (looked at again only at the ceiling). Flow 3 FIX1
                    // item 15: not for ever — a maker who has not come back within the limit has abandoned it: it
                    // ends (it holds nothing to give back) with its own reason, which the ended request answers.
                    : now.UtcTicks - operation.CreatedAtUtcTicksV1 >= AwaitingMakerLimit.Ticks
                        ? await Ended(claim, MakerNeverReturned, now, cancellationToken)
                    : await operations.AdvanceAsync(claim, new(operation.Checkpoint,
                        ProductIdentityWorkflowRecoveryDisposition.Retryable, now.UtcTicks,
                        now.Add(execution.MaxRetryDelay ?? ProductIdentityRecoveryStreak.DefaultMaxRetryDelay).UtcTicks,
                        AwaitingMaker, ReleaseLease: true, Streak: RecoveryStreakWrite.Keep), cancellationToken);
        return await RetryAsync(operation, claim, operation.Checkpoint, found.ErrorCode ?? StartLookupFailed,
            now, execution, cancellationToken,
            transient: Waits(ProductIdentityWorkflowCall.StartResult, found.Outcome, found.ErrorCode));
    }

    /*
     * The requester's same command on a request STUCK in manual reconciliation (the product's lock held). It reads the
     * engine again and puts the request back where the engine and the product say it stands:
     *   decision on record      → its application is tried again;
     *   an approval on record   → back to waiting for its decision, read in this same request;
     *   no approval on record   → was the start received? yes → started; no (settle window passed) → ended, freed;
     *   the engine cannot answer → said as such (503); nothing changes.
     */
    private async Task<bool> ReconcileAsync(
        GlobalProductRetirementRequestOperation operation,
        GlobalProductRetirementRequestClaim claim,
        DateTimeOffset now,
        GlobalProductRetirementRequestExecutionConfiguration execution,
        ReconcileSignal signal,
        CancellationToken cancellationToken)
    {
        // BL-552 flow 2 FIX1 item 9: the previous version's conflict record (124e30acc and before) left the product
        // held by this request one version past its base + 1. Nothing can be applied at that version: the repeat
        // ends the request and gives the product back, keeping the reason it stopped on.
        var held = await products.GetByIdAsync(operation.GlobalProductId, cancellationToken);
        if (held?.ActiveLifecycleOperation == Binding(operation)
            && held.Version > checked(operation.BaseProductVersion + 1))
            return await EndAndReleaseAsync(operation, claim, operation.LastFailureCode ?? ProductMoved, now,
                execution, cancellationToken);
        if (operation.DecisionKind.HasValue && operation.DecisionActorSubjectId.HasValue)
            return await AdvanceAsync(claim, GlobalProductRetirementRequestCheckpoint.DecisionObserved, null, now, true,
                cancellationToken);
        if (operation.WorkflowInstanceId.HasValue)
            return await AdvanceAsync(claim, GlobalProductRetirementRequestCheckpoint.AwaitingDecision, null, now, true,
                cancellationToken);
        var found = await LookupStartAsync(operation, cancellationToken);
        if (found.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
            return await RecordStartAsync(operation, claim, found, fromStart: false, now, execution, cancellationToken);
        if (found.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound && !Settling(operation, now, execution))
            return await EndAndReleaseAsync(operation, claim, StartNotReceived, now, execution, cancellationToken);
        // Not settled now: "nothing yet" inside the settle window, or a service fault — ask again (503). An answer
        // about the record (a recovery conflict, a tenant mismatch) will not change by asking again — 409, a person.
        if (found.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound
            || Waits(ProductIdentityWorkflowCall.StartResult, found.Outcome, found.ErrorCode))
            signal.EngineSilent = true;
        return await operations.AdvanceAsync(claim, new(operation.Checkpoint, operation.RecoveryDisposition,
            now.UtcTicks, LastFailureCode: operation.LastFailureCode, ReleaseLease: true,
            Streak: RecoveryStreakWrite.Keep), cancellationToken);
    }

    private Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> LookupStartAsync(
        GlobalProductRetirementRequestOperation operation, CancellationToken cancellationToken) =>
        workflowClient.GetStartResultAsync(operation.TenantId,
            new(operation.ObjectType, operation.ObjectId, operation.MakerSubjectId, operation.StartIdempotencyKey),
            cancellationToken);

    private async Task<bool> RecoverStartAsync(
        GlobalProductRetirementRequestOperation operation,
        GlobalProductRetirementRequestClaim claim,
        string? token,
        DateTimeOffset now,
        GlobalProductRetirementRequestExecutionConfiguration execution,
        CancellationToken cancellationToken)
    {
        var found = await LookupStartAsync(operation, cancellationToken);
        var fromStart = false;
        if (found.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound && token is not null)
        {
            found = await workflowClient.StartAsync(operation.TenantId, requestFactory.Rehydrate(operation), token,
                cancellationToken);
            fromStart = true;
        }
        return await RecordStartAsync(operation, claim, found, fromStart, now, execution, cancellationToken);
    }

    // The start's answer and the lookup's answer are read apart: only the START call's own "nothing was processed"
    // (or, after the settle window, the lookup's "nothing under this key") may give the product back.
    private async Task<bool> RecordStartAsync(
        GlobalProductRetirementRequestOperation operation,
        GlobalProductRetirementRequestClaim claim,
        ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult> result,
        bool fromStart,
        DateTimeOffset now,
        GlobalProductRetirementRequestExecutionConfiguration execution,
        CancellationToken cancellationToken)
    {
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.Success && result.Value is { } value
            && value.WorkflowInstanceId != Guid.Empty && value.TemplateId != Guid.Empty
            && value.TemplateVersionId != Guid.Empty && value.ApprovalTaskId != Guid.Empty
            && value.AssignmentSnapshotId != Guid.Empty && value.StartTransitionLogId != Guid.Empty
            && (!operation.WorkflowTemplateId.HasValue || value.TemplateId == operation.WorkflowTemplateId)
            && string.Equals(value.ObjectRef, operation.ObjectRef, StringComparison.Ordinal))
            return await operations.AdvanceAsync(claim, new(
                GlobalProductRetirementRequestCheckpoint.WorkflowStarted,
                ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
                WorkflowInstanceId: value.WorkflowInstanceId, WorkflowTemplateId: value.TemplateId,
                WorkflowTemplateVersionId: value.TemplateVersionId, ApprovalTaskId: value.ApprovalTaskId,
                AssignmentSnapshotId: value.AssignmentSnapshotId, StartTransitionLogId: value.StartTransitionLogId,
                WorkflowStartedAtUtcTicksV1: (value.StartedAt ?? now).UtcTicks, ReleaseLease: true),
                cancellationToken);
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound && !fromStart
            && Settling(operation, now, execution))
            // "Nothing under this key" right after a start may only mean the engine is still on it: the lock stays.
            return await RetryAsync(operation, claim, GlobalProductRetirementRequestCheckpoint.StartOutcomeUnknown,
                StartSettling, now, execution, cancellationToken, transient: true);
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound)
            // From the start: no such definition — nothing was started. From the lookup (the window passed): nothing
            // arrived. Either way the product is free.
            return await EndAndReleaseAsync(operation, claim, fromStart
                ? ProductIdentityWorkflowMakerCodes.DefinitionNotReady
                : StartNotReceived, now, execution, cancellationToken);
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.Timeout
            or ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Incomplete
            or ProductIdentityWorkflowTransportOutcome.Success)
            // No answer, or an answer that cannot be used: the engine may have started an approval — the lock stays
            // and the start is looked up again.
            return await StartUnknownAsync(operation, claim, result.ErrorCode
                    ?? (result.Outcome == ProductIdentityWorkflowTransportOutcome.Success ? StartEvidenceInvalid : null),
                now, execution, cancellationToken, fromStart,
                transient: Waits(fromStart ? ProductIdentityWorkflowCall.Start : ProductIdentityWorkflowCall.StartResult,
                    result.Outcome, result.ErrorCode));
        if (!fromStart)
            // The LOOKUP was refused (a service identity not granted, a rotated key, a tenant mismatch): that says
            // nothing about what the lost start did. The lock is kept and the engine asked again — a service fault
            // waits, anything else is counted and handed to a person at the limit.
            return await StartUnknownAsync(operation, claim, result.ErrorCode ?? StartLookupFailed, now, execution,
                cancellationToken, startSent: false,
                transient: Waits(ProductIdentityWorkflowCall.StartResult, result.Outcome, result.ErrorCode));
        var fault = ProductIdentityWorkflowFaultPolicy.Classify(
            ProductIdentityWorkflowCall.Start, result.Outcome, result.ErrorCode);
        if (!fault.StartedNothing)
            return await StartUnknownAsync(operation, claim, result.ErrorCode, now, execution, cancellationToken,
                fromStart, transient: fault.Kind == ProductIdentityWorkflowFaultKind.ServiceWait);
        // The START was refused before anything was processed: nothing exists at the engine — the product is given
        // back and the reason recorded; the maker learns it from the answer.
        return await EndAndReleaseAsync(operation, claim,
            // BL-552 flow 2 FIX1 item 6: the maker's own refusals keep their maker code (401 / 403); a refusal of how
            // the engine treats this service (its trust, its grant) is the service's — resubmit (503), as the identity
            // flow says it.
            fault.MakerCode ?? (fault.Kind == ProductIdentityWorkflowFaultKind.ServiceWait
                ? ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit
                : result.ErrorCode ?? "GLOBAL_PRODUCT_RETIREMENT_START_REFUSED"), now, execution,
            cancellationToken);
    }

    private Task<bool> StartUnknownAsync(GlobalProductRetirementRequestOperation operation, GlobalProductRetirementRequestClaim claim,
        string? code, DateTimeOffset now, GlobalProductRetirementRequestExecutionConfiguration execution,
        CancellationToken cancellationToken, bool startSent = false, bool transient = false)
    {
        if (code is not null && code.EndsWith("_INVALID", StringComparison.Ordinal) && operation.LastFailureCode != code)
            log.LogError("Global Product retirement request {OperationId} of tenant {TenantId}: the engine's start answer cannot be used ({ResultCode}); the lock is kept and the start is looked up again.",
                operation.OperationId, operation.TenantId, code);
        return RetryAsync(operation, claim, GlobalProductRetirementRequestCheckpoint.StartOutcomeUnknown, code, now,
            execution, cancellationToken, transient, startSentAt: startSent ? now.UtcTicks : null);
    }

    // The retirement request ends without its decision applied; the product's lock goes in the same write as the intent that
    // says why. If giving it back is refused, the shared write classification decides (wait / counted / stuck).
    private async Task<bool> EndAndReleaseAsync(GlobalProductRetirementRequestOperation operation,
        GlobalProductRetirementRequestClaim claim, string reason, DateTimeOffset now,
        GlobalProductRetirementRequestExecutionConfiguration execution, CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(operation.GlobalProductId, cancellationToken);
        if (product?.ActiveLifecycleOperation is { } held && held == Binding(operation))
        {
            // BL-552 flow 2 FIX1 items 9 and 12: given back at the version the product is at (its base + 1, or one
            // further for a row the previous version's name-conflict record moved on), with the request's own
            // "ended" audit operation and the reason it ended in its facts.
            var expected = product.Version;
            var audit = GlobalProductRetirementRequestAuditIntentFactory.Create(product, expected, operation.OperationId,
                operation.MakerSubjectId, ProductAuditOperation.GlobalProductRetirementEnded,
                operation.RequestReason, now, reason);
            var released = await products.ReleaseLifecycleOperationAsync(product.Id, expected, Binding(operation),
                audit, cancellationToken);
            // BL-552 FIX1 item 11: the lock already gone is the end this request wanted — not a refusal.
            if (!released.Succeeded && released.ErrorCode != "GLOBAL_PRODUCT_LIFECYCLE_NOT_HELD")
                return await WriteRefusedAsync(operation, claim, released.ErrorCode, now, execution, cancellationToken);
        }
        log.LogInformation("Global Product retirement request {OperationId} of tenant {TenantId} ended without its decision applied ({ResultCode}); the product is free again.",
            operation.OperationId, operation.TenantId, reason);
        return await Ended(claim, reason, now, cancellationToken);
    }

    private async Task<bool> ObserveDecisionAsync(
        GlobalProductRetirementRequestOperation operation,
        GlobalProductRetirementRequestClaim claim,
        DateTimeOffset now,
        GlobalProductRetirementRequestExecutionConfiguration execution,
        CancellationToken cancellationToken)
    {
        if (!operation.WorkflowInstanceId.HasValue)
            return await Stuck(claim, StartEvidenceInvalid, now, cancellationToken);
        var result = await workflowClient.GetTerminalEvidenceAsync(operation.TenantId,
            new(operation.WorkflowInstanceId.Value, operation.ObjectType, operation.ObjectId), cancellationToken);
        // NOT_TERMINAL (an escalated approval included): still open — not a failure, it waits.
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.NonTerminal)
            return await operations.AdvanceAsync(claim, new(operation.Checkpoint, operation.RecoveryDisposition,
                now.UtcTicks, now.Add(execution.RetryDelay).UtcTicks,
                result.ErrorCode ?? "GLOBAL_PRODUCT_RETIREMENT_DECISION_PENDING", ReleaseLease: true), cancellationToken);
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.NotFound
            or ProductIdentityWorkflowTransportOutcome.Incomplete
            or ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Timeout)
            return await RetryAsync(operation, claim, operation.Checkpoint,
                result.ErrorCode ?? "GLOBAL_PRODUCT_RETIREMENT_DECISION_PENDING", now, execution, cancellationToken,
                transient: IsTransient(result.Outcome));
        if (result.Outcome != ProductIdentityWorkflowTransportOutcome.Success
            && ProductIdentityWorkflowFaultPolicy.Classify(ProductIdentityWorkflowCall.TerminalEvidence, result.Outcome,
                result.ErrorCode).Kind == ProductIdentityWorkflowFaultKind.ServiceWait)
            return await RetryAsync(operation, claim, operation.Checkpoint, result.ErrorCode, now, execution,
                cancellationToken, transient: true);
        var evidence = result.Value;
        if (evidence is not null && Guid.TryParse(evidence.ActorUserId, out var self) && self == operation.MakerSubjectId)
        {
            // The maker decided their own request and the engine has closed it — no other decision will come. The
            // request ends unapplied and the product is given back (before: stuck as an evidence conflict for ever).
            log.LogWarning("Global Product retirement request {OperationId} of tenant {TenantId} was decided by its own maker; the decision is not applied ({ResultCode}).",
                operation.OperationId, operation.TenantId, SelfApprovalIgnored);
            return await EndAndReleaseAsync(operation, claim, SelfApprovalIgnored, now, execution, cancellationToken);
        }
        var isApproved = evidence?.TerminalAction == "Approve" && evidence.TaskStatus == "Approved"
            && evidence.InstanceStatus == "Completed";
        var isRejected = evidence?.TerminalAction == "Reject" && evidence.TaskStatus == "Rejected"
            && evidence.InstanceStatus == "Rejected";
        if ((!isApproved && !isRejected) || evidence is null
            || evidence.WorkflowInstanceId != operation.WorkflowInstanceId
            || evidence.ApprovalTaskId != operation.ApprovalTaskId
            || evidence.TemplateId != operation.WorkflowTemplateId
            || evidence.TemplateVersionId != operation.WorkflowTemplateVersionId
            || evidence.ObjectType != operation.ObjectType || evidence.ObjectId != operation.ObjectId
            || evidence.ObjectRef != operation.ObjectRef
            || !Guid.TryParse(evidence.ActorUserId, out var actor) || actor == Guid.Empty
            || evidence.TransitionSequence <= 0 || evidence.DecisionAt.Offset != TimeSpan.Zero)
            // The engine's three answers (BL-547):
            //   WORKFLOW_INSTANCE_CLOSED_WITHOUT_DECISION (cancelled in the Task Center, timed out) — nothing more can
            //     come of it: the request ends, the product is given back;
            //   WORKFLOW_TERMINAL_EVIDENCE_INCONSISTENT, or decided evidence this request cannot apply — stuck, the
            //     lock held; its requester's same command reads the engine again;
            //   an answer that is not evidence at all — asked again (the lock stays).
            return ProductIdentityWorkflowFaultPolicy.IsClosedWithoutDecision(result)
                ? await EndAndReleaseAsync(operation, claim, ClosedWithoutDecision, now, execution, cancellationToken)
                : evidence is null && result.Outcome != ProductIdentityWorkflowTransportOutcome.Conflict
                    ? await RetryAsync(operation, claim, operation.Checkpoint,
                        result.ErrorCode ?? "GLOBAL_PRODUCT_RETIREMENT_EVIDENCE_UNREADABLE", now, execution,
                        cancellationToken)
                    : await Stuck(claim, EvidenceConflict, now, cancellationToken);
        return await operations.AdvanceAsync(claim, new(
            GlobalProductRetirementRequestCheckpoint.DecisionObserved,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
            DecisionKind: isApproved ? ProductIdentityDecisionKind.Approved : ProductIdentityDecisionKind.Rejected,
            DecisionActorSubjectId: actor, DecisionReasonCode: evidence.ReasonCode,
            DecisionAtUtcTicksV1: evidence.DecisionAt.UtcTicks,
            DecisionTransitionSequence: evidence.TransitionSequence,
            DecisionTaskStatus: evidence.TaskStatus, DecisionInstanceStatus: evidence.InstanceStatus,
            ReleaseLease: true), cancellationToken);
    }

    private async Task<bool> ApplyDecisionAsync(
        GlobalProductRetirementRequestOperation operation,
        GlobalProductRetirementRequestClaim claim,
        DateTimeOffset now,
        GlobalProductRetirementRequestExecutionConfiguration execution,
        CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(operation.GlobalProductId, cancellationToken);
        if (product is null || !operation.DecisionActorSubjectId.HasValue)
            return await Stuck(claim, "GLOBAL_PRODUCT_RETIREMENT_PRODUCT_DRIFT", now, cancellationToken);
        var expectedVersion = checked(operation.BaseProductVersion + 1);
        // BL-552 flow 2 FIX1 item 9: held by this request but moved past its base + 1 — only the previous version's
        // conflict record did that (a row of 124e30acc and before). Its decision cannot be applied at this version:
        // the request ends and the product is given back.
        if (HeldByThis(product, operation) && product.Version != expectedVersion)
            return await EndAndReleaseAsync(operation, claim, operation.LastFailureCode ?? ProductMoved, now, execution,
                cancellationToken);
        var approved = operation.DecisionKind == ProductIdentityDecisionKind.Approved;
        var auditOperation = approved ? ProductAuditOperation.GlobalProductIdentityRetired
            : ProductAuditOperation.GlobalProductRetirementRejected;
        // BL-552 FIX1 item 2 (the GSKU alreadyApplied rule): the decision was written before and only its record here
        // was lost — its intent may be compacted to a receipt by now. Applied once; the request goes on.
        var appliedKey = $"global-product-retirement:{operation.OperationId:D}:{auditOperation}";
        if (product.ActiveLifecycleOperation is null
            && (product.AuditIntents.Any(x => x.IdempotencyKey == appliedKey)
                || product.AuditIntentReceipts.Any(x => x.IdempotencyKey == appliedKey)))
            return await AdvanceAsync(claim, GlobalProductRetirementRequestCheckpoint.DecisionApplied, null, now, true,
                cancellationToken);
        var audit = GlobalProductRetirementRequestAuditIntentFactory.Create(product, expectedVersion,
            operation.OperationId, operation.DecisionActorSubjectId.Value, auditOperation,
            operation.RequestReason, now);
        var result = await products.ApplyRetirementDecisionAsync(product.Id, expectedVersion, Binding(operation),
            approved, audit, cancellationToken);
        if (!result.Succeeded)
            // An approved retirement that meets a dependant (an approved or draft child found after the request)
            // cannot be applied: the request ends "not applicable", the requester sees why, and the product is given
            // back (the GSKU retirement request's rule; before: stuck with the lock held for ever). Anything else is
            // the shared write classification.
            return result.ErrorCode is "DEPENDENT_IDENTITIES_EXIST" or "DRAFT_CHILD_CANCELLATION_REQUIRED"
                ? await EndAndReleaseAsync(operation, claim, result.ErrorCode, now, execution, cancellationToken)
                : await WriteRefusedAsync(operation, claim, result.ErrorCode, now, execution, cancellationToken);
        return await AdvanceAsync(claim, GlobalProductRetirementRequestCheckpoint.DecisionApplied, null,
            now, true, cancellationToken);
    }

    // A refused product write (the shared classification): the capacity / store / a lease held elsewhere pass by
    // themselves (wait, never quarantined); a concurrent write is read and tried again (counted); anything else will
    // not change by waiting: stuck at once, the lock held — a person (BL-530) or the requester's same command.
    private Task<bool> WriteRefusedAsync(GlobalProductRetirementRequestOperation operation, GlobalProductRetirementRequestClaim claim,
        string? code, DateTimeOffset now, GlobalProductRetirementRequestExecutionConfiguration execution,
        CancellationToken cancellationToken) =>
        ProductIdentityWriteFaults.MayPassByItself(code)
            ? RetryAsync(operation, claim, operation.Checkpoint, code, now, execution, cancellationToken, transient: true)
            : ProductIdentityWriteFaults.MetAConcurrentWrite(code)
                ? RetryAsync(operation, claim, operation.Checkpoint, code, now, execution, cancellationToken)
                : Stuck(claim, code, now, cancellationToken);

    /*
     * Every retry goes through here. Only the WORKER's attempts count (a maker's repeat leaves the streak as it is), in
     * the store ($inc). After the configured number in a row:
     *   a service fault is NEVER quarantined — RETRY_LIMIT_REACHED, retrying at the ceiling, healing by itself;
     *   any other cause is stuck with its cause kept (STUCK_ON_RECORD_ANSWER / STUCK_ON_RELEASE_CONFLICT) — a person.
     * A request in manual reconciliation that has to WAIT goes back where the worker looks (never manual + retryable).
     */
    private async Task<bool> RetryAsync(GlobalProductRetirementRequestOperation operation, GlobalProductRetirementRequestClaim claim,
        GlobalProductRetirementRequestCheckpoint checkpoint, string? code, DateTimeOffset now,
        GlobalProductRetirementRequestExecutionConfiguration execution, CancellationToken cancellationToken,
        bool transient = false, long? startSentAt = null)
    {
        if (checkpoint == GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired)
            checkpoint = operation.WorkflowInstanceId.HasValue
                ? GlobalProductRetirementRequestCheckpoint.AwaitingDecision
                : GlobalProductRetirementRequestCheckpoint.StartOutcomeUnknown;
        var counted = !execution.Interactive;
        var (exhausted, _, next) = ProductIdentityRecoveryStreak.Next(
            counted ? operation.FailedAttemptCount : Math.Max(operation.FailedAttemptCount - 1, 0), now,
            execution.RetryDelay, execution.QuarantineAfterFailedAttempts, execution.MaxRetryDelay);
        if (counted && exhausted && !transient)
            return await Stuck(claim, code == ProductIdentityWriteFaults.GlobalProductReleaseConflict
                ? StuckOnReleaseConflict : StuckOnRecordAnswer, now, cancellationToken);
        if (counted && exhausted)
            code = RetryLimitReached;
        return await operations.AdvanceAsync(claim, new(checkpoint, ProductIdentityWorkflowRecoveryDisposition.Retryable,
            now.UtcTicks, next, code, ReleaseLease: true,
            Streak: counted ? RecoveryStreakWrite.Increment : RecoveryStreakWrite.Keep,
            LastStartSentAtUtcTicksV1: startSentAt), cancellationToken);
    }

    private static bool IsTransient(ProductIdentityWorkflowTransportOutcome outcome) =>
        outcome is ProductIdentityWorkflowTransportOutcome.Timeout or ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Incomplete;

    private static bool Waits(ProductIdentityWorkflowCall call, ProductIdentityWorkflowTransportOutcome outcome, string? code) =>
        IsTransient(outcome)
        || outcome is not (ProductIdentityWorkflowTransportOutcome.Success or ProductIdentityWorkflowTransportOutcome.NotFound
            or ProductIdentityWorkflowTransportOutcome.NonTerminal)
           && ProductIdentityWorkflowFaultPolicy.Classify(call, outcome, code).Kind == ProductIdentityWorkflowFaultKind.ServiceWait;

    // BL-552 FIX1 item 9: the window is read from fields no claim or retry refreshes.
    private static bool Settling(GlobalProductRetirementRequestOperation operation, DateTimeOffset now,
        GlobalProductRetirementRequestExecutionConfiguration execution) =>
        now.UtcTicks - (operation.LastStartSentAtUtcTicksV1 ?? operation.CreatedAtUtcTicksV1) < execution.SettleWindow.Ticks;

    private static bool HeldByThis(GlobalProduct? product, GlobalProductRetirementRequestOperation operation) =>
        product?.ActiveLifecycleOperation is { } held && held == Binding(operation);

    // BL-552 FIX1 item 11: a refusal that passes by itself is 503; a maker's code keeps its own; anything else 409.
    private static int StatusOf(string code) =>
        ProductIdentityWriteFaults.MayPassByItself(code) ? 503
        : ProductIdentityWorkflowMakerCodes.IsMakerCode(code) ? ProductIdentityWorkflowMakerCodes.StatusFor(code)
        : 409;

    private sealed class ReconcileSignal
    {
        public bool EngineSilent { get; set; }
    }

    private static bool IsStuck(GlobalProductRetirementRequestOperation operation) =>
        operation.Checkpoint == GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired
            ? operation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired
            : operation.LastFailureCode == RetryLimitReached;

    public const string RetryLimitReached = "GLOBAL_PRODUCT_RETIREMENT_RETRY_LIMIT_REACHED";
    public const string StuckOnRecordAnswer = "GLOBAL_PRODUCT_RETIREMENT_STUCK_ON_RECORD_ANSWER";
    public const string StuckOnReleaseConflict = "GLOBAL_PRODUCT_RETIREMENT_STUCK_ON_RELEASE_CONFLICT";
    public const string EvidenceConflict = "GLOBAL_PRODUCT_RETIREMENT_EVIDENCE_CONFLICT";
    public const string StartEvidenceInvalid = "GLOBAL_PRODUCT_RETIREMENT_START_EVIDENCE_INVALID";
    public const string ManualReconciliationNeeded = "GLOBAL_PRODUCT_RETIREMENT_MANUAL_RECONCILIATION_NEEDED";
    public const string StartSettling = "GLOBAL_PRODUCT_RETIREMENT_START_SETTLING";
    // BL-552 flow 2 FIX1: a request that never took the product waits for its maker; one held but moved on ends.
    public const string AwaitingMaker = "GLOBAL_PRODUCT_RETIREMENT_AWAITING_MAKER";
    // Flow 3 FIX1 item 15: how long a request that never took the product waits for its maker's same key.
    public const string MakerNeverReturned = "GLOBAL_PRODUCT_RETIREMENT_MAKER_NEVER_RETURNED";
    public static readonly TimeSpan AwaitingMakerLimit = TimeSpan.FromDays(7);
    public const string ProductMoved = "GLOBAL_PRODUCT_RETIREMENT_PRODUCT_MOVED";
    public const string StartNotReceived = "GLOBAL_PRODUCT_RETIREMENT_START_NOT_RECEIVED";
    public const string StartLookupFailed = "GLOBAL_PRODUCT_RETIREMENT_START_LOOKUP_FAILED";
    public const string ClosedWithoutDecision = "GLOBAL_PRODUCT_RETIREMENT_CLOSED_WITHOUT_DECISION";
    public const string SelfApprovalIgnored = "GLOBAL_PRODUCT_RETIREMENT_SELF_APPROVAL_IGNORED";

    private async Task ReleaseLeaseAsync(GlobalProductRetirementRequestOperation operation, string leaseOwner,
        long leaseUntil, GlobalProductRetirementRequestExecutionConfiguration execution)
    {
        using var bound = new CancellationTokenSource(ProductIdentityWorkflowClaims.Bound(execution.LeaseDuration));
        try { await operations.ReleaseIfOwnedAsync(operation.OperationId, leaseOwner, leaseUntil, bound.Token); }
        catch (Exception exception)
        {
            log.LogWarning("{Flow} {OperationId}: the lease of a step that did not finish could not be given back ({ExceptionType}).",
                "GLOBAL_PRODUCT_RETIREMENT", operation.OperationId, exception.GetType().Name);
        }
    }

    private static GlobalProductActiveLifecycleOperationBinding Binding(GlobalProductRetirementRequestOperation operation) =>
        new(GlobalProductLifecycleOperationKind.Retirement, operation.OperationId, operation.BaseProductVersion);

    // STUCK — handed to a person with the product's lock held (counted by the gauge; its requester's same command is
    // its way out unless only a person can settle it).
    private Task<bool> Stuck(GlobalProductRetirementRequestClaim claim, string? code,
        DateTimeOffset now, CancellationToken cancellationToken) =>
        operations.AdvanceAsync(claim, new(GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, now.UtcTicks,
            LastFailureCode: code ?? "GLOBAL_PRODUCT_RETIREMENT_RECONCILIATION_REQUIRED",
            ReleaseLease: true), cancellationToken);

    // ENDED — the request is over, nothing held (the record keeps why): not counted, not an error.
    private Task<bool> Ended(GlobalProductRetirementRequestClaim claim, string? code,
        DateTimeOffset now, CancellationToken cancellationToken) =>
        operations.AdvanceAsync(claim, new(GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
            LastFailureCode: code ?? "GLOBAL_PRODUCT_RETIREMENT_RECONCILIATION_REQUIRED",
            ReleaseLease: true), cancellationToken);

    private Task<bool> AdvanceAsync(GlobalProductRetirementRequestClaim claim,
        GlobalProductRetirementRequestCheckpoint checkpoint, string? error, DateTimeOffset now, bool release,
        CancellationToken cancellationToken) => operations.AdvanceAsync(claim, new(checkpoint,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks, LastFailureCode: error,
            ReleaseLease: release), cancellationToken);

    private static GlobalProductRetirementRequestProcessingResult Success(
        GlobalProductRetirementRequestOperation operation, bool replay) => new(true, operation, null,
        operation.Checkpoint == GlobalProductRetirementRequestCheckpoint.Completed ? 200 : 202, replay);
    private static GlobalProductRetirementRequestProcessingResult Fail(string code, int status) =>
        new(false, null, code, status, false);
    private static GlobalProductRetirementRequestProcessingResult Fail(
        GlobalProductRetirementRequestOperation operation, string code, int status) =>
        new(false, operation, code, status, false);
}
