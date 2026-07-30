using System.Text.RegularExpressions;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-028 — "this cannot start until that finishes", enforced rather than merely modelled.
///
/// <para>The shape existed for a long time: typed edges, a repository, a detail query that read them, even an
/// error code. What did not exist was any way to CREATE an edge and any consequence of having one, so the rule was
/// documentation. These tests pin both halves: the write refuses the graphs that cannot be honoured (cycles), and
/// the projection turns a live edge into a DISABLED action with a reason a person can read.</para>
/// </summary>
public sealed class TaskDependencyTests
{
    // ── The write side: which graphs are refused ─────────────────────────────

    [Fact]
    public async Task A_task_cannot_depend_on_itself()
    {
        var task = Task_(TaskLifecycle.Open);
        var (handler, _, _) = Harness(task);

        var result = await handler.Handle(Add(task.Id, task.Id), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskReasonCodes.DependencySelf, result.ReasonCode);
    }

    [Fact]
    public async Task A_two_task_cycle_is_refused()
    {
        var a = Task_(TaskLifecycle.Open);
        var b = Task_(TaskLifecycle.Open);
        var (handler, _, edges) = Harness(a, b);

        // A waits on B — fine.
        var first = await handler.Handle(Add(a.Id, b.Id), CancellationToken.None);
        Assert.True(first.IsSuccessful);

        // B waits on A — now neither could ever start.
        var second = await handler.Handle(Add(b.Id, a.Id), CancellationToken.None);

        Assert.False(second.IsSuccessful);
        Assert.Equal(TaskReasonCodes.DependencyCycle, second.ReasonCode);
        // Refused means NOT WRITTEN: a rejected command that still stored the edge would leave the deadlock behind.
        Assert.Single(edges.Edges);
    }

    [Fact]
    public async Task A_longer_cycle_is_refused_too()
    {
        var a = Task_(TaskLifecycle.Open);
        var b = Task_(TaskLifecycle.Open);
        var c = Task_(TaskLifecycle.Open);
        var (handler, _, edges) = Harness(a, b, c);

        Assert.True((await handler.Handle(Add(a.Id, b.Id), CancellationToken.None)).IsSuccessful);
        Assert.True((await handler.Handle(Add(b.Id, c.Id), CancellationToken.None)).IsSuccessful);

        // C → A closes A → B → C → A. A check that only looked one hop back would miss this.
        var result = await handler.Handle(Add(c.Id, a.Id), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskReasonCodes.DependencyCycle, result.ReasonCode);
        Assert.Equal(2, edges.Edges.Count);
    }

    [Fact]
    public async Task A_diamond_is_not_mistaken_for_a_cycle()
    {
        // A → B, A → C, and now B → D and C → D. D is reachable twice, which is not a loop, and a walk that
        // followed edges in both directions would report one.
        var a = Task_(TaskLifecycle.Open);
        var b = Task_(TaskLifecycle.Open);
        var c = Task_(TaskLifecycle.Open);
        var d = Task_(TaskLifecycle.Open);
        var (handler, _, _) = Harness(a, b, c, d);

        Assert.True((await handler.Handle(Add(a.Id, b.Id), CancellationToken.None)).IsSuccessful);
        Assert.True((await handler.Handle(Add(a.Id, c.Id), CancellationToken.None)).IsSuccessful);
        Assert.True((await handler.Handle(Add(b.Id, d.Id), CancellationToken.None)).IsSuccessful);

        var result = await handler.Handle(Add(c.Id, d.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful);
    }

    [Fact]
    public async Task The_same_edge_cannot_be_added_twice()
    {
        var a = Task_(TaskLifecycle.Open);
        var b = Task_(TaskLifecycle.Open);
        var (handler, _, edges) = Harness(a, b);

        Assert.True((await handler.Handle(Add(a.Id, b.Id), CancellationToken.None)).IsSuccessful);
        var again = await handler.Handle(Add(a.Id, b.Id), CancellationToken.None);

        Assert.False(again.IsSuccessful);
        Assert.Equal(TaskReasonCodes.DependencyDuplicate, again.ReasonCode);
        Assert.Single(edges.Edges);
    }

    [Fact]
    public async Task An_edge_to_a_task_that_cannot_be_read_is_refused()
    {
        // The repository read is tenant-filtered, so another tenant's task is simply absent — and the caller is
        // told NOT FOUND rather than "forbidden", which would confirm the id exists somewhere.
        var task = Task_(TaskLifecycle.Open);
        var (handler, _, _) = Harness(task);

        var result = await handler.Handle(Add(task.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskReasonCodes.DependencyTaskNotFound, result.ReasonCode);
    }

    [Fact]
    public async Task An_edge_belonging_to_another_task_cannot_be_removed_through_this_one()
    {
        var a = Task_(TaskLifecycle.Open);
        var b = Task_(TaskLifecycle.Open);
        var c = Task_(TaskLifecycle.Open);
        var (handler, _, edges) = Harness(a, b, c);
        await handler.Handle(Add(a.Id, b.Id), CancellationToken.None);
        var edge = Assert.Single(edges.Edges);

        var remover = new RemoveTaskDependencyHandler(edges);
        var refused = await remover.Handle(
            new RemoveTaskDependencyCommand(c.Id, edge.Id, "corr"), CancellationToken.None);

        Assert.False(refused.IsSuccessful);
        Assert.Equal(TaskReasonCodes.NotFound, refused.ReasonCode);
        Assert.Single(edges.Edges);

        var removed = await remover.Handle(
            new RemoveTaskDependencyCommand(a.Id, edge.Id, "corr"), CancellationToken.None);

        Assert.True(removed.IsSuccessful);
        Assert.Empty(edges.Edges);
    }

    // ── The read side: which ACT each edge type stops ────────────────────────

    [Theory]
    // The second half of the name is THIS task's end of the edge, so it is the act that gets blocked.
    [InlineData(TaskDependencyType.FinishToStart, "start")]
    [InlineData(TaskDependencyType.StartToStart, "start")]
    [InlineData(TaskDependencyType.FinishToFinish, "complete")]
    [InlineData(TaskDependencyType.StartToFinish, "complete")]
    public async Task Each_edge_type_blocks_the_act_it_names(TaskDependencyType type, string expectedCode)
    {
        // The dependent task is put in whichever state OFFERS the affected action, so the block is what removes
        // it rather than the lifecycle never having offered it in the first place.
        var dependent = Task_(expectedCode == "start" ? TaskLifecycle.Open : TaskLifecycle.InProgress);
        // A predecessor that satisfies NOTHING: not started, so neither a Finish* nor a Start* edge is met.
        var predecessor = Task_(TaskLifecycle.Open);
        var projection = await Project(dependent, predecessor, type);

        Assert.NotNull(projection.BlockedState);
        Assert.True(projection.BlockedState!.Blocked);
        Assert.Equal([expectedCode], projection.BlockedState.AffectedActionCodes);

        var blocked = Assert.Single(projection.Actions, a => a.Code == expectedCode);
        // DISABLED, never hidden: a button that vanishes tells the reader nothing about why work will not move.
        Assert.False(blocked.Enabled);
        Assert.Equal(WorkAggregationReasonCodes.DependencyBlocked, blocked.DisabledReasonCode);
        Assert.NotNull(blocked.DisabledReason);
    }

    [Theory]
    [InlineData(TaskDependencyType.FinishToStart, TaskLifecycle.Done)]
    [InlineData(TaskDependencyType.StartToStart, TaskLifecycle.InProgress)]
    public async Task A_satisfied_predecessor_blocks_nothing(TaskDependencyType type, TaskLifecycle state)
    {
        var projection = await Project(Task_(TaskLifecycle.Open), Task_(state), type);

        Assert.Null(projection.BlockedState);
        Assert.Contains(projection.Actions, a => a.Code == "start" && a.Enabled);
    }

    [Fact]
    public async Task A_started_predecessor_does_not_satisfy_a_finish_to_start_edge()
    {
        // The distinction that makes the four types worth having: FS waits for the END, SS only for the BEGINNING.
        var projection = await Project(
            Task_(TaskLifecycle.Open), Task_(TaskLifecycle.InProgress), TaskDependencyType.FinishToStart);

        Assert.NotNull(projection.BlockedState);
        Assert.Contains(projection.Actions, a => a.Code == "start" && !a.Enabled);
    }

    [Fact]
    public async Task A_cancelled_predecessor_blocks_nothing()
    {
        // Called-off work will never finish and never start. Treating it as unmet would park the dependent task
        // forever with nobody able to clear it — the same rule BL-035 applies to a cancelled subtask.
        var projection = await Project(
            Task_(TaskLifecycle.Open), Task_(TaskLifecycle.Cancelled), TaskDependencyType.FinishToStart);

        Assert.Null(projection.BlockedState);
        Assert.Contains(projection.Actions, a => a.Code == "start" && a.Enabled);
        // It still SHOWS as a dependency — the edge exists — it is simply not blocking.
        var edge = Assert.Single(projection.Dependencies!);
        Assert.Equal("cancelled", edge.State);
        Assert.False(edge.Blocking);
    }

    [Fact]
    public async Task A_blocker_whose_action_is_not_on_offer_is_dropped()
    {
        // A FinishToFinish edge constrains COMPLETION, and an Open task is not being offered `complete`. Keeping
        // the blocker would also break the contract, which requires every affected code to name a visible action.
        var projection = await Project(
            Task_(TaskLifecycle.Open), Task_(TaskLifecycle.Open), TaskDependencyType.FinishToFinish);

        Assert.Null(projection.BlockedState);
        Assert.Contains(projection.Actions, a => a.Code == "start" && a.Enabled);
    }

    [Fact]
    public async Task Every_affected_action_code_names_a_disabled_action_with_a_reason()
    {
        // The executable contract's BLOCKER_ACTION_REFERENCE_INVALID rule, asserted on the C# side so the
        // projection cannot emit a blockedState the browser would reject.
        var projection = await Project(
            Task_(TaskLifecycle.Open), Task_(TaskLifecycle.Open), TaskDependencyType.FinishToStart);

        Assert.NotNull(projection.BlockedState);
        Assert.NotEmpty(projection.BlockedState!.AffectedActionCodes);

        foreach (var code in projection.BlockedState.AffectedActionCodes)
        {
            var action = Assert.Single(projection.Actions, a => a.Code == code);
            Assert.False(action.Enabled);
            Assert.False(string.IsNullOrWhiteSpace(action.DisabledReasonCode));
            Assert.NotNull(action.DisabledReason);
        }

        // Each blocker names the task in the way, the edge type and the act it stops, so the client can build a
        // typed sentence with no localized text on the wire.
        var blocker = Assert.Single(projection.BlockedState.Blockers);
        Assert.Equal(WorkAggregationReasonCodes.DependencyBlocked, blocker.Code);
        Assert.Equal("FinishToStart", blocker.DependencyType);
        Assert.Equal("start", blocker.AffectedActionCode);
        Assert.Equal(WorkItemContract.LabelDisplay, blocker.Label.Kind);
        Assert.False(string.IsNullOrWhiteSpace(blocker.Label.Text));
        Assert.Contains(blocker.AffectedActionCode!, projection.BlockedState.AffectedActionCodes);
    }

    [Fact]
    public async Task Both_directions_are_projected_and_only_predecessors_block()
    {
        var dependent = Task_(TaskLifecycle.Open);
        var predecessor = Task_(TaskLifecycle.Open);
        var successor = Task_(TaskLifecycle.Open);
        var tasks = new FakeTaskItemRepository(dependent, predecessor, successor);
        var edges = new FakeTaskDependencyRepository(
            Edge(dependent.Id, predecessor.Id, TaskDependencyType.FinishToStart),
            Edge(successor.Id, dependent.Id, TaskDependencyType.FinishToStart));

        var projection = Assert.Single(
            (await Provider(tasks, edges).GetWorkItemsAsync(Actor(), CancellationToken.None))
                .Where(item => item.Id == dependent.Id.ToString()));

        Assert.Equal(2, projection.Dependencies!.Count);
        Assert.Equal(
            ["pred", "succ"],
            projection.Dependencies!.Select(d => d.Direction).OrderBy(d => d, StringComparer.Ordinal).ToList());

        // Only the predecessor edge holds this task up; being SOMEONE ELSE's predecessor blocks nothing here.
        var blocker = Assert.Single(projection.BlockedState!.Blockers);
        Assert.Equal(predecessor.Id.ToString(), blocker.TaskItemId);
    }

    [Fact]
    public async Task An_edge_whose_far_end_cannot_be_read_is_not_projected_at_all()
    {
        // A row that can only say "a task" asserts a dependency without letting anyone check it.
        var dependent = Task_(TaskLifecycle.Open);
        var tasks = new FakeTaskItemRepository(dependent);
        var edges = new FakeTaskDependencyRepository(
            Edge(dependent.Id, Guid.NewGuid(), TaskDependencyType.FinishToStart));

        var projection = Assert.Single(await Provider(tasks, edges).GetWorkItemsAsync(Actor(), CancellationToken.None));

        Assert.Null(projection.Dependencies);
        Assert.Null(projection.BlockedState);
    }

    // ── The contract mirror ──────────────────────────────────────────────────

    [Fact]
    public void The_engine_and_the_executable_contract_declare_the_same_dependency_types()
    {
        // Same technique as the action-code reachability guard: the browser's contract is the authority, the two
        // sides share no assembly, so the file is read as text. Fixtures said "FS" while the engine said
        // "FinishToStart" for the whole of BL-028's design phase.
        var contract = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/fixture-contract.js"));

        var declared = Regex.Match(contract, @"DEPENDENCY_TYPES\s*=\s*\[(?<values>[^\]]+)\]");
        Assert.True(declared.Success, "fixture-contract.js no longer declares DEPENDENCY_TYPES.");

        var fromContract = Regex.Matches(declared.Groups["values"].Value, @"'(?<value>[^']+)'")
            .Select(m => m.Groups["value"].Value)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();
        var fromEngine = Enum.GetNames<TaskDependencyType>().OrderBy(v => v, StringComparer.Ordinal).ToList();

        Assert.NotEmpty(fromContract);
        Assert.Equal(fromEngine, fromContract);
    }

    [Fact]
    public void The_engine_and_the_executable_contract_declare_the_same_priorities()
    {
        // BL-032: the owner's decision was "three levels, the engine's spelling". This is what keeps it true.
        var contract = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/fixture-contract.js"));

        var declared = Regex.Match(contract, @"PRIORITIES\s*=\s*\[(?<values>[^\]]+)\]");
        Assert.True(declared.Success, "fixture-contract.js no longer declares PRIORITIES.");

        var fromContract = Regex.Matches(declared.Groups["values"].Value, @"'(?<value>[^']+)'")
            .Select(m => m.Groups["value"].Value)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            Enum.GetNames<TaskPriority>().OrderBy(v => v, StringComparer.Ordinal).ToList(),
            fromContract);
    }

    [Fact]
    public async Task The_projection_carries_the_engines_priority_spelling()
    {
        var task = Task_(TaskLifecycle.Open);
        task.Priority = TaskPriority.High;
        var tasks = new FakeTaskItemRepository(task);

        var projection = Assert.Single(
            await Provider(tasks, new FakeTaskDependencyRepository()).GetWorkItemsAsync(Actor(), CancellationToken.None));

        // Not "high": the contract, the engine and both write surfaces all say High, and the lowercase spelling
        // is what left the priority column hidden.
        Assert.Equal("High", projection.Priority);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static AddTaskDependencyCommand Add(Guid taskId, Guid dependsOn)
        => new(taskId, new AddTaskDependencyRequest(dependsOn, TaskDependencyType.FinishToStart), "corr");

    private static (AddTaskDependencyHandler Handler, FakeTaskItemRepository Tasks, FakeTaskDependencyRepository Edges)
        Harness(params TaskItem[] tasks)
    {
        var repository = new FakeTaskItemRepository(tasks);
        var edges = new FakeTaskDependencyRepository();
        return (new AddTaskDependencyHandler(repository, edges, new FakeTenantContext(TaskTestData.Tenant)), repository, edges);
    }

    /// <summary>Projects <paramref name="dependent"/> with one edge onto <paramref name="predecessor"/>.</summary>
    private static async Task<WorkItemProjectionDto> Project(
        TaskItem dependent,
        TaskItem predecessor,
        TaskDependencyType type)
    {
        var tasks = new FakeTaskItemRepository(dependent, predecessor);
        var edges = new FakeTaskDependencyRepository(Edge(dependent.Id, predecessor.Id, type));

        var items = await Provider(tasks, edges).GetWorkItemsAsync(Actor(), CancellationToken.None);
        return Assert.Single(items.Where(item => item.Id == dependent.Id.ToString()));
    }

    private static TaskDependency Edge(Guid taskId, Guid dependsOn, TaskDependencyType type) => new()
    {
        TenantId = TaskTestData.Tenant,
        TaskItemId = taskId,
        DependsOnTaskItemId = dependsOn,
        DependencyType = type
    };

    private static TaskWorkItemProvider Provider(FakeTaskItemRepository tasks, FakeTaskDependencyRepository edges)
        => new(
            tasks,
            new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(),
            new FakeTaskApprovalService(),
            edges, new FakeTaskCommentRepository(), new FakePositionRepository(), new FakeOrganizationUnitRepository(), SlaForTests.Real(), new FakeTaskFieldDefinitionRepository());

    private static WorkItemActor Actor()
        => new(TaskTestData.Me, IsPlatformActor: false, new HashSet<string>(
            new[]
            {
                TaskPermissions.Update, TaskPermissions.Claim, TaskPermissions.Complete,
                TaskPermissions.Cancel, TaskPermissions.Assign
            },
            StringComparer.OrdinalIgnoreCase));

    private static TaskItem Task_(TaskLifecycle lifecycle) => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = $"Task in {lifecycle}",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        CreatedByUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = lifecycle,
        Version = 1
    };

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                && Directory.Exists(Path.Combine(directory.FullName, "frontend")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root walking up from {AppContext.BaseDirectory}.");
    }
}
