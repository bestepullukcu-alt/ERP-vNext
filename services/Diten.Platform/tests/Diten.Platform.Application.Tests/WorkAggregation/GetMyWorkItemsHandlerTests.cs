using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Queries;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.WorkAggregation;

// WC-1 (DCP-004) — the read-only aggregation handler over the MOD-0023 provider. Verifies
// actionable-scope + candidate filtering (parity with GetMyWorkflowTasks), the "no state written" invariant
// (write repository methods throw), tenant-scope pass-through, provider extensibility, and the OD-WC-04
// contract-version skip. End-to-end through the REAL provider + REAL projection service.
public sealed class GetMyWorkItemsHandlerTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Me = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid Other = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    [Fact]
    public async Task Aggregates_only_actionable_tasks_where_user_is_candidate_and_projects_them()
    {
        var instanceId = Guid.NewGuid();
        var instance = Instance(instanceId);

        // Candidate via snapshot, actionable.
        var s1 = Snapshot(instanceId, resolved: Other.ToString(), candidates: [Me.ToString(), Other.ToString()]);
        var t1 = MakeTask(instanceId, ApprovalTaskStatus.WaitingApproval, snapshotId: s1.Id);
        // Direct assignee, actionable.
        var t2 = MakeTask(instanceId, ApprovalTaskStatus.WaitingEvidence, assigneeRef: Me.ToString());
        // Actionable but not my candidacy → excluded.
        var s3 = Snapshot(instanceId, resolved: Other.ToString(), candidates: [Other.ToString()]);
        var t3 = MakeTask(instanceId, ApprovalTaskStatus.WaitingApproval, snapshotId: s3.Id);
        // My candidacy but terminal → not in the actionable inbox scope.
        var s4 = Snapshot(instanceId, resolved: Me.ToString(), candidates: [Me.ToString()]);
        var t4 = MakeTask(instanceId, ApprovalTaskStatus.Approved, snapshotId: s4.Id);

        var handler = Handler(
            new FakeApprovalTaskRepository(t1, t2, t3, t4),
            new FakeSnapshotRepository(s1, s3, s4),
            new FakeInstanceRepository(instance));

        var response = await handler.Handle(Query(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var ids = response.Data!.Select(x => x.Id).ToHashSet();
        Assert.Equal(2, ids.Count);
        Assert.Contains(t1.Id.ToString(), ids);
        Assert.Contains(t2.Id.ToString(), ids);
        Assert.All(response.Data!, item => Assert.Equal(WorkItemContract.FixtureKindWorkItem, item.FixtureKind));
    }

    [Fact]
    public async Task Writes_no_state_when_projecting()
    {
        // Every write method on these fakes throws; a green run proves the read/projection path performs no write.
        var instanceId = Guid.NewGuid();
        var handler = Handler(
            new FakeApprovalTaskRepository(MakeTask(instanceId, ApprovalTaskStatus.WaitingApproval, assigneeRef: Me.ToString())),
            new FakeSnapshotRepository(),
            new FakeInstanceRepository(Instance(instanceId)));

        var response = await handler.Handle(Query(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task Only_projects_tenant_scoped_tasks_returned_by_the_repository()
    {
        // The tenant-scoped repository returns only this tenant's task; a cross-tenant task never enters the
        // result because it is never returned by GetAllForTenantAsync (no client-supplied tenant is honored).
        var instanceId = Guid.NewGuid();
        var mine = MakeTask(instanceId, ApprovalTaskStatus.WaitingApproval, assigneeRef: Me.ToString());

        var handler = Handler(
            new FakeApprovalTaskRepository(mine),
            new FakeSnapshotRepository(),
            new FakeInstanceRepository(Instance(instanceId)));

        var response = await handler.Handle(Query(), CancellationToken.None);

        var item = Assert.Single(response.Data!);
        Assert.Equal(mine.Id.ToString(), item.Id);
    }

    [Fact]
    public async Task Skips_a_provider_with_an_unsupported_contract_version()
    {
        var handler = new GetMyWorkItemsHandler(
            new IWorkItemProvider[] { new FutureVersionProvider() },
            new FakeCurrentUserContext(Me));

        var response = await handler.Handle(Query(), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Empty(response.Data!); // unmapped provider version skipped, not mis-projected
    }

    // --- helpers ---------------------------------------------------------------------------------------

    private static GetMyWorkItemsQuery Query()
        => new(IsPlatformActor: true, new HashSet<string>(), "corr");

    private static GetMyWorkItemsHandler Handler(
        IApprovalTaskRepository tasks,
        IRuntimeAssignmentSnapshotRepository snapshots,
        IWorkflowInstanceRepository instances)
    {
        var provider = new WorkflowApprovalWorkItemProvider(
            tasks, snapshots, instances, new WorkItemProjectionService(Tasks.SlaForTests.Real()));
        return new GetMyWorkItemsHandler(new IWorkItemProvider[] { provider }, new FakeCurrentUserContext(Me));
    }

    private static ApprovalTask MakeTask(Guid instanceId, ApprovalTaskStatus status, Guid? snapshotId = null, string? assigneeRef = null)
        => new()
        {
            TenantId = Tenant,
            WorkflowInstanceId = instanceId,
            StageCode = "stage-1",
            StepCode = "step-1",
            Status = status,
            AssignmentSnapshotId = snapshotId,
            AssigneeRef = assigneeRef
        };

    private static RuntimeAssignmentSnapshot Snapshot(Guid instanceId, string resolved, List<string> candidates)
        => new()
        {
            TenantId = Tenant,
            WorkflowInstanceId = instanceId,
            ResolverSource = "TEST",
            ResolvedPrincipalId = resolved,
            CandidatePrincipalIds = candidates,
            TieBreakExplanation = "n/a"
        };

    private static WorkflowInstance Instance(Guid id) => new()
    {
        Id = id,
        TenantId = Tenant,
        TemplateId = Guid.NewGuid(),
        WorkflowTemplateId = Guid.NewGuid(),
        ObjectType = "invoice",
        ObjectId = "INV-1",
        ObjectRef = "finance|invoice|INV-1"
    };

    private sealed class FutureVersionProvider : IWorkItemProvider
    {
        public string ProviderCode => "future";
        public string ProviderContractVersion => "9.9";
        public IReadOnlyCollection<string> RequiredActionPermissions => [];
        public Task<IReadOnlyList<WorkItemProjectionDto>> GetWorkItemsAsync(WorkItemActor actor, CancellationToken ct = default)
            => throw new InvalidOperationException("An unsupported-version provider must be skipped, never invoked.");
    }

    private sealed class FakeCurrentUserContext(Guid userId) : ICurrentUserContext
    {
        public Guid UserId { get; } = userId;
        public string? Email => "me@diten.local";
        public string? DisplayName => "Me";
        public string ActorName => Email!;
        public bool IsAuthenticated => true;
    }

    private sealed class FakeApprovalTaskRepository(params ApprovalTask[] tasks) : IApprovalTaskRepository
    {
        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ApprovalTask>>(tasks.ToList());

        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateEscalationAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeSnapshotRepository(params RuntimeAssignmentSnapshot[] snapshots) : IRuntimeAssignmentSnapshotRepository
    {
        public Task<RuntimeAssignmentSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(snapshots.FirstOrDefault(s => s.Id == id));

        public Task<RuntimeAssignmentSnapshot> CreateAsync(RuntimeAssignmentSnapshot snapshot, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RuntimeAssignmentSnapshot>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeInstanceRepository(params WorkflowInstance[] instances) : IWorkflowInstanceRepository
    {
        public Task<WorkflowInstance?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(instances.FirstOrDefault(i => i.Id == id));

        public Task<WorkflowInstance> CreateAsync(WorkflowInstance instance, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<WorkflowInstance?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<WorkflowInstance?> GetLatestByObjectRefAsync(string objectRef, string objectType, string objectId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<WorkflowInstance>> GetAllForTenantAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> UpdateAsync(WorkflowInstance instance, int expectedVersion, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
