using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record GskuRetirementRequestProcessingResult(bool Succeeded, GskuRetirementRequestOperation? Operation,
    string? ErrorCode, int StatusCode, bool IsReplay);

public sealed class GskuRetirementRequestWorkflowProcessor(
    IGskuRetirementRequestOperationRepository operations,
    IGskuRepository gskus,
    IProductDefinitionRevisionRepository revisions,
    IProductIdentityWorkflowClient workflow,
    FirstGskuIdentityRetirementProcessor pairRetirement,
    GskuRetirementRequestWorkflowStartRequestFactory factory,
    TimeProvider clock)
{
    public async Task<GskuRetirementRequestProcessingResult> StartInteractiveAsync(Guid tenantId, Guid gskuId,
        int expectedVersion, Guid operationId, Guid makerId, string requestReason,
        string delegatedToken, GskuRetirementRequestExecutionConfiguration execution, CancellationToken ct)
    {
        if (tenantId == Guid.Empty || gskuId == Guid.Empty || operationId == Guid.Empty || makerId == Guid.Empty
            || expectedVersion < 0 || !GskuRetirementRequestWorkflowStartRequestFactory.HasValidReason(requestReason)
            || string.IsNullOrWhiteSpace(delegatedToken)) return Fail("GSKU_RETIREMENT_REQUEST_INVALID", 400);
        var operation = await operations.GetByOperationIdAsync(operationId, ct);
        var replay = operation is not null;
        if (operation is null)
        {
            var gsku = await gskus.GetByIdAsync(gskuId, ct);
            if (gsku is null) return Fail("GSKU_NOT_FOUND", 404);
            if (gsku.Version != expectedVersion) return Fail("GSKU_CONCURRENCY_CONFLICT", 409);
            if (await gskus.FindRetirementBlockerAsync(gskuId, ct) is { } blocker) return Fail(blocker, 409);
            var revision = await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, ct);
            if (revision is null) return Fail("GSKU_REVISION_NOT_FOUND", 409);
            GskuRetirementRequestStartPlan plan;
            try { plan = factory.Create(gsku, revision, operationId, makerId, requestReason); }
            catch (InvalidOperationException e)
            {
                return Fail(e.Message, e.Message.Contains("CONFIGURATION", StringComparison.Ordinal) ? 503 : 409);
            }
            var reserve = await operations.ReserveAsync(plan.Operation, ct);
            if (!reserve.Succeeded || reserve.Operation is null)
                return Fail(reserve.ErrorCode ?? "GSKU_RETIREMENT_REQUEST_OPERATION_CONFLICT", 409);
            operation = reserve.Operation;
            replay = reserve.IsReplay;
        }
        if (operation.TenantId != tenantId || operation.GskuId != gskuId
            || operation.BaseGskuVersion != expectedVersion || operation.MakerSubjectId != makerId
            || operation.RequestReason != GskuRetirementRequestWorkflowStartRequestFactory.NormalizeReason(requestReason)
            || !factory.MatchesConfiguration(operation))
            return Fail("GSKU_RETIREMENT_REQUEST_OPERATION_CONFLICT", 409);
        var result = await ProcessAsync(operation, delegatedToken, $"interactive-{makerId:N}", execution, true, ct);
        return result with { IsReplay = replay || result.IsReplay };
    }

    public Task<GskuRetirementRequestProcessingResult> RecoverAsync(GskuRetirementRequestOperation operation,
        string leaseOwner, GskuRetirementRequestExecutionConfiguration execution, CancellationToken ct) =>
        ProcessAsync(operation, null, leaseOwner, execution, false, ct);

    private async Task<GskuRetirementRequestProcessingResult> ProcessAsync(GskuRetirementRequestOperation operation,
        string? token, string leaseOwner, GskuRetirementRequestExecutionConfiguration execution,
        bool stopAtPending, CancellationToken ct)
    {
        for (var step = 0; step < 10; step++)
        {
            ct.ThrowIfCancellationRequested();
            if (operation.Checkpoint == GskuRetirementRequestCheckpoint.Completed) return Success(operation, true);
            if (operation.Checkpoint == GskuRetirementRequestCheckpoint.ManualReconciliationRequired)
                return Fail(operation, operation.LastFailureCode ?? "GSKU_RETIREMENT_REQUEST_RECONCILIATION_REQUIRED", 409);
            if (operation.Checkpoint == GskuRetirementRequestCheckpoint.AwaitingMakerReplay && token is null)
                return Fail(operation, "GSKU_RETIREMENT_REQUEST_MAKER_REPLAY_REQUIRED", 409);
            if (stopAtPending && operation.Checkpoint == GskuRetirementRequestCheckpoint.AwaitingDecision)
                return Success(operation, false);
            var now = clock.GetUtcNow();
            var claim = await operations.TryClaimAsync(operation.OperationId, operation.OperationFingerprint,
                [operation.Checkpoint], leaseOwner, now.UtcTicks, now.Add(execution.LeaseDuration).UtcTicks, ct);
            if (claim is null) return Fail(operation, "GSKU_RETIREMENT_REQUEST_BUSY", 409);
            var advanced = operation.Checkpoint switch
            {
                GskuRetirementRequestCheckpoint.Prepared => await PrepareAsync(operation, claim, token, now, execution, ct),
                GskuRetirementRequestCheckpoint.StartOutcomeUnknown or GskuRetirementRequestCheckpoint.AwaitingMakerReplay
                    => await RecoverStartAsync(operation, claim, token, now, execution, ct),
                GskuRetirementRequestCheckpoint.WorkflowStarted => await Advance(claim,
                    GskuRetirementRequestCheckpoint.AwaitingDecision, now, ct),
                GskuRetirementRequestCheckpoint.AwaitingDecision => await ObserveAsync(operation, claim, now, execution, ct),
                GskuRetirementRequestCheckpoint.DecisionObserved => await ApplyAsync(operation, claim, now, execution, ct),
                GskuRetirementRequestCheckpoint.DecisionApplied => await Advance(claim,
                    GskuRetirementRequestCheckpoint.Completed, now, ct),
                _ => false
            };
            if (!advanced) return Fail(operation, "GSKU_RETIREMENT_REQUEST_CONCURRENCY_CONFLICT", 409);
            operation = await operations.GetByOperationIdAsync(operation.OperationId, ct)
                ?? throw new InvalidOperationException("GSKU_RETIREMENT_REQUEST_OPERATION_LOST");
            if (operation.NextAttemptAtUtcTicksV1 > clock.GetUtcNow().UtcTicks)
                return Fail(operation, operation.LastFailureCode ?? "GSKU_RETIREMENT_REQUEST_RETRY_SCHEDULED", 503);
        }
        return Fail(operation, "GSKU_RETIREMENT_REQUEST_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<bool> PrepareAsync(GskuRetirementRequestOperation op, GskuRetirementRequestWorkflowClaim claim,
        string? token, DateTimeOffset now, GskuRetirementRequestExecutionConfiguration execution, CancellationToken ct)
    {
        var gsku = await gskus.GetByIdAsync(op.GskuId, ct);
        if (gsku is null) return await Manual(claim, "GSKU_NOT_FOUND", now, ct);
        var audit = GskuRetirementRequestAuditIntentFactory.Create(gsku, op.BaseGskuVersion, op.OperationId,
            op.MakerSubjectId, ProductAuditOperation.GskuRetirementRequested, op.RequestReason, now);
        var admitted = await gskus.AcquireRetirementRequestAsync(gsku.Id, op.BaseGskuVersion, Binding(op), audit, ct);
        if (!admitted.Succeeded) return await Manual(claim, admitted.ErrorCode, now, ct);
        if (token is null) return await operations.AdvanceAsync(claim, new(
            GskuRetirementRequestCheckpoint.AwaitingMakerReplay,
            ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, now.UtcTicks,
            LastFailureCode: "GSKU_RETIREMENT_REQUEST_MAKER_REPLAY_REQUIRED", ReleaseLease: true), ct);
        return await RecordStart(op, claim, await workflow.StartAsync(op.TenantId, factory.Rehydrate(op), token, ct),
            now, execution, ct);
    }

    private async Task<bool> RecoverStartAsync(GskuRetirementRequestOperation op,
        GskuRetirementRequestWorkflowClaim claim, string? token, DateTimeOffset now,
        GskuRetirementRequestExecutionConfiguration execution, CancellationToken ct)
    {
        var found = await workflow.GetStartResultAsync(op.TenantId,
            new(op.ObjectType, op.ObjectId, op.MakerSubjectId, op.StartIdempotencyKey), ct);
        if (found.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound && token is not null)
            found = await workflow.StartAsync(op.TenantId, factory.Rehydrate(op), token, ct);
        return await RecordStart(op, claim, found, now, execution, ct);
    }

    private async Task<bool> RecordStart(GskuRetirementRequestOperation op, GskuRetirementRequestWorkflowClaim claim,
        ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult> result,
        DateTimeOffset now, GskuRetirementRequestExecutionConfiguration execution, CancellationToken ct)
    {
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.Success && result.Value is { } v
            && v.WorkflowInstanceId != Guid.Empty && v.TemplateId != Guid.Empty && v.TemplateVersionId != Guid.Empty
            && v.ApprovalTaskId != Guid.Empty && v.AssignmentSnapshotId != Guid.Empty
            && v.StartTransitionLogId != Guid.Empty && v.ObjectRef == op.ObjectRef)
            return await operations.AdvanceAsync(claim, new(GskuRetirementRequestCheckpoint.WorkflowStarted,
                ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks, WorkflowInstanceId: v.WorkflowInstanceId,
                WorkflowTemplateId: v.TemplateId, WorkflowTemplateVersionId: v.TemplateVersionId,
                ApprovalTaskId: v.ApprovalTaskId, AssignmentSnapshotId: v.AssignmentSnapshotId,
                StartTransitionLogId: v.StartTransitionLogId,
                WorkflowStartedAtUtcTicksV1: (v.StartedAt ?? now).UtcTicks, ReleaseLease: true), ct);
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.Timeout or ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Incomplete or ProductIdentityWorkflowTransportOutcome.NotFound)
            return await operations.AdvanceAsync(claim, new(GskuRetirementRequestCheckpoint.StartOutcomeUnknown,
                ProductIdentityWorkflowRecoveryDisposition.Retryable, now.UtcTicks,
                now.Add(execution.RetryDelay).UtcTicks, result.ErrorCode, ReleaseLease: true), ct);
        return await Manual(claim, result.ErrorCode, now, ct);
    }

    private async Task<bool> ObserveAsync(GskuRetirementRequestOperation op, GskuRetirementRequestWorkflowClaim claim,
        DateTimeOffset now, GskuRetirementRequestExecutionConfiguration execution, CancellationToken ct)
    {
        if (!op.WorkflowInstanceId.HasValue) return await Manual(claim, "GSKU_RETIREMENT_REQUEST_START_EVIDENCE_INVALID", now, ct);
        var result = await workflow.GetTerminalEvidenceAsync(op.TenantId,
            new(op.WorkflowInstanceId.Value, op.ObjectType, op.ObjectId), ct);
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.NonTerminal or ProductIdentityWorkflowTransportOutcome.NotFound
            or ProductIdentityWorkflowTransportOutcome.Incomplete or ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Timeout)
            return await operations.AdvanceAsync(claim, new(op.Checkpoint, op.RecoveryDisposition, now.UtcTicks,
                now.Add(execution.RetryDelay).UtcTicks, result.ErrorCode ?? "GSKU_RETIREMENT_REQUEST_DECISION_PENDING",
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
            return await Manual(claim, "GSKU_RETIREMENT_REQUEST_EVIDENCE_CONFLICT", now, ct);
        return await operations.AdvanceAsync(claim, new(GskuRetirementRequestCheckpoint.DecisionObserved,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks,
            DecisionKind: approved ? ProductIdentityDecisionKind.Approved : ProductIdentityDecisionKind.Rejected,
            DecisionActorSubjectId: actor, DecisionReasonCode: e.ReasonCode, DecisionAtUtcTicksV1: e.DecisionAt.UtcTicks,
            DecisionTransitionSequence: e.TransitionSequence, DecisionTaskStatus: e.TaskStatus,
            DecisionInstanceStatus: e.InstanceStatus, ReleaseLease: true), ct);
    }

    private async Task<bool> ApplyAsync(GskuRetirementRequestOperation op, GskuRetirementRequestWorkflowClaim claim,
        DateTimeOffset now, GskuRetirementRequestExecutionConfiguration execution, CancellationToken ct)
    {
        if (!op.DecisionActorSubjectId.HasValue) return await Manual(claim, "GSKU_RETIREMENT_REQUEST_GSKU_DRIFT", now, ct);
        if (op.DecisionKind == ProductIdentityDecisionKind.Approved)
        {
            var pair = await pairRetirement.StartAsync(op.GskuId, checked(op.BaseGskuVersion + 1), op.OperationId,
                op.DecisionActorSubjectId.Value, op.ReasonCode, $"gsku-retirement-request-{op.OperationId:N}",
                execution.LeaseDuration, ct);
            if (!pair.Succeeded)
            {
                if (pair.Operation?.Checkpoint != FirstGskuIdentityRetirementCheckpoint.ManualReconciliationRequired)
                    return await Retry(claim, pair.ErrorCode, now, execution, ct);
                var gsku = await gskus.GetByIdAsync(op.GskuId, ct);
                if (gsku is null) return await Retry(claim,
                    "GSKU_RETIREMENT_REQUEST_MANUAL_AUDIT_NOT_PERSISTED", now, execution, ct);
                var conflictAudit = GskuRetirementRequestAuditIntentFactory.Create(gsku, gsku.Version,
                    op.OperationId, op.DecisionActorSubjectId.Value,
                    ProductAuditOperation.GskuRetirementManualReconciliationRequired,
                    op.RequestReason, now);
                var auditResult = await gskus.RecordRetirementRequestConflictAsync(gsku.Id, gsku.Version,
                    Binding(op), conflictAudit, ct);
                return auditResult.Succeeded
                    ? await Manual(claim, pair.ErrorCode, now, ct)
                    : await Retry(claim, "GSKU_RETIREMENT_REQUEST_MANUAL_AUDIT_NOT_PERSISTED",
                        now, execution, ct);
            }
        }
        else
        {
            var gsku = await gskus.GetByIdAsync(op.GskuId, ct);
            if (gsku is null) return await Manual(claim, "GSKU_NOT_FOUND", now, ct);
            var audit = GskuRetirementRequestAuditIntentFactory.Create(gsku, checked(op.BaseGskuVersion + 1),
                op.OperationId, op.DecisionActorSubjectId.Value, ProductAuditOperation.GskuRetirementRejected,
                op.RequestReason, op.DecisionAtUtcTicksV1.HasValue
                    ? new(op.DecisionAtUtcTicksV1.Value, TimeSpan.Zero) : now);
            var rejected = await gskus.RejectRetirementRequestAsync(gsku.Id, checked(op.BaseGskuVersion + 1),
                Binding(op), audit, ct);
            if (!rejected.Succeeded) return await Manual(claim, rejected.ErrorCode, now, ct);
        }
        return await Advance(claim, GskuRetirementRequestCheckpoint.DecisionApplied, now, ct);
    }

    private Task<bool> Retry(GskuRetirementRequestWorkflowClaim claim, string? code, DateTimeOffset now,
        GskuRetirementRequestExecutionConfiguration execution, CancellationToken ct) =>
        operations.AdvanceAsync(claim, new(claim.Checkpoint, ProductIdentityWorkflowRecoveryDisposition.Retryable,
            now.UtcTicks, now.Add(execution.RetryDelay).UtcTicks, code, ReleaseLease: true), ct);
    private Task<bool> Advance(GskuRetirementRequestWorkflowClaim claim, GskuRetirementRequestCheckpoint next,
        DateTimeOffset now, CancellationToken ct) => operations.AdvanceAsync(claim, new(next,
            ProductIdentityWorkflowRecoveryDisposition.None, now.UtcTicks, ReleaseLease: true), ct);
    private Task<bool> Manual(GskuRetirementRequestWorkflowClaim claim, string? code, DateTimeOffset now,
        CancellationToken ct) => operations.AdvanceAsync(claim, new(
            GskuRetirementRequestCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, now.UtcTicks,
            LastFailureCode: code ?? "GSKU_RETIREMENT_REQUEST_RECONCILIATION_REQUIRED", ReleaseLease: true), ct);
    private static GskuActiveLifecycleOperationBinding Binding(GskuRetirementRequestOperation op) =>
        new(GskuLifecycleOperationKind.Retirement, op.OperationId, op.BaseGskuVersion);
    private static GskuRetirementRequestProcessingResult Success(GskuRetirementRequestOperation op, bool replay) =>
        new(true, op, null, op.Checkpoint == GskuRetirementRequestCheckpoint.AwaitingDecision ? 202 : 200, replay);
    private static GskuRetirementRequestProcessingResult Fail(string code, int status) =>
        new(false, null, code, status, false);
    private static GskuRetirementRequestProcessingResult Fail(GskuRetirementRequestOperation op, string code, int status) =>
        new(false, op, code, status, false);
}
