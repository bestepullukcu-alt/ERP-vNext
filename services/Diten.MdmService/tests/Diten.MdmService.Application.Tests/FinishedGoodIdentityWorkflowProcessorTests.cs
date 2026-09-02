using System.Reflection;
using Diten.MdmService.Application.Contracts.Workflow;
using Diten.MdmService.Application.Features.ProductItemSkuMaster.Workflow;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class FinishedGoodIdentityWorkflowProcessorTests
{
    [Theory]
    [InlineData(FinishedGoodIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart)]
    [InlineData(FinishedGoodIdentityWorkflowCheckpoint.Superseded)]
    public async Task Recover_OperatorTerminalRecoveryState_IsPermanentNoOp(
        FinishedGoodIdentityWorkflowCheckpoint checkpoint)
    {
        var harness = new Harness(ProductIdentityDecisionKind.Approved);
        harness.ConfigureCheckpoint(checkpoint);
        var operation = harness.Operation;

        var result = await harness.Processor.RecoverAsync(
            operation, "worker-1", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.True(result.Succeeded);
        Assert.True(result.IsReplay);
        Assert.Same(operation, result.Operation);
    }

    [Fact]
    public async Task Reject_completes_without_parent_dependency_calls()
    {
        var harness = new Harness(ProductIdentityDecisionKind.Rejected);

        var result = await harness.Processor.RecoverAsync(
            harness.Operation, "worker", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.True(result.Succeeded);
        Assert.Equal(FinishedGoodIdentityWorkflowCheckpoint.Completed, result.Operation!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.Draft, harness.FinishedGood.LifecycleStatus);
        Assert.Equal(0, harness.GskuReads);
        Assert.Equal(0, harness.RevisionReads);
    }

    [Fact]
    public async Task Approve_revalidates_approved_gsku_and_revision_exactly_three_times()
    {
        var harness = new Harness(ProductIdentityDecisionKind.Approved);

        var result = await harness.Processor.RecoverAsync(
            harness.Operation, "worker", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.True(result.Succeeded);
        Assert.Equal(FinishedGoodIdentityWorkflowCheckpoint.Completed, result.Operation!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, harness.FinishedGood.LifecycleStatus);
        Assert.Equal(3, harness.GskuReads);
        Assert.Equal(3, harness.RevisionReads);
        Assert.True(result.Operation.ApprovalParentValidatedAtUtcTicksV1 > 0);
        Assert.Matches("^[0-9a-f]{64}$", result.Operation.ApprovalParentProofFingerprint!);
    }

    [Fact]
    public async Task Crash_after_aggregate_decision_replays_without_duplicate_audit_or_second_mutation()
    {
        var harness = new Harness(ProductIdentityDecisionKind.Approved);
        harness.Operations.FailNextAdvanceTo = FinishedGoodIdentityWorkflowCheckpoint.DecisionApplied;

        var interrupted = await harness.Processor.RecoverAsync(
            harness.Operation, "worker", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(interrupted.Succeeded);
        Assert.Equal(FinishedGoodIdentityWorkflowCheckpoint.ApprovalValidated,
            interrupted.Operation!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, harness.FinishedGood.LifecycleStatus);
        Assert.Single(harness.FinishedGood.AuditIntents, intent =>
            intent.Operation == ProductAuditOperation.FinishedGoodIdentityApproved);

        var recovered = await harness.Processor.RecoverAsync(
            harness.Operation, "worker", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.True(recovered.Succeeded);
        Assert.Equal(FinishedGoodIdentityWorkflowCheckpoint.Completed, recovered.Operation!.Checkpoint);
        Assert.Single(harness.FinishedGood.AuditIntents, intent =>
            intent.Operation == ProductAuditOperation.FinishedGoodIdentityApproved);
        Assert.Equal(1, harness.DecisionMutations);
    }

    [Fact]
    public async Task Approval_rejects_cross_tenant_parent_before_mutating_finished_good()
    {
        var harness = new Harness(ProductIdentityDecisionKind.Approved);
        harness.CrossTenantGsku = true;

        var result = await harness.Processor.RecoverAsync(
            harness.Operation, "worker", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(result.Succeeded);
        Assert.Equal(FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            result.Operation!.Checkpoint);
        Assert.Equal(ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired,
            result.Operation.RecoveryDisposition);
        Assert.Equal(ProductIdentityLifecycleStatus.PendingIdentityApproval,
            harness.FinishedGood.LifecycleStatus);
        Assert.Equal(0, harness.DecisionMutations);
    }

    [Theory]
    [InlineData(FinishedGoodIdentityWorkflowCheckpoint.Prepared,
        FinishedGoodIdentityWorkflowCheckpoint.StartOutcomeUnknown)]
    [InlineData(FinishedGoodIdentityWorkflowCheckpoint.StartOutcomeUnknown,
        FinishedGoodIdentityWorkflowCheckpoint.WorkflowStarted)]
    [InlineData(FinishedGoodIdentityWorkflowCheckpoint.WorkflowStarted,
        FinishedGoodIdentityWorkflowCheckpoint.LocalPendingApplied)]
    [InlineData(FinishedGoodIdentityWorkflowCheckpoint.LocalPendingApplied,
        FinishedGoodIdentityWorkflowCheckpoint.AwaitingDecision)]
    [InlineData(FinishedGoodIdentityWorkflowCheckpoint.AwaitingDecision,
        FinishedGoodIdentityWorkflowCheckpoint.DecisionObserved)]
    public async Task Recovery_resumes_each_early_durable_checkpoint_at_its_exact_next_seam(
        FinishedGoodIdentityWorkflowCheckpoint checkpoint,
        FinishedGoodIdentityWorkflowCheckpoint nextCheckpoint)
    {
        var harness = new Harness(ProductIdentityDecisionKind.Approved);
        harness.ConfigureCheckpoint(checkpoint);
        harness.Operations.FailNextAdvanceTo = nextCheckpoint;

        var result = await harness.Processor.RecoverAsync(
            harness.Operation, "worker", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.False(result.Succeeded);
        Assert.Equal("FINISHED_GOOD_IDENTITY_WORKFLOW_CONCURRENCY_CONFLICT", result.ErrorCode);
        Assert.Equal(checkpoint, result.Operation!.Checkpoint);
    }

    [Fact]
    public async Task Recovery_resumes_directly_from_decision_applied_without_second_mutation()
    {
        var harness = new Harness(ProductIdentityDecisionKind.Approved);
        harness.Operations.FailNextAdvanceTo = FinishedGoodIdentityWorkflowCheckpoint.Completed;

        var interrupted = await harness.Processor.RecoverAsync(
            harness.Operation, "worker", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));
        Assert.False(interrupted.Succeeded);
        Assert.Equal(FinishedGoodIdentityWorkflowCheckpoint.DecisionApplied,
            interrupted.Operation!.Checkpoint);
        Assert.Equal(1, harness.DecisionMutations);

        var recovered = await harness.Processor.RecoverAsync(
            harness.Operation, "worker", TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5));

        Assert.True(recovered.Succeeded);
        Assert.Equal(FinishedGoodIdentityWorkflowCheckpoint.Completed, recovered.Operation!.Checkpoint);
        Assert.Equal(1, harness.DecisionMutations);
    }

    [Fact]
    public void Durable_models_do_not_expose_token_secret_or_credential_fields()
    {
        var durableTypes = new[]
        {
            typeof(FinishedGoodIdentityWorkflowOperation),
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

    [Fact]
    public void Finished_good_workflow_contract_has_exact_object_type_and_no_market_dependency_or_evidence()
    {
        Assert.Equal("finished-good",
            FinishedGoodIdentityWorkflowStartRequestFactory.FinishedGoodObjectType);

        var durableNames = typeof(FinishedGoodIdentityWorkflowOperation).GetProperties()
            .Select(property => property.Name)
            .ToArray();
        Assert.DoesNotContain(durableNames, name =>
            name.Contains("Market", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            typeof(FinishedGoodIdentityWorkflowProcessor).GetConstructors().Single().GetParameters(),
            parameter => parameter.ParameterType.Name.Contains(
                "Market", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class Harness
    {
        public Harness(ProductIdentityDecisionKind decision)
        {
            FinishedGood = new FinishedGood
            {
                Id = FinishedGoodId,
                TenantId = TenantId,
                GskuId = GskuId,
                CanonicalCode = "FG-0001",
                CreationCommandId = "create-finished-good",
                LifecycleStatus = ProductIdentityLifecycleStatus.PendingIdentityApproval,
                Version = 1
            };
            Operation = DecisionObserved(decision);
            FinishedGood.IdentityWorkflowBinding = Binding(Operation);
            Operations.Current = Operation;

            var finishedGoodRepository = DispatchProxy.Create<IFinishedGoodRepository, RepositoryProxy>();
            ((RepositoryProxy)(object)finishedGoodRepository).Handler = (method, args) => method.Name switch
            {
                "GetByIdAsync" => Task.FromResult<FinishedGood?>((Guid)args![0]! == FinishedGoodId ? FinishedGood : null),
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
                Operations, finishedGoodRepository, gskuRepository, revisionRepository, workflow,
                new(new(TemplateId, null, [ApproverId], "FINISHED_GOOD_APPROVAL", true, true, null), Clock),
                Clock);
        }

        public FinishedGoodIdentityWorkflowProcessor Processor { get; }
        public FakeOperationRepository Operations { get; } = new();
        public FinishedGoodIdentityWorkflowOperation Operation { get; }
        public FinishedGood FinishedGood { get; }
        public int GskuReads { get; private set; }
        public int RevisionReads { get; private set; }
        public int DecisionMutations { get; private set; }
        public bool CrossTenantGsku { get; set; }

        public void ConfigureCheckpoint(FinishedGoodIdentityWorkflowCheckpoint checkpoint)
        {
            Operation.Checkpoint = checkpoint;
            Operation.RecoveryDisposition = ProductIdentityWorkflowRecoveryDisposition.None;
            if (checkpoint is FinishedGoodIdentityWorkflowCheckpoint.Prepared
                or FinishedGoodIdentityWorkflowCheckpoint.StartOutcomeUnknown
                or FinishedGoodIdentityWorkflowCheckpoint.WorkflowStarted)
            {
                FinishedGood.Version = 0;
                FinishedGood.LifecycleStatus = ProductIdentityLifecycleStatus.Draft;
                FinishedGood.IdentityWorkflowBinding = null;
                FinishedGood.AuditIntents.Clear();
            }
        }

        private Task<Gsku?> ReadGsku(Guid id)
        {
            GskuReads++;
            return Task.FromResult<Gsku?>(id == GskuId
                ? new Gsku
                {
                    Id = GskuId, TenantId = CrossTenantGsku ? Guid.NewGuid() : TenantId,
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

        private Task<FinishedGoodLifecycleWriteResult> Reconcile(object?[] args)
        {
            var expectedVersion = (int)args[1]!;
            var binding = (ProductIdentityWorkflowBinding)args[2]!;
            var evidence = (ProductIdentityWorkflowDecisionEvidence)args[3]!;
            var audit = (LocalAuditIntent)args[4]!;
            Assert.Equal(1, expectedVersion);
            if (FinishedGood.Version == 2 && FinishedGood.AuditIntents.Any(item => item.IntentId == audit.IntentId))
            {
                return Task.FromResult(new FinishedGoodLifecycleWriteResult(true, FinishedGood, IsReplay: true));
            }
            DecisionMutations++;
            FinishedGood.Version = 2;
            FinishedGood.LifecycleStatus = evidence.Decision == ProductIdentityDecisionKind.Approved
                ? ProductIdentityLifecycleStatus.IdentityApproved
                : ProductIdentityLifecycleStatus.Draft;
            Assert.Equal(FinishedGood.IdentityWorkflowBinding!.WorkflowInstanceId,
                binding.WorkflowInstanceId);
            Assert.Equal(FinishedGood.IdentityWorkflowBinding.StartRequestFingerprint,
                binding.StartRequestFingerprint);
            FinishedGood.IdentityWorkflowBinding!.TerminalDecision = evidence;
            FinishedGood.AuditIntents.Add(audit);
            return Task.FromResult(new FinishedGoodLifecycleWriteResult(true, FinishedGood));
        }

        private Task<FinishedGoodLifecycleWriteResult> Submit(object?[] args)
        {
            var expectedVersion = (int)args[1]!;
            var binding = (ProductIdentityWorkflowBinding)args[2]!;
            var audit = (LocalAuditIntent)args[3]!;
            Assert.Equal(0, expectedVersion);
            FinishedGood.Version = 1;
            FinishedGood.LifecycleStatus = ProductIdentityLifecycleStatus.PendingIdentityApproval;
            FinishedGood.IdentityWorkflowBinding = binding;
            FinishedGood.AuditIntents.Add(audit);
            return Task.FromResult(new FinishedGoodLifecycleWriteResult(true, FinishedGood));
        }
    }

    private sealed class FakeOperationRepository : IFinishedGoodIdentityWorkflowOperationRepository
    {
        public FinishedGoodIdentityWorkflowOperation? Current { get; set; }
        public FinishedGoodIdentityWorkflowCheckpoint? FailNextAdvanceTo { get; set; }

        public Task<FinishedGoodIdentityWorkflowReserveResult> ReserveAsync(
            FinishedGoodIdentityWorkflowOperation operation, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<FinishedGoodIdentityWorkflowOperation?> GetByOperationIdAsync(
            Guid operationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Current?.OperationId == operationId ? Current : null);

        public Task<FinishedGoodIdentityWorkflowOperation?> GetByStartIdempotencyKeyAsync(
            string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Current?.StartIdempotencyKey == key ? Current : null);

        public Task<FinishedGoodIdentityWorkflowClaim?> TryClaimAsync(
            FinishedGoodIdentityWorkflowClaimRequest request, CancellationToken cancellationToken = default)
        {
            if (Current is null || !request.EligibleCheckpoints.Contains(Current.Checkpoint))
                return Task.FromResult<FinishedGoodIdentityWorkflowClaim?>(null);
            Current.LeaseGeneration++;
            return Task.FromResult<FinishedGoodIdentityWorkflowClaim?>(new(
                Current.TenantId, Current.OperationId, Current.FinishedGoodId,
                Current.GskuId, Current.ProductDefinitionRevisionId,
                Current.DecisionTransitionLogId, Current.DecisionTransitionSequence,
                Current.OperationFingerprint, request.LeaseOwner, Current.LeaseGeneration,
                Current.Checkpoint, request.LeaseUntilUtcTicks));
        }

        public Task<FinishedGoodIdentityWorkflowRecoverablePage> DiscoverRecoverableAsync(
            long nowUtcTicks, int limit, FinishedGoodIdentityWorkflowRecoveryCursor? after = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new FinishedGoodIdentityWorkflowRecoverablePage(
                Current is null ? [] : [Current], null));

        public Task<bool> AdvanceAsync(
            FinishedGoodIdentityWorkflowClaim claim, FinishedGoodIdentityWorkflowCheckpointMutation mutation,
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
            Current.ApprovalParentValidatedAtUtcTicksV1 = mutation.ApprovalParentValidatedAtUtcTicksV1
                ?? Current.ApprovalParentValidatedAtUtcTicksV1;
            Current.ApprovalParentProofFingerprint = mutation.ApprovalParentProofFingerprint
                ?? Current.ApprovalParentProofFingerprint;
            return Task.FromResult(true);
        }
    }

    public class RepositoryProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Handler(targetMethod!, args);
    }

    private static FinishedGoodIdentityWorkflowOperation DecisionObserved(ProductIdentityDecisionKind decision)
    {
        var reason = decision == ProductIdentityDecisionKind.Rejected ? "REJECTED" : null;
        return new()
        {
            Id = OperationId, TenantId = TenantId, OperationId = OperationId, FinishedGoodId = FinishedGoodId,
            GskuId = GskuId, ProductDefinitionRevisionId = RevisionId, ExpectedFinishedGoodVersion = 0,
            MakerSubjectId = MakerId, WorkflowTemplateId = TemplateId,
            CandidatePrincipalIds = [ApproverId], ReasonCode = "FINISHED_GOOD_APPROVAL",
            ObjectType = "finished-good", ObjectId = FinishedGoodId.ToString("D"), ObjectRef = "FG-0001",
            StartIdempotencyKey = $"finished-good-identity:{TenantId:D}:{OperationId:D}",
            OperationFingerprint = new string('a', 64),
            WorkflowInstanceId = WorkflowId, WorkflowTemplateVersionId = TemplateVersionId,
            ApprovalTaskId = TaskId, AssignmentSnapshotId = SnapshotId,
            StartTransitionLogId = StartLogId, WorkflowStartedAtUtcTicksV1 = StartedAt.UtcTicks,
            DecisionTransitionLogId = DecisionLogId, DecisionKind = decision,
            DecisionObservedAtUtcTicksV1 = DecisionAt.UtcTicks,
            DecisionActorSubjectId = ApproverId, DecisionReasonCode = reason,
            DecisionObjectType = "finished-good", DecisionObjectId = FinishedGoodId.ToString("D"),
            DecisionObjectRef = "FG-0001", DecisionWorkflowTemplateId = TemplateId,
            DecisionWorkflowTemplateVersionId = TemplateVersionId,
            DecisionTaskStatus = decision == ProductIdentityDecisionKind.Approved ? "Approved" : "Rejected",
            DecisionInstanceStatus = decision == ProductIdentityDecisionKind.Approved ? "Completed" : "Rejected",
            DecisionTransitionSequence = 2, DecisionAtUtcTicksV1 = DecisionAt.UtcTicks,
            Checkpoint = FinishedGoodIdentityWorkflowCheckpoint.DecisionObserved,
            TemporalStorageVersion = FinishedGoodIdentityWorkflowOperation.CurrentTemporalStorageVersion
        };
    }

    private static ProductIdentityWorkflowBinding Binding(FinishedGoodIdentityWorkflowOperation operation) => new()
    {
        WorkflowInstanceId = WorkflowId, WorkflowTemplateId = TemplateId,
        WorkflowTemplateVersionId = TemplateVersionId, ApprovalTaskId = TaskId,
        AssignmentSnapshotId = SnapshotId, StartTransitionLogId = StartLogId,
        ObjectType = "finished-good", ObjectId = FinishedGoodId, ObjectRef = "FG-0001",
        SubmitterSubjectId = MakerId, StartIdempotencyKey = operation.StartIdempotencyKey,
        StartRequestFingerprint = operation.OperationFingerprint, SubmittedAtUtc = StartedAt
    };

    private static ProductIdentityWorkflowStartResult StartResult() => new(
        WorkflowId, TemplateId, TemplateVersionId, TaskId, SnapshotId, StartLogId,
        "FG-0001", "Running", "Approval", "Review", StartedAt, null, false, null);

    private static ProductIdentityWorkflowTerminalEvidence TerminalEvidence(
        ProductIdentityDecisionKind decision) => new(
        WorkflowId, TaskId, TemplateId, TemplateVersionId, "finished-good", FinishedGoodId.ToString("D"),
        "FG-0001", decision == ProductIdentityDecisionKind.Approved ? "Approve" : "Reject",
        ApproverId.ToString("D"),
        decision == ProductIdentityDecisionKind.Rejected ? "REJECTED" : null,
        DecisionAt, 2,
        decision == ProductIdentityDecisionKind.Approved ? "Approved" : "Rejected",
        decision == ProductIdentityDecisionKind.Approved ? "Completed" : "Rejected", null);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly FixedTimeProvider Clock = new();
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset StartedAt = Now.AddMinutes(-10);
    private static readonly DateTimeOffset DecisionAt = Now.AddMinutes(-1);
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid FinishedGoodId = Guid.Parse("20000000-0000-0000-0000-000000000001");
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
}
