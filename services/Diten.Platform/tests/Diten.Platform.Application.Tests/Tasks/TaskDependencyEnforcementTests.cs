using Diten.Platform.API.Controllers;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// The dependency rule ENFORCED, driven through the endpoint the browser posts to.
///
/// <para><b>The defect these exist for.</b> The projection was correct — `blockedState.blocked`, an affected
/// `start`, the button disabled with DEPENDENCY_BLOCKED beside it — and <c>POST /api/v1/tasks/{id}/start</c>
/// answered 204 anyway. The task started with an open predecessor. Twenty-two tests were green because every one
/// of them asserted the PROJECTION; not one posted the transition. A disabled button is presentation, the refusal
/// is the rule, and only the second one is a rule.</para>
///
/// <para><b>Why through the controller.</b> Asserting on the command handler would leave the same gap one level
/// down: <c>start</c> and <c>complete</c> are separate endpoints that each pick their own target lifecycle, and
/// the mapping from URL to target is part of what has to hold. These call the real
/// <see cref="TasksController"/> action and assert the <see cref="IActionResult"/> it returns, which
/// <c>CreateActionResultInstance</c> turns verbatim into the HTTP response.</para>
/// </summary>
public sealed class TaskDependencyEnforcementTests
{
    // ── The four edge types, each refusing the act it names ───────────────────

    [Theory]
    [InlineData(TaskDependencyType.FinishToStart)]
    [InlineData(TaskDependencyType.StartToStart)]
    public async Task Start_is_refused_while_a_start_gating_predecessor_is_unmet(TaskDependencyType type)
    {
        // Not started, so neither "must finish" nor "must have begun" is satisfied.
        var fixture = new Fixture(TaskLifecycle.Open, TaskLifecycle.Open, type);

        var result = await fixture.PostStartAsync();

        AssertRefused(result);
        // The task did NOT move: the gate is consulted before the commit, like the approval gate above it.
        Assert.Equal(TaskLifecycle.Open, fixture.Task.Lifecycle);
    }

    [Theory]
    [InlineData(TaskDependencyType.FinishToFinish)]
    [InlineData(TaskDependencyType.StartToFinish)]
    public async Task Complete_is_refused_while_a_completion_gating_predecessor_is_unmet(TaskDependencyType type)
    {
        var fixture = new Fixture(TaskLifecycle.InProgress, TaskLifecycle.Open, type);

        var result = await fixture.PostCompleteAsync();

        AssertRefused(result);
        Assert.Equal(TaskLifecycle.InProgress, fixture.Task.Lifecycle);
    }

    [Theory]
    // The other half of the rule: an edge gates ONE act, and must not quietly gate the other. A FinishToFinish
    // edge on an Open task says nothing about starting it.
    [InlineData(TaskDependencyType.FinishToFinish)]
    [InlineData(TaskDependencyType.StartToFinish)]
    public async Task A_completion_gating_edge_does_not_stop_the_task_from_starting(TaskDependencyType type)
    {
        var fixture = new Fixture(TaskLifecycle.Open, TaskLifecycle.Open, type);

        var result = await fixture.PostStartAsync();

        AssertAccepted(result);
        Assert.Equal(TaskLifecycle.InProgress, fixture.Task.Lifecycle);
    }

    [Theory]
    [InlineData(TaskDependencyType.FinishToStart)]
    [InlineData(TaskDependencyType.StartToStart)]
    public async Task A_start_gating_edge_does_not_stop_the_task_from_completing(TaskDependencyType type)
    {
        var fixture = new Fixture(TaskLifecycle.InProgress, TaskLifecycle.Open, type);

        var result = await fixture.PostCompleteAsync();

        AssertAccepted(result);
        Assert.Equal(TaskLifecycle.Done, fixture.Task.Lifecycle);
    }

    // ── When the rule lets go ────────────────────────────────────────────────

    [Fact]
    public async Task A_cancelled_predecessor_does_not_refuse_anything()
    {
        // Called-off work will never finish and never start. Refusing on it would park the dependent task with
        // nobody able to clear it — the same rule the projection applies, now on the write path too.
        var fixture = new Fixture(TaskLifecycle.Open, TaskLifecycle.Cancelled, TaskDependencyType.FinishToStart);

        var result = await fixture.PostStartAsync();

        AssertAccepted(result);
        Assert.Equal(TaskLifecycle.InProgress, fixture.Task.Lifecycle);
    }

    [Fact]
    public async Task Once_the_predecessor_closes_the_transition_goes_through()
    {
        // The rule is a CONDITION, not a permanent lock: the refusal has to stop being a refusal.
        var fixture = new Fixture(TaskLifecycle.Open, TaskLifecycle.Open, TaskDependencyType.FinishToStart);
        AssertRefused(await fixture.PostStartAsync());

        fixture.Predecessor.Lifecycle = TaskLifecycle.Done;

        AssertAccepted(await fixture.PostStartAsync());
        Assert.Equal(TaskLifecycle.InProgress, fixture.Task.Lifecycle);
    }

    [Fact]
    public async Task A_started_predecessor_releases_a_start_to_start_edge_but_not_a_finish_to_start_one()
    {
        var startToStart = new Fixture(TaskLifecycle.Open, TaskLifecycle.InProgress, TaskDependencyType.StartToStart);
        AssertAccepted(await startToStart.PostStartAsync());

        var finishToStart = new Fixture(TaskLifecycle.Open, TaskLifecycle.InProgress, TaskDependencyType.FinishToStart);
        AssertRefused(await finishToStart.PostStartAsync());
    }

    [Fact]
    public async Task A_task_with_no_dependencies_is_untouched_by_the_gate()
    {
        // Non-vacuity for every "refused" assertion above: the fixture must be able to start at all.
        var fixture = new Fixture(TaskLifecycle.Open, TaskLifecycle.Open, type: null);

        AssertAccepted(await fixture.PostStartAsync());
        Assert.Equal(TaskLifecycle.InProgress, fixture.Task.Lifecycle);
    }

    [Fact]
    public async Task A_predecessor_that_cannot_be_read_blocks_nothing()
    {
        // Mirrors the projection, which drops an edge whose far end it cannot resolve rather than showing an
        // unnamed blocker. Refusing here would park the task on a predecessor nobody can see or close.
        var fixture = new Fixture(TaskLifecycle.Open, TaskLifecycle.Open, TaskDependencyType.FinishToStart);
        fixture.PointTheEdgeAtAnUnreadableTask();

        AssertAccepted(await fixture.PostStartAsync());
    }

    // ── The code the client has to understand ────────────────────────────────

    [Fact]
    public void The_refusal_and_the_disabled_button_carry_the_same_reason_code()
    {
        // One fact from two sides. Two different strings would need two entries in the client's message map, and
        // the one nobody remembered to add would render as a raw code.
        Assert.Equal(WorkAggregationReasonCodes.DependencyBlocked, TaskReasonCodes.DependencyBlocked);
    }

    [Fact]
    public void The_reason_code_is_translatable_by_the_frontend_bridge()
    {
        // The bridge is a hand-maintained map; a code the handler emits and the map does not know reaches the
        // user as the raw string. Same guard the 409 SPLIT codes carry.
        var api = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "frontend/Diten.Web/wwwroot/assets/js/Tasks/api.js"));

        Assert.Contains(TaskReasonCodes.DependencyBlocked, api, StringComparison.Ordinal);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static void AssertRefused(IActionResult result)
    {
        // The status is read off the framework result the action actually returned, not off the Response<T>: the
        // wire carries whatever ASP.NET made of it, and that is what the browser sees.
        var status = Assert.IsAssignableFrom<IStatusCodeActionResult>(result);
        // 409, not 403: a state conflict the caller can clear by closing the predecessor, not a refusal of
        // authority. The distinction tells the client whether retrying could ever help.
        Assert.Equal(StatusCodes.Status409Conflict, status.StatusCode);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        var response = Assert.IsType<Response<NoContent>>(objectResult.Value);
        Assert.False(response.IsSuccessful);
        Assert.Equal(TaskReasonCodes.DependencyBlocked, response.ReasonCode);
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
    /// One dependent task, one predecessor, and the real controller in front of the real handler. The mediator is
    /// a thin router rather than a mock, so the command the CONTROLLER builds is the one the handler receives —
    /// that mapping (start → InProgress, complete → Done) is part of what these tests are for.
    /// </summary>
    private sealed class Fixture
    {
        private readonly FakeTaskItemRepository _tasks;
        private readonly FakeTaskDependencyRepository _edges;
        private readonly TasksController _controller;

        public Fixture(TaskLifecycle dependentState, TaskLifecycle predecessorState, TaskDependencyType? type)
        {
            Task = NewTask(dependentState);
            Predecessor = NewTask(predecessorState);
            _tasks = new FakeTaskItemRepository(Task, Predecessor);
            _edges = type is null
                ? new FakeTaskDependencyRepository()
                : new FakeTaskDependencyRepository(new TaskDependency
                {
                    TenantId = TaskTestData.Tenant,
                    TaskItemId = Task.Id,
                    DependsOnTaskItemId = Predecessor.Id,
                    DependencyType = type.Value
                });

            var handler = new TransitionTaskItemHandler(
                _tasks,
                new TaskLifecycleService(),
                new FakeCurrentUserContext(TaskTestData.Me),
                new FakeChecklistRunRepository(),
                new TaskChecklistService(),
                new PassingWorkflowGate(),
                _edges);

            var correlation = new CorrelationContext();
            correlation.SetCorrelationId("corr");
            _controller = new TasksController(new DirectMediator(handler), correlation)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public TaskItem Task { get; }

        public TaskItem Predecessor { get; }

        /// <summary>Repoints the edge at an id no repository will return, without touching anything else.</summary>
        public void PointTheEdgeAtAnUnreadableTask()
            => _edges.Edges.Single().DependsOnTaskItemId = Guid.NewGuid();

        public Task<IActionResult> PostStartAsync()
            => _controller.Start(Task.Id, Request(), CancellationToken.None);

        public Task<IActionResult> PostCompleteAsync()
            => _controller.Complete(Task.Id, Request(), CancellationToken.None);

        // The expected version is read back from the stored task so a passing transition is never refused for
        // concurrency instead — that would make an "accepted" assertion fail for the wrong reason.
        private TaskTransitionRequest Request() => new(_tasks.Items.First(t => t.Id == Task.Id).Version, null, null);

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
