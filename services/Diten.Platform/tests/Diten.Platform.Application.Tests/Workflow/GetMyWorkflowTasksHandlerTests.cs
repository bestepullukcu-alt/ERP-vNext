using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

// MOD-0023 — "my tasks" returns the actionable tasks where the caller is a candidate (snapshot) or the
// resolved assignee; it excludes tasks the user is not a candidate for and terminal tasks.
public sealed class GetMyWorkflowTasksHandlerTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Me = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid Other = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

    [Fact]
    public async Task Returns_only_actionable_tasks_where_user_is_candidate_or_assignee()
    {
        var instanceId = Guid.NewGuid();

        // T1: candidate via snapshot
        var s1 = Snapshot(instanceId, resolved: Other.ToString(), candidates: [Me.ToString(), Other.ToString()]);
        var t1 = MakeTask(instanceId, ApprovalTaskStatus.WaitingApproval, snapshotId: s1.Id);

        // T2: direct assignee match, no snapshot
        var t2 = MakeTask(instanceId, ApprovalTaskStatus.WaitingEvidence, assigneeRef: Me.ToString());

        // T3: actionable but user is NOT a candidate
        var s3 = Snapshot(instanceId, resolved: Other.ToString(), candidates: [Other.ToString()]);
        var t3 = MakeTask(instanceId, ApprovalTaskStatus.WaitingApproval, snapshotId: s3.Id);

        // T4: user IS candidate but task is terminal (Approved) → excluded
        var s4 = Snapshot(instanceId, resolved: Me.ToString(), candidates: [Me.ToString()]);
        var t4 = MakeTask(instanceId, ApprovalTaskStatus.Approved, snapshotId: s4.Id);

        var handler = new GetMyWorkflowTasksHandler(
            new FakeApprovalTaskRepository(t1, t2, t3, t4),
            new FakeSnapshotRepository(s1, s3, s4),
            new FakeCurrentUserContext(Me));

        var response = await handler.Handle(new GetMyWorkflowTasksQuery("corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        var ids = response.Data!.Select(x => x.Id).ToHashSet();
        Assert.Equal(2, ids.Count);
        Assert.Contains(t1.Id, ids);
        Assert.Contains(t2.Id, ids);
        Assert.DoesNotContain(t3.Id, ids);
        Assert.DoesNotContain(t4.Id, ids);
    }

    [Fact]
    public async Task Returns_empty_when_user_is_candidate_for_nothing()
    {
        var instanceId = Guid.NewGuid();
        var s1 = Snapshot(instanceId, resolved: Other.ToString(), candidates: [Other.ToString()]);
        var t1 = MakeTask(instanceId, ApprovalTaskStatus.WaitingApproval, snapshotId: s1.Id);

        var handler = new GetMyWorkflowTasksHandler(
            new FakeApprovalTaskRepository(t1),
            new FakeSnapshotRepository(s1),
            new FakeCurrentUserContext(Me));

        var response = await handler.Handle(new GetMyWorkflowTasksQuery("corr"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Empty(response.Data!);
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
        public Task<IReadOnlyList<ApprovalTask>> ListOverdueTasksAsync(DateTimeOffset nowUtc, int maxItems, CancellationToken ct = default) => throw new NotSupportedException();
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
}
