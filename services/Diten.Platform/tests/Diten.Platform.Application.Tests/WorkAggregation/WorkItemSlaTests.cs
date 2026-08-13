using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.WorkAggregation;

/// <summary>
/// WC-2 — the SLA decision, on the server, behind a working-time seam.
///
/// <para><b>What this replaces.</b> The browser decided it, in <c>mock-data.js computeSla()</c>: calendar-day
/// subtraction against a hard-coded <c>&lt;= 2</c>, with no notion of working time. That inverted the surface's
/// own law (the client renders decisions, it does not make them) and left the working calendar — the whole point
/// of WC-2 — with nothing on the server to arrive at.</para>
///
/// <para><b>The seam is the deliverable, not the arithmetic.</b> Today's calculator is honestly naive: 24/7,
/// every hour of every day. What must be true is that swapping it swaps every answer, which is what makes the
/// interface load-bearing instead of decorative — asserted below with a calculator no real calendar would
/// produce.</para>
/// </summary>
public sealed class WorkItemSlaTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 3, 10, 0, 0, TimeSpan.Zero); // a Monday

    // ── The three states, decided on the server ──────────────────────────────

    [Fact]
    public void Work_past_its_deadline_is_overdue()
    {
        Assert.Equal(WorkItemContract.SlaOverdue, Sla().Resolve(Now.AddDays(-1), Now));
    }

    [Fact]
    public void Work_inside_the_warning_window_is_due_soon()
    {
        Assert.Equal(WorkItemContract.SlaDueSoon, Sla().Resolve(Now.AddDays(1), Now));
    }

    [Fact]
    public void Work_beyond_the_window_is_on_track()
    {
        Assert.Equal(WorkItemContract.SlaOnTrack, Sla().Resolve(Now.AddDays(10), Now));
    }

    [Fact]
    public void Work_with_no_deadline_has_no_SLA()
    {
        // A legitimate state, not a missing one — reporting on-track would claim a comfort nobody measured.
        Assert.Equal(WorkItemContract.SlaNoSla, Sla().Resolve(null, Now));
    }

    [Fact]
    public void A_deadline_entered_as_a_DATE_lasts_until_the_end_of_that_day()
    {
        /*
         * Due dates are entered as dates and stored at midnight. Comparing `now` to that instant directly would
         * make every task overdue from 00:00 on the day it is due — the browser never did that, so shipping it
         * would have been a silent regression dressed up as a move to the server.
         */
        var dueToday = new DateTimeOffset(2026, 8, 3, 0, 0, 0, TimeSpan.Zero);

        Assert.NotEqual(WorkItemContract.SlaOverdue, Sla().Resolve(dueToday, Now));
    }

    [Fact]
    public void A_deadline_with_a_real_TIME_is_taken_literally()
    {
        // Non-vacuity for the rule above: it must apply to date-only deadlines, not swallow every deadline.
        // An approval task due at 09:00 IS late at 10:00.
        var dueThisMorning = new DateTimeOffset(2026, 8, 3, 9, 0, 0, TimeSpan.Zero);

        Assert.Equal(WorkItemContract.SlaOverdue, Sla().Resolve(dueThisMorning, Now));
    }

    // ── The threshold is not a constant in the code ──────────────────────────

    [Fact]
    public void Tightening_the_window_changes_the_answer()
    {
        /*
         * The proof that the threshold moved out of the code. The literal `2` in the browser was of unrecorded
         * origin, no tenant could change it and no test could vary it. Same deadline, same clock, two policies.
         */
        var dueInTwoDays = Now.AddDays(2).AddHours(-1);

        Assert.Equal(WorkItemContract.SlaDueSoon, Sla(dueSoonWithin: 2m).Resolve(dueInTwoDays, Now));
        Assert.Equal(WorkItemContract.SlaOnTrack, Sla(dueSoonWithin: 1m).Resolve(dueInTwoDays, Now));
    }

    [Fact]
    public void Widening_the_window_changes_it_the_other_way()
    {
        var dueInAWeek = Now.AddDays(7).AddHours(-1);

        Assert.Equal(WorkItemContract.SlaOnTrack, Sla(dueSoonWithin: 2m).Resolve(dueInAWeek, Now));
        Assert.Equal(WorkItemContract.SlaDueSoon, Sla(dueSoonWithin: 10m).Resolve(dueInAWeek, Now));
    }

    // ── The seam is real ─────────────────────────────────────────────────────

    [Fact]
    public void Swapping_the_working_time_calculator_swaps_the_answer()
    {
        /*
         * THE test of this slice. An interface nothing routes through is a comment; this proves every question
         * goes through it, because the only thing that changes here is the implementation behind it.
         *
         * The stand-in counts each day as ten working days, so two WORKING days is a fifth of a calendar day to
         * it: the same two-day policy opens a far narrower window. A deadline one day out is inside the window
         * under the real calculator and outside it under this one.
         */
        var dueTomorrow = Now.AddDays(1);

        Assert.Equal(WorkItemContract.SlaDueSoon, Sla().Resolve(dueTomorrow, Now));
        Assert.Equal(
            WorkItemContract.SlaOnTrack,
            SlaForTests.Over(new TenfoldWorkingTimeCalculator()).Resolve(dueTomorrow, Now));
    }

    [Fact]
    public void The_naive_calculator_treats_a_weekend_as_working_time()
    {
        /*
         * Pinned because it is a DECISION, not an oversight (see TwentyFourSevenWorkingTimeCalculator). Saturday
         * to Sunday is one working day here. When the real calendar lands this test moves to that class and this
         * one keeps asserting what the naive implementation promises — which is how anyone can tell the two apart.
         */
        var saturday = new DateTimeOffset(2026, 8, 8, 9, 0, 0, TimeSpan.Zero);
        var sunday = saturday.AddDays(1);
        var calculator = new TwentyFourSevenWorkingTimeCalculator();

        Assert.Equal(1m, calculator.UnitsBetween(saturday, sunday));
        Assert.Equal(sunday, calculator.Add(saturday, 1m));
    }

    [Fact]
    public void The_warning_boundary_is_walked_back_through_working_time()
    {
        /*
         * The reason the interface answers TWO questions. Under 24/7 the boundary and the measure agree, so this
         * pins the CALL rather than the arithmetic: with a calculator whose Add and UnitsBetween disagree, the
         * answer follows Add. A real calendar makes them disagree for real — two working days before a Monday
         * deadline is the preceding Thursday, not the preceding Saturday.
         */
        var sla = SlaForTests.Over(new AddOnlyIsHonestCalculator(), dueSoonWithinWorkingDays: 2m);

        // Add says the window opens 20 days back, so a deadline 10 days out is already inside it.
        Assert.Equal(WorkItemContract.SlaDueSoon, sla.Resolve(Now.AddDays(10), Now));
    }

    // ── The projection carries it ────────────────────────────────────────────

    [Fact]
    public async Task The_task_provider_projects_the_state_it_was_given()
    {
        // The provider must REPORT the decision, not make one of its own beside it.
        var item = await ProjectAsync(TaskWithDue(Now.AddDays(30)), new FakeWorkItemSlaCalculator(WorkItemContract.SlaOverdue));

        Assert.Equal(WorkItemContract.SlaOverdue, item.SlaState);
    }

    [Fact]
    public async Task A_task_with_no_deadline_projects_no_sla()
    {
        var item = await ProjectAsync(TaskWithDue(null), SlaForTests.Real());

        Assert.Equal(WorkItemContract.SlaNoSla, item.SlaState);
    }

    [Theory]
    [InlineData(-3, WorkItemContract.SlaOverdue)]
    [InlineData(1, WorkItemContract.SlaDueSoon)]
    [InlineData(30, WorkItemContract.SlaOnTrack)]
    public async Task All_three_states_reach_the_wire_from_the_real_calculator(int daysFromNow, string expected)
    {
        // Measured against the wall clock deliberately: the provider reads it, and a deadline placed relative to
        // "now" gives the same answer whenever this runs.
        var item = await ProjectAsync(TaskWithDue(DateTimeOffset.UtcNow.AddDays(daysFromNow)), SlaForTests.Real());

        Assert.Equal(expected, item.SlaState);
    }

    [Fact]
    public async Task The_provider_asks_the_calculator_rather_than_subtracting_dates()
    {
        /*
         * Gap proof, as a test: if the provider ever computed the state inline, the injected calculator would go
         * unasked and this fails. The absurd answer below cannot be arrived at by any date arithmetic.
         */
        var calculator = new FakeWorkItemSlaCalculator(WorkItemContract.SlaOverdue);
        var due = DateTimeOffset.UtcNow.AddDays(365);

        var item = await ProjectAsync(TaskWithDue(due), calculator);

        Assert.Equal(WorkItemContract.SlaOverdue, item.SlaState);
        Assert.Equal(due, Assert.Single(calculator.Asked));
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static IWorkItemSlaCalculator Sla(decimal dueSoonWithin = 2m)
        => SlaForTests.Real(dueSoonWithin);

    private static async Task<WorkItemProjectionDto> ProjectAsync(TaskItem task, IWorkItemSlaCalculator sla)
    {
        var provider = new TaskWorkItemProvider(
            new FakeTaskItemRepository(task),
            new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(),
            new FakeTaskApprovalService(),
            new FakeTaskDependencyRepository(),
            new FakeTaskCommentRepository(), new FakeTaskTransitionRepository(), TaskActors.PermitAll(),
            new FakePositionRepository(),
            new FakeOrganizationUnitRepository(),
            sla,
            new FakeTaskFieldDefinitionRepository());

        var actor = new WorkItemActor(TaskTestData.Me, IsPlatformActor: true, new HashSet<string>());
        return Assert.Single(await provider.GetWorkItemsAsync(actor, CancellationToken.None));
    }

    private static TaskItem TaskWithDue(DateTimeOffset? dueAt) => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Son tarihi olan iş",
        Lifecycle = TaskLifecycle.InProgress,
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        DueAt = dueAt,
        Version = 1
    };

    /// <summary>
    /// Answers its two questions INCONSISTENTLY on purpose: <c>Add</c> walks twenty days per unit while
    /// <c>UnitsBetween</c> reports elapsed days. No real calendar does this — it exists only to show WHICH
    /// question the boundary is derived from.
    /// </summary>
    private sealed class AddOnlyIsHonestCalculator : IWorkingTimeCalculator
    {
        public decimal UnitsBetween(DateTimeOffset from, DateTimeOffset to) => (decimal)(to - from).TotalDays;

        public DateTimeOffset Add(DateTimeOffset from, decimal units) => from.AddDays((double)units * 20d);
    }
}
