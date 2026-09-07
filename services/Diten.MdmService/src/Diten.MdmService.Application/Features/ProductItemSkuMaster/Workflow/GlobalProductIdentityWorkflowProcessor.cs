using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using System.Security.Cryptography;
using System.Text;
using PersistedCancellationEvidence = Diten.MdmService.Domain.ValueObjects.ProductIdentityWorkflowCancellationEvidence;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record GlobalProductIdentityWorkflowProcessingResult(
    bool Succeeded,
    GlobalProductIdentityWorkflowOperation? Operation,
    string? ErrorCode,
    int StatusCode,
    bool IsReplay);

public sealed record ProductIdentityWorkflowExecutionConfiguration(
    TimeSpan LeaseDuration,
    TimeSpan RetryDelay);

public sealed class GlobalProductIdentityWorkflowProcessor(
    IGlobalProductIdentityWorkflowOperationRepository operations,
    IGlobalProductRepository products,
    IProductIdentityWorkflowClient workflowClient,
    ProductIdentityWorkflowStartRequestFactory requestFactory,
    TimeProvider timeProvider)
{
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

        var existing = await operations.GetByOperationIdAsync(operationId, cancellationToken);
        GlobalProductIdentityWorkflowOperation operation;
        var replay = existing is not null;
        if (existing is null)
        {
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
                return Fail(reserved.ErrorCode ?? "PRODUCT_IDENTITY_WORKFLOW_OPERATION_CONFLICT", 409);
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
        CancellationToken cancellationToken = default) =>
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
            return Fail("PRODUCT_IDENTITY_WITHDRAWAL_OPERATION_CONFLICT", 409);
        }
        if (operation.WithdrawalCommandId.HasValue
            && operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.Completed)
        {
            if (product.Version == expectedVersion + 1
                && product.LifecycleStatus == ProductIdentityLifecycleStatus.Draft
                && product.WorkflowBinding.CancellationEvidence is { } persisted
                && string.Equals(persisted.IdempotencyKey, commandId.ToString("D"), StringComparison.Ordinal)
                && product.AuditIntents.Count(x =>
                    x.Operation == ProductAuditOperation.GlobalProductIdentityApprovalWithdrawn
                    && x.IdempotencyKey == persisted.IdempotencyKey) == 1)
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
        for (var step = 0; step < 10; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision
                && operation.WithdrawalCommandId.HasValue
                && (!commandId.HasValue || operation.LastFailureCode is not null))
                return Fail(operation, operation.LastFailureCode ??
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
            var claim = await operations.TryClaimAsync(new(operation.OperationId, operation.OperationFingerprint,
                [operation.Checkpoint], leaseOwner, now.UtcTicks, now.Add(leaseDuration).UtcTicks), cancellationToken);
            if (claim is null) return Fail(operation, "PRODUCT_IDENTITY_WORKFLOW_BUSY", 409);

            bool advanced;
            switch (operation.Checkpoint)
            {
                case GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision:
                    if (!commandId.HasValue || fingerprint is null || !requesterSubjectId.HasValue
                        || !expectedVersion.HasValue || reasonCode is null)
                    {
                        advanced = await AdvanceAsync(claim, operation.Checkpoint,
                            ProductIdentityWorkflowRecoveryDisposition.None, now,
                            "PRODUCT_IDENTITY_WORKFLOW_DECISION_PENDING", true, cancellationToken);
                        break;
                    }
                    advanced = await operations.AdvanceAsync(claim, new(
                        GlobalProductIdentityWorkflowCheckpoint.WithdrawalRequested,
                        ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
                        WithdrawalCommandId: commandId,
                        WithdrawalFingerprint: fingerprint,
                        WithdrawalRequesterSubjectId: requesterSubjectId,
                        WithdrawalExpectedProductVersion: expectedVersion,
                        WithdrawalReasonCode: reasonCode,
                        WithdrawalComment: comment,
                        ReleaseLease: true), cancellationToken);
                    break;
                case GlobalProductIdentityWorkflowCheckpoint.WithdrawalRequested:
                    advanced = await ObserveWithdrawalPreflightAsync(operation, claim, now, retryDelay,
                        cancellationToken);
                    break;
                case GlobalProductIdentityWorkflowCheckpoint.WithdrawalPreflightObserved:
                case GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown:
                case GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay:
                    advanced = await ObserveWithdrawalAsync(operation, claim, delegatedUserToken, now,
                        retryDelay, cancellationToken);
                    break;
                case GlobalProductIdentityWorkflowCheckpoint.WithdrawalObserved:
                    advanced = await ApplyWithdrawalAsync(operation, claim, now, cancellationToken);
                    break;
                case GlobalProductIdentityWorkflowCheckpoint.WithdrawalApplied:
                    advanced = await VerifyWithdrawalAndCompleteAsync(operation, claim, now, cancellationToken);
                    break;
                default:
                    return Fail(operation, "PRODUCT_IDENTITY_WORKFLOW_STATE_CONFLICT", 409);
            }
            if (!advanced) return Fail(operation, "PRODUCT_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT", 409);
            operation = await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken)
                ?? throw new InvalidOperationException("PRODUCT_IDENTITY_WORKFLOW_OPERATION_LOST");
            if (operation.NextAttemptAtUtcTicksV1 > timeProvider.GetUtcNow().UtcTicks)
                return Fail(operation, operation.LastFailureCode ?? "PRODUCT_IDENTITY_WORKFLOW_RETRY_SCHEDULED", 503);
            if (operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay
                && string.IsNullOrWhiteSpace(delegatedUserToken))
                return Fail(operation, "PRODUCT_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED", 409);
        }
        return Fail(operation, "PRODUCT_IDENTITY_WORKFLOW_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<bool> ObserveWithdrawalPreflightAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
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
            return await operations.AdvanceAsync(claim, new(
                GlobalProductIdentityWorkflowCheckpoint.WithdrawalPreflightObserved,
                ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
                WithdrawalExpectedWorkflowInstanceVersion: value.WorkflowInstanceVersion,
                WithdrawalExpectedApprovalTaskVersion: value.ApprovalTaskVersion,
                WithdrawalObjectRef: value.ObjectRef,
                ReleaseLease: true), cancellationToken);
        }
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.Conflict
            or ProductIdentityWorkflowTransportOutcome.NotFound)
            return await AdvanceAsync(claim, GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision,
                ProductIdentityWorkflowRecoveryDisposition.None, now, result.ErrorCode, true, cancellationToken);
        if (Retryable(result.Outcome))
            return await ScheduleRetryAsync(claim, operation.Checkpoint, result.ErrorCode, now, retryDelay,
                cancellationToken);
        return await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
    }

    private async Task<bool> ObserveWithdrawalAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        string? delegatedUserToken,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(delegatedUserToken))
            return await AdvanceAsync(claim, GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, now,
                "PRODUCT_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED", true, cancellationToken);
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
                ReleaseLease: true), cancellationToken);
        }
        if (Retryable(result.Outcome))
            return await ScheduleRetryAsync(claim, GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown,
                result.ErrorCode, now, retryDelay, cancellationToken);
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.Conflict
            or ProductIdentityWorkflowTransportOutcome.NotFound)
            return await AdvanceAsync(claim, GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision,
                ProductIdentityWorkflowRecoveryDisposition.None, now, result.ErrorCode, true, cancellationToken);
        return await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
    }

    private async Task<bool> ApplyWithdrawalAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        DateTimeOffset now,
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
        if (!result.Succeeded)
            return await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
        return await AdvanceAsync(claim, GlobalProductIdentityWorkflowCheckpoint.WithdrawalApplied,
            ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true,
            cancellationToken: cancellationToken);
    }

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
            || product.AuditIntents.Count(x => x.Operation == ProductAuditOperation.GlobalProductIdentityApprovalWithdrawn
                && x.IdempotencyKey == evidence.IdempotencyKey) != 1)
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
        for (var step = 0; step < 12; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.Completed)
            {
                return Success(operation, true);
            }
            if (operation.Checkpoint is GlobalProductIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart
                or GlobalProductIdentityWorkflowCheckpoint.Superseded)
            {
                return Success(operation, true);
            }
            if (stopWhenAwaitingDecision
                && operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision)
            {
                return Success(operation, false);
            }
            if (operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay
                && string.IsNullOrWhiteSpace(delegatedUserToken))
            {
                return Fail(operation, "PRODUCT_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED", 409);
            }
            if (operation.Checkpoint == GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired)
            {
                return Fail(operation, "PRODUCT_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED", 409);
            }

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
                return Fail(operation, "PRODUCT_IDENTITY_WORKFLOW_BUSY", 409);
            }

            var advanced = operation.Checkpoint switch
            {
                GlobalProductIdentityWorkflowCheckpoint.Prepared =>
                    await AdvanceAsync(claim, GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                        ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true,
                        cancellationToken: cancellationToken),
                GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown or
                    GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay =>
                    await ResolveStartAsync(operation, claim, delegatedUserToken, now, retryDelay, cancellationToken),
                GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted =>
                    await ApplyLocalPendingAsync(operation, claim, now, retryDelay, cancellationToken),
                GlobalProductIdentityWorkflowCheckpoint.LocalPendingApplied =>
                    await AdvanceAsync(claim, GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision,
                        ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true,
                        cancellationToken: cancellationToken),
                GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision =>
                    await ObserveDecisionAsync(operation, claim, now, retryDelay, cancellationToken),
                GlobalProductIdentityWorkflowCheckpoint.DecisionObserved =>
                    await ApplyDecisionAsync(operation, claim, now, retryDelay, cancellationToken),
                GlobalProductIdentityWorkflowCheckpoint.DecisionApplied =>
                    await VerifyDecisionAppliedAndCompleteAsync(operation, claim, now, cancellationToken),
                _ => false
            };
            if (!advanced)
            {
                return Fail(operation, "PRODUCT_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT", 409);
            }

            operation = await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken)
                ?? throw new InvalidOperationException("PRODUCT_IDENTITY_WORKFLOW_OPERATION_LOST");
            if (operation.NextAttemptAtUtcTicksV1 > timeProvider.GetUtcNow().UtcTicks)
            {
                return Fail(operation, operation.LastFailureCode ?? "PRODUCT_IDENTITY_WORKFLOW_RETRY_SCHEDULED", 503);
            }
        }

        return Fail(operation, "PRODUCT_IDENTITY_WORKFLOW_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<bool> ResolveStartAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
        string? delegatedUserToken,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult> outcome;
        if (!string.IsNullOrWhiteSpace(delegatedUserToken))
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
            if (outcome.Outcome is ProductIdentityWorkflowTransportOutcome.Forbidden
                or ProductIdentityWorkflowTransportOutcome.Invalid
                or ProductIdentityWorkflowTransportOutcome.Conflict
                or ProductIdentityWorkflowTransportOutcome.AuthenticationRejected)
            {
                return await QuarantineAsync(claim, outcome.ErrorCode, now, cancellationToken);
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
                GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay,
                now,
                lookup.ErrorCode ?? "PRODUCT_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED",
                releaseLease: true,
                cancellationToken: cancellationToken);
        }

        if (Retryable(lookup.Outcome))
        {
            return await ScheduleRetryAsync(
                claim, GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                lookup.ErrorCode, now, retryDelay, cancellationToken);
        }

        return await QuarantineAsync(claim, lookup.ErrorCode, now, cancellationToken);
    }

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
                ReleaseLease: true),
            cancellationToken);
    }

    private async Task<bool> ApplyLocalPendingAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
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
            return result.ErrorCode == "AUDIT_INTENT_CAPACITY_EXCEEDED"
                ? await ScheduleRetryAsync(claim, operation.Checkpoint, result.ErrorCode, now, retryDelay,
                    cancellationToken)
                : await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
        }
        if (result.GlobalProduct is null
            || result.GlobalProduct.Version != operation.ExpectedProductVersion + 1
            || result.GlobalProduct.LifecycleStatus != ProductIdentityLifecycleStatus.PendingIdentityApproval
            || !ExactBinding(result.GlobalProduct.WorkflowBinding, binding)
            || result.GlobalProduct.AuditIntents.Count(intent =>
                intent.Operation == ProductAuditOperation.GlobalProductIdentitySubmitted
                && string.Equals(intent.IdempotencyKey, operation.StartIdempotencyKey,
                    StringComparison.Ordinal)) != 1)
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
            or ProductIdentityWorkflowTransportOutcome.Incomplete
            || Retryable(result.Outcome))
        {
            return await ScheduleRetryAsync(
                claim,
                GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision,
                result.ErrorCode ?? "PRODUCT_IDENTITY_WORKFLOW_DECISION_PENDING",
                now,
                retryDelay,
                cancellationToken);
        }
        if (result.Outcome != ProductIdentityWorkflowTransportOutcome.Success || result.Value is null)
        {
            return await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
        }

        var evidence = result.Value;
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
            || actorId == operation.MakerSubjectId
            || evidence.TransitionSequence <= 0
            || evidence.DecisionAt.Offset != TimeSpan.Zero)
        {
            return await QuarantineAsync(claim, "PRODUCT_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
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
                ReleaseLease: true),
            cancellationToken);
    }

    private async Task<bool> ApplyDecisionAsync(
        GlobalProductIdentityWorkflowOperation operation,
        GlobalProductIdentityWorkflowClaim claim,
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
            return result.ErrorCode == "AUDIT_INTENT_CAPACITY_EXCEEDED"
                ? await ScheduleRetryAsync(claim, operation.Checkpoint, result.ErrorCode, now, retryDelay,
                    cancellationToken)
                : await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
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
        CancellationToken cancellationToken) =>
        operations.AdvanceAsync(
            claim,
            new(
                checkpoint,
                ProductIdentityWorkflowRecoveryDisposition.Retryable,
                now.UtcTicks,
                now.Add(retryDelay).UtcTicks,
                BoundedCode(errorCode, "PRODUCT_IDENTITY_WORKFLOW_PROVIDER_UNAVAILABLE"),
                ReleaseLease: true),
            cancellationToken);

    private Task<bool> QuarantineAsync(
        GlobalProductIdentityWorkflowClaim claim,
        string? errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        AdvanceAsync(
            claim,
            GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired,
            now,
            BoundedCode(errorCode, "PRODUCT_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED"),
            releaseLease: true,
            cancellationToken: cancellationToken);

    private Task<bool> AdvanceAsync(
        GlobalProductIdentityWorkflowClaim claim,
        GlobalProductIdentityWorkflowCheckpoint checkpoint,
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
        return product.AuditIntents.Count(intent => intent.Operation == expectedOperation
            && string.Equals(intent.IdempotencyKey, expectedKey, StringComparison.Ordinal)) == 1;
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

    private static bool Retryable(ProductIdentityWorkflowTransportOutcome outcome) =>
        outcome is ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Timeout;

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
