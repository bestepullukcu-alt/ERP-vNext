using System.Globalization;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record FinishedGoodIdentityWorkflowProcessingResult(
    bool Succeeded,
    FinishedGoodIdentityWorkflowOperation? Operation,
    string? ErrorCode,
    int StatusCode,
    bool IsReplay);

public sealed record FinishedGoodIdentityWorkflowExecutionConfiguration(
    TimeSpan LeaseDuration,
    TimeSpan RetryDelay);

public sealed class FinishedGoodIdentityWorkflowProcessor(
    IFinishedGoodIdentityWorkflowOperationRepository operations,
    IFinishedGoodRepository finishedGoods,
    IGskuRepository gskus,
    IProductDefinitionRevisionRepository revisions,
    IProductIdentityWorkflowClient workflowClient,
    FinishedGoodIdentityWorkflowStartRequestFactory requestFactory,
    TimeProvider timeProvider)
{
    private static readonly FinishedGoodIdentityWorkflowCheckpoint[] RecoverableCheckpoints =
    [
        FinishedGoodIdentityWorkflowCheckpoint.Prepared,
        FinishedGoodIdentityWorkflowCheckpoint.StartOutcomeUnknown,
        FinishedGoodIdentityWorkflowCheckpoint.WorkflowStarted,
        FinishedGoodIdentityWorkflowCheckpoint.LocalPendingApplied,
        FinishedGoodIdentityWorkflowCheckpoint.AwaitingDecision,
        FinishedGoodIdentityWorkflowCheckpoint.DecisionObserved,
        FinishedGoodIdentityWorkflowCheckpoint.ApprovalValidated,
        FinishedGoodIdentityWorkflowCheckpoint.DecisionApplied
    ];

    public async Task<FinishedGoodIdentityWorkflowProcessingResult> StartInteractiveAsync(
        Guid tenantId,
        Guid finishedGoodId,
        int expectedVersion,
        Guid operationId,
        Guid makerSubjectId,
        string delegatedUserToken,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || finishedGoodId == Guid.Empty || operationId == Guid.Empty
            || makerSubjectId == Guid.Empty || expectedVersion < 0
            || string.IsNullOrWhiteSpace(delegatedUserToken))
        {
            return Fail("FINISHED_GOOD_IDENTITY_WORKFLOW_START_INVALID", 400);
        }

        var existing = await operations.GetByOperationIdAsync(operationId, cancellationToken);
        FinishedGoodIdentityWorkflowOperation operation;
        var replay = existing is not null;
        if (existing is null)
        {
            var finishedGood = await finishedGoods.GetByIdAsync(finishedGoodId, cancellationToken);
            if (finishedGood is null)
            {
                return Fail("FINISHED_GOOD_IDENTITY_NOT_FOUND", 404);
            }

            var gsku = await gskus.GetByIdAsync(finishedGood.GskuId, cancellationToken);
            var revision = gsku is null
                ? null
                : await revisions.GetByIdAsync(gsku.ProductDefinitionRevisionId, cancellationToken);
            if (gsku is null || revision is null)
            {
                return Fail("FINISHED_GOOD_IDENTITY_PARENT_NOT_REFERENCEABLE", 409);
            }
            FinishedGoodIdentityWorkflowStartPlan plan;
            try
            {
                plan = requestFactory.Create(
                    tenantId, finishedGood, gsku, revision, operationId, expectedVersion, makerSubjectId);
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message, exception.Message == "FINISHED_GOOD_IDENTITY_WORKFLOW_CONFIGURATION_INVALID"
                    ? 503 : 409);
            }

            var reserved = await operations.ReserveAsync(plan.Operation, cancellationToken);
            if (!reserved.Succeeded || reserved.Operation is null)
            {
                return Fail(reserved.ErrorCode ?? "FINISHED_GOOD_IDENTITY_WORKFLOW_OPERATION_CONFLICT", 409);
            }

            operation = reserved.Operation;
            replay = reserved.IsReplay;
        }
        else
        {
            operation = existing;
        }

        if (!ExactInteractiveFacts(operation, tenantId, finishedGoodId, expectedVersion, makerSubjectId))
        {
            return Fail("FINISHED_GOOD_IDENTITY_WORKFLOW_OPERATION_CONFLICT", 409);
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

    public Task<FinishedGoodIdentityWorkflowProcessingResult> RecoverAsync(
        FinishedGoodIdentityWorkflowOperation operation,
        string leaseOwner,
        TimeSpan leaseDuration,
        TimeSpan retryDelay,
        CancellationToken cancellationToken = default) =>
        ProcessAsync(operation, null, leaseOwner, leaseDuration, retryDelay,
            stopWhenAwaitingDecision: false, cancellationToken);

    private async Task<FinishedGoodIdentityWorkflowProcessingResult> ProcessAsync(
        FinishedGoodIdentityWorkflowOperation initial,
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
            if (operation.Checkpoint == FinishedGoodIdentityWorkflowCheckpoint.Completed)
            {
                return Success(operation, true);
            }
            if (stopWhenAwaitingDecision
                && operation.Checkpoint == FinishedGoodIdentityWorkflowCheckpoint.AwaitingDecision)
            {
                return Success(operation, false);
            }
            if (operation.Checkpoint == FinishedGoodIdentityWorkflowCheckpoint.AwaitingMakerReplay
                && string.IsNullOrWhiteSpace(delegatedUserToken))
            {
                return Fail(operation, "FINISHED_GOOD_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED", 409);
            }
            if (operation.Checkpoint == FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired)
            {
                return Fail(operation, "FINISHED_GOOD_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED", 409);
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
                return Fail(operation, "FINISHED_GOOD_IDENTITY_WORKFLOW_BUSY", 409);
            }

            var advanced = operation.Checkpoint switch
            {
                FinishedGoodIdentityWorkflowCheckpoint.Prepared =>
                    await AdvanceAsync(claim, FinishedGoodIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                        ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true,
                        cancellationToken: cancellationToken),
                FinishedGoodIdentityWorkflowCheckpoint.StartOutcomeUnknown or
                    FinishedGoodIdentityWorkflowCheckpoint.AwaitingMakerReplay =>
                    await ResolveStartAsync(operation, claim, delegatedUserToken, now, retryDelay, cancellationToken),
                FinishedGoodIdentityWorkflowCheckpoint.WorkflowStarted =>
                    await ApplyLocalPendingAsync(operation, claim, now, retryDelay, cancellationToken),
                FinishedGoodIdentityWorkflowCheckpoint.LocalPendingApplied =>
                    await AdvanceAsync(claim, FinishedGoodIdentityWorkflowCheckpoint.AwaitingDecision,
                        ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true,
                        cancellationToken: cancellationToken),
                FinishedGoodIdentityWorkflowCheckpoint.AwaitingDecision =>
                    await ObserveDecisionAsync(operation, claim, now, retryDelay, cancellationToken),
                FinishedGoodIdentityWorkflowCheckpoint.DecisionObserved =>
                    await ValidateApprovalOrApplyRejectAsync(
                        operation, claim, now, retryDelay, cancellationToken),
                FinishedGoodIdentityWorkflowCheckpoint.ApprovalValidated =>
                    await ApplyDecisionAsync(operation, claim, now, retryDelay, cancellationToken),
                FinishedGoodIdentityWorkflowCheckpoint.DecisionApplied =>
                    await VerifyDecisionAppliedAndCompleteAsync(
                        operation, claim, now, retryDelay, cancellationToken),
                _ => false
            };
            if (!advanced)
            {
                return Fail(operation, "FINISHED_GOOD_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT", 409);
            }

            operation = await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken)
                ?? throw new InvalidOperationException("FINISHED_GOOD_IDENTITY_WORKFLOW_OPERATION_LOST");
            if (operation.NextAttemptAtUtcTicksV1 > timeProvider.GetUtcNow().UtcTicks)
            {
                return Fail(operation, operation.LastFailureCode ?? "FINISHED_GOOD_IDENTITY_WORKFLOW_RETRY_SCHEDULED", 503);
            }
        }

        return Fail(operation, "FINISHED_GOOD_IDENTITY_WORKFLOW_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<bool> ResolveStartAsync(
        FinishedGoodIdentityWorkflowOperation operation,
        FinishedGoodIdentityWorkflowClaim claim,
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
                FinishedGoodIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay,
                now,
                lookup.ErrorCode ?? "FINISHED_GOOD_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED",
                releaseLease: true,
                cancellationToken: cancellationToken);
        }

        if (Retryable(lookup.Outcome))
        {
            return await ScheduleRetryAsync(
                claim, FinishedGoodIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                lookup.ErrorCode, now, retryDelay, cancellationToken);
        }

        return await QuarantineAsync(claim, lookup.ErrorCode, now, cancellationToken);
    }

    private Task<bool> PersistStartedAsync(
        FinishedGoodIdentityWorkflowOperation operation,
        FinishedGoodIdentityWorkflowClaim claim,
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
            return QuarantineAsync(claim, "FINISHED_GOOD_IDENTITY_WORKFLOW_START_PROOF_INVALID", now, cancellationToken);
        }

        return operations.AdvanceAsync(
            claim,
            new(
                FinishedGoodIdentityWorkflowCheckpoint.WorkflowStarted,
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
        FinishedGoodIdentityWorkflowOperation operation,
        FinishedGoodIdentityWorkflowClaim claim,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        var finishedGood = await finishedGoods.GetByIdAsync(operation.FinishedGoodId, cancellationToken);
        if (finishedGood is null)
        {
            return await QuarantineAsync(claim, "FINISHED_GOOD_IDENTITY_NOT_FOUND", now, cancellationToken);
        }
        if (!CompleteStartProof(operation))
        {
            return await QuarantineAsync(claim, "FINISHED_GOOD_IDENTITY_WORKFLOW_START_PROOF_INVALID", now, cancellationToken);
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
            ObjectId = operation.FinishedGoodId,
            ObjectRef = operation.ObjectRef,
            SubmitterSubjectId = operation.MakerSubjectId,
            StartIdempotencyKey = operation.StartIdempotencyKey,
            StartRequestFingerprint = operation.OperationFingerprint,
            SubmittedAtUtc = submittedAt,
            DueAtUtc = operation.DueAtUtcTicksV1.HasValue
                ? new DateTimeOffset(operation.DueAtUtcTicksV1.Value, TimeSpan.Zero)
                : null
        };
        var audit = FinishedGoodIdentityLifecycleAuditIntentFactory.CreateSubmit(
            finishedGood, operation.ExpectedFinishedGoodVersion, binding);
        var result = await finishedGoods.SubmitIdentityAsync(
            operation.FinishedGoodId,
            operation.ExpectedFinishedGoodVersion,
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
        if (result.FinishedGood is null
            || result.FinishedGood.Version != operation.ExpectedFinishedGoodVersion + 1
            || result.FinishedGood.LifecycleStatus != ProductIdentityLifecycleStatus.PendingIdentityApproval
            || !ExactBinding(result.FinishedGood.IdentityWorkflowBinding, binding)
            || result.FinishedGood.AuditIntents.Count(intent =>
                intent.Operation == ProductAuditOperation.FinishedGoodIdentitySubmitted
                && string.Equals(intent.IdempotencyKey, operation.StartIdempotencyKey,
                    StringComparison.Ordinal)) != 1)
        {
            return await QuarantineAsync(claim,
                "FINISHED_GOOD_IDENTITY_WORKFLOW_LOCAL_PENDING_INCONSISTENT", now, cancellationToken);
        }

        return await AdvanceAsync(
            claim,
            FinishedGoodIdentityWorkflowCheckpoint.LocalPendingApplied,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now,
            releaseLease: true,
            cancellationToken: cancellationToken);
    }

    private async Task<bool> ObserveDecisionAsync(
        FinishedGoodIdentityWorkflowOperation operation,
        FinishedGoodIdentityWorkflowClaim claim,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!operation.WorkflowInstanceId.HasValue)
        {
            return await QuarantineAsync(claim, "FINISHED_GOOD_IDENTITY_WORKFLOW_START_PROOF_INVALID", now, cancellationToken);
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
                FinishedGoodIdentityWorkflowCheckpoint.AwaitingDecision,
                result.ErrorCode ?? "FINISHED_GOOD_IDENTITY_WORKFLOW_DECISION_PENDING",
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
            return await QuarantineAsync(claim, "FINISHED_GOOD_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        }

        return await operations.AdvanceAsync(
            claim,
            new(
                FinishedGoodIdentityWorkflowCheckpoint.DecisionObserved,
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
        FinishedGoodIdentityWorkflowOperation operation,
        FinishedGoodIdentityWorkflowClaim claim,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!TryRehydrateEvidence(operation, out var evidence))
        {
            return await QuarantineAsync(claim, "FINISHED_GOOD_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        }
        if (!TryRehydrateBinding(operation, out var expectedBinding))
        {
            return await QuarantineAsync(
                claim, "FINISHED_GOOD_IDENTITY_WORKFLOW_BINDING_CONFLICT", now, cancellationToken);
        }
        if (evidence.Decision == ProductIdentityDecisionKind.Approved)
        {
            if (!ExactApprovalProof(operation))
            {
                return await QuarantineAsync(
                    claim, "FINISHED_GOOD_IDENTITY_WORKFLOW_APPROVAL_PROOF_INVALID", now, cancellationToken);
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
        var finishedGood = await finishedGoods.GetByIdAsync(operation.FinishedGoodId, cancellationToken);
        if (finishedGood is null)
        {
            return await QuarantineAsync(claim, "FINISHED_GOOD_IDENTITY_NOT_FOUND", now, cancellationToken);
        }

        var expectedVersion = checked(operation.ExpectedFinishedGoodVersion + 1);
        var audit = FinishedGoodIdentityLifecycleAuditIntentFactory.CreateDecision(finishedGood, expectedVersion, evidence);
        var result = await finishedGoods.ReconcileIdentityDecisionAsync(
            operation.FinishedGoodId, expectedVersion, expectedBinding, evidence, audit, cancellationToken);
        if (!result.Succeeded)
        {
            return result.ErrorCode == "AUDIT_INTENT_CAPACITY_EXCEEDED"
                ? await ScheduleRetryAsync(claim, operation.Checkpoint, result.ErrorCode, now, retryDelay,
                    cancellationToken)
                : await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
        }
        if (!ExactDecisionResult(result.FinishedGood, operation, evidence))
        {
            return await QuarantineAsync(claim,
                "FINISHED_GOOD_IDENTITY_WORKFLOW_DECISION_INCONSISTENT", now, cancellationToken);
        }

        return await AdvanceAsync(
            claim,
            FinishedGoodIdentityWorkflowCheckpoint.DecisionApplied,
            ProductIdentityWorkflowRecoveryDisposition.None,
            now,
            releaseLease: true,
            cancellationToken: cancellationToken);
    }

    private async Task<bool> VerifyDecisionAppliedAndCompleteAsync(
        FinishedGoodIdentityWorkflowOperation operation,
        FinishedGoodIdentityWorkflowClaim claim,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!TryRehydrateEvidence(operation, out var evidence))
        {
            return await QuarantineAsync(claim,
                "FINISHED_GOOD_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        }
        if (evidence.Decision == ProductIdentityDecisionKind.Approved)
        {
            if (!ExactApprovalProof(operation))
            {
                return await QuarantineAsync(
                    claim, "FINISHED_GOOD_IDENTITY_WORKFLOW_APPROVAL_PROOF_INVALID", now, cancellationToken);
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
        var finishedGood = await finishedGoods.GetByIdAsync(operation.FinishedGoodId, cancellationToken);
        if (!ExactDecisionResult(finishedGood, operation, evidence))
        {
            return await QuarantineAsync(claim,
                "FINISHED_GOOD_IDENTITY_WORKFLOW_DECISION_INCONSISTENT", now, cancellationToken);
        }
        return await AdvanceAsync(claim, FinishedGoodIdentityWorkflowCheckpoint.Completed,
            ProductIdentityWorkflowRecoveryDisposition.None, now, releaseLease: true,
            cancellationToken: cancellationToken);
    }

    private async Task<bool> ValidateApprovalOrApplyRejectAsync(
        FinishedGoodIdentityWorkflowOperation operation,
        FinishedGoodIdentityWorkflowClaim claim,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!TryRehydrateEvidence(operation, out var evidence))
        {
            return await QuarantineAsync(
                claim, "FINISHED_GOOD_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        }

        // Rejection is deliberately provider- and parent-independent.
        if (evidence.Decision == ProductIdentityDecisionKind.Rejected)
        {
            return await ApplyDecisionAsync(operation, claim, now, retryDelay, cancellationToken);
        }

        var validation = await ValidateApprovalDependenciesAsync(operation, cancellationToken);
        if (!validation.Succeeded)
        {
            return validation.Retryable
                ? await ScheduleRetryAsync(
                    claim, operation.Checkpoint, validation.ErrorCode, now, retryDelay, cancellationToken)
                : await QuarantineAsync(claim, validation.ErrorCode, now, cancellationToken);
        }

        var proof = ApprovalProofFingerprint(
            operation.TenantId,
            operation.OperationId,
            operation.FinishedGoodId,
            operation.GskuId,
            operation.ProductDefinitionRevisionId,
            operation.DecisionTransitionSequence,
            now.UtcTicks);
        return await operations.AdvanceAsync(
            claim,
            new(
                FinishedGoodIdentityWorkflowCheckpoint.ApprovalValidated,
                ProductIdentityWorkflowRecoveryDisposition.None,
                now.UtcTicks,
                ApprovalParentValidatedAtUtcTicksV1: now.UtcTicks,
                ApprovalParentProofFingerprint: proof,
                ReleaseLease: true),
            cancellationToken);
    }

    private async Task<ApprovalDependencyValidation> ValidateApprovalDependenciesAsync(
        FinishedGoodIdentityWorkflowOperation operation,
        CancellationToken cancellationToken)
    {
        var gsku = await gskus.GetByIdAsync(operation.GskuId, cancellationToken);
        if (gsku is null || gsku.IsDeleted || gsku.TenantId != operation.TenantId
            || gsku.Id != operation.GskuId
            || gsku.ProductDefinitionRevisionId != operation.ProductDefinitionRevisionId
            || gsku.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved)
        {
            return new(false, false, "FINISHED_GOOD_IDENTITY_PARENT_NOT_REFERENCEABLE");
        }
        var revision = await revisions.GetByIdAsync(operation.ProductDefinitionRevisionId, cancellationToken);
        if (revision is null || revision.IsDeleted || revision.TenantId != operation.TenantId
            || revision.Id != operation.ProductDefinitionRevisionId
            || revision.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved)
        {
            return new(false, false, "FINISHED_GOOD_IDENTITY_PARENT_NOT_REFERENCEABLE");
        }

        return new(true, false, null);
    }

    private Task<bool> ScheduleRetryAsync(
        FinishedGoodIdentityWorkflowClaim claim,
        FinishedGoodIdentityWorkflowCheckpoint checkpoint,
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
                BoundedCode(errorCode, "FINISHED_GOOD_IDENTITY_WORKFLOW_PROVIDER_UNAVAILABLE"),
                ReleaseLease: true),
            cancellationToken);

    private Task<bool> QuarantineAsync(
        FinishedGoodIdentityWorkflowClaim claim,
        string? errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        AdvanceAsync(
            claim,
            FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired,
            now,
            BoundedCode(errorCode, "FINISHED_GOOD_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED"),
            releaseLease: true,
            cancellationToken: cancellationToken);

    private Task<bool> AdvanceAsync(
        FinishedGoodIdentityWorkflowClaim claim,
        FinishedGoodIdentityWorkflowCheckpoint checkpoint,
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

    private static bool CompleteStartProof(FinishedGoodIdentityWorkflowOperation operation) =>
        operation.WorkflowInstanceId is { } instanceId && instanceId != Guid.Empty
        && operation.WorkflowTemplateId is { } templateId && templateId != Guid.Empty
        && operation.WorkflowTemplateVersionId is { } versionId && versionId != Guid.Empty
        && operation.ApprovalTaskId is { } taskId && taskId != Guid.Empty
        && operation.AssignmentSnapshotId is { } snapshotId && snapshotId != Guid.Empty
        && operation.StartTransitionLogId is { } logId && logId != Guid.Empty
        && operation.WorkflowStartedAtUtcTicksV1 is > 0;

    private static bool TryRehydrateBinding(
        FinishedGoodIdentityWorkflowOperation operation,
        out ProductIdentityWorkflowBinding binding)
    {
        binding = null!;
        if (!CompleteStartProof(operation)) return false;
        binding = new ProductIdentityWorkflowBinding
        {
            WorkflowInstanceId = operation.WorkflowInstanceId!.Value,
            WorkflowTemplateId = operation.WorkflowTemplateId!.Value,
            WorkflowTemplateVersionId = operation.WorkflowTemplateVersionId!.Value,
            ApprovalTaskId = operation.ApprovalTaskId!.Value,
            AssignmentSnapshotId = operation.AssignmentSnapshotId!.Value,
            StartTransitionLogId = operation.StartTransitionLogId!.Value,
            ObjectType = operation.ObjectType,
            ObjectId = operation.FinishedGoodId,
            ObjectRef = operation.ObjectRef,
            SubmitterSubjectId = operation.MakerSubjectId,
            StartIdempotencyKey = operation.StartIdempotencyKey,
            StartRequestFingerprint = operation.OperationFingerprint,
            SubmittedAtUtc = new DateTimeOffset(operation.WorkflowStartedAtUtcTicksV1!.Value, TimeSpan.Zero),
            DueAtUtc = operation.DueAtUtcTicksV1.HasValue
                ? new DateTimeOffset(operation.DueAtUtcTicksV1.Value, TimeSpan.Zero)
                : null
        };
        return true;
    }

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
        FinishedGood? finishedGood,
        FinishedGoodIdentityWorkflowOperation operation,
        ProductIdentityWorkflowDecisionEvidence evidence)
    {
        if (finishedGood is null || finishedGood.Version != operation.ExpectedFinishedGoodVersion + 2
            || finishedGood.IdentityWorkflowBinding is not { } binding
            || !ExactOperationBinding(binding, operation)
            || binding.TerminalDecision is not { } persisted
            || !ExactTerminalDecision(persisted, evidence)
            || finishedGood.LifecycleStatus != (evidence.Decision == ProductIdentityDecisionKind.Approved
                ? ProductIdentityLifecycleStatus.IdentityApproved
                : ProductIdentityLifecycleStatus.Draft))
        {
            return false;
        }
        var expected = FinishedGoodIdentityLifecycleAuditIntentFactory.CreateDecision(
            finishedGood, operation.ExpectedFinishedGoodVersion + 1, evidence);
        var intents = finishedGood.AuditIntents.Where(intent => intent.IntentId == expected.IntentId).Take(2).ToArray();
        var receipts = finishedGood.AuditIntentReceipts
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
        FinishedGoodIdentityWorkflowOperation operation) =>
        binding.WorkflowInstanceId == operation.WorkflowInstanceId
        && binding.WorkflowTemplateId == operation.WorkflowTemplateId
        && binding.WorkflowTemplateVersionId == operation.WorkflowTemplateVersionId
        && binding.ApprovalTaskId == operation.ApprovalTaskId
        && binding.AssignmentSnapshotId == operation.AssignmentSnapshotId
        && binding.StartTransitionLogId == operation.StartTransitionLogId
        && binding.ObjectType == operation.ObjectType
        && binding.ObjectId == operation.FinishedGoodId
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
        FinishedGoodIdentityWorkflowOperation operation,
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
        FinishedGoodIdentityWorkflowOperation operation,
        Guid tenantId,
        Guid finishedGoodId,
        int expectedVersion,
        Guid makerSubjectId) =>
        !operation.IsDeleted && operation.TenantId == tenantId
        && operation.FinishedGoodId == finishedGoodId
        && operation.ExpectedFinishedGoodVersion == expectedVersion
        && operation.MakerSubjectId == makerSubjectId
        && string.Equals(operation.ObjectType, FinishedGoodIdentityWorkflowStartRequestFactory.FinishedGoodObjectType,
            StringComparison.Ordinal)
        && string.Equals(operation.ObjectId, finishedGoodId.ToString("D"), StringComparison.Ordinal);

    private static bool Retryable(ProductIdentityWorkflowTransportOutcome outcome) =>
        outcome is ProductIdentityWorkflowTransportOutcome.Retryable
            or ProductIdentityWorkflowTransportOutcome.Timeout;

    private static bool ExactApprovalProof(FinishedGoodIdentityWorkflowOperation operation) =>
        operation.ApprovalParentValidatedAtUtcTicksV1 is > 0
        && operation.ApprovalParentProofFingerprint is { } fingerprint
        && fingerprint == ApprovalProofFingerprint(
            operation.TenantId,
            operation.OperationId,
            operation.FinishedGoodId,
            operation.GskuId,
            operation.ProductDefinitionRevisionId,
            operation.DecisionTransitionSequence,
            operation.ApprovalParentValidatedAtUtcTicksV1.Value);

    private static string ApprovalProofFingerprint(
        Guid tenantId,
        Guid operationId,
        Guid finishedGoodId,
        Guid gskuId,
        Guid productDefinitionRevisionId,
        long? decisionTransitionSequence,
        long validatedAtUtcTicks)
    {
        var facts = string.Join('|',
            "finished-good-approval-parent-v1",
            tenantId.ToString("D"),
            operationId.ToString("D"),
            finishedGoodId.ToString("D"),
            gskuId.ToString("D"),
            productDefinitionRevisionId.ToString("D"),
            decisionTransitionSequence?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
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

    private static FinishedGoodIdentityWorkflowProcessingResult Success(
        FinishedGoodIdentityWorkflowOperation operation,
        bool replay) => new(true, operation, null, 200, replay);

    private static FinishedGoodIdentityWorkflowProcessingResult Fail(string errorCode, int statusCode) =>
        new(false, null, errorCode, statusCode, false);

    private static FinishedGoodIdentityWorkflowProcessingResult Fail(
        FinishedGoodIdentityWorkflowOperation operation,
        string errorCode,
        int statusCode) => new(false, operation, errorCode, statusCode, false);

    private sealed record ApprovalDependencyValidation(
        bool Succeeded,
        bool Retryable,
        string? ErrorCode);
}
