using Diten.Platform.Application.Contracts;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Tests.Tasks;

// MOD-0024 — shared in-memory doubles. Repositories that Phase 1 must NOT write to throw NotSupportedException,
// so an unintended write shows up as a failing test rather than silent behaviour.

internal static class TaskTestData
{
    internal static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    internal static readonly Guid OtherTenant = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    internal static readonly Guid Me = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    internal static readonly Guid Rival = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
}

internal sealed class FakeCurrentUserContext(Guid userId) : ICurrentUserContext
{
    public Guid UserId { get; } = userId;
    public string? Email => "me@diten.local";
    public string? DisplayName => "Me";
    public string ActorName => Email!;
    public bool IsAuthenticated => true;
}

internal sealed class FakeTenantContext(Guid tenantId) : ITenantContext
{
    public Guid TenantId { get; private set; } = tenantId;
    public bool IsResolved => true;
    public bool IsPlatformContext => false;
    public Guid? TargetTenantId => null;
    public void SetTenant(Guid tenantId) => TenantId = tenantId;
    public void SetPlatformContext(Guid targetTenantId) { }
    public void ClearTenant() { }
}

/// <summary>
/// In-memory task store that reproduces the ONE behaviour the claim race depends on: the update only applies when
/// the expected version still matches (the repository's conditional write).
/// </summary>
internal sealed class FakeTaskItemRepository : ITaskItemRepository
{
    private readonly List<TaskItem> _items = [];

    public FakeTaskItemRepository(params TaskItem[] seed) => _items.AddRange(seed);

    public IReadOnlyList<TaskItem> Items => _items;

    public Task<TaskItem> CreateAsync(TaskItem task, CancellationToken ct = default)
    {
        _items.Add(task);
        return Task.FromResult(task);
    }

    // Mirrors the tenant + IsDeleted execution filter: another tenant's row is invisible, not an error.
    //
    // Returns a DETACHED copy, like the real repository: Mongo deserializes a fresh document per read, so a
    // handler that mutates the entity and then loses the expected-version write leaves the stored state
    // untouched. Handing out the stored reference instead made a REJECTED write still appear to take effect —
    // a false green that hid exactly the concurrency behaviour these tests exist to prove.
    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var stored = _items.FirstOrDefault(
            x => x.Id == id && x.TenantId == TaskTestData.Tenant && !x.IsDeleted);
        return Task.FromResult(stored is null ? null : Detach(stored));
    }

    private static TaskItem Detach(TaskItem source)
    {
        var copy = new TaskItem
        {
            TenantId = source.TenantId,
            Title = source.Title,
            AssignmentTarget = source.AssignmentTarget,
            OrganizationUnitId = source.OrganizationUnitId
        };

        foreach (var property in typeof(TaskItem)
                     .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                     .Where(p => p.CanRead && p.CanWrite))
        {
            property.SetValue(copy, property.GetValue(source));
        }

        return copy;
    }

    public Task<IReadOnlyList<TaskItem>> GetAllForTenantAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskItem>>(
            _items.Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted).ToList());

    public Task<IReadOnlyList<TaskItem>> ListByAssigneeAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskItem>>(_items
            .Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted && x.AssigneeUserId == userId)
            .ToList());

    public Task<IReadOnlyList<TaskItem>> ListByParentAsync(Guid parentTaskItemId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskItem>>(_items
            .Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted && x.ParentTaskItemId == parentTaskItemId)
            .Select(Detach)
            .ToList());

    public Task<IReadOnlyList<TaskItem>> ListByParentsAsync(
        IReadOnlyCollection<Guid> parentTaskItemIds,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskItem>>(_items
            .Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted
                        && x.ParentTaskItemId is not null && parentTaskItemIds.Contains(x.ParentTaskItemId.Value))
            .Select(Detach)
            .ToList());

    public Task<IReadOnlyList<TaskItem>> ListUnclaimedByPositionsAsync(
        IReadOnlyCollection<Guid> positionIds,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskItem>>(_items
            .Where(x => x.TenantId == TaskTestData.Tenant
                        && !x.IsDeleted
                        && x.AssignmentTarget == Domain.Enums.Tasks.TaskAssignmentTarget.PositionPool
                        && x.AssigneeUserId is null
                        && x.PoolPositionId is not null
                        && positionIds.Contains(x.PoolPositionId.Value))
            .ToList());

    public Task<bool> UpdateAsync(TaskItem task, int expectedVersion, CancellationToken ct = default)
    {
        var stored = _items.FirstOrDefault(x => x.Id == task.Id && x.TenantId == TaskTestData.Tenant);
        if (stored is null || stored.Version != expectedVersion)
        {
            // Conditional write missed → the caller must surface a controlled conflict.
            return Task.FromResult(false);
        }

        stored.Version = expectedVersion + 1;
        stored.AssigneeUserId = task.AssigneeUserId;
        stored.Lifecycle = task.Lifecycle;
        stored.Title = task.Title;
        stored.CompletedAt = task.CompletedAt;
        stored.CancelledAt = task.CancelledAt;
        return Task.FromResult(true);
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var stored = _items.FirstOrDefault(x => x.Id == id && x.TenantId == TaskTestData.Tenant);
        if (stored is not null)
        {
            stored.IsDeleted = true;
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakeTaskAssignmentRepository : ITaskAssignmentRepository
{
    private readonly List<TaskAssignment> _events = [];

    public IReadOnlyList<TaskAssignment> Events => _events;

    public Task<TaskAssignment> CreateAsync(TaskAssignment assignment, CancellationToken ct = default)
    {
        _events.Add(assignment);
        return Task.FromResult(assignment);
    }

    public Task<IReadOnlyList<TaskAssignment>> ListByTaskIdAsync(Guid taskItemId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskAssignment>>(
            _events.Where(x => x.TaskItemId == taskItemId).ToList());
}

internal sealed class FakePositionAssignmentRepository(params PositionAssignment[] seed) : IPositionAssignmentRepository
{
    // Mirrors the tenant execution filter: another tenant's row is invisible, exactly as TenantRepository<T> makes it.
    public Task<IReadOnlyList<PositionAssignment>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PositionAssignment>>(
            seed.Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted).ToList());

    public Task<PositionAssignment> CreateAsync(PositionAssignment a, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0288 assignments.");

    public Task<PositionAssignment?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(seed.FirstOrDefault(x => x.Id == id));

    public Task<bool> HasOverlapAsync(
        Guid positionId, DateTimeOffset from, DateTimeOffset? to, Guid? excludeId, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task UpdateAsync(PositionAssignment a, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0288 assignments.");

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0288 assignments.");
}

internal sealed class FakePositionRepository(params Position[] seed) : IPositionRepository
{
    public Task<Position?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(seed.FirstOrDefault(x => x.Id == id));

    // Tenant execution filter, as above.
    public Task<IReadOnlyList<Position>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Position>>(
            seed.Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted).ToList());

    public Task<Position> CreateAsync(Position p, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0288 positions.");

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task UpdateAsync(Position p, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0288 positions.");

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0288 positions.");
}

internal sealed class FakeOrganizationUnitRepository(params OrganizationUnit[] seed) : IOrganizationUnitRepository
{
    public Task<OrganizationUnit?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(seed.FirstOrDefault(x => x.Id == id));

    // Tenant execution filter, as above.
    public Task<IReadOnlyList<OrganizationUnit>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<OrganizationUnit>>(
            seed.Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted).ToList());

    public Task<OrganizationUnit> CreateAsync(OrganizationUnit u, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0288 units.");

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task UpdateAsync(OrganizationUnit u, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0288 units.");

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => throw new NotSupportedException("MOD-0024 must not write MOD-0288 units.");
}

internal sealed class FakeTaskWatcherRepository : ITaskWatcherRepository
{
    private readonly List<TaskWatcher> _watchers = [];

    public IReadOnlyList<TaskWatcher> Watchers => _watchers;

    public Task<TaskWatcher> CreateAsync(TaskWatcher watcher, CancellationToken ct = default)
    {
        _watchers.Add(watcher);
        return Task.FromResult(watcher);
    }

    public Task<IReadOnlyList<TaskWatcher>> ListByTaskIdAsync(Guid taskItemId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskWatcher>>(
            _watchers.Where(x => x.TaskItemId == taskItemId).ToList());

    public Task<IReadOnlyList<TaskWatcher>> ListByUserIdAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskWatcher>>(_watchers.Where(x => x.UserId == userId).ToList());

    public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class FakeTaskDependencyRepository : ITaskDependencyRepository
{
    public Task<TaskDependency> CreateAsync(TaskDependency d, CancellationToken ct = default)
        => Task.FromResult(d);

    public Task<IReadOnlyList<TaskDependency>> ListByTaskIdAsync(Guid taskItemId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskDependency>>([]);

    public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class FakeTaskFieldDefinitionRepository(params TaskFieldDefinition[] seed)
    : ITaskFieldDefinitionRepository
{
    public Task<TaskFieldDefinition> CreateAsync(TaskFieldDefinition d, CancellationToken ct = default)
        => Task.FromResult(d);

    public Task<TaskFieldDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(seed.FirstOrDefault(x => x.Id == id));

    public Task<TaskFieldDefinition?> GetByCodeAsync(string code, CancellationToken ct = default)
        => Task.FromResult(seed.FirstOrDefault(x => x.Code == code));

    public Task<IReadOnlyList<TaskFieldDefinition>> ListActiveAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskFieldDefinition>>(seed.Where(x => x.IsActive).ToList());
}

/// <summary>
/// Display-name resolver double. Records every call so a test can prove resolution is BATCHED (one call for a
/// page of tasks, not one per user), and can play "AuthService unreachable" by resolving nothing.
/// </summary>
internal sealed class FakeUserDisplayNameResolver(params (Guid Id, string Name)[] known)
    : Diten.Platform.Application.Contracts.IUserDisplayNameResolver
{
    private readonly Dictionary<Guid, string> _known = known.ToDictionary(k => k.Id, k => k.Name);

    /// <summary>One entry per ResolveAsync invocation, holding the ids that call asked about.</summary>
    public List<IReadOnlyCollection<Guid>> Calls { get; } = [];

    /// <summary>When true the resolver returns nothing, as it does when AuthService is down.</summary>
    public bool Unavailable { get; set; }

    public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        Calls.Add(userIds.ToList());

        IReadOnlyDictionary<Guid, string> result = Unavailable
            ? new Dictionary<Guid, string>()
            : userIds.Where(_known.ContainsKey).ToDictionary(id => id, id => _known[id]);

        return Task.FromResult(result);
    }
}

/// <summary>Checklist runs, with the tenant filter and the conditional write the real repository performs.</summary>
internal sealed class FakeChecklistRunRepository(params ChecklistRun[] seed) : IChecklistRunRepository
{
    private readonly List<ChecklistRun> _runs = [.. seed];

    public IReadOnlyList<ChecklistRun> Runs => _runs;

    public Task<ChecklistRun> CreateAsync(ChecklistRun run, CancellationToken ct = default)
    {
        _runs.Add(run);
        return Task.FromResult(run);
    }

    public Task<ChecklistRun?> GetByTaskIdAsync(Guid taskItemId, CancellationToken ct = default)
        => Task.FromResult(_runs.FirstOrDefault(
            x => x.TaskItemId == taskItemId && x.TenantId == TaskTestData.Tenant && !x.IsDeleted));

    public Task<IReadOnlyList<ChecklistRun>> ListByTaskIdsAsync(
        IReadOnlyCollection<Guid> taskItemIds,
        CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult<IReadOnlyList<ChecklistRun>>(_runs
            .Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted && taskItemIds.Contains(x.TaskItemId))
            .ToList());
    }

    /// <summary>Batch reads issued — the N+1 assertion reads this.</summary>
    public int CallCount { get; private set; }

    public Task<bool> UpdateAsync(ChecklistRun run, int expectedVersion, CancellationToken ct = default)
    {
        var stored = _runs.FirstOrDefault(x => x.Id == run.Id && x.TenantId == TaskTestData.Tenant);
        if (stored is null || stored.Version != expectedVersion)
        {
            return Task.FromResult(false);
        }

        stored.Version = expectedVersion + 1;
        stored.Items = run.Items;
        stored.Status = run.Status;
        return Task.FromResult(true);
    }
}

internal sealed class FakeChecklistTemplateRepository(params ChecklistTemplate[] seed) : IChecklistTemplateRepository
{
    public Task<ChecklistTemplate> CreateAsync(ChecklistTemplate template, CancellationToken ct = default)
        => Task.FromResult(template);

    public Task<ChecklistTemplate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(seed.FirstOrDefault(x => x.Id == id && x.TenantId == TaskTestData.Tenant));

    public Task<IReadOnlyList<ChecklistTemplate>> ListActiveAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ChecklistTemplate>>(
            seed.Where(x => x.TenantId == TaskTestData.Tenant && x.IsActive).ToList());
}

/// <summary>
/// Workflow gate double. Defaults to ALLOW so Phase 1/2 tests are unaffected; a Phase 3 test flips it to blocked
/// or makes it throw, which must be treated as blocked (fail-closed).
/// </summary>
internal sealed class FakeWorkflowTransitionGate : Diten.Platform.Application.Contracts.IWorkflowTransitionGate
{
    public bool Blocked { get; set; }
    public bool Throws { get; set; }
    public List<Diten.Platform.Application.Contracts.WorkflowGateRequest> Calls { get; } = [];

    public Task<Diten.Platform.Application.Contracts.WorkflowGateResult> EvaluateAsync(
        Diten.Platform.Application.Contracts.WorkflowGateRequest request, CancellationToken ct = default)
    {
        Calls.Add(request);

        // The REAL gate is fail-closed: an evaluation that cannot complete is a block, never an allow. The double
        // mirrors that rather than propagating the exception, which is what the production seam does.
        if (Throws)
        {
            return Task.FromResult(new Diten.Platform.Application.Contracts.WorkflowGateResult(
                IsAllowed: false, Decision: "blocked", GateStatus: "evaluationFailed",
                BlockingReasonCode: "GATE_EVALUATION_FAILED",
                BlockingMessage: "The workflow gate could not be evaluated.", CorrelationId: null));
        }

        return Task.FromResult(new Diten.Platform.Application.Contracts.WorkflowGateResult(
            IsAllowed: !Blocked,
            Decision: Blocked ? "blocked" : "allowed",
            GateStatus: Blocked ? "pendingApproval" : "noWorkflow",
            BlockingReasonCode: Blocked ? "APPROVAL_PENDING" : null,
            BlockingMessage: Blocked ? "Approval is still pending." : null,
            CorrelationId: null));
    }

    public async Task EnsureAllowedOrThrowAsync(
        Diten.Platform.Application.Contracts.WorkflowGateRequest request, CancellationToken ct = default)
    {
        var result = await EvaluateAsync(request, ct);
        if (result.IsBlocked)
        {
            throw new Diten.Platform.Application.Contracts.WorkflowTransitionBlockedException(result);
        }
    }
}

/// <summary>Approval-service double: records starts/cancels and can simulate a workflow that will not start.</summary>
internal sealed class FakeTaskApprovalService : Diten.Platform.Application.Features.Tasks.Services.ITaskApprovalService
{
    public bool CannotStart { get; set; }
    public List<Guid> Started { get; } = [];
    public List<Guid> Cancelled { get; } = [];
    public Guid InstanceId { get; } = Guid.Parse("abcdabcd-abcd-abcd-abcd-abcdabcdabcd");

    public Task<Guid?> TryStartApprovalAsync(TaskItem task, CancellationToken ct)
    {
        Started.Add(task.Id);
        return Task.FromResult<Guid?>(CannotStart ? null : InstanceId);
    }

    public Task CancelApprovalAsync(TaskItem task, CancellationToken ct)
    {
        Cancelled.Add(task.Id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<Guid, Diten.Platform.Application.Features.Tasks.Services.TaskApprovalState>>
        GetStatesAsync(IReadOnlyCollection<Guid> workflowInstanceIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<Guid, Diten.Platform.Application.Features.Tasks.Services.TaskApprovalState>>(
            new Dictionary<Guid, Diten.Platform.Application.Features.Tasks.Services.TaskApprovalState>());
}
