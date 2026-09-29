using System.Net;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Domain.Enums.TimeEntry;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// MOD-0280-FU01 T1a — the person's own week on the wire (pack §12, §13, §17 T-07/T-08/T-09/T-21/T-25): limits,
/// the edit window and reopen, future days, the tenant's local day across a DST change, the day target and concurrency.
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TimesheetWeekHttpMongoTests : TimeEntryScenario
{
    public TimesheetWeekHttpMongoTests(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    // ── The happy path, and that reading writes nothing inside the window ─────────────────────────────────────────

    [Fact]
    public async Task Reading_a_week_inside_the_window_creates_nothing_and_a_first_save_creates_revision_one()
    {
        var read = await GetWeekAsync();
        Assert.Equal(HttpStatusCode.OK, read.Status);
        Assert.Equal(JsonValueKindNull, read.Data.GetProperty("weekId").ValueKind);
        Assert.Equal(0, read.Data.GetProperty("version").GetInt32());
        Assert.True(read.Data.GetProperty("editable").GetBoolean());
        Assert.Empty(await StoredWeeksAsync());

        var saved = await SaveAsync(0, CurrentWeek, null, Row(Monday, 120, TaskA), Row(Monday, 60));

        Assert.Equal(HttpStatusCode.OK, saved.Status);
        var week = Assert.Single(await StoredWeeksAsync());
        Assert.Equal(1, week.RevisionNumber);
        Assert.Equal(TimesheetWeekStatus.Draft, week.Status);
        Assert.Equal(CurrentWeek, week.WeekKey);
        Assert.Equal(Monday, week.WeekStartDate);
        Assert.Equal(Person, week.UserId);
        var rows = await StoredEntriesAsync(week.Id);
        Assert.Equal(180, rows.Sum(r => r.DurationMinutes));
        Assert.All(rows, r => Assert.Equal(TimeEntrySource.Manual, r.Source));
    }

    [Fact]
    public async Task A_row_left_out_of_the_next_save_is_soft_deleted_never_hard_deleted()
    {
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 120, TaskA), Row(Monday, 60))).Status);
        var weekId = Assert.Single(await StoredWeeksAsync()).Id;

        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 90, TaskA))).Status);

        var all = await Collection<Domain.Entities.TimeEntry.TimeEntry>(
                Infrastructure.Persistence.Schema.PlatformCollections.TimeEntryEntries)
            .Find(e => e.TimesheetWeekId == weekId).ToListAsync();
        Assert.Equal(2, all.Count);
        Assert.Single(all, e => e.IsDeleted);
        Assert.Equal(90, Assert.Single(all, e => !e.IsDeleted).DurationMinutes);
    }

    // ── A3 limits and D5 step ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_day_above_660_minutes_is_saved_and_flagged_and_the_flag_reaches_the_submitted_week()
    {
        var saved = await SaveFreshAsync(CurrentWeek, Row(Monday, 480, TaskA), Row(Monday, 210, TaskB));

        Assert.Equal(HttpStatusCode.OK, saved.Status);
        Assert.Equal(["2026-10-05"], Dates(saved.Data.GetProperty("flaggedDates")));
        var monday = (await GetWeekAsync()).Data.GetProperty("days").EnumerateArray().First();
        Assert.True(monday.GetProperty("isFlagged").GetBoolean());
        Assert.Equal(690, monday.GetProperty("recordedMinutes").GetInt32());

        var submitted = await SubmitAsync();
        Assert.Equal(HttpStatusCode.OK, submitted.Status);
        Assert.Contains(Monday, (await StoredWeekAsync(submitted.Data.GetProperty("weekId").GetGuid())).FlaggedDates);
    }

    [Fact]
    public async Task A_day_above_960_minutes_is_400_TIME_ENTRY_DAY_IMPLAUSIBLE_and_nothing_is_written()
    {
        // Two rows, each valid on its own (600 and 480), together 1080 minutes on one day.
        var saved = await SaveAsync(0, CurrentWeek, null, Row(Monday, 600, TaskA), Row(Monday, 480, TaskB));

        Assert.Equal(HttpStatusCode.BadRequest, saved.Status);
        Assert.Equal(TimeEntryReasonCodes.DayImplausible, saved.ReasonCode);
        Assert.Empty(await StoredWeeksAsync());
    }

    [Fact]
    public async Task A_20_minute_row_is_400_TIME_ENTRY_STEP_INVALID()
    {
        var saved = await SaveAsync(0, CurrentWeek, null, Row(Monday, 20, TaskA));

        Assert.Equal(HttpStatusCode.BadRequest, saved.Status);
        Assert.Equal(TimeEntryReasonCodes.StepInvalid, saved.ReasonCode);
        Assert.Empty(await StoredWeeksAsync());
    }

    [Fact]
    public async Task A_future_day_is_400_TIME_ENTRY_FUTURE_DATE()
    {
        // Wednesday is today; Thursday is tomorrow.
        var saved = await SaveAsync(0, CurrentWeek, null, Row(Monday.AddDays(3), 60, TaskA));

        Assert.Equal(HttpStatusCode.BadRequest, saved.Status);
        Assert.Equal(TimeEntryReasonCodes.FutureDate, saved.ReasonCode);
        Assert.Empty(await StoredWeeksAsync());
    }

    [Fact]
    public async Task An_unknown_task_or_an_inactive_category_is_refused()
    {
        await SeedCategoryAsync(Tenant, "RETIRED", active: false);

        var unknownTask = await SaveAsync(0, CurrentWeek, null, Row(Monday, 60, Guid.NewGuid()));
        var inactive = await SaveAsync(0, CurrentWeek, null, Row(Monday, 60, category: "RETIRED"));

        Assert.Equal(TimeEntryReasonCodes.TargetInvalid, unknownTask.ReasonCode);
        Assert.Equal(TimeEntryReasonCodes.CategoryInactive, inactive.ReasonCode);
        Assert.Empty(await StoredWeeksAsync());
    }

    [Fact]
    public async Task An_empty_week_cannot_be_submitted()
    {
        var never = await SubmitAsync();
        Assert.Equal(HttpStatusCode.Conflict, never.Status);
        Assert.Equal(TimeEntryReasonCodes.EmptyWeek, never.ReasonCode);

        // A week that had a row and lost it is empty too.
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 60, TaskA))).Status);
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek)).Status);
        var emptied = await SubmitAsync();
        Assert.Equal(HttpStatusCode.Conflict, emptied.Status);
        Assert.Equal(TimeEntryReasonCodes.EmptyWeek, emptied.ReasonCode);
    }

    // ── Concurrency ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Two_saves_from_the_same_version_the_second_is_409_and_writes_nothing()
    {
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 60, TaskA))).Status);
        var version = await VersionAsync();

        var first = await SaveAsync(version, CurrentWeek, null, Row(Monday, 90, TaskA));
        var second = await SaveAsync(version, CurrentWeek, null, Row(Monday, 240, TaskA));

        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Equal(HttpStatusCode.Conflict, second.Status);
        Assert.Equal(TimeEntryReasonCodes.ConcurrencyConflict, second.ReasonCode);
        Assert.Equal(90, Assert.Single(await StoredEntriesAsync(Assert.Single(await StoredWeeksAsync()).Id)).DurationMinutes);
    }

    // ── A9 edit window and the time-admin reopen ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Four_weeks_back_is_editable_five_weeks_back_is_409_until_a_time_admin_reopens_it_with_a_reason()
    {
        const string fourBack = "2026-W37";
        const string fiveBack = "2026-W36";

        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(0, fourBack, null, Row(new DateOnly(2026, 9, 7), 60, TaskA))).Status);

        var old = await GetWeekAsync(fiveBack);
        Assert.False(old.Data.GetProperty("editable").GetBoolean());
        Assert.Equal(TimeEntryReasonCodes.WeekOutsideEditWindow, old.Data.GetProperty("notEditableReason").GetString());

        var refused = await SaveAsync(0, fiveBack, null, Row(new DateOnly(2026, 8, 31), 60, TaskA));
        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        Assert.Equal(TimeEntryReasonCodes.WeekOutsideEditWindow, refused.ReasonCode);

        var noReason = await ReopenAsync(Person, fiveBack, 0, "  ");
        Assert.Equal(HttpStatusCode.BadRequest, noReason.Status);
        Assert.Equal(TimeEntryReasonCodes.ReopenReasonRequired, noReason.ReasonCode);

        // The week was never written: the reopen creates its Draft revision itself.
        var reopened = await ReopenAsync(Person, fiveBack, 0, "Forgot to enter the audit week");
        Assert.Equal(HttpStatusCode.OK, reopened.Status);
        var weekId = reopened.Data.GetProperty("weekId").GetGuid();

        var accepted = await SaveAsync(await VersionAsync(fiveBack), fiveBack, null, Row(new DateOnly(2026, 8, 31), 60, TaskA));
        Assert.Equal(HttpStatusCode.OK, accepted.Status);
        var stored = await StoredWeekAsync(weekId);
        Assert.True(stored.ReopenActive);
        Assert.Equal(PoolAdmin, stored.ReopenedByUserId);
        Assert.Equal("Forgot to enter the audit week", stored.ReopenReason);
    }

    [Fact]
    public async Task The_reopen_audit_entry_names_the_week_it_changed_created_or_existing()
    {
        // A week that has no revision yet: the reopen creates it, and the audit entry carries the created id.
        var created = await ReopenAsync(Person, "2026-W36", 0, "late entry");
        var createdId = created.Data.GetProperty("weekId").GetGuid();

        // A week that already has an open Draft outside the window (staged directly — reading never creates one).
        var existing = new Domain.Entities.TimeEntry.TimesheetWeek
        {
            TenantId = Tenant, UserId = Person, WeekKey = "2026-W35", WeekStartDate = new DateOnly(2026, 8, 24),
            TimeZoneId = Zone, RevisionNumber = 1
        };
        await Collection<Domain.Entities.TimeEntry.TimesheetWeek>(
            Infrastructure.Persistence.Schema.PlatformCollections.TimeEntryTimesheetWeeks).InsertOneAsync(existing);
        Assert.Equal(HttpStatusCode.OK, (await ReopenAsync(Person, "2026-W35", existing.Version, "late entry")).Status);

        var audited = Host.Audit.Requests.Where(r => r.RequestType == "ReopenTimesheetWeekCommand").ToList();
        Assert.Equal(2, audited.Count);
        Assert.Equal(createdId, audited[0].EntityId);
        Assert.Equal(existing.Id, audited[1].EntityId);
    }

    [Fact]
    public async Task Reading_a_week_never_writes_not_even_an_old_one_outside_the_window()
    {
        var old = await GetWeekAsync("2026-W30");
        var current = await GetWeekAsync();

        Assert.Equal(HttpStatusCode.OK, old.Status);
        Assert.Equal(JsonValueKindNull, old.Data.GetProperty("weekId").ValueKind);
        Assert.Equal(HttpStatusCode.OK, current.Status);
        Assert.Empty(await StoredWeeksAsync());
    }

    [Fact]
    public async Task A_week_inside_the_window_needs_no_reopen()
    {
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 60, TaskA))).Status);
        var week = Assert.Single(await StoredWeeksAsync());

        var reopen = await ReopenAsync(Person, CurrentWeek, week.Version, "not needed");

        Assert.Equal(HttpStatusCode.Conflict, reopen.Status);
        Assert.Equal(TimeEntryReasonCodes.ReopenNotNeeded, reopen.ReasonCode);
    }

    [Fact]
    public async Task A_time_admin_cannot_reopen_their_own_week_and_an_unknown_person_is_404()
    {
        var own = await ReopenAsync(PoolAdmin, "2026-W30", 0, "my own");
        var nobody = await ReopenAsync(Guid.NewGuid(), "2026-W30", 0, "who?");

        Assert.Equal(HttpStatusCode.Forbidden, own.Status);
        Assert.Equal(TimeEntryReasonCodes.ReopenOwnWeek, own.ReasonCode);
        Assert.Equal(HttpStatusCode.NotFound, nobody.Status);
        Assert.Equal(TimeEntryReasonCodes.PersonNotFound, nobody.ReasonCode);
        Assert.Empty(await StoredWeeksAsync());
    }

    [Fact]
    public async Task A_reopen_lasts_until_approval_so_a_rejected_reopened_week_can_still_be_fixed()
    {
        const string old = "2026-W36";
        var sundayOld = new DateOnly(2026, 9, 6);
        Assert.Equal(HttpStatusCode.OK, (await ReopenAsync(Person, old, 0, "late entry")).Status);
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(await VersionAsync(old), old, null, Row(sundayOld, 60, TaskA))).Status);
        var submitted = await SubmitAsync(old);
        Assert.Equal(HttpStatusCode.OK, submitted.Status);
        var weekId = submitted.Data.GetProperty("weekId").GetGuid();
        Assert.True((await StoredWeekAsync(weekId)).ReopenActive);

        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(Manager, weekId, approve: false, comment: "Sunday?")).Status);

        var read = await GetWeekAsync(old);
        Assert.Equal("Draft", read.Data.GetProperty("status").GetString());
        Assert.True(read.Data.GetProperty("editable").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(await VersionAsync(old), old, null, Row(sundayOld, 90, TaskA))).Status);

        // …and approval ends it.
        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(old)).Status);
        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(Manager, weekId, approve: true)).Status);
        await GetWeekAsync(old);
        Assert.False((await StoredWeekAsync(weekId)).ReopenActive);
    }

    // ── R3 / T-07 / T-08 — the tenant's local day, ISO weeks, DST ───────────────────────────────────────────────

    [Fact]
    public async Task Istanbul_late_Sunday_UTC_is_already_Monday_so_Monday_is_not_a_future_day()
    {
        // 2026-10-04 22:30Z = Monday 2026-10-05 01:30 in Istanbul (UTC+3). The UTC date is still Sunday the 4th:
        // a day computed in UTC would refuse Monday as "future" and put today in 2026-W40.
        Host.Clock.UtcNow = new DateTimeOffset(2026, 10, 4, 22, 30, 0, TimeSpan.Zero);

        var saved = await SaveAsync(0, CurrentWeek, null, Row(Monday, 60, TaskA));

        Assert.Equal(HttpStatusCode.OK, saved.Status);
        var week = await GetWeekAsync();
        Assert.Equal("2026-10-05", week.Data.GetProperty("localToday").GetString());
        // The window is counted from the LOCAL week (W41): W36 is five weeks back, outside.
        Assert.False((await GetWeekAsync("2026-W36")).Data.GetProperty("insideEditWindow").GetBoolean());
        Assert.True((await GetWeekAsync("2026-W37")).Data.GetProperty("insideEditWindow").GetBoolean());
    }

    [Fact]
    public async Task Berlin_on_the_night_the_clocks_go_back_the_local_day_follows_the_zone_not_a_fixed_offset()
    {
        await SetTenantZoneAsync("Europe/Berlin");
        var sunday = new DateOnly(2026, 10, 25);   // 25 hours long in Berlin: CEST → CET at 03:00
        var nextMonday = new DateOnly(2026, 10, 26);

        // 22:30Z = 23:30 CET, still Sunday. Summer time (+2) would call it Monday 00:30 and accept Monday.
        Host.Clock.UtcNow = new DateTimeOffset(2026, 10, 25, 22, 30, 0, TimeSpan.Zero);
        var tooEarly = await SaveAsync(0, "2026-W44", null, Row(nextMonday, 60, TaskA));
        Assert.Equal(HttpStatusCode.BadRequest, tooEarly.Status);
        Assert.Equal(TimeEntryReasonCodes.FutureDate, tooEarly.ReasonCode);

        // The DST Sunday itself belongs to 2026-W43 and takes a row.
        var dstDay = await SaveAsync(0, "2026-W43", null, Row(sunday, 240, TaskA));
        Assert.Equal(HttpStatusCode.OK, dstDay.Status);
        Assert.Equal("2026-W43", Assert.Single(await StoredWeeksAsync()).WeekKey);

        // 23:30Z = Monday 00:30 CET: now Monday is today, in 2026-W44.
        Host.Clock.UtcNow = new DateTimeOffset(2026, 10, 25, 23, 30, 0, TimeSpan.Zero);
        var monday = await SaveAsync(0, "2026-W44", null, Row(nextMonday, 60, TaskA));
        Assert.Equal(HttpStatusCode.OK, monday.Status);
        Assert.Equal("2026-10-26", (await GetWeekAsync("2026-W44")).Data.GetProperty("localToday").GetString());
    }

    [Fact]
    public async Task A_date_outside_the_week_key_is_refused()
    {
        var saved = await SaveAsync(0, CurrentWeek, null, Row(Monday.AddDays(-1), 60, TaskA));

        Assert.Equal(HttpStatusCode.BadRequest, saved.Status);
        Assert.Equal(TimeEntryReasonCodes.DateOutsideWeek, saved.ReasonCode);
    }

    [Fact]
    public async Task A_malformed_or_nonexistent_week_key_is_400()
    {
        Assert.Equal(TimeEntryReasonCodes.WeekKeyInvalid, (await GetWeekAsync("2026-40")).ReasonCode);
        Assert.Equal(TimeEntryReasonCodes.WeekKeyInvalid, (await GetWeekAsync("2027-W53")).ReasonCode); // 2027 has 52
    }

    // ── D13 / T-21 — the day target ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_day_target_is_the_tenant_target_a_half_day_holiday_is_half_of_it_and_a_weekend_is_zero()
    {
        Host.Calendar.HalfDays.Add(Monday.AddDays(1));   // Tuesday: half-day holiday (arife)
        Host.Calendar.Holidays.Add(Monday.AddDays(2));   // Wednesday: full holiday

        var days = (await GetWeekAsync()).Data.GetProperty("days").EnumerateArray().ToList();

        Assert.Equal(480, days[0].GetProperty("targetMinutes").GetInt32());
        Assert.Equal(240, days[1].GetProperty("targetMinutes").GetInt32());
        Assert.True(days[1].GetProperty("isHalfDay").GetBoolean());
        Assert.Equal("workingDay", days[1].GetProperty("dayKind").GetString());
        Assert.Equal(0, days[2].GetProperty("targetMinutes").GetInt32());
        Assert.Equal("holiday", days[2].GetProperty("dayKind").GetString());
        Assert.Equal(0, days[5].GetProperty("targetMinutes").GetInt32());
        Assert.Equal(0, days[6].GetProperty("targetMinutes").GetInt32());
    }

    private const System.Text.Json.JsonValueKind JsonValueKindNull = System.Text.Json.JsonValueKind.Null;
}
