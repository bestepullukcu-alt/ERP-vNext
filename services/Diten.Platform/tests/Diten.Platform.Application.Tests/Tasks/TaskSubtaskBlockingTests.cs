using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-035 — an open subtask stops its parent being completed, ENFORCED.
///
/// <para><b>The decision this reverses.</b> Open subtasks used to be reported and not enforced, on the reasoning
/// that two competing blocking mechanisms would make "why can't I finish this?" unanswerable. The owner reversed
/// it: "the work was split into three, two were never done, and the whole thing is complete" is not a sentence
/// this engine should be able to produce. The old objection is answered by something that did not exist when it
/// was written — blockedState.blockers[] names each blocker individually.</para>
///
/// <para><b>Why these post through the controller.</b> BL-028 shipped with twenty-two green tests and no rule,
/// because every one of them asserted the projection and none posted the transition. So the enforcement cases
/// here call the real <see cref="TasksController"/> action and read the status off the
/// <see cref="IActionResult"/> it returns.</para>
/// </summary>
public sealed class TaskSubtaskBlockingTests
{
    // ── Enforcement, over the endpoint ───────────────────────────────────────

    [Fact]
    public async Task Complete_is_refused_while_a_subtask_is_open()
    {
        var fixture = new Fixture(TaskLifecycle.Open);

        var result = await fixture.PostCompleteAsync();

        AssertRefused(result);
        // The parent did NOT move: the gate runs before the commit.
        Assert.Equal(TaskLifecycle.InProgress, fixture.Parent.Lifecycle);
    }

    [Fact]
    public async Task A_cancelled_subtask_blocks_nothing()
    {
        // Work that turned out to be unnecessary must not hold its parent forever — the same rule that releases a
        // cancelled predecessor on a dependency edge.
        var fixture = new Fixture(TaskLifecycle.Cancelled);

        AssertAccepted(await fixture.PostCompleteAsync());
        Assert.Equal(TaskLifecycle.Done, fixture.Parent.Lifecycle);
    }

    [Fact]
    public async Task A_parent_whose_subtasks_are_all_done_completes()
    {
        var fixture = new Fixture(TaskLifecycle.Done);

        AssertAccepted(await fixture.PostCompleteAsync());
        Assert.Equal(TaskLifecycle.Done, fixture.Parent.Lifecycle);
    }

    [Fact]
    public async Task One_done_and_one_open_still_refuses()
    {
        // The rule is about ANY open child, not about progress being made.
        var fixture = new Fixture(TaskLifecycle.Done, TaskLifecycle.InProgress);

        AssertRefused(await fixture.PostCompleteAsync());
    }

    [Fact]
    public async Task An_open_subtask_does_not_stop_the_parent_from_starting()
    {
        // The other half of every blocking rule: it stops ONE act. Work can begin with its parts still open.
        var fixture = Fixture.WithParent(TaskLifecycle.Open, TaskLifecycle.Open);

        AssertAccepted(await fixture.PostStartAsync());
        Assert.Equal(TaskLifecycle.InProgress, fixture.Parent.Lifecycle);
    }

    [Fact]
    public async Task An_open_subtask_does_not_stop_the_parent_from_being_cancelled()
    {
        // Cancelling a parent is HOW its open children get closed (CancelOpenSubtasksAsync). Blocking it would
        // make an unwanted task with open children impossible to call off — a trap, not a rule.
        var fixture = new Fixture(TaskLifecycle.Open);

        AssertAccepted(await fixture.PostCancelAsync());
        Assert.Equal(TaskLifecycle.Cancelled, fixture.Parent.Lifecycle);
    }

    [Fact]
    public async Task A_parent_with_no_subtasks_completes()
    {
        // Non-vacuity for every refusal above: this fixture must be able to complete at all.
        var fixture = new Fixture();

        AssertAccepted(await fixture.PostCompleteAsync());
        Assert.Equal(TaskLifecycle.Done, fixture.Parent.Lifecycle);
    }

    [Fact]
    public async Task Once_the_last_subtask_closes_the_parent_completes()
    {
        // A condition, not a permanent lock.
        var fixture = new Fixture(TaskLifecycle.Open);
        AssertRefused(await fixture.PostCompleteAsync());

        fixture.Children.Single().Lifecycle = TaskLifecycle.Done;

        AssertAccepted(await fixture.PostCompleteAsync());
    }

    // ── What the screen is told ──────────────────────────────────────────────

    [Fact]
    public async Task The_projection_names_every_open_subtask_as_its_own_blocker()
    {
        var fixture = new Fixture(TaskLifecycle.Open, TaskLifecycle.InProgress, TaskLifecycle.Done);

        var parent = await fixture.ProjectParentAsync();

        Assert.NotNull(parent.BlockedState);
        Assert.True(parent.BlockedState!.Blocked);
        Assert.Equal(["complete"], parent.BlockedState.AffectedActionCodes);

        // One blocker per open child — never one summarising them, which would lose WHICH children.
        var blockers = parent.BlockedState.Blockers;
        Assert.Equal(2, blockers.Count);
        Assert.All(blockers, blocker =>
        {
            Assert.Equal(WorkAggregationReasonCodes.SubtaskBlocked, blocker.Code);
            // Not an edge, so it carries no dependency type — the DTO left the field optional for this.
            Assert.Null(blocker.DependencyType);
            Assert.Equal("complete", blocker.AffectedActionCode);
            Assert.Equal(WorkItemContract.LabelDisplay, blocker.Label.Kind);
            Assert.False(string.IsNullOrWhiteSpace(blocker.Label.Text));
        });

        // The done child is not among them, and each blocker points at a real child.
        var openIds = fixture.Children
            .Where(child => child.Lifecycle != TaskLifecycle.Done)
            .Select(child => child.Id.ToString())
            .ToHashSet();
        Assert.Equal(openIds, blockers.Select(b => b.TaskItemId!).ToHashSet());
    }

    [Fact]
    public async Task The_complete_button_is_visible_and_disabled_rather_than_hidden()
    {
        var fixture = new Fixture(TaskLifecycle.Open);

        var parent = await fixture.ProjectParentAsync();

        var complete = Assert.Single(parent.Actions, action => action.Code == "complete");
        Assert.False(complete.Enabled);
        Assert.Equal(WorkAggregationReasonCodes.SubtaskBlocked, complete.DisabledReasonCode);
        Assert.NotNull(complete.DisabledReason);
    }

    [Fact]
    public async Task A_dependency_blocker_is_reported_before_a_subtask_one()
    {
        /*
         * Order is contractual, not cosmetic: the button's reason is taken from the FIRST blocker on that action,
         * and the handler checks dependencies before subtasks. If the projection listed them the other way, the
         * screen would blame an open subtask while the 409 blamed a predecessor.
         */
        var fixture = new Fixture(TaskLifecycle.Open);
        fixture.AddCompletionBlockingDependency();

        var parent = await fixture.ProjectParentAsync();

        Assert.Equal(
            WorkAggregationReasonCodes.DependencyBlocked,
            parent.BlockedState!.Blockers.First().Code);
        Assert.Equal(
            WorkAggregationReasonCodes.DependencyBlocked,
            Assert.Single(parent.Actions, a => a.Code == "complete").DisabledReasonCode);

        // ...and the server agrees, which is the whole reason the order matters.
        var refusal = Assert.IsAssignableFrom<ObjectResult>(await fixture.PostCompleteAsync());
        Assert.Equal(
            TaskReasonCodes.DependencyBlocked,
            Assert.IsType<Response<NoContent>>(refusal.Value).ReasonCode);
    }

    [Fact]
    public async Task A_subtask_is_never_itself_subject_to_the_rule()
    {
        // One level only (CreateTaskItemHandler refuses a subtask of a subtask), so a child's own child query is
        // always empty and completing a subtask is never gated by this.
        var fixture = new Fixture(TaskLifecycle.Open);

        AssertAccepted(await fixture.PostCompleteChildAsync());
        Assert.Equal(TaskLifecycle.Done, fixture.Children.Single().Lifecycle);
    }

    // ── The code the client has to understand ────────────────────────────────

    [Fact]
    public void The_refusal_and_the_disabled_button_carry_the_same_reason_code()
        => Assert.Equal(WorkAggregationReasonCodes.SubtaskBlocked, TaskReasonCodes.SubtaskBlocked);

    [Fact]
    public void The_reason_code_is_translatable_by_the_frontend_bridge()
    {
        // A code the handler emits and the client's map does not know reaches the user as a raw string.
        var api = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "frontend/Diten.Web/wwwroot/assets/js/Tasks/api.js"));

        Assert.Contains(TaskReasonCodes.SubtaskBlocked, api, StringComparison.Ordinal);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static void AssertRefused(IActionResult result)
    {
        var status = Assert.IsAssignableFrom<IStatusCodeActionResult>(result);
        // 409, not 403: closing the subtask clears it, so this is a state conflict rather than a refusal of
        // authority — and that distinction tells the client whether retrying could ever help.
        Assert.Equal(StatusCodes.Status409Conflict, status.StatusCode);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        var response = Assert.IsType<Response<NoContent>>(objectResult.Value);
        Assert.False(response.IsSuccessful);
        Assert.Equal(TaskReasonCodes.SubtaskBlocked, response.ReasonCode);
    }

    private static void AssertAccepted(IActionResult result)
    {
        var status = Assert.IsAssignableFrom<IStatusCodeActionResult>(result);
        Assert.Equal(StatusCodes.Status204NoContent, status.StatusCode);
    }

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

    /// <summary>
    /// A parent with the given children, behind the real controller and the real handler. The projection is built
    /// by the real provider off the same repositories, so what the screen would show and what the endpoint
    /// answers are read from ONE stored state.
    /// </summary>
    private sealed class Fixture
    {
        private readonly FakeTaskItemRepository _tasks;
        private readonly FakeTaskDependencyRepository _edges = new();
        private readonly TasksController _controller;

        public Fixture(params TaskLifecycle[] childStates)
            : this(TaskLifecycle.InProgress, childStates, explicitParentState: false)
        {
        }

        /// <summary>Names the parent's own state, which the params constructor cannot express unambiguously.</summary>
        public static Fixture WithParent(TaskLifecycle parentState, params TaskLifecycle[] childStates)
            => new(parentState, childStates, explicitParentState: true);

        private Fixture(TaskLifecycle parentState, TaskLifecycle[] childStates, bool explicitParentState)
        {
            _ = explicitParentState;
            Parent = NewTask(parentState);
            Children = childStates.Select(state =>
            {
                var child = NewTask(state);
                child.ParentTaskItemId = Parent.Id;
                return child;
            }).ToList();

            _tasks = new FakeTaskItemRepository([Parent, .. Children]);

            var handler = new TransitionTaskItemHandler(
                _tasks,
                new TaskLifecycleService(),
                new FakeCurrentUserContext(TaskTestData.Me),
                new FakeChecklistRunRepository(),
                new TaskChecklistService(),
                new PassingWorkflowGate(),
                _edges, new FakeTaskNotificationService(), NullLogger<TransitionTaskItemHandler>.Instance);

            var correlation = new CorrelationContext();
            correlation.SetCorrelationId("corr");
            _controller = new TasksController(new DirectMediator(handler), correlation)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public TaskItem Parent { get; }

        public IReadOnlyList<TaskItem> Children { get; }

        /// <summary>Adds an unmet FinishToFinish edge, which gates completion just as an open subtask does.</summary>
        public void AddCompletionBlockingDependency()
        {
            var predecessor = NewTask(TaskLifecycle.Open);
            _tasks.CreateAsync(predecessor, CancellationToken.None).GetAwaiter().GetResult();
            _edges.CreateAsync(
                new TaskDependency
                {
                    TenantId = TaskTestData.Tenant,
                    TaskItemId = Parent.Id,
                    DependsOnTaskItemId = predecessor.Id,
                    DependencyType = TaskDependencyType.FinishToFinish
                },
                CancellationToken.None).GetAwaiter().GetResult();
        }

        public Task<IActionResult> PostCompleteAsync()
            => _controller.Complete(Parent.Id, Request(Parent.Id), CancellationToken.None);

        public Task<IActionResult> PostStartAsync()
            => _controller.Start(Parent.Id, Request(Parent.Id), CancellationToken.None);

        public Task<IActionResult> PostCancelAsync()
            => _controller.Cancel(Parent.Id, Request(Parent.Id), CancellationToken.None);

        public Task<IActionResult> PostCompleteChildAsync()
        {
            var child = Children.Single();
            child.Lifecycle = TaskLifecycle.InProgress;
            return _controller.Complete(child.Id, Request(child.Id), CancellationToken.None);
        }

        public async Task<WorkItemProjectionDto> ProjectParentAsync()
        {
            var provider = new TaskWorkItemProvider(
                _tasks,
                new FakePositionAssignmentRepository(),
                new TaskLifecycleService(),
                new TaskAssignmentResolver(),
                new FakeUserDisplayNameResolver(),
                new FakeChecklistRunRepository(),
                new FakeTaskApprovalService(),
                _edges, new FakeTaskCommentRepository(), new FakeTaskTransitionRepository(), new FakeTaskPersonalOverlayRepository(), new FakeTaskWatcherRepository(), TaskActors.PermitAll(), new FakePositionRepository(), new FakeOrganizationUnitRepository(), SlaForTests.Real(), new FakeTaskFieldDefinitionRepository());

            var items = await provider.GetWorkItemsAsync(Actor(), CancellationToken.None);
            return Assert.Single(items.Where(item => item.Id == Parent.Id.ToString()));
        }

        private TaskTransitionRequest Request(Guid id)
            => new(_tasks.Items.First(task => task.Id == id).Version, null, null);

        private static WorkItemActor Actor()
            => new(TaskTestData.Me, IsPlatformActor: false, new HashSet<string>(
                new[]
                {
                    TaskPermissions.Update, TaskPermissions.Claim, TaskPermissions.Complete,
                    TaskPermissions.Cancel, TaskPermissions.Assign
                },
                StringComparer.OrdinalIgnoreCase));

        private static TaskItem NewTask(TaskLifecycle lifecycle) => new()
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
    }
}
