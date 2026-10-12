using System.Net;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Application.Features.TimeEntry.BackgroundJobs;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// MOD-0280-FU01 T1b, CT acceptance round (P-2026-09-29-05 v2): the person corrects captured rows (F1), the earliest
/// reason ends a run (F2), a segment closing onto a locked week is minimised at once (F3), Accept starts the timer like
/// Start (F4) but no kind that lands outside InProgress does (F12), timer drafts are recomputed rather than lost (F5),
/// a task write with the timer off audits nothing (F6), and the midnight job's cap comes after its filter (F9).
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TimerCorrectionV2HttpMongoTests : TimerScenario
{
    public TimerCorrectionV2HttpMongoTests(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    private async Task<System.Text.Json.JsonElement> OnlyRowAsync(string weekKey = CurrentWeek)
        => Assert.Single((await GetWeekAsync(weekKey)).Data.GetProperty("entries").EnumerateArray());

    // ── F1 — the person corrects a forgotten timer, then submits and it is approved ─────────────────────────────

    [Fact]
    public async Task A_forgotten_task_timer_is_corrected_by_the_person_then_submitted_and_approved()
    {
        await EnableTimerAsync();
        var task = Guid.NewGuid();
        await SeedTaskAsync(Tenant, task, lifecycle: TaskLifecycle.Planned);

        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 7, 7, 0);
        Ok(await TaskVerbAsync(task, "start"));            // the task start starts the timer…
        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 8, 0, 30);
        Ok(await GetTimerAsync());                          // …which nobody stopped: cut at local midnight

        var timerRow = await OnlyRowAsync();
        Assert.Equal("Timer", timerRow.GetProperty("source").GetString());
        Assert.Equal(1020, timerRow.GetProperty("durationMinutes").GetInt32()); // 07:00 → 24:00
        Assert.False(timerRow.GetProperty("editedFromTimer").GetBoolean());

        Ok(await SaveFreshAsync(CurrentWeek, Row(new DateOnly(2026, 10, 7), 480, task, source: "Timer")));
        var corrected = await OnlyRowAsync();
        Assert.Equal("Timer", corrected.GetProperty("source").GetString());   // the source stays
        Assert.Equal(480, corrected.GetProperty("durationMinutes").GetInt32());
        Assert.True(corrected.GetProperty("editedFromTimer").GetBoolean());

        var submitted = await SubmitAsync();
        Ok(submitted);
        Ok(await DecideAsync(Manager, submitted.Data.GetProperty("weekId").GetGuid(), approve: true));
        Ok(await GetWeekAsync());

        Assert.Equal(480, await ApprovedMinutesAsync(task));
        Assert.Single(await StoredEntriesAsync(submitted.Data.GetProperty("weekId").GetGuid())); // no second, Manual copy
    }

    [Fact]
    public async Task A_captured_row_can_be_corrected_but_not_created_by_a_save()
    {
        await EnableTimerAsync();
        var invented = await SaveFreshAsync(CurrentWeek, Row(Monday, 60, TaskA, source: "Timer"));

        Assert.Equal(HttpStatusCode.BadRequest, invented.Status);
        Assert.Equal(TimeEntryReasonCodes.CapturedRowNotFound, invented.ReasonCode);
    }

    [Fact]
    public async Task The_day_limit_applies_to_the_days_a_save_changes_and_the_submit_checks_the_whole_week()
    {
        await EnableTimerAsync();
        var today = new DateOnly(2026, 10, 7);
        await SeedClosedSegmentAsync(today, 17 * 3600);
        Ok(await StartTimerAsync(category: Category));
        Ok(await StopTimerAsync());                         // the 17-hour timer draft on Wednesday

        Ok(await SaveFreshAsync(CurrentWeek, Row(Monday, 60, TaskA)));             // another day: fine
        var sameDay = await SaveFreshAsync(CurrentWeek, Row(Monday, 60, TaskA), Row(today, 60, TaskB));
        Assert.Equal(TimeEntryReasonCodes.DayImplausible, sameDay.ReasonCode);      // the 17-hour day, touched: refused
        Assert.Equal(TimeEntryReasonCodes.DayImplausible, (await SubmitAsync()).ReasonCode);

        Ok(await SaveFreshAsync(CurrentWeek, Row(Monday, 60, TaskA), Row(today, 540, category: Category, source: "Timer")));
        Ok(await SubmitAsync());
    }

    // ── F2 — the earliest reason ends the run ───────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(false)] // the first read after midnight
    [InlineData(true)]  // the midnight job
    public async Task A_task_completed_before_midnight_behind_the_hooks_back_closes_at_its_completion_not_at_midnight(bool job)
    {
        await EnableTimerAsync();
        await Collection<Tenant>(PlatformCollections.Tenants).UpdateOneAsync(
            t => t.Id == Tenant, Builders<Tenant>.Update.Set(t => t.Status, TenantStatus.Active));
        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 7, 10, 0);
        Ok(await StartTimerAsync(TaskA));

        // Completed at 18:00, and the hook never heard of it (what a hook that threw leaves behind).
        await Collection<TaskItem>(PlatformCollections.TaskItems).UpdateOneAsync(
            t => t.Id == TaskA, Builders<TaskItem>.Update.Set(t => t.Lifecycle, TaskLifecycle.Done));
        await Collection<TaskTransition>(PlatformCollections.TaskTransitions).InsertOneAsync(new TaskTransition
        {
            TenantId = Tenant, TaskItemId = TaskA, Kind = TaskTransitionKind.Completed,
            FromLifecycle = TaskLifecycle.InProgress, ToLifecycle = TaskLifecycle.Done, ActorUserId = Person,
            CreatedAt = IstanbulLocal(2026, 10, 7, 18, 0)
        });

        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 8, 0, 30);
        if (job)
        {
            using var scope = Host.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<TimerMidnightCloseJob>()
                .HandleAsync(new TimerMidnightCloseJobArgs(500), new BackgroundJobContext());
        }
        else
        {
            Ok(await GetTimerAsync());
        }

        var segment = Assert.Single(await SegmentsAsync());
        Assert.Equal(TimerStopReason.Reconcile, segment.StopReason);
        Assert.Equal(IstanbulLocal(2026, 10, 7, 18, 0), segment.StoppedAtUtc);
        Assert.Equal(8 * 3600, segment.DurationSeconds);
        Assert.DoesNotContain(segment.Id, Host.Notifier.Segments); // not a forgotten timer: no morning notification
    }

    // ── F3 — a segment closing onto a locked week is minimised at once ──────────────────────────────────────────

    [Fact]
    public async Task A_segment_that_closes_onto_an_approved_week_loses_its_instants_at_once_and_is_reported()
    {
        await EnableTimerAsync();
        var weekId = await SubmittedWeekAsync(Row(Monday, 120, TaskA));
        Ok(await DecideAsync(Manager, weekId, approve: true));
        Ok(await GetWeekAsync());

        Ok(await StartTimerAsync(category: Category));
        Host.Clock.UtcNow = Wednesday.AddMinutes(40);
        Ok(await StopTimerAsync());

        var late = Assert.Single(await SegmentsAsync());
        Assert.Null(late.StartedAtUtc);
        Assert.Null(late.StoppedAtUtc);
        Assert.NotNull(late.MinimisedAtUtc);
        Assert.Equal(40 * 60, late.DurationSeconds);
        Assert.Contains(Host.Audit.Requests, r => r.EntityType == "TimerSegment" && r.Operation == AuditOperation.Redact && r.EntityId == late.Id);
        Assert.Equal(45, Assert.Single((await GetWeekAsync()).Data.GetProperty("timerOutsideOpenWeek").EnumerateArray())
            .GetProperty("minutes").GetInt32());
    }

    [Fact]
    public async Task Timer_time_on_a_week_outside_the_edit_window_is_reported_too()
    {
        var old = new DateOnly(2026, 8, 25); // W35, outside W37…W41
        await SeedClosedSegmentAsync(old, 3600);

        var week = await GetWeekAsync("2026-W35");
        Ok(week);
        Assert.Equal(60, Assert.Single(week.Data.GetProperty("timerOutsideOpenWeek").EnumerateArray()).GetProperty("minutes").GetInt32());
    }

    // ── F4 / F12 — Accept starts the timer like Start; no kind that lands outside InProgress does ───────────────

    [Fact]
    public async Task Accepting_an_open_task_straight_into_progress_starts_the_holders_timer()
    {
        await EnableTimerAsync();
        var task = Guid.NewGuid();
        await SeedTaskAsync(Tenant, task, lifecycle: TaskLifecycle.Open);

        await ChokePointAsync(task, TaskTransitionKind.Accepted, Person, t => t.Lifecycle = TaskLifecycle.InProgress);

        var segment = Assert.Single(await SegmentsAsync());
        Assert.True(segment.IsRunning);
        Assert.Equal(TimerStartSource.TaskAccepted, segment.StartSource);
    }

    [Theory]
    [InlineData(TaskTransitionKind.Accepted, TaskLifecycle.Open)]
    [InlineData(TaskTransitionKind.Started, TaskLifecycle.Waiting)]
    [InlineData(TaskTransitionKind.Resumed, TaskLifecycle.Planned)]
    public async Task A_start_kind_that_does_not_land_in_progress_starts_nothing(TaskTransitionKind kind, TaskLifecycle to)
    {
        await EnableTimerAsync();
        var task = Guid.NewGuid();
        await SeedTaskAsync(Tenant, task, lifecycle: to == TaskLifecycle.Open ? TaskLifecycle.Planned : TaskLifecycle.Open);

        await ChokePointAsync(task, kind, Person, t => t.Lifecycle = to);

        Assert.Empty(await SegmentsAsync());
    }

    // ── F5 — a draft is recomputed, never lost ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_draft_a_close_never_wrote_is_reported_by_the_read_and_written_before_the_submit()
    {
        var today = new DateOnly(2026, 10, 7);
        Ok(await SaveFreshAsync(CurrentWeek, Row(Monday, 60, TaskA)));
        await SeedClosedSegmentAsync(today, 50 * 60); // closed, and its draft never written
        var weekId = (await StoredWeeksAsync()).Single().Id;

        var read = (await GetWeekAsync()).Data;
        var pending = Assert.Single(read.GetProperty("timerDraftPending").EnumerateArray());
        Assert.Equal(45, pending.GetProperty("segmentMinutes").GetInt32());
        Assert.Equal(0, pending.GetProperty("draftMinutes").GetInt32());
        Assert.Single(await StoredEntriesAsync(weekId));                             // the read wrote nothing

        var stale = await Host.PostAsync($"/api/v1/time-entry/weeks/{CurrentWeek}/submit", PersonToken(),
            new { expectedVersion = read.GetProperty("version").GetInt32() });
        Assert.Equal(TimeEntryReasonCodes.ConcurrencyConflict, stale.ReasonCode);  // the submit wrote the draft first…
        Assert.Contains(await StoredEntriesAsync(weekId), e => e.Source == TimeEntrySource.Timer && e.DurationMinutes == 45);
        Assert.Empty((await GetWeekAsync()).Data.GetProperty("timerDraftPending").EnumerateArray());
        Ok(await SubmitAsync());                                                    // …and the person submits what they see
    }

    // ── F6 — the timer off: a task write audits nothing about timers ────────────────────────────────────────────

    [Fact]
    public async Task With_the_timer_off_starting_a_task_writes_and_audits_nothing_about_timers()
    {
        var task = Guid.NewGuid();
        await SeedTaskAsync(Tenant, task, lifecycle: TaskLifecycle.Planned);
        // On for ANOTHER legal entity only — the cheap tenant check passes, the person's own switch still says no.
        await EnableTimerAsync(legalEntity: Guid.NewGuid());

        Ok(await TaskVerbAsync(task, "start"));

        Assert.Empty(await SegmentsAsync());
        Assert.DoesNotContain(Host.Audit.Requests, r => r.EntityType == "TimerSegment");
    }

    // ── F9 — the midnight job's cap comes after its filter ──────────────────────────────────────────────────────

    [Fact]
    public async Task Timers_still_running_today_never_crowd_a_forgotten_one_out_of_the_midnight_run()
    {
        await EnableTimerAsync();
        await Collection<Tenant>(PlatformCollections.Tenants).UpdateOneAsync(
            t => t.Id == Tenant, Builders<Tenant>.Update.Set(t => t.Status, TenantStatus.Active));
        // Today's running timers go in FIRST, so a cap applied before the filter would pick one of them.
        foreach (var other in new[] { SecondPerson, Manager, GrandManager, Stranger })
        {
            await Collection<TimerSegment>(PlatformCollections.TimeEntryTimerSegments).InsertOneAsync(new TimerSegment
            {
                TenantId = Tenant, UserId = other, CategoryCode = Category, IsRunning = true,
                StartedAtUtc = IstanbulLocal(2026, 10, 7, 8, 0), LocalDate = new DateOnly(2026, 10, 7), TimeZoneId = Zone,
                WeekKey = CurrentWeek
            });
        }

        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 6, 23, 0);
        Ok(await StartTimerAsync(category: Category));  // Person's, forgotten over midnight
        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 7, 9, 0);

        using (var scope = Host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TimerMidnightCloseJob>()
                .HandleAsync(new TimerMidnightCloseJobArgs(1), new BackgroundJobContext());
        }

        Assert.Equal(TimerStopReason.LocalMidnight, Assert.Single(await SegmentsAsync()).StopReason);
        Assert.Equal(1, await RunningCountAsync(SecondPerson));
    }
}
