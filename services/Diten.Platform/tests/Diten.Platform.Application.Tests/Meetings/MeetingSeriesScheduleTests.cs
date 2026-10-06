using Diten.Platform.Application.Features.Meetings.Services;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>MOD-0357 S11 (KS5) — pure schedule math, mirroring TaskRecurrenceScheduleTests' own coverage shape
/// for the sibling MOD-0024 class this one copies.</summary>
public sealed class MeetingSeriesScheduleTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    private static MeetingSeries Series(
        DateTimeOffset startsAt,
        MeetingSeriesFrequency frequency = MeetingSeriesFrequency.Weekly,
        int interval = 1,
        int leadTimeDays = 14,
        DateTimeOffset? endsAt = null,
        bool isActive = true,
        DateTimeOffset? lastGeneratedAt = null) => new()
    {
        TenantId = TenantId, Name = "S", MeetingTypeId = Guid.NewGuid(), OrganizerUserId = Guid.NewGuid(),
        StartsAt = startsAt, Frequency = frequency, Interval = interval, LeadTimeDays = leadTimeDays,
        EndsAt = endsAt, IsActive = isActive, LastGeneratedAt = lastGeneratedAt, CreatedBy = "test"
    };

    [Fact]
    public void The_first_occurrence_is_generated_once_it_falls_inside_the_lead_time_window()
    {
        var startsAt = new DateTimeOffset(2027, 1, 20, 9, 0, 0, TimeSpan.Zero);
        var series = Series(startsAt, leadTimeDays: 14);

        // 15 days before start — still outside the window.
        Assert.Null(MeetingSeriesSchedule.NextDueOccurrence(series, startsAt.AddDays(-15)));

        // Exactly 14 days before start — inside the window.
        Assert.Equal(startsAt, MeetingSeriesSchedule.NextDueOccurrence(series, startsAt.AddDays(-14)));

        // The day of, and after — still owed (a missed sweep window is not a reason to skip it, K6's own
        // short-lead-time reasoning: the window only bounds "not yet", never "too late").
        Assert.Equal(startsAt, MeetingSeriesSchedule.NextDueOccurrence(series, startsAt));
        Assert.Equal(startsAt, MeetingSeriesSchedule.NextDueOccurrence(series, startsAt.AddDays(5)));
    }

    [Fact]
    public void After_the_first_instance_the_NEXT_occurrence_is_one_period_later_not_the_same_one_again()
    {
        var startsAt = new DateTimeOffset(2027, 1, 20, 9, 0, 0, TimeSpan.Zero);
        var series = Series(startsAt, MeetingSeriesFrequency.Weekly, leadTimeDays: 14, lastGeneratedAt: startsAt);

        var next = MeetingSeriesSchedule.NextDueOccurrence(series, startsAt.AddDays(1));

        Assert.Equal(startsAt.AddDays(7), next);
    }

    [Theory]
    [InlineData(MeetingSeriesFrequency.Weekly, 1, 14)]
    [InlineData(MeetingSeriesFrequency.Weekly, 2, 28)]
    public void Interval_and_frequency_scale_the_step_between_occurrences(
        MeetingSeriesFrequency frequency, int interval, int expectedDaysToThirdOccurrence)
    {
        var startsAt = new DateTimeOffset(2027, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var series = Series(startsAt, frequency, interval, leadTimeDays: 3650);

        // Occurrence index 2 — the THIRD occurrence (0, 1, 2).
        var third = MeetingSeriesSchedule.OccurrenceAt(series, 2);

        Assert.Equal(startsAt.AddDays(expectedDaysToThirdOccurrence), third);
    }

    [Fact]
    public void Quarterly_steps_by_three_months_per_interval()
    {
        var startsAt = new DateTimeOffset(2027, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var series = Series(startsAt, MeetingSeriesFrequency.Quarterly, interval: 1, leadTimeDays: 3650);

        Assert.Equal(new DateTimeOffset(2027, 4, 1, 9, 0, 0, TimeSpan.Zero), MeetingSeriesSchedule.OccurrenceAt(series, 1));
        Assert.Equal(new DateTimeOffset(2027, 7, 1, 9, 0, 0, TimeSpan.Zero), MeetingSeriesSchedule.OccurrenceAt(series, 2));
    }

    [Fact]
    public void A_monthly_series_anchored_on_the_31st_clamps_at_month_end_and_returns_to_31_in_a_longer_month()
    {
        // The exact example TaskRecurrenceSchedule's own doc comment states for its sibling class.
        var startsAt = new DateTimeOffset(2027, 1, 31, 9, 0, 0, TimeSpan.Zero);
        var series = Series(startsAt, MeetingSeriesFrequency.Monthly, leadTimeDays: 3650);

        Assert.Equal(new DateTimeOffset(2027, 1, 31, 9, 0, 0, TimeSpan.Zero), MeetingSeriesSchedule.OccurrenceAt(series, 0));
        Assert.Equal(new DateTimeOffset(2027, 2, 28, 9, 0, 0, TimeSpan.Zero), MeetingSeriesSchedule.OccurrenceAt(series, 1));
        Assert.Equal(new DateTimeOffset(2027, 3, 31, 9, 0, 0, TimeSpan.Zero), MeetingSeriesSchedule.OccurrenceAt(series, 2));
    }

    [Fact]
    public void Nothing_is_owed_once_the_next_occurrence_would_fall_after_EndsAt()
    {
        var startsAt = new DateTimeOffset(2027, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var series = Series(startsAt, MeetingSeriesFrequency.Weekly, leadTimeDays: 3650, endsAt: startsAt.AddDays(3));

        // The first occurrence (== StartsAt) is still within EndsAt.
        Assert.NotNull(MeetingSeriesSchedule.NextDueOccurrence(series, startsAt));

        var afterFirst = Series(
            startsAt, MeetingSeriesFrequency.Weekly, leadTimeDays: 3650, endsAt: startsAt.AddDays(3), lastGeneratedAt: startsAt);
        // The SECOND occurrence (a week later) is after EndsAt (3 days later) — nothing more is owed.
        Assert.Null(MeetingSeriesSchedule.NextDueOccurrence(afterFirst, startsAt.AddDays(1)));
    }

    [Fact]
    public void An_inactive_series_owes_nothing_regardless_of_the_window()
    {
        var startsAt = new DateTimeOffset(2027, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var series = Series(startsAt, leadTimeDays: 3650, isActive: false);

        Assert.Null(MeetingSeriesSchedule.NextDueOccurrence(series, startsAt));
    }

    [Fact]
    public void A_deleted_series_owes_nothing_even_while_flagged_active()
    {
        var startsAt = new DateTimeOffset(2027, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var series = Series(startsAt, leadTimeDays: 3650);
        series.DeletedAt = DateTimeOffset.UtcNow;

        Assert.Null(MeetingSeriesSchedule.NextDueOccurrence(series, startsAt));
    }

    [Fact]
    public void ProcessInstanceId_is_deterministic_for_the_same_series_and_occurrence()
    {
        var seriesId = Guid.NewGuid();
        var occurrence = new DateTimeOffset(2027, 3, 1, 9, 0, 0, TimeSpan.Zero);

        var first = MeetingSeriesSchedule.ProcessInstanceId(seriesId, occurrence);
        var second = MeetingSeriesSchedule.ProcessInstanceId(seriesId, occurrence);

        Assert.Equal(first, second);
        Assert.NotEqual(first, MeetingSeriesSchedule.ProcessInstanceId(seriesId, occurrence.AddDays(1)));
    }
}
