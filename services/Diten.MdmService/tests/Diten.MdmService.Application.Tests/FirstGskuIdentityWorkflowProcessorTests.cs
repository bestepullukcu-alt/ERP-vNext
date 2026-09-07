using System.Reflection;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
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

    [Fact]
    public async Task Maker_cannot_approve_own_pair()
    {
        var harness = new Harness { TerminalActorId = MakerId };
        var submitted = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        var result = await harness.Processor.RecoverAsync(
            submitted.Operation!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(result.Succeeded);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            harness.Operations.Current!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Revision.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Gsku.LifecycleStatus);
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

    [Fact]
    public async Task Parent_approval_dependency_only_resumes_by_exact_interactive_replay_without_new_workflow()
    {
        var harness = new Harness();
        harness.Parent.LifecycleStatus = ProductIdentityLifecycleStatus.Draft;
        var submitted = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        var blocked = await harness.Processor.RecoverAsync(
            submitted.Operation!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(blocked.Succeeded);
        Assert.Equal("FIRST_GSKU_IDENTITY_PARENT_NOT_APPROVED", blocked.Operation!.LastFailureCode);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            blocked.Operation.Checkpoint);
        Assert.Equal(1, harness.Client.StartCalls);

        harness.Parent.LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved;
        var backgroundAttempt = await harness.Processor.RecoverAsync(
            blocked.Operation, "worker-2", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        var replay = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(backgroundAttempt.Succeeded);
        Assert.Equal(409, backgroundAttempt.StatusCode);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.Equal(OperationId, replay.Operation!.OperationId);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.Completed, replay.Operation.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, harness.Revision.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, harness.Gsku.LifecycleStatus);
        Assert.Equal(1, harness.Client.StartCalls);
    }

    [Theory]
    [InlineData(401, "REFERENCE_UNAUTHENTICATED")]
    [InlineData(403, "REFERENCE_FORBIDDEN")]
    public async Task Legacy_interactive_reference_auth_failure_only_resumes_by_exact_interactive_replay_with_trusted_resolver(
        int statusCode,
        string failureCode)
    {
        var harness = new Harness();
        harness.References.Result = VerifiedGskuReferenceResolveResult.Fail(statusCode, failureCode);
        var submitted = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        var blocked = await harness.Processor.RecoverAsync(
            submitted.Operation!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(blocked.Succeeded);
        Assert.Equal(failureCode, blocked.Operation!.LastFailureCode);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired, blocked.Operation.Checkpoint);
        harness.References.Result = ReferenceResolver.Success(2);

        var backgroundAttempt = await harness.Processor.RecoverAsync(
            blocked.Operation, "worker-2", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        var replay = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(backgroundAttempt.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.Equal(OperationId, replay.Operation!.OperationId);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.Completed, replay.Operation.Checkpoint);
        Assert.Equal(1, harness.Client.StartCalls);
    }

    [Fact]
    public async Task Persistent_reference_auth_failure_does_not_spin_manual_resume_until_step_budget()
    {
        var harness = new Harness();
        harness.References.Result = VerifiedGskuReferenceResolveResult.Fail(403, "REFERENCE_FORBIDDEN");
        var submitted = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        var blocked = await harness.Processor.RecoverAsync(
            submitted.Operation!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        var replay = await harness.Processor.StartInteractiveAsync(
            TenantId, GskuId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(replay.Succeeded);
        Assert.Equal(409, replay.StatusCode);
        Assert.Equal("FIRST_GSKU_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED", replay.ErrorCode);
        Assert.Equal(2, harness.References.Calls);
        Assert.Equal(FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            replay.Operation!.Checkpoint);
        Assert.False(blocked.Succeeded);
    }

    [Fact]
    public async Task Unrelated_manual_reconciliation_cannot_resume_by_interactive_replay()
    {
        var harness = new Harness { TerminalActorId = MakerId };
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
        Assert.Equal("FIRST_GSKU_IDENTITY_WORKFLOW_RECONCILIATION_REQUIRED", replay.ErrorCode);
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

    private sealed class Harness
    {
        public Harness()
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
                    TimeProvider.System),
                TimeProvider.System);
        }

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
            CancellationToken cancellationToken = default) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(new(
                WorkflowId, TaskId, TemplateId, TemplateVersionId, "gsku", GskuId.ToString("D"), "GS-0001",
                "Approve", harness.TerminalActorId.ToString("D"), "APPROVED", DateTimeOffset.UtcNow,
                2, "Approved", "Completed", "corr")));

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>> GetCancellationPreflightAsync(
            Guid tenantId, ProductIdentityWorkflowCancellationPreflightRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>.Success(new(
                request.WorkflowInstanceId, request.ApprovalTaskId, request.ExpectedObjectType,
                request.ExpectedObjectId, "GS-0001", 3, 5, "Active", "WaitingApproval")));

        public Task<ProductIdentityWorkflowTransportResult<TransportCancellationEvidence>> CancelAsync(
            Guid tenantId, ProductIdentityWorkflowCancellationRequest request, string delegatedUserToken,
            CancellationToken cancellationToken = default)
        {
            CancellationCalls++;
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

        public static VerifiedGskuReferenceResolveResult Success(int version) =>
            VerifiedGskuReferenceResolveResult.Success([
                Verified("pack-applicability", "SCALAR_QUANTITY_APPLIES", version, 1),
                Verified("uom", "EA", version, 2)]);

        private static VerifiedGskuReferenceSelection Verified(
            string set, string code, int version, int discriminator) => new(
            set, code,
            Guid.Parse($"a0000000-0000-0000-0000-{version:D8}{discriminator:D4}"),
            version, "LATEST", StableReferenceTime, false, true);
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
            if (Current is null || !request.EligibleCheckpoints.Contains(Current.Checkpoint)) return Task.FromResult<FirstGskuIdentityWorkflowClaim?>(null);
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
            return Task.FromResult(true);
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
            Current.UpdatedAtUtcTicksV1 = updatedAtUtcTicks;
            return Task.FromResult(new FirstGskuIdentityWithdrawalWriteResult(
                true, false, Revision, Gsku, null));
        }
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
