using System.Globalization;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record LskuIdentityWorkflowProcessingResult(
    bool Succeeded,
    LskuIdentityWorkflowOperation? Operation,
    string? ErrorCode,
    int StatusCode,
    bool IsReplay);

public sealed record LskuIdentityWorkflowExecutionConfiguration(
    TimeSpan LeaseDuration,
    TimeSpan RetryDelay);

public sealed class LskuIdentityWorkflowProcessor(
    ILskuIdentityWorkflowOperationRepository operations,
    ILskuRepository lskus,
    IGskuRepository gskus,
    IProductDefinitionRevisionRepository revisions,
    IWorkflowVerifiedMarketReferenceResolver marketResolver,
    IProductIdentityWorkflowClient workflowClient,
    LskuIdentityWorkflowStartRequestFactory requestFactory,
    TimeProvider timeProvider)
{
    private static readonly LskuIdentityWorkflowCheckpoint[] RecoverableCheckpoints =
    [
        LskuIdentityWorkflowCheckpoint.Prepared,
        LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown,
        LskuIdentityWorkflowCheckpoint.WorkflowStarted,
        LskuIdentityWorkflowCheckpoint.LocalPendingApplied,
        LskuIdentityWorkflowCheckpoint.AwaitingDecision,
        LskuIdentityWorkflowCheckpoint.DecisionObserved,
        LskuIdentityWorkflowCheckpoint.ApprovalValidated,
        LskuIdentityWorkflowCheckpoint.DecisionApplied
    ];

    public async Task<LskuIdentityWorkflowProcessingResult> StartInteractiveAsync(
        Guid tenantId,
        Guid lskuId,
        int expectedVersion,
        Guid operationId,
        Guid makerSubjectId,
        string delegatedUserToken,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || lskuId == Guid.Empty || operationId == Guid.Empty
            || makerSubjectId == Guid.Empty || expectedVersion < 0
            || string.IsNullOrWhiteSpace(delegatedUserToken))
        {
            return Fail("LSKU_IDENTITY_WORKFLOW_START_INVALID", 400);
        }

        var existing = await operations.GetByOperationIdAsync(operationId, cancellationToken);
        LskuIdentityWorkflowOperation operation;
        var replay = existing is not null;
        if (existing is null)
        {
            var lsku = await lskus.GetByIdAsync(lskuId, cancellationToken);
            if (lsku is null)
            {
                return Fail("LSKU_IDENTITY_NOT_FOUND", 404);
            }

            var gsku = await gskus.GetByIdAsync(lsku.GskuId, cancellationToken);
            var revision = gsku is null
                ? null
                : await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
            if (gsku is null || revision is null)
            {
                return Fail("LSKU_IDENTITY_PARENT_NOT_REFERENCEABLE", 409);
            }
            LskuIdentityWorkflowStartPlan plan;
            try
            {
                plan = requestFactory.Create(
                    tenantId, lsku, gsku, revision, operationId, expectedVersion, makerSubjectId);
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message, exception.Message == "LSKU_IDENTITY_WORKFLOW_CONFIGURATION_INVALID"
                    ? 503 : 409);
            }

            var reserved = await operations.ReserveAsync(plan.Operation, cancellationToken);
            if (!reserved.Succeeded || reserved.Operation is null)
            {
                return Fail(reserved.ErrorCode ?? "LSKU_IDENTITY_WORKFLOW_OPERATION_CONFLICT", 409);
            }

            operation = reserved.Operation;
            replay = reserved.IsReplay;
        }
        else
        {
            operation = existing;
        }

        if (!ExactInteractiveFacts(operation, tenantId, lskuId, expectedVersion, makerSubjectId))
        {
            return Fail("LSKU_IDENTITY_WORKFLOW_OPERATION_CONFLICT", 409);
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

    public Task<LskuIdentityWorkflowProcessingResult> RecoverAsync(
        LskuIdentityWorkflowOperation operation,
        string leaseOwner,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default) =>
        ProcessAsync(operation, null, leaseOwner, leaseDuration, retryDelay,
            stopWhenAwaitingDecision: false, cancellationToken);

    private async Task<LskuIdentityWorkflowProcessingResult> ProcessAsync(
        LskuIdentityWorkflowOperation initial,
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
            if (operation.Checkpoint == LskuIdentityWorkflowCheckpoint.Completed)
            {
                return Success(operation, true);
            }
            if (operation.Checkpoint is LskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart
                or LskuIdentityWorkflowCheckpoint.Superseded)
            {
                return Success(operation, true);
            }
            if (stopWhenAwaitingDecision
                && operation.Checkpoint == LskuIdentityWorkflowCheckpoint.AwaitingDecision)
            {
                return Success(operation, false);
            }
            if (operation.Checkpoint == LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
                && string.IsNullOrWhiteSpace(delegatedUserToken))
            {
                return Fail(operation, "LSKU_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED", 409);
            }
            if (operation.Checkpoint == LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired)
            {
                return Fail(operation, "LSKU_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED", 409);
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
                return Fail(operation, "LSKU_IDENTITY_WORKFLOW_BUSY", 409);
            }

            var advanced = operation.Checkpoint switch
            {
                LskuIdentityWorkflowCheckpoint.Prepared =>
                    await AdvanceAsync(claim, LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                        ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true,
                        cancellationToken: cancellationToken),
                LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown or
                    LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay =>
                    await ResolveStartAsync(operation, claim, delegatedUserToken, now, retryDelay, cancellationToken),
                LskuIdentityWorkflowCheckpoint.WorkflowStarted =>
                    await ApplyLocalPendingAsync(operation, claim, now, retryDelay, cancellationToken),
                LskuIdentityWorkflowCheckpoint.LocalPendingApplied =>
                    await AdvanceAsync(claim, LskuIdentityWorkflowCheckpoint.AwaitingDecision,
                        ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true,
                        cancellationToken: cancellationToken),
                LskuIdentityWorkflowCheckpoint.AwaitingDecision =>
                    await ObserveDecisionAsync(operation, claim, now, retryDelay, cancellationToken),
                LskuIdentityWorkflowCheckpoint.DecisionObserved =>
                    await ValidateApprovalOrApplyRejectAsync(
                        operation, claim, now, retryDelay, cancellationToken),
                LskuIdentityWorkflowCheckpoint.ApprovalValidated =>
                    await ApplyDecisionAsync(operation, claim, now, retryDelay, cancellationToken),
                LskuIdentityWorkflowCheckpoint.DecisionApplied =>
                    await VerifyDecisionAppliedAndCompleteAsync(
                        operation, claim, now, retryDelay, cancellationToken),
                _ => false
            };
            if (!advanced)
            {
                return Fail(operation, "LSKU_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT", 409);
            }

            operation = await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken)
                ?? throw new InvalidOperationException("LSKU_IDENTITY_WORKFLOW_OPERATION_LOST");
            if (operation.NextAttemptAtUtcTicksV1 > timeProvider.GetUtcNow().UtcTicks)
            {
                return Fail(operation, operation.LastFailureCode ?? "LSKU_IDENTITY_WORKFLOW_RETRY_SCHEDULED", 503);
            }
        }

        return Fail(operation, "LSKU_IDENTITY_WORKFLOW_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<bool> ResolveStartAsync(
        LskuIdentityWorkflowOperation operation,
        LskuIdentityWorkflowClaim claim,
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
                LskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay,
                now,
                lookup.ErrorCode ?? "LSKU_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED",
                releaseLease: true,
                cancellationToken: cancellationToken);
        }

        if (Retryable(lookup.Outcome))
        {
            return await ScheduleRetryAsync(
                claim, LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                lookup.ErrorCode, now, retryDelay, cancellationToken);
        }

        return await QuarantineAsync(claim, lookup.ErrorCode, now, cancellationToken);
    }

    private Task<bool> PersistStartedAsync(
        LskuIdentityWorkflowOperation operation,
        LskuIdentityWorkflowClaim claim,
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
            return QuarantineAsync(claim, "LSKU_IDENTITY_WORKFLOW_START_PROOF_INVALID", now, cancellationToken);
        }

        return operations.AdvanceAsync(
            claim,
            new(
                LskuIdentityWorkflowCheckpoint.WorkflowStarted,
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
        LskuIdentityWorkflowOperation operation,
        LskuIdentityWorkflowClaim claim,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        var lsku = await lskus.GetByIdAsync(operation.LskuId, cancellationToken);
        if (lsku is null)
        {
            return await QuarantineAsync(claim, "LSKU_IDENTITY_NOT_FOUND", now, cancellationToken);
        }
        if (!CompleteStartProof(operation))
        {
            return await QuarantineAsync(claim, "LSKU_IDENTITY_WORKFLOW_START_PROOF_INVALID", now, cancellationToken);
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
            ObjectId = operation.LskuId,
            ObjectRef = operation.ObjectRef,
            SubmitterSubjectId = operation.MakerSubjectId,
            StartIdempotencyKey = operation.StartIdempotencyKey,
            StartRequestFingerprint = operation.OperationFingerprint,
            SubmittedAtUtc = submittedAt,
            DueAtUtc = operation.DueAtUtcTicksV1.HasValue
                ? new DateTimeOffset(operation.DueAtUtcTicksV1.Value, TimeSpan.Zero)
                : null
        };
        var audit = LskuIdentityLifecycleAuditIntentFactory.CreateSubmit(
            lsku, operation.ExpectedLskuVersion, binding);
        var result = await lskus.SubmitIdentityAsync(
            operation.LskuId,
            operation.ExpectedLskuVersion,
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
        if (result.Lsku is null
            || result.Lsku.Version != operation.ExpectedLskuVersion + 1
            || result.Lsku.LifecycleStatus != ProductIdentityLifecycleStatus.PendingIdentityApproval
            || !ExactBinding(result.Lsku.IdentityWorkflowBinding, binding)
            || result.Lsku.AuditIntents.Count(intent =>
                intent.Operation == ProductAuditOperation.LskuIdentitySubmitted
                && string.Equals(intent.IdempotencyKey, operation.StartIdempotencyKey,
                    StringComparison.Ordinal)) != 1)
        {
            return await QuarantineAsync(claim,
                "LSKU_IDENTITY_WORKFLOW_LOCAL_PENDING_INCONSISTENT", now, cancellationToken);
        }

        return await AdvanceAsync(
            claim,
            LskuIdentityWorkflowCheckpoint.LocalPendingApplied,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now,
            releaseLease: true,
            cancellationToken: cancellationToken);
    }

    private async Task<bool> ObserveDecisionAsync(
        LskuIdentityWorkflowOperation operation,
        LskuIdentityWorkflowClaim claim,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!operation.WorkflowInstanceId.HasValue)
        {
            return await QuarantineAsync(claim, "LSKU_IDENTITY_WORKFLOW_START_PROOF_INVALID", now, cancellationToken);
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
                LskuIdentityWorkflowCheckpoint.AwaitingDecision,
                result.ErrorCode ?? "LSKU_IDENTITY_WORKFLOW_DECISION_PENDING",
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
            || evidence.DecisionAt.Offset != TimeSpan.Zero
            || decision == ProductIdentityDecisionKind.Rejected
                && (string.IsNullOrWhiteSpace(evidence.ReasonCode)
                    || evidence.ReasonCode.Length > 128
                    || evidence.ReasonCode.Any(char.IsControl)
                    || evidence.ReasonCode != evidence.ReasonCode.Trim()))
        {
            return await QuarantineAsync(claim, "LSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        }

        return await operations.AdvanceAsync(
            claim,
            new(
                LskuIdentityWorkflowCheckpoint.DecisionObserved,
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
        LskuIdentityWorkflowOperation operation,
        LskuIdentityWorkflowClaim claim,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!TryRehydrateEvidence(operation, out var evidence))
        {
            return await QuarantineAsync(claim, "LSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        }
        if (evidence.Decision == ProductIdentityDecisionKind.Approved)
        {
            if (!ExactApprovalProof(operation))
            {
                return await QuarantineAsync(
                    claim, "LSKU_IDENTITY_WORKFLOW_APPROVAL_PROOF_INVALID", now, cancellationToken);
            }
            var validation = await ValidateApprovalDependenciesAsync(operation, cancellationToken);
            if (!validation.Succeeded)
            {
                return validation.Retryable
                    ? await ScheduleRetryAsync(
                        claim, operation.Checkpoint, validation.ErrorCode, now, retryDelay, cancellationToken)
                    : await QuarantineAsync(claim, validation.ErrorCode, now, cancellationToken);
            }
        }
        var lsku = await lskus.GetByIdAsync(operation.LskuId, cancellationToken);
        if (lsku is null)
        {
            return await QuarantineAsync(claim, "LSKU_IDENTITY_NOT_FOUND", now, cancellationToken);
        }

        var expectedVersion = checked(operation.ExpectedLskuVersion + 1);
        var audit = LskuIdentityLifecycleAuditIntentFactory.CreateDecision(lsku, expectedVersion, evidence);
        var result = await lskus.ReconcileIdentityDecisionAsync(
            operation.LskuId, expectedVersion, evidence, audit, cancellationToken);
        if (!result.Succeeded)
        {
            return result.ErrorCode == "AUDIT_INTENT_CAPACITY_EXCEEDED"
                ? await ScheduleRetryAsync(claim, operation.Checkpoint, result.ErrorCode, now, retryDelay,
                    cancellationToken)
                : await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
        }
        if (!ExactDecisionResult(result.Lsku, operation, evidence))
        {
            return await QuarantineAsync(claim,
                "LSKU_IDENTITY_WORKFLOW_DECISION_INCONSISTENT", now, cancellationToken);
        }

        return await AdvanceAsync(
            claim,
            LskuIdentityWorkflowCheckpoint.DecisionApplied,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now,
            releaseLease: true,
            cancellationToken: cancellationToken);
    }

    private async Task<bool> VerifyDecisionAppliedAndCompleteAsync(
        LskuIdentityWorkflowOperation operation,
        LskuIdentityWorkflowClaim claim,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!TryRehydrateEvidence(operation, out var evidence))
        {
            return await QuarantineAsync(claim,
                "LSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        }
        if (evidence.Decision == ProductIdentityDecisionKind.Approved)
        {
            if (!ExactApprovalProof(operation))
            {
                return await QuarantineAsync(
                    claim, "LSKU_IDENTITY_WORKFLOW_APPROVAL_PROOF_INVALID", now, cancellationToken);
            }
            var validation = await ValidateApprovalDependenciesAsync(operation, cancellationToken);
            if (!validation.Succeeded)
            {
                return validation.Retryable
                    ? await ScheduleRetryAsync(
                        claim, operation.Checkpoint, validation.ErrorCode, now,
                        retryDelay, cancellationToken)
                    : await QuarantineAsync(claim, validation.ErrorCode, now, cancellationToken);
            }
        }
        var lsku = await lskus.GetByIdAsync(operation.LskuId, cancellationToken);
        if (!ExactDecisionResult(lsku, operation, evidence))
        {
            return await QuarantineAsync(claim,
                "LSKU_IDENTITY_WORKFLOW_DECISION_INCONSISTENT", now, cancellationToken);
        }
        return await AdvanceAsync(claim, LskuIdentityWorkflowCheckpoint.Completed,
            ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true,
            cancellationToken: cancellationToken);
    }

    private async Task<bool> ValidateApprovalOrApplyRejectAsync(
        LskuIdentityWorkflowOperation operation,
        LskuIdentityWorkflowClaim claim,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!TryRehydrateEvidence(operation, out var evidence))
        {
            return await QuarantineAsync(
                claim, "LSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        }

        // Rejection is deliberately provider- and parent-independent.
        if (evidence.Decision == ProductIdentityDecisionKind.Rejected)
        {
            return await ApplyDecisionAsync(operation, claim, now, retryDelay, cancellationToken);
        }

        var validation = await ValidateApprovalDependenciesAsync(operation, cancellationToken);
        if (!validation.Succeeded || validation.Selection is null)
        {
            return validation.Retryable
                ? await ScheduleRetryAsync(
                    claim, operation.Checkpoint, validation.ErrorCode, now, retryDelay, cancellationToken)
                : await QuarantineAsync(claim, validation.ErrorCode, now, cancellationToken);
        }

        var proof = ApprovalProofFingerprint(
            operation.TenantId,
            operation.OperationId,
            operation.LskuId,
            operation.MarketCode,
            operation.DecisionTransitionSequence,
            validation.Selection,
            now.UtcTicks);
        return await operations.AdvanceAsync(
            claim,
            new(
                LskuIdentityWorkflowCheckpoint.ApprovalValidated,
                ProductIdentityWorkflowRecoveryDisposition.None,
                now.UtcTicks,
                ApprovalMarketSelection: validation.Selection,
                MarketValidatedAtUtcTicksV1: now.UtcTicks,
                ApprovalMarketProofFingerprint: proof,
                ReleaseLease: true),
            cancellationToken);
    }

    private async Task<ApprovalDependencyValidation> ValidateApprovalDependenciesAsync(
        LskuIdentityWorkflowOperation operation,
        CancellationToken cancellationToken)
    {
        var gsku = await gskus.GetByIdAsync(operation.GskuId, cancellationToken);
        if (gsku is null || gsku.IsDeleted || gsku.Id != operation.GskuId
            || gsku.ProductDefinitionRevisionId != operation.ProductDefinitionRevisionId
            || gsku.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved)
        {
            return new(false, false, "LSKU_IDENTITY_PARENT_NOT_REFERENCEABLE");
        }
        var revision = await revisions.GetByIdAsync(operation.ProductDefinitionRevisionId, cancellationToken);
        if (revision is null || revision.IsDeleted || revision.Id != operation.ProductDefinitionRevisionId
            || revision.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved)
        {
            return new(false, false, "LSKU_IDENTITY_PARENT_NOT_REFERENCEABLE");
        }

        var resolved = await marketResolver.ResolveLatestAsync(
            operation.TenantId, operation.MarketCode, cancellationToken);
        if (!resolved.IsSuccessful || resolved.Selection is null)
        {
            return new(
                false,
                resolved.StatusCode is 503 or 504,
                BoundedCode(resolved.FailureCode,
                    resolved.StatusCode == 504
                        ? "REFERENCE_PROVIDER_TIMEOUT"
                        : "REFERENCE_MARKET_NOT_FOUND"));
        }
        var value = resolved.Selection;
        if (value.SetCode != "market" || value.ValueCode != operation.MarketCode
            || value.CatalogVersionId == Guid.Empty || value.CatalogVersionNumber <= 0
            || value.ResolutionMode != "LATEST" || value.ResolvedAtUtc == default
            || value.ResolvedAtUtc.Offset != TimeSpan.Zero)
        {
            return new(false, false, "REFERENCE_MARKET_CONTRACT_CONFLICT");
        }
        return new(true, false, null, new ReferenceCatalogSelection
        {
            SetCode = value.SetCode,
            ValueCode = value.ValueCode,
            CatalogVersionId = value.CatalogVersionId,
            CatalogVersionNumber = value.CatalogVersionNumber,
            ResolutionMode = ReferenceCatalogResolutionMode.Latest,
            ResolvedAtUtc = value.ResolvedAtUtc
        });
    }

    private Task<bool> ScheduleRetryAsync(
        LskuIdentityWorkflowClaim claim,
        LskuIdentityWorkflowCheckpoint checkpoint,
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
                BoundedCode(errorCode, "LSKU_IDENTITY_WORKFLOW_PROVIDER_UNAVAILABLE"),
                ReleaseLease: true),
            cancellationToken);

    private Task<bool> QuarantineAsync(
        LskuIdentityWorkflowClaim claim,
        string? errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        AdvanceAsync(
            claim,
            LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired,
            now,
            BoundedCode(errorCode, "LSKU_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED"),
            releaseLease: true,
            cancellationToken: cancellationToken);

    private Task<bool> AdvanceAsync(
        LskuIdentityWorkflowClaim claim,
        LskuIdentityWorkflowCheckpoint checkpoint,
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

    private static bool CompleteStartProof(LskuIdentityWorkflowOperation operation) =>
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
        && actual.SubmittedAtUtc == expected.SubmittedAtUtc
        && actual.DueAtUtc == expected.DueAtUtc
        && actual.TerminalDecision is null
        && string.Equals(actual.ObjectType, expected.ObjectType, StringComparison.Ordinal)
        && string.Equals(actual.ObjectRef, expected.ObjectRef, StringComparison.Ordinal)
        && string.Equals(actual.StartIdempotencyKey, expected.StartIdempotencyKey, StringComparison.Ordinal)
        && string.Equals(actual.StartRequestFingerprint, expected.StartRequestFingerprint, StringComparison.Ordinal);

    private static bool ExactDecisionResult(
        Lsku? lsku,
        LskuIdentityWorkflowOperation operation,
        ProductIdentityWorkflowDecisionEvidence evidence)
    {
        if (lsku is null || lsku.Version != operation.ExpectedLskuVersion + 2
            || lsku.IdentityWorkflowBinding is not { } binding
            || !ExactOperationBinding(binding, operation)
            || binding.TerminalDecision is not { } persisted
            || !ExactTerminalDecision(persisted, evidence)
            || lsku.LifecycleStatus != (evidence.Decision == ProductIdentityDecisionKind.Approved
                ? ProductIdentityLifecycleStatus.IdentityApproved
                : ProductIdentityLifecycleStatus.Draft))
        {
            return false;
        }
        var expected = LskuIdentityLifecycleAuditIntentFactory.CreateDecision(
            lsku, operation.ExpectedLskuVersion + 1, evidence);
        var intents = lsku.AuditIntents.Where(intent => intent.IntentId == expected.IntentId).Take(2).ToArray();
        var receipts = lsku.AuditIntentReceipts
            .Where(receipt => receipt.IntentId == expected.IntentId).Take(2).ToArray();
        var exactIntent = intents.Length == 1 && receipts.Length == 0
            && SameImmutableAudit(intents[0], expected);
        var exactReceipt = intents.Length == 0 && receipts.Length == 1
            && receipts[0].TenantId == expected.TenantId
            && receipts[0].SourceService == expected.SourceService
            && receipts[0].IdempotencyKey == expected.IdempotencyKey
            && receipts[0].EvidenceHash == expected.EvidenceHash;
        return exactIntent || exactReceipt;
    }

    private static bool ExactOperationBinding(
        ProductIdentityWorkflowBinding binding,
        LskuIdentityWorkflowOperation operation) =>
        binding.WorkflowInstanceId == operation.WorkflowInstanceId
        && binding.WorkflowTemplateId == operation.WorkflowTemplateId
        && binding.WorkflowTemplateVersionId == operation.WorkflowTemplateVersionId
        && binding.ApprovalTaskId == operation.ApprovalTaskId
        && binding.AssignmentSnapshotId == operation.AssignmentSnapshotId
        && binding.StartTransitionLogId == operation.StartTransitionLogId
        && binding.ObjectType == operation.ObjectType
        && binding.ObjectId == operation.LskuId
        && binding.ObjectRef == operation.ObjectRef
        && binding.SubmitterSubjectId == operation.MakerSubjectId
        && binding.StartIdempotencyKey == operation.StartIdempotencyKey
        && binding.StartRequestFingerprint == operation.OperationFingerprint
        && binding.SubmittedAtUtc.UtcTicks == operation.WorkflowStartedAtUtcTicksV1
        && binding.DueAtUtc?.UtcTicks == operation.DueAtUtcTicksV1;

    private static bool ExactTerminalDecision(
        ProductIdentityWorkflowDecisionEvidence actual,
        ProductIdentityWorkflowDecisionEvidence expected) =>
        actual.Decision == expected.Decision
        && actual.WorkflowInstanceId == expected.WorkflowInstanceId
        && actual.ApprovalTaskId == expected.ApprovalTaskId
        && actual.WorkflowTemplateId == expected.WorkflowTemplateId
        && actual.WorkflowTemplateVersionId == expected.WorkflowTemplateVersionId
        && actual.ObjectType == expected.ObjectType
        && actual.ObjectId == expected.ObjectId
        && actual.ObjectRef == expected.ObjectRef
        && actual.DecisionActorSubjectId == expected.DecisionActorSubjectId
        && actual.ReasonCode == expected.ReasonCode
        && actual.DecisionAtUtc == expected.DecisionAtUtc
        && actual.TransitionSequence == expected.TransitionSequence
        && actual.TaskStatus == expected.TaskStatus
        && actual.InstanceStatus == expected.InstanceStatus;

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
        LskuIdentityWorkflowOperation operation,
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
        LskuIdentityWorkflowOperation operation,
        Guid tenantId,
        Guid lskuId,
        int expectedVersion,
        Guid makerSubjectId) =>
        !operation.IsDeleted && operation.TenantId == tenantId
        && operation.LskuId == lskuId
        && operation.ExpectedLskuVersion == expectedVersion
        && operation.MakerSubjectId == makerSubjectId
        && string.Equals(operation.ObjectType, LskuIdentityWorkflowStartRequestFactory.LskuObjectType,
            StringComparison.Ordinal)
        && string.Equals(operation.ObjectId, lskuId.ToString("D"), StringComparison.Ordinal);

    private static bool Retryable(ProductIdentityWorkflowTransportOutcome outcome) =>
        outcome is ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Timeout;

    private static bool ExactApprovalProof(LskuIdentityWorkflowOperation operation) =>
        operation.ApprovalMarketSelection is { } selection
        && selection.SetCode == "market"
        && selection.ValueCode == operation.MarketCode
        && selection.CatalogVersionId != Guid.Empty
        && selection.CatalogVersionNumber > 0
        && selection.ResolutionMode == ReferenceCatalogResolutionMode.Latest
        && selection.ResolvedAtUtc != default
        && selection.ResolvedAtUtc.Offset == TimeSpan.Zero
        && operation.MarketValidatedAtUtcTicksV1 is > 0
        && operation.ApprovalMarketProofFingerprint is { } fingerprint
        && fingerprint == ApprovalProofFingerprint(
            operation.TenantId,
            operation.OperationId,
            operation.LskuId,
            operation.MarketCode,
            operation.DecisionTransitionSequence,
            selection,
            operation.MarketValidatedAtUtcTicksV1.Value);

    private static string ApprovalProofFingerprint(
        Guid tenantId,
        Guid operationId,
        Guid lskuId,
        string marketCode,
        long? decisionTransitionSequence,
        ReferenceCatalogSelection selection,
        long validatedAtUtcTicks)
    {
        var facts = string.Join('|',
            "lsku-approval-market-v1",
            tenantId.ToString("D"),
            operationId.ToString("D"),
            lskuId.ToString("D"),
            marketCode,
            decisionTransitionSequence?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            selection.SetCode,
            selection.ValueCode,
            selection.CatalogVersionId.ToString("D"),
            selection.CatalogVersionNumber.ToString(CultureInfo.InvariantCulture),
            ((int)selection.ResolutionMode).ToString(CultureInfo.InvariantCulture),
            selection.ResolvedAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture),
            validatedAtUtcTicks.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(facts))).ToLowerInvariant();
    }

    private static bool SameImmutableAudit(LocalAuditIntent left, LocalAuditIntent right) =>
        left.SourceService == right.SourceService
        && left.SchemaVersion == right.SchemaVersion
        && left.ContractVersion == right.ContractVersion
        && left.IntentId == right.IntentId
        && left.TenantId == right.TenantId
        && left.AggregateType == right.AggregateType
        && left.AggregateId == right.AggregateId
        && left.PreVersion == right.PreVersion
        && left.PostVersion == right.PostVersion
        && left.Operation == right.Operation
        && left.ActorId == right.ActorId
        && left.CorrelationId == right.CorrelationId
        && left.CausationId == right.CausationId
        && left.CommandId == right.CommandId
        && left.Sequence == right.Sequence
        && left.TimestampUtc == right.TimestampUtc
        && left.TimestampUtcTicksV1 == right.TimestampUtcTicksV1
        && left.TemporalStorageVersion == right.TemporalStorageVersion
        && left.EvidenceHash == right.EvidenceHash
        && left.SnapshotReference == right.SnapshotReference
        && left.IdempotencyKey == right.IdempotencyKey;

    private static string BoundedCode(string? code, string fallback) =>
        !string.IsNullOrWhiteSpace(code) && code.Length <= 128 && code.All(character => !char.IsControl(character))
            ? code
            : fallback;

    private static LskuIdentityWorkflowProcessingResult Success(
        LskuIdentityWorkflowOperation operation,
        bool replay) => new(true, operation, null, 200, replay);

    private static LskuIdentityWorkflowProcessingResult Fail(string errorCode, int statusCode) =>
        new(false, null, errorCode, statusCode, false);

    private static LskuIdentityWorkflowProcessingResult Fail(
        LskuIdentityWorkflowOperation operation,
        string errorCode,
        int statusCode) => new(false, operation, errorCode, statusCode, false);

    private sealed record ApprovalDependencyValidation(
        bool Succeeded,
        bool Retryable,
        string? ErrorCode,
        ReferenceCatalogSelection? Selection = null);
}
