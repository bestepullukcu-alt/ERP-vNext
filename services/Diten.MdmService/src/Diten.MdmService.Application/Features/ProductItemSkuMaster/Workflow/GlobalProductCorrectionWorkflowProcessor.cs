using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record GlobalProductCorrectionProcessingResult(
    bool Succeeded,
    GlobalProductCorrectionOperation? Operation,
    string? ErrorCode,
    int StatusCode,
    bool IsReplay);

public sealed record GlobalProductCorrectionExecutionConfiguration(
    TimeSpan LeaseDuration,
    TimeSpan RetryDelay);

public sealed class GlobalProductCorrectionWorkflowProcessor(
    IGlobalProductCorrectionOperationRepository operations,
    IGlobalProductRepository products,
    IProductIdentityWorkflowClient workflowClient,
    GlobalProductCorrectionWorkflowStartRequestFactory requestFactory,
    TimeProvider timeProvider)
{
    public async Task<GlobalProductCorrectionProcessingResult> StartInteractiveAsync(
        Guid tenantId,
        Guid productId,
        int expectedVersion,
        Guid operationId,
        Guid makerSubjectId,
        string proposedName,
        string delegatedUserToken,
        GlobalProductCorrectionExecutionConfiguration execution,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || productId == Guid.Empty || operationId == Guid.Empty
            || makerSubjectId == Guid.Empty || expectedVersion < 0
            || string.IsNullOrWhiteSpace(delegatedUserToken))
            return Fail("GLOBAL_PRODUCT_CORRECTION_REQUEST_INVALID", 400);
        var existing = await operations.GetByOperationIdAsync(operationId, cancellationToken);
        GlobalProductCorrectionOperation operation;
        var replay = existing is not null;
        if (existing is null)
        {
            var product = await products.GetByIdAsync(productId, cancellationToken);
            if (product is null) return Fail("PRODUCT_IDENTITY_NOT_FOUND", 404);
            if (product.Version != expectedVersion) return Fail("GLOBAL_PRODUCT_CONCURRENCY_CONFLICT", 409);
            GlobalProductCorrectionStartPlan plan;
            try { plan = requestFactory.Create(product, operationId, makerSubjectId, proposedName); }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message, exception.Message.Contains("CONFIGURATION", StringComparison.Ordinal)
                    ? 503 : 409);
            }
            if (await products.NameExistsAsync(plan.Operation.ProposedGlobalProductNameNormalized, cancellationToken))
                return Fail("GLOBAL_PRODUCT_CORRECTION_NAME_CONFLICT", 409);
            var reserve = await operations.ReserveAsync(plan.Operation, cancellationToken);
            if (!reserve.Succeeded || reserve.Operation is null)
                return Fail(reserve.ErrorCode ?? "GLOBAL_PRODUCT_CORRECTION_OPERATION_CONFLICT", 409);
            operation = reserve.Operation;
            replay = reserve.IsReplay;
        }
        else operation = existing;
        if (operation.TenantId != tenantId || operation.GlobalProductId != productId
            || operation.BaseProductVersion != expectedVersion || operation.MakerSubjectId != makerSubjectId
            || operation.ProposedGlobalProductName != GlobalProductNameRules.CleanVisible(proposedName)
            || !requestFactory.MatchesConfiguration(operation))
            return Fail("GLOBAL_PRODUCT_CORRECTION_OPERATION_CONFLICT", 409);
        var result = await ProcessAsync(operation, delegatedUserToken, $"interactive-{makerSubjectId:N}",
            execution, true, cancellationToken);
        return result with { IsReplay = replay || result.IsReplay };
    }

    public Task<GlobalProductCorrectionProcessingResult> RecoverAsync(
        GlobalProductCorrectionOperation operation,
        string leaseOwner,
        GlobalProductCorrectionExecutionConfiguration execution,
        CancellationToken cancellationToken) =>
        ProcessAsync(operation, null, leaseOwner, execution, false, cancellationToken);

    private async Task<GlobalProductCorrectionProcessingResult> ProcessAsync(
        GlobalProductCorrectionOperation initial,
        string? delegatedToken,
        string leaseOwner,
        GlobalProductCorrectionExecutionConfiguration execution,
        bool stopAtAwaitingDecision,
        CancellationToken cancellationToken)
    {
        var operation = initial;
        for (var step = 0; step < 9; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation.Checkpoint == GlobalProductCorrectionCheckpoint.Completed)
                return Success(operation, true);
            if (operation.Checkpoint == GlobalProductCorrectionCheckpoint.ManualReconciliationRequired)
                return Fail(operation, operation.LastFailureCode ??
                    "GLOBAL_PRODUCT_CORRECTION_RECONCILIATION_REQUIRED", 409);
            if (operation.Checkpoint == GlobalProductCorrectionCheckpoint.AwaitingMakerReplay
                && string.IsNullOrWhiteSpace(delegatedToken))
                return Fail(operation, "GLOBAL_PRODUCT_CORRECTION_MAKER_REPLAY_REQUIRED", 409);
            if (stopAtAwaitingDecision && operation.Checkpoint == GlobalProductCorrectionCheckpoint.AwaitingDecision)
                return Success(operation, false);
            var now = timeProvider.GetUtcNow();
            var claim = await operations.TryClaimAsync(operation.OperationId, operation.OperationFingerprint,
                [operation.Checkpoint], leaseOwner, now.UtcTicks, now.Add(execution.LeaseDuration).UtcTicks,
                cancellationToken);
            if (claim is null) return Fail(operation, "GLOBAL_PRODUCT_CORRECTION_BUSY", 409);
            var advanced = operation.Checkpoint switch
            {
                GlobalProductCorrectionCheckpoint.Prepared => await PrepareAndStartAsync(
                    operation, claim, delegatedToken, now, execution.RetryDelay, cancellationToken),
                GlobalProductCorrectionCheckpoint.StartOutcomeUnknown
                    or GlobalProductCorrectionCheckpoint.AwaitingMakerReplay => await RecoverStartAsync(
                        operation, claim, delegatedToken, now, execution.RetryDelay, cancellationToken),
                GlobalProductCorrectionCheckpoint.WorkflowStarted => await AdvanceAsync(claim,
                    GlobalProductCorrectionCheckpoint.AwaitingDecision, null, now, true, cancellationToken),
                GlobalProductCorrectionCheckpoint.AwaitingDecision => await ObserveDecisionAsync(
                    operation, claim, now, execution.RetryDelay, cancellationToken),
                GlobalProductCorrectionCheckpoint.DecisionObserved => await ApplyDecisionAsync(
                    operation, claim, now, execution.RetryDelay, cancellationToken),
                GlobalProductCorrectionCheckpoint.DecisionApplied => await AdvanceAsync(claim,
                    GlobalProductCorrectionCheckpoint.Completed, null, now, true, cancellationToken),
                _ => false
            };
            if (!advanced) return Fail(operation, "GLOBAL_PRODUCT_CORRECTION_CONCURRENCY_CONFLICT", 409);
            operation = await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken)
                ?? throw new InvalidOperationException("GLOBAL_PRODUCT_CORRECTION_OPERATION_LOST");
            if (operation.NextAttemptAtUtcTicksV1 > timeProvider.GetUtcNow().UtcTicks)
                return Fail(operation, operation.LastFailureCode ?? "GLOBAL_PRODUCT_CORRECTION_RETRY_SCHEDULED", 503);
        }
        return Fail(operation, "GLOBAL_PRODUCT_CORRECTION_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<bool> PrepareAndStartAsync(
        GlobalProductCorrectionOperation operation,
        GlobalProductCorrectionClaim claim,
        string? token,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(operation.GlobalProductId, cancellationToken);
        if (product is null) return await ManualAsync(claim, "PRODUCT_IDENTITY_NOT_FOUND", now, cancellationToken);
        var binding = Binding(operation);
        var audit = GlobalProductCorrectionAuditIntentFactory.Create(product, operation.BaseProductVersion,
            operation.OperationId, operation.MakerSubjectId,
            ProductAuditOperation.GlobalProductCorrectionRequested,
            operation.ProposedGlobalProductName, now);
        var admitted = await products.AcquireLifecycleOperationAsync(product.Id, operation.BaseProductVersion,
            binding, audit, cancellationToken);
        if (!admitted.Succeeded)
            return await ManualAsync(claim, admitted.ErrorCode, now, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            return await operations.AdvanceAsync(claim, new(
                GlobalProductCorrectionCheckpoint.AwaitingMakerReplay,
                ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, now.UtcTicks,
                LastFailureCode: "GLOBAL_PRODUCT_CORRECTION_MAKER_REPLAY_REQUIRED", ReleaseLease: true),
                cancellationToken);
        var result = await workflowClient.StartAsync(operation.TenantId,
            requestFactory.Rehydrate(operation), token, cancellationToken);
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.Success && result.Value is { } value)
            return await RecordStartAsync(operation, claim, value, now, cancellationToken);
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.Timeout
            or ProductIdentityWorkflowTransportOutcome.Retryable)
            return await operations.AdvanceAsync(claim, new(
                GlobalProductCorrectionCheckpoint.StartOutcomeUnknown,
                ProductIdentityWorkflowRecoveryDisposition.Retryable, now.UtcTicks,
                now.Add(retryDelay).UtcTicks, result.ErrorCode, ReleaseLease: true), cancellationToken);
        return await ManualAsync(claim, result.ErrorCode, now, cancellationToken);
    }

    private async Task<bool> RecoverStartAsync(
        GlobalProductCorrectionOperation operation,
        GlobalProductCorrectionClaim claim,
        string? token,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        var result = await workflowClient.GetStartResultAsync(operation.TenantId,
            new(operation.ObjectType, operation.ObjectId, operation.MakerSubjectId,
                operation.StartIdempotencyKey), cancellationToken);
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.Success && result.Value is { } value)
            return await RecordStartAsync(operation, claim, value, now, cancellationToken);
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound
            && !string.IsNullOrWhiteSpace(token))
        {
            var start = await workflowClient.StartAsync(operation.TenantId,
                requestFactory.Rehydrate(operation), token, cancellationToken);
            if (start.Outcome == ProductIdentityWorkflowTransportOutcome.Success && start.Value is { } started)
                return await RecordStartAsync(operation, claim, started, now, cancellationToken);
            if (start.Outcome is ProductIdentityWorkflowTransportOutcome.Retryable
                or ProductIdentityWorkflowTransportOutcome.Timeout
                or ProductIdentityWorkflowTransportOutcome.Incomplete)
                return await operations.AdvanceAsync(claim, new(operation.Checkpoint,
                    ProductIdentityWorkflowRecoveryDisposition.Retryable, now.UtcTicks,
                    now.Add(retryDelay).UtcTicks, start.ErrorCode, ReleaseLease: true), cancellationToken);
            return await ManualAsync(claim, start.ErrorCode, now, cancellationToken);
        }
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.NotFound
            or ProductIdentityWorkflowTransportOutcome.Incomplete
            or ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Timeout)
            return await operations.AdvanceAsync(claim, new(operation.Checkpoint,
                operation.RecoveryDisposition, now.UtcTicks, now.Add(retryDelay).UtcTicks,
                result.ErrorCode, ReleaseLease: true), cancellationToken);
        return await ManualAsync(claim, result.ErrorCode, now, cancellationToken);
    }

    private async Task<bool> RecordStartAsync(
        GlobalProductCorrectionOperation operation,
        GlobalProductCorrectionClaim claim,
        ProductIdentityWorkflowStartResult value,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (value.WorkflowInstanceId == Guid.Empty || value.TemplateId == Guid.Empty
            || value.TemplateVersionId == Guid.Empty || value.ApprovalTaskId == Guid.Empty
            || value.AssignmentSnapshotId == Guid.Empty || value.StartTransitionLogId == Guid.Empty
            || operation.WorkflowTemplateId.HasValue && value.TemplateId != operation.WorkflowTemplateId
            || !string.Equals(value.ObjectRef, operation.ObjectRef, StringComparison.Ordinal))
            return await ManualAsync(claim, "GLOBAL_PRODUCT_CORRECTION_START_EVIDENCE_INVALID", now,
                cancellationToken);
        return await operations.AdvanceAsync(claim, new(
            GlobalProductCorrectionCheckpoint.WorkflowStarted,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
            WorkflowInstanceId: value.WorkflowInstanceId, WorkflowTemplateId: value.TemplateId,
            WorkflowTemplateVersionId: value.TemplateVersionId, ApprovalTaskId: value.ApprovalTaskId,
            AssignmentSnapshotId: value.AssignmentSnapshotId, StartTransitionLogId: value.StartTransitionLogId,
            WorkflowStartedAtUtcTicksV1: (value.StartedAt ?? now).UtcTicks, ReleaseLease: true),
            cancellationToken);
    }

    private async Task<bool> ObserveDecisionAsync(
        GlobalProductCorrectionOperation operation,
        GlobalProductCorrectionClaim claim,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!operation.WorkflowInstanceId.HasValue)
            return await ManualAsync(claim, "GLOBAL_PRODUCT_CORRECTION_START_EVIDENCE_INVALID", now,
                cancellationToken);
        var result = await workflowClient.GetTerminalEvidenceAsync(operation.TenantId,
            new(operation.WorkflowInstanceId.Value, operation.ObjectType, operation.ObjectId), cancellationToken);
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.NonTerminal
            or ProductIdentityWorkflowTransportOutcome.NotFound
            or ProductIdentityWorkflowTransportOutcome.Incomplete
            or ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Timeout)
            return await operations.AdvanceAsync(claim, new(operation.Checkpoint,
                operation.RecoveryDisposition, now.UtcTicks, now.Add(retryDelay).UtcTicks,
                result.ErrorCode ?? "GLOBAL_PRODUCT_CORRECTION_DECISION_PENDING", ReleaseLease: true),
                cancellationToken);
        if (result.Outcome != ProductIdentityWorkflowTransportOutcome.Success || result.Value is null)
            return await ManualAsync(claim, result.ErrorCode, now, cancellationToken);
        var evidence = result.Value;
        var isApproved = evidence.TerminalAction == "Approve" && evidence.TaskStatus == "Approved"
            && evidence.InstanceStatus == "Completed";
        var isRejected = evidence.TerminalAction == "Reject" && evidence.TaskStatus == "Rejected"
            && evidence.InstanceStatus == "Rejected";
        if ((!isApproved && !isRejected)
            || evidence.WorkflowInstanceId != operation.WorkflowInstanceId
            || evidence.ApprovalTaskId != operation.ApprovalTaskId
            || evidence.TemplateId != operation.WorkflowTemplateId
            || evidence.TemplateVersionId != operation.WorkflowTemplateVersionId
            || evidence.ObjectType != operation.ObjectType || evidence.ObjectId != operation.ObjectId
            || evidence.ObjectRef != operation.ObjectRef
            || !Guid.TryParse(evidence.ActorUserId, out var actor) || actor == Guid.Empty
            || actor == operation.MakerSubjectId
            || evidence.TransitionSequence <= 0 || evidence.DecisionAt.Offset != TimeSpan.Zero)
            return await ManualAsync(claim, "GLOBAL_PRODUCT_CORRECTION_EVIDENCE_CONFLICT", now,
                cancellationToken);
        return await operations.AdvanceAsync(claim, new(
            GlobalProductCorrectionCheckpoint.DecisionObserved,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
            DecisionKind: isApproved ? ProductIdentityDecisionKind.Approved : ProductIdentityDecisionKind.Rejected,
            DecisionActorSubjectId: actor, DecisionReasonCode: evidence.ReasonCode,
            DecisionAtUtcTicksV1: evidence.DecisionAt.UtcTicks,
            DecisionTransitionSequence: evidence.TransitionSequence,
            DecisionTaskStatus: evidence.TaskStatus, DecisionInstanceStatus: evidence.InstanceStatus,
            ReleaseLease: true), cancellationToken);
    }

    private async Task<bool> ApplyDecisionAsync(
        GlobalProductCorrectionOperation operation,
        GlobalProductCorrectionClaim claim,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(operation.GlobalProductId, cancellationToken);
        if (product is null || !operation.DecisionActorSubjectId.HasValue)
            return await ManualAsync(claim, "GLOBAL_PRODUCT_CORRECTION_PRODUCT_DRIFT", now, cancellationToken);
        var expectedVersion = checked(operation.BaseProductVersion + 1);
        var binding = Binding(operation);
        var approved = operation.DecisionKind == ProductIdentityDecisionKind.Approved;
        if (approved && await products.NameExistsOtherThanAsync(
                operation.ProposedGlobalProductNameNormalized, operation.GlobalProductId, cancellationToken))
        {
            var conflictAudit = GlobalProductCorrectionAuditIntentFactory.Create(product, expectedVersion,
                operation.OperationId, operation.DecisionActorSubjectId.Value,
                ProductAuditOperation.GlobalProductCorrectionManualReconciliationRequired,
                operation.ProposedGlobalProductName, now);
            var conflict = await products.RecordCorrectionConflictAsync(product.Id, expectedVersion, binding,
                conflictAudit, cancellationToken);
            if (!conflict.Succeeded)
                return await operations.AdvanceAsync(claim, new(operation.Checkpoint,
                    ProductIdentityWorkflowRecoveryDisposition.Retryable, now.UtcTicks,
                    now.Add(retryDelay).UtcTicks,
                    "GLOBAL_PRODUCT_CORRECTION_MANUAL_AUDIT_NOT_PERSISTED", ReleaseLease: true),
                    cancellationToken);
            return await ManualAsync(claim, "GLOBAL_PRODUCT_CORRECTION_NAME_CONFLICT", now,
                cancellationToken);
        }
        var auditOperation = approved ? ProductAuditOperation.GlobalProductCorrectionApplied
            : ProductAuditOperation.GlobalProductCorrectionRejected;
        var audit = GlobalProductCorrectionAuditIntentFactory.Create(product, expectedVersion,
            operation.OperationId, operation.DecisionActorSubjectId.Value, auditOperation,
            operation.ProposedGlobalProductName, now);
        var result = await products.ApplyCorrectionDecisionAsync(product.Id, expectedVersion, binding,
            approved ? operation.ProposedGlobalProductName : null,
            approved ? operation.ProposedGlobalProductNameNormalized : null,
            audit, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.ErrorCode == "AUDIT_INTENT_CAPACITY_EXCEEDED")
                return await operations.AdvanceAsync(claim, new(operation.Checkpoint,
                    operation.RecoveryDisposition, now.UtcTicks, now.Add(retryDelay).UtcTicks,
                    result.ErrorCode, ReleaseLease: true), cancellationToken);
            if (result.ErrorCode == "GLOBAL_PRODUCT_CORRECTION_NAME_CONFLICT")
            {
                var conflictAudit = GlobalProductCorrectionAuditIntentFactory.Create(product, expectedVersion,
                    operation.OperationId, operation.DecisionActorSubjectId.Value,
                    ProductAuditOperation.GlobalProductCorrectionManualReconciliationRequired,
                    operation.ProposedGlobalProductName, now);
                var conflict = await products.RecordCorrectionConflictAsync(product.Id, expectedVersion,
                    binding, conflictAudit, cancellationToken);
                if (!conflict.Succeeded)
                    return await operations.AdvanceAsync(claim, new(operation.Checkpoint,
                        ProductIdentityWorkflowRecoveryDisposition.Retryable, now.UtcTicks,
                        now.Add(retryDelay).UtcTicks,
                        "GLOBAL_PRODUCT_CORRECTION_MANUAL_AUDIT_NOT_PERSISTED", ReleaseLease: true),
                        cancellationToken);
                return await ManualAsync(claim, result.ErrorCode, now, cancellationToken);
            }
            return await ManualAsync(claim, result.ErrorCode, now, cancellationToken);
        }
        return await AdvanceAsync(claim, GlobalProductCorrectionCheckpoint.DecisionApplied, null,
            now, true, cancellationToken);
    }

    private static GlobalProductActiveLifecycleOperationBinding Binding(GlobalProductCorrectionOperation operation) =>
        new(GlobalProductLifecycleOperationKind.Correction, operation.OperationId, operation.BaseProductVersion);

    private Task<bool> ManualAsync(GlobalProductCorrectionClaim claim, string? code,
        DateTimeOffset now, CancellationToken cancellationToken) =>
        operations.AdvanceAsync(claim, new(GlobalProductCorrectionCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, now.UtcTicks,
            LastFailureCode: code ?? "GLOBAL_PRODUCT_CORRECTION_RECONCILIATION_REQUIRED",
            ReleaseLease: true), cancellationToken);

    private Task<bool> AdvanceAsync(GlobalProductCorrectionClaim claim,
        GlobalProductCorrectionCheckpoint checkpoint, string? error, DateTimeOffset now, bool release,
        CancellationToken cancellationToken) => operations.AdvanceAsync(claim, new(checkpoint,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks, LastFailureCode: error,
            ReleaseLease: release), cancellationToken);

    private static GlobalProductCorrectionProcessingResult Success(
        GlobalProductCorrectionOperation operation, bool replay) => new(true, operation, null,
        operation.Checkpoint == GlobalProductCorrectionCheckpoint.Completed ? 200 : 202, replay);
    private static GlobalProductCorrectionProcessingResult Fail(string code, int status) =>
        new(false, null, code, status, false);
    private static GlobalProductCorrectionProcessingResult Fail(
        GlobalProductCorrectionOperation operation, string code, int status) =>
        new(false, operation, code, status, false);
}
