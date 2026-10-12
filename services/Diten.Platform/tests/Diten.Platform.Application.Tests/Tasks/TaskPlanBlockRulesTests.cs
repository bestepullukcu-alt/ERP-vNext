using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkingHours;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>WP-TASK-CALENDAR-ENGINE-01 — the pure block arithmetic the plan write and the calendar feed share.</summary>
public sealed class TaskPlanBlockRulesTests
{
    private static readonly DateOnly Day = new(2026, 10, 5);
    private static readonly DateTimeOffset WindowStart = new(2026, 10, 5, 6, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset WindowEnd = new(2026, 10, 5, 15, 0, 0, TimeSpan.Zero);

    private static WorkingDay WorkingDay() => new(
        Day, WorkingDayKinds.WorkingDay, null, [new WorkingWindow(WindowStart, WindowEnd)],
        WorkingHoursSources.TenantDefault, CalendarUnresolved: false);

    [Theory]
    [InlineData(15, true)]
    [InlineData(45, true)]
    [InlineData(480, true)]
    [InlineData(0, false)]
    [InlineData(10, false)]
    [InlineData(20, false)]
    [InlineData(-15, false)]
    public void A_duration_is_whole_15_minute_steps_and_at_least_one(int minutes, bool valid)
        => Assert.Equal(valid, TaskPlanBlockRules.IsValidDuration(minutes));

    [Theory]
    [InlineData(null, 60)]
    [InlineData(0.0, 60)]
    [InlineData(1.0, 60)]
    [InlineData(1.4, 90)]
    [InlineData(0.1, 15)]
    [InlineData(2.25, 135)]
    public void The_default_length_is_the_estimate_rounded_up_else_60(double? hours, int expected)
        => Assert.Equal(expected, TaskPlanBlockRules.DefaultDuration(hours is null ? null : (decimal)hours));

    [Theory]
    [InlineData(4, 120, 120)]
    [InlineData(1, 120, 0)]
    public void Remaining_is_estimate_minus_block_floored_at_zero(double hours, int block, int expected)
        => Assert.Equal(expected, TaskPlanBlockRules.RemainingMinutes((decimal)hours, block));

    [Fact]
    public void Remaining_is_absent_without_an_estimate_or_a_block()
    {
        Assert.Null(TaskPlanBlockRules.RemainingMinutes(null, 60));
        Assert.Null(TaskPlanBlockRules.RemainingMinutes(2m, null));
    }

    [Fact]
    public void A_block_inside_the_window_is_kept_as_is()
        => Assert.Equal(new BlockFit(60, false, false), TaskPlanBlockRules.Fit(WindowStart, 60, WorkingDay()));

    [Fact]
    public void A_block_running_past_the_window_end_is_cut_there()
        => Assert.Equal(new BlockFit(120, true, false), TaskPlanBlockRules.Fit(WindowEnd.AddHours(-2), 240, WorkingDay()));

    [Fact]
    public void A_cut_is_floored_to_a_whole_step()
        => Assert.Equal(new BlockFit(30, true, false), TaskPlanBlockRules.Fit(WindowEnd.AddMinutes(-40), 60, WorkingDay()));

    [Fact]
    public void A_cut_that_would_leave_less_than_one_step_is_not_made_and_the_block_is_flagged_outside()
        => Assert.Equal(new BlockFit(60, false, true), TaskPlanBlockRules.Fit(WindowEnd.AddMinutes(-10), 60, WorkingDay()));

    [Fact]
    public void A_block_starting_before_or_after_the_window_is_flagged_not_cut()
    {
        Assert.Equal(new BlockFit(60, false, true), TaskPlanBlockRules.Fit(WindowStart.AddHours(-1), 60, WorkingDay()));
        Assert.Equal(new BlockFit(60, false, true), TaskPlanBlockRules.Fit(WindowEnd.AddHours(2), 60, WorkingDay()));
    }

    [Fact]
    public void A_day_with_no_window_flags_every_block()
    {
        var weekend = new WorkingDay(Day, WorkingDayKinds.Weekend, null, [], WorkingHoursSources.TenantDefault, false);
        Assert.Equal(new BlockFit(60, false, true), TaskPlanBlockRules.Fit(WindowStart, 60, weekend));
    }

    [Fact]
    public void Blocks_touching_end_to_start_do_not_overlap_but_crossing_ones_do()
    {
        var nine = WindowStart;
        Assert.False(TaskPlanBlockRules.Overlaps(nine, nine.AddHours(1), nine.AddHours(1), nine.AddHours(2)));
        Assert.True(TaskPlanBlockRules.Overlaps(nine, nine.AddHours(1), nine.AddMinutes(59), nine.AddHours(2)));
        Assert.True(TaskPlanBlockRules.Overlaps(nine, nine.AddHours(3), nine.AddHours(1), nine.AddHours(2)));
    }
}
