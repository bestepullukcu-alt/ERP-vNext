using System.Reflection;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Xunit;
using PersistedCancellationEvidence = Diten.MdmService.Domain.ValueObjects.ProductIdentityWorkflowCancellationEvidence;
using TransportCancellationEvidence = Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowCancellationEvidence;

namespace Diten.MdmService.Application.Tests;

public sealed class FirstGskuIdentityWorkflowProcessorTests
{
    [Theory]
    [InlineData(FirstGskuIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart)]
    [InlineData(FirstGskuIdentityWorkflowCheckpoint.Superseded)]
    public async Task Recover_OperatorTerminalRecoveryState_IsPermanentNoOp(
        FirstGskuIdentityWorkflowCheckpoint checkpoint)
    {
        var harness = new Harness();
        var operation = new FirstGskuIdentityWorkflowStartRequestFactory(
                new(TemplateId, null, [ApproverId], "IDENTITY_APPROVAL", true, true, null),
                TimeProvider.System)
            .Create(TenantId, harness.Revision, harness.Gsku, OperationId, 0, MakerId).Operation;
        operation.Checkpoint = checkpoint;
        harness.Operations.Current = operation;

        var result = await harness.Processor.RecoverAsync(
            operation, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.True(result.Succeeded);
        Assert.True(result.IsReplay);
        Assert.Same(operation, result.Operation);
    }

    [Fact]
    public async Task Lost_start_response_is_recovered_and_approval_applies_pair_once()
    {
        var harness = new Harness();
        harness.Client.StartOutcome = ProductIdentityWorkflowTransportOutcome.Timeout;

        var submitted = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.True(submitted.Succeeded);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision, submitted.Operation!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Revision.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Gsku.LifecycleStatus);
        Assert.Equal(1, harness.Client.StartResultCalls);

        var approved = await harness.Processor.RecoverAsync(
            submitted.Operation, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        var replay = await harness.Processor.RecoverAsync(
            harness.Operations.Current!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.True(approved.Succeeded);
        Assert.True(replay.Succeeded && replay.IsReplay);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.Completed, harness.Operations.Current!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, harness.Revision.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, harness.Gsku.LifecycleStatus);
        Assert.Single(harness.Revision.AuditIntents,
            item => item.Operation == ProductAuditOperation.ProductDefinitionRevisionIdentityApproved);
        Assert.Single(harness.Gsku.AuditIntents,
            item => item.Operation == ProductAuditOperation.GskuIdentityApproved);
        Assert.NotNull(harness.Operations.Current.ApprovalReferenceProofFingerprint);
    }

    [Fact]
    public async Task Crash_after_revision_pending_replays_without_duplicate_audit()
    {
        var harness = new Harness();
        harness.Operations.FailNextAdvanceTo = FirstGskuIdentityWorkflowCheckpoint.RevisionPendingApplied;

        var first = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(first.Succeeded);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.WorkflowStarted, harness.Operations.Current!.Checkpoint);
        Assert.Single(harness.Revision.AuditIntents,
            item => item.Operation == ProductAuditOperation.ProductDefinitionRevisionIdentitySubmitted);

        var recovered = await harness.Processor.RecoverAsync(
            harness.Operations.Current, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.True(recovered.Succeeded);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.Completed, harness.Operations.Current!.Checkpoint);
        Assert.Single(harness.Revision.AuditIntents,
            item => item.Operation == ProductAuditOperation.ProductDefinitionRevisionIdentitySubmitted);
        Assert.Single(harness.Gsku.AuditIntents,
            item => item.Operation == ProductAuditOperation.GskuIdentitySubmitted);
    }

    // B8. History: before FIX1 the maker's own decision quarantined the pair; FIX1 ignored it and kept waiting; FIX2
    // item 3 makes it not applicable (the engine has closed the approval — waiting cannot end).
    [Fact]
    public async Task A_decision_by_the_maker_returns_the_pair_to_draft_and_is_not_applied()
    {
        // Changed by FIX2 item 3: FIX1 ignored the decision and kept the pair waiting — but the engine had closed the
        // approval with that decision, nothing else would come, and the GSKU stayed pending for ever. Now the decision
        // is not applicable: the pair is a draft again (one write, with the audit intents that say why).
        var harness = new Harness { TerminalActorId = MakerId };
        var submitted = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        await harness.Processor.RecoverAsync(
            submitted.Operation!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        var operation = harness.Operations.Current!;
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.DecisionNotApplicable, operation.Checkpoint);
        Assert.Equal(FirstGskuIdentityWorkflowProcessor.SelfApprovalIgnored, operation.LastFailureCode);
        Assert.Equal(1, harness.Operations.NotApplicableWrites);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, harness.Revision.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, harness.Gsku.LifecycleStatus);
        var intent = Assert.Single(harness.Gsku.AuditIntents,
            item => item.Operation == ProductAuditOperation.GskuIdentityDecisionNotApplicable);
        Assert.Equal(MakerId.ToString("D"), intent.ActorId);
        Assert.DoesNotContain(harness.Gsku.AuditIntents, item => item.Operation == ProductAuditOperation.GskuIdentityApproved);
        Assert.Contains(harness.Log.Entries, entry => entry.Level == LogLevel.Warning
            && entry.Message.Contains(FirstGskuIdentityWorkflowProcessor.SelfApprovalIgnored, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Approval_reference_provider_unavailable_schedules_retry_without_approving_pair()
    {
        var harness = new Harness();
        harness.References.Result = VerifiedGskuReferenceResolveResult.Fail(
            503, "REFERENCE_DATA_CONTRACT_UNAVAILABLE");
        var submitted = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        var result = await harness.Processor.RecoverAsync(
            submitted.Operation!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(result.Succeeded);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.DecisionObserved,
            harness.Operations.Current!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Revision.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Gsku.LifecycleStatus);
        Assert.DoesNotContain(harness.Revision.AuditIntents,
            item => item.Operation == ProductAuditOperation.ProductDefinitionRevisionIdentityApproved);
        Assert.DoesNotContain(harness.Gsku.AuditIntents,
            item => item.Operation == ProductAuditOperation.GskuIdentityApproved);
    }

    // B2 (b) (GSKU FIX1). Previously the parent's withdrawn approval quarantined the pair (a resume needed the
    // maker's exact replay, and the screen offered none). The engine approved and MDM cannot apply it: both are
    // drafts again, the reason is on the operation and in the audit intents, and the operation is final.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_approval_that_can_no_longer_be_applied_returns_the_pair_to_draft_with_its_reason(bool unitRetired)
    {
        var harness = new Harness();
        var submitted = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        // Between the submit and the decision: the parent loses its approval, or the unit is retired.
        if (unitRetired) harness.References.Result = ReferenceResolver.Success(2, retiredUnit: true);
        else harness.Parent.LifecycleStatus = ProductIdentityLifecycleStatus.Draft;
        // FIX2 item 2: a retired unit now reaches the processor as the real client says it — "not selectable".
        var code = unitRetired ? VerifiedGskuReferenceResolveResult.NotSelectable : "FIRST_GSKU_IDENTITY_PARENT_NOT_APPROVED";

        var decided = await harness.Processor.RecoverAsync(
            submitted.Operation!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        var operation = harness.Operations.Current!;
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.DecisionNotApplicable, operation.Checkpoint);
        Assert.Equal(code, operation.LastFailureCode);
        Assert.Equal((409, code), (decided.StatusCode, decided.ErrorCode));
        Assert.Equal(1, harness.Operations.NotApplicableWrites);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, harness.Revision.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, harness.Gsku.LifecycleStatus);
        Assert.Single(harness.Revision.AuditIntents,
            item => item.Operation == ProductAuditOperation.ProductDefinitionRevisionIdentityDecisionNotApplicable);
        Assert.Single(harness.Gsku.AuditIntents,
            item => item.Operation == ProductAuditOperation.GskuIdentityDecisionNotApplicable);
        Assert.DoesNotContain(harness.Gsku.AuditIntents, item => item.Operation == ProductAuditOperation.GskuIdentityApproved);

        // Final: the same submit replayed gets the same reason and starts nothing.
        var replay = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        Assert.Equal((409, code), (replay.StatusCode, replay.ErrorCode));
        Assert.Equal(1, harness.Client.StartCalls);
    }

    // A reference service that refuses THIS service is the chain's fault, not the record's: it waits.
    [Theory]
    [InlineData(401, "REFERENCE_UNAUTHENTICATED")]
    [InlineData(403, "REFERENCE_FORBIDDEN")]
    public async Task A_reference_service_that_refuses_this_service_waits_and_then_applies(int statusCode, string failureCode)
    {
        var harness = new Harness();
        harness.References.Result = VerifiedGskuReferenceResolveResult.Fail(statusCode, failureCode);
        var submitted = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        var waiting = await harness.Processor.RecoverAsync(
            submitted.Operation!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        var operation = harness.Operations.Current!;
        Assert.False(waiting.Succeeded);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.DecisionObserved, operation.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, operation.RecoveryDisposition);
        Assert.Equal(1, operation.ServiceFaultCount);
        Assert.Equal(failureCode, operation.LastFailureCode);

        harness.References.Result = ReferenceResolver.Success(2);
        operation.NextAttemptAtUtcTicksV1 = null;
        var applied = await harness.Processor.RecoverAsync(
            operation, "worker-2", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.True(applied.Succeeded, applied.ErrorCode);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.Completed, harness.Operations.Current!.Checkpoint);
        Assert.Equal(0, harness.Operations.Current.ServiceFaultCount);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, harness.Gsku.LifecycleStatus);
    }

    // Rows an earlier version quarantined for the parent or a refused reference still resume by the maker's replay.
    [Theory]
    [InlineData("FIRST_GSKU_IDENTITY_PARENT_NOT_APPROVED")]
    [InlineData("REFERENCE_UNAUTHENTICATED")]
    public async Task Legacy_rows_quarantined_for_the_parent_or_a_refused_reference_still_resume_by_exact_replay(string code)
    {
        var harness = new Harness();
        harness.References.Result = VerifiedGskuReferenceResolveResult.Fail(503, "REFERENCE_DATA_CONTRACT_UNAVAILABLE");
        var submitted = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        await harness.Processor.RecoverAsync(
            submitted.Operation!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        var legacy = harness.Operations.Current!;
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.DecisionObserved, legacy.Checkpoint); // the decision is read
        legacy.Checkpoint = FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired; // as the old code left it
        legacy.RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired;
        legacy.LastFailureCode = code;
        legacy.NextAttemptAtUtcTicksV1 = null;
        harness.References.Result = ReferenceResolver.Success(2);

        var background = await harness.Processor.RecoverAsync(
            legacy, "worker-2", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        var replay = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.Equal(409, background.StatusCode);
        Assert.True(replay.Succeeded, replay.ErrorCode);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.Completed, replay.Operation!.Checkpoint);
        Assert.Equal(1, harness.Client.StartCalls);
    }

    [Fact]
    public async Task Persistent_reference_auth_failure_is_answered_at_once_and_does_not_spin()
    {
        var harness = new Harness();
        harness.References.Result = VerifiedGskuReferenceResolveResult.Fail(403, "REFERENCE_FORBIDDEN");
        var submitted = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        await harness.Processor.RecoverAsync(
            submitted.Operation!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        var replay = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.Equal((503, ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit), (replay.StatusCode, replay.ErrorCode));
        Assert.Equal(2, harness.References.Calls); // one per attempt — no spinning to the step budget
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.DecisionObserved, harness.Operations.Current!.Checkpoint);
    }

    [Fact]
    public async Task Unrelated_manual_reconciliation_cannot_resume_by_interactive_replay()
    {
        var harness = new Harness { TerminalActorId = Guid.Empty }; // evidence that names nobody: a fault of the record
        var submitted = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        var quarantined = await harness.Processor.RecoverAsync(
            submitted.Operation!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        var replay = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(quarantined.Succeeded);
        Assert.False(replay.Succeeded);
        // FIX3 E2: the submitter's repeat reads the engine again (the approval was decided, the pair is pending), puts
        // the operation back to its decision — and the evidence still names nobody: the quarantine stands, and the
        // answer says a person has to reconcile it (FIX2 answered "reconciliation required", i.e. "submit again").
        Assert.Equal(FirstGskuIdentityWorkflowProcessor.ManualReconciliationNeeded, replay.ErrorCode);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            replay.Operation!.Checkpoint);
        Assert.Equal(1, harness.Client.StartCalls);
    }

    [Fact]
    public async Task Reference_drift_after_revision_approval_blocks_gsku_approval()
    {
        var harness = new Harness();
        harness.References.Results.Enqueue(ReferenceResolver.Success(version: 2));
        harness.References.Results.Enqueue(ReferenceResolver.Success(version: 2));
        harness.References.Results.Enqueue(ReferenceResolver.Success(version: 3));
        var submitted = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        var result = await harness.Processor.RecoverAsync(
            submitted.Operation!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(result.Succeeded);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            harness.Operations.Current!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, harness.Revision.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Gsku.LifecycleStatus);
        Assert.DoesNotContain(harness.Gsku.AuditIntents,
            item => item.Operation == ProductAuditOperation.GskuIdentityApproved);
    }

    [Fact]
    public async Task Reference_drift_before_completion_prevents_false_completion()
    {
        var harness = new Harness();
        harness.References.Results.Enqueue(ReferenceResolver.Success(version: 2));
        harness.References.Results.Enqueue(ReferenceResolver.Success(version: 2));
        harness.References.Results.Enqueue(ReferenceResolver.Success(version: 2));
        harness.References.Results.Enqueue(ReferenceResolver.Success(version: 3));
        var submitted = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        var result = await harness.Processor.RecoverAsync(
            submitted.Operation!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(result.Succeeded);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            harness.Operations.Current!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, harness.Revision.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, harness.Gsku.LifecycleStatus);
    }

    [Fact]
    public async Task Crash_after_revision_approval_cannot_replace_persisted_proof_with_new_catalog_version()
    {
        var harness = new Harness();
        harness.Operations.FailNextAdvanceTo = FirstGskuIdentityWorkflowCheckpoint.RevisionApproved;
        harness.References.Results.Enqueue(ReferenceResolver.Success(version: 2));
        harness.References.Results.Enqueue(ReferenceResolver.Success(version: 2));
        harness.References.Results.Enqueue(ReferenceResolver.Success(version: 3));
        var submitted = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        var crashed = await harness.Processor.RecoverAsync(
            submitted.Operation!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        var recovered = await harness.Processor.RecoverAsync(
            harness.Operations.Current!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(crashed.Succeeded);
        Assert.False(recovered.Succeeded);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            harness.Operations.Current!.Checkpoint);
        Assert.Equal(2, harness.Operations.Current.ApprovalPackUomSelection!.CatalogVersionNumber);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, harness.Revision.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Gsku.LifecycleStatus);
    }

    [Fact]
    public async Task WithdrawInteractive_VerifiedCancellation_RestoresPairToDraftAndReplaysExactly()
    {
        var harness = new Harness();
        var submitted = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        Assert.True(submitted.Succeeded);

        var withdrawalId = Guid.NewGuid();
        var first = await harness.Processor.WithdrawInteractiveAsync(
            TenantId, GskuId, 1, withdrawalId, MakerId, "REQUESTER_WITHDRAWAL", "Changed my mind",
            "maker-token", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        var replay = await harness.Processor.WithdrawInteractiveAsync(
            TenantId, GskuId, 1, withdrawalId, MakerId, "REQUESTER_WITHDRAWAL", "Changed my mind",
            "maker-token", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, harness.Revision.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, harness.Gsku.LifecycleStatus);
        Assert.Equal(2, harness.Revision.Version);
        Assert.Equal(2, harness.Gsku.Version);
        Assert.Equal(1, harness.Operations.WithdrawalApplyCount);
        Assert.Single(harness.Revision.AuditIntents,
            x => x.Operation == ProductAuditOperation.ProductDefinitionRevisionIdentityApprovalWithdrawn);
        Assert.Single(harness.Gsku.AuditIntents,
            x => x.Operation == ProductAuditOperation.GskuIdentityApprovalWithdrawn);
    }

    [Fact]
    public async Task WithdrawInteractive_NonMaker_IsDeniedWithoutRemoteOrLocalMutation()
    {
        var harness = new Harness();
        await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        var result = await harness.Processor.WithdrawInteractiveAsync(
            TenantId, GskuId, 1, Guid.NewGuid(), ApproverId, "REQUESTER_WITHDRAWAL", null,
            "approver-token", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(result.Succeeded);
        Assert.Equal(403, result.StatusCode);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Revision.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Gsku.LifecycleStatus);
        Assert.Equal(0, harness.Client.CancellationCalls);
        Assert.Equal(0, harness.Operations.WithdrawalApplyCount);
    }

    [Fact]
    public async Task WithdrawInteractive_Timeout_LeavesAtomicPairPendingAndRecoverable()
    {
        var harness = new Harness();
        await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        harness.Client.CancellationOutcome = ProductIdentityWorkflowTransportOutcome.Timeout;

        var result = await harness.Processor.WithdrawInteractiveAsync(
            TenantId, GskuId, 1, Guid.NewGuid(), MakerId, "REQUESTER_WITHDRAWAL", null,
            "maker-token", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(result.Succeeded);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown,
            harness.Operations.Current!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Revision.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Gsku.LifecycleStatus);
        Assert.Equal(0, harness.Operations.WithdrawalApplyCount);
    }

    // ── GSKU FIX1 · B1 / B9 / A3 / A1 / 2(a) ─────────────────────────────────────────────────────────────────────

    private static async Task<Harness> PendingPair()
    {
        var harness = new Harness();
        var submitted = await harness.Processor.StartInteractiveAsync(TenantId, GskuId, 0, OperationId, MakerId,
            "maker-token", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        Assert.True(submitted.Succeeded, submitted.ErrorCode);
        return harness;
    }

    private static Task<FirstGskuIdentityWorkflowProcessingResult> Withdraw(Harness harness, Guid commandId) =>
        harness.Processor.WithdrawInteractiveAsync(TenantId, GskuId, 1, commandId, MakerId,
            "REQUESTER_WITHDRAWAL", null, "maker-token", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

    // Rule IDP:952 (named by WP-MDM-GSKU-ACCEPT-01): a reference service that is down or slow (503 / 504) is waited
    // for — the decision stays observed and nothing is approved, returned to draft or quarantined.
    [Theory]
    [InlineData(503)]
    [InlineData(504)]
    public async Task IDP_A_reference_outage_at_approval_is_waited_for_and_changes_nothing(int status)
    {
        var harness = new Harness();
        harness.References.Result = VerifiedGskuReferenceResolveResult.Fail(status, "REFERENCE_DATA_CONTRACT_UNAVAILABLE");
        var submitted = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        var result = await harness.Processor.RecoverAsync(
            submitted.Operation!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(result.Succeeded);
        var operation = harness.Operations.Current!;
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.DecisionObserved, operation.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, operation.RecoveryDisposition);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Gsku.LifecycleStatus);
        Assert.Equal(0, harness.Operations.NotApplicableWrites);
    }

    // Rule IDP:386 (named by WP-MDM-GSKU-ACCEPT-01): a cancellation the engine answers with a conflict or "not
    // found" means the approval is still the engine's — the pair goes back to awaiting the decision, never to
    // manual reconciliation, and the GSKU stays pending.
    [Theory]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Conflict, "WORKFLOW_TRUSTED_CANCEL_CONFLICT")]
    [InlineData(ProductIdentityWorkflowTransportOutcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND")]
    public async Task IDP_A_cancellation_answered_conflict_or_not_found_returns_to_awaiting_the_decision(
        ProductIdentityWorkflowTransportOutcome outcome, string code)
    {
        var harness = await PendingPair();
        harness.Client.Cancel = _ => (outcome, code);

        var result = await Withdraw(harness, Guid.NewGuid());

        Assert.False(result.Succeeded);
        var operation = harness.Operations.Current!;
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision, operation.Checkpoint);
        Assert.NotEqual(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired, operation.RecoveryDisposition);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Gsku.LifecycleStatus);
    }

    // B1: the task changed between the read of its versions and the cancellation (claimed, reassigned, delegated).
    // The approval is still open: the withdrawal is asked again with the versions read after the conflict.
    [Fact]
    public async Task Withdraw_WhenTheTaskChangedInBetween_IsAskedAgainWithFreshVersions_AndWithdraws()
    {
        var harness = await PendingPair();
        harness.Client.Preflight = call => (ProductIdentityWorkflowTransportOutcome.Success, "", 3 + call, 5 + call);
        harness.Client.Cancel = call => call == 1
            ? (ProductIdentityWorkflowTransportOutcome.Conflict, "WORKFLOW_TRUSTED_CANCEL_CONFLICT")
            : null;

        var result = await Withdraw(harness, Guid.NewGuid());

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal([(4, 6), (6, 8)], harness.Client.CancelledWithVersions);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, harness.Gsku.LifecycleStatus);
    }

    // B1: it keeps changing — the requester is told so, every withdrawal field is forgotten, and the same command
    // as well as a new one go through afterwards. Previously the fields stayed and the GSKU was locked for good.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Withdraw_WhenTheTaskKeepsChanging_IsAnswered_ForgetsTheWithdrawal_AndCanBeAskedAgain(bool newCommand)
    {
        var harness = await PendingPair();
        var commandId = Guid.NewGuid();
        harness.Client.Cancel = call => call <= 2
            ? (ProductIdentityWorkflowTransportOutcome.Conflict, "WORKFLOW_TRUSTED_CANCEL_CONFLICT")
            : null;

        var refused = await Withdraw(harness, commandId);

        var operation = harness.Operations.Current!;
        Assert.Equal((409, ProductIdentityWorkflowMakerCodes.ApprovalChanging), (refused.StatusCode, refused.ErrorCode));
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision, operation.Checkpoint);
        var withdrawalFields = typeof(FirstGskuIdentityWorkflowOperation).GetProperties()
            .Where(property => property.Name.StartsWith("Withdrawal", StringComparison.Ordinal)).ToList();
        Assert.Equal(18, withdrawalFields.Count);
        Assert.All(withdrawalFields, property => Assert.True(property.GetValue(operation) is null, property.Name));

        var again = await Withdraw(harness, newCommand ? Guid.NewGuid() : commandId);

        Assert.True(again.Succeeded, again.ErrorCode);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, harness.Gsku.LifecycleStatus);
    }

    // B1: the approval was decided or closed first — nothing to withdraw; the same answer for a repeat.
    [Fact]
    public async Task Withdraw_WhenTheApprovalIsClosed_HasNothingToWithdraw_AndSaysTheSameEveryTime()
    {
        var harness = await PendingPair();
        var commandId = Guid.NewGuid();
        harness.Client.Preflight = _ => (ProductIdentityWorkflowTransportOutcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND", 0, 0);

        var first = await Withdraw(harness, commandId);
        var again = await Withdraw(harness, commandId);

        Assert.Equal((409, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND"), (first.StatusCode, first.ErrorCode));
        Assert.Equal((409, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND"), (again.StatusCode, again.ErrorCode));
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision, harness.Operations.Current!.Checkpoint);
        Assert.Equal("WORKFLOW_TRUSTED_CANCEL_NOT_FOUND", harness.Operations.Current.WithdrawalRefusalCode);
        Assert.Equal(0, harness.Client.CancellationCalls);
    }

    // Shared item 14 (A1) on GSKU: told the command the earlier withdrawal stands under.
    [Fact]
    public async Task Withdraw_UnderAnotherCommand_IsToldTheCommandItsWithdrawalStandsUnder()
    {
        var harness = await PendingPair();
        var first = Guid.NewGuid();
        harness.Client.CancellationOutcome = ProductIdentityWorkflowTransportOutcome.Timeout;
        await Withdraw(harness, first);

        var other = await Withdraw(harness, Guid.NewGuid());

        Assert.Equal((409, "FIRST_GSKU_IDENTITY_WITHDRAWAL_OPERATION_CONFLICT"), (other.StatusCode, other.ErrorCode));
        Assert.Equal(first, other.PendingWithdrawalCommandId);
    }

    // B2 (a): no submit before the parent is approved — nothing written, nothing started.
    [Fact]
    public async Task Submit_BeforeTheParentIsApproved_IsRefused_AndStartsNothing()
    {
        var harness = new Harness();
        harness.Parent.LifecycleStatus = ProductIdentityLifecycleStatus.Draft;

        var result = await harness.Processor.StartInteractiveAsync(TenantId, GskuId, 0, OperationId, MakerId,
            "maker-token", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.Equal((409, FirstGskuIdentityWorkflowProcessor.ParentNotApproved), (result.StatusCode, result.ErrorCode));
        Assert.Null(harness.Operations.Current);
        Assert.Equal(0, harness.Client.StartCalls);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, harness.Gsku.LifecycleStatus);
    }

    // Shared item 13 (A3) on GSKU: a claim the store never answers does not hang the request.
    [Fact]
    public async Task Submit_WhoseClaimTheStoreNeverAnswers_IsAnsweredWithinTheBound_AndGivesTheLeaseBack()
    {
        var harness = await PendingPair();
        harness.Operations.ClaimsNeverAnswer = true;
        harness.Operations.Current!.Checkpoint = FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay;

        var result = await harness.Processor.StartInteractiveAsync(TenantId, GskuId, 0, OperationId, MakerId,
            "maker-token", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5));

        Assert.Equal((503, ProductIdentityWorkflowClaims.StoreUnavailable), (result.StatusCode, result.ErrorCode));
        Assert.Equal(1, harness.Operations.ReleaseIfOwnedCalls);
    }

    // The shared classification on GSKU: a refused service identity at the start is not the record's fault.
    [Theory]
    [InlineData(ProductIdentityWorkflowTransportOutcome.AuthenticationRejected)]
    [InlineData(ProductIdentityWorkflowTransportOutcome.Forbidden)]
    public async Task Submit_WhoseServiceIdentityIsRefused_IsNotQuarantined_AndTheMakerIsToldToResubmit(
        ProductIdentityWorkflowTransportOutcome outcome)
    {
        var harness = new Harness();
        harness.Client.StartOutcome = outcome;

        var result = await harness.Processor.StartInteractiveAsync(TenantId, GskuId, 0, OperationId, MakerId,
            "maker-token", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.Equal((503, ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit), (result.StatusCode, result.ErrorCode));
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay, harness.Operations.Current!.Checkpoint);
        Assert.NotEqual(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired,
            harness.Operations.Current.RecoveryDisposition);
    }

    // FIX2 item 7: once the audit intents of the decision were delivered and compacted to receipts, the read-back that
    // completes the operation still recognises them — FIX1 counted only intents and quarantined a finished decision.
    [Fact]
    public async Task IDP_A_decision_whose_intents_became_receipts_still_completes()
    {
        var harness = await PendingPair();
        harness.Operations.FailNextAdvanceTo = FirstGskuIdentityWorkflowCheckpoint.Completed;
        await harness.Processor.RecoverAsync(harness.Operations.Current!, "worker-1",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.PairApproved, harness.Operations.Current!.Checkpoint);
        Compact(harness.Revision);
        Compact(harness.Gsku);

        var completed = await harness.Processor.RecoverAsync(harness.Operations.Current!, "worker-1",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.True(completed.Succeeded, completed.ErrorCode);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.Completed, harness.Operations.Current!.Checkpoint);
    }

    // FIX2 item 7: an approval write refused because the GSKU is at its audit-intent capacity waits for delivery; it
    // is not a quarantine (FIX1 said "binding conflict" and the processor quarantined the pair).
    [Fact]
    public async Task IDP_An_approval_write_at_the_audit_intent_capacity_waits_and_is_not_quarantined()
    {
        var harness = await PendingPair();
        while (harness.Gsku.AuditIntents.Count < AuditIntentLimits.MaxPerAggregate)
            harness.Gsku.AuditIntents.Add(new LocalAuditIntent { IdempotencyKey = $"filler-{harness.Gsku.AuditIntents.Count}" });

        await harness.Processor.RecoverAsync(harness.Operations.Current!, "worker-1",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        var operation = harness.Operations.Current!;
        Assert.NotEqual(FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired, operation.Checkpoint);
        Assert.Equal("AUDIT_INTENT_CAPACITY_REACHED", operation.LastFailureCode);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.Retryable, operation.RecoveryDisposition);
    }

    // FIX2 item 12 (B1, the accepted GP rule): a cancellation answered "not found" means the approval is closed — the
    // request is told so, the same command gets the same answer, and a new command is told the command the earlier
    // withdrawal stands under (its repeat is what the screen offers). The task-changed (conflict, still open) path —
    // where the same and a new command DO withdraw — is Withdraw_WhenTheTaskKeepsChanging_….
    [Fact]
    public async Task IDP_A_cancellation_answered_not_found_gives_the_same_answer_to_the_same_command_and_names_it_to_a_new_one()
    {
        var harness = await PendingPair();
        var commandId = Guid.NewGuid();
        harness.Client.Cancel = _ => (ProductIdentityWorkflowTransportOutcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND");

        var first = await Withdraw(harness, commandId);
        var same = await Withdraw(harness, commandId);
        var other = await Withdraw(harness, Guid.NewGuid());

        Assert.Equal((409, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND"), (first.StatusCode, first.ErrorCode));
        Assert.Equal((409, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND"), (same.StatusCode, same.ErrorCode));
        Assert.Equal((409, "FIRST_GSKU_IDENTITY_WITHDRAWAL_OPERATION_CONFLICT"), (other.StatusCode, other.ErrorCode));
        Assert.Equal(commandId, other.PendingWithdrawalCommandId);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Gsku.LifecycleStatus);
    }

    // The 72-hour rule on GSKU (untested in FIX1), at its exact edge on both sides: a withdrawal that landed and waits
    // for its requester is handed to reconciliation AT 72 hours, not a tick earlier.
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public async Task IDP_The_72_hour_limit_holds_exactly_at_its_edge(long ticksShort, bool handedOver)
    {
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        var harness = new Harness(new FixedClock(now));
        Assert.True((await harness.Processor.StartInteractiveAsync(TenantId, GskuId, 0, OperationId, MakerId,
            "maker-token", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5))).Succeeded);
        harness.Client.CancellationOutcome = ProductIdentityWorkflowTransportOutcome.Timeout;
        await Withdraw(harness, Guid.NewGuid());
        var operation = harness.Operations.Current!;
        operation.LeaseOwner = null;
        operation.NextAttemptAtUtcTicksV1 = null;
        operation.WithdrawalAwaitingRequesterSinceUtcTicksV1 =
            now.UtcTicks - ProductIdentityWorkflowFaultRules.WithdrawalRequesterWaitLimit.Ticks + ticksShort;
        harness.Client.Preflight = _ => (ProductIdentityWorkflowTransportOutcome.NotFound, "WORKFLOW_TRUSTED_CANCEL_NOT_FOUND", 0, 0);
        harness.Client.TerminalFailure = ProductIdentityWorkflowTransportOutcome.Conflict;

        await harness.Processor.RecoverAsync(operation, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.Equal(handedOver, harness.Operations.Current!.LastFailureCode
            == FirstGskuIdentityWorkflowProcessor.WithdrawalRequesterNeverReturned);
    }

    // IDP:104 — the parent gate refuses every parent that is not approved, not only a draft one.
    [Theory]
    [InlineData(ProductIdentityLifecycleStatus.PendingIdentityApproval)]
    [InlineData(ProductIdentityLifecycleStatus.Retired)]
    public async Task IDP_Submit_under_a_pending_or_retired_global_product_is_refused(ProductIdentityLifecycleStatus parent)
    {
        var harness = new Harness();
        harness.Parent.LifecycleStatus = parent;

        var result = await harness.Processor.StartInteractiveAsync(TenantId, GskuId, 0, OperationId, MakerId,
            "maker-token", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.Equal((409, FirstGskuIdentityWorkflowProcessor.ParentNotApproved), (result.StatusCode, result.ErrorCode));
        Assert.Equal(0, harness.Client.StartCalls);
    }

    // IDP:280 (B9) — an interactive withdrawal is not held back by the worker's retry schedule (the fake now keeps
    // the schedule as the store does, so this can fail).
    [Fact]
    public async Task IDP_An_interactive_withdrawal_passes_the_workers_retry_schedule()
    {
        var harness = await PendingPair();
        harness.Operations.Current!.NextAttemptAtUtcTicksV1 = DateTimeOffset.UtcNow.AddHours(1).UtcTicks;

        var result = await Withdraw(harness, Guid.NewGuid());

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, harness.Gsku.LifecycleStatus);
    }

    private static void Compact(IAuditIntentAggregate aggregate)
    {
        foreach (var intent in aggregate.AuditIntents)
            aggregate.AuditIntentReceipts.Add(new LocalAuditIntentReceipt
            {
                SourceService = "mdm", IntentId = intent.IntentId, TenantId = intent.TenantId,
                IdempotencyKey = intent.IdempotencyKey, CentralAcknowledgement = "ack",
                CentralIdempotencyKey = intent.IdempotencyKey, ContractVersion = "1",
                AcknowledgedAt = DateTimeOffset.UtcNow, DeliveredAt = DateTimeOffset.UtcNow,
                CompactedAt = DateTimeOffset.UtcNow, CompactReceiptReference = $"receipt-{intent.IntentId:N}",
                EvidenceHash = intent.EvidenceHash
            });
        aggregate.AuditIntents.Clear();
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Harness
    {
        public Harness(TimeProvider? clock = null)
        {
            Revision = new()
            {
                Id = RevisionId, TenantId = TenantId, GlobalProductId = ProductId,
                RevisionIdentifier = "REV-001", CreationCommandId = "CREATE-1", Version = 0
            };
            Gsku = new()
            {
                Id = GskuId, TenantId = TenantId, ProductDefinitionRevisionId = RevisionId,
                CanonicalCode = "GS-0001", CreationCommandId = "CREATE-1", Version = 0,
                PackApplicabilityCode = "SCALAR_QUANTITY_APPLIES", PackQuantity = 1m, PackUomCode = "EA",
                PackApplicabilitySelection = Selection("pack-applicability", "SCALAR_QUANTITY_APPLIES"),
                PackUomSelection = Selection("uom", "EA")
            };
            Operations.Revision = Revision;
            Operations.Gsku = Gsku;
            Revisions = new PairRevisionRepository(Revision);
            Gskus = new PairGskuRepository(Gsku);
            Parent = new GlobalProduct
            {
                Id = ProductId, TenantId = TenantId, CanonicalCode = "GP-0001",
                GlobalProductName = "Product", GlobalProductNameNormalized = "PRODUCT",
                LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved
            };
            var products = DispatchProxy.Create<IGlobalProductRepository, RepositoryProxy>();
            ((RepositoryProxy)(object)products).Handler = (method, args) => method.Name == "GetByIdAsync"
                ? Task.FromResult<GlobalProduct?>((Guid)args![0]! == ProductId ? Parent : null)
                : throw new NotSupportedException(method.Name);
            Client = new(this);
            References = new();
            Processor = new(
                Operations, Revisions, Gskus, products, References, Client,
                new(new(TemplateId, null, [ApproverId], "IDENTITY_APPROVAL", true, true, null),
                    clock ?? TimeProvider.System),
                clock ?? TimeProvider.System,
                ProductIdentityWorkflowServiceFaultSettings.Default,
                Log);
        }

        public CapturingLogger<FirstGskuIdentityWorkflowProcessor> Log { get; } = new();

        public FirstGskuIdentityWorkflowProcessor Processor { get; }
        public FakeOperationRepository Operations { get; } = new();
        public PairRevisionRepository Revisions { get; }
        public PairGskuRepository Gskus { get; }
        public FakeWorkflowClient Client { get; }
        public ReferenceResolver References { get; }
        public ProductDefinitionRevision Revision { get; }
        public Gsku Gsku { get; }
        public GlobalProduct Parent { get; }
        public Guid TerminalActorId { get; set; } = ApproverId;
    }

    private sealed class FakeWorkflowClient(Harness harness) : IProductIdentityWorkflowClient
    {
        public ProductIdentityWorkflowTransportOutcome StartOutcome { get; set; } = ProductIdentityWorkflowTransportOutcome.Success;
        public int StartCalls { get; private set; }
        public int StartResultCalls { get; private set; }
        public int CancellationCalls { get; private set; }
        public ProductIdentityWorkflowTransportOutcome CancellationOutcome { get; set; } =
            ProductIdentityWorkflowTransportOutcome.Success;
        public int PreflightCalls { get; private set; }
        public List<(int Instance, int Task)> CancelledWithVersions { get; } = [];
        // Scripted answers by call number (1-based); null falls back to the default answer.
        public Func<int, (ProductIdentityWorkflowTransportOutcome Outcome, string Code, int Instance, int Task)?>? Preflight { get; set; }
        public Func<int, (ProductIdentityWorkflowTransportOutcome Outcome, string Code)?>? Cancel { get; set; }
        public bool StartNeverAnswers { get; set; }
        public ProductIdentityWorkflowTransportOutcome? TerminalFailure { get; set; }

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> StartAsync(
            Guid tenantId, ProductIdentityWorkflowStartRequest request, string delegatedUserToken,
            CancellationToken cancellationToken = default)
        {
            StartCalls++;
            return Task.FromResult(
                StartOutcome == ProductIdentityWorkflowTransportOutcome.Success
                    ? ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(StartResult())
                    : ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Fail(StartOutcome, "TIMEOUT"));
        }

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> GetStartResultAsync(
            Guid tenantId, ProductIdentityWorkflowStartResultRequest request,
            CancellationToken cancellationToken = default)
        {
            StartResultCalls++;
            Assert.Equal("gsku", request.ExpectedObjectType);
            Assert.Equal(GskuId.ToString("D"), request.ExpectedObjectId);
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(StartResult()));
        }

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>> GetTerminalEvidenceAsync(
            Guid tenantId, ProductIdentityWorkflowTerminalEvidenceRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(TerminalFailure is { } failure
            ? ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Fail(
                failure, "WORKFLOW_TERMINAL_EVIDENCE_INCONSISTENT")
            : ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(new(
                WorkflowId, TaskId, TemplateId, TemplateVersionId, "gsku", GskuId.ToString("D"), "GS-0001",
                "Approve", harness.TerminalActorId.ToString("D"), "APPROVED", DateTimeOffset.UtcNow,
                2, "Approved", "Completed", "corr")));

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>> GetCancellationPreflightAsync(
            Guid tenantId, ProductIdentityWorkflowCancellationPreflightRequest request,
            CancellationToken cancellationToken = default)
        {
            var scripted = Preflight?.Invoke(++PreflightCalls);
            if (scripted is { } answer && answer.Outcome != ProductIdentityWorkflowTransportOutcome.Success)
                return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>.Fail(
                    answer.Outcome, answer.Code));
            return Task.FromResult(
                ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>.Success(new(
                    request.WorkflowInstanceId, request.ApprovalTaskId, request.ExpectedObjectType,
                    request.ExpectedObjectId, "GS-0001", scripted?.Instance ?? 3, scripted?.Task ?? 5, "Active",
                    "WaitingApproval")));
        }

        public Task<ProductIdentityWorkflowTransportResult<TransportCancellationEvidence>> CancelAsync(
            Guid tenantId, ProductIdentityWorkflowCancellationRequest request, string delegatedUserToken,
            CancellationToken cancellationToken = default)
        {
            CancellationCalls++;
            CancelledWithVersions.Add((request.ExpectedWorkflowInstanceVersion, request.ExpectedApprovalTaskVersion));
            if (Cancel?.Invoke(CancellationCalls) is { } scripted)
                return Task.FromResult(ProductIdentityWorkflowTransportResult<TransportCancellationEvidence>.Fail(
                    scripted.Outcome, scripted.Code));
            if (CancellationOutcome != ProductIdentityWorkflowTransportOutcome.Success)
                return Task.FromResult(ProductIdentityWorkflowTransportResult<TransportCancellationEvidence>.Fail(
                    CancellationOutcome, "WORKFLOW_TIMEOUT"));
            return Task.FromResult(ProductIdentityWorkflowTransportResult<TransportCancellationEvidence>.Success(new(
                request.WorkflowInstanceId, request.ApprovalTaskId, TemplateId, TemplateVersionId,
                request.ExpectedObjectType, request.ExpectedObjectId, "GS-0001", "Cancel", MakerId,
                request.ReasonCode, request.Comment, DateTimeOffset.UtcNow, 3, Guid.NewGuid(), "Cancelled",
                "Cancelled", 4, 6, false, "corr")));
        }

        private static ProductIdentityWorkflowStartResult StartResult() => new(
            WorkflowId, TemplateId, TemplateVersionId, TaskId, SnapshotId, StartLogId,
            "GS-0001", "Running", "Approval", "Review", DateTimeOffset.UtcNow, null, false, "corr");
    }

    private sealed class ReferenceResolver : IWorkflowVerifiedGskuReferenceResolver
    {
        public VerifiedGskuReferenceResolveResult? Result { get; set; }
        public Queue<VerifiedGskuReferenceResolveResult> Results { get; } = new();
        public int Calls { get; private set; }

        public Task<VerifiedGskuReferenceResolveResult> ResolveLatestAsync(
            Guid tenantId, string packApplicabilityValueCode, string uomValueCode,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : Result ?? Success(2));
        }

        // FIX2 item 2: a retired unit is answered as the real resolver client answers it — "not selectable", a 409 of
        // its own — never as a success carrying a retired selection (which no production client returns, and which
        // let the B2 (b) test pass while the production path quarantined).
        public static VerifiedGskuReferenceResolveResult Success(int version, bool retiredUnit = false) =>
            retiredUnit
                ? VerifiedGskuReferenceResolveResult.Fail(409, VerifiedGskuReferenceResolveResult.NotSelectable)
                : VerifiedGskuReferenceResolveResult.Success([
                    Verified("pack-applicability", "SCALAR_QUANTITY_APPLIES", version, 1),
                    Verified("uom", "EA", version, 2, retiredUnit)]);

        private static VerifiedGskuReferenceSelection Verified(
            string set, string code, int version, int discriminator, bool retired = false) => new(
            set, code,
            Guid.Parse($"a0000000-0000-0000-0000-{version:D8}{discriminator:D4}"),
            version, "LATEST", StableReferenceTime, retired, !retired);
    }

    private sealed class PairRevisionRepository(ProductDefinitionRevision value) : IProductDefinitionRevisionRepository
    {
        public Task<ProductDefinitionRevision?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(id == value.Id ? value : null);
        public Task<ProductDefinitionRevision?> GetByCreationCommandIdAsync(string id, CancellationToken ct = default) =>
            Task.FromResult<ProductDefinitionRevision?>(null);
        public Task<FirstGskuPairAllocationResult> AllocateForFirstGskuAsync(Guid id, string command, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ProductDefinitionRevisionCreateResult> CreateForFirstGskuAsync(ProductDefinitionRevision revision, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<FirstGskuIdentityLifecycleMutationResult<ProductDefinitionRevision>> MarkIdentityPendingAsync(Guid id, int expected, FirstGskuIdentityWorkflowBinding binding, LocalAuditIntent intent, CancellationToken ct = default) =>
            Task.FromResult(Mutate(value, expected, ProductIdentityLifecycleStatus.PendingIdentityApproval, binding, intent));
        public Task<FirstGskuIdentityLifecycleMutationResult<ProductDefinitionRevision>> ApproveIdentityAsync(Guid id, int expected, FirstGskuIdentityWorkflowBinding binding, LocalAuditIntent intent, CancellationToken ct = default) =>
            Task.FromResult(Mutate(value, expected, ProductIdentityLifecycleStatus.IdentityApproved, binding, intent));
        public Task<FirstGskuIdentityLifecycleMutationResult<ProductDefinitionRevision>> RestoreDraftAfterRejectionAsync(Guid id, int expected, FirstGskuIdentityWorkflowBinding binding, LocalAuditIntent intent, CancellationToken ct = default) =>
            Task.FromResult(Mutate(value, expected, ProductIdentityLifecycleStatus.Draft, binding, intent));
    }

    private sealed class PairGskuRepository(Gsku value) : IGskuRepository
    {
        public Task<Gsku?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(id == value.Id ? value : null);
        public Task<Gsku?> GetReferenceableByIdAsync(Guid id, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);
        public Task<IReadOnlyList<Gsku>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Gsku>>([]);
        public Task<GskuPage> GetReferenceablePageAsync(int page, int size, string? search, CancellationToken ct = default) => Task.FromResult(new GskuPage([], 0));
        public Task<IReadOnlyList<Guid>> FindIdsByCanonicalCodeAsync(string search, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<Gsku?> GetByCreationCommandIdAsync(string id, CancellationToken ct = default) => Task.FromResult<Gsku?>(null);
        public Task<GskuCreateResult> CreateDraftAsync(Gsku gsku, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<GskuUpdateResult> UpdateDraftAsync(Gsku gsku, int version, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<FirstGskuIdentityLifecycleMutationResult<Gsku>> MarkIdentityPendingAsync(Guid id, int expected, FirstGskuIdentityWorkflowBinding binding, LocalAuditIntent intent, CancellationToken ct = default) =>
            Task.FromResult(Mutate(value, expected, ProductIdentityLifecycleStatus.PendingIdentityApproval, binding, intent));
        public Task<FirstGskuIdentityLifecycleMutationResult<Gsku>> ApproveIdentityAsync(Guid id, int expected, FirstGskuIdentityWorkflowBinding binding, LocalAuditIntent intent, CancellationToken ct = default) =>
            Task.FromResult(Mutate(value, expected, ProductIdentityLifecycleStatus.IdentityApproved, binding, intent));
        public Task<FirstGskuIdentityLifecycleMutationResult<Gsku>> RestoreDraftAfterRejectionAsync(Guid id, int expected, FirstGskuIdentityWorkflowBinding binding, LocalAuditIntent intent, CancellationToken ct = default) =>
            Task.FromResult(Mutate(value, expected, ProductIdentityLifecycleStatus.Draft, binding, intent));
    }

    private sealed class FakeOperationRepository : IFirstGskuIdentityWorkflowOperationRepository
    {
        public FirstGskuIdentityWorkflowOperation? Current { get; set; }
        public FirstGskuIdentityWorkflowCheckpoint? FailNextAdvanceTo { get; set; }
        public int WithdrawalApplyCount { get; private set; }
        public ProductDefinitionRevision? Revision { get; set; }
        public Gsku? Gsku { get; set; }
        public Task<FirstGskuIdentityWorkflowReserveResult> ReserveAsync(FirstGskuIdentityWorkflowOperation operation, CancellationToken ct = default)
        {
            Current ??= operation;
            return Task.FromResult(new FirstGskuIdentityWorkflowReserveResult(true, Current != operation, Current, null));
        }
        public Task<FirstGskuIdentityWorkflowOperation?> GetByOperationIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Current?.OperationId == id ? Current : null);
        public Task<FirstGskuIdentityWorkflowOperation?> GetByStartIdempotencyKeyAsync(string key, CancellationToken ct = default) => Task.FromResult(Current?.StartIdempotencyKey == key ? Current : null);
        public Task<FirstGskuIdentityWorkflowClaim?> TryClaimAsync(FirstGskuIdentityWorkflowClaimRequest request, CancellationToken ct = default)
        {
            if (ClaimsNeverAnswer)
                return Task.Delay(Timeout.Infinite, ct).ContinueWith<FirstGskuIdentityWorkflowClaim?>(_ => null, ct,
                    TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);
            if (Current is null || !request.EligibleCheckpoints.Contains(Current.Checkpoint)) return Task.FromResult<FirstGskuIdentityWorkflowClaim?>(null);
            // As the store (FIX2 — the fake used to ignore both, so the B9 rule had no test that could fail): a live
            // lease of another owner refuses; the worker's retry schedule refuses unless the claim is interactive.
            if (Current.LeaseOwner is not null && Current.LeaseUntilUtcTicksV1 > request.NowUtcTicks)
                return Task.FromResult<FirstGskuIdentityWorkflowClaim?>(null);
            if (!request.IgnoreRetrySchedule && Current.NextAttemptAtUtcTicksV1 > request.NowUtcTicks)
                return Task.FromResult<FirstGskuIdentityWorkflowClaim?>(null);
            Current.LeaseOwner = request.LeaseOwner;
            Current.LeaseUntilUtcTicksV1 = request.LeaseUntilUtcTicks;
            Current.LeaseGeneration++;
            return Task.FromResult<FirstGskuIdentityWorkflowClaim?>(new(Current.TenantId, Current.OperationId,
                Current.OperationFingerprint, request.LeaseOwner, Current.LeaseGeneration, Current.Checkpoint,
                request.LeaseUntilUtcTicks));
        }
        public Task<FirstGskuIdentityWorkflowRecoverablePage> DiscoverRecoverableAsync(long now, int limit, FirstGskuIdentityWorkflowRecoveryCursor? after = null, CancellationToken ct = default) =>
            Task.FromResult(new FirstGskuIdentityWorkflowRecoverablePage(Current is null ? [] : [Current], null));
        public Task<bool> AdvanceAsync(FirstGskuIdentityWorkflowClaim claim, FirstGskuIdentityWorkflowCheckpointMutation mutation, CancellationToken ct = default)
        {
            if (Current is null || Current.Checkpoint != claim.Checkpoint) return Task.FromResult(false);
            if (FailNextAdvanceTo == mutation.NextCheckpoint) { FailNextAdvanceTo = null; return Task.FromResult(false); }
            AdvanceCalls++;
            Current.UpdatedAtUtcTicksV1 = mutation.UpdatedAtUtcTicks;
            if (mutation.ReleaseLease) { Current.LeaseOwner = null; Current.LeaseUntilUtcTicksV1 = null; }
            if (mutation.ServiceFaultCount.HasValue)
            {
                Current.ServiceFaultCount = mutation.ServiceFaultCount.Value;
                Current.ServiceFaultSinceUtcTicksV1 = mutation.ServiceFaultSinceUtcTicksV1;
            }
            Current.WithdrawalRefusalCode = mutation.WithdrawalRefusalCode ?? Current.WithdrawalRefusalCode;
            Current.WithdrawalAwaitingRequesterSinceUtcTicksV1 = mutation.WithdrawalAwaitingRequesterSinceUtcTicksV1
                ?? Current.WithdrawalAwaitingRequesterSinceUtcTicksV1;
            Current.Checkpoint = mutation.NextCheckpoint;
            Current.RecoveryDisposition = mutation.RecoveryDisposition;
            Current.NextAttemptAtUtcTicksV1 = mutation.NextAttemptAtUtcTicksV1;
            Current.LastFailureCode = mutation.LastFailureCode;
            Current.WorkflowInstanceId = mutation.WorkflowInstanceId ?? Current.WorkflowInstanceId;
            Current.WorkflowTemplateId = mutation.WorkflowTemplateId ?? Current.WorkflowTemplateId;
            Current.WorkflowTemplateVersionId = mutation.WorkflowTemplateVersionId ?? Current.WorkflowTemplateVersionId;
            Current.ApprovalTaskId = mutation.ApprovalTaskId ?? Current.ApprovalTaskId;
            Current.AssignmentSnapshotId = mutation.AssignmentSnapshotId ?? Current.AssignmentSnapshotId;
            Current.StartTransitionLogId = mutation.StartTransitionLogId ?? Current.StartTransitionLogId;
            Current.WorkflowStartedAtUtcTicksV1 = mutation.WorkflowStartedAtUtcTicksV1 ?? Current.WorkflowStartedAtUtcTicksV1;
            Current.DecisionKind = mutation.DecisionKind ?? Current.DecisionKind;
            Current.DecisionObservedAtUtcTicksV1 = mutation.DecisionObservedAtUtcTicksV1 ?? Current.DecisionObservedAtUtcTicksV1;
            Current.DecisionActorSubjectId = mutation.DecisionActorSubjectId ?? Current.DecisionActorSubjectId;
            Current.DecisionReasonCode = mutation.DecisionReasonCode ?? Current.DecisionReasonCode;
            Current.DecisionObjectType = mutation.DecisionObjectType ?? Current.DecisionObjectType;
            Current.DecisionObjectId = mutation.DecisionObjectId ?? Current.DecisionObjectId;
            Current.DecisionObjectRef = mutation.DecisionObjectRef ?? Current.DecisionObjectRef;
            Current.DecisionWorkflowTemplateId = mutation.DecisionWorkflowTemplateId ?? Current.DecisionWorkflowTemplateId;
            Current.DecisionWorkflowTemplateVersionId = mutation.DecisionWorkflowTemplateVersionId ?? Current.DecisionWorkflowTemplateVersionId;
            Current.DecisionTaskStatus = mutation.DecisionTaskStatus ?? Current.DecisionTaskStatus;
            Current.DecisionInstanceStatus = mutation.DecisionInstanceStatus ?? Current.DecisionInstanceStatus;
            Current.DecisionTransitionSequence = mutation.DecisionTransitionSequence ?? Current.DecisionTransitionSequence;
            Current.DecisionAtUtcTicksV1 = mutation.DecisionAtUtcTicksV1 ?? Current.DecisionAtUtcTicksV1;
            Current.ApprovalPackApplicabilitySelection = mutation.ApprovalPackApplicabilitySelection ?? Current.ApprovalPackApplicabilitySelection;
            Current.ApprovalPackUomSelection = mutation.ApprovalPackUomSelection ?? Current.ApprovalPackUomSelection;
            Current.ReferencesValidatedAtUtcTicksV1 = mutation.ReferencesValidatedAtUtcTicksV1 ?? Current.ReferencesValidatedAtUtcTicksV1;
            Current.ApprovalReferenceProofFingerprint = mutation.ApprovalReferenceProofFingerprint ?? Current.ApprovalReferenceProofFingerprint;
            Current.WithdrawalCommandId = mutation.WithdrawalCommandId ?? Current.WithdrawalCommandId;
            Current.WithdrawalFingerprint = mutation.WithdrawalFingerprint ?? Current.WithdrawalFingerprint;
            Current.WithdrawalRequesterSubjectId = mutation.WithdrawalRequesterSubjectId ?? Current.WithdrawalRequesterSubjectId;
            Current.WithdrawalExpectedGskuVersion = mutation.WithdrawalExpectedGskuVersion ?? Current.WithdrawalExpectedGskuVersion;
            Current.WithdrawalReasonCode = mutation.WithdrawalReasonCode ?? Current.WithdrawalReasonCode;
            Current.WithdrawalComment = mutation.WithdrawalComment ?? Current.WithdrawalComment;
            Current.WithdrawalExpectedWorkflowInstanceVersion = mutation.WithdrawalExpectedWorkflowInstanceVersion ?? Current.WithdrawalExpectedWorkflowInstanceVersion;
            Current.WithdrawalExpectedApprovalTaskVersion = mutation.WithdrawalExpectedApprovalTaskVersion ?? Current.WithdrawalExpectedApprovalTaskVersion;
            Current.WithdrawalTransitionLogId = mutation.WithdrawalTransitionLogId ?? Current.WithdrawalTransitionLogId;
            Current.WithdrawalObservedAtUtcTicksV1 = mutation.WithdrawalObservedAtUtcTicksV1 ?? Current.WithdrawalObservedAtUtcTicksV1;
            Current.WithdrawalTransitionSequence = mutation.WithdrawalTransitionSequence ?? Current.WithdrawalTransitionSequence;
            Current.WithdrawalResultWorkflowInstanceVersion = mutation.WithdrawalResultWorkflowInstanceVersion ?? Current.WithdrawalResultWorkflowInstanceVersion;
            Current.WithdrawalResultApprovalTaskVersion = mutation.WithdrawalResultApprovalTaskVersion ?? Current.WithdrawalResultApprovalTaskVersion;
            Current.WithdrawalTaskStatus = mutation.WithdrawalTaskStatus ?? Current.WithdrawalTaskStatus;
            Current.WithdrawalInstanceStatus = mutation.WithdrawalInstanceStatus ?? Current.WithdrawalInstanceStatus;
            Current.WithdrawalObjectRef = mutation.WithdrawalObjectRef ?? Current.WithdrawalObjectRef;
            if (mutation.ClearWithdrawal)
            {
                Current.WithdrawalCommandId = null;
                Current.WithdrawalFingerprint = null;
                Current.WithdrawalRequesterSubjectId = null;
                Current.WithdrawalExpectedGskuVersion = null;
                Current.WithdrawalReasonCode = null;
                Current.WithdrawalComment = null;
                Current.WithdrawalExpectedWorkflowInstanceVersion = null;
                Current.WithdrawalExpectedApprovalTaskVersion = null;
                Current.WithdrawalTransitionLogId = null;
                Current.WithdrawalObservedAtUtcTicksV1 = null;
                Current.WithdrawalTransitionSequence = null;
                Current.WithdrawalResultWorkflowInstanceVersion = null;
                Current.WithdrawalResultApprovalTaskVersion = null;
                Current.WithdrawalTaskStatus = null;
                Current.WithdrawalInstanceStatus = null;
                Current.WithdrawalObjectRef = null;
                Current.WithdrawalRefusalCode = null;
                Current.WithdrawalAwaitingRequesterSinceUtcTicksV1 = null;
            }
            return Task.FromResult(true);
        }

        public int AdvanceCalls { get; private set; }
        public bool ClaimsNeverAnswer { get; set; }
        public int ReleaseIfOwnedCalls { get; private set; }
        public int NotApplicableWrites { get; private set; }

        public Task ReleaseIfOwnedAsync(Guid operationId, string leaseOwner, long leaseUntilUtcTicks, CancellationToken ct = default)
        {
            ReleaseIfOwnedCalls++;
            return Task.CompletedTask;
        }

        public Task<FirstGskuIdentitySubmitState> GetSubmitStateAsync(Guid gskuId, int currentGskuVersion,
            CancellationToken ct = default) =>
            Task.FromResult(FirstGskuIdentitySubmitState.Of(Current?.GskuId == gskuId ? Current : null, currentGskuVersion));

        public Task<FirstGskuIdentityDecisionNotApplicableWriteResult> ApplyDecisionNotApplicableAsync(
            FirstGskuIdentityWorkflowClaim claim,
            FirstGskuIdentityWorkflowOperation operation,
            string reasonCode,
            LocalAuditIntent revisionAuditIntent,
            LocalAuditIntent gskuAuditIntent,
            long updatedAtUtcTicks,
            CancellationToken cancellationToken = default)
        {
            // As the store: from DecisionObserved, or (FIX2 item 3) from AwaitingDecision for the maker's own decision.
            if (Current is null || Revision is null || Gsku is null
                || !(Current.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.DecisionObserved
                     || Current.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.AwaitingDecision
                        && reasonCode == FirstGskuIdentityWorkflowProcessor.SelfApprovalIgnored))
                return Task.FromResult(new FirstGskuIdentityDecisionNotApplicableWriteResult(
                    false, false, Revision, Gsku, "FIRST_GSKU_IDENTITY_NOT_APPLICABLE_PAIR_CONFLICT"));
            NotApplicableWrites++;
            Revision.LifecycleStatus = ProductIdentityLifecycleStatus.Draft;
            Gsku.LifecycleStatus = ProductIdentityLifecycleStatus.Draft;
            Revision.Version++;
            Gsku.Version++;
            Revision.AuditIntents.Add(revisionAuditIntent);
            Gsku.AuditIntents.Add(gskuAuditIntent);
            Current.Checkpoint = FirstGskuIdentityWorkflowCheckpoint.DecisionNotApplicable;
            Current.LeaseOwner = null;
            Current.LeaseUntilUtcTicksV1 = null;
            Current.LastFailureCode = reasonCode;
            Current.UpdatedAtUtcTicksV1 = updatedAtUtcTicks;
            return Task.FromResult(new FirstGskuIdentityDecisionNotApplicableWriteResult(
                true, false, Revision, Gsku, null));
        }

        public Task<FirstGskuIdentityWithdrawalWriteResult> ApplyWithdrawalAsync(
            FirstGskuIdentityWorkflowClaim claim,
            FirstGskuIdentityWorkflowOperation operation,
            PersistedCancellationEvidence cancellationEvidence,
            LocalAuditIntent revisionAuditIntent,
            LocalAuditIntent gskuAuditIntent,
            long updatedAtUtcTicks,
            CancellationToken cancellationToken = default)
        {
            if (Current?.Checkpoint == FirstGskuIdentityWorkflowCheckpoint.WithdrawalApplied)
                return Task.FromResult(new FirstGskuIdentityWithdrawalWriteResult(
                    true, true, null, null, null));
            if (Current is null || Current.Checkpoint != FirstGskuIdentityWorkflowCheckpoint.WithdrawalObserved)
                return Task.FromResult(new FirstGskuIdentityWithdrawalWriteResult(
                    false, false, null, null, "FIRST_GSKU_IDENTITY_WITHDRAWAL_STATE_CONFLICT"));
            if (Revision is null || Gsku is null
                || Revision.LifecycleStatus != ProductIdentityLifecycleStatus.PendingIdentityApproval
                || Gsku.LifecycleStatus != ProductIdentityLifecycleStatus.PendingIdentityApproval
                || Revision.Version != operation.ExpectedRevisionVersion + 1
                || Gsku.Version != operation.WithdrawalExpectedGskuVersion)
                return Task.FromResult(new FirstGskuIdentityWithdrawalWriteResult(
                    false, false, Revision, Gsku, "FIRST_GSKU_IDENTITY_WITHDRAWAL_STATE_CONFLICT"));

            WithdrawalApplyCount++;
            Revision.LifecycleStatus = ProductIdentityLifecycleStatus.Draft;
            Gsku.LifecycleStatus = ProductIdentityLifecycleStatus.Draft;
            Revision.Version++;
            Gsku.Version++;
            Revision.AuditIntents.Add(revisionAuditIntent);
            Gsku.AuditIntents.Add(gskuAuditIntent);
            Current.Checkpoint = FirstGskuIdentityWorkflowCheckpoint.WithdrawalApplied;
            Current.LeaseOwner = null; // as the store: the step's write gives the lease back
            Current.LeaseUntilUtcTicksV1 = null;
            Current.UpdatedAtUtcTicksV1 = updatedAtUtcTicks;
            return Task.FromResult(new FirstGskuIdentityWithdrawalWriteResult(
                true, false, Revision, Gsku, null));
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception) + (exception is null ? string.Empty : " " + exception)));
    }

    public class RepositoryProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
    }

    private static FirstGskuIdentityLifecycleMutationResult<T> Mutate<T>(
        T aggregate, int expected, ProductIdentityLifecycleStatus target,
        FirstGskuIdentityWorkflowBinding binding, LocalAuditIntent intent) where T : EntityBase, IAuditIntentAggregate
    {
        var lifecycle = aggregate switch
        {
            Gsku gsku => gsku.LifecycleStatus,
            ProductDefinitionRevision revision => revision.LifecycleStatus,
            _ => throw new NotSupportedException()
        };
        var currentBinding = aggregate switch
        {
            Gsku gsku => gsku.IdentityWorkflowBinding,
            ProductDefinitionRevision revision => revision.IdentityWorkflowBinding,
            _ => null
        };
        if (aggregate.Version == expected + 1 && lifecycle == target
            && currentBinding?.StartRequestFingerprint == binding.StartRequestFingerprint
            && currentBinding.TerminalDecision?.TransitionSequence == binding.TerminalDecision?.TransitionSequence)
        {
            return new(true, true, aggregate, null);
        }
        if (aggregate.Version != expected) return new(false, false, aggregate, "CONCURRENCY_CONFLICT");
        // As the stores (FIX2 item 7): an aggregate at its audit-intent capacity says so.
        if (aggregate.AuditIntents.Count >= AuditIntentLimits.MaxPerAggregate)
            return new(false, false, aggregate, "AUDIT_INTENT_CAPACITY_REACHED");
        aggregate.Version++;
        aggregate.AuditIntents.Add(intent);
        if (aggregate is Gsku g) { g.LifecycleStatus = target; g.IdentityWorkflowBinding = binding; }
        if (aggregate is ProductDefinitionRevision r) { r.LifecycleStatus = target; r.IdentityWorkflowBinding = binding; }
        return new(true, false, aggregate, null);
    }

    private static ReferenceCatalogSelection Selection(string set, string code) => new()
    {
        SetCode = set, ValueCode = code, CatalogVersionId = Guid.NewGuid(), CatalogVersionNumber = 1,
        ResolutionMode = ReferenceCatalogResolutionMode.Latest, ResolvedAtUtc = DateTimeOffset.UtcNow
    };

    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ProductId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid RevisionId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid GskuId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid OperationId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid MakerId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly Guid ApproverId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid TemplateId = Guid.Parse("80000000-0000-0000-0000-000000000001");
    private static readonly Guid TemplateVersionId = Guid.Parse("90000000-0000-0000-0000-000000000001");
    private static readonly Guid WorkflowId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid TaskId = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid SnapshotId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid StartLogId = Guid.Parse("d0000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset StableReferenceTime =
        new(2026, 8, 29, 8, 0, 0, TimeSpan.Zero);
}
