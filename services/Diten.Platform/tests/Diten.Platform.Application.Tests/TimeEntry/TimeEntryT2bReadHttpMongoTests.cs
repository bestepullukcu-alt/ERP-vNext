using System.Net;
using System.Text.Json;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// MOD-0280-FU01 T2b — the read fields the approvals page, the approver's week and My Timesheet's side panel need, on
/// the wire against the disposable mongod:
/// <list type="bullet">
/// <item>U4 — the OPEN MOD-0023 approval task of the week (the Task Center's work-item id, so the page approves on the
/// SAME action path) and the marks a bulk approval must not skip (auto-closed timer, outside hours, holiday).</item>
/// <item>U5 — a correction's row-by-row difference from the in-force approved revision.</item>
/// <item>E1 — a day the working calendar could not resolve says so. E4 — the caller's OWN un-minimised timer segments.</item>
/// </list>
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TimeEntryT2bReadHttpMongoTests : TimerScenario
{
    public TimeEntryT2bReadHttpMongoTests(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    private Task<ApiResult> ApprovalListAsync(Guid? approver = null)
        => Host.GetAsync("/api/v1/time-entry/approvals", ApproverToken(approver ?? Manager));

    private Task<ApiResult> ApprovalWeekAsync(Guid weekId, Guid? approver = null)
        => Host.GetAsync($"/api/v1/time-entry/approvals/{weekId}", ApproverToken(approver ?? Manager));

    private async Task<ApprovalTask> OpenApprovalTaskAsync(Guid weekId)
    {
        var week = await StoredWeekAsync(weekId);
        return await Collection<ApprovalTask>(PlatformCollections.ApprovalTasks)
            .Find(t => t.WorkflowInstanceId == week.WorkflowInstanceId && t.Status == ApprovalTaskStatus.WaitingApproval)
            .SingleAsync();
    }

    private static List<string> Dates(JsonElement element, string property)
        => element.GetProperty(property).EnumerateArray().Select(d => d.GetString()!).ToList();

    // ── U4 — the approval task is the Task Center's work item ────────────────────────────────────────────────────

    [Fact]
    public async Task List_and_week_name_the_open_MOD_0023_approval_task_the_Task_Center_acts_on()
    {
        var weekId = await SubmittedWeekAsync();
        var task = await OpenApprovalTaskAsync(weekId);

        var list = await ApprovalListAsync();
        var row = Assert.Single(list.Data.GetProperty("items").EnumerateArray());
        Assert.Equal(task.Id, row.GetProperty("approvalTaskId").GetGuid());
        Assert.Equal(task.Version, row.GetProperty("approvalTaskVersion").GetInt32());

        var week = await ApprovalWeekAsync(weekId);
        Assert.Equal(HttpStatusCode.OK, week.Status);
        Assert.Equal(task.Id, week.Data.GetProperty("approvalTaskId").GetGuid());
    }

    [Fact]
    public async Task The_marks_a_bulk_approval_must_not_skip_are_on_the_row_and_the_week()
    {
        Host.Calendar.Holidays.Add(Monday.AddDays(1));                           // Tuesday is a holiday
        var weekId = await SubmittedWeekAsync(Row(Monday, 120, TaskA), Row(Monday.AddDays(1), 60));
        // A forgotten timer, cut at local midnight (staged after the submit so no draft recompute moves the week).
        await SeedClosedSegmentAsync(Monday, 1800, task: TaskB);
        var cut = (await SegmentsAsync()).Single();
        await RawSetAsync(PlatformCollections.TimeEntryTimerSegments, cut.Id, ("StopReason", new BsonInt32(3)));
        var monday = (await StoredEntriesAsync(weekId)).Single(e => e.LocalDate == Monday);
        await RawSetAsync(PlatformCollections.TimeEntryEntries, monday.Id, ("OutsideWorkingMinutes", new BsonInt32(45)));

        var row = Assert.Single((await ApprovalListAsync()).Data.GetProperty("items").EnumerateArray());
        Assert.Equal(["2026-10-05"], Dates(row, "autoClosedDates"));
        Assert.Equal(["2026-10-06"], Dates(row, "holidayDates"));
        Assert.Equal(45, row.GetProperty("outsideWorkingMinutes").GetInt32());

        var week = (await ApprovalWeekAsync(weekId)).Data;
        Assert.Equal(["2026-10-05"], Dates(week, "autoClosedDates"));
        Assert.Equal(["2026-10-06"], Dates(week, "holidayDates"));
        Assert.Equal(45, week.GetProperty("outsideWorkingMinutes").GetInt32());
    }

    [Fact]
    public async Task An_unmarked_week_carries_no_marks()
    {
        await SubmittedWeekAsync();

        var row = Assert.Single((await ApprovalListAsync()).Data.GetProperty("items").EnumerateArray());
        Assert.Empty(Dates(row, "autoClosedDates"));
        Assert.Empty(Dates(row, "holidayDates"));
        Assert.Empty(Dates(row, "flaggedDates"));
        Assert.Equal(0, row.GetProperty("outsideWorkingMinutes").GetInt32());
    }

    // ── U5 — what a correction changes ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_correction_shows_row_by_row_what_it_changes_against_the_revision_in_force()
    {
        // CT acceptance (T2b): Tuesday's task-B row is the SAME on both sides — without it, listing unchanged rows went
        // unmeasured (the clock is Wednesday, so no later day can hold time).
        var originalId = await SubmittedWeekAsync(Row(Monday, 120, TaskA), Row(Monday.AddDays(1), 60), Row(Monday.AddDays(1), 45, TaskB));
        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(Manager, originalId, approve: true)).Status);
        await GetWeekAsync();
        var opened = await Host.PostAsync($"/api/v1/time-entry/weeks/{CurrentWeek}/corrections", PersonToken(),
            new { reason = "Monday was three hours; Wednesday was on task B" });
        var correctionId = opened.Data.GetProperty("weekId").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek,
            Row(Monday, 180, TaskA), Row(Monday.AddDays(2), 30, TaskB), Row(Monday.AddDays(1), 45, TaskB))).Status);
        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync()).Status);

        var week = await ApprovalWeekAsync(correctionId);

        Assert.Equal(HttpStatusCode.OK, week.Status);
        Assert.Equal(1, week.Data.GetProperty("inForceRevisionNumber").GetInt32());
        var changes = week.Data.GetProperty("correctionChanges").EnumerateArray()
            .Select(c => (
                Date: c.GetProperty("localDate").GetString(),
                Target: c.GetProperty("taskItemId").ValueKind == JsonValueKind.Null ? c.GetProperty("categoryCode").GetString() : c.GetProperty("taskItemId").GetString(),
                Was: c.GetProperty("previousMinutes").ValueKind == JsonValueKind.Null ? (int?)null : c.GetProperty("previousMinutes").GetInt32(),
                Now: c.GetProperty("currentMinutes").ValueKind == JsonValueKind.Null ? (int?)null : c.GetProperty("currentMinutes").GetInt32()))
            .ToList();
        Assert.Equal(3, changes.Count);
        Assert.Contains(("2026-10-05", TaskA.ToString(), (int?)120, (int?)180), changes);   // changed
        Assert.Contains(("2026-10-06", Category, (int?)60, (int?)null), changes);           // removed
        Assert.Contains(("2026-10-07", TaskB.ToString(), (int?)null, (int?)30), changes);   // added
        Assert.DoesNotContain(changes, c => c.Date == "2026-10-06" && c.Target == TaskB.ToString()); // unchanged ⇒ not listed
    }

    [Fact]
    public async Task A_first_submission_has_no_revision_in_force_and_no_changes()
    {
        var weekId = await SubmittedWeekAsync();

        var week = (await ApprovalWeekAsync(weekId)).Data;

        Assert.Equal(JsonValueKind.Null, week.GetProperty("inForceRevisionNumber").ValueKind);
        Assert.Empty(week.GetProperty("correctionChanges").EnumerateArray());
    }

    // ── E1 — an unresolved calendar day says so ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_day_the_working_calendar_cannot_resolve_is_flagged_on_the_week()
    {
        Host.Calendar.Unresolved.Add(Monday.AddDays(2));

        var days = (await GetWeekAsync()).Data.GetProperty("days").EnumerateArray().ToList();

        Assert.True(days[2].GetProperty("calendarUnresolved").GetBoolean());
        Assert.False(days[0].GetProperty("calendarUnresolved").GetBoolean());
    }

    // ── E4 — the caller's own segment breakdown ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_week_lists_only_the_callers_own_segments_whose_instants_are_still_there()
    {
        await SeedClosedSegmentAsync(Monday, 1500, task: TaskA);
        var own = (await SegmentsAsync()).Single();
        await SeedClosedSegmentAsync(Monday, 900, task: TaskA);
        var minimised = (await SegmentsAsync()).Single(s => s.Id != own.Id);
        await RawSetAsync(PlatformCollections.TimeEntryTimerSegments, minimised.Id,
            ("MinimisedAtUtc", BsonNull.Value), ("StartedAtUtc", BsonNull.Value), ("StoppedAtUtc", BsonNull.Value));
        await Collection<TimerSegment>(PlatformCollections.TimeEntryTimerSegments).InsertOneAsync(new TimerSegment
        {
            TenantId = Tenant, UserId = SecondPerson, TaskItemId = TaskA, IsRunning = false,
            StartedAtUtc = Wednesday.AddHours(-2), StoppedAtUtc = Wednesday.AddHours(-1), DurationSeconds = 3600,
            LocalDate = Monday, TimeZoneId = Zone, WeekKey = CurrentWeek,
            StartSource = Domain.Enums.TimeEntry.TimerStartSource.TimerControl,
            StopReason = Domain.Enums.TimeEntry.TimerStopReason.TimerControl
        });

        var segments = (await GetWeekAsync()).Data.GetProperty("timerSegments").EnumerateArray().ToList();

        var only = Assert.Single(segments);
        Assert.Equal(own.Id, only.GetProperty("segmentId").GetGuid());
        Assert.Equal(1500, only.GetProperty("durationSeconds").GetInt32());
        Assert.Equal("TimerControl", only.GetProperty("stopReason").GetString());
    }

    // ── Server-mode list protocol (the approvals page's createList) ─────────────────────────────────────────────

    [Fact]
    public async Task The_list_speaks_the_server_mode_protocol_search_order_and_filtered_total()
    {
        Host.Names.Names[GrandManager] = "Zeynep Arslan";
        await SubmittedWeekAsync();                                                       // Ayşe Yılmaz → Mehmet
        await SeatAsync(SecondPerson, PersonSeat);
        Host.Names.Names[SecondPerson] = "Burak Demir";
        await SaveAsync(await VersionAsync(CurrentWeek, PersonToken(person: SecondPerson)), CurrentWeek, PersonToken(person: SecondPerson), Row(Monday, 300, category: Category));
        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync(CurrentWeek, PersonToken(person: SecondPerson))).Status);

        var all = await Host.GetAsync("/api/v1/time-entry/approvals?start=0&length=10&orderBy=displayName&orderDir=asc", ApproverToken(Manager));
        Assert.Equal(HttpStatusCode.OK, all.Status);
        Assert.Equal(["Ayşe Yılmaz", "Burak Demir"],
            all.Data.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("displayName").GetString()).ToList());
        Assert.Equal(2, all.Data.GetProperty("filteredTotal").GetInt32());

        var searched = await Host.GetAsync("/api/v1/time-entry/approvals?start=0&length=10&search=burak", ApproverToken(Manager));
        Assert.Equal(1, searched.Data.GetProperty("filteredTotal").GetInt32());
        Assert.Equal(2, searched.Data.GetProperty("total").GetInt32());
        Assert.Equal("Burak Demir", Assert.Single(searched.Data.GetProperty("items").EnumerateArray()).GetProperty("displayName").GetString());

        var paged = await Host.GetAsync("/api/v1/time-entry/approvals?start=1&length=1&orderBy=totalMinutes&orderDir=desc", ApproverToken(Manager));
        Assert.Equal(120, Assert.Single(paged.Data.GetProperty("items").EnumerateArray()).GetProperty("totalMinutes").GetInt32());
    }

    [Theory]
    [InlineData("orderBy=userId")]
    [InlineData("length=0")]
    [InlineData("length=501")]
    [InlineData("start=-1&length=10")]
    public async Task A_query_outside_the_protocol_is_refused_not_guessed(string query)
    {
        var refused = await Host.GetAsync($"/api/v1/time-entry/approvals?{query}", ApproverToken(Manager));

        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);
        Assert.Equal(TimeEntryReasonCodes.ApprovalsQueryInvalid, refused.ReasonCode);
    }

    [Fact]
    public async Task Another_tenant_sees_nothing_of_the_approvers_weeks()
    {
        await SubmittedWeekAsync();

        var elsewhere = await Host.GetAsync("/api/v1/time-entry/approvals", ApproverToken(Manager, OtherTenant));

        Assert.Equal(HttpStatusCode.OK, elsewhere.Status);
        Assert.Empty(elsewhere.Data.GetProperty("items").EnumerateArray());
    }
}
