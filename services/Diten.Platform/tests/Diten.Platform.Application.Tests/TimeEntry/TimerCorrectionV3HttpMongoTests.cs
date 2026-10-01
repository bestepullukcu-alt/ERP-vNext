using System.Net;
using System.Text.Json;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Application.Features.TimeEntry.BackgroundJobs;
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
/// MOD-0280-FU01 T1b, CT acceptance round 2 (P-2026-09-29-05 v3): timer time after a correction is kept (G1), every
/// correction is audited with before/after and shown to the approver (G2), an untouched captured row never fails a save
/// (G3), one person's failure never stops the midnight run (G4), sources parse strictly (G5), only a transition that really
/// took the task away ends a run (G6), and the midnight close audits the reason it closed with (G7).
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TimerCorrectionV3HttpMongoTests : TimerScenario
{
    private static readonly DateOnly Today = new(2026, 10, 7);

    public TimerCorrectionV3HttpMongoTests(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    private async Task<JsonElement> RowOfAsync(Guid task)
        => (await GetWeekAsync()).Data.GetProperty("entries").EnumerateArray()
            .Single(r => r.TryGetProperty("taskItemId", out var t) && t.ValueKind == JsonValueKind.String && t.GetGuid() == task);

    /// <summary>10:00–11:00 on TaskA by the timer, then the person says it was 90 minutes.</summary>
    private async Task RunAnHourThenCorrectTo90Async()
    {
        await EnableTimerAsync();
        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 7, 10, 0);
        Ok(await StartTimerAsync(TaskA));
        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 7, 11, 0);
        Ok(await StopTimerAsync());
        Assert.Equal(60, (await RowOfAsync(TaskA)).GetProperty("durationMinutes").GetInt32());

        Ok(await SaveFreshAsync(CurrentWeek, Row(Today, 90, TaskA, source: "Timer")));
    }

    // ── G1 ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Timer_time_after_a_correction_is_added_to_the_persons_value_and_reaches_the_approval()
    {
        await RunAnHourThenCorrectTo90Async();

        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 7, 13, 0);
        Ok(await StartTimerAsync(TaskA));
        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 7, 17, 0);
        Ok(await StopTimerAsync());

        var row = await RowOfAsync(TaskA);
        Assert.Equal(90 + 240, row.GetProperty("durationMinutes").GetInt32());
        Assert.True(row.GetProperty("editedFromTimer").GetBoolean());
        Assert.Empty((await GetWeekAsync()).Data.GetProperty("timerDraftPending").EnumerateArray());

        var submitted = await SubmitAsync();
        Ok(submitted);
        Ok(await DecideAsync(Manager, submitted.Data.GetProperty("weekId").GetGuid(), approve: true));
        Ok(await GetWeekAsync());
        Assert.Equal(330, await ApprovedMinutesAsync(TaskA));
    }

    [Fact]
    public async Task Timer_time_after_a_correction_that_is_not_yet_in_the_draft_is_shown_as_pending()
    {
        await RunAnHourThenCorrectTo90Async();
        await SeedClosedSegmentAsync(Today, 3600, task: TaskA); // closed, its draft never written

        var pending = Assert.Single((await GetWeekAsync()).Data.GetProperty("timerDraftPending").EnumerateArray());
        Assert.Equal(150, pending.GetProperty("segmentMinutes").GetInt32());
        Assert.Equal(90, pending.GetProperty("draftMinutes").GetInt32());
    }

    // ── G2 ──────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// CT acceptance (2026-09-30): a person who only adds a NOTE to a timer row (minutes unchanged) is saved and audited —
    /// treating "same minutes" as "untouched" passed the whole suite before this test.
    /// </summary>
    [Fact]
    public async Task A_note_added_to_a_timer_row_with_the_same_minutes_is_saved_and_audited()
    {
        await EnableTimerAsync();
        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 7, 10, 0);
        Ok(await StartTimerAsync(TaskA));
        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 7, 11, 0);
        Ok(await StopTimerAsync());

        Ok(await SaveFreshAsync(CurrentWeek, Row(Today, 60, TaskA, note: "Müşteri hazırlığı", source: "Timer")));

        var row = await RowOfAsync(TaskA);
        Assert.Equal(60, row.GetProperty("durationMinutes").GetInt32());
        Assert.Equal("Müşteri hazırlığı", row.GetProperty("note").GetString());
        var audit = Assert.Single(Host.Audit.Requests, r => r.Metadata?.GetValueOrDefault("transition") as string == "correct-captured-row");
        Assert.Equal(true, audit.Metadata!["noteChanged"]);
    }

    // ── CT acceptance (2026-09-30): a failed recompute never lets timer time fall out of the record ─────────────────

    [Fact]
    public async Task A_submit_is_refused_while_the_timer_drafts_cannot_be_recomputed_and_goes_through_once_they_can()
    {
        await RunAnHourThenCorrectTo90Async();
        Host.Probes.BeforeWeekDraftRecompute = (_, _) => throw new InvalidOperationException("recompute down");

        var refused = await SubmitAsync();
        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        Assert.Equal(TimeEntryReasonCodes.TimerDraftsUnavailable, refused.ReasonCode);

        Host.Probes.BeforeWeekDraftRecompute = null;
        Ok(await SubmitAsync());
    }

    [Fact]
    public async Task With_the_recompute_down_a_captured_row_correction_waits_but_a_typed_row_is_saved()
    {
        await EnableTimerAsync();
        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 7, 10, 0);
        Ok(await StartTimerAsync(TaskA));
        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 7, 11, 0);
        Ok(await StopTimerAsync());
        Host.Probes.BeforeWeekDraftRecompute = (_, _) => throw new InvalidOperationException("recompute down");

        var correction = await SaveFreshAsync(CurrentWeek, Row(Today, 90, TaskA, source: "Timer"));
        Assert.Equal(HttpStatusCode.Conflict, correction.Status);
        Assert.Equal(TimeEntryReasonCodes.TimerDraftsUnavailable, correction.ReasonCode);
        Assert.Equal(60, (await RowOfAsync(TaskA)).GetProperty("durationMinutes").GetInt32());

        Ok(await SaveFreshAsync(CurrentWeek, Row(Monday, 30, TaskA)));
    }

    /// <summary>CT acceptance (2026-09-30): a correction revision copies the captured/corrected/baseline fields, so its
    /// approver sees the captured value and late timer time is still added to a corrected row.</summary>
    [Fact]
    public async Task A_correction_revision_keeps_the_captured_value_the_correction_and_its_baseline()
    {
        await RunAnHourThenCorrectTo90Async();
        var submitted = await SubmitAsync();
        Ok(submitted);
        var approvedWeekId = submitted.Data.GetProperty("weekId").GetGuid();
        Ok(await DecideAsync(Manager, approvedWeekId, approve: true));
        Ok(await GetWeekAsync());                                // the pull finalizer applies the decision on read
        var approvedRow = Assert.Single(await StoredEntriesAsync(approvedWeekId));

        var opened = await Host.PostAsync($"/api/v1/time-entry/weeks/{CurrentWeek}/corrections", PersonToken(), new { reason = "Toplantı notu eksik" });
        Assert.Equal(HttpStatusCode.Created, opened.Status);
        var correctionId = (await StoredWeeksAsync()).Single(w => w.Id != approvedWeekId).Id;
        var copied = Assert.Single(await StoredEntriesAsync(correctionId));

        Assert.True(copied.EditedFromTimer);
        Assert.Equal(approvedRow.CapturedMinutes, copied.CapturedMinutes);
        Assert.Equal(60, copied.CapturedMinutes);
        Assert.Equal(approvedRow.CorrectedMinutes, copied.CorrectedMinutes);
        Assert.Equal(approvedRow.CorrectionBaselineSeconds, copied.CorrectionBaselineSeconds);
        Assert.NotNull(copied.CorrectionBaselineSeconds);
    }

    [Fact]
    public async Task A_correction_is_audited_with_before_and_after_on_the_week_and_the_approver_sees_the_captured_value()
    {
        await RunAnHourThenCorrectTo90Async();
        var weekId = (await StoredWeeksAsync()).Single().Id;

        var audit = Assert.Single(Host.Audit.Requests, r => r.EntityType == "TimesheetWeek"
                                                         && r.Metadata?.GetValueOrDefault("transition") as string == "correct-captured-row");
        Assert.Equal(weekId, audit.EntityId);
        Assert.Equal(60, Convert.ToInt32(audit.Metadata!["beforeMinutes"]));
        Assert.Equal(90, Convert.ToInt32(audit.Metadata["afterMinutes"]));
        Assert.Equal(false, audit.Metadata["noteChanged"]);

        Ok(await SubmitAsync());
        var approval = await Host.GetAsync($"/api/v1/time-entry/approvals/{weekId}", ApproverToken(Manager));
        Ok(approval);
        var entry = Assert.Single(approval.Data.GetProperty("entries").EnumerateArray());
        Assert.True(entry.GetProperty("editedFromTimer").GetBoolean());
        Assert.Equal(60, entry.GetProperty("capturedMinutes").GetInt32());
        Assert.Equal(90, entry.GetProperty("durationMinutes").GetInt32());
    }

    // ── G3 ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_untouched_captured_row_sent_back_never_fails_a_save_even_over_16_hours()
    {
        await EnableTimerAsync();
        await SeedClosedSegmentAsync(Today, 17 * 3600);
        Ok(await StartTimerAsync(category: Category));
        Ok(await StopTimerAsync());                              // a 1020-minute timer draft

        // Sent back exactly as stored — a blank note is no note — together with a new row on another day: fine.
        Ok(await SaveFreshAsync(CurrentWeek,
            Row(Today, 1020, category: Category, note: "", source: "Timer"), Row(Monday, 60, TaskA)));
        Assert.Equal(1020, Assert.Single((await GetWeekAsync()).Data.GetProperty("entries").EnumerateArray(),
            r => r.GetProperty("source").GetString() == "Timer").GetProperty("durationMinutes").GetInt32());
        Assert.DoesNotContain(Host.Audit.Requests, r => r.Metadata?.GetValueOrDefault("transition") as string == "correct-captured-row");

        // Actually changed, and still not a 15-minute step up to 960: refused.
        var changed = await SaveFreshAsync(CurrentWeek, Row(Today, 1000, category: Category, source: "Timer"));
        Assert.Equal(TimeEntryReasonCodes.StepInvalid, changed.ReasonCode);
    }

    // ── G4 ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task One_persons_failure_does_not_stop_the_midnight_run_for_the_rest_of_the_tenant()
    {
        await Collection<Tenant>(PlatformCollections.Tenants).UpdateOneAsync(
            t => t.Id == Tenant, Builders<Tenant>.Update.Set(t => t.Status, TenantStatus.Active));
        // Two forgotten timers; the one whose close fails goes first.
        foreach (var user in new[] { SecondPerson, Person })
        {
            await Collection<TimerSegment>(PlatformCollections.TimeEntryTimerSegments).InsertOneAsync(new TimerSegment
            {
                TenantId = Tenant, UserId = user, CategoryCode = Category, IsRunning = true,
                StartedAtUtc = IstanbulLocal(2026, 10, 6, 23, 0), LocalDate = new DateOnly(2026, 10, 6), TimeZoneId = Zone,
                WeekKey = CurrentWeek, StartSource = TimerStartSource.TimerControl
            });
        }

        await EnableTimerAsync();
        Host.Notifier.ThrowFor.Add(SecondPerson);
        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 7, 3, 0);

        using (var scope = Host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<TimerMidnightCloseJob>()
                .HandleAsync(new TimerMidnightCloseJobArgs(500), new BackgroundJobContext());
        }

        var mine = Assert.Single(await SegmentsAsync());
        Assert.Equal(TimerStopReason.LocalMidnight, mine.StopReason);
        Assert.Contains(mine.Id, Host.Notifier.Segments);
    }

    // ── G5 ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Timer,Timer")]
    [InlineData("Manual,Plan")]
    [InlineData("1")]
    [InlineData("Manualx")]
    public async Task Only_a_declared_source_name_is_accepted(string source)
    {
        var result = await SaveFreshAsync(CurrentWeek, Row(Monday, 60, TaskA, source: source));

        Assert.Equal(HttpStatusCode.BadRequest, result.Status);
        Assert.Equal(TimeEntryReasonCodes.SourceInvalid, result.ReasonCode);
    }

    [Fact]
    public async Task Two_rows_for_one_cell_are_a_400_not_a_500()
    {
        var twice = await SaveFreshAsync(CurrentWeek, Row(Monday, 60, TaskA), Row(Monday, 30, TaskA, source: "Plan"));

        Assert.Equal(HttpStatusCode.BadRequest, twice.Status);
        Assert.Equal(TimeEntryReasonCodes.DuplicateRow, twice.ReasonCode);
    }

    // ── G6 ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Only_the_transition_that_really_took_the_task_away_ends_the_run()
    {
        await EnableTimerAsync();
        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 7, 10, 0);
        Ok(await StartTimerAsync(TaskA));

        var transitions = Collection<TaskTransition>(PlatformCollections.TaskTransitions);
        await transitions.InsertManyAsync(
        [
            // 11:00 and 12:00 — the person still holds it: a re-save that recorded the same assignee, a claim with no change.
            Transition(TaskTransitionKind.Reassigned, TaskLifecycle.InProgress, 11,
                new TaskFieldChange { Field = TaskFieldChangeCodes.Assignee, From = Person.ToString(), To = Person.ToString() }),
            Transition(TaskTransitionKind.Claimed, TaskLifecycle.InProgress, 12),
            // 15:00 — handed to someone else.
            Transition(TaskTransitionKind.Reassigned, TaskLifecycle.InProgress, 15,
                new TaskFieldChange { Field = TaskFieldChangeCodes.Assignee, From = Person.ToString(), To = SecondPerson.ToString() })
        ]);
        await Collection<TaskItem>(PlatformCollections.TaskItems).UpdateOneAsync(
            t => t.Id == TaskA, Builders<TaskItem>.Update.Set(t => t.AssigneeUserId, SecondPerson));

        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 7, 16, 0);
        Ok(await GetTimerAsync());

        var segment = Assert.Single(await SegmentsAsync());
        Assert.Equal(TimerStopReason.Reconcile, segment.StopReason);
        Assert.Equal(IstanbulLocal(2026, 10, 7, 15, 0), segment.StoppedAtUtc);
    }

    private TaskTransition Transition(TaskTransitionKind kind, TaskLifecycle to, int hour, params TaskFieldChange[] changes) => new()
    {
        TenantId = Tenant, TaskItemId = TaskA, Kind = kind, FromLifecycle = TaskLifecycle.InProgress, ToLifecycle = to,
        ActorUserId = Manager, CreatedAt = IstanbulLocal(2026, 10, 7, hour, 0), FieldChanges = [.. changes]
    };

    // ── G7 ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_midnight_close_audits_the_reason_it_actually_closed_with()
    {
        await EnableTimerAsync();
        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 6, 23, 30);
        Ok(await StartTimerAsync(category: Category));
        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 7, 0, 10);

        Ok(await GetTimerAsync());

        var audit = Assert.Single(Host.Audit.Requests, r => r.Metadata?.GetValueOrDefault("transition") as string == "close-at-local-midnight");
        Assert.Equal("LocalMidnight", audit.Metadata!["closedReason"]);
    }
}
