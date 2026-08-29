using Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Domain.ValueObjects;
using Xunit;

namespace Diten.MdmService.Application.Tests;

public sealed class FirstGskuIdentityRetirementUnitTests
{
    [Fact]
    public async Task Retire_pair_replay_is_exact_and_does_not_duplicate_audit()
    {
        var harness = new Harness();

        var first = await harness.Processor.StartAsync(
            GskuId, 2, OperationId, ActorId, "OBSOLETE", "test", TimeSpan.FromMinutes(1));
        var replay = await harness.Processor.StartAsync(
            GskuId, 2, OperationId, ActorId, "OBSOLETE", "test", TimeSpan.FromMinutes(1));

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded && replay.IsReplay);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, harness.Gsku.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, harness.Revision.LifecycleStatus);
        Assert.Single(harness.Gsku.AuditIntents,
            intent => intent.Operation == ProductAuditOperation.GskuIdentityRetired);
        Assert.Single(harness.Revision.AuditIntents,
            intent => intent.Operation == ProductAuditOperation.ProductDefinitionRevisionIdentityRetired);
    }

    [Fact]
    public async Task Child_blocker_keeps_fence_and_never_retires_or_cascades()
    {
        var harness = new Harness { ChildBlocker = "LSKU_ACTIVE" };

        var result = await harness.Processor.StartAsync(
            GskuId, 2, OperationId, ActorId, "OBSOLETE", "test", TimeSpan.FromMinutes(1));

        Assert.False(result.Succeeded);
        Assert.Equal("FIRST_GSKU_RETIREMENT_CHILD_BLOCKED", result.ErrorCode);
        Assert.Equal(FirstGskuIdentityRetirementCheckpoint.AdmissionFenceClosed,
            harness.Operations.Current!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, harness.Gsku.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, harness.Revision.LifecycleStatus);
        Assert.NotNull(harness.Gsku.RetirementOperationId);
    }

    [Fact]
    public async Task Sibling_race_after_gsku_retirement_blocks_revision_retirement()
    {
        var harness = new Harness { SiblingOnRevisionCheck = true };

        var result = await harness.Processor.StartAsync(
            GskuId, 2, OperationId, ActorId, "OBSOLETE", "test", TimeSpan.FromMinutes(1));

        Assert.False(result.Succeeded);
        Assert.Equal(FirstGskuIdentityRetirementCheckpoint.ManualReconciliationRequired,
            harness.Operations.Current!.Checkpoint);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, harness.Gsku.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, harness.Revision.LifecycleStatus);
    }

    [Fact]
    public async Task Crash_after_gsku_write_before_checkpoint_recovers_without_duplicate_audit()
    {
        var harness = new Harness
        {
            FailNextAdvanceTo = FirstGskuIdentityRetirementCheckpoint.GskuRetired
        };

        var interrupted = await harness.Processor.StartAsync(
            GskuId, 2, OperationId, ActorId, "OBSOLETE", "test", TimeSpan.FromMinutes(1));
        var recovered = await harness.Processor.RecoverAsync(
            harness.Operations.Current!, "recovery", TimeSpan.FromMinutes(1));

        Assert.False(interrupted.Succeeded);
        Assert.True(recovered.Succeeded && recovered.IsReplay);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, harness.Gsku.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.Retired, harness.Revision.LifecycleStatus);
        Assert.Single(harness.Gsku.AuditIntents,
            intent => intent.Operation == ProductAuditOperation.GskuIdentityRetired);
        Assert.Single(harness.Revision.AuditIntents,
            intent => intent.Operation == ProductAuditOperation.ProductDefinitionRevisionIdentityRetired);
    }

    [Fact]
    public async Task Corrupted_retirement_audit_after_pair_writes_is_quarantined()
    {
        var harness = new Harness
        {
            FailNextAdvanceTo = FirstGskuIdentityRetirementCheckpoint.Completed
        };

        var interrupted = await harness.Processor.StartAsync(
            GskuId, 2, OperationId, ActorId, "OBSOLETE", "test", TimeSpan.FromMinutes(1));
        Assert.False(interrupted.Succeeded);
        harness.Gsku.AuditIntents.Single(
            intent => intent.Operation == ProductAuditOperation.GskuIdentityRetired).EvidenceHash = new string('f', 64);

        var recovered = await harness.Processor.RecoverAsync(
            harness.Operations.Current!, "recovery", TimeSpan.FromMinutes(1));

        Assert.False(recovered.Succeeded);
        Assert.Equal(FirstGskuIdentityRetirementCheckpoint.ManualReconciliationRequired,
            harness.Operations.Current!.Checkpoint);
        Assert.Equal("FIRST_GSKU_RETIREMENT_PAIR_INCONSISTENT", recovered.ErrorCode);
    }

    [Fact]
    public async Task Corrupted_or_divergent_approval_evidence_blocks_retirement_before_reservation()
    {
        var harness = new Harness();
        harness.Revision.IdentityWorkflowBinding!.TerminalDecision!.TaskStatus = "Completed";

        var result = await harness.Processor.StartAsync(
            GskuId, 2, OperationId, ActorId, "OBSOLETE", "test", TimeSpan.FromMinutes(1));

        Assert.False(result.Succeeded);
        Assert.Equal("FIRST_GSKU_RETIREMENT_STATE_CONFLICT", result.ErrorCode);
        Assert.Null(harness.Operations.Current);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, harness.Gsku.LifecycleStatus);
        Assert.Equal(ProductIdentityLifecycleStatus.IdentityApproved, harness.Revision.LifecycleStatus);
    }

    private sealed class Harness
    {
        public Harness()
        {
            Operations = new(this);
            var binding = Binding();
            Revision = new ProductDefinitionRevision
            {
                Id = RevisionId, TenantId = TenantId, GlobalProductId = ProductId,
                RevisionIdentifier = "REV-001", CreationCommandId = "CREATE-1",
                Version = 4, LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved,
                IdentityWorkflowBinding = binding
            };
            Gsku = new Gsku
            {
                Id = GskuId, TenantId = TenantId, ProductDefinitionRevisionId = RevisionId,
                CanonicalCode = "GS-001", CreationCommandId = "CREATE-1",
                Version = 2, LifecycleStatus = ProductIdentityLifecycleStatus.IdentityApproved,
                IdentityWorkflowBinding = Binding()
            };
            Gskus = new(this);
            Revisions = new(this);
            Processor = new(Operations, Gskus, Revisions, new FixedTimeProvider(Now));
        }

        public FirstGskuIdentityRetirementProcessor Processor { get; }
        public FakeOperations Operations { get; }
        public FakeGskus Gskus { get; }
        public FakeRevisions Revisions { get; }
        public Gsku Gsku { get; }
        public ProductDefinitionRevision Revision { get; }
        public string? ChildBlocker { get; set; }
        public bool SiblingOnRevisionCheck { get; set; }
        public int SiblingChecks { get; set; }
        public FirstGskuIdentityRetirementCheckpoint? FailNextAdvanceTo { get; set; }
    }

    private sealed class FakeGskus(Harness harness) : IGskuRepository
    {
        public Task<Gsku?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<Gsku?>(id == GskuId ? harness.Gsku : null);
        public Task<Gsku?> GetReferenceableByIdAsync(Guid id, CancellationToken ct = default) => GetByIdAsync(id, ct);
        public Task<IReadOnlyList<Gsku>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Gsku>>([]);
        public Task<GskuPage> GetReferenceablePageAsync(int page, int size, string? search, CancellationToken ct = default) =>
            Task.FromResult(new GskuPage([], 0));
        public Task<IReadOnlyList<Guid>> FindIdsByCanonicalCodeAsync(string search, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<Gsku?> GetByCreationCommandIdAsync(string command, CancellationToken ct = default) =>
            Task.FromResult<Gsku?>(null);
        public Task<GskuCreateResult> CreateDraftAsync(Gsku gsku, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<GskuUpdateResult> UpdateDraftAsync(Gsku gsku, int version, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<FirstGskuIdentityRetirementWriteResult<Gsku>> CloseChildAdmissionFenceAsync(
            Guid id, int expected, Guid operationId, string fingerprint, CancellationToken ct = default)
        {
            if (harness.Gsku.Version == expected)
            {
                harness.Gsku.Version++;
                harness.Gsku.RetirementOperationId = operationId;
                harness.Gsku.RetirementOperationFingerprint = fingerprint;
            }
            return Task.FromResult(new FirstGskuIdentityRetirementWriteResult<Gsku>(
                true, harness.Gsku.Version != expected + 1, harness.Gsku, null));
        }
        public async Task<string?> FindRetirementBlockerAsync(Guid id, CancellationToken ct = default) =>
            harness.ChildBlocker ?? (await HasNonRetiredSiblingAsync(RevisionId, id, ct)
                ? "DEPENDENT_IDENTITIES_EXIST"
                : null);
        public Task<bool> HasNonRetiredSiblingAsync(Guid revisionId, Guid excluded, CancellationToken ct = default)
        {
            harness.SiblingChecks++;
            return Task.FromResult(harness.SiblingOnRevisionCheck && harness.SiblingChecks >= 3);
        }
        public Task<FirstGskuIdentityRetirementWriteResult<Gsku>> RetireIdentityAsync(
            Guid id, int expected, Guid operationId, string fingerprint,
            LocalAuditIntent intent, CancellationToken ct = default)
        {
            if (harness.Gsku.Version == expected)
            {
                harness.Gsku.Version++;
                harness.Gsku.LifecycleStatus = ProductIdentityLifecycleStatus.Retired;
                harness.Gsku.RetirementOperationId = operationId;
                harness.Gsku.RetirementOperationFingerprint = fingerprint;
                harness.Gsku.AuditIntents.Add(intent);
                return Task.FromResult(new FirstGskuIdentityRetirementWriteResult<Gsku>(true, false, harness.Gsku, null));
            }
            return Task.FromResult(new FirstGskuIdentityRetirementWriteResult<Gsku>(
                harness.Gsku.LifecycleStatus == ProductIdentityLifecycleStatus.Retired,
                true, harness.Gsku, null));
        }
    }

    private sealed class FakeRevisions(Harness harness) : IProductDefinitionRevisionRepository
    {
        public Task<ProductDefinitionRevision?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult<ProductDefinitionRevision?>(id == RevisionId ? harness.Revision : null);
        public Task<ProductDefinitionRevision?> GetByCreationCommandIdAsync(string id, CancellationToken ct = default) =>
            Task.FromResult<ProductDefinitionRevision?>(null);
        public Task<FirstGskuPairAllocationResult> AllocateForFirstGskuAsync(Guid id, string command, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<ProductDefinitionRevisionCreateResult> CreateForFirstGskuAsync(ProductDefinitionRevision revision, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<FirstGskuIdentityRetirementWriteResult<ProductDefinitionRevision>> RetireIdentityAsync(
            Guid id, int expected, Guid operationId, string fingerprint,
            LocalAuditIntent intent, CancellationToken ct = default)
        {
            if (harness.Revision.Version == expected)
            {
                harness.Revision.Version++;
                harness.Revision.LifecycleStatus = ProductIdentityLifecycleStatus.Retired;
                harness.Revision.RetirementOperationId = operationId;
                harness.Revision.RetirementOperationFingerprint = fingerprint;
                harness.Revision.AuditIntents.Add(intent);
                return Task.FromResult(new FirstGskuIdentityRetirementWriteResult<ProductDefinitionRevision>(
                    true, false, harness.Revision, null));
            }
            return Task.FromResult(new FirstGskuIdentityRetirementWriteResult<ProductDefinitionRevision>(
                harness.Revision.LifecycleStatus == ProductIdentityLifecycleStatus.Retired,
                true, harness.Revision, null));
        }
    }

    private sealed class FakeOperations(Harness harness) : IFirstGskuIdentityRetirementOperationRepository
    {
        public FirstGskuIdentityRetirementOperation? Current { get; private set; }
        public Task<FirstGskuIdentityRetirementReserveResult> ReserveAsync(
            FirstGskuIdentityRetirementOperation operation, CancellationToken ct = default)
        {
            Current ??= operation;
            return Task.FromResult(new FirstGskuIdentityRetirementReserveResult(
                true, !ReferenceEquals(Current, operation), Current, null));
        }
        public Task<FirstGskuIdentityRetirementOperation?> GetByOperationIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Current?.OperationId == id ? Current : null);
        public Task<FirstGskuIdentityRetirementClaim?> TryClaimAsync(
            FirstGskuIdentityRetirementClaimRequest request, CancellationToken ct = default)
        {
            if (Current is null || !request.EligibleCheckpoints.Contains(Current.Checkpoint))
                return Task.FromResult<FirstGskuIdentityRetirementClaim?>(null);
            Current.LeaseGeneration++;
            return Task.FromResult<FirstGskuIdentityRetirementClaim?>(new(
                Current.TenantId, Current.OperationId, Current.OperationFingerprint,
                request.LeaseOwner, Current.LeaseGeneration, Current.Checkpoint, request.LeaseUntilUtcTicks));
        }
        public Task<bool> AdvanceAsync(
            FirstGskuIdentityRetirementClaim claim, FirstGskuIdentityRetirementMutation mutation,
            CancellationToken ct = default)
        {
            if (Current is null || Current.Checkpoint != claim.Checkpoint) return Task.FromResult(false);
            if (harness.FailNextAdvanceTo == mutation.NextCheckpoint)
            {
                harness.FailNextAdvanceTo = null;
                return Task.FromResult(false);
            }
            Current.Checkpoint = mutation.NextCheckpoint;
            Current.RecoveryDisposition = mutation.RecoveryDisposition;
            Current.NextAttemptAtUtcTicksV1 = mutation.NextAttemptAtUtcTicksV1;
            Current.LastFailureCode = mutation.LastFailureCode;
            return Task.FromResult(true);
        }

        public Task<FirstGskuIdentityRetirementRecoverablePage> DiscoverRecoverableAsync(
            long now, int limit, FirstGskuIdentityRetirementRecoveryCursor? after = null,
            CancellationToken ct = default) => Task.FromResult(new FirstGskuIdentityRetirementRecoverablePage([], null));
    }

    private static FirstGskuIdentityWorkflowBinding Binding() => new()
    {
        WorkflowInstanceId = WorkflowId,
        WorkflowTemplateId = Guid.Parse("a0000000-0000-0000-0000-000000000002"),
        WorkflowTemplateVersionId = Guid.Parse("a0000000-0000-0000-0000-000000000003"),
        ApprovalTaskId = Guid.Parse("a0000000-0000-0000-0000-000000000004"),
        AssignmentSnapshotId = Guid.Parse("a0000000-0000-0000-0000-000000000005"),
        StartTransitionLogId = Guid.Parse("a0000000-0000-0000-0000-000000000006"),
        ObjectType = "gsku", GskuId = GskuId, ProductDefinitionRevisionId = RevisionId,
        ObjectRef = "GS-001", SubmitterSubjectId = Guid.Parse("a0000000-0000-0000-0000-000000000007"),
        StartIdempotencyKey = "first-gsku", StartRequestFingerprint = new string('a', 64),
        SubmittedAtUtc = Now,
        TerminalDecision = new()
        {
            Decision = ProductIdentityDecisionKind.Approved,
            WorkflowInstanceId = WorkflowId,
            ApprovalTaskId = Guid.Parse("a0000000-0000-0000-0000-000000000004"),
            WorkflowTemplateId = Guid.Parse("a0000000-0000-0000-0000-000000000002"),
            WorkflowTemplateVersionId = Guid.Parse("a0000000-0000-0000-0000-000000000003"),
            ObjectType = "gsku", ObjectId = GskuId, ObjectRef = "GS-001",
            DecisionActorSubjectId = Guid.Parse("a0000000-0000-0000-0000-000000000008"),
            DecisionAtUtc = Now, TransitionSequence = 2, TaskStatus = "Approved", InstanceStatus = "Completed"
        }
    };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 29, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ProductId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid RevisionId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid GskuId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid OperationId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly Guid WorkflowId = Guid.Parse("70000000-0000-0000-0000-000000000001");
}
