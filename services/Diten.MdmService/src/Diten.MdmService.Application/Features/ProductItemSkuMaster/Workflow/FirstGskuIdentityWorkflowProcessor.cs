using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using PersistedCancellationEvidence = Diten.MdmService.Domain.ValueObjects.ProductIdentityWorkflowCancellationEvidence;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record FirstGskuIdentityWorkflowProcessingResult(
    bool Succeeded,
    FirstGskuIdentityWorkflowOperation? Operation,
    string? ErrorCode,
    int StatusCode,
    bool IsReplay,
    // Set when the attempt met a fault of the service chain: what a recovery cycle may skip without calling again.
    ProductIdentityWorkflowFaultScope FaultScope = ProductIdentityWorkflowFaultScope.None,
    // On a conflicting withdrawal: the command the requester's earlier withdrawal stands under (only to them).
    Guid? PendingWithdrawalCommandId = null);

public sealed record FirstGskuIdentityWorkflowExecutionConfiguration(
    TimeSpan LeaseDuration,
    TimeSpan RetryDelay);

public sealed class FirstGskuIdentityWorkflowProcessor(
    IFirstGskuIdentityWorkflowOperationRepository operations,
    IProductDefinitionRevisionRepository revisions,
    IGskuRepository gskus,
    IGlobalProductRepository globalProducts,
    IWorkflowVerifiedGskuReferenceResolver references,
    IProductIdentityWorkflowClient workflowClient,
    FirstGskuIdentityWorkflowStartRequestFactory requestFactory,
    TimeProvider timeProvider,
    ProductIdentityWorkflowServiceFaultSettings faultSettings,
    ILogger<FirstGskuIdentityWorkflowProcessor> logger)
{
    // The same rules as the Global Product processor (WP-GP-1B-A-RESILIENCE-01 and its three fix rounds), carried
    // over by WP-MDM-GSKU-ACCEPT-01: a fault of the service chain waits, it never quarantines the pair.
    private sealed class Attempt(bool interactive)
    {
        public bool Interactive { get; } = interactive;
        public ProductIdentityWorkflowFaultScope Scope { get; set; }
        // What the step that just ran wants the caller told, instead of going round again.
        public (string Code, int Status)? Answer { get; set; }
        // The versions the cancellation carries were read in THIS request: no earlier cancellation can be in flight.
        public bool VersionsReadNow { get; set; }
        // One request asks again after a conflict at most once.
        public bool ConflictRetried { get; set; }
        // The worker's schedule as this request found it (known only when the request began the withdrawal itself).
        public (bool Known, long? Ticks) ScheduleAtEntry { get; set; }
        // FIX3 E2: the submitter's repeat found the approval closed at the engine — its outcome is read now, in this
        // request, instead of stopping at "awaiting decision".
        public bool ObserveNow { get; set; }
    }


    private static readonly FirstGskuIdentityWorkflowCheckpoint[] RecoverableCheckpoints =
        Enum.GetValues<FirstGskuIdentityWorkflowCheckpoint>()
            .Except([
                FirstGskuIdentityWorkflowCheckpoint.Completed,
                FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired])
            .ToArray();

    public async Task<FirstGskuIdentityWorkflowProcessingResult> StartInteractiveAsync(
        Guid tenantId,
        Guid gskuId,
        int expectedGskuVersion,
        Guid operationId,
        Guid makerSubjectId,
        string delegatedUserToken,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || gskuId == Guid.Empty || expectedGskuVersion < 0
            || operationId == Guid.Empty || makerSubjectId == Guid.Empty
            || string.IsNullOrWhiteSpace(delegatedUserToken))
        {
            return Fail("FIRST_GSKU_IDENTITY_WORKFLOW_START_INVALID", 400);
        }

        var existing = await operations.GetByOperationIdAsync(operationId, cancellationToken);
        FirstGskuIdentityWorkflowOperation operation;
        var replay = existing is not null;
        if (existing is null)
        {
            var gsku = await gskus.GetByIdAsync(gskuId, cancellationToken);
            if (gsku is null)
            {
                return Fail("GSKU_NOT_FOUND", 404);
            }
            var revision = await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
            if (revision is null)
            {
                return Fail("GSKU_NOT_FOUND", 404);
            }
            // B2 (a): a child is not submitted before its parent is approved (as in SAP / Oracle, an item is not
            // released before the definition it belongs to). Checked before anything is written or started.
            var parent = await globalProducts.GetByIdAsync(revision.GlobalProductId, cancellationToken);
            if (parent is null || parent.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved)
            {
                return Fail(ParentNotApproved, 409);
            }

            FirstGskuIdentityWorkflowStartPlan plan;
            try
            {
                plan = requestFactory.Create(
                    tenantId, revision, gsku, operationId, expectedGskuVersion, makerSubjectId);
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message,
                    exception.Message == "FIRST_GSKU_IDENTITY_WORKFLOW_CONFIGURATION_INVALID" ? 503 : 409);
            }

            var reserved = await operations.ReserveAsync(plan.Operation, cancellationToken);
            if (!reserved.Succeeded || reserved.Operation is null)
            {
                return Fail(reserved.ErrorCode ?? "FIRST_GSKU_IDENTITY_WORKFLOW_OPERATION_CONFLICT",
                    reserved.ErrorCode == ProductIdentityStore.Unavailable ? 503 : 409);
            }
            operation = reserved.Operation;
            replay = reserved.IsReplay;
        }
        else
        {
            operation = existing;
        }

        if (!ExactInteractiveFacts(operation, tenantId, gskuId, expectedGskuVersion, makerSubjectId))
        {
            return Fail("FIRST_GSKU_IDENTITY_WORKFLOW_OPERATION_CONFLICT", 409);
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

    public Task<FirstGskuIdentityWorkflowProcessingResult> RecoverAsync(
        FirstGskuIdentityWorkflowOperation operation,
        string leaseOwner,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default) =>
        operation.WithdrawalCommandId.HasValue && operation.Checkpoint is
            FirstGskuIdentityWorkflowCheckpoint.WithdrawalRequested
            or FirstGskuIdentityWorkflowCheckpoint.WithdrawalPreflightObserved
            or FirstGskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown
            or FirstGskuIdentityWorkflowCheckpoint.WithdrawalObserved
            or FirstGskuIdentityWorkflowCheckpoint.WithdrawalApplied
            or FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
            ? ProcessWithdrawalAsync(operation, null, leaseOwner, leaseDuration, retryDelay, cancellationToken)
            : ProcessAsync(operation, null, leaseOwner, leaseDuration, retryDelay,
                stopWhenAwaitingDecision: false, cancellationToken);

    public async Task<FirstGskuIdentityWorkflowProcessingResult> WithdrawInteractiveAsync(
        Guid tenantId,
        Guid gskuId,
        int expectedGskuVersion,
        Guid commandId,
        Guid requesterSubjectId,
        string reasonCode,
        string? comment,
        string delegatedUserToken,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || gskuId == Guid.Empty || commandId == Guid.Empty
            || requesterSubjectId == Guid.Empty || expectedGskuVersion < 1
            || !ExactText(reasonCode, 128) || !OptionalExactText(comment, 2000)
            || string.IsNullOrWhiteSpace(delegatedUserToken))
            return Fail("FIRST_GSKU_IDENTITY_WITHDRAWAL_INVALID", 400);

        var gsku = await gskus.GetByIdAsync(gskuId, cancellationToken);
        if (gsku is null) return Fail("GSKU_NOT_FOUND", 404);
        var revision = await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
        if (revision is null || gsku.IdentityWorkflowBinding is not { } binding
            || revision.IdentityWorkflowBinding is not { } revisionBinding)
            return Fail("FIRST_GSKU_IDENTITY_PAIR_INCONSISTENT", 409);
        if (binding.SubmitterSubjectId != requesterSubjectId
            || revisionBinding.SubmitterSubjectId != requesterSubjectId)
            return Fail("FIRST_GSKU_IDENTITY_WITHDRAWAL_NOT_REQUESTER", 403);
        if (!SameBinding(binding, revisionBinding))
            return Fail("FIRST_GSKU_IDENTITY_WORKFLOW_BINDING_CONFLICT", 409);

        var operation = await operations.GetByStartIdempotencyKeyAsync(
            binding.StartIdempotencyKey, cancellationToken);
        if (operation is null || operation.TenantId != tenantId
            || operation.GskuId != gskuId
            || operation.ProductDefinitionRevisionId != revision.Id
            || operation.MakerSubjectId != requesterSubjectId
            || operation.WorkflowInstanceId != binding.WorkflowInstanceId
            || operation.ApprovalTaskId != binding.ApprovalTaskId
            || operation.WorkflowTemplateId != binding.WorkflowTemplateId
            || operation.WorkflowTemplateVersionId != binding.WorkflowTemplateVersionId)
            return Fail("FIRST_GSKU_IDENTITY_WORKFLOW_BINDING_CONFLICT", 409);

        var fingerprint = WithdrawalFingerprint(tenantId, gskuId, expectedGskuVersion,
            commandId, requesterSubjectId, reasonCode, comment, binding);
        if (operation.WithdrawalCommandId.HasValue
            && !ExactWithdrawalFacts(operation, commandId, fingerprint, requesterSubjectId,
                expectedGskuVersion, reasonCode, comment))
            // The requester is the submitter of this approval in this tenant (checked above): they may learn the
            // command their earlier withdrawal stands under, and repeat THAT one.
            return Fail("FIRST_GSKU_IDENTITY_WITHDRAWAL_OPERATION_CONFLICT", 409)
                with { PendingWithdrawalCommandId = operation.WithdrawalCommandId };
        if (operation.WithdrawalCommandId.HasValue
            && operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.Completed)
            return ExactWithdrawalState(operation, revision, gsku)
                ? Success(operation, true)
                : Fail("FIRST_GSKU_IDENTITY_WITHDRAWAL_REPLAY_DRIFT", 409);
        if (gsku.Version != expectedGskuVersion
            || revision.Version != operation.ExpectedRevisionVersion + 1
            || gsku.LifecycleStatus != ProductIdentityLifecycleStatus.PendingIdentityApproval
            || revision.LifecycleStatus != ProductIdentityLifecycleStatus.PendingIdentityApproval)
            return Fail("FIRST_GSKU_IDENTITY_STATE_CONFLICT", 409);
        if (!operation.WithdrawalCommandId.HasValue
            && operation.Checkpoint != FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision)
            return Fail("FIRST_GSKU_IDENTITY_WORKFLOW_STATE_CONFLICT", 409);

        var result = await ProcessWithdrawalAsync(operation, delegatedUserToken,
            $"withdraw-{requesterSubjectId:N}", leaseDuration, retryDelay, cancellationToken,
            commandId, fingerprint, requesterSubjectId, expectedGskuVersion, reasonCode, comment);
        return result with { IsReplay = operation.WithdrawalCommandId.HasValue || result.IsReplay };
    }

    private async Task<FirstGskuIdentityWorkflowProcessingResult> ProcessWithdrawalAsync(
        FirstGskuIdentityWorkflowOperation initial,
        string? delegatedUserToken,
        string leaseOwner,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        CancellationToken cancellationToken,
        Guid? commandId = null,
        string? fingerprint = null,
        Guid? requesterSubjectId = null,
        int? expectedGskuVersion = null,
        string? reasonCode = null,
        string? comment = null)
    {
        var operation = initial;
        var attempt = new Attempt(!string.IsNullOrWhiteSpace(delegatedUserToken));
        if (initial.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision)
            attempt.ScheduleAtEntry = (true, initial.NextAttemptAtUtcTicksV1);
        for (var step = 0; step < 10; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Refused for good: the withdrawal's own answer, from its own field — the decision poll rewrites
            // LastFailureCode at every cycle, and a repeat must still be told what the first request was told.
            if (operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision
                && operation.WithdrawalCommandId.HasValue)
                return Fail(operation, operation.WithdrawalRefusalCode ??
                    "FIRST_GSKU_IDENTITY_WORKFLOW_DECISION_PENDING", 409);
            if (operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.Completed)
            {
                return operation.WithdrawalCommandId.HasValue
                    ? Success(operation, true)
                    : Fail(operation, "FIRST_GSKU_IDENTITY_WORKFLOW_STATE_CONFLICT", 409);
            }
            var reconcileWithdrawal = false;
            if (operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired)
            {
                // FIX3 E2: a withdrawal in manual reconciliation (its requester never came back for the evidence, its
                // evidence did not fit, its local write was refused…) is reconciled by its requester's repeat of the
                // SAME withdrawal (same command, checked by the caller): the repeat reads the engine again with the
                // requester's own token. If the repeat cannot settle it in this request, a person has to.
                if (step > 0 || !attempt.Interactive || operation.WithdrawalCommandId is null
                    || operation.WithdrawalCommandId != commandId)
                    return Fail(operation, step > 0 && attempt.Interactive
                        ? ManualReconciliationNeeded
                        : "FIRST_GSKU_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED", 409);
                reconcileWithdrawal = true;
            }

            var now = timeProvider.GetUtcNow();
            // Not the caller's token (a claim cut off half-way may be written with nobody to give it back) and not
            // unbounded either (a half-open connection would hang the request and the worker): see ClaimAsync.
            var (claim, storeSilent) = await ClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
                [operation.Checkpoint], leaseOwner, now.UtcTicks, now.Add(leaseDuration).UtcTicks,
                IgnoreRetrySchedule: attempt.Interactive), leaseDuration);
            if (storeSilent) return Fail(operation, ProductIdentityWorkflowClaims.StoreUnavailable, 503);
            if (claim is null) return Fail(operation, "FIRST_GSKU_IDENTITY_WORKFLOW_BUSY", 409);

            bool advanced;
            try
            {
                switch (operation.Checkpoint)
                {
                    case FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired when reconcileWithdrawal:
                        advanced = await operations.AdvanceAsync(claim, new(
                            FirstGskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown,
                            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
                            ReleaseLease: true, ServiceFaultCount: 0), cancellationToken);
                        break;
                    case FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision
                        when commandId.HasValue && fingerprint is not null && requesterSubjectId.HasValue
                             && expectedGskuVersion.HasValue && reasonCode is not null:
                        advanced = await operations.AdvanceAsync(claim, new(
                            FirstGskuIdentityWorkflowCheckpoint.WithdrawalRequested,
                            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
                            SettledNextAttempt(operation, now, retryDelay),
                            WithdrawalCommandId: commandId,
                            WithdrawalFingerprint: fingerprint,
                            WithdrawalRequesterSubjectId: requesterSubjectId,
                            WithdrawalExpectedGskuVersion: expectedGskuVersion,
                            WithdrawalReasonCode: reasonCode,
                            WithdrawalComment: comment,
                            ReleaseLease: true,
                            ServiceFaultCount: StreakIsOver(operation, now) ? 0 : null), cancellationToken);
                        break;
                    case FirstGskuIdentityWorkflowCheckpoint.WithdrawalRequested:
                        advanced = attempt.Interactive
                            ? await ObserveWithdrawalPreflightAsync(operation, claim, attempt, now, retryDelay,
                                cancellationToken)
                            : await ResolveAbandonedWithdrawalAsync(operation, claim, attempt, now, retryDelay,
                                cancellationToken);
                        break;
                    case FirstGskuIdentityWorkflowCheckpoint.WithdrawalPreflightObserved:
                    case FirstGskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown:
                    case FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay:
                        advanced = attempt.Interactive
                            ? await ObserveWithdrawalAsync(operation, claim, delegatedUserToken!, attempt, now,
                                retryDelay, cancellationToken)
                            : await ResolveAbandonedWithdrawalAsync(operation, claim, attempt, now, retryDelay,
                                cancellationToken);
                        break;
                    case FirstGskuIdentityWorkflowCheckpoint.WithdrawalObserved:
                        advanced = await ApplyWithdrawalAsync(operation, claim, now, cancellationToken);
                        break;
                    case FirstGskuIdentityWorkflowCheckpoint.WithdrawalApplied:
                        advanced = await VerifyWithdrawalAndCompleteAsync(operation, claim, now, cancellationToken);
                        break;
                    default:
                        await ReleaseClaimAsync(claim, operation, now);
                        return Fail(operation, "FIRST_GSKU_IDENTITY_WORKFLOW_STATE_CONFLICT", 409);
                }
            }
            catch
            {
                // Cancelled by the caller, or the store failed in the middle of the step: the lease must not
                // outlive the attempt, or the requester's next try is told "busy" until it runs out. The worker is
                // kept away for the settle window: a cancellation that was cut off may still land at the engine.
                await ReleaseClaimAsync(claim, operation, now, SettledNextAttempt(operation, now, retryDelay));
                throw;
            }
            if (!advanced)
            {
                await ReleaseClaimAsync(claim, operation, now);
                return Fail(operation, "FIRST_GSKU_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT", 409);
            }
            operation = await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken)
                ?? throw new InvalidOperationException("FIRST_GSKU_IDENTITY_WORKFLOW_OPERATION_LOST");
            if (attempt.Answer is { } answer)
                return Fail(operation, answer.Code, answer.Status) with { FaultScope = attempt.Scope };
            // The withdrawal was dropped and the decision is awaited again: the worker goes on polling it, in this
            // very cycle. (A requester who was just refused has been answered above.)
            if (operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision
                && !operation.WithdrawalCommandId.HasValue && !attempt.Interactive)
                return await ProcessAsync(operation, null, leaseOwner, leaseDuration, retryDelay,
                    stopWhenAwaitingDecision: false, cancellationToken);
            if (operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay)
                return MakerAnswer(operation, attempt);
            // A requester who is present walks on through the settle window their own steps wrote (it keeps the
            // WORKER away, not them); only a retry that was really scheduled ends the request.
            if (operation.NextAttemptAtUtcTicksV1 > timeProvider.GetUtcNow().UtcTicks
                && (!attempt.Interactive
                    || operation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable))
                return ScheduledAnswer(operation, attempt);
        }
        // A withdrawal that was applied is a success however many steps it took: the product is a draft already.
        if (operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.Completed
            && operation.WithdrawalCommandId.HasValue)
            return Success(operation, true);
        return Fail(operation, "FIRST_GSKU_IDENTITY_WORKFLOW_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<bool> ObserveWithdrawalPreflightAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!CompleteWithdrawalRequest(operation) || !CompleteStartProof(operation))
            return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_WITHDRAWAL_CONTRACT_INVALID", now, cancellationToken);
        var result = await workflowClient.GetCancellationPreflightAsync(operation.TenantId,
            new(operation.WorkflowInstanceId!.Value, operation.ApprovalTaskId!.Value, operation.ObjectType,
                operation.ObjectId, operation.MakerSubjectId), cancellationToken);
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.Success && result.Value is { } value)
        {
            if (value.WorkflowInstanceId != operation.WorkflowInstanceId
                || value.ApprovalTaskId != operation.ApprovalTaskId
                || !string.Equals(value.ObjectType, operation.ObjectType, StringComparison.Ordinal)
                || !string.Equals(value.ObjectId, operation.ObjectId, StringComparison.Ordinal)
                || value.WorkflowInstanceVersion <= 0 || value.ApprovalTaskVersion <= 0)
                return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_WITHDRAWAL_PREFLIGHT_INVALID", now,
                    cancellationToken);
            attempt.VersionsReadNow = true;
            return await operations.AdvanceAsync(claim, new(
                FirstGskuIdentityWorkflowCheckpoint.WithdrawalPreflightObserved,
                ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
                SettledNextAttempt(operation, now, retryDelay),
                WithdrawalExpectedWorkflowInstanceVersion: value.WorkflowInstanceVersion,
                WithdrawalExpectedApprovalTaskVersion: value.ApprovalTaskVersion,
                WithdrawalObjectRef: value.ObjectRef,
                // The fault streak is left as it stands: the versions are only read here, and what the request
                // meets is said by the cancellation that follows (its answer ends a streak, its fault goes on in it).
                ReleaseLease: true), cancellationToken);
        }
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.Conflict
            or ProductIdentityWorkflowTransportOutcome.NotFound)
            // The approval is not open any more (decided, or closed): there is nothing to withdraw. The request is
            // kept on the operation so that the same answer is given again; the decision poll takes it from here.
            return await ClosedBeforeWithdrawalAsync(operation, claim, attempt, result.ErrorCode, now,
                cancellationToken);
        var fault = ProductIdentityWorkflowFaultPolicy.Classify(
            ProductIdentityWorkflowCall.CancellationPreflight, result.Outcome, result.ErrorCode);
        if (fault.Kind == ProductIdentityWorkflowFaultKind.ServiceWait)
            return await WaitForServiceAsync(operation, claim, operation.Checkpoint,
                ProductIdentityWorkflowCall.CancellationPreflight, result.ErrorCode, fault, attempt, now, retryDelay,
                cancellationToken);
        return await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
    }

    /*
     * A withdrawal the requester did not finish, met by the WORKER. The approval exists at the engine, so somebody
     * may decide it at any moment — and the worker reads decisions only while the operation awaits one. An
     * operation that carries a live approval therefore never waits in a state the worker does not read: the worker
     * asks the engine where the approval stands and puts the operation back where the truth is.
     *   - still open            → the withdrawal did not happen: it is forgotten, the decision is awaited again;
     *   - closed, with a decision (or not yet readable) → the same: the decision poll takes it from here;
     *   - closed, and no decision can be read → the cancellation landed and only the requester's repeat (same
     *     command, their own token) can fetch its evidence: the operation stays where the worker keeps reading it.
     */
    private async Task<bool> ResolveAbandonedWithdrawalAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!CompleteWithdrawalRequest(operation) || !CompleteStartProof(operation))
            return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_WITHDRAWAL_CONTRACT_INVALID", now, cancellationToken);
        var preflight = await workflowClient.GetCancellationPreflightAsync(operation.TenantId,
            new(operation.WorkflowInstanceId!.Value, operation.ApprovalTaskId!.Value, operation.ObjectType,
                operation.ObjectId, operation.MakerSubjectId), cancellationToken);
        if (preflight.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
            return await BackToDecisionAsync(operation, claim, attempt, now, cancellationToken);
        if (preflight.Outcome is ProductIdentityWorkflowTransportOutcome.Conflict
            or ProductIdentityWorkflowTransportOutcome.NotFound)
        {
            var evidence = await workflowClient.GetTerminalEvidenceAsync(operation.TenantId,
                new(operation.WorkflowInstanceId.Value, operation.ObjectType, operation.ObjectId), cancellationToken);
            if (evidence.Outcome is ProductIdentityWorkflowTransportOutcome.Success
                or ProductIdentityWorkflowTransportOutcome.NonTerminal
                or ProductIdentityWorkflowTransportOutcome.NotFound
                or ProductIdentityWorkflowTransportOutcome.Incomplete)
                return await BackToDecisionAsync(operation, claim, attempt, now, cancellationToken);
            var evidenceFault = ProductIdentityWorkflowFaultPolicy.Classify(
                ProductIdentityWorkflowCall.TerminalEvidence, evidence.Outcome, evidence.ErrorCode);
            if (evidenceFault.Kind == ProductIdentityWorkflowFaultKind.ServiceWait)
                return await WaitForServiceAsync(operation, claim, claim.Checkpoint,
                    ProductIdentityWorkflowCall.TerminalEvidence, evidence.ErrorCode, evidenceFault, attempt, now,
                    retryDelay, cancellationToken);
            var waitingSince = operation.WithdrawalAwaitingRequesterSinceUtcTicksV1 ?? now.UtcTicks;
            if (now.UtcTicks - waitingSince >= ProductIdentityWorkflowFaultRules.WithdrawalRequesterWaitLimit.Ticks)
            {
                logger.LogError(
                    "First GSKU identity workflow operation {OperationId} of tenant {TenantId}: the approval is closed and the withdrawal has waited for its requester since {SinceUtc:O} ({ResultCode}); nobody came back to finish it. It is handed to manual reconciliation.",
                    operation.OperationId, operation.TenantId, new DateTimeOffset(waitingSince, TimeSpan.Zero),
                    BoundedCode(evidence.ErrorCode, ProductIdentityWorkflowMakerCodes.MakerReplayRequired));
                return await QuarantineAsync(claim, WithdrawalRequesterNeverReturned, now, cancellationToken);
            }
            if (evidence.Outcome == ProductIdentityWorkflowTransportOutcome.Conflict)
                logger.LogWarning(
                    "First GSKU identity workflow operation {OperationId} of tenant {TenantId}: the approval is closed and holds no decision ({ResultCode}); the withdrawal appears to have landed and waits for its requester to repeat it. The worker keeps reading.",
                    operation.OperationId, operation.TenantId,
                    BoundedCode(evidence.ErrorCode, ProductIdentityWorkflowMakerCodes.MakerReplayRequired));
            else
                logger.LogWarning(
                    "First GSKU identity workflow operation {OperationId} of tenant {TenantId}: the approval is closed and its decision could not be accepted ({ResultCode}); whether the withdrawal landed is not known. It waits for its requester to repeat it. The worker keeps reading.",
                    operation.OperationId, operation.TenantId,
                    BoundedCode(evidence.ErrorCode, ProductIdentityWorkflowMakerCodes.MakerReplayRequired));
            return await operations.AdvanceAsync(claim, new(
                claim.Checkpoint,
                ProductIdentityWorkflowRecoveryDisposition.Retryable,
                now.UtcTicks,
                now.Add(faultSettings.MaxRetryDelay).UtcTicks,
                ProductIdentityWorkflowMakerCodes.MakerReplayRequired,
                ReleaseLease: true,
                ServiceFaultCount: 0, // the engine answered both questions
                WithdrawalAwaitingRequesterSinceUtcTicksV1: waitingSince), cancellationToken);
        }
        var fault = ProductIdentityWorkflowFaultPolicy.Classify(
            ProductIdentityWorkflowCall.CancellationPreflight, preflight.Outcome, preflight.ErrorCode);
        if (fault.Kind == ProductIdentityWorkflowFaultKind.ServiceWait)
            return await WaitForServiceAsync(operation, claim, claim.Checkpoint,
                ProductIdentityWorkflowCall.CancellationPreflight, preflight.ErrorCode, fault, attempt, now, retryDelay,
                cancellationToken);
        return await QuarantineAsync(claim, preflight.ErrorCode, now, cancellationToken);
    }

    // The withdrawal did not happen: it is forgotten (so its requester may ask again) and the operation awaits the
    // decision once more. Nothing can land any more, so the worker's schedule goes back to what it was before the
    // request (the settle window is only for a cancellation that may still land). faultStreak is given when a
    // service fault ended the withdrawal (the streak goes on); without it the engine answered, and an answer ends
    // the streak.
    private Task<bool> BackToDecisionAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        Attempt attempt,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        (int Count, long Since)? faultStreak = null) =>
        operations.AdvanceAsync(claim, new(
            FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now.UtcTicks,
            ScheduleBeforeTheRequest(operation, attempt),
            ReleaseLease: true,
            ServiceFaultCount: faultStreak?.Count ?? 0,
            ServiceFaultSinceUtcTicksV1: faultStreak?.Since,
            ClearWithdrawal: true), cancellationToken);

    // The approval was decided or closed before the withdrawal reached it. The request stays on the operation (the
    // same answer is given to a repeat) and the decision poll goes on; the engine answered, so a fault streak ends.
    private Task<bool> ClosedBeforeWithdrawalAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        Attempt attempt,
        string? errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // The engine's own reason, when it is one that can be stored and handed out; never a text of any length.
        var code = BoundedCode(errorCode, ProductIdentityWorkflowMakerCodes.ApprovalClosed);
        return operations.AdvanceAsync(claim, new(
            FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now.UtcTicks,
            ScheduleBeforeTheRequest(operation, attempt),
            code,
            ReleaseLease: true,
            ServiceFaultCount: 0,
            WithdrawalRefusalCode: code), cancellationToken);
    }

    // What the worker had scheduled before this request began the withdrawal; a request that only resumed one does
    // not know, and leaves the schedule as it stands.
    private static long? ScheduleBeforeTheRequest(
        FirstGskuIdentityWorkflowOperation operation, Attempt attempt) =>
        attempt.ScheduleAtEntry.Known ? attempt.ScheduleAtEntry.Ticks : operation.NextAttemptAtUtcTicksV1;

    /*
     * The cancellation was refused before it was processed — but an EARLIER cancellation of this withdrawal may
     * have landed (its answer was lost, or its request was cut off). Forgetting the withdrawal now would leave an
     * approval that is cancelled at the engine to the decision poll, which can only quarantine it. So the operation
     * stays a withdrawal whose outcome is unknown; the worker reads the approval and decides. Nothing new can land
     * from this attempt, so the worker's schedule is not moved.
     */
    private Task<bool> KeepUnknownWithdrawalAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        string code,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        (int Count, long Since)? faultStreak = null) =>
        operations.AdvanceAsync(claim, new(
            FirstGskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown,
            ProductIdentityWorkflowRecoveryDisposition.Retryable,
            now.UtcTicks,
            operation.NextAttemptAtUtcTicksV1,
            code,
            ReleaseLease: true,
            ServiceFaultCount: faultStreak?.Count ?? 0,
            ServiceFaultSinceUtcTicksV1: faultStreak?.Since), cancellationToken);

    private async Task<bool> ObserveWithdrawalAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        string delegatedUserToken,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!CompleteWithdrawalPreflight(operation))
            return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_WITHDRAWAL_PREFLIGHT_INVALID", now,
                cancellationToken);
        var request = new ProductIdentityWorkflowCancellationRequest(
            operation.WorkflowInstanceId!.Value, operation.ApprovalTaskId!.Value, operation.ObjectType,
            operation.ObjectId, operation.MakerSubjectId,
            operation.WithdrawalExpectedWorkflowInstanceVersion!.Value,
            operation.WithdrawalExpectedApprovalTaskVersion!.Value,
            operation.WithdrawalReasonCode!, operation.WithdrawalComment,
            operation.WithdrawalCommandId!.Value.ToString("D"));
        var result = await workflowClient.CancelAsync(operation.TenantId, request, delegatedUserToken,
            cancellationToken);
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.Success && result.Value is { } evidence)
        {
            return await operations.AdvanceAsync(claim, new(
                FirstGskuIdentityWorkflowCheckpoint.WithdrawalObserved,
                ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
                WithdrawalTransitionLogId: evidence.TransitionLogId,
                WithdrawalObservedAtUtcTicksV1: evidence.DecisionAt.UtcTicks,
                WithdrawalTransitionSequence: evidence.TransitionSequence,
                WithdrawalResultWorkflowInstanceVersion: evidence.WorkflowInstanceVersion,
                WithdrawalResultApprovalTaskVersion: evidence.ApprovalTaskVersion,
                WithdrawalTaskStatus: evidence.TaskStatus,
                WithdrawalInstanceStatus: evidence.InstanceStatus,
                WithdrawalObjectRef: evidence.ObjectRef,
                ReleaseLease: true,
                ServiceFaultCount: 0), cancellationToken);
        }
        // Only a cancellation whose versions were read in this very request is known to be the first one.
        var earlierMayHaveLanded = !(attempt.VersionsReadNow
            && claim.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.WithdrawalPreflightObserved);
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound)
            return await ClosedBeforeWithdrawalAsync(operation, claim, attempt, result.ErrorCode, now,
                cancellationToken);
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.Conflict)
        {
            /*
             * The engine answers "conflict" for two different things: the approval was decided or closed — or it is
             * STILL OPEN and merely moved on since the versions were read (somebody delegated it, a reminder ran).
             * Only the first means there is nothing to withdraw. So the approval is asked: still open → this
             * withdrawal is forgotten and, the requester being here, asked again with fresh versions in this same
             * request; not open → nothing to withdraw; no answer → the worker finds out.
             */
            var open = await workflowClient.GetCancellationPreflightAsync(operation.TenantId,
                new(operation.WorkflowInstanceId.Value, operation.ApprovalTaskId.Value, operation.ObjectType,
                    operation.ObjectId, operation.MakerSubjectId), cancellationToken);
            if (open.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
            {
                logger.LogInformation(
                    "First GSKU identity workflow operation {OperationId} of tenant {TenantId}: the approval moved on while it was being withdrawn ({ResultCode}) and is still open; the withdrawal is asked again with fresh versions.",
                    operation.OperationId, operation.TenantId,
                    BoundedCode(result.ErrorCode, "FIRST_GSKU_IDENTITY_WORKFLOW_PROVIDER_UNAVAILABLE"));
                // Once per request. An approval that conflicts again is changing right now: the requester is told
                // so, and the withdrawal is forgotten — a repeat begins a fresh one.
                if (attempt.ConflictRetried)
                    attempt.Answer = (ProductIdentityWorkflowMakerCodes.ApprovalChanging,
                        ProductIdentityWorkflowMakerCodes.StatusFor(ProductIdentityWorkflowMakerCodes.ApprovalChanging));
                attempt.ConflictRetried = true;
                return await BackToDecisionAsync(operation, claim, attempt, now, cancellationToken);
            }
            if (open.Outcome is ProductIdentityWorkflowTransportOutcome.Conflict
                or ProductIdentityWorkflowTransportOutcome.NotFound)
                return await ClosedBeforeWithdrawalAsync(operation, claim, attempt, result.ErrorCode, now,
                    cancellationToken);
            var openFault = ProductIdentityWorkflowFaultPolicy.Classify(
                ProductIdentityWorkflowCall.CancellationPreflight, open.Outcome, open.ErrorCode);
            if (openFault.Kind != ProductIdentityWorkflowFaultKind.ServiceWait)
                return await QuarantineAsync(claim, open.ErrorCode, now, cancellationToken);
            var openStreak = FaultStreak(operation, now, countsAsWorkerAttempt: false);
            attempt.Scope = openFault.Scope;
            attempt.Answer = (ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, 503);
            var openCode = BoundedCode(open.ErrorCode, "FIRST_GSKU_IDENTITY_WORKFLOW_PROVIDER_UNAVAILABLE");
            LogServiceFault(operation, ProductIdentityWorkflowCall.CancellationPreflight, openCode, openStreak.Count,
                openStreak.Since, now, openFault.Loud,
                "the worker reads the approval to learn whether it is still open");
            return await operations.AdvanceAsync(claim, new(
                FirstGskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown,
                ProductIdentityWorkflowRecoveryDisposition.Retryable,
                now.UtcTicks,
                SettledNextAttempt(operation, now, retryDelay),
                openCode,
                ReleaseLease: true,
                ServiceFaultCount: openStreak.Count,
                ServiceFaultSinceUtcTicksV1: openStreak.Since), cancellationToken);
        }

        // The cancellation carries the requester's own token: the worker can never repeat it. The approval, though,
        // is still at the engine and may be decided at any moment — so whatever stops the cancellation, the
        // operation ends where the worker reads it, and the requester is told to ask again.
        var fault = ProductIdentityWorkflowFaultPolicy.Classify(
            ProductIdentityWorkflowCall.Cancel, result.Outcome, result.ErrorCode);
        var code = BoundedCode(result.ErrorCode, "FIRST_GSKU_IDENTITY_WORKFLOW_PROVIDER_UNAVAILABLE");
        switch (fault.Kind)
        {
            case ProductIdentityWorkflowFaultKind.MakerReplay:
                // Refused for a reason that is the requester's (their session): nothing was cancelled.
                logger.LogWarning(
                    "First GSKU identity workflow operation {OperationId} of tenant {TenantId} was refused to the maker on {Call}: {ResultCode} -> {MakerCode}.",
                    operation.OperationId, operation.TenantId, ProductIdentityWorkflowCall.Cancel, code,
                    fault.MakerCode);
                attempt.Answer = (fault.MakerCode!, ProductIdentityWorkflowMakerCodes.StatusFor(fault.MakerCode));
                return earlierMayHaveLanded
                    ? await KeepUnknownWithdrawalAsync(operation, claim, code, now, cancellationToken)
                    : await BackToDecisionAsync(operation, claim, attempt, now, cancellationToken);
            case ProductIdentityWorkflowFaultKind.ServiceWait:
            {
                var streak = FaultStreak(operation, now, countsAsWorkerAttempt: false);
                attempt.Scope = fault.Scope;
                attempt.Answer = (ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, 503);
                if (fault.StartedNothing)
                {
                    // Refused before it was processed: THIS cancellation did not happen.
                    LogServiceFault(operation, ProductIdentityWorkflowCall.Cancel, code, streak.Count,
                        streak.Since, now, fault.Loud,
                        earlierMayHaveLanded
                            ? "an earlier cancellation may have landed; the worker reads the approval to learn whether it did"
                            : "the withdrawal was dropped and its requester repeats it; the decision is awaited again");
                    return earlierMayHaveLanded
                        ? await KeepUnknownWithdrawalAsync(operation, claim, code, now, cancellationToken, streak)
                        : await BackToDecisionAsync(operation, claim, attempt, now, cancellationToken, streak);
                }

                // No answer: the cancellation may have landed — or may still land. The worker reads the approval
                // to find out, and not before the settle window has passed.
                LogServiceFault(operation, ProductIdentityWorkflowCall.Cancel, code, streak.Count,
                    streak.Since, now, fault.Loud,
                    "the worker reads the approval to learn whether the withdrawal landed");
                return await operations.AdvanceAsync(claim, new(
                    FirstGskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown,
                    ProductIdentityWorkflowRecoveryDisposition.Retryable,
                    now.UtcTicks,
                    SettledNextAttempt(operation, now, retryDelay),
                    code,
                    ReleaseLease: true,
                    ServiceFaultCount: streak.Count,
                    ServiceFaultSinceUtcTicksV1: streak.Since), cancellationToken);
            }
            default:
                return await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
        }
    }

    private async Task<bool> ApplyWithdrawalAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!TryRehydrateCancellationEvidence(operation, out var evidence))
            return await QuarantineAsync(claim,
                "FIRST_GSKU_IDENTITY_WITHDRAWAL_EVIDENCE_INVALID", now, cancellationToken);
        var revision = await revisions.GetByIdAsync(operation.ProductDefinitionRevisionId, cancellationToken);
        var gsku = await gskus.GetByIdAsync(operation.GskuId, cancellationToken);
        if (revision is null || gsku is null)
            return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_PAIR_INCONSISTENT", now,
                cancellationToken);
        var revisionAudit = CreateWithdrawalAudit(revision, AuditAggregateType.ProductDefinitionRevision,
            ProductAuditOperation.ProductDefinitionRevisionIdentityApprovalWithdrawn,
            operation.ExpectedRevisionVersion + 1, operation, evidence);
        var gskuAudit = CreateWithdrawalAudit(gsku, AuditAggregateType.Gsku,
            ProductAuditOperation.GskuIdentityApprovalWithdrawn,
            operation.WithdrawalExpectedGskuVersion!.Value, operation, evidence);
        var result = await operations.ApplyWithdrawalAsync(
            claim, operation, evidence, revisionAudit, gskuAudit, now.UtcTicks, cancellationToken);
        return result.Succeeded;
    }

    private async Task<bool> VerifyWithdrawalAndCompleteAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var revision = await revisions.GetByIdAsync(operation.ProductDefinitionRevisionId, cancellationToken);
        var gsku = await gskus.GetByIdAsync(operation.GskuId, cancellationToken);
        if (!ExactWithdrawalState(operation, revision, gsku))
            return await QuarantineAsync(claim,
                "FIRST_GSKU_IDENTITY_WITHDRAWAL_LOCAL_STATE_INCONSISTENT", now, cancellationToken);
        return await AdvanceAsync(claim, FirstGskuIdentityWorkflowCheckpoint.Completed,
            now, releaseLease: true, cancellationToken: cancellationToken);
    }

    private async Task<FirstGskuIdentityWorkflowProcessingResult> ProcessAsync(
        FirstGskuIdentityWorkflowOperation initial,
        string? delegatedUserToken,
        string leaseOwner,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        bool stopWhenAwaitingDecision,
        CancellationToken cancellationToken)
    {
        var operation = initial;
        var attempt = new Attempt(!string.IsNullOrWhiteSpace(delegatedUserToken));
        var manualResumeAttempted = false;
        for (var step = 0; step < 18; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.Completed)
            {
                return Success(operation, true);
            }
            if (operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.DecisionNotApplicable)
            {
                return Fail(operation, operation.LastFailureCode ?? DecisionNotApplicableCode, 409);
            }
            if (operation.Checkpoint is FirstGskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart
                or FirstGskuIdentityWorkflowCheckpoint.Superseded)
            {
                // FIX2 item 1: a submit that ended before reaching the engine cannot be repeated at the same draft
                // version — the maker is told so (and edits the draft), not answered "done" while nothing happened.
                return attempt.Interactive
                    ? Fail(operation, SubmissionEnded, 409)
                    : Success(operation, true);
            }
            if (stopWhenAwaitingDecision && !attempt.ObserveNow
                && operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision)
            {
                return Success(operation, false);
            }
            if (operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
                && string.IsNullOrWhiteSpace(delegatedUserToken))
            {
                return Fail(operation, "FIRST_GSKU_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED", 409);
            }
            var reconcileStart = false;
            var reconcileAfterStart = false;
            if (operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired)
            {
                // FIX2 item 1 (b) / item 6, CT decision C(A): the submitter's repeat of the same submit is the human way
                // out of manual reconciliation for a submit that has no approval on record — it reads the engine.
                // Only a repeat: the request that put the submit into reconciliation does not settle it itself.
                reconcileStart = !manualResumeAttempted && attempt.Interactive
                                 && initial.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired
                                 && !operation.WorkflowInstanceId.HasValue && !operation.DecisionKind.HasValue;
                // FIX3 E2: after the engine started the approval, the same repeat reads the engine again and puts the
                // operation back where the pair stands (see ReconcileAfterStartAsync); a quarantine it cannot settle
                // stays frozen and the submitter is told so in words — never "submit again" when that does not help.
                reconcileAfterStart = !reconcileStart && !manualResumeAttempted && attempt.Interactive
                                      && initial.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired
                                      && operation.WithdrawalCommandId is null && CompleteStartProof(operation)
                                      && !CanResumeApprovalDependency(operation, delegatedUserToken);
                if (!reconcileStart && !reconcileAfterStart
                    && (manualResumeAttempted || !CanResumeApprovalDependency(operation, delegatedUserToken)))
                {
                    return Fail(operation, manualResumeAttempted && initial.Checkpoint
                        == FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired && attempt.Interactive
                            ? ManualReconciliationNeeded
                            : ReconciliationRequired, 409);
                }
            }
            if (operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired
                && !reconcileStart && !reconcileAfterStart)
            {

                var parent = await globalProducts.GetByIdAsync(operation.GlobalProductId, cancellationToken);
                if (parent is null || parent.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved)
                {
                    return Fail(operation, "FIRST_GSKU_IDENTITY_PARENT_NOT_APPROVED", 409);
                }
            }

            var now = timeProvider.GetUtcNow();
            var (claim, storeSilent) = await ClaimAsync(new(
                operation.OperationId,
                operation.OperationFingerprint,
                [operation.Checkpoint],
                leaseOwner,
                now.UtcTicks,
                now.Add(leaseDuration).UtcTicks,
                IgnoreRetrySchedule: attempt.Interactive), leaseDuration);
            if (storeSilent)
            {
                return Fail(operation, ProductIdentityWorkflowClaims.StoreUnavailable, 503);
            }
            if (claim is null)
            {
                return Fail(operation, "FIRST_GSKU_IDENTITY_WORKFLOW_BUSY", 409);
            }

            bool advanced;
            try
            {
            advanced = operation.Checkpoint switch
            {
                FirstGskuIdentityWorkflowCheckpoint.Prepared =>
                    await AdvanceAsync(claim, FirstGskuIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                        now, releaseLease: true, cancellationToken: cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.StartOutcomeUnknown or
                    FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay =>
                    await ResolveStartAsync(operation, claim, delegatedUserToken, attempt, now, retryDelay,
                        cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted =>
                    await ApplyRevisionPendingAsync(operation, claim, now, cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.RevisionPendingApplied =>
                    await ApplyGskuPendingAsync(operation, claim, now, cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.PairPendingApplied =>
                    await AdvanceAsync(claim, FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision,
                        now, releaseLease: true, cancellationToken: cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision =>
                    await ObserveDecisionAsync(operation, claim, attempt, now, retryDelay, cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.DecisionObserved =>
                    await BeginDecisionAsync(operation, claim, attempt, now, retryDelay, cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.ApprovalValidated =>
                    await ApplyRevisionApprovalAsync(operation, claim, attempt, now, retryDelay, cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.RevisionApproved =>
                    await ApplyGskuApprovalAsync(operation, claim, attempt, now, retryDelay, cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.PairApproved or
                    FirstGskuIdentityWorkflowCheckpoint.PairDraftRestored =>
                    await VerifyAndCompleteAsync(operation, claim, attempt, now, retryDelay, cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.GskuDraftRestored =>
                    await ApplyRevisionRejectionAsync(operation, claim, now, cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired when reconcileStart =>
                    await ReconcileStartAsync(operation, claim, attempt, now, cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired when reconcileAfterStart =>
                    await ReconcileAfterStartAsync(operation, claim, attempt, now, cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired =>
                    await AdvanceAsync(
                        claim, FirstGskuIdentityWorkflowCheckpoint.DecisionObserved,
                        now, releaseLease: true, cancellationToken: cancellationToken),
                _ => false
            };
            }
            catch
            {
                // Cancelled by the caller, or the store failed in the middle of the step: the lease must not
                // outlive the attempt, or the maker's next submit is told "busy" until it runs out.
                await ReleaseClaimAsync(claim, operation, now);
                throw;
            }
            if (!advanced)
            {
                await ReleaseClaimAsync(claim, operation, now);
                return Fail(operation, "FIRST_GSKU_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT", 409);
            }

            if (operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired)
            {
                manualResumeAttempted = true;
            }

            operation = await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken)
                ?? throw new InvalidOperationException("FIRST_GSKU_IDENTITY_WORKFLOW_OPERATION_LOST");
            // The step that just ran has something to tell the caller (a service fault met by a maker who is
            // present, a refusal): the answer goes out now, the refused call is not repeated.
            if (attempt.Answer is { } answer)
            {
                return Fail(operation, answer.Code, answer.Status) with { FaultScope = attempt.Scope };
            }
            if (operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
                && attempt.Interactive)
            {
                return MakerAnswer(operation, attempt);
            }
            if (operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.DecisionNotApplicable)
            {
                return Fail(operation, operation.LastFailureCode ?? DecisionNotApplicableCode, 409);
            }
            if (operation.NextAttemptAtUtcTicksV1 > timeProvider.GetUtcNow().UtcTicks
                && (!attempt.Interactive
                    || operation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable))
            {
                return ScheduledAnswer(operation, attempt);
            }
        }

        return Fail(operation, "FIRST_GSKU_IDENTITY_WORKFLOW_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<bool> ResolveStartAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        string? delegatedUserToken,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        // A start carries the maker's own token, so only a maker's submit can make it. The worker can find out
        // whether an earlier start arrived (the lookup below) and nothing more: whenever a start is known not to
        // have happened, the operation is the maker's to resubmit — it is never reported as "queued".
        var makerPresent = !string.IsNullOrWhiteSpace(delegatedUserToken);
        (ProductIdentityWorkflowFaultDecision Fault, string? Code)? unansweredStart = null;
        if (makerPresent)
        {
            var started = await workflowClient.StartAsync(
                operation.TenantId, requestFactory.Rehydrate(operation), delegatedUserToken!, cancellationToken);
            if (started.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
            {
                return await PersistStartedAsync(operation, claim, started.Value, now, cancellationToken);
            }
            if (started.Outcome != ProductIdentityWorkflowTransportOutcome.Incomplete)
            {
                var fault = ProductIdentityWorkflowFaultPolicy.Classify(
                    ProductIdentityWorkflowCall.Start, started.Outcome, started.ErrorCode);
                switch (fault.Kind)
                {
                    case ProductIdentityWorkflowFaultKind.Quarantine when RefusedBeforeProcessing(started.Outcome, started.ErrorCode):
                        // FIX2 item 1 (b): the engine refused the start before processing it — nothing exists there.
                        // Not a reconciliation case: the submit ends, the draft opens, the maker edits and submits again.
                        return await AbandonAsync(claim, BoundedCode(started.ErrorCode, "FIRST_GSKU_IDENTITY_WORKFLOW_START_REFUSED"),
                            now, cancellationToken);
                    case ProductIdentityWorkflowFaultKind.Quarantine:
                        return await QuarantineAsync(claim, started.ErrorCode, now, cancellationToken);
                    case ProductIdentityWorkflowFaultKind.MakerReplay:
                        return await ToMakerAsync(operation, claim, ProductIdentityWorkflowCall.Start,
                            started.ErrorCode, fault.MakerCode!, null, attempt, now, cancellationToken);
                    case ProductIdentityWorkflowFaultKind.ServiceWait when fault.StartedNothing:
                        return await ToMakerAsync(operation, claim, ProductIdentityWorkflowCall.Start,
                            started.ErrorCode, ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, fault,
                            attempt, now, cancellationToken);
                }
                // No answer, or one nobody could accept: the start may have arrived. Ask.
                unansweredStart = (fault, started.ErrorCode);
            }
        }

        var lookup = await workflowClient.GetStartResultAsync(
            operation.TenantId,
            new(operation.ObjectType, operation.ObjectId, operation.MakerSubjectId, operation.StartIdempotencyKey),
            cancellationToken);
        if (lookup.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
        {
            return await PersistStartedAsync(operation, claim, lookup.Value, now, cancellationToken);
        }
        if (lookup.Outcome is ProductIdentityWorkflowTransportOutcome.NotFound
            or ProductIdentityWorkflowTransportOutcome.Incomplete)
        {
            // "Nothing arrived" is the engine answering for this operation: the streak of faults ends here. The
            // start that went unanswered is still what the maker met — said as a service fault, resubmit.
            if (unansweredStart is { } unanswered)
            {
                return await ToMakerAsync(operation, claim, ProductIdentityWorkflowCall.Start, unanswered.Code,
                    ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, unanswered.Fault, attempt, now,
                    cancellationToken, answeredSince: true);
            }
            logger.LogWarning(
                "First GSKU identity workflow operation {OperationId} of tenant {TenantId} has no completed start at the approval engine ({ResultCode}); it is the maker's to resubmit.",
                operation.OperationId, operation.TenantId,
                BoundedCode(lookup.ErrorCode, ProductIdentityWorkflowMakerCodes.MakerReplayRequired));
            return await operations.AdvanceAsync(claim, new(
                FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay,
                now.UtcTicks,
                LastFailureCode: makerPresent
                    ? ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit
                    : BoundedCode(lookup.ErrorCode, ProductIdentityWorkflowMakerCodes.MakerReplayRequired),
                ReleaseLease: true,
                ServiceFaultCount: 0), cancellationToken);
        }
        var lookupFault = ProductIdentityWorkflowFaultPolicy.Classify(
            ProductIdentityWorkflowCall.StartResult, lookup.Outcome, lookup.ErrorCode);
        return lookupFault.Kind == ProductIdentityWorkflowFaultKind.ServiceWait
            ? await WaitForServiceAsync(operation, claim, FirstGskuIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                ProductIdentityWorkflowCall.StartResult, lookup.ErrorCode, lookupFault, attempt, now, retryDelay,
                cancellationToken)
            : await QuarantineAsync(claim, lookup.ErrorCode, now, cancellationToken);
    }

    // An approval engine refusal of the start that it gives before processing it (a request it rejects, a tenant it
    // will not serve this service for): nothing was started.
    private static bool RefusedBeforeProcessing(ProductIdentityWorkflowTransportOutcome outcome, string? code) =>
        IdentityApprovalBinding.RefusedBeforeProcessing(outcome, code);

    private Task<bool> AbandonAsync(FirstGskuIdentityWorkflowClaim claim, string code, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "First GSKU identity workflow operation {OperationId} of tenant {TenantId} ended before the approval engine started anything ({ResultCode}); the draft is open again.",
            claim.OperationId, claim.TenantId, code);
        return operations.AdvanceAsync(claim, new(
            FirstGskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
            LastFailureCode: code, ReleaseLease: true, ServiceFaultCount: 0), cancellationToken);
    }

    // CT decision C(A): the submitter is back on a submit in manual reconciliation that has no approval on record.
    // What the engine knows decides: it has the start → the submit goes on (the decision is applied as usual); it has
    // nothing under this submit's key → the submit ends and the draft opens; it cannot say → still manual (409, the
    // screen tells the submitter to try again later).
    private async Task<bool> ReconcileStartAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        Attempt attempt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var lookup = await workflowClient.GetStartResultAsync(
            operation.TenantId,
            new(operation.ObjectType, operation.ObjectId, operation.MakerSubjectId, operation.StartIdempotencyKey),
            cancellationToken);
        if (lookup.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
            return await PersistStartedAsync(operation, claim, lookup.Value, now, cancellationToken);
        if (lookup.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound)
            return await AbandonAsync(claim, "FIRST_GSKU_IDENTITY_RECONCILED_NOTHING_STARTED", now, cancellationToken);
        attempt.Answer = (ReconciliationRequired, 409);
        await ReleaseClaimAsync(claim, operation, now);
        return true;
    }

    /*
     * FIX3 E2 — the submitter's repeat of a submit quarantined AFTER the engine started its approval. FIX2's way out
     * (C(A)) covered only a submit that never started; every later quarantine refused the repeat and the screen said
     * "submit again, that settles it". Now the repeat reads the engine and puts the operation back where the pair
     * stands, and the ordinary steps take it from there:
     *   pair pending          → awaiting the decision (decided → applied, or not applicable → draft; closed without a
     *                            decision → draft, E1; still open → it waits, "the approval is in the Task Center");
     *   pair not yet pending  → started (the pending writes are made again, then the same);
     *   decision on record    → decision observed (its application is tried again);
     *   anything else, or an engine that does not know the approval → stays frozen, and the answer says a person has
     *   to reconcile it (ManualReconciliationNeeded) — until the reconciliation endpoint of BL-530 exists.
     * A service the engine cannot be reached through is said as such; nothing changes.
     */
    private async Task<bool> ReconcileAfterStartAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        Attempt attempt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var outcome = await workflowClient.GetTerminalEvidenceAsync(
            operation.TenantId,
            new(operation.WorkflowInstanceId!.Value, operation.ObjectType, operation.ObjectId),
            cancellationToken);
        var closed = outcome.Outcome is ProductIdentityWorkflowTransportOutcome.Success
                     || ProductIdentityWorkflowFaultPolicy.IsClosedWithoutDecision(outcome);
        var open = outcome.Outcome is ProductIdentityWorkflowTransportOutcome.NonTerminal
            or ProductIdentityWorkflowTransportOutcome.Incomplete;
        if (!closed && !open)
        {
            var fault = outcome.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound
                ? null
                : ProductIdentityWorkflowFaultPolicy.Classify(
                    ProductIdentityWorkflowCall.TerminalEvidence, outcome.Outcome, outcome.ErrorCode);
            attempt.Answer = fault?.Kind == ProductIdentityWorkflowFaultKind.ServiceWait
                ? (ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, 503)
                : (ManualReconciliationNeeded, 409);
            await ReleaseClaimAsync(claim, operation, now);
            return true;
        }
        var revision = await revisions.GetByIdAsync(operation.ProductDefinitionRevisionId, cancellationToken);
        var gsku = await gskus.GetByIdAsync(operation.GskuId, cancellationToken);
        // FIX4 item 5: a decision on record goes back to the gate the pair itself is at — not to the first one, where a
        // unit retired after the approval would be quarantined again; from the third gate CT decision 1 applies.
        FirstGskuIdentityWorkflowCheckpoint? reentry =
            operation.DecisionKind == ProductIdentityDecisionKind.Approved
                ? ExactApprovedRevision(revision, operation) && ExactApprovedGsku(gsku, operation)
                    ? FirstGskuIdentityWorkflowCheckpoint.PairApproved
                : ExactApprovedRevision(revision, operation) && ExactPendingGsku(gsku, operation)
                    ? FirstGskuIdentityWorkflowCheckpoint.RevisionApproved
                : ExactPendingRevision(revision, operation) && ExactPendingGsku(gsku, operation)
                    ? FirstGskuIdentityWorkflowCheckpoint.DecisionObserved
                : null
            : operation.DecisionKind == ProductIdentityDecisionKind.Rejected
                ? ExactRejectedRevision(revision, operation) && ExactRejectedGsku(gsku, operation)
                    ? FirstGskuIdentityWorkflowCheckpoint.PairDraftRestored
                : ExactPendingRevision(revision, operation) && ExactRejectedGsku(gsku, operation)
                    ? FirstGskuIdentityWorkflowCheckpoint.GskuDraftRestored
                : ExactPendingRevision(revision, operation) && ExactPendingGsku(gsku, operation)
                    ? FirstGskuIdentityWorkflowCheckpoint.DecisionObserved
                : null
            : ExactPendingRevision(revision, operation) && ExactPendingGsku(gsku, operation)
                ? FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision
            : ExactInitialGsku(gsku, operation)
              && (ExactInitialRevision(revision, operation) || ExactPendingRevision(revision, operation))
                ? FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted
            : null;
        if (reentry is null)
        {
            attempt.Answer = (ManualReconciliationNeeded, 409);
            await ReleaseClaimAsync(claim, operation, now);
            return true;
        }
        logger.LogWarning(
            "First GSKU identity workflow operation {OperationId} of tenant {TenantId}: the submitter's repeat reconciled it at {Checkpoint} (the engine's approval is {State}).",
            operation.OperationId, operation.TenantId, reentry, closed ? "closed" : "open");
        attempt.ObserveNow = closed;
        // Back to "started": the start's own facts go with it (the store accepts "started" only with all of them).
        var started = reentry == FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted;
        return await operations.AdvanceAsync(claim, new(
            reentry.Value,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now.UtcTicks,
            WorkflowInstanceId: started ? operation.WorkflowInstanceId : null,
            WorkflowTemplateId: started ? operation.WorkflowTemplateId : null,
            WorkflowTemplateVersionId: started ? operation.WorkflowTemplateVersionId : null,
            ApprovalTaskId: started ? operation.ApprovalTaskId : null,
            AssignmentSnapshotId: started ? operation.AssignmentSnapshotId : null,
            StartTransitionLogId: started ? operation.StartTransitionLogId : null,
            WorkflowStartedAtUtcTicksV1: started ? operation.WorkflowStartedAtUtcTicksV1 : null,
            ReleaseLease: true,
            ServiceFaultCount: 0), cancellationToken);
    }

    private async Task<bool> PersistStartedAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        ProductIdentityWorkflowStartResult? result,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (result is null || result.WorkflowInstanceId == Guid.Empty || result.TemplateId == Guid.Empty
            || result.TemplateVersionId == Guid.Empty || result.ApprovalTaskId == Guid.Empty
            || result.AssignmentSnapshotId == Guid.Empty || result.StartTransitionLogId == Guid.Empty
            || result.StartedAt is not { Offset: var offset } || offset != TimeSpan.Zero
            || !string.Equals(result.ObjectRef, operation.ObjectRef, StringComparison.Ordinal))
        {
            return await QuarantineAsync(claim,
                "FIRST_GSKU_IDENTITY_WORKFLOW_START_EVIDENCE_CONFLICT", now, cancellationToken);
        }
        return await operations.AdvanceAsync(claim, new(
            FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now.UtcTicks,
            WorkflowInstanceId: result.WorkflowInstanceId,
            WorkflowTemplateId: result.TemplateId,
            WorkflowTemplateVersionId: result.TemplateVersionId,
            ApprovalTaskId: result.ApprovalTaskId,
            AssignmentSnapshotId: result.AssignmentSnapshotId,
            StartTransitionLogId: result.StartTransitionLogId,
            WorkflowStartedAtUtcTicksV1: result.StartedAt.Value.UtcTicks,
            ReleaseLease: true), cancellationToken);
    }

    private async Task<bool> ApplyRevisionPendingAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!TryBinding(operation, out var binding))
        {
            return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_WORKFLOW_BINDING_INVALID", now, cancellationToken);
        }
        var revision = await revisions.GetByIdAsync(operation.ProductDefinitionRevisionId, cancellationToken);
        if (!ExactInitialRevision(revision, operation) && !ExactPendingRevision(revision, operation))
        {
            return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_PAIR_INCONSISTENT", now, cancellationToken);
        }
        var intent = FirstGskuIdentityLifecycleAuditIntentFactory.CreateRevisionSubmit(
            revision!, operation.ExpectedRevisionVersion, binding);
        var result = await revisions.MarkIdentityPendingAsync(
            revision!.Id, operation.ExpectedRevisionVersion, binding, intent, cancellationToken);
        return result.Succeeded && ExactPendingRevision(result.Aggregate, operation)
            ? await AdvanceAsync(claim, FirstGskuIdentityWorkflowCheckpoint.RevisionPendingApplied,
                now, releaseLease: true, cancellationToken: cancellationToken)
            : await LifecycleWriteFailedAsync(claim, result.ErrorCode, now, cancellationToken);
    }

    private async Task<bool> ApplyGskuPendingAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!TryBinding(operation, out var binding))
        {
            return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_WORKFLOW_BINDING_INVALID", now, cancellationToken);
        }
        var gsku = await gskus.GetByIdAsync(operation.GskuId, cancellationToken);
        if (!ExactInitialGsku(gsku, operation) && !ExactPendingGsku(gsku, operation))
        {
            return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_PAIR_INCONSISTENT", now, cancellationToken);
        }
        var intent = FirstGskuIdentityLifecycleAuditIntentFactory.CreateGskuSubmit(
            gsku!, operation.ExpectedGskuVersion, binding);
        var result = await gskus.MarkIdentityPendingAsync(
            gsku!.Id, operation.ExpectedGskuVersion, binding, intent, cancellationToken);
        return result.Succeeded && ExactPendingGsku(result.Aggregate, operation)
            ? await AdvanceAsync(claim, FirstGskuIdentityWorkflowCheckpoint.PairPendingApplied,
                now, releaseLease: true, cancellationToken: cancellationToken)
            : await LifecycleWriteFailedAsync(claim, result.ErrorCode, now, cancellationToken);
    }

    private async Task<bool> ObserveDecisionAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!CompleteStartProof(operation))
        {
            return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_WORKFLOW_START_EVIDENCE_CONFLICT", now, cancellationToken);
        }
        var outcome = await workflowClient.GetTerminalEvidenceAsync(
            operation.TenantId,
            new(operation.WorkflowInstanceId!.Value, operation.ObjectType, operation.ObjectId),
            cancellationToken);
        if (outcome.Outcome is ProductIdentityWorkflowTransportOutcome.NonTerminal
            or ProductIdentityWorkflowTransportOutcome.NotFound
            or ProductIdentityWorkflowTransportOutcome.Incomplete)
        {
            return await ScheduleRetryAsync(claim, claim.Checkpoint,
                outcome.ErrorCode ?? "FIRST_GSKU_IDENTITY_WORKFLOW_DECISION_PENDING", now, retryDelay,
                cancellationToken, chainAnswered: true);
        }
        if (ProductIdentityWorkflowFaultPolicy.IsClosedWithoutDecision(outcome))
        {
            // FIX3 E1: the approval was closed without a decision (an administrator or its maker cancelled it in the Task
            // Center, or it expired). No decision will ever come: the pair goes back to draft in one write with the
            // audit intent that says why, the operation ends, and the maker may edit and submit again — the rule the
            // correction and the retirement request got in FIX2 (FIX2 quarantined it here, and the GSKU stayed
            // pending for ever).
            logger.LogWarning(
                "First GSKU identity workflow operation {OperationId} of tenant {TenantId}: the approval was closed without a decision ({ResultCode}); the pair goes back to draft.",
                operation.OperationId, operation.TenantId, ClosedWithoutDecision);
            return await DecisionNotApplicableAsync(operation, claim, ClosedWithoutDecision, now, cancellationToken);
        }
        if (outcome.Outcome != ProductIdentityWorkflowTransportOutcome.Success)
        {
            // The decision is polled with the service's own token: the worker carries a waiting operation on.
            var fault = ProductIdentityWorkflowFaultPolicy.Classify(
                ProductIdentityWorkflowCall.TerminalEvidence, outcome.Outcome, outcome.ErrorCode);
            return fault.Kind == ProductIdentityWorkflowFaultKind.ServiceWait
                ? await WaitForServiceAsync(operation, claim, FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision,
                    ProductIdentityWorkflowCall.TerminalEvidence, outcome.ErrorCode, fault, attempt, now, retryDelay,
                    cancellationToken)
                : await QuarantineAsync(claim, outcome.ErrorCode, now, cancellationToken);
        }
        if (outcome.Value is null)
        {
            return await QuarantineAsync(claim, outcome.ErrorCode, now, cancellationToken);
        }
        var evidence = outcome.Value;
        if (TryMapDecision(evidence.TerminalAction, out _)
            && Guid.TryParse(evidence.ActorUserId, out var self) && self == operation.MakerSubjectId)
        {
            // The maker decided their own pair (B8). The engine has closed the approval with that decision and no other
            // will come (FIX2 item 3 — FIX1's "ignore and keep waiting" left the pair pending for ever): the decision is
            // not applicable, the pair goes back to draft in one write with the audit intent that says why, and the
            // maker submits again for someone else to decide.
            logger.LogWarning(
                "First GSKU identity workflow operation {OperationId} of tenant {TenantId}: the approval was decided by its own maker; the decision is not applied ({ResultCode}).",
                operation.OperationId, operation.TenantId, SelfApprovalIgnored);
            return await DecisionNotApplicableAsync(operation, claim, SelfApprovalIgnored, now, cancellationToken,
                decisionActor: self);
        }
        if (!TryMapDecision(evidence.TerminalAction, out var decision)
            || !Guid.TryParse(evidence.ActorUserId, out var actorId) || actorId == Guid.Empty
            || evidence.WorkflowInstanceId != operation.WorkflowInstanceId
            || evidence.ApprovalTaskId != operation.ApprovalTaskId
            || evidence.TemplateId != operation.WorkflowTemplateId
            || evidence.TemplateVersionId != operation.WorkflowTemplateVersionId
            || !string.Equals(evidence.ObjectType, operation.ObjectType, StringComparison.Ordinal)
            || !string.Equals(evidence.ObjectId, operation.ObjectId, StringComparison.Ordinal)
            || !string.Equals(evidence.ObjectRef, operation.ObjectRef, StringComparison.Ordinal)
            || evidence.DecisionAt.Offset != TimeSpan.Zero || evidence.TransitionSequence <= 0
            || !ExactTerminalStatuses(decision, evidence.TaskStatus, evidence.InstanceStatus))
        {
            return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        }
        return await operations.AdvanceAsync(claim, new(
            FirstGskuIdentityWorkflowCheckpoint.DecisionObserved,
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
            ReleaseLease: true,
            ServiceFaultCount: 0), cancellationToken);
    }

    private async Task<bool> BeginDecisionAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!TryEvidence(operation, out var evidence) || !TryBinding(operation, out var binding))
        {
            return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        }
        binding.TerminalDecision = evidence;
        if (evidence.Decision == ProductIdentityDecisionKind.Rejected)
        {
            var gsku = await gskus.GetByIdAsync(operation.GskuId, cancellationToken);
            if (!ExactPendingGsku(gsku, operation) && !ExactRejectedGsku(gsku, operation))
            {
                return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_PAIR_INCONSISTENT", now, cancellationToken);
            }
            var intent = FirstGskuIdentityLifecycleAuditIntentFactory.CreateGskuDecision(
                gsku!, operation.ExpectedGskuVersion + 1, binding, evidence);
            var result = await gskus.RestoreDraftAfterRejectionAsync(
                gsku!.Id, operation.ExpectedGskuVersion + 1, binding, intent, cancellationToken);
            return result.Succeeded && ExactRejectedGsku(result.Aggregate, operation)
                ? await AdvanceAsync(claim, FirstGskuIdentityWorkflowCheckpoint.GskuDraftRestored,
                    now, releaseLease: true, cancellationToken: cancellationToken)
                : await LifecycleWriteFailedAsync(claim, result.ErrorCode, now, cancellationToken);
        }

        var approval = await RevalidateApprovalAsync(operation, cancellationToken);
        if (!approval.Succeeded)
        {
            return approval.Kind == ApprovalFailure.NotApplicable
                ? await DecisionNotApplicableAsync(operation, claim, approval.ErrorCode!, now, cancellationToken)
                : await ApprovalFailedAsync(operation, claim, approval, attempt, now, retryDelay, cancellationToken);
        }
        return await operations.AdvanceAsync(claim, new(
            FirstGskuIdentityWorkflowCheckpoint.ApprovalValidated,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now.UtcTicks,
            ApprovalPackApplicabilitySelection: approval.Applicability,
            ApprovalPackUomSelection: approval.Uom,
            ReferencesValidatedAtUtcTicksV1: now.UtcTicks,
            ApprovalReferenceProofFingerprint: ApprovalReferenceFingerprint(
                operation, approval.Applicability!, approval.Uom!, now.UtcTicks),
            ReleaseLease: true,
            ServiceFaultCount: 0), cancellationToken); // the reference service answered
    }

    private async Task<bool> ApplyRevisionApprovalAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!TryEvidence(operation, out var evidence) || !TryBinding(operation, out var binding)
            || !ExactApprovalReferenceProof(operation))
        {
            return await QuarantineAsync(
                claim, "FIRST_GSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        }
        var approval = await RevalidateApprovalAsync(operation, cancellationToken);
        // FIX4 item 5: a repeat of this gate after the revision WAS approved (cut off before the step was recorded) meets
        // a unit retired since: that is "after the approval" — applied with the submit-time proof (CT decision 1), not
        // "not applicable" (the not-applicable write needs a pending revision and would quarantine the pair).
        if (!approval.Succeeded && approval.ErrorCode == VerifiedGskuReferenceResolveResult.NotSelectable
            && ExactApprovedRevision(await revisions.GetByIdAsync(operation.ProductDefinitionRevisionId, cancellationToken),
                operation))
            approval = WithSubmitProof(operation, approval);
        if (!approval.Succeeded)
        {
            // FIX3 E2: at the second gate nothing of the pair is approved yet (both still pending): a reference the
            // maker can fix is "not applicable", as at the first gate — the pair goes back to draft. From the third gate
            // on, the revision is approved and that cannot be undone here: quarantined (the submitter's repeat
            // re-reads and tries again, FIX3 E2).
            return approval.Kind == ApprovalFailure.NotApplicable
                ? await DecisionNotApplicableAsync(operation, claim, approval.ErrorCode ?? DecisionNotApplicableCode,
                    now, cancellationToken)
                : await ApprovalFailedAsync(operation, claim, approval, attempt, now, retryDelay, cancellationToken);
        }
        if (!ExactReferenceSelection(operation.ApprovalPackApplicabilitySelection!, approval.Applicability!)
            || !ExactReferenceSelection(operation.ApprovalPackUomSelection!, approval.Uom!))
        {
            return await QuarantineAsync(
                claim, "FIRST_GSKU_IDENTITY_APPROVAL_REFERENCE_DRIFT", now, cancellationToken);
        }
        binding.TerminalDecision = evidence;
        var revision = await revisions.GetByIdAsync(operation.ProductDefinitionRevisionId, cancellationToken);
        if (!ExactPendingRevision(revision, operation) && !ExactApprovedRevision(revision, operation))
        {
            return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_PAIR_INCONSISTENT", now, cancellationToken);
        }
        var intent = FirstGskuIdentityLifecycleAuditIntentFactory.CreateRevisionDecision(
            revision!, operation.ExpectedRevisionVersion + 1, binding, evidence);
        var result = await revisions.ApproveIdentityAsync(
            revision!.Id, operation.ExpectedRevisionVersion + 1, binding, intent, cancellationToken);
        return result.Succeeded && ExactApprovedRevision(result.Aggregate, operation)
            ? await AdvanceAsync(claim, FirstGskuIdentityWorkflowCheckpoint.RevisionApproved,
                now, releaseLease: true, cancellationToken: cancellationToken)
            : await LifecycleWriteFailedAsync(claim, result.ErrorCode, now, cancellationToken);
    }

    private async Task<bool> ApplyGskuApprovalAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!TryEvidence(operation, out var evidence) || !TryBinding(operation, out var binding))
        {
            return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        }
        binding.TerminalDecision = evidence;
        var approval = WithSubmitProof(operation, await RevalidateApprovalAsync(operation, cancellationToken));
        if (approval.Succeeded && approval.ErrorCode == RetiredAfterApproval && operation.ApprovalAppliedNote is null)
        {
            // FIX3 (CT decision 1): the note is recorded first, so that a repeat of this step writes the same decision
            // evidence whatever the reference service says by then.
            return await operations.AdvanceAsync(claim, new(
                claim.Checkpoint, ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
                ReleaseLease: true, ApprovalAppliedNote: RetiredAfterApproval), cancellationToken);
        }
        if (!approval.Succeeded)
        {
            // Past the first gate the revision is already approved: a reference retired after the approval is applied
            // with the submit-time proof (WithSubmitProof); any other failure here is quarantined.
            return await ApprovalFailedAsync(operation, claim, approval, attempt, now, retryDelay, cancellationToken);
        }
        if (!ExactApprovalReferenceProof(operation)
            || !ExactReferenceSelection(operation.ApprovalPackApplicabilitySelection!, approval.Applicability!)
            || !ExactReferenceSelection(operation.ApprovalPackUomSelection!, approval.Uom!))
        {
            return await QuarantineAsync(
                claim, "FIRST_GSKU_IDENTITY_APPROVAL_REFERENCE_DRIFT", now, cancellationToken);
        }
        var gsku = await gskus.GetByIdAsync(operation.GskuId, cancellationToken);
        if (!ExactPendingGsku(gsku, operation) && !ExactApprovedGsku(gsku, operation))
        {
            return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_PAIR_INCONSISTENT", now, cancellationToken);
        }
        var intent = FirstGskuIdentityLifecycleAuditIntentFactory.CreateGskuDecision(
            gsku!, operation.ExpectedGskuVersion + 1, binding, evidence, operation.ApprovalAppliedNote);
        if (operation.ApprovalAppliedNote is not null)
            logger.LogWarning(
                "First GSKU identity workflow operation {OperationId} of tenant {TenantId}: a reference was retired after the approval; the pair is completed with the proof validated at submission ({ResultCode}).",
                operation.OperationId, operation.TenantId, RetiredAfterApproval);
        var result = await gskus.ApproveIdentityAsync(
            gsku!.Id, operation.ExpectedGskuVersion + 1, binding, intent, cancellationToken);
        return result.Succeeded && ExactApprovedGsku(result.Aggregate, operation)
            ? await AdvanceAsync(claim, FirstGskuIdentityWorkflowCheckpoint.PairApproved,
                now, releaseLease: true, cancellationToken: cancellationToken)
            : await LifecycleWriteFailedAsync(claim, result.ErrorCode, now, cancellationToken);
    }

    private async Task<bool> ApplyRevisionRejectionAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!TryEvidence(operation, out var evidence) || !TryBinding(operation, out var binding))
        {
            return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        }
        binding.TerminalDecision = evidence;
        var revision = await revisions.GetByIdAsync(operation.ProductDefinitionRevisionId, cancellationToken);
        if (!ExactPendingRevision(revision, operation) && !ExactRejectedRevision(revision, operation))
        {
            return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_PAIR_INCONSISTENT", now, cancellationToken);
        }
        var intent = FirstGskuIdentityLifecycleAuditIntentFactory.CreateRevisionDecision(
            revision!, operation.ExpectedRevisionVersion + 1, binding, evidence);
        var result = await revisions.RestoreDraftAfterRejectionAsync(
            revision!.Id, operation.ExpectedRevisionVersion + 1, binding, intent, cancellationToken);
        return result.Succeeded && ExactRejectedRevision(result.Aggregate, operation)
            ? await AdvanceAsync(claim, FirstGskuIdentityWorkflowCheckpoint.PairDraftRestored,
                now, releaseLease: true, cancellationToken: cancellationToken)
            : await LifecycleWriteFailedAsync(claim, result.ErrorCode, now, cancellationToken);
    }

    private async Task<bool> VerifyAndCompleteAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        var revision = await revisions.GetByIdAsync(operation.ProductDefinitionRevisionId, cancellationToken);
        var gsku = await gskus.GetByIdAsync(operation.GskuId, cancellationToken);
        var exact = false;
        if (operation.DecisionKind == ProductIdentityDecisionKind.Approved)
        {
            var approval = WithSubmitProof(operation, await RevalidateApprovalAsync(operation, cancellationToken));
            if (!approval.Succeeded)
            {
                return await ApprovalFailedAsync(operation, claim, approval, attempt, now, retryDelay,
                    cancellationToken);
            }
            exact = ExactApprovalReferenceProof(operation)
                && ExactReferenceSelection(operation.ApprovalPackApplicabilitySelection!, approval.Applicability!)
                && ExactReferenceSelection(operation.ApprovalPackUomSelection!, approval.Uom!)
                && ExactApprovedRevision(revision, operation) && ExactApprovedGsku(gsku, operation);
        }
        else
        {
            exact = ExactRejectedRevision(revision, operation) && ExactRejectedGsku(gsku, operation);
        }
        if (!exact || !ExactDecisionAudit(revision!, gsku!, operation))
        {
            return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_WORKFLOW_DECISION_INCONSISTENT", now, cancellationToken);
        }
        return await AdvanceAsync(claim, FirstGskuIdentityWorkflowCheckpoint.Completed,
            now, releaseLease: true, cancellationToken: cancellationToken);
    }

    /*
     * FIX3 (CT decision 1) — from the third gate on (the revision is approved): a value no longer selectable now was
     * validated and selectable when the approver approved it. A value retired later does not void a past approval, it
     * only stops new use (as in SAP and Oracle: approved master data keeps its own reference). The pair is completed
     * with the proof of the first gate, and that is said in the audit evidence and on the screen. The first gate's rule
     * (changed BEFORE the approval → not applicable) is unchanged.
     */
    public const string RetiredAfterApproval = "FIRST_GSKU_IDENTITY_REFERENCE_RETIRED_AFTER_APPROVAL";

    private static ApprovalValidation WithSubmitProof(FirstGskuIdentityWorkflowOperation operation, ApprovalValidation approval) =>
        !approval.Succeeded && approval.ErrorCode == VerifiedGskuReferenceResolveResult.NotSelectable
                            && ExactApprovalReferenceProof(operation)
            ? new(true, ApprovalFailure.None, RetiredAfterApproval,
                operation.ApprovalPackApplicabilitySelection, operation.ApprovalPackUomSelection)
            : approval;

    private async Task<ApprovalValidation> RevalidateApprovalAsync(
        FirstGskuIdentityWorkflowOperation operation,
        CancellationToken cancellationToken)
    {
        var parent = await globalProducts.GetByIdAsync(operation.GlobalProductId, cancellationToken);
        if (parent is null || parent.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved)
        {
            return new(false, ApprovalFailure.NotApplicable, "FIRST_GSKU_IDENTITY_PARENT_NOT_APPROVED");
        }
        VerifiedGskuReferenceResolveResult result;
        try
        {
            result = await references.ResolveLatestAsync(
                operation.TenantId, operation.PackApplicabilityCode, operation.PackUomCode, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new(false, ApprovalFailure.Transient, "REFERENCE_DATA_CONTRACT_UNAVAILABLE");
        }
        if (!result.IsSuccessful)
        {
            // FIX2 item 2: a value that can no longer be chosen will not come back by waiting — the approval is not
            // applicable and the pair goes back to draft with the reason. A broken contract stays a fault.
            if (result.FailureCode == VerifiedGskuReferenceResolveResult.NotSelectable)
                return new(false, ApprovalFailure.NotApplicable, VerifiedGskuReferenceResolveResult.NotSelectable);
            return new(false, result.StatusCode switch
                {
                    503 or 504 => ApprovalFailure.Transient,
                    401 or 403 => ApprovalFailure.ServiceIdentity,
                    _ => ApprovalFailure.Fault
                },
                BoundedCode(result.FailureCode, "REFERENCE_DATA_CONTRACT_UNAVAILABLE"));
        }
        var applicability = ExactSelection(result.Selections, "pack-applicability", operation.PackApplicabilityCode);
        var uom = ExactSelection(result.Selections, "uom", operation.PackUomCode);
        // A value that is no longer selectable (retired, withdrawn from new use) is a reference the maker can fix.
        return applicability is not null && uom is not null && result.Selections.Count == 2
            ? new(true, ApprovalFailure.None, null, ToSelection(applicability), ToSelection(uom))
            : new(false, ApprovalFailure.NotApplicable, "REFERENCE_DATA_CONTRACT_CONFLICT", null, null);
    }

    private static bool CanResumeApprovalDependency(
        FirstGskuIdentityWorkflowOperation operation,
        string? delegatedUserToken) =>
        !string.IsNullOrWhiteSpace(delegatedUserToken)
        && operation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired
        && operation.LastFailureCode is "FIRST_GSKU_IDENTITY_PARENT_NOT_APPROVED"
            or "REFERENCE_UNAUTHENTICATED"
        && operation.DecisionKind == ProductIdentityDecisionKind.Approved
        && operation.ApprovalPackApplicabilitySelection is null
        && operation.ApprovalPackUomSelection is null
        && !operation.ReferencesValidatedAtUtcTicksV1.HasValue
        && operation.ApprovalReferenceProofFingerprint is null
        && CompleteStartProof(operation)
        && TryEvidence(operation, out var evidence)
        && evidence.Decision == ProductIdentityDecisionKind.Approved
        && evidence.WorkflowInstanceId == operation.WorkflowInstanceId
        && evidence.ApprovalTaskId == operation.ApprovalTaskId
        && evidence.WorkflowTemplateId == operation.WorkflowTemplateId
        && evidence.WorkflowTemplateVersionId == operation.WorkflowTemplateVersionId
        && evidence.ObjectType == operation.ObjectType
        && evidence.ObjectId == operation.GskuId
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

    private static VerifiedGskuReferenceSelection? ExactSelection(
        IReadOnlyList<VerifiedGskuReferenceSelection> selections, string setCode, string valueCode) =>
        selections.SingleOrDefault(item => item.SetCode == setCode && item.ValueCode == valueCode
            && item.CatalogVersionId != Guid.Empty && item.CatalogVersionNumber > 0
            && item.ResolutionMode == "LATEST" && item.ResolvedAtUtc.Offset == TimeSpan.Zero
            && !item.IsRetired && item.SelectableForNew);

    private static ReferenceCatalogSelection ToSelection(VerifiedGskuReferenceSelection source) => new()
    {
        SetCode = source.SetCode,
        ValueCode = source.ValueCode,
        CatalogVersionId = source.CatalogVersionId,
        CatalogVersionNumber = source.CatalogVersionNumber,
        ResolutionMode = ReferenceCatalogResolutionMode.Latest,
        ResolvedAtUtc = source.ResolvedAtUtc
    };

    private static string ApprovalReferenceFingerprint(
        FirstGskuIdentityWorkflowOperation operation,
        ReferenceCatalogSelection applicability,
        ReferenceCatalogSelection uom,
        long validatedAtUtcTicks)
    {
        var facts = string.Join('|',
            "first-gsku-approval-reference-v1",
            operation.TenantId.ToString("D"),
            operation.OperationId.ToString("D"),
            operation.GskuId.ToString("D"),
            applicability.SetCode, applicability.ValueCode, applicability.CatalogVersionId.ToString("D"),
            applicability.CatalogVersionNumber, applicability.ResolvedAtUtc.UtcTicks,
            uom.SetCode, uom.ValueCode, uom.CatalogVersionId.ToString("D"),
            uom.CatalogVersionNumber, uom.ResolvedAtUtc.UtcTicks,
            validatedAtUtcTicks);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(facts))).ToLowerInvariant();
    }

    private static bool CompleteWithdrawalRequest(FirstGskuIdentityWorkflowOperation operation) =>
        operation.WithdrawalCommandId is { } commandId && commandId != Guid.Empty
        && operation.WithdrawalRequesterSubjectId == operation.MakerSubjectId
        && operation.WithdrawalExpectedGskuVersion is > 0
        && ExactText(operation.WithdrawalReasonCode, 128)
        && OptionalExactText(operation.WithdrawalComment, 2000)
        && ExactText(operation.WithdrawalFingerprint, 256);

    private static bool CompleteWithdrawalPreflight(FirstGskuIdentityWorkflowOperation operation) =>
        CompleteWithdrawalRequest(operation)
        && operation.WithdrawalExpectedWorkflowInstanceVersion is > 0
        && operation.WithdrawalExpectedApprovalTaskVersion is > 0;

    private static bool ExactWithdrawalFacts(
        FirstGskuIdentityWorkflowOperation operation,
        Guid commandId,
        string fingerprint,
        Guid requesterSubjectId,
        int expectedGskuVersion,
        string reasonCode,
        string? comment) =>
        operation.WithdrawalCommandId == commandId
        && operation.WithdrawalRequesterSubjectId == requesterSubjectId
        && operation.WithdrawalExpectedGskuVersion == expectedGskuVersion
        && string.Equals(operation.WithdrawalFingerprint, fingerprint, StringComparison.Ordinal)
        && string.Equals(operation.WithdrawalReasonCode, reasonCode, StringComparison.Ordinal)
        && string.Equals(operation.WithdrawalComment, comment, StringComparison.Ordinal);

    private static string WithdrawalFingerprint(
        Guid tenantId,
        Guid gskuId,
        int expectedGskuVersion,
        Guid commandId,
        Guid requesterSubjectId,
        string reasonCode,
        string? comment,
        FirstGskuIdentityWorkflowBinding binding)
    {
        var canonical = string.Join('|',
            tenantId.ToString("D"),
            gskuId.ToString("D"),
            expectedGskuVersion.ToString(CultureInfo.InvariantCulture),
            commandId.ToString("D"),
            requesterSubjectId.ToString("D"),
            reasonCode,
            comment ?? string.Empty,
            binding.WorkflowInstanceId.ToString("D"),
            binding.ApprovalTaskId.ToString("D"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static bool TryRehydrateCancellationEvidence(
        FirstGskuIdentityWorkflowOperation operation,
        out PersistedCancellationEvidence evidence)
    {
        evidence = null!;
        if (!CompleteWithdrawalPreflight(operation)
            || operation.WithdrawalTransitionLogId is not { } logId || logId == Guid.Empty
            || operation.WithdrawalObservedAtUtcTicksV1 is not > 0
            || operation.WithdrawalTransitionSequence is not > 0
            || operation.WithdrawalResultWorkflowInstanceVersion is not > 0
            || operation.WithdrawalResultApprovalTaskVersion is not > 0
            || operation.WorkflowInstanceId is not { } instanceId
            || operation.ApprovalTaskId is not { } taskId
            || operation.WorkflowTemplateId is not { } templateId
            || operation.WorkflowTemplateVersionId is not { } templateVersionId
            || !Guid.TryParseExact(operation.ObjectId, "D", out var objectId))
            return false;
        evidence = new()
        {
            WorkflowInstanceId = instanceId,
            ApprovalTaskId = taskId,
            WorkflowTemplateId = templateId,
            WorkflowTemplateVersionId = templateVersionId,
            ObjectType = operation.ObjectType,
            ObjectId = objectId,
            ObjectRef = operation.WithdrawalObjectRef ?? operation.ObjectRef,
            RequesterSubjectId = operation.MakerSubjectId,
            ReasonCode = operation.WithdrawalReasonCode!,
            Comment = operation.WithdrawalComment,
            CancelledAtUtc = new DateTimeOffset(operation.WithdrawalObservedAtUtcTicksV1.Value, TimeSpan.Zero),
            TransitionSequence = operation.WithdrawalTransitionSequence.Value,
            TransitionLogId = logId,
            TaskStatus = operation.WithdrawalTaskStatus ?? string.Empty,
            InstanceStatus = operation.WithdrawalInstanceStatus ?? string.Empty,
            WorkflowInstanceVersion = operation.WithdrawalResultWorkflowInstanceVersion.Value,
            ApprovalTaskVersion = operation.WithdrawalResultApprovalTaskVersion.Value,
            IdempotencyKey = operation.WithdrawalCommandId!.Value.ToString("D")
        };
        return evidence.TaskStatus == "Cancelled" && evidence.InstanceStatus == "Cancelled";
    }

    private static bool ExactWithdrawalState(
        FirstGskuIdentityWorkflowOperation operation,
        ProductDefinitionRevision? revision,
        Gsku? gsku)
    {
        if (revision is null || gsku is null
            || !TryRehydrateCancellationEvidence(operation, out var evidence)
            || revision.Version != operation.ExpectedRevisionVersion + 2
            || gsku.Version != operation.WithdrawalExpectedGskuVersion + 1
            || revision.LifecycleStatus != ProductIdentityLifecycleStatus.Draft
            || gsku.LifecycleStatus != ProductIdentityLifecycleStatus.Draft)
            return false;
        var expectedRevision = CreateWithdrawalAudit(revision,
            AuditAggregateType.ProductDefinitionRevision,
            ProductAuditOperation.ProductDefinitionRevisionIdentityApprovalWithdrawn,
            operation.ExpectedRevisionVersion + 1, operation, evidence);
        var expectedGsku = CreateWithdrawalAudit(gsku,
            AuditAggregateType.Gsku,
            ProductAuditOperation.GskuIdentityApprovalWithdrawn,
            operation.WithdrawalExpectedGskuVersion!.Value, operation, evidence);
        // FIX2 item 7: the intent, or its receipt once delivered and compacted.
        return AuditWriteReadBack.Persisted(revision, expectedRevision,
                   intent => ExactImmutableAuditIntent(intent, expectedRevision))
            && AuditWriteReadBack.Persisted(gsku, expectedGsku,
                   intent => ExactImmutableAuditIntent(intent, expectedGsku));
    }

    private static LocalAuditIntent CreateWithdrawalAudit(
        EntityBase aggregate,
        AuditAggregateType aggregateType,
        ProductAuditOperation auditOperation,
        int expectedVersion,
        FirstGskuIdentityWorkflowOperation operation,
        PersistedCancellationEvidence evidence)
    {
        var operationKey = operation.WithdrawalCommandId!.Value.ToString("D");
        var canonical = string.Join('|',
            aggregate.TenantId.ToString("D"), aggregateType, aggregate.Id.ToString("D"),
            auditOperation, expectedVersion.ToString(CultureInfo.InvariantCulture),
            evidence.WorkflowInstanceId.ToString("D"), evidence.ApprovalTaskId.ToString("D"),
            evidence.RequesterSubjectId.ToString("D"), evidence.TransitionLogId.ToString("D"),
            evidence.TransitionSequence.ToString(CultureInfo.InvariantCulture),
            evidence.CancelledAtUtc.ToString("O"), evidence.ReasonCode, evidence.Comment ?? string.Empty);
        return new LocalAuditIntent
        {
            IntentId = DeterministicGuid(
                $"{aggregate.TenantId:D}|{aggregateType}|{aggregate.Id:D}|{auditOperation}|{operationKey}"),
            TenantId = aggregate.TenantId,
            AggregateType = aggregateType,
            AggregateId = aggregate.Id,
            PreVersion = expectedVersion,
            PostVersion = expectedVersion + 1,
            Operation = auditOperation,
            ActorId = evidence.RequesterSubjectId.ToString("D"),
            CorrelationId = operationKey,
            CausationId = operation.StartIdempotencyKey,
            CommandId = operationKey,
            Sequence = expectedVersion + 1L,
            TimestampUtc = evidence.CancelledAtUtc,
            TimestampUtcTicksV1 = evidence.CancelledAtUtc.UtcTicks,
            TemporalStorageVersion = AuditIntentTemporalStorage.CurrentVersion,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))),
            SnapshotReference = $"{aggregateType}/{aggregate.Id:N}/{expectedVersion + 1}",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = operationKey
        };
    }

    private static Guid DeterministicGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }

    private static bool SameBinding(
        FirstGskuIdentityWorkflowBinding left,
        FirstGskuIdentityWorkflowBinding right) =>
        left.WorkflowInstanceId == right.WorkflowInstanceId
        && left.WorkflowTemplateId == right.WorkflowTemplateId
        && left.WorkflowTemplateVersionId == right.WorkflowTemplateVersionId
        && left.ApprovalTaskId == right.ApprovalTaskId
        && left.AssignmentSnapshotId == right.AssignmentSnapshotId
        && left.StartTransitionLogId == right.StartTransitionLogId
        && left.GskuId == right.GskuId
        && left.ProductDefinitionRevisionId == right.ProductDefinitionRevisionId
        && left.SubmitterSubjectId == right.SubmitterSubjectId
        && left.ObjectType == right.ObjectType
        && left.ObjectRef == right.ObjectRef
        && left.StartIdempotencyKey == right.StartIdempotencyKey
        && left.StartRequestFingerprint == right.StartRequestFingerprint;

    private static bool ExactText(string? value, int maximum) =>
        value is { Length: > 0 } && value.Length <= maximum
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);

    private static bool OptionalExactText(string? value, int maximum) =>
        value is null || ExactText(value, maximum);

    private Task<bool> ScheduleRetryAsync(
        FirstGskuIdentityWorkflowClaim claim,
        FirstGskuIdentityWorkflowCheckpoint checkpoint,
        string? errorCode,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken,
        bool chainAnswered = false) =>
        operations.AdvanceAsync(claim, new(
            checkpoint,
            ProductIdentityWorkflowRecoveryDisposition.Retryable,
            now.UtcTicks,
            now.Add(retryDelay).UtcTicks,
            BoundedCode(errorCode, "FIRST_GSKU_IDENTITY_WORKFLOW_PROVIDER_UNAVAILABLE"),
            ReleaseLease: true,
            ServiceFaultCount: chainAnswered ? 0 : null), cancellationToken);

    private Task<bool> QuarantineAsync(
        FirstGskuIdentityWorkflowClaim claim,
        string? errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var code = BoundedCode(errorCode, "FIRST_GSKU_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED");
        logger.LogError(
            "First GSKU identity workflow operation {OperationId} of tenant {TenantId} was quarantined at {Checkpoint}: {ResultCode}.",
            claim.OperationId, claim.TenantId, claim.Checkpoint, code);
        return operations.AdvanceAsync(claim, new(
            FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired,
            now.UtcTicks,
            LastFailureCode: code,
            ReleaseLease: true), cancellationToken);
    }

    public const string ParentNotApproved = "GSKU_PARENT_NOT_APPROVED";
    public const string SelfApprovalIgnored = "FIRST_GSKU_IDENTITY_SELF_APPROVAL_IGNORED";
    public const string ClosedWithoutDecision = "FIRST_GSKU_IDENTITY_CLOSED_WITHOUT_DECISION";
    public const string SubmissionEnded = "FIRST_GSKU_IDENTITY_SUBMISSION_ENDED";
    public const string ReconciliationRequired = "FIRST_GSKU_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED";
    // FIX3 E2: the repeat read the engine and could not settle the submit — a person has to (BL-530).
    public const string ManualReconciliationNeeded = "FIRST_GSKU_IDENTITY_MANUAL_RECONCILIATION_NEEDED";
    public const string DecisionNotApplicableCode = "FIRST_GSKU_IDENTITY_DECISION_NOT_APPLICABLE";
    public const string WithdrawalRequesterNeverReturned = "FIRST_GSKU_IDENTITY_WITHDRAWAL_REQUESTER_NEVER_RETURNED";

    // A revalidation that failed: an outage waits (it is not the record's fault), anything else about the record
    // is quarantined.
    private async Task<bool> ApprovalFailedAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        ApprovalValidation approval,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken) => approval.Kind switch
    {
        ApprovalFailure.Transient => await ScheduleRetryAsync(claim, claim.Checkpoint, approval.ErrorCode, now,
            retryDelay, cancellationToken),
        ApprovalFailure.ServiceIdentity => await WaitForServiceAsync(operation, claim, claim.Checkpoint,
            ProductIdentityWorkflowCall.ReferenceData, approval.ErrorCode,
            new ProductIdentityWorkflowFaultDecision(ProductIdentityWorkflowFaultKind.ServiceWait), attempt, now,
            retryDelay, cancellationToken),
        _ => await QuarantineAsync(claim, approval.ErrorCode, now, cancellationToken)
    };

    /*
     * The engine approved, and the approval cannot be applied: a reference the maker can fix changed between the
     * submit and the decision (the parent product is no longer approved, a value is no longer selectable). That is
     * not a fault of the record, and the pair must not stay locked: revision and GSKU go back to draft with an audit
     * intent that says so ("the engine approved, MDM could not apply: <code>"), and the operation ends with the
     * reason — one transaction. The maker sees the reason, fixes the reference and submits again (a new operation).
     */
    // FIX2 item 7: a lifecycle write refused because the aggregate is at its audit-intent capacity waits for delivery
    // (the cap clears when the intents are delivered and compacted); anything else is the quarantine it was.
    private async Task<bool> LifecycleWriteFailedAsync(
        FirstGskuIdentityWorkflowClaim claim, string? errorCode, DateTimeOffset now, CancellationToken cancellationToken) =>
        errorCode == "AUDIT_INTENT_CAPACITY_REACHED"
            ? await ScheduleRetryAsync(claim, claim.Checkpoint, errorCode, now, faultSettings.MaxRetryDelay, cancellationToken)
            : await QuarantineAsync(claim, errorCode, now, cancellationToken);

    private async Task<bool> DecisionNotApplicableAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        string reasonCode,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        Guid? decisionActor = null)
    {
        var code = BoundedCode(reasonCode, DecisionNotApplicableCode);
        var revision = await revisions.GetByIdAsync(operation.ProductDefinitionRevisionId, cancellationToken);
        var gsku = await gskus.GetByIdAsync(operation.GskuId, cancellationToken);
        if (!ExactPendingRevision(revision, operation) || !ExactPendingGsku(gsku, operation))
        {
            return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_PAIR_INCONSISTENT", now, cancellationToken);
        }
        var revisionAudit = CreateNotApplicableAudit(revision!, AuditAggregateType.ProductDefinitionRevision,
            ProductAuditOperation.ProductDefinitionRevisionIdentityDecisionNotApplicable,
            operation.ExpectedRevisionVersion + 1, operation, code, now, decisionActor);
        var gskuAudit = CreateNotApplicableAudit(gsku!, AuditAggregateType.Gsku,
            ProductAuditOperation.GskuIdentityDecisionNotApplicable,
            operation.ExpectedGskuVersion + 1, operation, code, now, decisionActor);
        var result = await operations.ApplyDecisionNotApplicableAsync(
            claim, operation, code, revisionAudit, gskuAudit, now.UtcTicks, cancellationToken);
        if (result.Succeeded)
        {
            logger.LogWarning(
                "First GSKU identity workflow operation {OperationId} of tenant {TenantId}: the engine approved the pair and it could not be applied ({ResultCode}); revision and GSKU are drafts again for the maker to fix and resubmit.",
                operation.OperationId, operation.TenantId, code);
            return true;
        }
        // A store that is away and an aggregate at its audit-intent capacity (FIX2 item 7) are waited out.
        return result.ErrorCode is "FIRST_GSKU_IDENTITY_NOT_APPLICABLE_PERSISTENCE_UNAVAILABLE"
                or "AUDIT_INTENT_CAPACITY_REACHED"
            ? await ScheduleRetryAsync(claim, claim.Checkpoint, result.ErrorCode, now,
                faultSettings.MaxRetryDelay, cancellationToken)
            : await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
    }

    private static LocalAuditIntent CreateNotApplicableAudit(
        EntityBase aggregate,
        AuditAggregateType aggregateType,
        ProductAuditOperation auditOperation,
        int expectedVersion,
        FirstGskuIdentityWorkflowOperation operation,
        string reasonCode,
        DateTimeOffset now,
        Guid? decisionActor = null)
    {
        var key = FirstGskuIdentityDecisionNotApplicable.Key(operation.OperationId);
        var actor = decisionActor ?? operation.DecisionActorSubjectId ?? Guid.Empty;
        var canonical = string.Join('|',
            aggregate.TenantId.ToString("D"), aggregateType, aggregate.Id.ToString("D"),
            auditOperation, expectedVersion.ToString(CultureInfo.InvariantCulture),
            operation.WorkflowInstanceId?.ToString("D") ?? string.Empty,
            operation.ApprovalTaskId?.ToString("D") ?? string.Empty,
            actor.ToString("D"),
            // What happened, in the evidence: the engine approved and MDM could not apply it — or the engine closed
            // the approval with the maker's own decision, which MDM does not accept.
            reasonCode == SelfApprovalIgnored ? "engine-closed-by-maker-not-applied"
            : reasonCode == ClosedWithoutDecision ? "engine-closed-without-decision"
            : "engine-approved-mdm-could-not-apply",
            reasonCode);
        return new LocalAuditIntent
        {
            IntentId = DeterministicGuid($"{aggregate.TenantId:D}|{aggregateType}|{aggregate.Id:D}|{auditOperation}|{key}"),
            TenantId = aggregate.TenantId,
            AggregateType = aggregateType,
            AggregateId = aggregate.Id,
            PreVersion = expectedVersion,
            PostVersion = expectedVersion + 1,
            Operation = auditOperation,
            ActorId = actor.ToString("D"),
            CorrelationId = key,
            CausationId = operation.StartIdempotencyKey,
            CommandId = key,
            Sequence = expectedVersion + 1L,
            TimestampUtc = now,
            TimestampUtcTicksV1 = now.UtcTicks,
            TemporalStorageVersion = AuditIntentTemporalStorage.CurrentVersion,
            EvidenceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))),
            SnapshotReference = $"{aggregateType}/{aggregate.Id:N}/{expectedVersion + 1}",
            DeliveryState = AuditIntentDeliveryState.Pending,
            IdempotencyKey = key
        };
    }

    private Task<bool> WaitForServiceAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        FirstGskuIdentityWorkflowCheckpoint checkpoint,
        ProductIdentityWorkflowCall call,
        string? errorCode,
        ProductIdentityWorkflowFaultDecision fault,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        var streak = FaultStreak(operation, now, countsAsWorkerAttempt: !attempt.Interactive);
        var code = BoundedCode(errorCode, "FIRST_GSKU_IDENTITY_WORKFLOW_PROVIDER_UNAVAILABLE");
        attempt.Scope = fault.Scope;
        if (attempt.Interactive)
        {
            attempt.Answer = (ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, 503);
            LogServiceFault(operation, call, code, streak.Count, streak.Since, now, fault.Loud,
                "the maker was told to resubmit; the worker's schedule is unchanged");
            return operations.AdvanceAsync(
                claim,
                new(
                    checkpoint,
                    ProductIdentityWorkflowRecoveryDisposition.Retryable,
                    now.UtcTicks,
                    operation.NextAttemptAtUtcTicksV1,
                    code,
                    ReleaseLease: true,
                    ServiceFaultCount: streak.Count,
                    ServiceFaultSinceUtcTicksV1: streak.Since),
                cancellationToken);
        }

        LogServiceFault(operation, call, code, streak.Count, streak.Since, now, fault.Loud, "the worker retries");
        return operations.AdvanceAsync(
            claim,
            new(
                checkpoint,
                ProductIdentityWorkflowRecoveryDisposition.Retryable,
                now.UtcTicks,
                now.Add(ProductIdentityWorkflowFaultPolicy.Backoff(
                        streak.Count, retryDelay, faultSettings.MaxRetryDelay))
                    .UtcTicks,
                code,
                ReleaseLease: true,
                ServiceFaultCount: streak.Count,
                ServiceFaultSinceUtcTicksV1: streak.Since),
            cancellationToken);
    }

    // The operation is handed to the maker: nothing was started and only a resubmit can do it. serviceFault is null
    // when the engine answered and the reason is the maker's or the set-up's — an answer, which ends a fault streak.
    // Only a maker's own attempt gets here, so a service fault does not count as a worker attempt.
    private Task<bool> ToMakerAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        ProductIdentityWorkflowCall call,
        string? errorCode,
        string makerCode,
        ProductIdentityWorkflowFaultDecision? serviceFault,
        Attempt attempt,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        bool answeredSince = false)
    {
        var code = BoundedCode(errorCode, makerCode);
        (int Count, long Since)? streak = null;
        if (serviceFault is not null)
        {
            streak = FaultStreak(operation, now, countsAsWorkerAttempt: false);
            attempt.Scope = serviceFault.Scope;
            LogServiceFault(operation, call, code, streak.Value.Count, streak.Value.Since, now, serviceFault.Loud,
                "nothing was started; the maker resubmits");
        }
        else
        {
            logger.LogWarning(
                "First GSKU identity workflow operation {OperationId} of tenant {TenantId} was refused to the maker on {Call}: {ResultCode} -> {MakerCode}.",
                operation.OperationId, operation.TenantId, call, code, makerCode);
        }

        return operations.AdvanceAsync(
            claim,
            new(
                FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay,
                now.UtcTicks,
                LastFailureCode: makerCode,
                ReleaseLease: true,
                ServiceFaultCount: answeredSince ? 0 : streak?.Count ?? 0,
                ServiceFaultSinceUtcTicksV1: answeredSince ? null : streak?.Since),
            cancellationToken);
    }

    private async Task<(FirstGskuIdentityWorkflowClaim? Claim, bool StoreSilent)> ClaimAsync(
        FirstGskuIdentityWorkflowClaimRequest request,
        TimeSpan leaseDuration) =>
        await ProductIdentityWorkflowClaims.ClaimAsync(
            token => operations.TryClaimAsync(request, token),
            token => operations.ReleaseIfOwnedAsync(request.OperationId, request.LeaseOwner,
                request.LeaseUntilUtcTicks, token),
            leaseDuration,
            exception => logger.LogWarning(
                "First GSKU identity workflow operation {OperationId}: the store did not answer a claim, and a lease it may have written could not be given back ({ExceptionType}); it expires by itself.",
                request.OperationId, exception.GetType().FullName));

    private (int Count, long Since) FaultStreak(
        FirstGskuIdentityWorkflowOperation operation,
        DateTimeOffset now,
        bool countsAsWorkerAttempt) =>
        ProductIdentityWorkflowFaultRules.Streak(operation, now, countsAsWorkerAttempt, faultSettings);

    private bool StreakIsOver(FirstGskuIdentityWorkflowOperation operation, DateTimeOffset now) =>
        ProductIdentityWorkflowFaultRules.StreakIsOver(operation, now, faultSettings);

    private static long SettledNextAttempt(
        FirstGskuIdentityWorkflowOperation operation,
        DateTimeOffset now,
        TimeSpan retryDelay) =>
        ProductIdentityWorkflowFaultRules.SettledNextAttempt(operation, now, retryDelay);

    // An error when the worker has tried often enough, when the streak of consecutive faults has lasted longer than
    // the longest back-off (so a fault met only by makers still becomes an error — by how long it has gone on, not
    // by their clicks), or on every attempt for a fault that waiting will not mend.
    private void LogServiceFault(
        FirstGskuIdentityWorkflowOperation operation,
        ProductIdentityWorkflowCall call,
        string code,
        int workerAttempts,
        long sinceUtcTicks,
        DateTimeOffset now,
        bool loud,
        string whatHappensNext) =>
        logger.Log(
            ProductIdentityWorkflowFaultRules.Level(loud, workerAttempts, sinceUtcTicks, now, faultSettings),
            "First GSKU identity workflow operation {OperationId} of tenant {TenantId} met a service fault at {Call}: {ResultCode}; worker attempt {Attempt}, failing since {SinceUtc:O}; {WhatHappensNext}.",
            operation.OperationId, operation.TenantId, call, code, workerAttempts,
            new DateTimeOffset(sinceUtcTicks, TimeSpan.Zero), whatHappensNext);

    private static FirstGskuIdentityWorkflowProcessingResult MakerAnswer(
        FirstGskuIdentityWorkflowOperation operation,
        Attempt attempt)
    {
        var code = ProductIdentityWorkflowMakerCodes.IsMakerCode(operation.LastFailureCode)
            ? operation.LastFailureCode!
            : ProductIdentityWorkflowMakerCodes.MakerReplayRequired;
        return Fail(operation, code, ProductIdentityWorkflowMakerCodes.StatusFor(code))
            with { FaultScope = attempt.Scope };
    }

    // A retry is scheduled for a reason that is not a service fault met by a maker (that one is answered through
    // the attempt): the caller is told the reason the operation carries.
    private static FirstGskuIdentityWorkflowProcessingResult ScheduledAnswer(
        FirstGskuIdentityWorkflowOperation operation,
        Attempt attempt) =>
        Fail(operation, operation.LastFailureCode ?? "FIRST_GSKU_IDENTITY_WORKFLOW_RETRY_SCHEDULED", 503)
            with { FaultScope = attempt.Scope };

    // A lease this call took and is not going to finish under — the step was refused (a transition that is not
    // allowed, a checkpoint this path does not handle), the caller went away, or the store failed mid-step — is
    // given back, leaving everything else about the operation as it was, the worker's schedule included. Best
    // effort: if giving it back fails too, the lease runs out by itself, and that is said.
    private async Task ReleaseClaimAsync(
        FirstGskuIdentityWorkflowClaim claim,
        FirstGskuIdentityWorkflowOperation operation,
        DateTimeOffset now,
        long? nextAttemptAtUtcTicks = null)
    {
        try
        {
            await operations.AdvanceAsync(
                claim,
                new(
                    claim.Checkpoint,
                    operation.RecoveryDisposition,
                    now.UtcTicks,
                    nextAttemptAtUtcTicks ?? operation.NextAttemptAtUtcTicksV1,
                    operation.LastFailureCode,
                    ReleaseLease: true,
                    ServiceFaultCount: StreakIsOver(operation, now) ? 0 : null),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                "First GSKU identity workflow operation {OperationId} of tenant {TenantId}: the lease could not be given back ({ExceptionType}); it expires by itself.",
                claim.OperationId, claim.TenantId, exception.GetType().FullName);
        }
    }


    private Task<bool> AdvanceAsync(
        FirstGskuIdentityWorkflowClaim claim,
        FirstGskuIdentityWorkflowCheckpoint checkpoint,
        DateTimeOffset now,
        string? errorCode = null,
        bool releaseLease = false,
        CancellationToken cancellationToken = default) =>
        operations.AdvanceAsync(claim, new(
            checkpoint,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now.UtcTicks,
            LastFailureCode: errorCode,
            ReleaseLease: releaseLease), cancellationToken);

    private static bool TryBinding(
        FirstGskuIdentityWorkflowOperation operation,
        out FirstGskuIdentityWorkflowBinding binding)
    {
        binding = null!;
        if (!CompleteStartProof(operation)) return false;
        binding = new()
        {
            WorkflowInstanceId = operation.WorkflowInstanceId!.Value,
            WorkflowTemplateId = operation.WorkflowTemplateId!.Value,
            WorkflowTemplateVersionId = operation.WorkflowTemplateVersionId!.Value,
            ApprovalTaskId = operation.ApprovalTaskId!.Value,
            AssignmentSnapshotId = operation.AssignmentSnapshotId!.Value,
            StartTransitionLogId = operation.StartTransitionLogId!.Value,
            ObjectType = operation.ObjectType,
            GskuId = operation.GskuId,
            ProductDefinitionRevisionId = operation.ProductDefinitionRevisionId,
            ObjectRef = operation.ObjectRef,
            SubmitterSubjectId = operation.MakerSubjectId,
            StartIdempotencyKey = operation.StartIdempotencyKey,
            StartRequestFingerprint = operation.OperationFingerprint,
            SubmittedAtUtc = new DateTimeOffset(operation.WorkflowStartedAtUtcTicksV1!.Value, TimeSpan.Zero),
            DueAtUtc = operation.DueAtUtcTicksV1.HasValue
                ? new DateTimeOffset(operation.DueAtUtcTicksV1.Value, TimeSpan.Zero) : null
        };
        return true;
    }

    private static bool TryEvidence(
        FirstGskuIdentityWorkflowOperation operation,
        out ProductIdentityWorkflowDecisionEvidence evidence)
    {
        evidence = null!;
        if (!operation.DecisionKind.HasValue || !operation.WorkflowInstanceId.HasValue
            || !operation.ApprovalTaskId.HasValue || !operation.DecisionWorkflowTemplateId.HasValue
            || !operation.DecisionWorkflowTemplateVersionId.HasValue
            || !operation.DecisionActorSubjectId.HasValue || !operation.DecisionTransitionSequence.HasValue
            || !operation.DecisionAtUtcTicksV1.HasValue || !Guid.TryParse(operation.DecisionObjectId, out var objectId))
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

    private static bool CompleteStartProof(FirstGskuIdentityWorkflowOperation operation) =>
        operation.WorkflowInstanceId is { } workflowId && workflowId != Guid.Empty
        && operation.WorkflowTemplateId is { } templateId && templateId != Guid.Empty
        && operation.WorkflowTemplateVersionId is { } templateVersionId && templateVersionId != Guid.Empty
        && operation.ApprovalTaskId is { } taskId && taskId != Guid.Empty
        && operation.AssignmentSnapshotId is { } snapshotId && snapshotId != Guid.Empty
        && operation.StartTransitionLogId is { } transitionId && transitionId != Guid.Empty
        && operation.WorkflowStartedAtUtcTicksV1 is > 0;

    private static bool ExactInitialRevision(ProductDefinitionRevision? revision, FirstGskuIdentityWorkflowOperation op) =>
        revision is not null && !revision.IsDeleted && revision.Id == op.ProductDefinitionRevisionId
        && revision.GlobalProductId == op.GlobalProductId && revision.Version == op.ExpectedRevisionVersion
        && revision.LifecycleStatus == ProductIdentityLifecycleStatus.Draft
        && revision.CreationCommandId == op.CreationCommandId;

    private static bool ExactInitialGsku(Gsku? gsku, FirstGskuIdentityWorkflowOperation op) =>
        gsku is not null && !gsku.IsDeleted && gsku.Id == op.GskuId
        && gsku.ProductDefinitionRevisionId == op.ProductDefinitionRevisionId
        && gsku.Version == op.ExpectedGskuVersion && gsku.LifecycleStatus == ProductIdentityLifecycleStatus.Draft
        && gsku.CreationCommandId == op.CreationCommandId && gsku.PackApplicabilityCode == op.PackApplicabilityCode
        && gsku.PackQuantity == op.PackQuantity && gsku.PackUomCode == op.PackUomCode;

    private static bool ExactPendingRevision(ProductDefinitionRevision? value, FirstGskuIdentityWorkflowOperation op) =>
        value is not null && value.Version == op.ExpectedRevisionVersion + 1
        && value.LifecycleStatus == ProductIdentityLifecycleStatus.PendingIdentityApproval
        && ExactBinding(value.IdentityWorkflowBinding, op, terminal: false);

    private static bool ExactPendingGsku(Gsku? value, FirstGskuIdentityWorkflowOperation op) =>
        value is not null && value.Version == op.ExpectedGskuVersion + 1
        && value.LifecycleStatus == ProductIdentityLifecycleStatus.PendingIdentityApproval
        && ExactBinding(value.IdentityWorkflowBinding, op, terminal: false);

    private static bool ExactApprovedRevision(ProductDefinitionRevision? value, FirstGskuIdentityWorkflowOperation op) =>
        value is not null && value.Version == op.ExpectedRevisionVersion + 2
        && value.LifecycleStatus == ProductIdentityLifecycleStatus.IdentityApproved
        && ExactBinding(value.IdentityWorkflowBinding, op, terminal: true);

    private static bool ExactApprovedGsku(Gsku? value, FirstGskuIdentityWorkflowOperation op) =>
        value is not null && value.Version == op.ExpectedGskuVersion + 2
        && value.LifecycleStatus == ProductIdentityLifecycleStatus.IdentityApproved
        && ExactBinding(value.IdentityWorkflowBinding, op, terminal: true);

    private static bool ExactRejectedRevision(ProductDefinitionRevision? value, FirstGskuIdentityWorkflowOperation op) =>
        value is not null && value.Version == op.ExpectedRevisionVersion + 2
        && value.LifecycleStatus == ProductIdentityLifecycleStatus.Draft
        && ExactBinding(value.IdentityWorkflowBinding, op, terminal: true);

    private static bool ExactRejectedGsku(Gsku? value, FirstGskuIdentityWorkflowOperation op) =>
        value is not null && value.Version == op.ExpectedGskuVersion + 2
        && value.LifecycleStatus == ProductIdentityLifecycleStatus.Draft
        && ExactBinding(value.IdentityWorkflowBinding, op, terminal: true);

    private static bool ExactBinding(
        FirstGskuIdentityWorkflowBinding? binding,
        FirstGskuIdentityWorkflowOperation op,
        bool terminal) =>
        binding is not null && binding.WorkflowInstanceId == op.WorkflowInstanceId
        && binding.WorkflowTemplateId == op.WorkflowTemplateId
        && binding.WorkflowTemplateVersionId == op.WorkflowTemplateVersionId
        && binding.ApprovalTaskId == op.ApprovalTaskId
        && binding.AssignmentSnapshotId == op.AssignmentSnapshotId
        && binding.StartTransitionLogId == op.StartTransitionLogId
        && binding.GskuId == op.GskuId && binding.ProductDefinitionRevisionId == op.ProductDefinitionRevisionId
        && binding.ObjectType == op.ObjectType && binding.ObjectRef == op.ObjectRef
        && binding.GskuId.ToString("D") == op.ObjectId
        && binding.SubmitterSubjectId == op.MakerSubjectId
        && binding.StartIdempotencyKey == op.StartIdempotencyKey
        && binding.StartRequestFingerprint == op.OperationFingerprint
        && binding.SubmittedAtUtc.Offset == TimeSpan.Zero
        && binding.SubmittedAtUtc.UtcTicks == op.WorkflowStartedAtUtcTicksV1
        && binding.DueAtUtc?.UtcTicks == op.DueAtUtcTicksV1
        && (terminal
            ? ExactTerminalBinding(binding.TerminalDecision, op)
            : binding.TerminalDecision is null);

    private static bool ExactDecisionAudit(
        ProductDefinitionRevision revision,
        Gsku gsku,
        FirstGskuIdentityWorkflowOperation operation)
    {
        if (!TryBinding(operation, out var binding) || !TryEvidence(operation, out var evidence)) return false;
        binding.TerminalDecision = evidence;
        var expectedRevision = FirstGskuIdentityLifecycleAuditIntentFactory.CreateRevisionDecision(
            revision, operation.ExpectedRevisionVersion + 1, binding, evidence);
        var expectedGsku = FirstGskuIdentityLifecycleAuditIntentFactory.CreateGskuDecision(
            gsku, operation.ExpectedGskuVersion + 1, binding, evidence, operation.ApprovalAppliedNote);
        // FIX2 item 7: the intent, or its receipt once delivered and compacted.
        return AuditWriteReadBack.Persisted(revision, expectedRevision,
                   intent => ExactImmutableAuditIntent(intent, expectedRevision))
            && AuditWriteReadBack.Persisted(gsku, expectedGsku,
                   intent => ExactImmutableAuditIntent(intent, expectedGsku));
    }

    private static bool ExactTerminalBinding(
        ProductIdentityWorkflowDecisionEvidence? evidence,
        FirstGskuIdentityWorkflowOperation operation) =>
        evidence is not null
        && evidence.Decision == operation.DecisionKind
        && evidence.WorkflowInstanceId == operation.WorkflowInstanceId
        && evidence.ApprovalTaskId == operation.ApprovalTaskId
        && evidence.WorkflowTemplateId == operation.DecisionWorkflowTemplateId
        && evidence.WorkflowTemplateVersionId == operation.DecisionWorkflowTemplateVersionId
        && evidence.ObjectType == operation.DecisionObjectType
        && evidence.ObjectId.ToString("D") == operation.DecisionObjectId
        && evidence.ObjectRef == operation.DecisionObjectRef
        && evidence.DecisionActorSubjectId == operation.DecisionActorSubjectId
        && evidence.ReasonCode == operation.DecisionReasonCode
        && evidence.DecisionAtUtc.Offset == TimeSpan.Zero
        && evidence.DecisionAtUtc.UtcTicks == operation.DecisionAtUtcTicksV1
        && evidence.TransitionSequence == operation.DecisionTransitionSequence
        && evidence.TaskStatus == operation.DecisionTaskStatus
        && evidence.InstanceStatus == operation.DecisionInstanceStatus;

    private static bool ExactImmutableAuditIntent(LocalAuditIntent actual, LocalAuditIntent expected) =>
        actual.IntentId == expected.IntentId && actual.TenantId == expected.TenantId
        && actual.AggregateType == expected.AggregateType && actual.AggregateId == expected.AggregateId
        && actual.PreVersion == expected.PreVersion && actual.PostVersion == expected.PostVersion
        && actual.Operation == expected.Operation && actual.ActorId == expected.ActorId
        && actual.CorrelationId == expected.CorrelationId && actual.CausationId == expected.CausationId
        && actual.CommandId == expected.CommandId && actual.Sequence == expected.Sequence
        && actual.TimestampUtc == expected.TimestampUtc
        && actual.TimestampUtcTicksV1 == expected.TimestampUtcTicksV1
        && actual.TemporalStorageVersion == expected.TemporalStorageVersion
        && actual.EvidenceHash == expected.EvidenceHash
        && actual.SnapshotReference == expected.SnapshotReference
        && actual.IdempotencyKey == expected.IdempotencyKey;

    private static bool ExactApprovalReferenceProof(FirstGskuIdentityWorkflowOperation operation) =>
        operation.ApprovalPackApplicabilitySelection is { } applicability
        && operation.ApprovalPackUomSelection is { } uom
        && operation.ReferencesValidatedAtUtcTicksV1 is > 0
        && operation.ApprovalReferenceProofFingerprint is { Length: 64 } fingerprint
        && string.Equals(
            fingerprint,
            ApprovalReferenceFingerprint(
                operation, applicability, uom, operation.ReferencesValidatedAtUtcTicksV1.Value),
            StringComparison.Ordinal);

    private static bool ExactReferenceSelection(
        ReferenceCatalogSelection expected,
        ReferenceCatalogSelection current) =>
        expected.SetCode == current.SetCode && expected.ValueCode == current.ValueCode
        && expected.CatalogVersionId == current.CatalogVersionId
        && expected.CatalogVersionNumber == current.CatalogVersionNumber
        && expected.ResolutionMode == current.ResolutionMode
        && current.ResolvedAtUtc.Offset == TimeSpan.Zero
        && current.ResolvedAtUtc != default;

    private static bool ExactInteractiveFacts(
        FirstGskuIdentityWorkflowOperation operation,
        Guid tenantId,
        Guid gskuId,
        int expectedGskuVersion,
        Guid makerSubjectId) =>
        !operation.IsDeleted && operation.TenantId == tenantId && operation.GskuId == gskuId
        && operation.ExpectedGskuVersion == expectedGskuVersion && operation.MakerSubjectId == makerSubjectId
        && operation.ObjectType == FirstGskuIdentityWorkflowStartRequestFactory.ObjectType
        && operation.ObjectId == gskuId.ToString("D");

    // FU04 S2: the shared binding part (IdentityApprovalBinding) — the same rules, one place.
    private static bool TryMapDecision(string action, out ProductIdentityDecisionKind decision) =>
        IdentityApprovalBinding.TryMapDecision(action, out decision);

    private static bool ExactTerminalStatuses(
        ProductIdentityDecisionKind decision, string taskStatus, string instanceStatus) =>
        IdentityApprovalBinding.ExactTerminalStatuses(decision, taskStatus, instanceStatus);

    private static string BoundedCode(string? code, string fallback) => IdentityApprovalBinding.BoundedCode(code, fallback);

    private static FirstGskuIdentityWorkflowProcessingResult Success(
        FirstGskuIdentityWorkflowOperation operation, bool replay) => new(true, operation, null, 200, replay);

    private static FirstGskuIdentityWorkflowProcessingResult Fail(string errorCode, int statusCode) =>
        new(false, null, errorCode, statusCode, false);

    private static FirstGskuIdentityWorkflowProcessingResult Fail(
        FirstGskuIdentityWorkflowOperation operation, string errorCode, int statusCode) =>
        new(false, operation, errorCode, statusCode, false);

    private enum ApprovalFailure
    {
        None,
        // 503 / 504 or an exception: ask again.
        Transient,
        // The reference service refused this service's identity: the chain, not the record.
        ServiceIdentity,
        // A reference the maker can fix changed between submit and decision (parent no longer approved, a value no
        // longer selectable).
        NotApplicable,
        // Anything else the reference service answered about the record.
        Fault
    }

    private sealed record ApprovalValidation(
        bool Succeeded,
        ApprovalFailure Kind,
        string? ErrorCode,
        ReferenceCatalogSelection? Applicability = null,
        ReferenceCatalogSelection? Uom = null);
}
