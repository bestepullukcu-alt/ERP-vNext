using System.Net;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Features.TimeEntry.BackgroundJobs;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// MOD-0280-FU01 T1b — what happens to timer time after it is counted: the local-midnight cut with the jobs OFF and
/// with the job ON (D3, T-06), the DST nights of Berlin and Istanbul (T-07), minutes outside the working window (D3), the
/// A2 rounding and 8-minute floor (T-27), time that lands on a locked week (§13) and minimisation at approval (D4, T-23).
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TimerMidnightAndDraftHttpMongoTests : TimerScenario
{
    public TimerMidnightAndDraftHttpMongoTests(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    // ── D3 / T-06 — midnight ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_timer_left_running_is_closed_at_local_midnight_by_the_first_read_with_the_jobs_off()
    {
        await EnableTimerAsync();
        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 6, 23, 30);
        Ok(await StartTimerAsync(category: Category));

        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 7, 0, 10);
        var timer = await GetTimerAsync();
        Ok(timer);

        Assert.Equal(System.Text.Json.JsonValueKind.Null, timer.Data.GetProperty("running").ValueKind);
        var segment = Assert.Single(await SegmentsAsync());
        Assert.Equal(TimerStopReason.LocalMidnight, segment.StopReason);
        Assert.Equal(IstanbulLocal(2026, 10, 7, 0, 0), segment.StoppedAtUtc);   // 00:00 local, not the read's 00:10
        Assert.Equal(30 * 60, segment.DurationSeconds);
        Assert.Equal(new DateOnly(2026, 10, 6), segment.LocalDate);
        Assert.Equal(30, segment.OutsideWorkingMinutes);                          // 23:30–00:00 is outside 09–18
        Assert.Empty(Host.Notifier.Segments);                                     // a read never notifies; the banner does

        var banner = Assert.Single(timer.Data.GetProperty("closedAtMidnightYesterday").EnumerateArray());
        Assert.Equal(segment.Id, banner.GetProperty("segmentId").GetGuid());

        var row = Assert.Single((await GetWeekAsync()).Data.GetProperty("entries").EnumerateArray());
        Assert.Equal("2026-10-06", row.GetProperty("localDate").GetString());
        Assert.Equal(30, row.GetProperty("durationMinutes").GetInt32());
        Assert.Equal(30, row.GetProperty("outsideWorkingMinutes").GetInt32());
    }

    [Fact]
    public async Task The_midnight_job_closes_at_midnight_and_sends_exactly_one_notification()
    {
        await EnableTimerAsync();
        await Collection<Tenant>(PlatformCollections.Tenants).UpdateOneAsync(
            t => t.Id == Tenant, Builders<Tenant>.Update.Set(t => t.Status, TenantStatus.Active));
        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 6, 23, 30);
        Ok(await StartTimerAsync(category: Category));

        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 7, 3, 0);
        await RunMidnightJobAsync();
        await RunMidnightJobAsync();
        Ok(await GetTimerAsync());

        var segment = Assert.Single(await SegmentsAsync());
        Assert.Equal(TimerStopReason.LocalMidnight, segment.StopReason);
        Assert.Equal(IstanbulLocal(2026, 10, 7, 0, 0), segment.StoppedAtUtc);
        Assert.Single(Host.Notifier.Segments, id => id == segment.Id);
        Assert.NotNull(segment.AutoCloseNotifiedAtUtc);
    }

    private async Task RunMidnightJobAsync()
    {
        using var scope = Host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TimerMidnightCloseJob>()
            .HandleAsync(new TimerMidnightCloseJobArgs(500), new BackgroundJobContext(TriggerType: BackgroundJobTriggerTypes.Recurring));
    }

    [Fact]
    public async Task A_local_day_is_the_tenant_zones_not_the_utc_date()
    {
        await EnableTimerAsync();
        Host.Clock.UtcNow = IstanbulLocal(2026, 10, 7, 1, 0); // 2026-10-06 22:00 UTC
        Ok(await StartTimerAsync(category: Category));

        Assert.Equal(new DateOnly(2026, 10, 7), Assert.Single(await SegmentsAsync()).LocalDate);
    }

    // ── T-07 — DST nights ───────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Europe/Berlin", 240)]   // 00:30 → 03:30 on the night the clocks go back: 4 real hours
    [InlineData("Europe/Istanbul", 180)] // no DST since 2016: 3 hours
    public async Task A_run_through_the_October_clock_change_counts_real_minutes_on_the_local_day(string zone, int minutes)
    {
        await SetTenantZoneAsync(zone);
        await EnableTimerAsync();
        var tz = TimeZoneInfo.FindSystemTimeZoneById(zone);
        Host.Clock.UtcNow = Local(tz, 2026, 10, 25, 0, 30);
        Ok(await StartTimerAsync(category: Category));
        Host.Clock.UtcNow = Local(tz, 2026, 10, 25, 3, 30, afterTransition: true);
        Ok(await StopTimerAsync());

        var segment = Assert.Single(await SegmentsAsync());
        Assert.Equal(minutes * 60, segment.DurationSeconds);
        Assert.Equal(new DateOnly(2026, 10, 25), segment.LocalDate);
        Assert.Equal("2026-W43", segment.WeekKey);

        var row = Assert.Single((await GetWeekAsync("2026-W43")).Data.GetProperty("entries").EnumerateArray());
        Assert.Equal(minutes, row.GetProperty("durationMinutes").GetInt32());
    }

    [Theory]
    [InlineData("Europe/Berlin")]
    [InlineData("Europe/Istanbul")]
    public async Task A_sunday_night_run_is_cut_at_monday_00_00_local_and_stays_in_its_week(string zone)
    {
        await SetTenantZoneAsync(zone);
        await EnableTimerAsync();
        var tz = TimeZoneInfo.FindSystemTimeZoneById(zone);
        Host.Clock.UtcNow = Local(tz, 2026, 10, 25, 23, 30);
        Ok(await StartTimerAsync(category: Category));

        Host.Clock.UtcNow = Local(tz, 2026, 10, 26, 0, 10);
        Ok(await GetTimerAsync());

        var segment = Assert.Single(await SegmentsAsync());
        Assert.Equal(TimerStopReason.LocalMidnight, segment.StopReason);
        Assert.Equal(Local(tz, 2026, 10, 26, 0, 0), segment.StoppedAtUtc);
        Assert.Equal(30 * 60, segment.DurationSeconds);
        Assert.Equal("2026-W43", segment.WeekKey);
    }

    private static DateTimeOffset Local(TimeZoneInfo zone, int y, int mo, int d, int h, int mi, bool afterTransition = false)
    {
        var local = new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Unspecified);
        var offsets = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local) : [zone.GetUtcOffset(local)];
        var offset = afterTransition ? offsets.Min() : offsets.Max();
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }

    // ── T-27 — A2 rounding and the 8-minute floor ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(new[] { 180, 240 }, 0)]     // 3 + 4 min = 7 → too short, no row
    [InlineData(new[] { 420, 480 }, 15)]    // 7 + 8 min = 15 → 15
    [InlineData(new[] { 1350 }, 30)]        // 22.5 min → 30 (ties up)
    [InlineData(new[] { 1349 }, 15)]        // 22 min 29 s → 15
    [InlineData(new[] { 480 }, 15)]         // exactly 8 min → counts
    [InlineData(new[] { 479 }, 0)]          // a second short → does not
    public async Task Timer_time_is_summed_per_day_and_target_and_rounded_to_15_with_an_8_minute_floor(int[] seconds, int expected)
    {
        await EnableTimerAsync();
        var today = new DateOnly(2026, 10, 7);
        foreach (var s in seconds)
        {
            await SeedClosedSegmentAsync(today, s);
        }

        // A zero-length run on the same day and target closes a segment, and every close recomputes that day's draft.
        Ok(await StartTimerAsync(category: Category));
        Ok(await StopTimerAsync());

        var week = (await GetWeekAsync()).Data;
        var rows = week.GetProperty("entries").EnumerateArray().Where(r => r.GetProperty("source").GetString() == "Timer").ToList();
        var tooShort = week.GetProperty("tooShortToCount").EnumerateArray().ToList();
        if (expected == 0)
        {
            Assert.Empty(rows);
            Assert.Equal(seconds.Sum(), Assert.Single(tooShort).GetProperty("seconds").GetInt32());
        }
        else
        {
            Assert.Equal(expected, Assert.Single(rows).GetProperty("durationMinutes").GetInt32());
            Assert.Empty(tooShort);
        }
    }

    // ── §13 — timer time on a locked week ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Timer_time_on_a_submitted_week_is_kept_and_reported_never_added_to_the_locked_revision()
    {
        await EnableTimerAsync();
        var weekId = await SubmittedWeekAsync(Row(Monday, 120, TaskA));
        var rowsBefore = (await StoredEntriesAsync(weekId)).Count;

        Ok(await StartTimerAsync(category: Category));
        Host.Clock.UtcNow = Wednesday.AddMinutes(40);
        Ok(await StopTimerAsync());

        Assert.Equal(rowsBefore, (await StoredEntriesAsync(weekId)).Count);
        Assert.False(Assert.Single(await SegmentsAsync()).IsRunning);
        var outside = Assert.Single((await GetWeekAsync()).Data.GetProperty("timerOutsideOpenWeek").EnumerateArray());
        Assert.Equal(45, outside.GetProperty("minutes").GetInt32());
    }

    [Fact]
    public async Task A_timer_draft_over_16_hours_is_written_but_blocks_submit()
    {
        await EnableTimerAsync();
        await SeedClosedSegmentAsync(new DateOnly(2026, 10, 7), 17 * 3600);
        Ok(await StartTimerAsync(category: Category));
        Ok(await StopTimerAsync());

        var row = Assert.Single((await GetWeekAsync()).Data.GetProperty("entries").EnumerateArray());
        Assert.Equal(17 * 60, row.GetProperty("durationMinutes").GetInt32());
        var submit = await SubmitAsync();
        Assert.Equal(HttpStatusCode.Conflict, submit.Status); // T1a's submit answer for an implausible day
        Assert.Equal(Application.Features.TimeEntry.TimeEntryReasonCodes.DayImplausible, submit.ReasonCode);
    }

    // ── D4 / T-23 — minimisation at approval ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Approval_clears_the_weeks_segment_instants_keeps_every_row_and_duration_and_audits_it_once()
    {
        await EnableTimerAsync();
        Ok(await StartTimerAsync(TaskA));
        Host.Clock.UtcNow = Wednesday.AddMinutes(60);
        Ok(await StopTimerAsync());
        Ok(await StartTimerAsync(category: Category));
        Host.Clock.UtcNow = Wednesday.AddMinutes(90);
        Ok(await StopTimerAsync());
        var before = await SegmentsAsync();
        Assert.Equal(2, before.Count);

        var submitted = await SubmitAsync();
        Ok(submitted);
        var weekId = submitted.Data.GetProperty("weekId").GetGuid();
        Ok(await DecideAsync(Manager, weekId, approve: true));
        Ok(await GetWeekAsync()); // the read takes the decision on board (and minimises)
        Ok(await GetWeekAsync()); // a replayed read changes nothing

        var after = await SegmentsAsync();
        Assert.Equal(before.Count, after.Count);                                        // nothing deleted
        Assert.All(after, s =>
        {
            Assert.Null(s.StartedAtUtc);
            Assert.Null(s.StoppedAtUtc);
            Assert.NotNull(s.MinimisedAtUtc);
            Assert.False(s.IsDeleted);
        });
        Assert.Equal(before.Select(s => s.DurationSeconds).Order(), after.Select(s => s.DurationSeconds).Order());
        Assert.Equal(before.Select(s => s.OutsideWorkingMinutes).Order(), after.Select(s => s.OutsideWorkingMinutes).Order());
        Assert.Single(Host.Audit.Requests, r => r.EntityType == "TimerSegment" && r.Operation == AuditOperation.Redact);
        Assert.Equal(60, await ApprovedMinutesAsync(TaskA));
    }
}
