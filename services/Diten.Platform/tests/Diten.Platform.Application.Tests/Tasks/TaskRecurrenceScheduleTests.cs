using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// Phase 4 — WHEN a rule is due, and what the occurrence is CALLED. Pure arithmetic, so every answer here is
/// exactly reproducible and none of it depends on a clock.
/// </summary>
public sealed class TaskRecurrenceScheduleTests
{
    private static readonly Guid RuleId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly DateTimeOffset Anchor = new(2026, 1, 5, 9, 0, 0, TimeSpan.Zero); // a Monday

    // ── The occurrence NAME ──────────────────────────────────────────────────

    [Fact]
    public void The_process_instance_id_names_the_PERIOD()
    {
        /*
         * The whole duplicate guard rests on this being derivable rather than invented. A random id would
         * distinguish nothing: two passes over the same period would mint two names, the "already made?" check
         * could never fire, and the duplicate would be hidden rather than prevented.
         */
        var id = TaskRecurrenceSchedule.ProcessInstanceId(RuleId, Anchor);

        Assert.Equal("task-recurrence:11111111222233334444555555555555:20260105T090000Z", id);
    }

    [Fact]
    public void The_SAME_period_always_gets_the_same_name()
    {
        // Computed twice, from equal-but-different instants (one expressed in another offset).
        var utc = new DateTimeOffset(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);
        var sameMomentElsewhere = new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.FromHours(3));

        Assert.Equal(
            TaskRecurrenceSchedule.ProcessInstanceId(RuleId, utc),
            TaskRecurrenceSchedule.ProcessInstanceId(RuleId, sameMomentElsewhere));
    }

    [Fact]
    public void DIFFERENT_periods_get_different_names()
    {
        // Non-vacuity for the two above: a name that ignored the instant would satisfy both and prevent every
        // occurrence after the first.
        Assert.NotEqual(
            TaskRecurrenceSchedule.ProcessInstanceId(RuleId, Anchor),
            TaskRecurrenceSchedule.ProcessInstanceId(RuleId, Anchor.AddDays(1)));
    }

    [Fact]
    public void Two_rules_firing_at_the_same_instant_get_different_names()
    {
        Assert.NotEqual(
            TaskRecurrenceSchedule.ProcessInstanceId(RuleId, Anchor),
            TaskRecurrenceSchedule.ProcessInstanceId(Guid.NewGuid(), Anchor));
    }

    // ── Every frequency ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(TaskRecurrenceFrequency.Daily, 1, 1, "2026-01-06")]
    [InlineData(TaskRecurrenceFrequency.Daily, 3, 2, "2026-01-11")]
    [InlineData(TaskRecurrenceFrequency.Weekly, 1, 1, "2026-01-12")]
    [InlineData(TaskRecurrenceFrequency.Weekly, 2, 2, "2026-02-02")]
    [InlineData(TaskRecurrenceFrequency.Monthly, 1, 1, "2026-02-05")]
    [InlineData(TaskRecurrenceFrequency.Monthly, 2, 3, "2026-07-05")]
    [InlineData(TaskRecurrenceFrequency.Quarterly, 1, 1, "2026-04-05")]
    [InlineData(TaskRecurrenceFrequency.Quarterly, 2, 1, "2026-07-05")]
    [InlineData(TaskRecurrenceFrequency.Yearly, 1, 1, "2027-01-05")]
    [InlineData(TaskRecurrenceFrequency.Yearly, 2, 2, "2030-01-05")]
    public void Occurrences_step_by_frequency_and_interval(
        TaskRecurrenceFrequency frequency, int interval, int index, string expectedDate)
    {
        var occurrence = TaskRecurrenceSchedule.OccurrenceAt(Rule(frequency, interval), index);

        Assert.Equal(DateTime.Parse(expectedDate).Date, occurrence.UtcDateTime.Date);
        // The time of day is the anchor's — a daily rule fires at the hour it was scheduled for, not midnight.
        Assert.Equal(Anchor.UtcDateTime.TimeOfDay, occurrence.UtcDateTime.TimeOfDay);
    }

    // ── The month-end decision ───────────────────────────────────────────────

    [Fact]
    public void A_rule_anchored_on_the_31st_CLAMPS_in_a_short_month()
    {
        /*
         * THE DECISION, pinned. A monthly rule on the 31st runs on the 28th in February rather than skipping
         * February entirely. Skipping silently loses a period of work, and a monthly task that simply does not
         * appear is the kind of absence nobody notices until an audit asks for it.
         */
        var rule = Rule(TaskRecurrenceFrequency.Monthly, 1, anchor: new DateTimeOffset(2026, 1, 31, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTime(2026, 2, 28), TaskRecurrenceSchedule.OccurrenceAt(rule, 1).UtcDateTime.Date);
    }

    [Fact]
    public void And_it_RETURNS_to_the_31st_the_next_long_month()
    {
        /*
         * The other half, and the reason occurrences are computed from the ANCHOR rather than by stepping.
         * Stepping would give 28 Feb → 28 Mar → 28 Apr: the rule would walk off the month end and never come
         * back, which is the failure this arrangement exists to avoid.
         */
        var rule = Rule(TaskRecurrenceFrequency.Monthly, 1, anchor: new DateTimeOffset(2026, 1, 31, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTime(2026, 3, 31), TaskRecurrenceSchedule.OccurrenceAt(rule, 2).UtcDateTime.Date);
        Assert.Equal(new DateTime(2026, 4, 30), TaskRecurrenceSchedule.OccurrenceAt(rule, 3).UtcDateTime.Date);
    }

    [Fact]
    public void A_leap_day_rule_finds_the_29th_when_there_is_one()
    {
        var rule = Rule(TaskRecurrenceFrequency.Yearly, 1, anchor: new DateTimeOffset(2028, 2, 29, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTime(2029, 2, 28), TaskRecurrenceSchedule.OccurrenceAt(rule, 1).UtcDateTime.Date);
        Assert.Equal(new DateTime(2032, 2, 29), TaskRecurrenceSchedule.OccurrenceAt(rule, 4).UtcDateTime.Date);
    }

    // ── What is owed right now ───────────────────────────────────────────────

    [Fact]
    public void Nothing_is_owed_before_the_rule_starts()
    {
        var rule = Rule(TaskRecurrenceFrequency.Daily, 1);

        Assert.Null(TaskRecurrenceSchedule.LatestDueOccurrence(rule, Anchor.AddHours(-1)));
    }

    [Fact]
    public void The_LATEST_begun_occurrence_is_owed_and_only_that_one()
    {
        /*
         * A daily rule dormant for three weeks does not produce twenty-one tasks the moment the sweep notices.
         * A daily task from three weeks ago is not work anyone wants appearing now, and a flood is how a recovery
         * becomes an incident.
         */
        var rule = Rule(TaskRecurrenceFrequency.Daily, 1);

        var owed = TaskRecurrenceSchedule.LatestDueOccurrence(rule, Anchor.AddDays(21).AddHours(2));

        Assert.Equal(Anchor.AddDays(21), owed);
    }

    [Fact]
    public void Nothing_is_owed_after_the_rule_ENDS()
    {
        var rule = Rule(TaskRecurrenceFrequency.Daily, 1);
        rule.EndsAt = Anchor.AddDays(3);

        Assert.Null(TaskRecurrenceSchedule.LatestDueOccurrence(rule, Anchor.AddDays(10)));
    }

    [Fact]
    public void An_occurrence_PAST_the_end_is_not_owed_even_inside_the_window()
    {
        // The subtler half: `now` is still before EndsAt, but the occurrence that would fire is after it.
        var rule = Rule(TaskRecurrenceFrequency.Daily, 1);
        rule.EndsAt = Anchor.AddDays(3).AddHours(-1);

        Assert.Equal(Anchor.AddDays(2), TaskRecurrenceSchedule.LatestDueOccurrence(rule, Anchor.AddDays(3).AddHours(-2)));
    }

    [Fact]
    public void An_INACTIVE_rule_owes_nothing()
    {
        var rule = Rule(TaskRecurrenceFrequency.Daily, 1);
        rule.IsActive = false;

        Assert.Null(TaskRecurrenceSchedule.LatestDueOccurrence(rule, Anchor.AddDays(5)));
    }

    [Fact]
    public void A_DELETED_rule_owes_nothing()
    {
        var rule = Rule(TaskRecurrenceFrequency.Daily, 1);
        rule.DeletedAt = DateTimeOffset.UtcNow;

        Assert.Null(TaskRecurrenceSchedule.LatestDueOccurrence(rule, Anchor.AddDays(5)));
    }

    [Fact]
    public void A_rule_with_NO_frequency_owes_nothing()
    {
        // A schedule that never fires. Accepting it silently would put a live-looking row in the list that
        // produces nothing, which reads as a broken sweep rather than a misconfigured rule.
        Assert.Null(TaskRecurrenceSchedule.LatestDueOccurrence(
            Rule(TaskRecurrenceFrequency.None, 1), Anchor.AddDays(5)));
    }

    [Fact]
    public void An_ACTIVE_undeleted_rule_inside_its_window_DOES_owe_something()
    {
        // Non-vacuity for the four tests above: if LatestDueOccurrence always returned null they would all pass
        // while recurrence never produced anything at all.
        Assert.NotNull(TaskRecurrenceSchedule.LatestDueOccurrence(Rule(TaskRecurrenceFrequency.Daily, 1), Anchor.AddDays(5)));
    }

    // ── The generated task's deadline ────────────────────────────────────────

    [Fact]
    public void The_next_occurrence_is_the_deadline_recurring_work_inherits()
    {
        // Recurring work is expected to be finished before its replacement arrives.
        var rule = Rule(TaskRecurrenceFrequency.Weekly, 1);

        Assert.Equal(Anchor.AddDays(7), TaskRecurrenceSchedule.NextOccurrenceAfter(rule, Anchor));
        Assert.Equal(Anchor.AddDays(14), TaskRecurrenceSchedule.NextOccurrenceAfter(rule, Anchor.AddDays(7)));
    }

    [Fact]
    public void The_deadline_clamps_with_the_schedule()
    {
        var rule = Rule(TaskRecurrenceFrequency.Monthly, 1, anchor: new DateTimeOffset(2026, 1, 31, 9, 0, 0, TimeSpan.Zero));

        var next = TaskRecurrenceSchedule.NextOccurrenceAfter(rule, new DateTimeOffset(2026, 1, 31, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTime(2026, 2, 28), next.UtcDateTime.Date);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static TaskRecurrenceRule Rule(
        TaskRecurrenceFrequency frequency, int interval, DateTimeOffset? anchor = null) => new()
    {
        Id = RuleId,
        TenantId = TaskTestData.Tenant,
        Name = "Tekrarlayan iş",
        Frequency = frequency,
        Interval = interval,
        StartsAt = anchor ?? Anchor,
        IsActive = true,
        Version = 1
    };
}
