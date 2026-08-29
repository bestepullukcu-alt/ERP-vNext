using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;

public sealed record FirstGskuIdentityWorkflowProcessingResult(
    bool Succeeded,
    FirstGskuIdentityWorkflowOperation? Operation,
    string? ErrorCode,
    int StatusCode,
    bool IsReplay);

public sealed record FirstGskuIdentityWorkflowExecutionConfiguration(
    TimeSpan LeaseDuration,
    TimeSpan RetryDelay);

public sealed class FirstGskuIdentityWorkflowProcessor(
    IFirstGskuIdentityWorkflowOperationRepository operations,
    IProductDefinitionRevisionRepository revisions,
    IGskuRepository gskus,
    IGlobalProductRepository globalProducts,
    IVerifiedGskuReferenceResolver references,
    IProductIdentityWorkflowClient workflowClient,
    FirstGskuIdentityWorkflowStartRequestFactory requestFactory,
    TimeProvider timeProvider)
{
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
                return Fail(reserved.ErrorCode ?? "FIRST_GSKU_IDENTITY_WORKFLOW_OPERATION_CONFLICT", 409);
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
        ProcessAsync(operation, null, leaseOwner, leaseDuration, retryDelay,
            stopWhenAwaitingDecision: false, cancellationToken);

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
        for (var step = 0; step < 18; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.Completed)
            {
                return Success(operation, true);
            }
            if (stopWhenAwaitingDecision
                && operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision)
            {
                return Success(operation, false);
            }
            if (operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay
                && string.IsNullOrWhiteSpace(delegatedUserToken))
            {
                return Fail(operation, "FIRST_GSKU_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED", 409);
            }
            if (operation.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired)
            {
                return Fail(operation, "FIRST_GSKU_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED", 409);
            }

            var now = timeProvider.GetUtcNow();
            var claim = await operations.TryClaimAsync(new(
                operation.OperationId,
                operation.OperationFingerprint,
                [operation.Checkpoint],
                leaseOwner,
                now.UtcTicks,
                now.Add(leaseDuration).UtcTicks), cancellationToken);
            if (claim is null)
            {
                return Fail(operation, "FIRST_GSKU_IDENTITY_WORKFLOW_BUSY", 409);
            }

            var advanced = operation.Checkpoint switch
            {
                FirstGskuIdentityWorkflowCheckpoint.Prepared =>
                    await AdvanceAsync(claim, FirstGskuIdentityWorkflowCheckpoint.StartOutcomeUnknown,
                        now, releaseLease: true, cancellationToken: cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.StartOutcomeUnknown or
                    FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay =>
                    await ResolveStartAsync(operation, claim, delegatedUserToken, now, retryDelay, cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted =>
                    await ApplyRevisionPendingAsync(operation, claim, now, cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.RevisionPendingApplied =>
                    await ApplyGskuPendingAsync(operation, claim, now, cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.PairPendingApplied =>
                    await AdvanceAsync(claim, FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision,
                        now, releaseLease: true, cancellationToken: cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision =>
                    await ObserveDecisionAsync(operation, claim, now, retryDelay, cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.DecisionObserved =>
                    await BeginDecisionAsync(operation, claim, now, retryDelay, cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.ApprovalValidated =>
                    await ApplyRevisionApprovalAsync(operation, claim, now, retryDelay, cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.RevisionApproved =>
                    await ApplyGskuApprovalAsync(operation, claim, now, retryDelay, cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.PairApproved or
                    FirstGskuIdentityWorkflowCheckpoint.PairDraftRestored =>
                    await VerifyAndCompleteAsync(operation, claim, now, retryDelay, cancellationToken),
                FirstGskuIdentityWorkflowCheckpoint.GskuDraftRestored =>
                    await ApplyRevisionRejectionAsync(operation, claim, now, cancellationToken),
                _ => false
            };
            if (!advanced)
            {
                return Fail(operation, "FIRST_GSKU_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT", 409);
            }

            operation = await operations.GetByOperationIdAsync(operation.OperationId, cancellationToken)
                ?? throw new InvalidOperationException("FIRST_GSKU_IDENTITY_WORKFLOW_OPERATION_LOST");
            if (operation.NextAttemptAtUtcTicksV1 > timeProvider.GetUtcNow().UtcTicks)
            {
                return Fail(operation,
                    operation.LastFailureCode ?? "FIRST_GSKU_IDENTITY_WORKFLOW_RETRY_SCHEDULED", 503);
            }
        }

        return Fail(operation, "FIRST_GSKU_IDENTITY_WORKFLOW_STEP_BUDGET_EXCEEDED", 503);
    }

    private async Task<bool> ResolveStartAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        string? delegatedUserToken,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(delegatedUserToken))
        {
            var started = await workflowClient.StartAsync(
                operation.TenantId, requestFactory.Rehydrate(operation), delegatedUserToken, cancellationToken);
            if (started.Outcome == ProductIdentityWorkflowTransportOutcome.Success)
            {
                return await PersistStartedAsync(operation, claim, started.Value, now, cancellationToken);
            }
            if (TerminalTransportFailure(started.Outcome))
            {
                return await QuarantineAsync(claim, started.ErrorCode, now, cancellationToken);
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
            return await operations.AdvanceAsync(claim, new(
                FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
                ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay,
                now.UtcTicks,
                LastFailureCode: BoundedCode(lookup.ErrorCode,
                    "FIRST_GSKU_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED"),
                ReleaseLease: true), cancellationToken);
        }
        return Retryable(lookup.Outcome)
            ? await ScheduleRetryAsync(claim, claim.Checkpoint, lookup.ErrorCode, now, retryDelay, cancellationToken)
            : await QuarantineAsync(claim, lookup.ErrorCode, now, cancellationToken);
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
            : await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
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
            : await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
    }

    private async Task<bool> ObserveDecisionAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
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
        if (outcome.Outcome == ProductIdentityWorkflowTransportOutcome.NonTerminal)
        {
            return await ScheduleRetryAsync(claim, claim.Checkpoint, outcome.ErrorCode, now, retryDelay, cancellationToken);
        }
        if (outcome.Outcome != ProductIdentityWorkflowTransportOutcome.Success || outcome.Value is null)
        {
            return Retryable(outcome.Outcome)
                ? await ScheduleRetryAsync(claim, claim.Checkpoint, outcome.ErrorCode, now, retryDelay, cancellationToken)
                : await QuarantineAsync(claim, outcome.ErrorCode, now, cancellationToken);
        }
        var evidence = outcome.Value;
        if (!TryMapDecision(evidence.TerminalAction, out var decision)
            || !Guid.TryParse(evidence.ActorUserId, out var actorId) || actorId == Guid.Empty
            || actorId == operation.MakerSubjectId
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
            ReleaseLease: true), cancellationToken);
    }

    private async Task<bool> BeginDecisionAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
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
                : await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
        }

        var approval = await RevalidateApprovalAsync(operation, cancellationToken);
        if (!approval.Succeeded)
        {
            return approval.Retryable
                ? await ScheduleRetryAsync(claim, claim.Checkpoint, approval.ErrorCode, now, retryDelay, cancellationToken)
                : await QuarantineAsync(claim, approval.ErrorCode, now, cancellationToken);
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
            ReleaseLease: true), cancellationToken);
    }

    private async Task<bool> ApplyRevisionApprovalAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
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
        if (!approval.Succeeded)
        {
            return approval.Retryable
                ? await ScheduleRetryAsync(claim, claim.Checkpoint, approval.ErrorCode, now, retryDelay, cancellationToken)
                : await QuarantineAsync(claim, approval.ErrorCode, now, cancellationToken);
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
            : await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
    }

    private async Task<bool> ApplyGskuApprovalAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        if (!TryEvidence(operation, out var evidence) || !TryBinding(operation, out var binding))
        {
            return await QuarantineAsync(claim, "FIRST_GSKU_IDENTITY_WORKFLOW_EVIDENCE_CONFLICT", now, cancellationToken);
        }
        binding.TerminalDecision = evidence;
        var approval = await RevalidateApprovalAsync(operation, cancellationToken);
        if (!approval.Succeeded)
        {
            return approval.Retryable
                ? await ScheduleRetryAsync(claim, claim.Checkpoint, approval.ErrorCode, now, retryDelay, cancellationToken)
                : await QuarantineAsync(claim, approval.ErrorCode, now, cancellationToken);
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
            gsku!, operation.ExpectedGskuVersion + 1, binding, evidence);
        var result = await gskus.ApproveIdentityAsync(
            gsku!.Id, operation.ExpectedGskuVersion + 1, binding, intent, cancellationToken);
        return result.Succeeded && ExactApprovedGsku(result.Aggregate, operation)
            ? await AdvanceAsync(claim, FirstGskuIdentityWorkflowCheckpoint.PairApproved,
                now, releaseLease: true, cancellationToken: cancellationToken)
            : await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
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
            : await QuarantineAsync(claim, result.ErrorCode, now, cancellationToken);
    }

    private async Task<bool> VerifyAndCompleteAsync(
        FirstGskuIdentityWorkflowOperation operation,
        FirstGskuIdentityWorkflowClaim claim,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        var revision = await revisions.GetByIdAsync(operation.ProductDefinitionRevisionId, cancellationToken);
        var gsku = await gskus.GetByIdAsync(operation.GskuId, cancellationToken);
        var exact = false;
        if (operation.DecisionKind == ProductIdentityDecisionKind.Approved)
        {
            var approval = await RevalidateApprovalAsync(operation, cancellationToken);
            if (!approval.Succeeded)
            {
                return approval.Retryable
                    ? await ScheduleRetryAsync(claim, claim.Checkpoint, approval.ErrorCode, now, retryDelay, cancellationToken)
                    : await QuarantineAsync(claim, approval.ErrorCode, now, cancellationToken);
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

    private async Task<ApprovalValidation> RevalidateApprovalAsync(
        FirstGskuIdentityWorkflowOperation operation,
        CancellationToken cancellationToken)
    {
        var parent = await globalProducts.GetByIdAsync(operation.GlobalProductId, cancellationToken);
        if (parent is null || parent.LifecycleStatus != ProductIdentityLifecycleStatus.IdentityApproved)
        {
            return new(false, false, "FIRST_GSKU_IDENTITY_PARENT_NOT_APPROVED");
        }
        VerifiedGskuReferenceResolveResult result;
        try
        {
            result = await references.ResolveLatestAsync(
                operation.PackApplicabilityCode, operation.PackUomCode, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new(false, true, "REFERENCE_DATA_CONTRACT_UNAVAILABLE");
        }
        if (!result.IsSuccessful)
        {
            return new(false, result.StatusCode is 503 or 504,
                BoundedCode(result.FailureCode, "REFERENCE_DATA_CONTRACT_UNAVAILABLE"));
        }
        var applicability = ExactSelection(result.Selections, "pack-applicability", operation.PackApplicabilityCode);
        var uom = ExactSelection(result.Selections, "uom", operation.PackUomCode);
        return applicability is not null && uom is not null && result.Selections.Count == 2
            ? new(true, false, null, ToSelection(applicability), ToSelection(uom))
            : new(false, false, "REFERENCE_DATA_CONTRACT_CONFLICT", null, null);
    }

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

    private Task<bool> ScheduleRetryAsync(
        FirstGskuIdentityWorkflowClaim claim,
        FirstGskuIdentityWorkflowCheckpoint checkpoint,
        string? errorCode,
        DateTimeOffset now,
        TimeSpan retryDelay,
        CancellationToken cancellationToken) =>
        operations.AdvanceAsync(claim, new(
            checkpoint,
            ProductIdentityWorkflowRecoveryDisposition.Retryable,
            now.UtcTicks,
            now.Add(retryDelay).UtcTicks,
            BoundedCode(errorCode, "FIRST_GSKU_IDENTITY_WORKFLOW_PROVIDER_UNAVAILABLE"),
            ReleaseLease: true), cancellationToken);

    private Task<bool> QuarantineAsync(
        FirstGskuIdentityWorkflowClaim claim,
        string? errorCode,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        operations.AdvanceAsync(claim, new(
            FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired,
            now.UtcTicks,
            LastFailureCode: BoundedCode(errorCode, "FIRST_GSKU_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED"),
            ReleaseLease: true), cancellationToken);

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
            gsku, operation.ExpectedGskuVersion + 1, binding, evidence);
        return revision.AuditIntents.Count(intent => ExactImmutableAuditIntent(intent, expectedRevision)) == 1
            && gsku.AuditIntents.Count(intent => ExactImmutableAuditIntent(intent, expectedGsku)) == 1;
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

    private static bool TryMapDecision(string action, out ProductIdentityDecisionKind decision)
    {
        if (action == "Approve") { decision = ProductIdentityDecisionKind.Approved; return true; }
        if (action == "Reject") { decision = ProductIdentityDecisionKind.Rejected; return true; }
        decision = default;
        return false;
    }

    private static bool ExactTerminalStatuses(
        ProductIdentityDecisionKind decision, string taskStatus, string instanceStatus) => decision switch
        {
            ProductIdentityDecisionKind.Approved => taskStatus == "Approved" && instanceStatus == "Completed",
            ProductIdentityDecisionKind.Rejected => taskStatus == "Rejected" && instanceStatus == "Rejected",
            _ => false
        };

    private static bool TerminalTransportFailure(ProductIdentityWorkflowTransportOutcome outcome) =>
        outcome is ProductIdentityWorkflowTransportOutcome.Forbidden
            or ProductIdentityWorkflowTransportOutcome.Invalid
            or ProductIdentityWorkflowTransportOutcome.Conflict
            or ProductIdentityWorkflowTransportOutcome.AuthenticationRejected;

    private static bool Retryable(ProductIdentityWorkflowTransportOutcome outcome) =>
        outcome is ProductIdentityWorkflowTransportOutcome.Retryable or ProductIdentityWorkflowTransportOutcome.Timeout;

    private static string BoundedCode(string? code, string fallback) =>
        !string.IsNullOrWhiteSpace(code) && code.Length <= 128 && code.All(character => !char.IsControl(character))
            ? code : fallback;

    private static FirstGskuIdentityWorkflowProcessingResult Success(
        FirstGskuIdentityWorkflowOperation operation, bool replay) => new(true, operation, null, 200, replay);

    private static FirstGskuIdentityWorkflowProcessingResult Fail(string errorCode, int statusCode) =>
        new(false, null, errorCode, statusCode, false);

    private static FirstGskuIdentityWorkflowProcessingResult Fail(
        FirstGskuIdentityWorkflowOperation operation, string errorCode, int statusCode) =>
        new(false, operation, errorCode, statusCode, false);

    private sealed record ApprovalValidation(
        bool Succeeded,
        bool Retryable,
        string? ErrorCode,
        ReferenceCatalogSelection? Applicability = null,
        ReferenceCatalogSelection? Uom = null);
}
