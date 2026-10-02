using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Diten.MdmService.Infrastructure.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using PersistedCancellationEvidence = Diten.MdmService.Domain.ValueObjects.ProductIdentityWorkflowCancellationEvidence;
using TransportCancellationEvidence = Diten.MdmService.Application.Contracts.Workflow.ProductIdentityWorkflowCancellationEvidence;

namespace Diten.MdmService.Application.Tests;

public sealed partial class GlobalProductIdentityWorkflowProcessorTests
{
    [Theory]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.Superseded)]
    public async Task Recover_OperatorTerminalRecoveryState_IsPermanentNoOp(
        GlobalProductIdentityWorkflowCheckpoint checkpoint)
    {
        var harness = Harness.Create();
        var operation = harness.CreateOperation(checkpoint);
        harness.Repository.Current = operation;

        var result = await harness.Processor.RecoverAsync(
            operation, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.True(result.Succeeded);
        Assert.True(result.IsReplay);
        Assert.Same(operation, result.Operation);
    }

    [Fact]
    public async Task StartInteractive_LostStartResponseRecoveredByLookup_AppliesPendingOnce()
    {
        var harness = Harness.Create();
        harness.Client.Start = (_, _, _, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Fail(
                ProductIdentityWorkflowTransportOutcome.Timeout, "WORKFLOW_TIMEOUT"));
        harness.Client.StartResult = (_, request, _) =>
        {
            Assert.Equal("GlobalProduct", request.ExpectedObjectType);
            Assert.Equal(ProductId.ToString("D"), request.ExpectedObjectId);
            Assert.Equal(MakerId, request.ExpectedMakerSubjectId);
            return Task.FromResult(ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(
                StartResult()));
        };

        var result = await harness.Processor.StartInteractiveAsync(
            TenantId, ProductId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.True(result.Succeeded);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, result.Operation!.Checkpoint);
        Assert.Equal(1, harness.Products.SubmitCount);
        Assert.Equal(WorkflowInstanceId, result.Operation.WorkflowInstanceId);
        Assert.Equal(1, harness.Client.StartResultCalls);
    }

    [Fact]
    public async Task Recover_StartNotFoundWithoutMakerToken_RequiresFreshMakerReplay()
    {
        var harness = Harness.Create();
        var operation = harness.CreateOperation(GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown);
        harness.Repository.Current = operation;
        harness.Client.StartResult = (_, _, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Fail(
                ProductIdentityWorkflowTransportOutcome.NotFound, "WORKFLOW_START_NOT_FOUND"));

        var result = await harness.Processor.RecoverAsync(
            operation, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.False(result.Succeeded);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay,
            harness.Repository.Current!.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.AwaitingMakerReplay,
            harness.Repository.Current.RecoveryDisposition);
        Assert.Equal(0, harness.Client.StartCalls);
        // What the engine said is kept on the operation for the operator; the caller is never handed a code that is
        // not one of the fixed ones.
        Assert.Equal("WORKFLOW_START_NOT_FOUND", harness.Repository.Current.LastFailureCode);
        Assert.Equal(ProductIdentityWorkflowMakerCodes.MakerReplayRequired, result.ErrorCode);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task Recover_TerminalApprove_AppliesDecisionAndReplayDoesNotApplyTwice()
    {
        var harness = Harness.CreatePending();
        var operation = harness.Repository.Current!;
        harness.Client.Terminal = (_, _, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(
                TerminalEvidence()));

        var first = await harness.Processor.RecoverAsync(
            operation, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));
        var second = await harness.Processor.RecoverAsync(
            harness.Repository.Current!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.True(second.IsReplay);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.Completed, harness.Repository.Current!.Checkpoint);
        Assert.Equal(1, harness.Products.ReconcileCount);
        Assert.Equal(ProductIdentityDecisionKind.Approved, harness.Repository.Current.DecisionKind);
        Assert.Equal("Completed", harness.Repository.Current.DecisionInstanceStatus);
    }

    [Fact]
    public async Task Recover_TerminalEvidenceMakerViolation_QuarantinesWithoutMutation()
    {
        var harness = Harness.CreatePending();
        harness.Client.Terminal = (_, _, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(
                TerminalEvidence() with { ActorUserId = MakerId.ToString("D") }));

        var result = await harness.Processor.RecoverAsync(
            harness.Repository.Current!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.False(result.Succeeded);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            harness.Repository.Current!.Checkpoint);
        Assert.Equal(0, harness.Products.ReconcileCount);
    }

    [Theory]
    [InlineData("Pending", "Active")]
    [InlineData("Rejected", "Rejected")]
    public async Task Recover_ApproveWithContradictoryTerminalStatuses_QuarantinesWithoutMutation(
        string taskStatus,
        string instanceStatus)
    {
        var harness = Harness.CreatePending();
        harness.Client.Terminal = (_, _, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(
                TerminalEvidence() with { TaskStatus = taskStatus, InstanceStatus = instanceStatus }));

        var result = await harness.Processor.RecoverAsync(
            harness.Repository.Current!, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.False(result.Succeeded);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            harness.Repository.Current!.Checkpoint);
        Assert.Equal(0, harness.Products.ReconcileCount);
    }

    [Fact]
    public async Task Recover_AfterLocalCommitBeforeCheckpoint_ReusesImmutableStartedAtWithoutDuplicateAudit()
    {
        var harness = Harness.Create();
        harness.Repository.FailNextAdvanceTo = GlobalProductIdentityWorkflowCheckpoint.LocalPendingApplied;
        harness.Client.Start = (_, _, _, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(StartResult()));

        var first = await harness.Processor.StartInteractiveAsync(
            TenantId, ProductId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.False(first.Succeeded);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted,
            harness.Repository.Current!.Checkpoint);
        Assert.Equal(1, harness.Products.SubmitMutationCount);

        await Task.Delay(2);
        harness.Client.Terminal = (_, _, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Fail(
                ProductIdentityWorkflowTransportOutcome.NonTerminal, "WORKFLOW_DECISION_PENDING"));
        var second = await harness.Processor.RecoverAsync(
            harness.Repository.Current, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.False(second.Succeeded);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision,
            harness.Repository.Current.Checkpoint);
        Assert.Equal(1, harness.Products.SubmitMutationCount);
        Assert.Single(harness.Product.AuditIntents,
            intent => intent.Operation == ProductAuditOperation.GlobalProductIdentitySubmitted);
    }

    [Theory]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.WorkflowStarted, 1)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.LocalPendingApplied, 0)]
    public async Task Recover_AfterLocalCheckpointCrash_ReachesDecisionPollingWithoutDuplicateSubmit(
        GlobalProductIdentityWorkflowCheckpoint checkpoint,
        int expectedSubmitCount)
    {
        var harness = Harness.Create();
        var operation = harness.CreateOperation(checkpoint);
        Harness.AddStartProof(operation);
        harness.Repository.Current = operation;
        if (checkpoint == GlobalProductIdentityWorkflowCheckpoint.LocalPendingApplied)
        {
            harness.MakeProductPending(operation);
        }
        harness.Client.Terminal = (_, _, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Fail(
                ProductIdentityWorkflowTransportOutcome.NonTerminal, "WORKFLOW_DECISION_PENDING"));

        var result = await harness.Processor.RecoverAsync(
            operation, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.False(result.Succeeded);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision,
            harness.Repository.Current!.Checkpoint);
        Assert.Equal(expectedSubmitCount, harness.Products.SubmitCount);
    }

    [Theory]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.DecisionObserved, 1)]
    [InlineData(GlobalProductIdentityWorkflowCheckpoint.DecisionApplied, 0)]
    public async Task Recover_AfterDecisionCheckpointCrash_CompletesWithoutDuplicateDecision(
        GlobalProductIdentityWorkflowCheckpoint checkpoint,
        int expectedReconcileCount)
    {
        var harness = Harness.CreatePending();
        var operation = harness.Repository.Current!;
        operation.Checkpoint = checkpoint;
        Harness.AddDecisionProof(operation);
        if (checkpoint == GlobalProductIdentityWorkflowCheckpoint.DecisionApplied)
        {
            harness.ApplyDecisionBeforeRecovery(operation);
        }

        var result = await harness.Processor.RecoverAsync(
            operation, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.True(result.Succeeded);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.Completed,
            harness.Repository.Current!.Checkpoint);
        Assert.Equal(expectedReconcileCount, harness.Products.ReconcileCount);
    }

    [Fact]
    public async Task StartInteractive_CancellationDuringRemoteStart_PropagatesAndLeavesRecoverableCheckpoint()
    {
        var harness = Harness.Create();
        using var source = new CancellationTokenSource();
        harness.Client.Start = (_, _, _, _) => throw new OperationCanceledException(source.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            harness.Processor.StartInteractiveAsync(
                TenantId, ProductId, 0, OperationId, MakerId, "maker-token",
                TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30), source.Token));

        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.StartOutcomeUnknown,
            harness.Repository.Current!.Checkpoint);
    }

    [Fact]
    public async Task WithdrawInteractive_VerifiedCancellation_AppliesDraftAndAuditExactlyOnce()
    {
        var harness = Harness.CreatePending();
        var commandId = Guid.NewGuid();
        harness.Client.Preflight = (_, request, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>.Success(new(
                request.WorkflowInstanceId, request.ApprovalTaskId, request.ExpectedObjectType,
                request.ExpectedObjectId, "GP-0001", 3, 5, "Active", "WaitingApproval")));
        harness.Client.Cancel = (_, request, token, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<TransportCancellationEvidence>.Success(new(
                request.WorkflowInstanceId, request.ApprovalTaskId, TemplateId, TemplateVersionId,
                request.ExpectedObjectType, request.ExpectedObjectId, "GP-0001", "Cancel", MakerId,
                request.ReasonCode, request.Comment, DateTimeOffset.UtcNow, 3, Guid.NewGuid(), "Cancelled",
                "Cancelled", 4, 6, false, "corr")));

        var first = await harness.Processor.WithdrawInteractiveAsync(TenantId, ProductId, 1, commandId,
            MakerId, "REQUESTER_WITHDRAWAL", null, "maker-token", TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(30));
        var replay = await harness.Processor.WithdrawInteractiveAsync(TenantId, ProductId, 1, commandId,
            MakerId, "REQUESTER_WITHDRAWAL", null, "maker-token", TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(30));

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.True(replay.IsReplay);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, harness.Product.LifecycleStatus);
        Assert.Equal(2, harness.Product.Version);
        Assert.Equal(1, harness.Products.WithdrawCount);
        Assert.Single(harness.Product.AuditIntents,
            x => x.Operation == ProductAuditOperation.GlobalProductIdentityApprovalWithdrawn);
    }

    [Fact]
    public async Task WithdrawInteractive_Timeout_LeavesProductPendingAndRecoverable()
    {
        var harness = Harness.CreatePending();
        harness.Client.Preflight = (_, request, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>.Success(new(
                request.WorkflowInstanceId, request.ApprovalTaskId, request.ExpectedObjectType,
                request.ExpectedObjectId, "GP-0001", 3, 5, "Active", "WaitingApproval")));
        harness.Client.Cancel = (_, _, _, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<TransportCancellationEvidence>.Fail(
                ProductIdentityWorkflowTransportOutcome.Timeout, "WORKFLOW_TIMEOUT"));

        var result = await harness.Processor.WithdrawInteractiveAsync(TenantId, ProductId, 1, Guid.NewGuid(),
            MakerId, "REQUESTER_WITHDRAWAL", null, "maker-token", TimeSpan.FromMinutes(1),
            TimeSpan.FromSeconds(30));

        Assert.False(result.Succeeded);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval, harness.Product.LifecycleStatus);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.WithdrawalOutcomeUnknown,
            harness.Repository.Current!.Checkpoint);
        Assert.Equal(0, harness.Products.WithdrawCount);
    }

    // ── WP-GP-1A-TRUST-CHAIN-01 (D) ─────────────────────────────────────────────────────────────────────────────
    // A submit against a transport THIS service was never configured for must not lock the draft. The transport in
    // these tests is the PRODUCTION client and identity provider with production options; the HTTP factory throws,
    // which also proves that neither fault ever reaches the network.

    [Theory]
    [InlineData(false, true, "PRODUCT_WORKFLOW_CLIENT_CONFIGURATION_INVALID")]
    [InlineData(true, false, "PRODUCT_WORKFLOW_IDENTITY_CONFIGURATION_INVALID")]
    [InlineData(false, false, "PRODUCT_WORKFLOW_CLIENT_CONFIGURATION_INVALID")]
    public async Task StartInteractive_WhenTransportIsNotConfigured_Returns503AndWritesNoOperationDocument(
        bool platformAddressConfigured, bool identityProviderConfigured, string expectedCode)
    {
        var (transport, readiness) = ProductionTransport(platformAddressConfigured, identityProviderConfigured);
        var harness = Harness.Create(transport, readiness);

        var result = await harness.Processor.StartInteractiveAsync(
            TenantId, ProductId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.False(result.Succeeded);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Null(result.Operation);
        Assert.Null(harness.Repository.Current); // no operation document was reserved
        Assert.Equal(0, harness.Repository.ReserveCalls);
        Assert.Equal(0, harness.Products.SubmitCount);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, harness.Product.LifecycleStatus);
        Assert.Null(harness.Product.WorkflowBinding);
    }

    [Fact]
    public async Task StartInteractive_PastThePreflight_AnInvalidPlatformAddressStillQuarantines()
    {
        // What the pre-flight stands before, measured on the production transport with a readiness that lets it
        // through: the client cannot tell a bad address from a bad request (one code for both), so this one fault
        // is only ever kept away from the record by the pre-flight.
        var (transport, _) = ProductionTransport(platformAddressConfigured: false, identityProviderConfigured: true);
        var harness = Harness.Create(transport, new FixedReadiness(null));

        var result = await harness.Processor.StartInteractiveAsync(
            TenantId, ProductId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.False(result.Succeeded);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            harness.Repository.Current!.Checkpoint);
        Assert.Equal("PRODUCT_WORKFLOW_REQUEST_INVALID", harness.Repository.Current.LastFailureCode);
    }

    [Fact]
    public async Task StartInteractive_PastThePreflight_AnInvalidIdentitySettingDoesNotQuarantine()
    {
        var (transport, _) = ProductionTransport(platformAddressConfigured: true, identityProviderConfigured: false);
        var harness = Harness.Create(transport, new FixedReadiness(null));

        var result = await harness.Processor.StartInteractiveAsync(
            TenantId, ProductId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.Equal(503, result.StatusCode);
        Assert.Equal(ProductIdentityWorkflowMakerCodes.ServiceUnavailableResubmit, result.ErrorCode);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay,
            harness.Repository.Current!.Checkpoint);
        Assert.Equal(0, harness.Repository.Current.ServiceFaultCount); // a maker's attempt is not a worker attempt
        Assert.NotNull(harness.Repository.Current.ServiceFaultSinceUtcTicksV1);
    }

    [Fact]
    public async Task StartInteractive_WhenTransportIsConfigured_StartsExactlyAsBefore()
    {
        var harness = Harness.Create(transport: null, new FixedReadiness(null));
        harness.Client.Start = (_, _, _, _) => Task.FromResult(
            ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(StartResult()));

        var result = await harness.Processor.StartInteractiveAsync(
            TenantId, ProductId, 0, OperationId, MakerId, "maker-token",
            TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.True(result.Succeeded);
        Assert.Equal(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision, result.Operation!.Checkpoint);
        Assert.Equal(1, harness.Products.SubmitCount);
    }

    private static (IProductIdentityWorkflowClient Transport, IProductIdentityWorkflowTransportReadiness Readiness)
        ProductionTransport(bool platformAddressConfigured, bool identityProviderConfigured)
    {
        var client = Options.Create(new ProductIdentityWorkflowClientOptions
        {
            PlatformBaseUrl = platformAddressConfigured ? "https://platform.internal/" : string.Empty
        });
        var identity = Options.Create(identityProviderConfigured
            ? new AuthProductIdentityWorkflowServiceIdentityProviderOptions
            {
                AuthBaseUrl = "https://auth.internal/",
                ExpectedIssuer = "issuer",
                ClientId = "workflow-client",
                ActiveClientSecret = "configured-but-never-sent"
            }
            : new AuthProductIdentityWorkflowServiceIdentityProviderOptions());
        var network = new NoNetwork();
        return (
            new PlatformProductIdentityWorkflowClient(
                network,
                new AuthProductIdentityWorkflowServiceIdentityProvider(network, identity, TimeProvider.System),
                client),
            new ProductIdentityWorkflowTransportReadiness(client, identity));
    }

    private sealed class NoNetwork : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new InvalidOperationException("A configuration fault must be decided without any network call.");
    }

    private sealed class FixedReadiness(string? fault) : IProductIdentityWorkflowTransportReadiness
    {
        public string? FindConfigurationFault() => fault;
    }

    private sealed class Harness
    {
        private Harness(
            IProductIdentityWorkflowClient? transport = null,
            IProductIdentityWorkflowTransportReadiness? readiness = null,
            ProductIdentityWorkflowServiceFaultSettings? settings = null)
        {
            Product = DraftProduct();
            Products = new(Product);
            Processor = new(
                Repository,
                Products,
                transport ?? Client,
                new(StartConfiguration, TimeProvider.System),
                TimeProvider.System,
                readiness ?? new FixedReadiness(null),
                settings ?? ProductIdentityWorkflowServiceFaultSettings.Default,
                Log);
        }

        public CapturingLogger<GlobalProductIdentityWorkflowProcessor> Log { get; } = new();

        public static Harness Create(ProductIdentityWorkflowServiceFaultSettings settings) =>
            new(null, null, settings);

        public static Harness CreatePending(
            IProductIdentityWorkflowTransportReadiness? readiness = null,
            ProductIdentityWorkflowServiceFaultSettings? settings = null)
        {
            var harness = new Harness(null, readiness, settings);
            var operation = harness.CreateOperation(GlobalProductIdentityWorkflowCheckpoint.AwaitingDecision);
            AddStartProof(operation);
            harness.Repository.Current = operation;
            harness.MakeProductPending(operation);
            return harness;
        }

        public static ProductIdentityWorkflowStartConfiguration StartConfiguration { get; } =
            new(TemplateId, null, [ApproverId], "IDENTITY_APPROVAL", true, true, null);

        public FakeOperationRepository Repository { get; } = new();
        public TestProductRepository Products { get; }
        public FakeWorkflowClient Client { get; } = new();
        public GlobalProductIdentityWorkflowProcessor Processor { get; }
        public GlobalProduct Product { get; }

        public static Harness Create() => new();

        public static Harness Create(
            IProductIdentityWorkflowClient? transport,
            IProductIdentityWorkflowTransportReadiness? readiness) => new(transport, readiness);

        public void MakeProductPending(GlobalProductIdentityWorkflowOperation operation)
        {
            Product.WorkflowBinding = Binding(operation);
            Product.LifecycleStatus = ProductIdentityLifecycleStatus.PendingIdentityApproval;
            Product.Version = 1;
        }

        public static void AddStartProof(GlobalProductIdentityWorkflowOperation operation)
        {
            operation.WorkflowInstanceId = WorkflowInstanceId;
            operation.WorkflowTemplateId = TemplateId;
            operation.WorkflowTemplateVersionId = TemplateVersionId;
            operation.ApprovalTaskId = TaskId;
            operation.AssignmentSnapshotId = AssignmentSnapshotId;
            operation.StartTransitionLogId = StartLogId;
            operation.WorkflowStartedAtUtcTicksV1 = DateTimeOffset.UtcNow.UtcTicks;
        }

        public static void AddDecisionProof(GlobalProductIdentityWorkflowOperation operation)
        {
            operation.DecisionKind = ProductIdentityDecisionKind.Approved;
            operation.DecisionActorSubjectId = ApproverId;
            operation.DecisionReasonCode = "APPROVED";
            operation.DecisionObjectType = "GlobalProduct";
            operation.DecisionObjectId = ProductId.ToString("D");
            operation.DecisionObjectRef = "GP-0001";
            operation.DecisionWorkflowTemplateId = TemplateId;
            operation.DecisionWorkflowTemplateVersionId = TemplateVersionId;
            operation.DecisionTaskStatus = "Approved";
            operation.DecisionInstanceStatus = "Completed";
            operation.DecisionTransitionSequence = 2;
            operation.DecisionAtUtcTicksV1 = DateTimeOffset.UtcNow.UtcTicks;
        }

        public void ApplyDecisionBeforeRecovery(GlobalProductIdentityWorkflowOperation operation)
        {
            Assert.True(GlobalProductIdentityWorkflowProcessorTests.TryEvidence(operation, out var evidence));
            Product.WorkflowBinding!.TerminalDecision = evidence;
            Product.LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved;
            Product.Version = 2;
            Product.AuditIntents.Add(ProductIdentityLifecycleAuditIntentFactory.CreateDecision(
                Product, 1, evidence));
        }

        public GlobalProductIdentityWorkflowOperation CreateOperation(
            GlobalProductIdentityWorkflowCheckpoint checkpoint)
        {
            var plan = new ProductIdentityWorkflowStartRequestFactory(
                    new(TemplateId, null, [ApproverId], "IDENTITY_APPROVAL", true, true, null),
                    TimeProvider.System)
                .Create(TenantId, Product, OperationId, 0, MakerId);
            plan.Operation.Checkpoint = checkpoint;
            return plan.Operation;
        }
    }

    private sealed class FakeWorkflowClient : IProductIdentityWorkflowClient
    {
        public Func<Guid, ProductIdentityWorkflowStartRequest, string, CancellationToken,
            Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>>>? Start { get; set; }
        public Func<Guid, ProductIdentityWorkflowStartResultRequest, CancellationToken,
            Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>>>? StartResult { get; set; }
        public Func<Guid, ProductIdentityWorkflowTerminalEvidenceRequest, CancellationToken,
            Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>>>? Terminal { get; set; }
        public Func<Guid, ProductIdentityWorkflowCancellationPreflightRequest, CancellationToken,
            Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>>>? Preflight { get; set; }
        public Func<Guid, ProductIdentityWorkflowCancellationRequest, string, CancellationToken,
            Task<ProductIdentityWorkflowTransportResult<TransportCancellationEvidence>>>? Cancel { get; set; }
        public int StartCalls { get; private set; }
        public int StartResultCalls { get; private set; }

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> StartAsync(
            Guid tenantId, ProductIdentityWorkflowStartRequest request, string delegatedUserToken,
            CancellationToken cancellationToken = default)
        {
            StartCalls++;
            return (Start ?? throw new InvalidOperationException("Unexpected start call."))(
                tenantId, request, delegatedUserToken, cancellationToken);
        }

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>> GetStartResultAsync(
            Guid tenantId, ProductIdentityWorkflowStartResultRequest request,
            CancellationToken cancellationToken = default)
        {
            StartResultCalls++;
            return (StartResult ?? throw new InvalidOperationException("Unexpected start-result call."))(
                tenantId, request, cancellationToken);
        }

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>> GetTerminalEvidenceAsync(
            Guid tenantId, ProductIdentityWorkflowTerminalEvidenceRequest request,
            CancellationToken cancellationToken = default) =>
            (Terminal ?? throw new InvalidOperationException("Unexpected terminal-evidence call."))(
                tenantId, request, cancellationToken);

        public Task<ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowCancellationPreflight>> GetCancellationPreflightAsync(
            Guid tenantId, ProductIdentityWorkflowCancellationPreflightRequest request,
            CancellationToken cancellationToken = default) =>
            (Preflight ?? throw new InvalidOperationException("Unexpected cancellation-preflight call."))(
                tenantId, request, cancellationToken);

        public Task<ProductIdentityWorkflowTransportResult<TransportCancellationEvidence>> CancelAsync(
            Guid tenantId, ProductIdentityWorkflowCancellationRequest request, string delegatedUserToken,
            CancellationToken cancellationToken = default) =>
            (Cancel ?? throw new InvalidOperationException("Unexpected cancellation call."))(
                tenantId, request, delegatedUserToken, cancellationToken);
    }

    private sealed class TestProductRepository(GlobalProduct product) : IGlobalProductRepository
    {
        public int SubmitCount { get; private set; }
        public int SubmitMutationCount { get; private set; }
        public int ReconcileCount { get; private set; }
        public int WithdrawCount { get; private set; }
        public string? SubmitFailure { get; set; }

        public Task<GlobalProduct?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(product.Id == id && !product.IsDeleted ? product : null);

        public Task<GlobalProduct?> GetByReservationIdAsync(
            Guid reservationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<GlobalProduct?>(null);

        public Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<GlobalProductPage> GetPageAsync(
            int pageNumber, int pageSize, string? normalizedSearch,
            ProductIdentityLifecycleStatus? lifecycleStatus,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new GlobalProductPage([], 0));

        public Task<GlobalProductCreateResult> CreateDraftAsync(
            GlobalProduct globalProduct, CancellationToken cancellationToken = default) =>
            Task.FromResult(new GlobalProductCreateResult(false, null, "NOT_USED"));

        public Task<GlobalProductLifecycleWriteResult> SubmitIdentityAsync(
            Guid id, int expectedVersion, ProductIdentityWorkflowBinding workflowBinding,
            LocalAuditIntent auditIntent, CancellationToken cancellationToken = default)
        {
            SubmitCount++;
            if (SubmitFailure is not null)
            {
                return Task.FromResult(new GlobalProductLifecycleWriteResult(false, product, SubmitFailure));
            }
            if (product.WorkflowBinding is { } existingBinding
                && product.LifecycleStatus == ProductIdentityLifecycleStatus.PendingIdentityApproval
                && product.Version == expectedVersion + 1
                && existingBinding.WorkflowInstanceId == workflowBinding.WorkflowInstanceId
                && existingBinding.StartIdempotencyKey == workflowBinding.StartIdempotencyKey
                && existingBinding.StartRequestFingerprint == workflowBinding.StartRequestFingerprint
                && existingBinding.SubmittedAtUtc == workflowBinding.SubmittedAtUtc)
            {
                return Task.FromResult(new GlobalProductLifecycleWriteResult(
                    true, product, IsReplay: true));
            }
            if (product.Version != expectedVersion || product.LifecycleStatus != ProductIdentityLifecycleStatus.Draft)
            {
                return Task.FromResult(new GlobalProductLifecycleWriteResult(
                    false, product, "PRODUCT_IDENTITY_STATE_CONFLICT"));
            }
            product.WorkflowBinding = workflowBinding;
            SubmitMutationCount++;
            product.LifecycleStatus = ProductIdentityLifecycleStatus.PendingIdentityApproval;
            product.Version++;
            product.AuditIntents.Add(auditIntent);
            return Task.FromResult(new GlobalProductLifecycleWriteResult(true, product));
        }

        public Task<GlobalProductLifecycleWriteResult> ReconcileIdentityDecisionAsync(
            Guid id, int expectedVersion, ProductIdentityWorkflowDecisionEvidence decisionEvidence,
            LocalAuditIntent auditIntent, CancellationToken cancellationToken = default)
        {
            ReconcileCount++;
            if (product.WorkflowBinding?.TerminalDecision is { } persisted)
            {
                return Task.FromResult(persisted.TransitionSequence == decisionEvidence.TransitionSequence
                    ? new GlobalProductLifecycleWriteResult(true, product, IsReplay: true)
                    : new GlobalProductLifecycleWriteResult(false, product, "PRODUCT_IDENTITY_IDEMPOTENCY_CONFLICT"));
            }
            if (product.Version != expectedVersion
                || product.LifecycleStatus != ProductIdentityLifecycleStatus.PendingIdentityApproval)
            {
                return Task.FromResult(new GlobalProductLifecycleWriteResult(
                    false, product, "PRODUCT_IDENTITY_STATE_CONFLICT"));
            }
            product.WorkflowBinding!.TerminalDecision = decisionEvidence;
            product.LifecycleStatus = decisionEvidence.Decision == ProductIdentityDecisionKind.Approved
                ? ProductIdentityLifecycleStatus.IdentityApproved
                : ProductIdentityLifecycleStatus.Draft;
            product.Version++;
            product.AuditIntents.Add(auditIntent);
            return Task.FromResult(new GlobalProductLifecycleWriteResult(true, product));
        }

        public Task<GlobalProductLifecycleWriteResult> RetireIdentityAsync(
            Guid id, int expectedVersion, LocalAuditIntent auditIntent,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new GlobalProductLifecycleWriteResult(false, product, "NOT_USED"));

        public Task<GlobalProductLifecycleWriteResult> WithdrawIdentityApprovalAsync(
            Guid id, int expectedVersion, PersistedCancellationEvidence cancellationEvidence,
            LocalAuditIntent auditIntent, CancellationToken cancellationToken = default)
        {
            if (product.WorkflowBinding?.CancellationEvidence is { } existing)
                return Task.FromResult(existing.IdempotencyKey == cancellationEvidence.IdempotencyKey
                    ? new GlobalProductLifecycleWriteResult(true, product, IsReplay: true)
                    : new GlobalProductLifecycleWriteResult(false, product, "PRODUCT_IDENTITY_IDEMPOTENCY_CONFLICT"));
            if (product.Version != expectedVersion
                || product.LifecycleStatus != ProductIdentityLifecycleStatus.PendingIdentityApproval)
                return Task.FromResult(new GlobalProductLifecycleWriteResult(false, product,
                    "PRODUCT_IDENTITY_STATE_CONFLICT"));
            WithdrawCount++;
            product.WorkflowBinding!.CancellationEvidence = cancellationEvidence;
            product.LifecycleStatus = ProductIdentityLifecycleStatus.Draft;
            product.Version++;
            product.AuditIntents.Add(auditIntent);
            return Task.FromResult(new GlobalProductLifecycleWriteResult(true, product));
        }
    }

    private sealed class FakeOperationRepository : IGlobalProductIdentityWorkflowOperationRepository
    {
        public GlobalProductIdentityWorkflowOperation? Current { get; set; }
        public GlobalProductIdentityWorkflowCheckpoint? FailNextAdvanceTo { get; set; }
        public int ReserveCalls { get; private set; }
        public int ClaimCalls { get; private set; }
        public int AdvanceCalls { get; private set; }
        public List<bool> ClaimsIgnoringSchedule { get; } = [];
        // Refuses the next advance to this checkpoint the way the real store refuses a transition it does not
        // allow: it answers false and leaves the lease exactly as it is.
        public GlobalProductIdentityWorkflowCheckpoint? RefuseNextAdvanceTo { get; set; }

        public Task<GlobalProductIdentityWorkflowReserveResult> ReserveAsync(
            GlobalProductIdentityWorkflowOperation operation, CancellationToken cancellationToken = default)
        {
            ReserveCalls++;
            if (Current is null)
            {
                Current = operation;
                return Task.FromResult(new GlobalProductIdentityWorkflowReserveResult(true, false, Current, null));
            }
            return Task.FromResult(new GlobalProductIdentityWorkflowReserveResult(
                Current.OperationFingerprint == operation.OperationFingerprint,
                true,
                Current,
                Current.OperationFingerprint == operation.OperationFingerprint ? null : "CONFLICT"));
        }

        public Task<GlobalProductIdentityWorkflowOperation?> GetByOperationIdAsync(
            Guid operationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Current?.OperationId == operationId ? Current : null);

        public Task<GlobalProductIdentityWorkflowOperation?> GetByStartIdempotencyKeyAsync(
            string startIdempotencyKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(Current?.StartIdempotencyKey == startIdempotencyKey ? Current : null);

        public Task<GlobalProductIdentityWorkflowClaim?> TryClaimAsync(
            GlobalProductIdentityWorkflowClaimRequest request, CancellationToken cancellationToken = default)
        {
            ClaimCalls++;
            ClaimsIgnoringSchedule.Add(request.IgnoreRetrySchedule);
            if (Current is null || Current.OperationId != request.OperationId
                || !request.EligibleCheckpoints.Contains(Current.Checkpoint))
            {
                return Task.FromResult<GlobalProductIdentityWorkflowClaim?>(null);
            }
            Current.LeaseGeneration++;
            Current.LeaseOwner = request.LeaseOwner;
            Current.LeaseUntilUtcTicksV1 = request.LeaseUntilUtcTicks;
            Current.UpdatedAtUtcTicksV1 = request.NowUtcTicks;
            return Task.FromResult<GlobalProductIdentityWorkflowClaim?>(new(
                Current.TenantId, Current.OperationId, Current.OperationFingerprint,
                request.LeaseOwner, Current.LeaseGeneration, Current.Checkpoint, request.LeaseUntilUtcTicks));
        }

        public Task<GlobalProductIdentityWorkflowRecoverablePage> DiscoverRecoverableAsync(
            long nowUtcTicks, int limit, GlobalProductIdentityWorkflowRecoveryCursor? after = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new GlobalProductIdentityWorkflowRecoverablePage(
                Current is null ? [] : [Current], null));

        public Task<bool> AdvanceAsync(
            GlobalProductIdentityWorkflowClaim claim,
            GlobalProductIdentityWorkflowCheckpointMutation mutation,
            CancellationToken cancellationToken = default)
        {
            AdvanceCalls++;
            if (Current is null || Current.LeaseGeneration != claim.LeaseGeneration
                || Current.Checkpoint != claim.Checkpoint)
            {
                return Task.FromResult(false);
            }
            if (RefuseNextAdvanceTo == mutation.NextCheckpoint)
            {
                RefuseNextAdvanceTo = null;
                return Task.FromResult(false);
            }
            if (FailNextAdvanceTo == mutation.NextCheckpoint)
            {
                FailNextAdvanceTo = null;
                Current.LeaseOwner = null;
                Current.LeaseUntilUtcTicksV1 = null;
                return Task.FromResult(false);
            }
            Current.Checkpoint = mutation.NextCheckpoint;
            Current.RecoveryDisposition = mutation.RecoveryDisposition;
            Current.UpdatedAtUtcTicksV1 = mutation.UpdatedAtUtcTicks;
            Current.NextAttemptAtUtcTicksV1 = mutation.NextAttemptAtUtcTicksV1;
            Current.LastFailureCode = mutation.LastFailureCode;
            Current.WorkflowInstanceId = mutation.WorkflowInstanceId ?? Current.WorkflowInstanceId;
            Current.WorkflowTemplateId = mutation.WorkflowTemplateId ?? Current.WorkflowTemplateId;
            Current.WorkflowTemplateVersionId = mutation.WorkflowTemplateVersionId ?? Current.WorkflowTemplateVersionId;
            Current.ApprovalTaskId = mutation.ApprovalTaskId ?? Current.ApprovalTaskId;
            Current.AssignmentSnapshotId = mutation.AssignmentSnapshotId ?? Current.AssignmentSnapshotId;
            Current.StartTransitionLogId = mutation.StartTransitionLogId ?? Current.StartTransitionLogId;
            Current.WorkflowStartedAtUtcTicksV1 =
                mutation.WorkflowStartedAtUtcTicksV1 ?? Current.WorkflowStartedAtUtcTicksV1;
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
            Current.WithdrawalCommandId = mutation.WithdrawalCommandId ?? Current.WithdrawalCommandId;
            Current.WithdrawalFingerprint = mutation.WithdrawalFingerprint ?? Current.WithdrawalFingerprint;
            Current.WithdrawalRequesterSubjectId = mutation.WithdrawalRequesterSubjectId ?? Current.WithdrawalRequesterSubjectId;
            Current.WithdrawalExpectedProductVersion = mutation.WithdrawalExpectedProductVersion ?? Current.WithdrawalExpectedProductVersion;
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
            if (mutation.ServiceFaultCount.HasValue)
            {
                Current.ServiceFaultCount = mutation.ServiceFaultCount.Value;
                Current.ServiceFaultSinceUtcTicksV1 = mutation.ServiceFaultSinceUtcTicksV1;
            }
            if (mutation.ClearWithdrawal)
            {
                Current.WithdrawalCommandId = null;
                Current.WithdrawalFingerprint = null;
                Current.WithdrawalRequesterSubjectId = null;
                Current.WithdrawalExpectedProductVersion = null;
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
            }
            if (mutation.ReleaseLease)
            {
                Current.LeaseOwner = null;
                Current.LeaseUntilUtcTicksV1 = null;
            }
            return Task.FromResult(true);
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

    private static GlobalProduct DraftProduct() => new()
    {
        Id = ProductId,
        TenantId = TenantId,
        CanonicalCode = "GP-0001",
        GlobalProductName = "Product",
        GlobalProductNameNormalized = "PRODUCT",
        LifecycleStatus = ProductIdentityLifecycleStatus.Draft,
        Version = 0
    };

    private static ProductIdentityWorkflowStartResult StartResult() => new(
        WorkflowInstanceId, TemplateId, TemplateVersionId, TaskId, AssignmentSnapshotId,
        StartLogId, "GP-0001", "Running", "Approval", "Review", DateTimeOffset.UtcNow,
        null, false, "corr");

    private static ProductIdentityWorkflowTerminalEvidence TerminalEvidence() => new(
        WorkflowInstanceId, TaskId, TemplateId, TemplateVersionId, "GlobalProduct",
        ProductId.ToString("D"), "GP-0001", "Approve", ApproverId.ToString("D"),
        "APPROVED", DateTimeOffset.UtcNow, 2, "Approved", "Completed", "corr");

    private static ProductIdentityWorkflowBinding Binding(GlobalProductIdentityWorkflowOperation operation) => new()
    {
        WorkflowInstanceId = WorkflowInstanceId,
        WorkflowTemplateId = TemplateId,
        WorkflowTemplateVersionId = TemplateVersionId,
        ApprovalTaskId = TaskId,
        AssignmentSnapshotId = AssignmentSnapshotId,
        StartTransitionLogId = StartLogId,
        ObjectType = "GlobalProduct",
        ObjectId = ProductId,
        ObjectRef = "GP-0001",
        SubmitterSubjectId = MakerId,
        StartIdempotencyKey = operation.StartIdempotencyKey,
        StartRequestFingerprint = operation.OperationFingerprint,
        SubmittedAtUtc = DateTimeOffset.UtcNow
    };

    private static bool TryEvidence(
        GlobalProductIdentityWorkflowOperation operation,
        out ProductIdentityWorkflowDecisionEvidence evidence)
    {
        evidence = new()
        {
            Decision = operation.DecisionKind!.Value,
            WorkflowInstanceId = operation.WorkflowInstanceId!.Value,
            ApprovalTaskId = operation.ApprovalTaskId!.Value,
            WorkflowTemplateId = operation.DecisionWorkflowTemplateId!.Value,
            WorkflowTemplateVersionId = operation.DecisionWorkflowTemplateVersionId!.Value,
            ObjectType = operation.DecisionObjectType!,
            ObjectId = Guid.Parse(operation.DecisionObjectId!),
            ObjectRef = operation.DecisionObjectRef!,
            DecisionActorSubjectId = operation.DecisionActorSubjectId!.Value,
            ReasonCode = operation.DecisionReasonCode,
            DecisionAtUtc = new DateTimeOffset(operation.DecisionAtUtcTicksV1!.Value, TimeSpan.Zero),
            TransitionSequence = operation.DecisionTransitionSequence!.Value,
            TaskStatus = operation.DecisionTaskStatus!,
            InstanceStatus = operation.DecisionInstanceStatus!
        };
        return true;
    }

    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ProductId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid OperationId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid MakerId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid ApproverId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid TemplateId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly Guid TemplateVersionId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid WorkflowInstanceId = Guid.Parse("80000000-0000-0000-0000-000000000001");
    private static readonly Guid TaskId = Guid.Parse("90000000-0000-0000-0000-000000000001");
    private static readonly Guid AssignmentSnapshotId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid StartLogId = Guid.Parse("b0000000-0000-0000-0000-000000000001");
}
