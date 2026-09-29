using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Application.Features.WorkingHours;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>MOD-0280-FU01 T1b — the timer arithmetic on its own (A2, D3): the HTTP suites measure it end to end.</summary>
public sealed class TimerRulesTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(479, 0)]
    [InlineData(480, 15)]
    [InlineData(1349, 15)]
    [InlineData(1350, 30)]
    [InlineData(3600, 60)]
    [InlineData(4049, 60)]
    [InlineData(4050, 75)]
    public void Draft_minutes_round_to_the_nearest_15_ties_up_with_an_8_minute_floor(long seconds, int minutes)
        => Assert.Equal(minutes, TimerRules.DraftMinutes(seconds));

    [Fact]
    public void Local_midnight_follows_the_zone_rules_on_a_dst_night()
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        // The night of 2026-10-25 is 25 hours long in Berlin: 24th 22:00Z (00:00 CEST) → 25th 23:00Z (00:00 CET).
        Assert.Equal(new DateTimeOffset(2026, 10, 24, 22, 0, 0, TimeSpan.Zero), TimerRules.LocalMidnightAfter(new DateOnly(2026, 10, 24), berlin));
        Assert.Equal(new DateTimeOffset(2026, 10, 25, 23, 0, 0, TimeSpan.Zero), TimerRules.LocalMidnightAfter(new DateOnly(2026, 10, 25), berlin));
    }

    [Fact]
    public void A_stop_after_midnight_is_cut_at_midnight()
    {
        var istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        var start = new DateTimeOffset(2026, 10, 6, 20, 30, 0, TimeSpan.Zero); // 23:30 local
        var (stop, atMidnight) = TimerRules.StopPoint(start, new DateOnly(2026, 10, 6), istanbul, start.AddHours(5));

        Assert.True(atMidnight);
        Assert.Equal(new DateTimeOffset(2026, 10, 6, 21, 0, 0, TimeSpan.Zero), stop);
    }

    [Fact]
    public void Minutes_outside_the_working_window_are_counted()
    {
        var day = new WorkingDay(new DateOnly(2026, 10, 7), WorkingDayKinds.WorkingDay, null,
            [new WorkingWindow(new DateTimeOffset(2026, 10, 7, 6, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 7, 15, 0, 0, TimeSpan.Zero))],
            WorkingHoursSources.TenantDefault, false);

        // 17:30–19:00 local (14:30Z–16:00Z) with a 09:00–18:00 window: 60 minutes outside.
        Assert.Equal(60, TimerRules.OutsideWorkingMinutes(
            new DateTimeOffset(2026, 10, 7, 14, 30, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 7, 16, 0, 0, TimeSpan.Zero), day));
        Assert.Equal(90, TimerRules.OutsideWorkingMinutes(
            new DateTimeOffset(2026, 10, 10, 14, 30, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 10, 16, 0, 0, TimeSpan.Zero),
            day with { Windows = [] }));
    }
}
