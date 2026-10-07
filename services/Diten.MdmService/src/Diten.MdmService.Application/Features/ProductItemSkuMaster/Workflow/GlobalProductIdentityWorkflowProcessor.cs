using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using PersistedCancellationEvidence = Diten.MdmService.Domain.ValueObjects.ProductIdentityWorkflowCancellationEvidence;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record GlobalProductIdentityWorkflowProcessingResult(
    bool Succeeded,
    GlobalProductIdentityWorkflowOperation? Operation,
    string? ErrorCode,
    int StatusCode,
    bool IsReplay,
    // Set when the attempt met a fault of the service chain: what a recovery cycle may skip without calling again.
    ProductIdentityWorkflowFaultScope FaultScope = ProductIdentityWorkflowFaultScope.None,
    // On a conflicting withdrawal: the command the requester's earlier withdrawal stands under, so that it can be
    // repeated (only ever given to that requester, in that tenant).
    Guid? PendingWithdrawalCommandId = null);

public sealed record ProductIdentityWorkflowExecutionConfiguration(
    TimeSpan LeaseDuration,
    TimeSpan RetryDelay);

public sealed class GlobalProductIdentityWorkflowProcessor(
    IGlobalProductIdentityWorkflowOperationRepository operations,
    IGlobalProductRepository products,
    IProductIdentityWorkflowClient workflowClient,
    ProductIdentityWorkflowStartRequestFactory requestFactory,
    TimeProvider timeProvider,
    IProductIdentityWorkflowTransportReadiness transportReadiness,
    ProductIdentityWorkflowServiceFaultSettings faultSettings,
    ILogger<GlobalProductIdentityWorkflowProcessor> logger)
{
    // One attempt = one call into this processor. Interactive means the maker is present (a delegated token).
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
        // BL-552: the submitter's repeat read the engine for a stuck submit (once per request), what it met, and
        // whether the decision is to be read in this same request.
        public bool Reconciled { get; set; }
        public bool EngineSilent { get; set; }
        public bool ObserveNow { get; set; }
        public bool Restarted { get; set; }
    }


    private static readonly GlobalProductIdentityWorkflowCheckpoint[] RecoverableCheckpoints =
    [
        GlobalProductIdentityWorkflowCheckpoint.Prepared,
        GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown,
        GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted,
        GlobalProductIdentityWorkflowCheckpoint.LocalPendingApplied,
        GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision,
        GlobalProductIdentityWorkflowCheckpoint.DecisionObserved,
        GlobalProductIdentityWorkflowCheckpoint.DecisionApplied
    ];

    public async Task<GlobalProductIdentityWorkflowProcessingResult> StartInteractiveAsync(
        Guid tenantId,
        Guid globalProductId,
        int expectedVersion,
        Guid operationId,
        Guid makerSubjectId,
        string delegatedUserToken,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || globalProductId == Guid.Empty || operationId == Guid.Empty
            || makerSubjectId == Guid.Empty || expectedVersion < 0
            || string.IsNullOrWhiteSpace(delegatedUserToken))
        {
            return Fail("PRODUCT_IDENTITY_WORKFLOW_START_INVALID", 400);
        }

        // A submit against a transport THIS service has not been configured for (no or invalid Platform address, no
        // or invalid service-identity provider settings) is refused before the first write — no operation document,
        // no lease — as retryable. A repeat of an operation that needs no transport any more (decided, awaiting the
        // decision, quarantined, abandoned) is answered from what is stored, whatever the settings say now.
        var existing = await operations.GetByOperationIdAsync(operationId, cancellationToken);
        GlobalProductIdentityWorkflowOperation operation;
        var replay = existing is not null;
        if (existing is null)
        {
            if (ConfigurationFault(tenantId, operationId) is { } configurationFault)
            {
                return configurationFault;
            }

            var product = await products.GetByIdAsync(globalProductId, cancellationToken);
            if (product is null)
            {
                return Fail("PRODUCT_IDENTITY_NOT_FOUND", 404);
            }

            ProductIdentityWorkflowStartPlan plan;
            try
            {
                plan = requestFactory.Create(
                    tenantId, product, operationId, expectedVersion, makerSubjectId);
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message, exception.Message == "PRODUCT_IDENTITY_WORKFLOW_CONFIGURATION_INVALID"
                    ? 503 : 409);
            }

            var reserved = await operations.ReserveAsync(plan.Operation, cancellationToken);
            if (!reserved.Succeeded || reserved.Operation is null)
            {
                return Fail(reserved.ErrorCode ?? "PRODUCT_IDENTITY_WORKFLOW_OPERATION_CONFLICT",
                    reserved.ErrorCode == ProductIdentityStore.Unavailable ? 503 : 409);
            }

            operation = reserved.Operation;
            replay = reserved.IsReplay;
        }
        else
        {
            operation = existing;
        }

        if (!ExactInteractiveFacts(operation, tenantId, globalProductId, expectedVersion, makerSubjectId))
        {
            return Fail("PRODUCT_IDENTITY_WORKFLOW_OPERATION_CONFLICT", 409);
        }
        // AwaitingMakerReplay is where BOTH a start and (in rows written before the withdrawal stopped parking there)
        // a withdrawal wait for the maker. A start repeated on an operation that carries a withdrawal would run the
        // start again over a live — or already closed — approval: it is refused before any call is made.
        if (existing is not null && existing.WithdrawalCommandId.HasValue
            && existing.Checkpoint is GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay
                or GlobalProductIdentityWorkflowCheckpoint.WithdrawalRequested
                or GlobalProductIdentityWorkflowCheckpoint.WithdrawalPreflightObserved
                or GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown
                or GlobalProductIdentityWorkflowCheckpoint.WithdrawalObserved
                or GlobalProductIdentityWorkflowCheckpoint.WithdrawalApplied)
        {
            return Fail(existing, "PRODUCT_IDENTITY_WORKFLOW_STATE_CONFLICT", 409) with { IsReplay = true };
        }
        if (existing is not null
            && existing.Checkpoint is GlobalProductIdentityWorkflowCheckpoint.Prepared
                or GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown
                or GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay
            && ConfigurationFault(tenantId, operationId) is { } storedConfigurationFault)
        {
            return storedConfigurationFault with { Operation = existing };
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

    public Task<GlobalProductIdentityWorkflowProcessingResult> RecoverAsync(
        GlobalProductIdentityWorkflowOperation operation,
        string leaseOwner,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        // The worker meets the same settings as a submit does. An operation that is already at rest is still
        // answered; anything else is left exactly where it is — no lease, no write — until the settings are right.
        if (operation.Checkpoint is not (GlobalProductIdentityWorkflowCheckpoint.Completed
                or GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired
                or GlobalProductIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart
                or GlobalProductIdentityWorkflowCheckpoint.Superseded)
            && ConfigurationFault(operation.TenantId, operation.OperationId) is { } configurationFault)
        {
            return Task.FromResult(configurationFault with { Operation = operation });
        }

        return RecoverConfiguredAsync(operation, leaseOwner, leaseDuration, retryDelay, cancellationToken);
    }

    private Task<GlobalProductIdentityWorkflowProcessingResult> RecoverConfiguredAsync(
        GlobalProductIdentityWorkflowOperation operation,
        string leaseOwner,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        CancellationToken cancellationToken) =>
        operation.WithdrawalCommandId.HasValue && operation.Checkpoint is
            GlobalProductIdentityWorkflowCheckpoint.WithdrawalRequested
            or GlobalProductIdentityWorkflowCheckpoint.WithdrawalPreflightObserved
            or GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown
            or GlobalProductIdentityWorkflowCheckpoint.WithdrawalObserved
            or GlobalProductIdentityWorkflowCheckpoint.WithdrawalApplied
            or GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay
            ? ProcessWithdrawalAsync(operation, null, leaseOwner, leaseDuration, retryDelay, cancellationToken)
            : ProcessAsync(operation, null, leaseOwner, leaseDuration, retryDelay,
                stopWhenAwaitingDecision: false, cancellationToken);

    public async Task<GlobalProductIdentityWorkflowProcessingResult> WithdrawInteractiveAsync(
        Guid tenantId,
        Guid globalProductId,
        int expectedVersion,
        Guid commandId,
        Guid requesterSubjectId,
        string reasonCode,
        string? comment,
        string delegatedUserToken,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || globalProductId == Guid.Empty || commandId == Guid.Empty
            || requesterSubjectId == Guid.Empty || expectedVersion < 1
            || !ExactText(reasonCode, 128) || !OptionalExactText(comment, 2000)
            || string.IsNullOrWhiteSpace(delegatedUserToken))
        {
            return Fail("PRODUCT_IDENTITY_WITHDRAWAL_INVALID", 400);
        }

        var product = await products.GetByIdAsync(globalProductId, cancellationToken);
        if (product is null) return Fail("PRODUCT_IDENTITY_NOT_FOUND", 404);
        if (product.WorkflowBinding is not { } binding)
        {
            return Fail("PRODUCT_IDENTITY_STATE_CONFLICT", 409);
        }
        if (binding.SubmitterSubjectId != requesterSubjectId)
        {
            return Fail("PRODUCT_IDENTITY_WITHDRAWAL_NOT_REQUESTER", 403);
        }

        var operation = await operations.GetByStartIdempotencyKeyAsync(
            binding.StartIdempotencyKey, cancellationToken);
        if (operation is null || operation.TenantId != tenantId
            || operation.GlobalProductId != globalProductId || operation.MakerSubjectId != requesterSubjectId
            || operation.WorkflowInstanceId != binding.WorkflowInstanceId
            || operation.ApprovalTaskId != binding.ApprovalTaskId
            || operation.WorkflowTemplateId != binding.WorkflowTemplateId
            || operation.WorkflowTemplateVersionId != binding.WorkflowTemplateVersionId)
        {
            return Fail("PRODUCT_IDENTITY_WORKFLOW_BINDING_CONFLICT", 409);
        }

        var fingerprint = WithdrawalFingerprint(tenantId, globalProductId, expectedVersion, commandId,
            requesterSubjectId, reasonCode, comment, binding);
        if (operation.WithdrawalCommandId.HasValue
            && !ExactWithdrawalFacts(operation, commandId, fingerprint, requesterSubjectId,
                expectedVersion, reasonCode, comment))
        {
            // The requester is the submitter of this approval in this tenant (checked above): they may learn the
            // command their earlier withdrawal stands under, and repeat THAT one.
            return Fail("PRODUCT_IDENTITY_WITHDRAWAL_OPERATION_CONFLICT", 409)
                with { PendingWithdrawalCommandId = operation.WithdrawalCommandId };
        }
        if (operation.WithdrawalCommandId.HasValue
            && operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.Completed)
        {
            if (product.Version == expectedVersion + 1
                && product.LifecycleStatus == ProductIdentityLifecycleStatus.Draft
                && product.WorkflowBinding.CancellationEvidence is { } persisted
                && string.Equals(persisted.IdempotencyKey, commandId.ToString("D"), StringComparison.Ordinal)
                && WithdrawalRecords(product, persisted.IdempotencyKey) == 1)
                return Success(operation, true);
            return Fail("PRODUCT_IDENTITY_WITHDRAWAL_REPLAY_DRIFT", 409);
        }
        if (product.Version != expectedVersion
            || product.LifecycleStatus != ProductIdentityLifecycleStatus.PendingIdentityApproval)
        {
            return Fail("PRODUCT_IDENTITY_STATE_CONFLICT", 409);
        }
        if (!operation.WithdrawalCommandId.HasValue
            && operation.Checkpoint != GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision)
        {
            return Fail("PRODUCT_IDENTITY_WORKFLOW_STATE_CONFLICT", 409);
        }

        if (operation.Checkpoint is GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision
                or GlobalProductIdentityWorkflowCheckpoint.WithdrawalRequested
                or GlobalProductIdentityWorkflowCheckpoint.WithdrawalPreflightObserved
                or GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown
                or GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay
            && ConfigurationFault(tenantId, operation.OperationId) is { } configurationFault)
        {
            return configurationFault with { Operation = operation };
        }

        var result = await ProcessWithdrawalAsync(operation, delegatedUserToken,
            $"withdraw-{requesterSubjectId:N}", leaseDuration, retryDelay, cancellationToken,
            commandId, fingerprint, requesterSubjectId, expectedVersion, reasonCode, comment);
        return result with { IsReplay = operation.WithdrawalCommandId.HasValue || result.IsReplay };
    }

    private async Task<GlobalProductIdentityWorkflowProcessingResult> ProcessWithdrawalAsync(
        GlobalProductIdentityWorkflowOperation initial,
        string? delegatedUserToken,
        string leaseOwner,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        CancellationToken cancellationToken,
        Guid? commandId = null,
        string? fingerprint = null,
        Guid? requesterSubjectId = null,
        int? expectedVersion = null,
        string? reasonCode = null,
        string? comment = null)
    {
        var operation = initial;
        var attempt = new Attempt(!string.IsNullOrWhiteSpace(delegatedUserToken));
        if (initial.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision)
            attempt.ScheduleAtEntry = (true, initial.NextAttemptAtUtcTicksV1);
        for (var step = 0; step < 10; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Refused for good: the withdrawal's own answer, from its own field — the decision poll rewrites
            // LastFailureCode at every cycle, and a repeat must still be told what the first request was told.
            if (operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision
                && operation.WithdrawalCommandId.HasValue)
                return Fail(operation, operation.WithdrawalRefusalCode ??
                    "PRODUCT_IDENTITY_WORKFLOW_DECISION_PENDING", 409);
            if (operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.Completed)
            {
                return operation.WithdrawalCommandId.HasValue
                    ? Success(operation, true)
                    : Fail(operation, "PRODUCT_IDENTITY_WORKFLOW_STATE_CONFLICT", 409);
            }
            if (operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired)
                return Fail(operation, "PRODUCT_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED", 409);

            var now = timeProvider.GetUtcNow();
            // Not the caller's token (a claim cut off half-way may be written with nobody to give it back) and not
            // unbounded either (a half-open connection would hang the request and the worker): see ClaimAsync.
            var (claim, storeSilent) = await ClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
                [operation.Checkpoint], leaseOwner, now.UtcTicks, now.Add(leaseDuration).UtcTicks,
                IgnoreRetrySchedule: attempt.Interactive), leaseDuration);
            if (storeSilent) return Fail(operation, ProductIdentityWorkflowClaims.StoreUnavailable, 503);
            if (claim is null) return Fail(operation, "PRODUCT_IDENTITY_WORKFLOW_BUSY", 409);

            bool advanced;
            try
            {
                switch (operation.Checkpoint)
                {
                    case GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision
                        when commandId.HasValue && fingerprint is not null && requesterSubjectId.HasValue
                             && expectedVersion.HasValue && reasonCode is not null:
                        advanced = await operations.AdvanceAsync(claim, new(
                            GlobalProductIdentityWorkflowCheckpoint.WithdrawalRequested,
                            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
                            SettledNextAttempt(operation, now, retryDelay),
                            WithdrawalCommandId: commandId,
                            WithdrawalFingerprint: fingerprint,
                            WithdrawalRequesterSubjectId: requesterSubjectId,
                            WithdrawalExpectedProductVersion: expectedVersion,
                            WithdrawalReasonCode: reasonCode,
                            WithdrawalComment: comment,
                            ReleaseLease: true,
                            ServiceFaultCount: StreakIsOver(operation, now) ? 0 : null), cancellationToken);
                        break;
                    case GlobalProductIdentityWorkflowCheckpoint.WithdrawalRequested:
                        advanced = attempt.Interactive
                            ? await ObserveWithdrawalPreflightAsync(operation, claim, attempt, now, retryDelay,
                                cancellationToken)
                            : await ResolveAbandonedWithdrawalAsync(operation, claim, attempt, now, retryDelay,
                                cancellationToken);
                        break;
                    case GlobalProductIdentityWorkflowCheckpoint.WithdrawalPreflightObserved:
                    case GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown:
                    case GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay:
                        advanced = attempt.Interactive
                            ? await ObserveWithdrawalAsync(operation, claim, delegatedUserToken!, attempt, now,
                                retryDelay, cancellationToken)
                            : await ResolveAbandonedWithdrawalAsync(operation, claim, attempt, now, retryDelay,
                                cancellationToken);
                        break;
                    case GlobalProductIdentityWorkflowCheckpoint.WithdrawalObserved:
                        advanced = await ApplyWithdrawalAsync(operation, claim, attempt, now, retryDelay, cancellationToken);
                        break;
                    case GlobalProductIdentityWorkflowCheckpoint.WithdrawalApplied:
                        advanced = await VerifyWithdrawalAndCompleteAsync(operation, claim, now, cancellationToken);
                        break;
                    default:
                        await ReleaseClaimAsync(claim, operation, now);
                        return Fail(operation, "PRODUCT_IDENTITY_WORKFLOW_STATE_CONFLICT", 409);
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
                return Fail(operation, "PRODUCT_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT", 409);
            }
            operation = await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken)
                ?? throw new InvalidOperationException("PRODUCT_IDENTITY_WORKFLOW_OPERATION_LOST");
            if (attempt.Answer is { } answer)
                return Fail(operation, answer.Code, answer.Status) with { FaultScope = attempt.Scope };
            // The withdrawal was dropped and the decision is awaited again: the worker goes on polling it, in this
            // very cycle. (A requester who was just refused has been answered above.)
            if (operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision
                && !operation.WithdrawalCommandId.HasValue && !attempt.Interactive)
                return await ProcessAsync(operation, null, leaseOwner, leaseDuration, retryDelay,
                    stopWhenAwaitingDecision: false, cancellationToken);
            if (operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay)
                return MakerAnswer(operation, attempt);
            // A requester who is present walks on through the settle window their own steps wrote (it keeps the
            // WORKER away, not them); only a retry that was really scheduled ends the request.
            if (operation.NextAttemptAtUtcTicksV1 > timeProvider.GetUtcNow().UtcTicks
                && (!attempt.Interactive
                    || operation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable))
                return ScheduledAnswer(operation, attempt);
        }
        // A withdrawal that was applied is a success however many steps it took: the product is a draft already.
        if (operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.Completed
            && operation.WithdrawalCommandId.HasValue)
            return Success(operation, true);
        return Fail(operation, "PRODUCT_IDENTITY_WORKFLOW_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<bool> ObserveWithdrawalPreflightAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!CompleteWithdrawalRequest(operation) || !CompleteStartProof(operation))
            return await QuarantineAsync(claim, "PRODUCT_IDENTITY_WITHDRAWAL_CONTRACT_INVALID", now, cancellationToken);
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
                return await QuarantineAsync(claim, "PRODUCT_IDENTITY_WITHDRAWAL_PREFLIGHT_INVALID", now,
                    cancellationToken);
            attempt.VersionsReadNow = true;
            return await operations.AdvanceAsync(claim, new(
                GlobalProductIdentityWorkflowCheckpoint.WithdrawalPreflightObserved,
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
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!CompleteWithdrawalRequest(operation) || !CompleteStartProof(operation))
            return await QuarantineAsync(claim, "PRODUCT_IDENTITY_WITHDRAWAL_CONTRACT_INVALID", now, cancellationToken);
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
                    "Product identity workflow operation {OperationId} of tenant {TenantId}: the approval is closed and the withdrawal has waited for its requester since {SinceUtc:O} ({ResultCode}); nobody came back to finish it. It is handed to manual reconciliation.",
                    operation.OperationId, operation.TenantId, new DateTimeOffset(waitingSince, TimeSpan.Zero),
                    BoundedCode(evidence.ErrorCode, ProductIdentityWorkflowMakerCodes.MakerReplayRequired));
                return await QuarantineAsync(claim, WithdrawalRequesterNeverReturned, now, cancellationToken);
            }
            if (evidence.Outcome == ProductIdentityWorkflowTransportOutcome.Conflict)
                logger.LogWarning(
                    "Product identity workflow operation {OperationId} of tenant {TenantId}: the approval is closed and holds no decision ({ResultCode}); the withdrawal appears to have landed and waits for its requester to repeat it. The worker keeps reading.",
                    operation.OperationId, operation.TenantId,
                    BoundedCode(evidence.ErrorCode, ProductIdentityWorkflowMakerCodes.MakerReplayRequired));
            else
                logger.LogWarning(
                    "Product identity workflow operation {OperationId} of tenant {TenantId}: the approval is closed and its decision could not be accepted ({ResultCode}); whether the withdrawal landed is not known. It waits for its requester to repeat it. The worker keeps reading.",
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
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        Attempt attempt,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        (int Count, long Since)? faultStreak = null) =>
        operations.AdvanceAsync(claim, new(
            GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision,
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
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        Attempt attempt,
        string? errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // The engine's own reason, when it is one that can be stored and handed out; never a text of any length.
        var code = BoundedCode(errorCode, ProductIdentityWorkflowMakerCodes.ApprovalClosed);
        return operations.AdvanceAsync(claim, new(
            GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision,
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
        GlobalProductIdentityWorkflowOperation operation, Attempt attempt) =>
        attempt.ScheduleAtEntry.Known ? attempt.ScheduleAtEntry.Ticks : operation.NextAttemptAtUtcTicksV1;

    /*
     * The cancellation was refused before it was processed — but an EARLIER cancellation of this withdrawal may
     * have landed (its answer was lost, or its request was cut off). Forgetting the withdrawal now would leave an
     * approval that is cancelled at the engine to the decision poll, which can only quarantine it. So the operation
     * stays a withdrawal whose outcome is unknown; the worker reads the approval and decides. Nothing new can land
     * from this attempt, so the worker's schedule is not moved.
     */
    private Task<bool> KeepUnknownWithdrawalAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        string code,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        (int Count, long Since)? faultStreak = null) =>
        operations.AdvanceAsync(claim, new(
            GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown,
            ProductIdentityWorkflowRecoveryDisposition.Retryable,
            now.UtcTicks,
            operation.NextAttemptAtUtcTicksV1,
            code,
            ReleaseLease: true,
            ServiceFaultCount: faultStreak?.Count ?? 0,
            ServiceFaultSinceUtcTicksV1: faultStreak?.Since), cancellationToken);

    private async Task<bool> ObserveWithdrawalAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        string delegatedUserToken,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!CompleteWithdrawalPreflight(operation))
            return await QuarantineAsync(claim, "PRODUCT_IDENTITY_WITHDRAWAL_PREFLIGHT_INVALID", now,
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
                GlobalProductIdentityWorkflowCheckpoint.WithdrawalObserved,
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
            && claim.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.WithdrawalPreflightObserved);
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
                    "Product identity workflow operation {OperationId} of tenant {TenantId}: the approval moved on while it was being withdrawn ({ResultCode}) and is still open; the withdrawal is asked again with fresh versions.",
                    operation.OperationId, operation.TenantId,
                    BoundedCode(result.ErrorCode, "PRODUCT_IDENTITY_WORKFLOW_PROVIDER_UNAVAILABLE"));
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
            var openCode = BoundedCode(open.ErrorCode, "PRODUCT_IDENTITY_WORKFLOW_PROVIDER_UNAVAILABLE");
            LogServiceFault(operation, ProductIdentityWorkflowCall.CancellationPreflight, openCode, openStreak.Count,
                openStreak.Since, now, openFault.Loud,
                "the worker reads the approval to learn whether it is still open");
            return await operations.AdvanceAsync(claim, new(
                GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown,
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
        var code = BoundedCode(result.ErrorCode, "PRODUCT_IDENTITY_WORKFLOW_PROVIDER_UNAVAILABLE");
        switch (fault.Kind)
        {
            case ProductIdentityWorkflowFaultKind.MakerReplay:
                // Refused for a reason that is the requester's (their session): nothing was cancelled.
                logger.LogWarning(
                    "Product identity workflow operation {OperationId} of tenant {TenantId} was refused to the maker on {Call}: {ResultCode} -> {MakerCode}.",
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
                    GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown,
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
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!TryRehydrateCancellationEvidence(operation, out var evidence))
            return await QuarantineAsync(claim, "PRODUCT_IDENTITY_WITHDRAWAL_EVIDENCE_INVALID", now,
                cancellationToken);
        var product = await products.GetByIdAsync(operation.GlobalProductId, cancellationToken);
        if (product is null) return await QuarantineAsync(claim, "PRODUCT_IDENTITY_NOT_FOUND", now,
            cancellationToken);
        var audit = ProductIdentityLifecycleAuditIntentFactory.CreateWithdrawal(product,
            operation.WithdrawalExpectedProductVersion!.Value, evidence);
        var result = await products.WithdrawIdentityApprovalAsync(operation.GlobalProductId,
            operation.WithdrawalExpectedProductVersion.Value, evidence, audit, cancellationToken);
        // BL-552 flow 3 FIX1 item 1 (a): the engine has already cancelled the approval; a refusal of the local write
        // that passes by itself (the audit cap, the store) waits here, a concurrent write is counted — the shared
        // classification. Before: every refusal was a quarantine no repeat could reach (the withdrawal guard).
        if (!result.Succeeded)
            return await WriteRefusedAsync(operation, claim, result.ErrorCode, attempt, now, retryDelay,
                cancellationToken);
        return await AdvanceAsync(claim, GlobalProductIdentityWorkflowCheckpoint.WithdrawalApplied,
            ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true,
            cancellationToken: cancellationToken);
    }

    // BL-552 flow 3 FIX1 item 1 (b): the withdrawal's audit intent, or its receipt once the audit delivery compacted
    // it (the flow 1 FIX1 rule) — a compaction between the apply and its check is not an inconsistency.
    private static int WithdrawalRecords(GlobalProduct product, string idempotencyKey) =>
        product.AuditIntents.Count(x => x.Operation == ProductAuditOperation.GlobalProductIdentityApprovalWithdrawn
            && string.Equals(x.IdempotencyKey, idempotencyKey, StringComparison.Ordinal))
        + product.AuditIntentReceipts.Count(x => string.Equals(x.IdempotencyKey, idempotencyKey, StringComparison.Ordinal));

    private async Task<bool> VerifyWithdrawalAndCompleteAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!TryRehydrateCancellationEvidence(operation, out var evidence))
            return await QuarantineAsync(claim, "PRODUCT_IDENTITY_WITHDRAWAL_EVIDENCE_INVALID", now,
                cancellationToken);
        var product = await products.GetByIdAsync(operation.GlobalProductId, cancellationToken);
        if (product is null || product.Version != operation.WithdrawalExpectedProductVersion + 1
            || product.LifecycleStatus != ProductIdentityLifecycleStatus.Draft
            || product.WorkflowBinding?.CancellationEvidence is not { } persisted
            || !SameCancellation(persisted, evidence)
            || WithdrawalRecords(product, evidence.IdempotencyKey) != 1)
            return await QuarantineAsync(claim, "PRODUCT_IDENTITY_WITHDRAWAL_LOCAL_STATE_INCONSISTENT", now,
                cancellationToken);
        return await AdvanceAsync(claim, GlobalProductIdentityWorkflowCheckpoint.Completed,
            ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true,
            cancellationToken: cancellationToken);
    }

    private async Task<GlobalProductIdentityWorkflowProcessingResult> ProcessAsync(
        GlobalProductIdentityWorkflowOperation initial,
        string? delegatedUserToken,
        string leaseOwner,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        bool stopWhenAwaitingDecision,
        CancellationToken cancellationToken)
    {
        var operation = initial;
        var attempt = new Attempt(!string.IsNullOrWhiteSpace(delegatedUserToken));
        for (var step = 0; step < 12; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.Completed)
            {
                // BL-552: a submit whose approval closed without an applicable decision ended with the product a draft
                // again: its maker's repeat is told why, not "done".
                return attempt.Interactive && operation.LastFailureCode is ClosedWithoutDecision or SelfApprovalIgnored
                    ? Fail(operation, operation.LastFailureCode, 409)
                    : Success(operation, true);
            }
            if (operation.Checkpoint is GlobalProductIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart
                or GlobalProductIdentityWorkflowCheckpoint.Superseded)
            {
                return Success(operation, true);
            }
            if (stopWhenAwaitingDecision && !attempt.ObserveNow
                && operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision)
            {
                return Success(operation, false);
            }
            if (operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay
                && string.IsNullOrWhiteSpace(delegatedUserToken))
            {
                return Fail(operation, "PRODUCT_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED", 409);
            }
            var reconcile = false;
            if (operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired)
            {
                // BL-552 (the GSKU identity E2 rule): a submit STUCK for a person has a way out — its submitter's same
                // command reads the engine again (once) and puts it back where the engine and the product say it
                // stands. One the repeat could not settle says so (a person, BL-530); an engine that could not be asked
                // is a service fault (503). The worker never reconciles; a withdrawal's quarantine is not this one.
                if (attempt.EngineSilent)
                {
                    return Fail(operation, ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, 503);
                }
                // Only a REPEAT reconciles: the request that just put the submit here does not settle it itself.
                // Flow 3 FIX1 item 12: the repeat reads the engine — not with the chain unready: the stored answer then,
                // no claim and no call.
                if (!attempt.Interactive || attempt.Reconciled || step > 0 || operation.WithdrawalCommandId.HasValue
                    || operation.RecoveryDisposition != ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired
                    || ConfigurationFault(operation.TenantId, operation.OperationId) is not null)
                {
                    if (attempt.Reconciled)
                        operation = await HandToAPersonAsync(operation, leaseOwner, leaseDuration, cancellationToken);
                    return Fail(operation, attempt.Reconciled ? ManualReconciliationNeeded
                        : "PRODUCT_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED", 409);
                }
                reconcile = attempt.Reconciled = true;
            }

            var now = timeProvider.GetUtcNow();
            var (claim, storeSilent) = await ClaimAsync(
                new(
                    operation.OperationId,
                    operation.OperationFingerprint,
                    [operation.Checkpoint],
                    leaseOwner,
                    now.UtcTicks,
                    now.Add(leaseDuration).UtcTicks,
                    IgnoreRetrySchedule: attempt.Interactive),
                leaseDuration); // see the withdrawal path
            if (storeSilent)
            {
                return Fail(operation, ProductIdentityWorkflowClaims.StoreUnavailable, 503);
            }
            if (claim is null)
            {
                return Fail(operation, "PRODUCT_IDENTITY_WORKFLOW_BUSY", 409);
            }

            bool advanced;
            try
            {
                advanced = operation.Checkpoint switch
                {
                    GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired when reconcile =>
                        operation.WorkflowInstanceId.HasValue
                            ? await ReconcileAfterStartAsync(operation, claim, attempt, now, cancellationToken)
                            : await ReconcileStartAsync(operation, claim, attempt, now, cancellationToken),
                    GlobalProductIdentityWorkflowCheckpoint.Prepared =>
                        await AdvanceAsync(claim, GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                            ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true,
                            cancellationToken: cancellationToken),
                    GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown or
                        GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay =>
                        await ResolveStartAsync(operation, claim, delegatedUserToken, attempt, now, retryDelay,
                            cancellationToken),
                    GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted =>
                        await ApplyLocalPendingAsync(operation, claim, attempt, now, retryDelay, cancellationToken),
                    GlobalProductIdentityWorkflowCheckpoint.LocalPendingApplied =>
                        await AdvanceAsync(claim, GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision,
                            ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true,
                            cancellationToken: cancellationToken),
                    GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision =>
                        await ObserveDecisionAsync(operation, claim, attempt, now, retryDelay, cancellationToken),
                    GlobalProductIdentityWorkflowCheckpoint.DecisionObserved =>
                        await ApplyDecisionAsync(operation, claim, attempt, now, retryDelay, cancellationToken),
                    GlobalProductIdentityWorkflowCheckpoint.DecisionApplied =>
                        await VerifyDecisionAppliedAndCompleteAsync(operation, claim, now, cancellationToken),
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
                return Fail(operation, "PRODUCT_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT", 409);
            }

            operation = await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken)
                ?? throw new InvalidOperationException("PRODUCT_IDENTITY_WORKFLOW_OPERATION_LOST");
            // The step that just ran has something to tell the caller (a service fault met by a maker who is
            // present, a refusal): the answer goes out now. Going round again would repeat the same refused call
            // until the step budget ran out.
            if (attempt.Answer is { } answer)
            {
                return Fail(operation, answer.Code, answer.Status) with { FaultScope = attempt.Scope };
            }
            // BL-552: the submitter's reconciling repeat that found nothing started IS the resubmit — it goes on.
            if (operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay
                && !(attempt.Reconciled && attempt.Interactive && !attempt.Restarted))
            {
                return MakerAnswer(operation, attempt);
            }
            if (operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay)
            {
                attempt.Restarted = true;
            }
            if (operation.NextAttemptAtUtcTicksV1 > timeProvider.GetUtcNow().UtcTicks
                && (!attempt.Interactive
                    || operation.RecoveryDisposition == ProductIdentityWorkflowRecoveryDisposition.Retryable))
            {
                return ScheduledAnswer(operation, attempt);
            }
        }

        return Fail(operation, "PRODUCT_IDENTITY_WORKFLOW_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<bool> ResolveStartAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
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
        string? startCode = null;
        if (makerPresent)
        {
            var outcome = await workflowClient.StartAsync(
                operation.TenantId,
                requestFactory.Rehydrate(operation),
                delegatedUserToken!,
                cancellationToken);
            if (outcome.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
            {
                return await PersistStartedAsync(operation, claim, outcome.Value, now, cancellationToken);
            }
            if (outcome.Outcome != ProductIdentityWorkflowTransportOutcome.Incomplete)
            {
                var fault = ProductIdentityWorkflowFaultPolicy.Classify(
                    ProductIdentityWorkflowCall.Start, outcome.Outcome, outcome.ErrorCode);
                switch (fault.Kind)
                {
                    case ProductIdentityWorkflowFaultKind.Quarantine:
                        return await QuarantineAsync(claim, outcome.ErrorCode, now, cancellationToken);
                    case ProductIdentityWorkflowFaultKind.MakerReplay:
                        return await ToMakerAsync(operation, claim, ProductIdentityWorkflowCall.Start,
                            outcome.ErrorCode, fault.MakerCode!, null, attempt, now, cancellationToken);
                    case ProductIdentityWorkflowFaultKind.ServiceWait when fault.StartedNothing:
                        return await ToMakerAsync(operation, claim, ProductIdentityWorkflowCall.Start,
                            outcome.ErrorCode, ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, fault,
                            attempt, now, cancellationToken);
                }
                // No answer, or one nobody could accept: the start may have arrived. Ask (below).
                startCode = outcome.ErrorCode;
            }
        }

        // BL-552: a start that went out and got no usable answer: "nothing arrived" is not trusted before the settle
        // window has passed — the engine may still be on it.
        long? startSentAt = makerPresent ? now.UtcTicks : null;
        var lookup = await workflowClient.GetStartResultAsync(
            operation.TenantId,
            new(operation.ObjectType, operation.ObjectId, operation.MakerSubjectId,
                operation.StartIdempotencyKey),
            cancellationToken);
        if (lookup.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
        {
            return await PersistStartedAsync(operation, claim, lookup.Value, now, cancellationToken);
        }

        // BL-552 — the LOOKUP's answer is read apart from the start's. Only "nothing under this key" after the settle
        // window lets the draft go (the maker's to resubmit, an edit may set it aside); an answer that could not be
        // read (Incomplete) says nothing about what the start did — the submit keeps its draft and is asked again.
        if (lookup.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound
            && (startSentAt.HasValue || StartSettling(operation, now)))
        {
            return await StartSettlingAsync(operation, claim, attempt, now, startSentAt, cancellationToken, startCode);
        }
        if (lookup.Outcome == ProductIdentityWorkflowTransportOutcome.Incomplete)
        {
            return await WaitForServiceAsync(
                operation, claim, GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                ProductIdentityWorkflowCall.StartResult, lookup.ErrorCode ?? StartLookupUnreadable,
                new(ProductIdentityWorkflowFaultKind.ServiceWait), attempt, now, retryDelay, cancellationToken,
                startSentAt);
        }

        if (lookup.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound)
        {
            // "Nothing arrived", the settle window passed (BL-552): the engine answering for this operation — the
            // streak of faults ends here, and the submit is the maker's to resubmit (a start sent in THIS request is
            // still settling, above).
            logger.LogWarning(
                "Product identity workflow operation {OperationId} of tenant {TenantId} has no completed start at the approval engine ({ResultCode}); it is the maker's to resubmit.",
                operation.OperationId, operation.TenantId,
                BoundedCode(lookup.ErrorCode, ProductIdentityWorkflowMakerCodes.MakerReplayRequired));
            return await AdvanceAsync(
                claim,
                GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay,
                now,
                lookup.ErrorCode ?? ProductIdentityWorkflowMakerCodes.MakerReplayRequired,
                releaseLease: true,
                cancellationToken: cancellationToken,
                serviceFaultCount: 0);
        }

        var lookupFault = ProductIdentityWorkflowFaultPolicy.Classify(
            ProductIdentityWorkflowCall.StartResult, lookup.Outcome, lookup.ErrorCode);
        if (lookupFault.Kind == ProductIdentityWorkflowFaultKind.ServiceWait)
        {
            return await WaitForServiceAsync(
                operation, claim, GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                ProductIdentityWorkflowCall.StartResult, lookup.ErrorCode, lookupFault, attempt, now, retryDelay,
                cancellationToken, startSentAt);
        }

        // BL-552: a refused LOOKUP (an answer about the record — a tenant mismatch, a refusal of who asks) says nothing
        // about what the start did; the engine may hold an approval for this submit. Before, it was quarantined —
        // and a submit in manual reconciliation does not hold the draft (Global Product's rule), so an edit could leave
        // that approval without its product. Now the draft stays held and the engine is asked again; the worker's
        // attempts are counted, and at the limit a person is needed (said once, counted by needing_a_person).
        return await LookupRefusedAsync(operation, claim, lookup.ErrorCode, attempt, now, retryDelay, startSentAt,
            cancellationToken);
    }

    private Task<bool> StartSettlingAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        Attempt attempt,
        DateTimeOffset now,
        long? startSentAt,
        CancellationToken cancellationToken,
        string? startCode = null)
    {
        var sentAt = startSentAt ?? operation.LastStartSentAtUtcTicksV1 ?? operation.CreatedAtUtcTicksV1;
        if (attempt.Interactive)
        {
            attempt.Answer = (ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, 503);
        }
        if (startSentAt.HasValue)
        {
            logger.LogWarning(
                "Product identity workflow operation {OperationId} of tenant {TenantId} met a service fault at Start ({ResultCode}) and the engine has nothing under its key yet; the draft is held for the settle window.",
                operation.OperationId, operation.TenantId, BoundedCode(startCode, StartSettlingCode));
        }
        return operations.AdvanceAsync(
            claim,
            new(
                GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                ProductIdentityWorkflowRecoveryDisposition.Retryable,
                now.UtcTicks,
                Math.Max(sentAt + faultSettings.StartSettleWindow.Ticks, now.UtcTicks + 1),
                StartSettlingCode,
                ReleaseLease: true,
                Streak: RecoveryStreakWrite.Keep,
                LastStartSentAtUtcTicksV1: startSentAt),
            cancellationToken);
    }

    private async Task<bool> LookupRefusedAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        string? errorCode,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        long? startSentAt,
        CancellationToken cancellationToken)
    {
        if (attempt.Interactive)
        {
            // The maker is told the engine could not confirm the submit yet — not "resubmit", which would not help.
            attempt.Answer = (StartLookupRefused, 409);
        }
        return await CountedRetryAsync(operation, claim, GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown,
            BoundedCode(errorCode, StartLookupRefused), attempt, now, retryDelay, startSentAt, cancellationToken);
    }

    // BL-552: an answer about the record that a repeat may still change (a refused lookup, a concurrent write): only the
    // worker's attempts count, in the store; at the limit the submit is stuck with its cause kept — a person's.
    private async Task<bool> CountedRetryAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        GlobalProductIdentityWorkflowCheckpoint checkpoint,
        string code,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        long? startSentAt,
        CancellationToken cancellationToken)
    {
        var counted = !attempt.Interactive;
        var (exhausted, _, next) = ProductIdentityRecoveryStreak.Next(
            counted ? operation.FailedAttemptCount : Math.Max(operation.FailedAttemptCount - 1, 0), now,
            retryDelay, faultSettings.QuarantineAfterFailedAttempts, faultSettings.MaxRetryDelay);
        if (counted && exhausted)
        {
            return await QuarantineAsync(claim, StuckOnRecordAnswer, now, cancellationToken);
        }
        return await operations.AdvanceAsync(
            claim,
            new(
                checkpoint,
                ProductIdentityWorkflowRecoveryDisposition.Retryable,
                now.UtcTicks,
                attempt.Interactive ? operation.NextAttemptAtUtcTicksV1 ?? next : next,
                code,
                ReleaseLease: true,
                Streak: counted ? RecoveryStreakWrite.Increment : RecoveryStreakWrite.Keep,
                LastStartSentAtUtcTicksV1: startSentAt),
            cancellationToken);
    }

    // BL-552 (flow 1 FIX1 item 9): the window is read from fields no claim or retry refreshes — when a start was last
    // sent, else when the submit was made.
    private bool StartSettling(GlobalProductIdentityWorkflowOperation operation, DateTimeOffset now) =>
        now.UtcTicks - (operation.LastStartSentAtUtcTicksV1 ?? operation.CreatedAtUtcTicksV1)
        < faultSettings.StartSettleWindow.Ticks;

    /*
     * BL-552 (the GSKU identity rule C(A) / E2) — the submitter's same command on a submit STUCK for a person.
     * Before the engine's approval is on record: what the engine knows decides — the start arrived → it goes on as
     * started; nothing under this submit's key (the settle window passed) → the maker's to resubmit, which this same
     * request does (once); it cannot say → said as such (503), or a person (409).
     */
    private async Task<bool> ReconcileStartAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        Attempt attempt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var lookup = await workflowClient.GetStartResultAsync(
            operation.TenantId,
            new(operation.ObjectType, operation.ObjectId, operation.MakerSubjectId, operation.StartIdempotencyKey),
            cancellationToken);
        if (lookup.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
        {
            return await PersistStartedAsync(operation, claim, lookup.Value, now, cancellationToken);
        }
        if (lookup.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound && !StartSettling(operation, now))
        {
            logger.LogWarning(
                "Product identity workflow operation {OperationId} of tenant {TenantId}: the submitter's repeat found nothing started at the engine; the submit is the maker's to resubmit.",
                operation.OperationId, operation.TenantId);
            return await AdvanceAsync(claim, GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, now,
                ProductIdentityWorkflowMakerCodes.MakerReplayRequired, releaseLease: true,
                cancellationToken: cancellationToken, serviceFaultCount: 0);
        }
        attempt.EngineSilent = lookup.Outcome is ProductIdentityWorkflowTransportOutcome.NotFound
                                   or ProductIdentityWorkflowTransportOutcome.Incomplete
                               || ProductIdentityWorkflowFaultPolicy.Classify(ProductIdentityWorkflowCall.StartResult,
                                   lookup.Outcome, lookup.ErrorCode).Kind == ProductIdentityWorkflowFaultKind.ServiceWait;
        await ReleaseClaimAsync(claim, operation, now);
        return true;
    }

    // Flow 3 FIX1 item 6: the engine answered the submitter's repeat and the submit is STILL stuck on a code that repeat
    // was meant to settle (the engine's evidence still contradicts itself, the start proof is still unusable, nothing
    // decides where it stands): only a person can now. Its code becomes STUCK_ON_RECORD_ANSWER, so needing_a_person
    // counts it — before, the code stayed "settled by the repeat" and S2 held the draft with no alarm. A claim that
    // cannot be had leaves it as it is (the next repeat tries again).
    private async Task<GlobalProductIdentityWorkflowOperation> HandToAPersonAsync(
        GlobalProductIdentityWorkflowOperation operation,
        string leaseOwner,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (operation.RecoveryDisposition != ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired
            || !SettledBySubmittersRepeat.Contains(operation.LastFailureCode ?? string.Empty))
            return operation;
        var now = timeProvider.GetUtcNow();
        var (claim, storeSilent) = await ClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
            [GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired], leaseOwner, now.UtcTicks,
            now.Add(leaseDuration).UtcTicks, IgnoreRetrySchedule: true), leaseDuration);
        if (storeSilent || claim is null) return operation;
        logger.LogError(
            "Product identity workflow operation {OperationId} of tenant {TenantId}: its submitter's repeat could not settle {ResultCode}; a person has to (BL-530).",
            operation.OperationId, operation.TenantId, operation.LastFailureCode);
        if (!await AdvanceAsync(claim, GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired,
                ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, now, StuckOnRecordAnswer,
                releaseLease: true, cancellationToken: cancellationToken))
        {
            await ReleaseClaimAsync(claim, operation, now);
            return operation;
        }
        return await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken) ?? operation;
    }

    /*
     * After the engine's approval is on record: the engine is read again and the submit is put back where the product
     * stands — a decision on record → applied again; the product pending under this approval → waiting for (or reading
     * now, if the engine has closed it) its decision; the product still the submitted draft → started again (the
     * pending write is made again). Anything else, or an engine that does not know the approval → a person (409).
     */
    private async Task<bool> ReconcileAfterStartAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        Attempt attempt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var outcome = await workflowClient.GetTerminalEvidenceAsync(
            operation.TenantId,
            new(operation.WorkflowInstanceId!.Value, operation.ObjectType, operation.ObjectId),
            cancellationToken);
        var closed = outcome.Outcome == ProductIdentityWorkflowTransportOutcome.Success
                     || ProductIdentityWorkflowFaultPolicy.IsClosedWithoutDecision(outcome);
        var open = outcome.Outcome is ProductIdentityWorkflowTransportOutcome.NonTerminal
            or ProductIdentityWorkflowTransportOutcome.Incomplete;
        var product = await products.GetByIdAsync(operation.GlobalProductId, cancellationToken);
        GlobalProductIdentityWorkflowCheckpoint? reentry = !closed && !open ? null
            : !CompleteStartProof(operation) || product is null ? null
            : product.LifecycleStatus == ProductIdentityLifecycleStatus.PendingIdentityApproval
              && product.Version == operation.ExpectedProductVersion + 1
              && product.WorkflowBinding?.WorkflowInstanceId == operation.WorkflowInstanceId
                ? operation.DecisionKind.HasValue
                    ? GlobalProductIdentityWorkflowCheckpoint.DecisionObserved
                    : GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision
            : product.LifecycleStatus == ProductIdentityLifecycleStatus.Draft
              && product.Version == operation.ExpectedProductVersion && !operation.DecisionKind.HasValue
                ? GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted
            : null;
        if (reentry is null)
        {
            attempt.EngineSilent = !closed && !open && outcome.Outcome != ProductIdentityWorkflowTransportOutcome.NotFound
                && ProductIdentityWorkflowFaultPolicy.Classify(ProductIdentityWorkflowCall.TerminalEvidence,
                    outcome.Outcome, outcome.ErrorCode).Kind == ProductIdentityWorkflowFaultKind.ServiceWait;
            await ReleaseClaimAsync(claim, operation, now);
            return true;
        }
        logger.LogWarning(
            "Product identity workflow operation {OperationId} of tenant {TenantId}: the submitter's repeat reconciled it at {Checkpoint} (the engine's approval is {State}).",
            operation.OperationId, operation.TenantId, reentry, closed ? "closed" : "open");
        attempt.ObserveNow = closed;
        var started = reentry == GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted;
        return await operations.AdvanceAsync(
            claim,
            new(
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
                ServiceFaultCount: 0),
            cancellationToken);
    }

    /*
     * BL-552 (the GSKU identity E1 rule) — the engine closed the approval and no decision can be applied: cancelled in
     * the Task Center or timed out (WORKFLOW_INSTANCE_CLOSED_WITHOUT_DECISION), or decided by its own maker. Before, it
     * was quarantined and the product stayed pending for ever. Now the product is a draft again, in one write with the
     * intent that says why, and the submit ends (Completed, with the reason): the maker may edit and submit anew.
     */
    private async Task<bool> BackToDraftAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        Attempt attempt,
        string reasonCode,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(operation.GlobalProductId, cancellationToken);
        if (product is null || !CompleteStartProof(operation))
        {
            return await QuarantineAsync(claim, "PRODUCT_IDENTITY_NOT_FOUND", now, cancellationToken);
        }
        var expectedVersion = checked(operation.ExpectedProductVersion + 1);
        var audit = ProductIdentityLifecycleAuditIntentFactory.CreateDecisionNotApplicable(
            product, expectedVersion, operation.OperationId, operation.WorkflowInstanceId!.Value,
            operation.ApprovalTaskId!.Value, operation.MakerSubjectId, reasonCode, now);
        var result = await products.ReturnIdentityToDraftAsync(product.Id, expectedVersion,
            operation.WorkflowInstanceId.Value, operation.ApprovalTaskId.Value, audit, cancellationToken);
        if (!result.Succeeded)
        {
            // The capacity, the store: they pass by themselves. Anything else is about the record: stuck, and its
            // submitter's same command reads the engine again.
            return await WriteRefusedAsync(operation, claim, result.ErrorCode, attempt, now, retryDelay,
                cancellationToken);
        }
        logger.LogWarning(
            "Product identity workflow operation {OperationId} of tenant {TenantId}: the approval was closed without a decision that can be applied ({ResultCode}); the product is a draft again.",
            operation.OperationId, operation.TenantId, reasonCode);
        return await AdvanceAsync(claim, GlobalProductIdentityWorkflowCheckpoint.Completed,
            ProductIdentityWorkflowRecoveryDisposition.None, now, reasonCode, releaseLease: true,
            cancellationToken: cancellationToken, serviceFaultCount: 0);
    }

    // BL-552 (the GSKU FIX5 item 3 rule): a refused product write. The audit cap and the store pass by themselves
    // (wait, never stuck); a concurrent write is read and tried again (counted); anything else will not change by
    // waiting: stuck at once, the draft held — a person (BL-530) or the submitter's same command.
    private Task<bool> WriteRefusedAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        string? code,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken) =>
        ProductIdentityWriteFaults.MayPassByItself(code)
            ? ScheduleRetryAsync(claim, operation.Checkpoint, code, now, retryDelay, cancellationToken)
            : ProductIdentityWriteFaults.MetAConcurrentWrite(code)
                ? CountedRetryAsync(operation, claim, operation.Checkpoint, BoundedCode(code, StuckOnRecordAnswer),
                    attempt, now, retryDelay, null, cancellationToken)
                : QuarantineAsync(claim, code, now, cancellationToken);

    public const string ClosedWithoutDecision = "PRODUCT_IDENTITY_WORKFLOW_CLOSED_WITHOUT_DECISION";
    public const string SelfApprovalIgnored = "PRODUCT_IDENTITY_WORKFLOW_SELF_APPROVAL_IGNORED";
    public const string ManualReconciliationNeeded = "PRODUCT_IDENTITY_WORKFLOW_MANUAL_RECONCILIATION_NEEDED";
    public const string StuckOnRecordAnswer = "PRODUCT_IDENTITY_WORKFLOW_STUCK_ON_RECORD_ANSWER";
    public const string StartSettlingCode = "PRODUCT_IDENTITY_WORKFLOW_START_SETTLING";
    public const string StartLookupRefused = "PRODUCT_IDENTITY_WORKFLOW_START_LOOKUP_REFUSED";
    public const string StartLookupUnreadable = "PRODUCT_IDENTITY_WORKFLOW_START_LOOKUP_UNREADABLE";
    public const string EvidenceConflict = "PRODUCT_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT";
    public const string StartProofInvalid = "PRODUCT_IDENTITY_WORKFLOW_START_PROOF_INVALID";

    // BL-552: the stuck reasons the submitter's same command settles (it reads the engine again); every other one is a
    // person's (BL-530) and is counted by needing_a_person.
    public static readonly IReadOnlyList<string> SettledBySubmittersRepeat =
    [
        EvidenceConflict,
        StartProofInvalid,
        "WORKFLOW_TERMINAL_EVIDENCE_INCONSISTENT"
    ];

    private Task<bool> PersistStartedAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
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
            return QuarantineAsync(claim, "PRODUCT_IDENTITY_WORKFLOW_START_PROOF_INVALID", now, cancellationToken);
        }

        return operations.AdvanceAsync(
            claim,
            new(
                GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted,
                ProductIdentityWorkflowRecoveryDisposition.None,
                now.UtcTicks,
                WorkflowInstanceId: result.WorkflowInstanceId,
                WorkflowTemplateId: result.TemplateId,
                WorkflowTemplateVersionId: result.TemplateVersionId,
                ApprovalTaskId: result.ApprovalTaskId,
                AssignmentSnapshotId: result.AssignmentSnapshotId,
                StartTransitionLogId: result.StartTransitionLogId,
                WorkflowStartedAtUtcTicksV1: startedAt.UtcTicks,
                ReleaseLease: true,
                ServiceFaultCount: 0),
            cancellationToken);
    }

    private async Task<bool> ApplyLocalPendingAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(operation.GlobalProductId, cancellationToken);
        if (product is null)
        {
            return await QuarantineAsync(claim, "PRODUCT_IDENTITY_NOT_FOUND", now, cancellationToken);
        }
        if (!CompleteStartProof(operation))
        {
            return await QuarantineAsync(claim, "PRODUCT_IDENTITY_WORKFLOW_START_PROOF_INVALID", now, cancellationToken);
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
            ObjectId = operation.GlobalProductId,
            ObjectRef = operation.ObjectRef,
            SubmitterSubjectId = operation.MakerSubjectId,
            StartIdempotencyKey = operation.StartIdempotencyKey,
            StartRequestFingerprint = operation.OperationFingerprint,
            SubmittedAtUtc = submittedAt,
            DueAtUtc = operation.DueAtUtcTicksV1.HasValue
                ? new DateTimeOffset(operation.DueAtUtcTicksV1.Value, TimeSpan.Zero)
                : null
        };
        var audit = ProductIdentityLifecycleAuditIntentFactory.CreateSubmit(
            product, operation.ExpectedProductVersion, binding);
        var result = await products.SubmitIdentityAsync(
            operation.GlobalProductId,
            operation.ExpectedProductVersion,
            binding,
            audit,
            cancellationToken);
        if (!result.Succeeded)
        {
            return await WriteRefusedAsync(operation, claim, result.ErrorCode, attempt, now, retryDelay,
                cancellationToken);
        }
        if (result.GlobalProduct is null
            || result.GlobalProduct.Version != operation.ExpectedProductVersion + 1
            || result.GlobalProduct.LifecycleStatus != ProductIdentityLifecycleStatus.PendingIdentityApproval
            || !ExactBinding(result.GlobalProduct.WorkflowBinding, binding)
            // BL-552 flow 3 (flow 1 FIX1 item 1): the submit's intent, or its receipt once audit delivery compacted it.
            || result.GlobalProduct.AuditIntents.Count(intent =>
                intent.Operation == ProductAuditOperation.GlobalProductIdentitySubmitted
                && string.Equals(intent.IdempotencyKey, operation.StartIdempotencyKey,
                    StringComparison.Ordinal))
               + result.GlobalProduct.AuditIntentReceipts.Count(receipt =>
                   string.Equals(receipt.IdempotencyKey, operation.StartIdempotencyKey, StringComparison.Ordinal)) != 1)
        {
            return await QuarantineAsync(claim,
                "PRODUCT_IDENTITY_WORKFLOW_LOCAL_PENDING_INCONSISTENT", now, cancellationToken);
        }

        return await AdvanceAsync(
            claim,
            GlobalProductIdentityWorkflowCheckpoint.LocalPendingApplied,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now,
            releaseLease: true,
            cancellationToken: cancellationToken);
    }

    private async Task<bool> ObserveDecisionAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!operation.WorkflowInstanceId.HasValue)
        {
            return await QuarantineAsync(claim, "PRODUCT_IDENTITY_WORKFLOW_START_PROOF_INVALID", now, cancellationToken);
        }

        var result = await workflowClient.GetTerminalEvidenceAsync(
            operation.TenantId,
            new(operation.WorkflowInstanceId.Value, operation.ObjectType, operation.ObjectId),
            cancellationToken);
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.NonTerminal
            or ProductIdentityWorkflowTransportOutcome.NotFound
            or ProductIdentityWorkflowTransportOutcome.Incomplete)
        {
            return await ScheduleRetryAsync(
                claim,
                GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision,
                result.ErrorCode ?? "PRODUCT_IDENTITY_WORKFLOW_DECISION_PENDING",
                now,
                retryDelay,
                cancellationToken,
                chainAnswered: true);
        }
        // BL-552 — the engine's three answers: NOT_TERMINAL (an escalated approval included) waits, above;
        // CLOSED_WITHOUT_DECISION ends the submit and the product is a draft again; TERMINAL_EVIDENCE_INCONSISTENT is
        // stuck below (the product stays pending) and its submitter's same command reads the engine again.
        if (ProductIdentityWorkflowFaultPolicy.IsClosedWithoutDecision(result))
        {
            return await BackToDraftAsync(operation, claim, attempt, ClosedWithoutDecision, now, retryDelay, cancellationToken);
        }
        if (result.Outcome != ProductIdentityWorkflowTransportOutcome.Success)
        {
            // The decision is polled with the service's own token: the worker carries a waiting operation on.
            var fault = ProductIdentityWorkflowFaultPolicy.Classify(
                ProductIdentityWorkflowCall.TerminalEvidence, result.Outcome, result.ErrorCode);
            return fault.Kind == ProductIdentityWorkflowFaultKind.ServiceWait
                ? await WaitForServiceAsync(operation, claim, GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision,
                    ProductIdentityWorkflowCall.TerminalEvidence, result.ErrorCode, fault, attempt, now, retryDelay,
                    cancellationToken)
                : await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
        }
        if (result.Value is null)
        {
            return await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
        }

        var evidence = result.Value;
        // Flow 3 FIX1 item 8 (the GSKU E1 order): only a DECISION the engine names can be "its own maker's" — an
        // action that is not a decision (a withdrawal, say) is evidence that does not fit, whoever the actor.
        if (TryMapDecision(evidence.TerminalAction, out _)
            && Guid.TryParse(evidence.ActorUserId, out var self) && self == operation.MakerSubjectId)
        {
            // BL-552: the maker decided their own submit and the engine has closed it — no other decision will come.
            // It is not applied; the product is a draft again (before: quarantined, the product pending for ever).
            return await BackToDraftAsync(operation, claim, attempt, SelfApprovalIgnored, now, retryDelay, cancellationToken);
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
            || evidence.DecisionAt.Offset != TimeSpan.Zero)
        {
            return await QuarantineAsync(claim, EvidenceConflict, now, cancellationToken);
        }

        return await operations.AdvanceAsync(
            claim,
            new(
                GlobalProductIdentityWorkflowCheckpoint.DecisionObserved,
                ProductIdentityWorkflowRecoveryDisposition.None,
                now.UtcTicks,
                WorkflowInstanceId: evidence.WorkflowInstanceId,
                WorkflowTemplateVersionId: evidence.TemplateVersionId,
                ApprovalTaskId: evidence.ApprovalTaskId,
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
                ServiceFaultCount: 0),
            cancellationToken);
    }

    private async Task<bool> ApplyDecisionAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!TryRehydrateEvidence(operation, out var evidence))
        {
            return await QuarantineAsync(claim, "PRODUCT_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        }
        var product = await products.GetByIdAsync(operation.GlobalProductId, cancellationToken);
        if (product is null)
        {
            return await QuarantineAsync(claim, "PRODUCT_IDENTITY_NOT_FOUND", now, cancellationToken);
        }

        var expectedVersion = checked(operation.ExpectedProductVersion + 1);
        var audit = ProductIdentityLifecycleAuditIntentFactory.CreateDecision(product, expectedVersion, evidence);
        var result = await products.ReconcileIdentityDecisionAsync(
            operation.GlobalProductId, expectedVersion, evidence, audit, cancellationToken);
        if (!result.Succeeded)
        {
            return await WriteRefusedAsync(operation, claim, result.ErrorCode, attempt, now, retryDelay,
                cancellationToken);
        }
        if (!ExactDecisionResult(result.GlobalProduct, operation, evidence))
        {
            return await QuarantineAsync(claim,
                "PRODUCT_IDENTITY_WORKFLOW_DECISION_INCONSISTENT", now, cancellationToken);
        }

        return await AdvanceAsync(
            claim,
            GlobalProductIdentityWorkflowCheckpoint.DecisionApplied,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now,
            releaseLease: true,
            cancellationToken: cancellationToken);
    }

    private async Task<bool> VerifyDecisionAppliedAndCompleteAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!TryRehydrateEvidence(operation, out var evidence))
        {
            return await QuarantineAsync(claim,
                "PRODUCT_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        }
        var product = await products.GetByIdAsync(operation.GlobalProductId, cancellationToken);
        if (!ExactDecisionResult(product, operation, evidence))
        {
            return await QuarantineAsync(claim,
                "PRODUCT_IDENTITY_WORKFLOW_DECISION_INCONSISTENT", now, cancellationToken);
        }
        return await AdvanceAsync(claim, GlobalProductIdentityWorkflowCheckpoint.Completed,
            ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true,
            cancellationToken: cancellationToken);
    }

    private Task<bool> ScheduleRetryAsync(
        GlobalProductIdentityWorkflowClaim claim,
        GlobalProductIdentityWorkflowCheckpoint checkpoint,
        string? errorCode,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken,
        bool chainAnswered = false) =>
        operations.AdvanceAsync(
            claim,
            new(
                checkpoint,
                ProductIdentityWorkflowRecoveryDisposition.Retryable,
                now.UtcTicks,
                now.Add(retryDelay).UtcTicks,
                BoundedCode(errorCode, "PRODUCT_IDENTITY_WORKFLOW_PROVIDER_UNAVAILABLE"),
                ReleaseLease: true,
                ServiceFaultCount: chainAnswered ? 0 : null),
            cancellationToken);

    /*
     * A fault of the service chain at a stage the worker can carry on: the operation stays at its checkpoint and it
     * is said out loud.
     *
     * WHO COUNTS. Only the worker's attempts count and back off. A maker who is present is answered at once
     * ("resubmit") and changes neither the count nor the worker's next attempt — six clicks during an outage must
     * not push the worker's retry to the ceiling or turn a two-minute outage into an "error since attempt 6".
     */
    private Task<bool> WaitForServiceAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        GlobalProductIdentityWorkflowCheckpoint checkpoint,
        ProductIdentityWorkflowCall call,
        string? errorCode,
        ProductIdentityWorkflowFaultDecision fault,
        Attempt attempt,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken,
        long? startSentAt = null)
    {
        var streak = FaultStreak(operation, now, countsAsWorkerAttempt: !attempt.Interactive);
        var code = BoundedCode(errorCode, "PRODUCT_IDENTITY_WORKFLOW_PROVIDER_UNAVAILABLE");
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
                    ServiceFaultSinceUtcTicksV1: streak.Since,
                    LastStartSentAtUtcTicksV1: startSentAt),
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
                ServiceFaultSinceUtcTicksV1: streak.Since,
                LastStartSentAtUtcTicksV1: startSentAt),
            cancellationToken);
    }

    // The operation is handed to the maker: nothing was started and only a resubmit can do it. serviceFault is null
    // when the engine answered and the reason is the maker's or the set-up's — an answer, which ends a fault streak.
    // Only a maker's own attempt gets here, so a service fault does not count as a worker attempt.
    private Task<bool> ToMakerAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
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
                "Product identity workflow operation {OperationId} of tenant {TenantId} was refused to the maker on {Call}: {ResultCode} -> {MakerCode}.",
                operation.OperationId, operation.TenantId, call, code, makerCode);
        }

        return operations.AdvanceAsync(
            claim,
            new(
                GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay,
                now.UtcTicks,
                LastFailureCode: makerCode,
                ReleaseLease: true,
                ServiceFaultCount: answeredSince ? 0 : streak?.Count ?? 0,
                ServiceFaultSinceUtcTicksV1: answeredSince ? null : streak?.Since),
            cancellationToken);
    }

    public const string WithdrawalRequesterNeverReturned = "PRODUCT_IDENTITY_WITHDRAWAL_REQUESTER_NEVER_RETURNED";

    private async Task<(GlobalProductIdentityWorkflowClaim? Claim, bool StoreSilent)> ClaimAsync(
        GlobalProductIdentityWorkflowClaimRequest request,
        TimeSpan leaseDuration) =>
        await ProductIdentityWorkflowClaims.ClaimAsync(
            token => operations.TryClaimAsync(request, token),
            token => operations.ReleaseIfOwnedAsync(request.OperationId, request.LeaseOwner,
                request.LeaseUntilUtcTicks, token),
            leaseDuration,
            exception => logger.LogWarning(
                "Product identity workflow operation {OperationId}: the store did not answer a claim, and a lease it may have written could not be given back ({ExceptionType}); it expires by itself.",
                request.OperationId, exception.GetType().FullName));

    private (int Count, long Since) FaultStreak(
        GlobalProductIdentityWorkflowOperation operation,
        DateTimeOffset now,
        bool countsAsWorkerAttempt) =>
        ProductIdentityWorkflowFaultRules.Streak(operation, now, countsAsWorkerAttempt, faultSettings);

    private bool StreakIsOver(GlobalProductIdentityWorkflowOperation operation, DateTimeOffset now) =>
        ProductIdentityWorkflowFaultRules.StreakIsOver(operation, now, faultSettings);

    private static long SettledNextAttempt(
        GlobalProductIdentityWorkflowOperation operation,
        DateTimeOffset now,
        TimeSpan retryDelay) =>
        ProductIdentityWorkflowFaultRules.SettledNextAttempt(operation, now, retryDelay);

    // An error when the worker has tried often enough, when the streak of consecutive faults has lasted longer than
    // the longest back-off (so a fault met only by makers still becomes an error — by how long it has gone on, not
    // by their clicks), or on every attempt for a fault that waiting will not mend.
    private void LogServiceFault(
        GlobalProductIdentityWorkflowOperation operation,
        ProductIdentityWorkflowCall call,
        string code,
        int workerAttempts,
        long sinceUtcTicks,
        DateTimeOffset now,
        bool loud,
        string whatHappensNext) =>
        logger.Log(
            ProductIdentityWorkflowFaultRules.Level(loud, workerAttempts, sinceUtcTicks, now, faultSettings),
            "Product identity workflow operation {OperationId} of tenant {TenantId} met a service fault at {Call}: {ResultCode}; worker attempt {Attempt}, failing since {SinceUtc:O}; {WhatHappensNext}.",
            operation.OperationId, operation.TenantId, call, code, workerAttempts,
            new DateTimeOffset(sinceUtcTicks, TimeSpan.Zero), whatHappensNext);

    private GlobalProductIdentityWorkflowProcessingResult? ConfigurationFault(Guid tenantId, Guid operationId)
    {
        if (transportReadiness.FindConfigurationFault() is not { } code)
        {
            return null;
        }

        logger.LogError(
            "Product identity workflow transport is not configured ({ResultCode}); operation {OperationId} of tenant {TenantId} was not touched.",
            code, operationId, tenantId);
        return Fail(code, 503) with { FaultScope = ProductIdentityWorkflowFaultScope.AllTenants };
    }

    private static GlobalProductIdentityWorkflowProcessingResult MakerAnswer(
        GlobalProductIdentityWorkflowOperation operation,
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
    private static GlobalProductIdentityWorkflowProcessingResult ScheduledAnswer(
        GlobalProductIdentityWorkflowOperation operation,
        Attempt attempt) =>
        Fail(operation, operation.LastFailureCode ?? "PRODUCT_IDENTITY_WORKFLOW_RETRY_SCHEDULED", 503)
            with { FaultScope = attempt.Scope };

    // A lease this call took and is not going to finish under — the step was refused (a transition that is not
    // allowed, a checkpoint this path does not handle), the caller went away, or the store failed mid-step — is
    // given back, leaving everything else about the operation as it was, the worker's schedule included. Best
    // effort: if giving it back fails too, the lease runs out by itself, and that is said.
    private async Task ReleaseClaimAsync(
        GlobalProductIdentityWorkflowClaim claim,
        GlobalProductIdentityWorkflowOperation operation,
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
                "Product identity workflow operation {OperationId} of tenant {TenantId}: the lease could not be given back ({ExceptionType}); it expires by itself.",
                claim.OperationId, claim.TenantId, exception.GetType().FullName);
        }
    }

    private Task<bool> QuarantineAsync(
        GlobalProductIdentityWorkflowClaim claim,
        string? errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var code = BoundedCode(errorCode, "PRODUCT_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED");
        logger.LogError(
            "Product identity workflow operation {OperationId} of tenant {TenantId} was quarantined at {Checkpoint}: {ResultCode}.",
            claim.OperationId, claim.TenantId, claim.Checkpoint, code);
        return AdvanceAsync(
            claim,
            GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired,
            now,
            code,
            releaseLease: true,
            cancellationToken: cancellationToken);
    }

    private Task<bool> AdvanceAsync(
        GlobalProductIdentityWorkflowClaim claim,
        GlobalProductIdentityWorkflowCheckpoint checkpoint,
        ProductIdentityWorkflowRecoveryDisposition disposition,
        DateTimeOffset now,
        string? errorCode = null,
        bool releaseLease = false,
        CancellationToken cancellationToken = default,
        int? serviceFaultCount = null) =>
        operations.AdvanceAsync(
            claim,
            new(checkpoint, disposition, now.UtcTicks, LastFailureCode: errorCode,
                ReleaseLease: releaseLease, ServiceFaultCount: serviceFaultCount),
            cancellationToken);

    private static bool CompleteStartProof(GlobalProductIdentityWorkflowOperation operation) =>
        operation.WorkflowInstanceId is { } instanceId && instanceId != Guid.Empty
        && operation.WorkflowTemplateId is { } templateId && templateId != Guid.Empty
        && operation.WorkflowTemplateVersionId is { } versionId && versionId != Guid.Empty
        && operation.ApprovalTaskId is { } taskId && taskId != Guid.Empty
        && operation.AssignmentSnapshotId is { } snapshotId && snapshotId != Guid.Empty
        && operation.StartTransitionLogId is { } logId && logId != Guid.Empty
        && operation.WorkflowStartedAtUtcTicksV1 is > 0;

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
        && string.Equals(actual.ObjectType, expected.ObjectType, StringComparison.Ordinal)
        && string.Equals(actual.ObjectRef, expected.ObjectRef, StringComparison.Ordinal)
        && string.Equals(actual.StartIdempotencyKey, expected.StartIdempotencyKey, StringComparison.Ordinal)
        && string.Equals(actual.StartRequestFingerprint, expected.StartRequestFingerprint, StringComparison.Ordinal);

    private static bool ExactDecisionResult(
        GlobalProduct? product,
        GlobalProductIdentityWorkflowOperation operation,
        ProductIdentityWorkflowDecisionEvidence evidence)
    {
        if (product is null || product.Version != operation.ExpectedProductVersion + 2
            || product.WorkflowBinding?.TerminalDecision is not { } persisted
            || persisted.Decision != evidence.Decision
            || persisted.WorkflowInstanceId != evidence.WorkflowInstanceId
            || persisted.TransitionSequence != evidence.TransitionSequence
            || persisted.DecisionActorSubjectId != evidence.DecisionActorSubjectId
            || product.LifecycleStatus != (evidence.Decision == ProductIdentityDecisionKind.Approved
                ? ProductIdentityLifecycleStatus.IdentityApproved
                : ProductIdentityLifecycleStatus.Draft))
        {
            return false;
        }
        var expectedOperation = evidence.Decision == ProductIdentityDecisionKind.Approved
            ? ProductAuditOperation.GlobalProductIdentityApproved
            : ProductAuditOperation.GlobalProductIdentityRejected;
        var expectedKey = $"workflow:{evidence.WorkflowInstanceId:D}:{evidence.TransitionSequence}";
        // BL-552 flow 3: the decision's intent, or its receipt once audit delivery compacted it.
        return product.AuditIntents.Count(intent => intent.Operation == expectedOperation
                   && string.Equals(intent.IdempotencyKey, expectedKey, StringComparison.Ordinal))
               + product.AuditIntentReceipts.Count(receipt =>
                   string.Equals(receipt.IdempotencyKey, expectedKey, StringComparison.Ordinal)) == 1;
    }

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
        GlobalProductIdentityWorkflowOperation operation,
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
        GlobalProductIdentityWorkflowOperation operation,
        Guid tenantId,
        Guid productId,
        int expectedVersion,
        Guid makerSubjectId) =>
        !operation.IsDeleted && operation.TenantId == tenantId
        && operation.GlobalProductId == productId
        && operation.ExpectedProductVersion == expectedVersion
        && operation.MakerSubjectId == makerSubjectId
        && string.Equals(operation.ObjectType, ProductIdentityWorkflowStartRequestFactory.GlobalProductObjectType,
            StringComparison.Ordinal)
        && string.Equals(operation.ObjectId, productId.ToString("D"), StringComparison.Ordinal);

    private static bool CompleteWithdrawalRequest(GlobalProductIdentityWorkflowOperation operation) =>
        operation.WithdrawalCommandId is { } commandId && commandId != Guid.Empty
        && operation.WithdrawalRequesterSubjectId == operation.MakerSubjectId
        && operation.WithdrawalExpectedProductVersion is > 0
        && ExactText(operation.WithdrawalReasonCode, 128)
        && OptionalExactText(operation.WithdrawalComment, 2000)
        && ExactText(operation.WithdrawalFingerprint, 256);

    private static bool CompleteWithdrawalPreflight(GlobalProductIdentityWorkflowOperation operation) =>
        CompleteWithdrawalRequest(operation)
        && operation.WithdrawalExpectedWorkflowInstanceVersion is > 0
        && operation.WithdrawalExpectedApprovalTaskVersion is > 0;

    private static bool ExactWithdrawalFacts(
        GlobalProductIdentityWorkflowOperation operation,
        Guid commandId,
        string fingerprint,
        Guid requesterSubjectId,
        int expectedVersion,
        string reasonCode,
        string? comment) =>
        operation.WithdrawalCommandId == commandId
        && operation.WithdrawalRequesterSubjectId == requesterSubjectId
        && operation.WithdrawalExpectedProductVersion == expectedVersion
        && string.Equals(operation.WithdrawalFingerprint, fingerprint, StringComparison.Ordinal)
        && string.Equals(operation.WithdrawalReasonCode, reasonCode, StringComparison.Ordinal)
        && string.Equals(operation.WithdrawalComment, comment, StringComparison.Ordinal);

    private static string WithdrawalFingerprint(
        Guid tenantId,
        Guid productId,
        int expectedVersion,
        Guid commandId,
        Guid requesterSubjectId,
        string reasonCode,
        string? comment,
        ProductIdentityWorkflowBinding binding)
    {
        var canonical = string.Join('|', tenantId.ToString("D"), productId.ToString("D"), expectedVersion,
            commandId.ToString("D"), requesterSubjectId.ToString("D"), reasonCode, comment ?? string.Empty,
            binding.WorkflowInstanceId.ToString("D"), binding.ApprovalTaskId.ToString("D"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static bool TryRehydrateCancellationEvidence(
        GlobalProductIdentityWorkflowOperation operation,
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
            || !Guid.TryParse(operation.ObjectId, out var objectId))
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
        return string.Equals(evidence.TaskStatus, "Cancelled", StringComparison.Ordinal)
               && string.Equals(evidence.InstanceStatus, "Cancelled", StringComparison.Ordinal);
    }

    private static bool SameCancellation(
        PersistedCancellationEvidence left,
        PersistedCancellationEvidence right) =>
        left.WorkflowInstanceId == right.WorkflowInstanceId
        && left.ApprovalTaskId == right.ApprovalTaskId
        && left.TransitionLogId == right.TransitionLogId
        && left.TransitionSequence == right.TransitionSequence
        && left.RequesterSubjectId == right.RequesterSubjectId
        && left.CancelledAtUtc.EqualsExact(right.CancelledAtUtc)
        && string.Equals(left.IdempotencyKey, right.IdempotencyKey, StringComparison.Ordinal);

    private static bool ExactText(string? value, int maximum) =>
        value is { Length: > 0 } && value.Length <= maximum
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsControl);

    private static bool OptionalExactText(string? value, int maximum) =>
        value is null || ExactText(value, maximum);

    private static string BoundedCode(string? code, string fallback) =>
        !string.IsNullOrWhiteSpace(code) && code.Length <= 128 && code.All(character => !char.IsControl(character))
            ? code
            : fallback;

    private static GlobalProductIdentityWorkflowProcessingResult Success(
        GlobalProductIdentityWorkflowOperation operation,
        bool replay) => new(true, operation, null, 200, replay);

    private static GlobalProductIdentityWorkflowProcessingResult Fail(string errorCode, int statusCode) =>
        new(false, null, errorCode, statusCode, false);

    private static GlobalProductIdentityWorkflowProcessingResult Fail(
        GlobalProductIdentityWorkflowOperation operation,
        string errorCode,
        int statusCode) => new(false, operation, errorCode, statusCode, false);
}
