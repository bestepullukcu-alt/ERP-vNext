using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record LskuRetirementRequestProcessingResult(bool Succeeded,
    LskuRetirementRequestOperation? Operation, string? ErrorCode, int StatusCode, bool IsReplay);
public sealed record LskuRetirementRequestExecutionConfiguration(TimeSpan LeaseDuration, TimeSpan RetryDelay);

public sealed class LskuRetirementRequestWorkflowProcessor(
    ILskuRetirementRequestOperationRepository operations, ILskuRepository lskus,
    IProductIdentityWorkflowClient workflow, LskuRetirementRequestWorkflowStartRequestFactory factory,
    TimeProvider clock)
{
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
            if (lsku is null) return Fail("LSKU_NOT_FOUND", 404);
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
        var result = await ProcessAsync(operation, delegatedToken, $"interactive-{makerId:N}", execution, true, ct);
        return result with { IsReplay = replay || result.IsReplay };
    }

    public Task<LskuRetirementRequestProcessingResult> RecoverAsync(LskuRetirementRequestOperation operation,
        string leaseOwner, LskuRetirementRequestExecutionConfiguration execution, CancellationToken ct) =>
        ProcessAsync(operation, null, leaseOwner, execution, false, ct);

    private async Task<LskuRetirementRequestProcessingResult> ProcessAsync(LskuRetirementRequestOperation operation,
        string? token, string leaseOwner, LskuRetirementRequestExecutionConfiguration execution,
        bool stopAtPending, CancellationToken ct)
    {
        for (var step = 0; step < 9; step++)
        {
            ct.ThrowIfCancellationRequested();
            if (operation.Checkpoint == LskuRetirementRequestCheckpoint.Completed) return Success(operation, true);
            if (operation.Checkpoint == LskuRetirementRequestCheckpoint.ManualReconciliationRequired)
                return Fail(operation, operation.LastFailureCode ?? "LSKU_RETIREMENT_RECONCILIATION_REQUIRED", 409);
            if (operation.Checkpoint == LskuRetirementRequestCheckpoint.AwaitingMakerReplay && token is null)
                return Fail(operation, "LSKU_RETIREMENT_MAKER_REPLAY_REQUIRED", 409);
            if (stopAtPending && operation.Checkpoint == LskuRetirementRequestCheckpoint.AwaitingDecision)
                return Success(operation, false);
            var now = clock.GetUtcNow();
            var claim = await operations.TryClaimAsync(operation.OperationId, operation.OperationFingerprint,
                [operation.Checkpoint], leaseOwner, now.UtcTicks, now.Add(execution.LeaseDuration).UtcTicks, ct);
            if (claim is null) return Fail(operation, "LSKU_RETIREMENT_BUSY", 409);
            var advanced = operation.Checkpoint switch
            {
                LskuRetirementRequestCheckpoint.Prepared => await StartAsync(operation, claim, token, now, execution, ct),
                LskuRetirementRequestCheckpoint.StartOutcomeUnknown or LskuRetirementRequestCheckpoint.AwaitingMakerReplay
                    => await RecoverStartAsync(operation, claim, token, now, execution, ct),
                LskuRetirementRequestCheckpoint.WorkflowStarted => await AdvanceAsync(claim,
                    LskuRetirementRequestCheckpoint.AwaitingDecision, now, ct),
                LskuRetirementRequestCheckpoint.AwaitingDecision => await ObserveAsync(operation, claim, now, execution, ct),
                LskuRetirementRequestCheckpoint.DecisionObserved => await ApplyAsync(operation, claim, now, execution, ct),
                LskuRetirementRequestCheckpoint.DecisionApplied => await AdvanceAsync(claim,
                    LskuRetirementRequestCheckpoint.Completed, now, ct),
                _ => false
            };
            if (!advanced) return Fail(operation, "LSKU_RETIREMENT_CONCURRENCY_CONFLICT", 409);
            operation = await operations.GetByOperationIdAsync(operation.OperationId, ct)
                ?? throw new InvalidOperationException("LSKU_RETIREMENT_OPERATION_LOST");
            if (operation.NextAttemptAtUtcTicksV1 > clock.GetUtcNow().UtcTicks)
                return Fail(operation, operation.LastFailureCode ?? "LSKU_RETIREMENT_RETRY_SCHEDULED", 503);
        }
        return Fail(operation, "LSKU_RETIREMENT_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<bool> StartAsync(LskuRetirementRequestOperation op, LskuRetirementRequestClaim claim,
        string? token, DateTimeOffset now, LskuRetirementRequestExecutionConfiguration execution, CancellationToken ct)
    {
        var lsku = await lskus.GetByIdAsync(op.LskuId, ct);
        if (lsku is null) return await ManualAsync(claim, "LSKU_NOT_FOUND", now, ct);
        var audit = LskuIdentityLifecycleAuditIntentFactory.CreateRetirementOperation(lsku, op.BaseLskuVersion,
            op.OperationId, op.MakerSubjectId, ProductAuditOperation.LskuRetirementRequested, op.RequestReason, now);
        var admitted = await lskus.AcquireLifecycleOperationAsync(lsku.Id, op.BaseLskuVersion, Binding(op), audit, ct);
        if (!admitted.Succeeded) return await ManualAsync(claim, admitted.ErrorCode, now, ct);
        if (token is null)
            return await operations.AdvanceAsync(claim, new(LskuRetirementRequestCheckpoint.AwaitingMakerReplay,
                ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, now.UtcTicks,
                LastFailureCode: "LSKU_RETIREMENT_MAKER_REPLAY_REQUIRED", ReleaseLease: true), ct);
        return await RecordStartAsync(op, claim,
            await workflow.StartAsync(op.TenantId, factory.Rehydrate(op), token, ct), now, execution, ct);
    }

    private async Task<bool> RecoverStartAsync(LskuRetirementRequestOperation op, LskuRetirementRequestClaim claim,
        string? token, DateTimeOffset now, LskuRetirementRequestExecutionConfiguration execution, CancellationToken ct)
    {
        var result = await workflow.GetStartResultAsync(op.TenantId,
            new(op.ObjectType, op.ObjectId, op.MakerSubjectId, op.StartIdempotencyKey), ct);
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound && token is not null)
            result = await workflow.StartAsync(op.TenantId, factory.Rehydrate(op), token, ct);
        return await RecordStartAsync(op, claim, result, now, execution, ct);
    }

    private async Task<bool> RecordStartAsync(LskuRetirementRequestOperation op, LskuRetirementRequestClaim claim,
        ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult> result, DateTimeOffset now,
        LskuRetirementRequestExecutionConfiguration execution, CancellationToken ct)
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
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.Timeout or ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Incomplete or ProductIdentityWorkflowTransportOutcome.NotFound)
            return await operations.AdvanceAsync(claim, new(LskuRetirementRequestCheckpoint.StartOutcomeUnknown,
                ProductIdentityWorkflowRecoveryDisposition.Retryable, now.UtcTicks,
                now.Add(execution.RetryDelay).UtcTicks, result.ErrorCode, ReleaseLease: true), ct);
        return await ManualAsync(claim, result.ErrorCode, now, ct);
    }

    private async Task<bool> ObserveAsync(LskuRetirementRequestOperation op, LskuRetirementRequestClaim claim,
        DateTimeOffset now, LskuRetirementRequestExecutionConfiguration execution, CancellationToken ct)
    {
        if (!op.WorkflowInstanceId.HasValue) return await ManualAsync(claim, "LSKU_RETIREMENT_START_EVIDENCE_INVALID", now, ct);
        var result = await workflow.GetTerminalEvidenceAsync(op.TenantId,
            new(op.WorkflowInstanceId.Value, op.ObjectType, op.ObjectId), ct);
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.NonTerminal or ProductIdentityWorkflowTransportOutcome.NotFound
            or ProductIdentityWorkflowTransportOutcome.Incomplete or ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Timeout)
            return await operations.AdvanceAsync(claim, new(op.Checkpoint, op.RecoveryDisposition, now.UtcTicks,
                now.Add(execution.RetryDelay).UtcTicks, result.ErrorCode ?? "LSKU_RETIREMENT_DECISION_PENDING",
                ReleaseLease: true), ct);
        var e = result.Value;
        var approved = e?.TerminalAction == "Approve" && e.TaskStatus == "Approved" && e.InstanceStatus == "Completed";
        var rejected = e?.TerminalAction == "Reject" && e.TaskStatus == "Rejected" && e.InstanceStatus == "Rejected";
        if ((!approved && !rejected) || e is null || e.WorkflowInstanceId != op.WorkflowInstanceId
            || e.ApprovalTaskId != op.ApprovalTaskId || e.TemplateId != op.WorkflowTemplateId
            || e.TemplateVersionId != op.WorkflowTemplateVersionId || e.ObjectType != op.ObjectType
            || e.ObjectId != op.ObjectId || e.ObjectRef != op.ObjectRef
            || !Guid.TryParse(e.ActorUserId, out var actor) || actor == Guid.Empty || actor == op.MakerSubjectId
            || e.TransitionSequence <= 0 || e.DecisionAt.Offset != TimeSpan.Zero)
            return await ManualAsync(claim, "LSKU_RETIREMENT_EVIDENCE_CONFLICT", now, ct);
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
        if (lsku is null || !op.DecisionActorSubjectId.HasValue)
            return await ManualAsync(claim, "LSKU_RETIREMENT_LSKU_DRIFT", now, ct);
        var approved = op.DecisionKind == ProductIdentityDecisionKind.Approved;
        var expected = checked(op.BaseLskuVersion + 1);
        var audit = LskuIdentityLifecycleAuditIntentFactory.CreateRetirementOperation(lsku, expected,
            op.OperationId, op.DecisionActorSubjectId.Value,
            approved ? ProductAuditOperation.LskuIdentityRetired : ProductAuditOperation.LskuRetirementRejected,
            op.RequestReason, op.DecisionAtUtcTicksV1.HasValue
                ? new(op.DecisionAtUtcTicksV1.Value, TimeSpan.Zero) : now);
        var result = await lskus.ApplyRetirementDecisionAsync(lsku.Id, expected, Binding(op), approved, audit, ct);
        if (!result.Succeeded)
            return await operations.AdvanceAsync(claim, new(op.Checkpoint,
                ProductIdentityWorkflowRecoveryDisposition.Retryable, now.UtcTicks,
                now.Add(execution.RetryDelay).UtcTicks, result.ErrorCode, ReleaseLease: true), ct);
        return await AdvanceAsync(claim, LskuRetirementRequestCheckpoint.DecisionApplied, now, ct);
    }

    private static LskuActiveLifecycleOperationBinding Binding(LskuRetirementRequestOperation op) =>
        new(LskuLifecycleOperationKind.Retirement, op.OperationId, op.BaseLskuVersion);
    private Task<bool> AdvanceAsync(LskuRetirementRequestClaim claim, LskuRetirementRequestCheckpoint checkpoint,
        DateTimeOffset now, CancellationToken ct) => operations.AdvanceAsync(claim, new(checkpoint,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks, ReleaseLease: true), ct);
    private Task<bool> ManualAsync(LskuRetirementRequestClaim claim, string? code, DateTimeOffset now,
        CancellationToken ct) => operations.AdvanceAsync(claim, new(
            LskuRetirementRequestCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, now.UtcTicks,
            LastFailureCode: code ?? "LSKU_RETIREMENT_RECONCILIATION_REQUIRED", ReleaseLease: true), ct);
    private static LskuRetirementRequestProcessingResult Success(LskuRetirementRequestOperation op, bool replay) =>
        new(true, op, null, op.Checkpoint == LskuRetirementRequestCheckpoint.Completed ? 200 : 202, replay);
    private static LskuRetirementRequestProcessingResult Fail(string code, int status) => new(false, null, code, status, false);
    private static LskuRetirementRequestProcessingResult Fail(LskuRetirementRequestOperation op, string code, int status) =>
        new(false, op, code, status, false);
}
