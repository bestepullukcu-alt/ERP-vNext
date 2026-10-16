using Diten.MdmService.Application.Features.Approvals;
using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Tests.Approvals;

// WP-VW-W4 (W4-BE-b1) — TEST-ONLY doubles. The approvable record below exists only to prove the core: there is no
// production aggregate in b1 (W4-BE-b2 brings the competitor proposal decision).

/// <summary>A test-only approvable record (<c>mdm.test-approvable</c>).</summary>
public sealed class TestApprovable : EntityBase, IWorkflowApprovable
{
    public string Status { get; set; } = TestApprovableStatuses.Draft;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public List<WorkflowApprovalRound> ReviewRounds { get; set; } = [];
    public List<WorkflowApprovalDecision> Decisions { get; set; } = [];

    public TestApprovable Clone() => new()
    {
        Id = Id, TenantId = TenantId, Version = Version, Status = Status, CreatedBy = CreatedBy, UpdatedBy = UpdatedBy,
        ApprovedBy = ApprovedBy, ApprovedAt = ApprovedAt, UpdatedAt = UpdatedAt, CreatedAt = CreatedAt, IsDeleted = IsDeleted,
        ReviewRounds = ReviewRounds.Select(r => new WorkflowApprovalRound
        {
            WorkflowInstanceId = r.WorkflowInstanceId, SubmittedAt = r.SubmittedAt, SubmittedBy = r.SubmittedBy, Outcome = r.Outcome,
            ClosedAt = r.ClosedAt, RoundNo = r.RoundNo, TemplateCode = r.TemplateCode, CompletedBy = r.CompletedBy, ReasonCode = r.ReasonCode
        }).ToList(),
        Decisions = Decisions.Select(d => new WorkflowApprovalDecision { RoundNo = d.RoundNo, By = d.By, Outcome = d.Outcome, Comment = d.Comment, At = d.At }).ToList()
    };
}

public static class TestApprovableStatuses
{
    public const string Draft = "draft";
    public const string InReview = "in_review";
    public const string Applied = "applied";
}

public static class TestApprovableKind
{
    public static readonly WorkflowApprovalKind Kind = new("mdm.test-approvable", "MDM_TEST_APPROVABLE", id => $"/MDM/Test/{id:D}");
    public const string SodCode = "test_approvable_sod";

    public static WorkflowApprovalTarget<TestApprovable> Target(IWorkflowApprovalStore<TestApprovable> store,
        Func<TestApprovable, string, CancellationToken, Task<string?>>? extra = null)
        => new(Kind, store, TestApprovableStatuses.Draft, SodCode, (r, by, at) =>
        {
            r.Status = TestApprovableStatuses.Applied;
            r.ApprovedBy = by;
            r.ApprovedAt = at;
        }, extra);
}

/// <summary>An in-memory explicit-tenant CAS store (same contract as MongoWorkflowApprovalStore).</summary>
public sealed class InMemoryApprovalStore : IWorkflowApprovalStore<TestApprovable>
{
    private readonly Dictionary<(Guid Tenant, Guid Id), TestApprovable> _rows = new();

    public int Replaces { get; private set; }
    public bool AlwaysConflict { get; set; }

    public void Seed(TestApprovable record) => _rows[(record.TenantId, record.Id)] = record.Clone();

    public TestApprovable? Peek(Guid tenantId, Guid id) => _rows.TryGetValue((tenantId, id), out var r) ? r.Clone() : null;

    public Task<TestApprovable?> GetAsync(Guid tenantId, Guid id, CancellationToken ct)
        => Task.FromResult(_rows.TryGetValue((tenantId, id), out var r) && !r.IsDeleted ? r.Clone() : null);

    public Task<bool> ReplaceAsync(TestApprovable record, int expectedVersion, CancellationToken ct)
    {
        if (AlwaysConflict || !_rows.TryGetValue((record.TenantId, record.Id), out var stored) || stored.Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        record.Version = expectedVersion + 1;
        _rows[(record.TenantId, record.Id)] = record.Clone();
        Replaces++;
        return Task.FromResult(true);
    }
}

public sealed class FakeWorkflowClient : IMdmWorkflowClient
{
    public List<WorkflowStartRequest> Starts { get; } = [];
    public WorkflowCallOutcome StartOutcome { get; set; } = WorkflowCallOutcome.Ok;
    public Guid NextInstanceId { get; set; } = Guid.NewGuid();
    public List<(Guid TaskId, string Actor, string Reason, string Key)> Cancels { get; } = [];
    public List<(Guid TaskId, bool Approve, string Actor, string Key, string? Comment)> Decisions { get; } = [];
    public Dictionary<string, IReadOnlyList<WorkflowInstanceState>> Instances { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<WorkflowTaskState> Tasks { get; } = [];
    public List<WorkflowTaskState> MyTasks { get; } = [];
    public int ByObjectsCalls { get; private set; }

    public Task<WorkflowStartResult> StartAsync(WorkflowStartRequest request, CancellationToken ct)
    {
        Starts.Add(request);
        return Task.FromResult(StartOutcome == WorkflowCallOutcome.Ok
            ? new WorkflowStartResult(WorkflowCallOutcome.Ok, NextInstanceId, null)
            : new WorkflowStartResult(StartOutcome, null, "x"));
    }

    public Task<IReadOnlyDictionary<string, IReadOnlyList<WorkflowInstanceState>>?> GetInstancesByObjectsAsync(
        string objectType, IReadOnlyList<string> objectIds, CancellationToken ct)
    {
        ByObjectsCalls++;
        return Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<WorkflowInstanceState>>?>(Instances);
    }

    public Task<IReadOnlyList<WorkflowTaskState>?> GetTasksAsync(IReadOnlyCollection<Guid> workflowInstanceIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<WorkflowTaskState>?>(Tasks.Where(t => workflowInstanceIds.Contains(t.WorkflowInstanceId)).ToList());

    public Task<WorkflowCallOutcome> CancelTaskAsync(Guid taskId, string actorId, string reasonCode, string idempotencyKey, CancellationToken ct)
    {
        Cancels.Add((taskId, actorId, reasonCode, idempotencyKey));
        return Task.FromResult(WorkflowCallOutcome.Ok);
    }

    public Task<IReadOnlyList<WorkflowTaskState>?> GetMyTasksAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<WorkflowTaskState>?>(MyTasks);

    public Task<WorkflowDecisionResult> DecideTaskAsync(Guid taskId, bool approve, string actorId, string reasonCode,
        string idempotencyKey, string? comment, CancellationToken ct)
    {
        Decisions.Add((taskId, approve, actorId, idempotencyKey, comment));
        return Task.FromResult(new WorkflowDecisionResult(WorkflowCallOutcome.Ok, null));
    }

    public Task<IReadOnlyList<WorkflowHistoryEntry>?> GetInstanceHistoryAsync(Guid workflowInstanceId, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<WorkflowHistoryEntry>?>([]);

    public static WorkflowTaskState OpenTask(Guid instanceId, string status = "WaitingApproval", string? actionedBy = null,
        DateTimeOffset? completedAt = null)
        => new(Guid.NewGuid(), instanceId, "S1", "APPROVE", status, null, actionedBy, null, null, completedAt);
}

public sealed class FixedActor(string? actor) : IMdmActorContext
{
    public string? ActorName { get; set; } = actor;
}

public sealed class InMemoryInbox : Domain.Repositories.IMdmEventInboxRepository
{
    private readonly HashSet<Guid> _seen = [];

    public Task<bool> TryInsertAsync(Guid eventId, string eventName, Guid tenantId, CancellationToken ct = default)
        => Task.FromResult(_seen.Add(eventId));
}

public sealed class ManualClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}
