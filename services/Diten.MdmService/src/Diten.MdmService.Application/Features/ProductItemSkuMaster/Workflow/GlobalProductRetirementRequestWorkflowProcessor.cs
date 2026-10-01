using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record GlobalProductRetirementRequestProcessingResult(bool Succeeded,
    GlobalProductRetirementRequestOperation? Operation, string? ErrorCode, int StatusCode, bool IsReplay);
public sealed record GlobalProductRetirementRequestExecutionConfiguration(TimeSpan LeaseDuration, TimeSpan RetryDelay);

public sealed class GlobalProductRetirementRequestWorkflowProcessor(
    IGlobalProductRetirementRequestOperationRepository operations,
    IGlobalProductRepository products,
    IProductIdentityWorkflowClient workflowClient,
    GlobalProductRetirementRequestWorkflowStartRequestFactory requestFactory,
    TimeProvider timeProvider)
{
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
        var result = await ProcessAsync(operation, delegatedToken, $"interactive-{makerSubjectId:N}",
            execution, true, cancellationToken);
        return result with { IsReplay = replay || result.IsReplay };
    }

    public Task<GlobalProductRetirementRequestProcessingResult> RecoverAsync(
        GlobalProductRetirementRequestOperation operation, string leaseOwner,
        GlobalProductRetirementRequestExecutionConfiguration execution, CancellationToken cancellationToken) =>
        ProcessAsync(operation, null, leaseOwner, execution, false, cancellationToken);

    private async Task<GlobalProductRetirementRequestProcessingResult> ProcessAsync(
        GlobalProductRetirementRequestOperation operation, string? token, string leaseOwner,
        GlobalProductRetirementRequestExecutionConfiguration execution, bool stopAtWaiting,
        CancellationToken cancellationToken)
    {
        for (var step = 0; step < 9; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation.Checkpoint == GlobalProductRetirementRequestCheckpoint.Completed)
                return Success(operation, true);
            if (operation.Checkpoint == GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired)
                return Fail(operation, operation.LastFailureCode ??
                    "GLOBAL_PRODUCT_RETIREMENT_RECONCILIATION_REQUIRED", 409);
            if (operation.Checkpoint == GlobalProductRetirementRequestCheckpoint.AwaitingMakerReplay
                && string.IsNullOrWhiteSpace(token))
                return Fail(operation, "GLOBAL_PRODUCT_RETIREMENT_MAKER_REPLAY_REQUIRED", 409);
            if (stopAtWaiting && operation.Checkpoint == GlobalProductRetirementRequestCheckpoint.AwaitingDecision)
                return Success(operation, false);
            var now = timeProvider.GetUtcNow();
            var claim = await operations.TryClaimAsync(operation.OperationId, operation.OperationFingerprint,
                [operation.Checkpoint], leaseOwner, now.UtcTicks, now.Add(execution.LeaseDuration).UtcTicks,
                cancellationToken);
            if (claim is null) return Fail(operation, "GLOBAL_PRODUCT_RETIREMENT_BUSY", 409);
            var advanced = operation.Checkpoint switch
            {
                GlobalProductRetirementRequestCheckpoint.Prepared => await StartAsync(operation, claim, token,
                    now, execution.RetryDelay, cancellationToken),
                GlobalProductRetirementRequestCheckpoint.StartOutcomeUnknown
                    or GlobalProductRetirementRequestCheckpoint.AwaitingMakerReplay => await RecoverStartAsync(
                        operation, claim, token, now, execution.RetryDelay, cancellationToken),
                GlobalProductRetirementRequestCheckpoint.WorkflowStarted => await AdvanceAsync(claim,
                    GlobalProductRetirementRequestCheckpoint.AwaitingDecision, now, cancellationToken),
                GlobalProductRetirementRequestCheckpoint.AwaitingDecision => await ObserveAsync(operation, claim,
                    now, execution.RetryDelay, cancellationToken),
                GlobalProductRetirementRequestCheckpoint.DecisionObserved => await ApplyAsync(operation, claim,
                    now, execution.RetryDelay, cancellationToken),
                GlobalProductRetirementRequestCheckpoint.DecisionApplied => await AdvanceAsync(claim,
                    GlobalProductRetirementRequestCheckpoint.Completed, now, cancellationToken),
                _ => false
            };
            if (!advanced) return Fail(operation, "GLOBAL_PRODUCT_RETIREMENT_CONCURRENCY_CONFLICT", 409);
            operation = await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken)
                ?? throw new InvalidOperationException("GLOBAL_PRODUCT_RETIREMENT_OPERATION_LOST");
            if (operation.NextAttemptAtUtcTicksV1 > timeProvider.GetUtcNow().UtcTicks)
                return Fail(operation, operation.LastFailureCode ?? "GLOBAL_PRODUCT_RETIREMENT_RETRY_SCHEDULED", 503);
        }
        return Fail(operation, "GLOBAL_PRODUCT_RETIREMENT_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<bool> StartAsync(GlobalProductRetirementRequestOperation operation,
        GlobalProductRetirementRequestClaim claim, string? token, DateTimeOffset now, TimeSpan retry,
        CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(operation.GlobalProductId, cancellationToken);
        if (product is null) return await ManualAsync(claim, "PRODUCT_IDENTITY_NOT_FOUND", now, cancellationToken);
        var binding = Binding(operation);
        var audit = GlobalProductRetirementRequestAuditIntentFactory.Create(product, operation.BaseProductVersion,
            operation.OperationId, operation.MakerSubjectId, ProductAuditOperation.GlobalProductRetirementRequested,
            operation.RequestReason, now);
        var admitted = await products.AcquireLifecycleOperationAsync(product.Id, operation.BaseProductVersion,
            binding, audit, cancellationToken);
        if (!admitted.Succeeded) return await ManualAsync(claim, admitted.ErrorCode, now, cancellationToken);
        if (string.IsNullOrWhiteSpace(token))
            return await operations.AdvanceAsync(claim, new(
                GlobalProductRetirementRequestCheckpoint.AwaitingMakerReplay,
                ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, now.UtcTicks,
                LastFailureCode: "GLOBAL_PRODUCT_RETIREMENT_MAKER_REPLAY_REQUIRED", ReleaseLease: true),
                cancellationToken);
        var result = await workflowClient.StartAsync(operation.TenantId, requestFactory.Rehydrate(operation),
            token, cancellationToken);
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.Success && result.Value is { } value)
            return await RecordStartAsync(operation, claim, value, now, cancellationToken);
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.Timeout
            or ProductIdentityWorkflowTransportOutcome.Retryable)
            return await operations.AdvanceAsync(claim, new(
                GlobalProductRetirementRequestCheckpoint.StartOutcomeUnknown,
                ProductIdentityWorkflowRecoveryDisposition.Retryable, now.UtcTicks,
                now.Add(retry).UtcTicks, result.ErrorCode, ReleaseLease: true), cancellationToken);
        return await ManualAsync(claim, result.ErrorCode, now, cancellationToken);
    }

    private async Task<bool> RecoverStartAsync(GlobalProductRetirementRequestOperation operation,
        GlobalProductRetirementRequestClaim claim, string? token, DateTimeOffset now, TimeSpan retry,
        CancellationToken cancellationToken)
    {
        var result = await workflowClient.GetStartResultAsync(operation.TenantId,
            new(operation.ObjectType, operation.ObjectId, operation.MakerSubjectId,
                operation.StartIdempotencyKey), cancellationToken);
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.Success && result.Value is { } value)
            return await RecordStartAsync(operation, claim, value, now, cancellationToken);
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound && !string.IsNullOrWhiteSpace(token))
        {
            var start = await workflowClient.StartAsync(operation.TenantId, requestFactory.Rehydrate(operation),
                token, cancellationToken);
            if (start.Outcome == ProductIdentityWorkflowTransportOutcome.Success && start.Value is { } started)
                return await RecordStartAsync(operation, claim, started, now, cancellationToken);
        }
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.NotFound
            or ProductIdentityWorkflowTransportOutcome.Incomplete or ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Timeout)
            return await operations.AdvanceAsync(claim, new(operation.Checkpoint, operation.RecoveryDisposition,
                now.UtcTicks, now.Add(retry).UtcTicks, result.ErrorCode, ReleaseLease: true), cancellationToken);
        return await ManualAsync(claim, result.ErrorCode, now, cancellationToken);
    }

    private async Task<bool> RecordStartAsync(GlobalProductRetirementRequestOperation operation,
        GlobalProductRetirementRequestClaim claim, ProductIdentityWorkflowStartResult value, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (value.WorkflowInstanceId == Guid.Empty || value.TemplateId == Guid.Empty
            || value.TemplateVersionId == Guid.Empty || value.ApprovalTaskId == Guid.Empty
            || value.AssignmentSnapshotId == Guid.Empty || value.StartTransitionLogId == Guid.Empty
            || operation.WorkflowTemplateId.HasValue && value.TemplateId != operation.WorkflowTemplateId
            || !string.Equals(value.ObjectRef, operation.ObjectRef, StringComparison.Ordinal))
            return await ManualAsync(claim, "GLOBAL_PRODUCT_RETIREMENT_START_EVIDENCE_INVALID", now, cancellationToken);
        return await operations.AdvanceAsync(claim, new(GlobalProductRetirementRequestCheckpoint.WorkflowStarted,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
            WorkflowInstanceId: value.WorkflowInstanceId, WorkflowTemplateId: value.TemplateId,
            WorkflowTemplateVersionId: value.TemplateVersionId, ApprovalTaskId: value.ApprovalTaskId,
            AssignmentSnapshotId: value.AssignmentSnapshotId, StartTransitionLogId: value.StartTransitionLogId,
            WorkflowStartedAtUtcTicksV1: (value.StartedAt ?? now).UtcTicks, ReleaseLease: true), cancellationToken);
    }

    private async Task<bool> ObserveAsync(GlobalProductRetirementRequestOperation operation,
        GlobalProductRetirementRequestClaim claim, DateTimeOffset now, TimeSpan retry,
        CancellationToken cancellationToken)
    {
        if (!operation.WorkflowInstanceId.HasValue)
            return await ManualAsync(claim, "GLOBAL_PRODUCT_RETIREMENT_START_EVIDENCE_INVALID", now, cancellationToken);
        var result = await workflowClient.GetTerminalEvidenceAsync(operation.TenantId,
            new(operation.WorkflowInstanceId.Value, operation.ObjectType, operation.ObjectId), cancellationToken);
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.NonTerminal
            or ProductIdentityWorkflowTransportOutcome.NotFound or ProductIdentityWorkflowTransportOutcome.Incomplete
            or ProductIdentityWorkflowTransportOutcome.Retryable or ProductIdentityWorkflowTransportOutcome.Timeout)
            return await operations.AdvanceAsync(claim, new(operation.Checkpoint, operation.RecoveryDisposition,
                now.UtcTicks, now.Add(retry).UtcTicks, result.ErrorCode ?? "GLOBAL_PRODUCT_RETIREMENT_DECISION_PENDING",
                ReleaseLease: true), cancellationToken);
        if (result.Outcome != ProductIdentityWorkflowTransportOutcome.Success || result.Value is null)
            return await ManualAsync(claim, result.ErrorCode, now, cancellationToken);
        var evidence = result.Value;
        var approved = evidence.TerminalAction == "Approve" && evidence.TaskStatus == "Approved"
            && evidence.InstanceStatus == "Completed";
        var rejected = evidence.TerminalAction == "Reject" && evidence.TaskStatus == "Rejected"
            && evidence.InstanceStatus == "Rejected";
        if ((!approved && !rejected) || evidence.WorkflowInstanceId != operation.WorkflowInstanceId
            || evidence.ApprovalTaskId != operation.ApprovalTaskId || evidence.TemplateId != operation.WorkflowTemplateId
            || evidence.TemplateVersionId != operation.WorkflowTemplateVersionId
            || evidence.ObjectType != operation.ObjectType || evidence.ObjectId != operation.ObjectId
            || evidence.ObjectRef != operation.ObjectRef || !Guid.TryParse(evidence.ActorUserId, out var actor)
            || actor == Guid.Empty || actor == operation.MakerSubjectId || evidence.TransitionSequence <= 0
            || evidence.DecisionAt.Offset != TimeSpan.Zero)
            return await ManualAsync(claim, "GLOBAL_PRODUCT_RETIREMENT_EVIDENCE_CONFLICT", now, cancellationToken);
        return await operations.AdvanceAsync(claim, new(GlobalProductRetirementRequestCheckpoint.DecisionObserved,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
            DecisionKind: approved ? ProductIdentityDecisionKind.Approved : ProductIdentityDecisionKind.Rejected,
            DecisionActorSubjectId: actor, DecisionReasonCode: evidence.ReasonCode,
            DecisionAtUtcTicksV1: evidence.DecisionAt.UtcTicks, DecisionTransitionSequence: evidence.TransitionSequence,
            DecisionTaskStatus: evidence.TaskStatus, DecisionInstanceStatus: evidence.InstanceStatus,
            ReleaseLease: true), cancellationToken);
    }

    private async Task<bool> ApplyAsync(GlobalProductRetirementRequestOperation operation,
        GlobalProductRetirementRequestClaim claim, DateTimeOffset now, TimeSpan retry,
        CancellationToken cancellationToken)
    {
        var product = await products.GetByIdAsync(operation.GlobalProductId, cancellationToken);
        if (product is null || !operation.DecisionActorSubjectId.HasValue)
            return await ManualAsync(claim, "GLOBAL_PRODUCT_RETIREMENT_PRODUCT_DRIFT", now, cancellationToken);
        var expectedVersion = checked(operation.BaseProductVersion + 1);
        var approved = operation.DecisionKind == ProductIdentityDecisionKind.Approved;
        var auditOperation = approved ? ProductAuditOperation.GlobalProductIdentityRetired
            : ProductAuditOperation.GlobalProductRetirementRejected;
        var audit = GlobalProductRetirementRequestAuditIntentFactory.Create(product, expectedVersion,
            operation.OperationId, operation.DecisionActorSubjectId.Value, auditOperation,
            operation.RequestReason, now);
        var result = await products.ApplyRetirementDecisionAsync(product.Id, expectedVersion, Binding(operation),
            approved, audit, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.ErrorCode == "AUDIT_INTENT_CAPACITY_EXCEEDED")
                return await operations.AdvanceAsync(claim, new(operation.Checkpoint, operation.RecoveryDisposition,
                    now.UtcTicks, now.Add(retry).UtcTicks, result.ErrorCode, ReleaseLease: true), cancellationToken);
            var conflictAudit = GlobalProductRetirementRequestAuditIntentFactory.Create(product, expectedVersion,
                operation.OperationId, operation.DecisionActorSubjectId.Value,
                ProductAuditOperation.GlobalProductRetirementManualReconciliationRequired,
                operation.RequestReason, now);
            var auditResult = await products.RecordRetirementConflictAsync(product.Id, expectedVersion, Binding(operation),
                conflictAudit, cancellationToken);
            if (!auditResult.Succeeded)
                return await operations.AdvanceAsync(claim, new(operation.Checkpoint,
                    ProductIdentityWorkflowRecoveryDisposition.Retryable, now.UtcTicks,
                    now.Add(retry).UtcTicks,
                    "GLOBAL_PRODUCT_RETIREMENT_MANUAL_AUDIT_NOT_PERSISTED", ReleaseLease: true),
                    cancellationToken);
            return await ManualAsync(claim, result.ErrorCode, now, cancellationToken);
        }
        return await AdvanceAsync(claim, GlobalProductRetirementRequestCheckpoint.DecisionApplied, now,
            cancellationToken);
    }

    private static GlobalProductActiveLifecycleOperationBinding Binding(
        GlobalProductRetirementRequestOperation operation) => new(GlobalProductLifecycleOperationKind.Retirement,
        operation.OperationId, operation.BaseProductVersion);
    private Task<bool> ManualAsync(GlobalProductRetirementRequestClaim claim, string? code, DateTimeOffset now,
        CancellationToken cancellationToken) => operations.AdvanceAsync(claim, new(
            GlobalProductRetirementRequestCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, now.UtcTicks,
            LastFailureCode: code ?? "GLOBAL_PRODUCT_RETIREMENT_RECONCILIATION_REQUIRED", ReleaseLease: true),
            cancellationToken);
    private Task<bool> AdvanceAsync(GlobalProductRetirementRequestClaim claim,
        GlobalProductRetirementRequestCheckpoint checkpoint, DateTimeOffset now,
        CancellationToken cancellationToken) => operations.AdvanceAsync(claim, new(checkpoint,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks, ReleaseLease: true), cancellationToken);
    private static GlobalProductRetirementRequestProcessingResult Success(
        GlobalProductRetirementRequestOperation operation, bool replay) => new(true, operation, null,
        operation.Checkpoint == GlobalProductRetirementRequestCheckpoint.Completed ? 200 : 202, replay);
    private static GlobalProductRetirementRequestProcessingResult Fail(string code, int status) =>
        new(false, null, code, status, false);
    private static GlobalProductRetirementRequestProcessingResult Fail(
        GlobalProductRetirementRequestOperation operation, string code, int status) =>
        new(false, operation, code, status, false);
}
