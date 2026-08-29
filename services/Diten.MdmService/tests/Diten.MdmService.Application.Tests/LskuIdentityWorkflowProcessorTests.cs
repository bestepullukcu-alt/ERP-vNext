using System.Reflection;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class LskuIdentityWorkflowProcessorTests
{
    [Fact]
    public async Task Reject_completes_without_parent_or_market_dependency_calls()
    {
        var harness = new Harness(ProductIdentityDecisionKind.Rejected);

        var result = await harness.Processor.RecoverAsync(
            harness.Operation, "worker", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.True(result.Succeeded);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.Completed, result.Operation!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, harness.Lsku.LifecycleStatus);
        Assert.Equal(0, harness.GskuReads);
        Assert.Equal(0, harness.RevisionReads);
        Assert.Equal(0, harness.Market.Calls);
    }

    [Fact]
    public async Task Approve_revalidates_dependencies_three_times_and_preserves_create_time_market_selection()
    {
        var harness = new Harness(ProductIdentityDecisionKind.Approved);
        var createTimeVersion = harness.Lsku.MarketSelection.CatalogVersionId;
        var createTimeResolvedAt = harness.Lsku.MarketSelection.ResolvedAtUtc;

        var result = await harness.Processor.RecoverAsync(
            harness.Operation, "worker", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.True(result.Succeeded);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.Completed, result.Operation!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, harness.Lsku.LifecycleStatus);
        Assert.Equal(3, harness.GskuReads);
        Assert.Equal(3, harness.RevisionReads);
        Assert.Equal(3, harness.Market.Calls);
        Assert.Equal(createTimeVersion, harness.Lsku.MarketSelection.CatalogVersionId);
        Assert.Equal(createTimeResolvedAt, harness.Lsku.MarketSelection.ResolvedAtUtc);
        Assert.NotNull(result.Operation.ApprovalMarketSelection);
        Assert.Equal(CurrentMarketVersionId, result.Operation.ApprovalMarketSelection!.CatalogVersionId);
        Assert.NotEqual(result.Operation.MarketSelection.CatalogVersionId,
            result.Operation.ApprovalMarketSelection.CatalogVersionId);
    }

    [Fact]
    public async Task Crash_after_aggregate_decision_replays_without_duplicate_audit_or_second_mutation()
    {
        var harness = new Harness(ProductIdentityDecisionKind.Approved);
        harness.Operations.FailNextAdvanceTo = LskuIdentityWorkflowCheckpoint.DecisionApplied;

        var interrupted = await harness.Processor.RecoverAsync(
            harness.Operation, "worker", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(interrupted.Succeeded);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.ApprovalValidated,
            interrupted.Operation!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, harness.Lsku.LifecycleStatus);
        Assert.Single(harness.Lsku.AuditIntents, intent =>
            intent.Operation == ProductAuditOperation.LskuIdentityApproved);

        var recovered = await harness.Processor.RecoverAsync(
            harness.Operation, "worker", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.True(recovered.Succeeded);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.Completed, recovered.Operation!.Checkpoint);
        Assert.Single(harness.Lsku.AuditIntents, intent =>
            intent.Operation == ProductAuditOperation.LskuIdentityApproved);
        Assert.Equal(1, harness.DecisionMutations);
    }

    [Theory]
    [InlineData(404, false)]
    [InlineData(409, false)]
    [InlineData(503, true)]
    [InlineData(504, true)]
    public async Task Market_failure_is_terminal_only_for_non_retryable_contract_results(
        int statusCode, bool retryable)
    {
        var harness = new Harness(ProductIdentityDecisionKind.Approved);
        harness.Market.Result = VerifiedMarketReferenceResolveResult.Fail(
            statusCode, $"MARKET_{statusCode}");

        var result = await harness.Processor.RecoverAsync(
            harness.Operation, "worker", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(result.Succeeded);
        Assert.Equal(retryable
                ? LskuIdentityWorkflowCheckpoint.DecisionObserved
                : LskuIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            result.Operation!.Checkpoint);
        Assert.Equal(retryable
                ? ProductIdentityWorkflowRecoveryDisposition.Retryable
                : ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired,
            result.Operation.RecoveryDisposition);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval,
            harness.Lsku.LifecycleStatus);
    }

    [Theory]
    [InlineData(LskuIdentityWorkflowCheckpoint.Prepared,
        LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown)]
    [InlineData(LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown,
        LskuIdentityWorkflowCheckpoint.WorkflowStarted)]
    [InlineData(LskuIdentityWorkflowCheckpoint.WorkflowStarted,
        LskuIdentityWorkflowCheckpoint.LocalPendingApplied)]
    [InlineData(LskuIdentityWorkflowCheckpoint.LocalPendingApplied,
        LskuIdentityWorkflowCheckpoint.AwaitingDecision)]
    [InlineData(LskuIdentityWorkflowCheckpoint.AwaitingDecision,
        LskuIdentityWorkflowCheckpoint.DecisionObserved)]
    public async Task Recovery_resumes_each_early_durable_checkpoint_at_its_exact_next_seam(
        LskuIdentityWorkflowCheckpoint checkpoint,
        LskuIdentityWorkflowCheckpoint nextCheckpoint)
    {
        var harness = new Harness(ProductIdentityDecisionKind.Approved);
        harness.ConfigureCheckpoint(checkpoint);
        harness.Operations.FailNextAdvanceTo = nextCheckpoint;

        var result = await harness.Processor.RecoverAsync(
            harness.Operation, "worker", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(result.Succeeded);
        Assert.Equal("LSKU_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT", result.ErrorCode);
        Assert.Equal(checkpoint, result.Operation!.Checkpoint);
    }

    [Fact]
    public async Task Recovery_resumes_directly_from_decision_applied_without_second_mutation()
    {
        var harness = new Harness(ProductIdentityDecisionKind.Approved);
        harness.Operations.FailNextAdvanceTo = LskuIdentityWorkflowCheckpoint.Completed;

        var interrupted = await harness.Processor.RecoverAsync(
            harness.Operation, "worker", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        Assert.False(interrupted.Succeeded);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.DecisionApplied,
            interrupted.Operation!.Checkpoint);
        Assert.Equal(1, harness.DecisionMutations);

        var recovered = await harness.Processor.RecoverAsync(
            harness.Operation, "worker", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.True(recovered.Succeeded);
        Assert.Equal(LskuIdentityWorkflowCheckpoint.Completed, recovered.Operation!.Checkpoint);
        Assert.Equal(1, harness.DecisionMutations);
    }

    [Fact]
    public void Durable_models_do_not_expose_token_secret_or_credential_fields()
    {
        var durableTypes = new[]
        {
            typeof(LskuIdentityWorkflowOperation),
            typeof(ProductIdentityWorkflowBinding),
            typeof(ProductIdentityWorkflowDecisionEvidence)
        };
        var forbidden = new[] { "Token", "Secret", "Credential", "Password", "Bearer" };

        var matches = durableTypes.SelectMany(type => type.GetProperties()
                .Select(property => $"{type.Name}.{property.Name}"))
            .Where(name => forbidden.Any(term =>
                name.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        Assert.Empty(matches);
    }

    private sealed class Harness
    {
        public Harness(ProductIdentityDecisionKind decision)
        {
            Lsku = new Lsku
            {
                Id = LskuId,
                TenantId = TenantId,
                GskuId = GskuId,
                CanonicalCode = "LS-0001",
                CreationCommandId = "create-lsku",
                MarketCode = "TR",
                MarketSelection = Selection(CreateMarketVersionId, 1, CreateMarketTime),
                LifecycleStatus = ProductIdentityLifecycleStatus.PendingIdentityApproval,
                Version = 1
            };
            Operation = DecisionObserved(decision);
            Lsku.IdentityWorkflowBinding = Binding(Operation);
            Operations.Current = Operation;

            var lskuRepository = DispatchProxy.Create<ILskuRepository, RepositoryProxy>();
            ((RepositoryProxy)(object)lskuRepository).Handler = (method, args) => method.Name switch
            {
                "GetByIdAsync" => Task.FromResult<Lsku?>((Guid)args![0]! == LskuId ? Lsku : null),
                "SubmitIdentityAsync" => Submit(args!),
                "ReconcileIdentityDecisionAsync" => Reconcile(args!),
                _ => throw new NotSupportedException(method.Name)
            };
            var gskuRepository = DispatchProxy.Create<IGskuRepository, RepositoryProxy>();
            ((RepositoryProxy)(object)gskuRepository).Handler = (method, args) => method.Name == "GetByIdAsync"
                ? ReadGsku((Guid)args![0]!)
                : throw new NotSupportedException(method.Name);
            var revisionRepository = DispatchProxy.Create<IProductDefinitionRevisionRepository, RepositoryProxy>();
            ((RepositoryProxy)(object)revisionRepository).Handler = (method, args) => method.Name == "GetByIdAsync"
                ? ReadRevision((Guid)args![0]!)
                : throw new NotSupportedException(method.Name);
            var workflow = DispatchProxy.Create<IProductIdentityWorkflowClient, RepositoryProxy>();
            ((RepositoryProxy)(object)workflow).Handler = (method, _) => method.Name switch
            {
                "GetStartResultAsync" => Task.FromResult(
                    ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowStartResult>.Success(
                        StartResult())),
                "GetTerminalEvidenceAsync" => Task.FromResult(
                    ProductIdentityWorkflowTransportResult<ProductIdentityWorkflowTerminalEvidence>.Success(
                        TerminalEvidence(decision))),
                _ => throw new InvalidOperationException(
                    $"Workflow dependency must not be called from {method.Name}.")
            };

            Processor = new(
                Operations, lskuRepository, gskuRepository, revisionRepository, Market, workflow,
                new(new(TemplateId, null, [ApproverId], "LSKU_APPROVAL", true, true, null), Clock),
                Clock);
        }

        public LskuIdentityWorkflowProcessor Processor { get; }
        public FakeOperationRepository Operations { get; } = new();
        public MarketResolver Market { get; } = new();
        public LskuIdentityWorkflowOperation Operation { get; }
        public Lsku Lsku { get; }
        public int GskuReads { get; private set; }
        public int RevisionReads { get; private set; }
        public int DecisionMutations { get; private set; }

        public void ConfigureCheckpoint(LskuIdentityWorkflowCheckpoint checkpoint)
        {
            Operation.Checkpoint = checkpoint;
            Operation.RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None;
            if (checkpoint is LskuIdentityWorkflowCheckpoint.Prepared
                or LskuIdentityWorkflowCheckpoint.StartOutcomeUnknown
                or LskuIdentityWorkflowCheckpoint.WorkflowStarted)
            {
                Lsku.Version = 0;
                Lsku.LifecycleStatus = ProductIdentityLifecycleStatus.Draft;
                Lsku.IdentityWorkflowBinding = null;
                Lsku.AuditIntents.Clear();
            }
        }

        private Task<Gsku?> ReadGsku(Guid id)
        {
            GskuReads++;
            return Task.FromResult<Gsku?>(id == GskuId
                ? new Gsku
                {
                    Id = GskuId, TenantId = TenantId,
                    ProductDefinitionRevisionId = RevisionId,
                    LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved
                }
                : null);
        }

        private Task<ProductDefinitionRevision?> ReadRevision(Guid id)
        {
            RevisionReads++;
            return Task.FromResult<ProductDefinitionRevision?>(id == RevisionId
                ? new ProductDefinitionRevision
                {
                    Id = RevisionId, TenantId = TenantId,
                    LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved
                }
                : null);
        }

        private Task<LskuLifecycleWriteResult> Reconcile(object?[] args)
        {
            var expectedVersion = (int)args[1]!;
            var evidence = (ProductIdentityWorkflowDecisionEvidence)args[2]!;
            var audit = (LocalAuditIntent)args[3]!;
            Assert.Equal(1, expectedVersion);
            if (Lsku.Version == 2 && Lsku.AuditIntents.Any(item => item.IntentId == audit.IntentId))
            {
                return Task.FromResult(new LskuLifecycleWriteResult(true, Lsku, IsReplay: true));
            }
            DecisionMutations++;
            Lsku.Version = 2;
            Lsku.LifecycleStatus = evidence.Decision == ProductIdentityDecisionKind.Approved
                ? ProductIdentityLifecycleStatus.IdentityApproved
                : ProductIdentityLifecycleStatus.Draft;
            Lsku.IdentityWorkflowBinding!.TerminalDecision = evidence;
            Lsku.AuditIntents.Add(audit);
            return Task.FromResult(new LskuLifecycleWriteResult(true, Lsku));
        }

        private Task<LskuLifecycleWriteResult> Submit(object?[] args)
        {
            var expectedVersion = (int)args[1]!;
            var binding = (ProductIdentityWorkflowBinding)args[2]!;
            var audit = (LocalAuditIntent)args[3]!;
            Assert.Equal(0, expectedVersion);
            Lsku.Version = 1;
            Lsku.LifecycleStatus = ProductIdentityLifecycleStatus.PendingIdentityApproval;
            Lsku.IdentityWorkflowBinding = binding;
            Lsku.AuditIntents.Add(audit);
            return Task.FromResult(new LskuLifecycleWriteResult(true, Lsku));
        }
    }

    private sealed class FakeOperationRepository : ILskuIdentityWorkflowOperationRepository
    {
        public LskuIdentityWorkflowOperation? Current { get; set; }
        public LskuIdentityWorkflowCheckpoint? FailNextAdvanceTo { get; set; }

        public Task<LskuIdentityWorkflowReserveResult> ReserveAsync(
            LskuIdentityWorkflowOperation operation, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<LskuIdentityWorkflowOperation?> GetByOperationIdAsync(
            Guid operationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Current?.OperationId == operationId ? Current : null);

        public Task<LskuIdentityWorkflowOperation?> GetByStartIdempotencyKeyAsync(
            string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Current?.StartIdempotencyKey == key ? Current : null);

        public Task<LskuIdentityWorkflowClaim?> TryClaimAsync(
            LskuIdentityWorkflowClaimRequest request, CancellationToken cancellationToken = default)
        {
            if (Current is null || !request.EligibleCheckpoints.Contains(Current.Checkpoint))
                return Task.FromResult<LskuIdentityWorkflowClaim?>(null);
            Current.LeaseGeneration++;
            return Task.FromResult<LskuIdentityWorkflowClaim?>(new(
                Current.TenantId, Current.OperationId, Current.LskuId, Current.MarketCode,
                Current.DecisionTransitionLogId, Current.DecisionTransitionSequence,
                Current.OperationFingerprint, request.LeaseOwner, Current.LeaseGeneration,
                Current.Checkpoint, request.LeaseUntilUtcTicks));
        }

        public Task<LskuIdentityWorkflowRecoverablePage> DiscoverRecoverableAsync(
            long nowUtcTicks, int limit, LskuIdentityWorkflowRecoveryCursor? after = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LskuIdentityWorkflowRecoverablePage(
                Current is null ? [] : [Current], null));

        public Task<bool> AdvanceAsync(
            LskuIdentityWorkflowClaim claim, LskuIdentityWorkflowCheckpointMutation mutation,
            CancellationToken cancellationToken = default)
        {
            if (Current is null || Current.Checkpoint != claim.Checkpoint) return Task.FromResult(false);
            if (FailNextAdvanceTo == mutation.NextCheckpoint)
            {
                FailNextAdvanceTo = null;
                return Task.FromResult(false);
            }
            Current.Checkpoint = mutation.NextCheckpoint;
            Current.RecoveryDisposition = mutation.RecoveryDisposition;
            Current.NextAttemptAtUtcTicksV1 = mutation.NextAttemptAtUtcTicksV1;
            Current.LastFailureCode = mutation.LastFailureCode;
            Current.WorkflowInstanceId = mutation.WorkflowInstanceId ?? Current.WorkflowInstanceId;
            Current.WorkflowTemplateId = mutation.WorkflowTemplateId ?? Current.WorkflowTemplateId;
            Current.WorkflowTemplateVersionId = mutation.WorkflowTemplateVersionId
                ?? Current.WorkflowTemplateVersionId;
            Current.ApprovalTaskId = mutation.ApprovalTaskId ?? Current.ApprovalTaskId;
            Current.AssignmentSnapshotId = mutation.AssignmentSnapshotId ?? Current.AssignmentSnapshotId;
            Current.StartTransitionLogId = mutation.StartTransitionLogId ?? Current.StartTransitionLogId;
            Current.WorkflowStartedAtUtcTicksV1 = mutation.WorkflowStartedAtUtcTicksV1
                ?? Current.WorkflowStartedAtUtcTicksV1;
            Current.DecisionKind = mutation.DecisionKind ?? Current.DecisionKind;
            Current.DecisionObservedAtUtcTicksV1 = mutation.DecisionObservedAtUtcTicksV1
                ?? Current.DecisionObservedAtUtcTicksV1;
            Current.DecisionActorSubjectId = mutation.DecisionActorSubjectId
                ?? Current.DecisionActorSubjectId;
            Current.DecisionReasonCode = mutation.DecisionReasonCode ?? Current.DecisionReasonCode;
            Current.DecisionObjectType = mutation.DecisionObjectType ?? Current.DecisionObjectType;
            Current.DecisionObjectId = mutation.DecisionObjectId ?? Current.DecisionObjectId;
            Current.DecisionObjectRef = mutation.DecisionObjectRef ?? Current.DecisionObjectRef;
            Current.DecisionWorkflowTemplateId = mutation.DecisionWorkflowTemplateId
                ?? Current.DecisionWorkflowTemplateId;
            Current.DecisionWorkflowTemplateVersionId = mutation.DecisionWorkflowTemplateVersionId
                ?? Current.DecisionWorkflowTemplateVersionId;
            Current.DecisionTaskStatus = mutation.DecisionTaskStatus ?? Current.DecisionTaskStatus;
            Current.DecisionInstanceStatus = mutation.DecisionInstanceStatus
                ?? Current.DecisionInstanceStatus;
            Current.DecisionTransitionSequence = mutation.DecisionTransitionSequence
                ?? Current.DecisionTransitionSequence;
            Current.DecisionAtUtcTicksV1 = mutation.DecisionAtUtcTicksV1
                ?? Current.DecisionAtUtcTicksV1;
            Current.ApprovalMarketSelection = mutation.ApprovalMarketSelection ?? Current.ApprovalMarketSelection;
            Current.MarketValidatedAtUtcTicksV1 = mutation.MarketValidatedAtUtcTicksV1
                ?? Current.MarketValidatedAtUtcTicksV1;
            Current.ApprovalMarketProofFingerprint = mutation.ApprovalMarketProofFingerprint
                ?? Current.ApprovalMarketProofFingerprint;
            return Task.FromResult(true);
        }
    }

    private sealed class MarketResolver : IWorkflowVerifiedMarketReferenceResolver
    {
        public int Calls { get; private set; }
        public VerifiedMarketReferenceResolveResult Result { get; set; } =
            VerifiedMarketReferenceResolveResult.Success(new(
                "market", "TR", CurrentMarketVersionId, 2, "LATEST", CurrentMarketTime));

        public Task<VerifiedMarketReferenceResolveResult> ResolveLatestAsync(
            Guid tenantId, string marketCode, CancellationToken cancellationToken = default)
        {
            Calls++;
            Assert.Equal(TenantId, tenantId);
            Assert.Equal("TR", marketCode);
            return Task.FromResult(Result);
        }
    }

    public class RepositoryProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Handler(targetMethod!, args);
    }

    private static LskuIdentityWorkflowOperation DecisionObserved(ProductIdentityDecisionKind decision)
    {
        var reason = decision == ProductIdentityDecisionKind.Rejected ? "REJECTED" : null;
        return new()
        {
            Id = OperationId, TenantId = TenantId, OperationId = OperationId, LskuId = LskuId,
            GskuId = GskuId, ProductDefinitionRevisionId = RevisionId, ExpectedLskuVersion = 0,
            MakerSubjectId = MakerId, WorkflowTemplateId = TemplateId,
            CandidatePrincipalIds = [ApproverId], ReasonCode = "LSKU_APPROVAL",
            ObjectType = "lsku", ObjectId = LskuId.ToString("D"), ObjectRef = "LS-0001",
            MarketCode = "TR", MarketSelection = Selection(CreateMarketVersionId, 1, CreateMarketTime),
            StartIdempotencyKey = $"lsku-identity:{TenantId:D}:{OperationId:D}",
            OperationFingerprint = new string('a', 64),
            WorkflowInstanceId = WorkflowId, WorkflowTemplateVersionId = TemplateVersionId,
            ApprovalTaskId = TaskId, AssignmentSnapshotId = SnapshotId,
            StartTransitionLogId = StartLogId, WorkflowStartedAtUtcTicksV1 = StartedAt.UtcTicks,
            DecisionTransitionLogId = DecisionLogId, DecisionKind = decision,
            DecisionObservedAtUtcTicksV1 = DecisionAt.UtcTicks,
            DecisionActorSubjectId = ApproverId, DecisionReasonCode = reason,
            DecisionObjectType = "lsku", DecisionObjectId = LskuId.ToString("D"),
            DecisionObjectRef = "LS-0001", DecisionWorkflowTemplateId = TemplateId,
            DecisionWorkflowTemplateVersionId = TemplateVersionId,
            DecisionTaskStatus = decision == ProductIdentityDecisionKind.Approved ? "Approved" : "Rejected",
            DecisionInstanceStatus = decision == ProductIdentityDecisionKind.Approved ? "Completed" : "Rejected",
            DecisionTransitionSequence = 2, DecisionAtUtcTicksV1 = DecisionAt.UtcTicks,
            Checkpoint = LskuIdentityWorkflowCheckpoint.DecisionObserved,
            TemporalStorageVersion = LskuIdentityWorkflowOperation.CurrentTemporalStorageVersion
        };
    }

    private static ProductIdentityWorkflowBinding Binding(LskuIdentityWorkflowOperation operation) => new()
    {
        WorkflowInstanceId = WorkflowId, WorkflowTemplateId = TemplateId,
        WorkflowTemplateVersionId = TemplateVersionId, ApprovalTaskId = TaskId,
        AssignmentSnapshotId = SnapshotId, StartTransitionLogId = StartLogId,
        ObjectType = "lsku", ObjectId = LskuId, ObjectRef = "LS-0001",
        SubmitterSubjectId = MakerId, StartIdempotencyKey = operation.StartIdempotencyKey,
        StartRequestFingerprint = operation.OperationFingerprint, SubmittedAtUtc = StartedAt
    };

    private static ProductIdentityWorkflowStartResult StartResult() => new(
        WorkflowId, TemplateId, TemplateVersionId, TaskId, SnapshotId, StartLogId,
        "LS-0001", "Running", "Approval", "Review", StartedAt, null, false, null);

    private static ProductIdentityWorkflowTerminalEvidence TerminalEvidence(
        ProductIdentityDecisionKind decision) => new(
        WorkflowId, TaskId, TemplateId, TemplateVersionId, "lsku", LskuId.ToString("D"),
        "LS-0001", decision == ProductIdentityDecisionKind.Approved ? "Approve" : "Reject",
        ApproverId.ToString("D"),
        decision == ProductIdentityDecisionKind.Rejected ? "REJECTED" : null,
        DecisionAt, 2,
        decision == ProductIdentityDecisionKind.Approved ? "Approved" : "Rejected",
        decision == ProductIdentityDecisionKind.Approved ? "Completed" : "Rejected", null);

    private static ReferenceCatalogSelection Selection(Guid versionId, int version, DateTimeOffset time) => new()
    {
        SetCode = "market", ValueCode = "TR", CatalogVersionId = versionId,
        CatalogVersionNumber = version, ResolutionMode = ReferenceCatalogResolutionMode.Latest,
        ResolvedAtUtc = time
    };

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly FixedTimeProvider Clock = new();
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset StartedAt = Now.AddMinutes(-10);
    private static readonly DateTimeOffset DecisionAt = Now.AddMinutes(-1);
    private static readonly DateTimeOffset CreateMarketTime = Now.AddDays(-2);
    private static readonly DateTimeOffset CurrentMarketTime = Now.AddMinutes(-2);
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid LskuId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid GskuId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid RevisionId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid OperationId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid MakerId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly Guid ApproverId = Guid.Parse("70000000-0000-0000-0000-000000000001");
    private static readonly Guid TemplateId = Guid.Parse("80000000-0000-0000-0000-000000000001");
    private static readonly Guid TemplateVersionId = Guid.Parse("90000000-0000-0000-0000-000000000001");
    private static readonly Guid WorkflowId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid TaskId = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    private static readonly Guid SnapshotId = Guid.Parse("c0000000-0000-0000-0000-000000000001");
    private static readonly Guid StartLogId = Guid.Parse("d0000000-0000-0000-0000-000000000001");
    private static readonly Guid DecisionLogId = Guid.Parse("e0000000-0000-0000-0000-000000000001");
    private static readonly Guid CreateMarketVersionId = Guid.Parse("f0000000-0000-0000-0000-000000000001");
    private static readonly Guid CurrentMarketVersionId = Guid.Parse("f0000000-0000-0000-0000-000000000002");
}
