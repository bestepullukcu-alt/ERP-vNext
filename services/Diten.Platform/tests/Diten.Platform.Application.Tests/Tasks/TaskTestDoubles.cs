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
    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_items.FirstOrDefault(
            x => x.Id == id && x.TenantId == TaskTestData.Tenant && !x.IsDeleted));

    public Task<IReadOnlyList<TaskItem>> GetAllForTenantAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskItem>>(
            _items.Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted).ToList());

    public Task<IReadOnlyList<TaskItem>> ListByAssigneeAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TaskItem>>(_items
            .Where(x => x.TenantId == TaskTestData.Tenant && !x.IsDeleted && x.AssigneeUserId == userId)
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
    public Task<IReadOnlyList<PositionAssignment>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PositionAssignment>>(seed.ToList());

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

    public Task<IReadOnlyList<Position>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Position>>(seed.ToList());

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

    public Task<IReadOnlyList<OrganizationUnit>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<OrganizationUnit>>(seed.ToList());

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
