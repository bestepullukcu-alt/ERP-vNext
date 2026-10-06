using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// BL-484 (CT review L2, L6) — the batched reads measured against the REAL stores on the disposable mongod, where the
/// in-memory doubles cannot speak for them:
/// <list type="bullet">
/// <item>L2 — "the newest open approval task" is one task even when two share their creation instant: the single and the
/// batched read break the tie the same way.</item>
/// <item>L6 — the batched task read rule sees exactly what the single one sees: a removed watcher, a removed parent and
/// another tenant's watcher row grant nothing, through the repositories' own tenant + soft-delete filter.</item>
/// </list>
/// Fresh tenant ids per test; the shared development database is never touched.
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TimesheetReadBatchStoreMongoTests
{
    private readonly TimeEntryMongoFixture _fixture;
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _otherTenant = Guid.NewGuid();
    private readonly Guid _me = Guid.NewGuid();
    private readonly Guid _other = Guid.NewGuid();

    public TimesheetReadBatchStoreMongoTests(TimeEntryMongoFixture fixture) => _fixture = fixture;

    // ── L2 — a tie on CreatedAt is broken by id, the same way in both reads ──────────────────────────────────────

    [Fact]
    public async Task Two_open_tasks_created_in_the_same_instant_are_named_the_same_by_the_single_and_the_batched_read()
    {
        var instance = Guid.NewGuid();
        var otherInstance = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);
        var tail = Guid.NewGuid().ToString("D")[2..];
        var low = Guid.Parse("00" + tail);                    // stored first: the natural order would name it
        var high = Guid.Parse("ff" + tail);                   // the larger id: the tie-breaker names it
        await _fixture.Database.GetCollection<ApprovalTask>(PlatformCollections.ApprovalTasks).InsertManyAsync(
        [
            OpenTask(low, instance, at),
            OpenTask(high, instance, at),
            OpenTask(Guid.NewGuid(), otherInstance, at.AddMinutes(-5))
        ]);
        var store = new ApprovalTaskRepository(_fixture.DbContext, new FakeTenantContext(_tenant));

        var single = await store.GetActiveByInstanceIdAsync(instance);
        var batched = (await store.ListActiveByInstanceIdsAsync([otherInstance, instance]))
            .First(t => t.WorkflowInstanceId == instance);

        Assert.Equal(high, single!.Id);
        Assert.Equal(high, batched.Id);
    }

    // ── L6 — removed and foreign rows grant nothing, in the batch as in the single read ──────────────────────────

    [Fact]
    public async Task A_removed_watcher_a_removed_parent_and_another_tenants_watcher_grant_nothing_in_the_batch_either()
    {
        var watched = NewTask();                              // control: a live watcher row — readable
        var removedWatcher = NewTask();
        var removedParent = NewTask(assignee: _me);
        removedParent.IsDeleted = true;
        var orphan = NewTask(parent: removedParent.Id);
        var foreignWatcher = NewTask();
        await _fixture.Database.GetCollection<TaskItem>(PlatformCollections.TaskItems).InsertManyAsync(
            [watched, removedWatcher, removedParent, orphan, foreignWatcher]);
        await _fixture.Database.GetCollection<TaskWatcher>(PlatformCollections.TaskWatchers).InsertManyAsync(
        [
            new TaskWatcher { TenantId = _tenant, TaskItemId = watched.Id, UserId = _me },
            new TaskWatcher { TenantId = _tenant, TaskItemId = removedWatcher.Id, UserId = _me, IsDeleted = true },
            new TaskWatcher { TenantId = _otherTenant, TaskItemId = foreignWatcher.Id, UserId = _me }
        ]);

        var tenant = new FakeTenantContext(_tenant);
        var taskStore = new TaskItemRepository(_fixture.DbContext, tenant, new FakeTaskTransitionRepository());
        var policy = new TaskReadAccessPolicy(
            taskStore,
            new TaskWatcherRepository(_fixture.DbContext, tenant),
            new FakeTaskNotificationService(),
            new FakeOrganizationUnitRepository(),
            new NoScope(),
            new FakeTaskTeamResolver(),
            TaskActors.None(),
            new FakeCurrentUserContext(_me));
        var tasks = await taskStore.ListByIdsAsync([watched.Id, removedWatcher.Id, orphan.Id, foreignWatcher.Id]);
        Assert.Equal(4, tasks.Count);

        var batched = await policy.ReadableTaskIdsAsync(tasks, _me, CancellationToken.None);
        var oneByOne = new HashSet<Guid>();
        foreach (var task in tasks)
        {
            if (await policy.CanReadAsync(task, _me, CancellationToken.None))
            {
                oneByOne.Add(task.Id);
            }
        }

        Assert.Equal([watched.Id], oneByOne.ToList());
        Assert.Equal([watched.Id], batched.ToList());
    }

    private ApprovalTask OpenTask(Guid id, Guid instance, DateTimeOffset createdAt) => new()
    {
        Id = id,
        TenantId = _tenant,
        WorkflowInstanceId = instance,
        Status = ApprovalTaskStatus.WaitingApproval,
        CreatedAt = createdAt,
        UpdatedAt = createdAt,
        CreatedBy = "test"
    };

    private TaskItem NewTask(Guid? assignee = null, Guid? parent = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = _tenant,
        Title = "Store probe",
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = assignee ?? _other,
        CreatedByUserId = _other,
        ParentTaskItemId = parent,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.InProgress,
        Version = 1
    };

    private sealed class NoScope : ITaskAssignmentScopeResolver
    {
        public Task<TaskAssignmentScope> ResolveAsync(CancellationToken ct) => Task.FromResult(TaskAssignmentScope.Empty);
    }
}
