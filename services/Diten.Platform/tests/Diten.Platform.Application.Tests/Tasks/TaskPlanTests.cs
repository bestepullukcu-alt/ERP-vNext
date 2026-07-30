using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// A personal plan date, actually stored.
///
/// <para><b>What existed before this.</b> <c>POST .../plan</c> moved the lifecycle Open → Planned and accepted no
/// date at all. <c>PlannedDate</c> was writable only through create or a full-replace update, so the frontend's
/// date picker was deliberately never shown to a real user — asking for a date the endpoint would discard would
/// have been a new lie. This closes that gap: the date travels with the transition, re-planning is a real
/// operation, and <c>DueAt</c> — a different fact, owned by the source — is never touched.</para>
///
/// <para>Write cases post through the real <see cref="TasksController"/> action. This module has repeatedly
/// shipped a rule that lived only in a fake and answered success when posted to directly (dependencies,
/// subtasks); the fixture here is what would have caught that.</para>
/// </summary>
public sealed class TaskPlanTests
{
    // ── The round trip ───────────────────────────────────────────────────────

    [Fact]
    public async Task Planning_an_open_task_stores_the_date_and_moves_the_lifecycle()
    {
        var fixture = new Fixture(TaskLifecycle.Open);
        var date = new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);

        var result = await fixture.PostPlanAsync(date);

        AssertAccepted(result);
        Assert.Equal(TaskLifecycle.Planned, fixture.Task.Lifecycle);
        Assert.Equal(date, fixture.Task.PlannedDate);
    }

    [Fact]
    public async Task Replanning_a_planned_task_moves_the_date_and_keeps_the_lifecycle()
    {
        var fixture = new Fixture(TaskLifecycle.Planned);
        fixture.Task.PlannedDate = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var movedDate = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero);

        var result = await fixture.PostPlanAsync(movedDate);

        AssertAccepted(result);
        Assert.Equal(TaskLifecycle.Planned, fixture.Task.Lifecycle);
        Assert.Equal(movedDate, fixture.Task.PlannedDate);
    }

    [Theory]
    [InlineData(TaskLifecycle.InProgress)]
    [InlineData(TaskLifecycle.Waiting)]
    [InlineData(TaskLifecycle.PendingReview)]
    [InlineData(TaskLifecycle.Done)]
    [InlineData(TaskLifecycle.Cancelled)]
    public async Task Planning_is_refused_once_work_has_moved_past_open_or_planned(TaskLifecycle lifecycle)
    {
        // Planning is a BEFORE-THE-WORK act. Once it has started, moved elsewhere, or closed, "plan" is not a
        // sentence that means anything — the same shape as every other lifecycle rule in this matrix.
        var fixture = new Fixture(lifecycle);

        var result = await fixture.PostPlanAsync(new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero));

        var status = Assert.IsAssignableFrom<IStatusCodeActionResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, status.StatusCode);
        Assert.Equal(lifecycle, fixture.Task.Lifecycle);
        Assert.Null(fixture.Task.PlannedDate);
    }

    // ── The one thing that IS refused ────────────────────────────────────────

    [Fact]
    public async Task No_date_is_refused()
    {
        // The JSON-omitted-field case: PlannedDate deserializes to its zero value rather than throwing, so the
        // handler has to catch it explicitly or a missing date would silently "succeed" as year 1.
        var fixture = new Fixture(TaskLifecycle.Open);

        var result = await fixture.PostPlanAsync(default);

        var status = Assert.IsAssignableFrom<IStatusCodeActionResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, status.StatusCode);
        Assert.Equal(TaskReasonCodes.PlanDateRequired, ReasonOf(result));
        Assert.Equal(TaskLifecycle.Open, fixture.Task.Lifecycle);
        Assert.Null(fixture.Task.PlannedDate);
    }

    // ── Deliberately loose validation ─────────────────────────────────────────

    [Fact]
    public async Task A_date_after_the_source_due_date_is_accepted()
    {
        // "I won't make it, planning for the 5th instead" is a real situation. Refusing it would force a lie —
        // an earlier date typed just to get the write accepted. The screen flags the mismatch as a warning; this
        // endpoint does not block on it.
        var fixture = new Fixture(TaskLifecycle.Open);
        fixture.Task.DueAt = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var laterDate = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero);

        AssertAccepted(await fixture.PostPlanAsync(laterDate));
        Assert.Equal(laterDate, fixture.Task.PlannedDate);
    }

    [Fact]
    public async Task A_date_in_the_past_is_accepted()
    {
        // PlannedDate is a personal note, not a commitment the system enforces.
        var fixture = new Fixture(TaskLifecycle.Open);
        var pastDate = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

        AssertAccepted(await fixture.PostPlanAsync(pastDate));
        Assert.Equal(pastDate, fixture.Task.PlannedDate);
    }

    // ── DueAt is a different fact ─────────────────────────────────────────────

    [Fact]
    public async Task Planning_never_touches_the_source_due_date()
    {
        // DueAt is the source's deadline and the basis for SLA; PlannedDate is the holder's own plan. If planning
        // ever overwrote DueAt, an SLA would silently move because someone picked a personal date.
        var fixture = new Fixture(TaskLifecycle.Open);
        var dueAt = new DateTimeOffset(2026, 9, 1, 13, 45, 30, TimeSpan.FromHours(3));
        fixture.Task.DueAt = dueAt;

        await fixture.PostPlanAsync(new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero));

        // Bit-for-bit: same instant AND same offset, not merely the same UTC moment.
        Assert.Equal(dueAt, fixture.Task.DueAt);
        Assert.Equal(dueAt.Offset, fixture.Task.DueAt!.Value.Offset);
    }

    [Fact]
    public async Task Replanning_does_not_touch_due_at_either()
    {
        var fixture = new Fixture(TaskLifecycle.Planned);
        var dueAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        fixture.Task.DueAt = dueAt;
        fixture.Task.PlannedDate = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        await fixture.PostPlanAsync(new DateTimeOffset(2026, 8, 25, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(dueAt, fixture.Task.DueAt);
    }

    // ── The projection round trip ─────────────────────────────────────────────

    [Fact]
    public async Task A_stored_plan_is_visible_on_the_next_projection()
    {
        /*
         * The write alone is not "actually stored" if nobody can ever see it again: the projection has to carry
         * it back, or the reader could never see their own plan and re-planning could never seed from the date
         * they actually chose — the same half-a-feature shape a capability with no container would be.
         */
        var fixture = new Fixture(TaskLifecycle.Open);
        var date = new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);

        await fixture.PostPlanAsync(date);
        var projection = await fixture.ProjectAsync();

        Assert.Equal(date, projection.PlannedDate);
    }

    [Fact]
    public async Task The_projection_carries_no_plan_when_nobody_has_planned_yet()
    {
        // Omitted, not today's date and not the due date — a task nobody has scheduled has no plan.
        var fixture = new Fixture(TaskLifecycle.Open);

        var projection = await fixture.ProjectAsync();

        Assert.Null(projection.PlannedDate);
    }

    [Fact]
    public async Task Planning_never_touches_the_projected_due_date_either()
    {
        var fixture = new Fixture(TaskLifecycle.Open);
        var dueAt = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        fixture.Task.DueAt = dueAt;

        await fixture.PostPlanAsync(new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero));
        var projection = await fixture.ProjectAsync();

        Assert.Equal(dueAt, projection.DueAt);
    }

    // ── The full-replace trap: nothing else moves ─────────────────────────────

    [Fact]
    public async Task Planning_does_not_disturb_unrelated_fields()
    {
        /*
         * This module has already lost data to a "read, mutate two fields, write the whole document" handler
         * that mutated MORE than two fields, or to a caller that round-tripped a stale copy. Pin every field a
         * plan write has no business touching.
         */
        var fixture = new Fixture(TaskLifecycle.Open);
        fixture.Task.SpentHours = 3.5m;
        fixture.Task.StartAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        fixture.Task.CompletedAt = null;
        fixture.Task.WaitingReason = "önceki not";
        fixture.Task.Title = "Değişmeyecek başlık";

        await fixture.PostPlanAsync(new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(3.5m, fixture.Task.SpentHours);
        Assert.Equal(new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero), fixture.Task.StartAt);
        Assert.Null(fixture.Task.CompletedAt);
        Assert.Equal("önceki not", fixture.Task.WaitingReason);
        Assert.Equal("Değişmeyecek başlık", fixture.Task.Title);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static void AssertAccepted(IActionResult result)
        => Assert.Equal(
            StatusCodes.Status204NoContent,
            Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);

    private static string? ReasonOf(IActionResult result)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        return Assert.IsType<Response<NoContent>>(objectResult.Value).ReasonCode;
    }

    private sealed class Fixture
    {
        private readonly FakeTaskItemRepository _tasks;
        private readonly TasksController _controller;

        public Fixture(TaskLifecycle lifecycle)
        {
            Task = new TaskItem
            {
                TenantId = TaskTestData.Tenant,
                Title = "CT probe",
                AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
                AssigneeUserId = TaskTestData.Me,
                CreatedByUserId = TaskTestData.Me,
                OrganizationUnitId = Guid.NewGuid(),
                Lifecycle = lifecycle,
                Version = 1
            };
            _tasks = new FakeTaskItemRepository(Task);

            var handler = new PlanTaskItemHandler(
                _tasks,
                new TaskLifecycleService(),
                new FakeCurrentUserContext(TaskTestData.Me));

            var correlation = new CorrelationContext();
            correlation.SetCorrelationId("corr");
            _controller = new TasksController(new DirectMediator(handler), correlation)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public TaskItem Task { get; }

        public Task<IActionResult> PostPlanAsync(DateTimeOffset plannedDate)
            => _controller.Plan(
                Task.Id,
                new PlanTaskItemRequest(Task.Version, plannedDate),
                CancellationToken.None);

        public async Task<WorkItemProjectionDto> ProjectAsync()
        {
            var provider = new TaskWorkItemProvider(
                _tasks,
                new FakePositionAssignmentRepository(),
                new TaskLifecycleService(),
                new TaskAssignmentResolver(),
                new FakeUserDisplayNameResolver(),
                new FakeChecklistRunRepository(),
                new FakeTaskApprovalService(),
                new FakeTaskDependencyRepository(),
                new FakeTaskCommentRepository());

            var actor = new WorkItemActor(TaskTestData.Me, IsPlatformActor: false, new HashSet<string>(
                new[] { TaskPermissions.Update, TaskPermissions.Claim, TaskPermissions.Complete },
                StringComparer.OrdinalIgnoreCase));

            return Assert.Single(
                (await provider.GetWorkItemsAsync(actor, CancellationToken.None))
                    .Where(item => item.Id == Task.Id.ToString()));
        }
    }
}
