using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-WCN-TASK-REQUEST-01 FIX4 — the PRODUCTION read rule (<see cref="TaskReadAccessPolicy"/>) and the PRODUCTION write
/// gate (<see cref="TaskWriteGate"/>):
/// <list type="bullet">
/// <item>item 3 — a batch reads the seats ONCE for every pool task in it (it read them once per pool task);</item>
/// <item>item 4 (G4) — the parent legs are FULL legs: the parent's assignee, and a holder of the parent's pool, may
/// CHANGE a subtask through the real gate — one task at a time and in a batch.</item>
/// </list>
/// </summary>
public sealed class TaskReadRuleFix4Tests
{
    // ── item 3 — the seats are read once per batch ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Fifty_pool_tasks_read_the_seat_assignments_once_and_still_admit_their_holders()
    {
        var assignments = new CountingAssignments();
        var tasks = new FakeTaskItemRepository();
        var pooled = new List<TaskItem>();
        for (var i = 0; i < 50; i++)
        {
            var position = Guid.NewGuid();
            // Me holds every tenth seat: those ten tasks are readable through the pool leg, the other forty are not.
            assignments.Add(position, i % 10 == 0 ? TaskTestData.Me : TaskTestData.Other);
            var task = NewTask(holder: null, creator: TaskTestData.Rival);
            task.AssignmentTarget = TaskAssignmentTarget.PositionPool;
            task.PoolPositionId = position;
            pooled.Add(task);
            await tasks.CreateAsync(task);
        }

        var policy = Policy(tasks, assignments, TaskTestData.Me);
        var readable = await policy.ReadableTaskIdsAsync(pooled, TaskTestData.Me, CancellationToken.None);

        Assert.Equal(1, assignments.Reads);
        Assert.Equal(pooled.Where((_, i) => i % 10 == 0).Select(t => t.Id).ToHashSet(), readable);
    }

    // ── item 4 (G4) — the parent legs are Full: change the subtask through the real gate ───────────────────────

    [Fact]
    public async Task The_parents_assignee_may_change_a_subtask_through_the_real_gate()
    {
        var parent = NewTask(holder: TaskTestData.Me, creator: TaskTestData.Rival);
        var subtask = NewTask(holder: TaskTestData.Other, creator: TaskTestData.Other, parent: parent.Id);
        var tasks = new FakeTaskItemRepository(parent, subtask);
        var gate = TaskWriteGateDoubles.Real(
            Policy(tasks, new CountingAssignments(), TaskTestData.Me), new FakeCurrentUserContext(TaskTestData.Me));

        Assert.Null(await gate.CheckAsync(subtask, TaskWriteKind.Change, CancellationToken.None));
        Assert.Contains(subtask.Id,
            await gate.AllowedTaskIdsAsync([subtask], TaskTestData.Me, TaskWriteKind.Change, CancellationToken.None));
    }

    [Fact]
    public async Task A_holder_of_the_parents_pool_may_change_a_subtask_through_the_real_gate()
    {
        var position = Guid.NewGuid();
        var assignments = new CountingAssignments();
        assignments.Add(position, TaskTestData.Me);
        var parent = NewTask(holder: null, creator: TaskTestData.Rival);
        parent.AssignmentTarget = TaskAssignmentTarget.PositionPool;
        parent.PoolPositionId = position;
        var subtask = NewTask(holder: TaskTestData.Other, creator: TaskTestData.Other, parent: parent.Id);
        var tasks = new FakeTaskItemRepository(parent, subtask);
        var gate = TaskWriteGateDoubles.Real(
            Policy(tasks, assignments, TaskTestData.Me), new FakeCurrentUserContext(TaskTestData.Me));

        Assert.Null(await gate.CheckAsync(subtask, TaskWriteKind.Change, CancellationToken.None));
        Assert.Contains(subtask.Id,
            await gate.AllowedTaskIdsAsync([subtask], TaskTestData.Me, TaskWriteKind.Change, CancellationToken.None));
    }

    [Fact]
    public async Task Somebody_on_neither_leg_does_not_reach_the_subtask()
    {
        // Non-vacuity for the two tests above: the same subtask, a parent that names somebody else.
        var parent = NewTask(holder: TaskTestData.Rival, creator: TaskTestData.Rival);
        var subtask = NewTask(holder: TaskTestData.Other, creator: TaskTestData.Other, parent: parent.Id);
        var tasks = new FakeTaskItemRepository(parent, subtask);
        var gate = TaskWriteGateDoubles.Real(
            Policy(tasks, new CountingAssignments(), TaskTestData.Me), new FakeCurrentUserContext(TaskTestData.Me));

        Assert.NotNull(await gate.CheckAsync(subtask, TaskWriteKind.Change, CancellationToken.None));
    }

    // ── harness ────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The production read rule over the PRODUCTION pool resolution (TaskNotificationService → TaskSeatDirectory
    /// → the assignments below); only the data legs admit (no scope, no team, no read-all).</summary>
    private static TaskReadAccessPolicy Policy(ITaskItemRepository tasks, CountingAssignments assignments, Guid caller)
        => new(
            tasks,
            new FakeTaskWatcherRepository(),
            new TaskNotificationService(null!, null!, null!, new TaskSeatDirectory(assignments), null!,
                new FakeTenantContext(TaskTestData.Tenant), NullLogger<TaskNotificationService>.Instance),
            new FakeOrganizationUnitRepository(),
            new NoScope(),
            new FakeTaskTeamResolver(),
            TaskActors.None(),
            new FakeCurrentUserContext(caller));

    private static TaskItem NewTask(Guid? holder, Guid creator, Guid? parent = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TaskTestData.Tenant,
        Title = "Fix4",
        AssignmentTarget = holder is null ? TaskAssignmentTarget.SelfAssigned : TaskAssignmentTarget.Person,
        AssigneeUserId = holder,
        CreatedByUserId = creator,
        ParentTaskItemId = parent,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.Open,
        CreatedBy = "fix4"
    };

    private sealed class NoScope : ITaskAssignmentScopeResolver
    {
        public Task<TaskAssignmentScope> ResolveAsync(CancellationToken ct) => Task.FromResult(TaskAssignmentScope.Empty);
    }

    /// <summary>Seat assignments, counting every read of the whole table.</summary>
    private sealed class CountingAssignments : IPositionAssignmentRepository
    {
        private readonly List<PositionAssignment> _rows = [];

        public int Reads { get; private set; }

        public void Add(Guid position, Guid user) => _rows.Add(new PositionAssignment
        {
            TenantId = TaskTestData.Tenant,
            PositionId = position,
            UserId = user,
            EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
        });

        public Task<IReadOnlyList<PositionAssignment>> GetAllAsync(CancellationToken ct = default)
        {
            Reads++;
            return Task.FromResult<IReadOnlyList<PositionAssignment>>(_rows.ToList());
        }

        public Task<PositionAssignment> CreateAsync(PositionAssignment assignment, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<PositionAssignment?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> HasOverlapAsync(Guid positionId, DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo, Guid? excludeId = null, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(PositionAssignment assignment, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
