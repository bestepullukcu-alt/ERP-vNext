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
        for (var step = 0; step < 9; step++)
        {
            ct.ThrowIfCancellationRequested();
            if (operation.Checkpoint == GskuCorrectionWorkflowCheckpoint.Completed) return Success(operation, true);
            if (operation.Checkpoint == GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired)
                return Fail(operation, operation.LastFailureCode ?? "GSKU_CORRECTION_RECONCILIATION_REQUIRED", 409);
            if (operation.Checkpoint == GskuCorrectionWorkflowCheckpoint.AwaitingMakerReplay && token is null)
                return Fail(operation, "GSKU_CORRECTION_MAKER_REPLAY_REQUIRED", 409);
            if (stopAtPending && operation.Checkpoint == GskuCorrectionWorkflowCheckpoint.AwaitingDecision)
                return Success(operation, false);
            var now = clock.GetUtcNow();
            var claim = await operations.TryClaimAsync(operation.OperationId, operation.OperationFingerprint,
                [operation.Checkpoint], leaseOwner, now.UtcTicks, now.Add(execution.LeaseDuration).UtcTicks, ct);
            if (claim is null) return Fail(operation, "GSKU_CORRECTION_BUSY", 409);
            var advanced = operation.Checkpoint switch
            {
                GskuCorrectionWorkflowCheckpoint.Prepared => await PrepareAsync(operation, claim, token, now, execution, ct),
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
            if (!advanced) return Fail(operation, "GSKU_CORRECTION_CONCURRENCY_CONFLICT", 409);
            operation = await operations.GetByOperationIdAsync(operation.OperationId, ct)
                ?? throw new InvalidOperationException("GSKU_CORRECTION_OPERATION_LOST");
            if (operation.NextAttemptAtUtcTicksV1 > clock.GetUtcNow().UtcTicks)
                return Fail(operation, operation.LastFailureCode ?? "GSKU_CORRECTION_RETRY_SCHEDULED", 503);
        }
        return Fail(operation, "GSKU_CORRECTION_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<bool> PrepareAsync(GskuCorrectionWorkflowOperation op, GskuCorrectionWorkflowClaim claim,
        string? token, DateTimeOffset now, GskuCorrectionExecutionConfiguration execution, CancellationToken ct)
    {
        var gsku = await gskus.GetByIdAsync(op.GskuId, ct);
        if (gsku is null) return await Manual(claim, "GSKU_NOT_FOUND", now, ct);
        var audit = GskuCorrectionAuditIntentFactory.Create(gsku, op.BaseGskuVersion, op.OperationId,
            op.MakerSubjectId, ProductAuditOperation.GskuCorrectionRequested,
            op.ProposedPackQuantity, op.ProposedPackUomCode,
            op.ProposedPackApplicabilitySelection, op.ProposedPackUomSelection, now);
        var admitted = await gskus.AcquireCorrectionAsync(gsku.Id, op.BaseGskuVersion, Binding(op), audit, ct);
        if (!admitted.Succeeded) return await Manual(claim, admitted.ErrorCode, now, ct);
        if (token is null) return await operations.AdvanceAsync(claim, new(
            GskuCorrectionWorkflowCheckpoint.AwaitingMakerReplay,
            ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay, now.UtcTicks,
            LastFailureCode: "GSKU_CORRECTION_MAKER_REPLAY_REQUIRED", ReleaseLease: true), ct);
        var started = await workflow.StartAsync(op.TenantId, factory.Rehydrate(op), token, ct);
        return await RecordStart(op, claim, started, now, execution, ct);
    }

    private async Task<bool> RecoverStartAsync(GskuCorrectionWorkflowOperation op, GskuCorrectionWorkflowClaim claim,
        string? token, DateTimeOffset now, GskuCorrectionExecutionConfiguration execution, CancellationToken ct)
    {
        var found = await workflow.GetStartResultAsync(op.TenantId,
            new(op.ObjectType, op.ObjectId, op.MakerSubjectId, op.StartIdempotencyKey), ct);
        if (found.Outcome == ProductIdentityWorkflowTransportOutcome.NotFound && token is not null)
            found = await workflow.StartAsync(op.TenantId, factory.Rehydrate(op), token, ct);
        return await RecordStart(op, claim, found, now, execution, ct);
    }

    private async Task<bool> RecordStart(GskuCorrectionWorkflowOperation op, GskuCorrectionWorkflowClaim claim,
        ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult> result,
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
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.Timeout or ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Incomplete or ProductIdentityWorkflowTransportOutcome.NotFound)
            return await operations.AdvanceAsync(claim, new(GskuCorrectionWorkflowCheckpoint.StartOutcomeUnknown,
                ProductIdentityWorkflowRecoveryDisposition.Retryable, now.UtcTicks,
                now.Add(execution.RetryDelay).UtcTicks, result.ErrorCode, ReleaseLease: true), ct);
        if (result.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
            // The engine said "started" with evidence this service cannot accept: an approval may exist, and it
            // cannot be cancelled without the maker's token — the GSKU is NOT given back (no double decision).
            return await Manual(claim, "GSKU_CORRECTION_START_EVIDENCE_INVALID", now, ct);
        // The start was refused before anything was processed (the shared classification): nothing exists at the
        // engine, so the GSKU is given back and the reason recorded; the maker learns it from the answer.
        var fault = ProductIdentityWorkflowFaultPolicy.Classify(
            ProductIdentityWorkflowCall.Start, result.Outcome, result.ErrorCode);
        var reason = fault.MakerCode ?? result.ErrorCode ?? "GSKU_CORRECTION_START_REFUSED";
        return await EndAndReleaseAsync(op, claim, reason, now, ct);
    }

    // B3: the correction ends without its decision applied; the GSKU's lock goes in the same write as the intent
    // that says why. If giving it back fails for a reason that may pass (the cap, the store), the operation waits.
    private async Task<bool> EndAndReleaseAsync(GskuCorrectionWorkflowOperation op, GskuCorrectionWorkflowClaim claim,
        string reason, DateTimeOffset now, CancellationToken ct)
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
                return await operations.AdvanceAsync(claim, new(op.Checkpoint,
                    ProductIdentityWorkflowRecoveryDisposition.Retryable, now.UtcTicks,
                    now.AddMinutes(1).UtcTicks, released.ErrorCode, ReleaseLease: true), ct);
        }
        log.LogWarning("GSKU correction {OperationId} of tenant {TenantId} ended without its decision applied ({ResultCode}); the GSKU is free again.",
            op.OperationId, op.TenantId, reason);
        return await Manual(claim, reason, now, ct);
    }

    private async Task<bool> ObserveAsync(GskuCorrectionWorkflowOperation op, GskuCorrectionWorkflowClaim claim,
        DateTimeOffset now, GskuCorrectionExecutionConfiguration execution, CancellationToken ct)
    {
        if (!op.WorkflowInstanceId.HasValue) return await Manual(claim, "GSKU_CORRECTION_START_EVIDENCE_INVALID", now, ct);
        var result = await workflow.GetTerminalEvidenceAsync(op.TenantId,
            new(op.WorkflowInstanceId.Value, op.ObjectType, op.ObjectId), ct);
        if (result.Outcome is ProductIdentityWorkflowTransportOutcome.NonTerminal or ProductIdentityWorkflowTransportOutcome.NotFound
            or ProductIdentityWorkflowTransportOutcome.Incomplete or ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Timeout)
            return await operations.AdvanceAsync(claim, new(op.Checkpoint, op.RecoveryDisposition, now.UtcTicks,
                now.Add(execution.RetryDelay).UtcTicks, result.ErrorCode ?? "GSKU_CORRECTION_DECISION_PENDING", ReleaseLease: true), ct);
        if (result.Outcome != ProductIdentityWorkflowTransportOutcome.Success
            && ProductIdentityWorkflowFaultPolicy.Classify(ProductIdentityWorkflowCall.TerminalEvidence, result.Outcome,
                result.ErrorCode).Kind == ProductIdentityWorkflowFaultKind.ServiceWait)
            // The service's identity or reachability: the chain's fault, not the record's — it waits.
            return await operations.AdvanceAsync(claim, new(op.Checkpoint, ProductIdentityWorkflowRecoveryDisposition.Retryable,
                now.UtcTicks, now.Add(execution.RetryDelay).UtcTicks, result.ErrorCode, ReleaseLease: true), ct);
        var e = result.Value;
        if (e is not null && Guid.TryParse(e.ActorUserId, out var self) && self == op.MakerSubjectId)
        {
            // B8: the maker decided their own correction — ignored and said; the operation keeps waiting.
            log.LogWarning("GSKU correction {OperationId} of tenant {TenantId} was decided by its own maker; the decision is ignored ({ResultCode}).",
                op.OperationId, op.TenantId, SelfApprovalIgnored);
            return await operations.AdvanceAsync(claim, new(op.Checkpoint, ProductIdentityWorkflowRecoveryDisposition.Retryable,
                now.UtcTicks, now.AddMinutes(15).UtcTicks, SelfApprovalIgnored, ReleaseLease: true), ct);
        }
        var approved = e?.TerminalAction == "Approve" && e.TaskStatus == "Approved" && e.InstanceStatus == "Completed";
        var rejected = e?.TerminalAction == "Reject" && e.TaskStatus == "Rejected" && e.InstanceStatus == "Rejected";
        if ((!approved && !rejected) || e is null || e.WorkflowInstanceId != op.WorkflowInstanceId
            || e.ApprovalTaskId != op.ApprovalTaskId || e.TemplateId != op.WorkflowTemplateId
            || e.TemplateVersionId != op.WorkflowTemplateVersionId || e.ObjectType != op.ObjectType
            || e.ObjectId != op.ObjectId || e.ObjectRef != op.ObjectRef
            || !Guid.TryParse(e.ActorUserId, out var actor) || actor == Guid.Empty || actor == op.MakerSubjectId
            || e.TransitionSequence <= 0 || e.DecisionAt.Offset != TimeSpan.Zero)
            // The approval is decided (terminal evidence) and its evidence cannot be used: nothing more can come of
            // it — the GSKU is given back (B3).
            return e is null
                ? await Manual(claim, "GSKU_CORRECTION_EVIDENCE_CONFLICT", now, ct)
                : await EndAndReleaseAsync(op, claim, "GSKU_CORRECTION_EVIDENCE_CONFLICT", now, ct);
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
            return await Manual(claim, "GSKU_CORRECTION_GSKU_DRIFT", now, ct);
        var approved = op.DecisionKind == ProductIdentityDecisionKind.Approved;
        ReferenceCatalogSelection? applicability = null, uom = null;
        if (approved)
        {
            if (await gskus.FindRetirementBlockerAsync(op.GskuId, ct) is { } blocker)
                return await EndAndReleaseAsync(op, claim, blocker, now, ct);
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
                return !resolved.IsSuccessful && resolved.StatusCode is 503 or 504 or 401 or 403
                    ? await operations.AdvanceAsync(claim, new(op.Checkpoint,
                        ProductIdentityWorkflowRecoveryDisposition.Retryable, now.UtcTicks,
                        now.Add(execution.RetryDelay).UtcTicks,
                        resolved.FailureCode ?? "REFERENCE_DATA_CONTRACT_UNAVAILABLE", ReleaseLease: true), ct)
                    : await EndAndReleaseAsync(op, claim, NotApplicable, now, ct);
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
                ? await EndAndReleaseAsync(op, claim, result.ErrorCode, now, ct)
                : await operations.AdvanceAsync(claim, new(op.Checkpoint,
                    ProductIdentityWorkflowRecoveryDisposition.Retryable, now.UtcTicks,
                    now.Add(execution.RetryDelay).UtcTicks, result.ErrorCode, ReleaseLease: true), ct);
        return await Advance(claim, GskuCorrectionWorkflowCheckpoint.DecisionApplied, now, release: true, ct: ct);
    }

    private Task<bool> Advance(GskuCorrectionWorkflowClaim claim, GskuCorrectionWorkflowCheckpoint next,
        DateTimeOffset now, string? error = null, bool release = false, CancellationToken ct = default) =>
        operations.AdvanceAsync(claim, new(next, ProductIdentityWorkflowRecoveryDisposition.None,
            now.UtcTicks, LastFailureCode: error, ReleaseLease: release), ct);
    private Task<bool> Manual(GskuCorrectionWorkflowClaim claim, string? code, DateTimeOffset now, CancellationToken ct) =>
        operations.AdvanceAsync(claim, new(GskuCorrectionWorkflowCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, now.UtcTicks,
            LastFailureCode: code ?? "GSKU_CORRECTION_RECONCILIATION_REQUIRED", ReleaseLease: true), ct);
    private static GskuActiveLifecycleOperationBinding Binding(GskuCorrectionWorkflowOperation op) =>
        new(GskuLifecycleOperationKind.Correction, op.OperationId, op.BaseGskuVersion);
    private static GskuCorrectionProcessingResult Success(GskuCorrectionWorkflowOperation op, bool replay) =>
        new(true, op, null, op.Checkpoint == GskuCorrectionWorkflowCheckpoint.AwaitingDecision ? 202 : 200, replay);
    private static GskuCorrectionProcessingResult Fail(string code, int status) => new(false, null, code, status, false);
    private static GskuCorrectionProcessingResult Fail(GskuCorrectionWorkflowOperation op, string code, int status) =>
        new(false, op, code, status, false);
}
