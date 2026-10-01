using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Application.Features.WorkingHours;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using Xunit;
using Xunit.Abstractions;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// BL-484 — the timesheet read paths read per PAGE, not per row, and bulk approval reads back exactly the weeks it was
/// asked about. Measured on the wire against the disposable mongod: the host talks to the store through a client that
/// reports every command it sends, so a "read" here is a real round trip, not a method call.
/// <list type="bullet">
/// <item>(1) The approvals page's marks — entries, the MOD-0023 approval task, timer segments — cost ONE read each for the
/// whole page, and the results (every mark kind, the order) are the ones the single-week read computes.</item>
/// <item>(2) <c>weekIds=</c> narrows the caller's OWN queue to the listed weeks, however long the queue is; a week of
/// another approver's queue is simply absent; a marked week still carries its marks.</item>
/// <item>(3) The person's week asks MOD-0024's read rule once for all its tasks (one watcher read, not one per task) and
/// still names exactly the tasks the person may read.</item>
/// </list>
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TimesheetReadBatchHttpMongoTests : TimerScenario
{
    private static readonly DateOnly PreviousMonday = Monday.AddDays(-7);          // 2026-09-28, W40
    private const string PreviousWeek = "2026-W40";

    private readonly MongoReadCounter _reads = new();
    private readonly CallCounter _workingHoursCalls = new();
    private readonly IPlatformDbContext _observed;
    private readonly ITestOutputHelper _output;

    public TimesheetReadBatchHttpMongoTests(TimeEntryMongoFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _observed = fixture.ObservedDbContext(_reads.Record);
        _output = output;
    }

    protected override IPlatformDbContext HostDbContext => _observed;

    protected override void ConfigureHost(IServiceCollection services)
    {
        // The real provider, counted: how many times the page asks the working calendar, and for which (person, week).
        services.AddScoped<WorkingHoursProvider>();
        services.AddScoped<IWorkingHoursProvider>(sp =>
            new CountingWorkingHours(sp.GetRequiredService<WorkingHoursProvider>(), _workingHoursCalls));
    }

    // ── (1) the approvals page reads per page ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_page_of_approvals_reads_entries_approval_tasks_and_segments_once_whether_it_holds_two_weeks_or_six()
    {
        // Three people × two weeks = six submitted weeks routed to the manager, each with rows, an open approval task and
        // a timer segment — every store the marks read has something to find for every row.
        var people = new[] { Person, SecondPerson, await ExtraReportAsync() };
        foreach (var person in people)
        {
            await SubmitWeekAsync(person, PreviousWeek, Row(PreviousMonday, 120, category: Category));
            await SubmitWeekAsync(person, CurrentWeek, Row(Monday, 90, category: Category));
        }

        foreach (var person in people)
        {
            await SeedSegmentAsync(person, PreviousMonday.AddDays(1), TimerStopReason.LocalMidnight);
            await SeedSegmentAsync(person, Monday, TimerStopReason.TimerControl);
        }

        var two = await MeasureAsync("/api/v1/time-entry/approvals?start=0&length=2");
        var six = await MeasureAsync("/api/v1/time-entry/approvals?start=0&length=6");

        _output.WriteLine($"page of 2: {Describe(two.Reads)}; working hours asked {two.WorkingHoursCalls.Count}x");
        _output.WriteLine($"page of 6: {Describe(six.Reads)}; working hours asked {six.WorkingHoursCalls.Count}x");
        Assert.Equal(2, two.Items.Count);
        Assert.Equal(6, six.Items.Count);
        foreach (var collection in new[]
                 {
                     PlatformCollections.TimeEntryEntries, PlatformCollections.ApprovalTasks, PlatformCollections.TimeEntryTimerSegments
                 })
        {
            Assert.True(two.Reads.GetValueOrDefault(collection) == 1, $"{collection}: {two.Reads.GetValueOrDefault(collection)} reads for a page of 2");
            Assert.True(six.Reads.GetValueOrDefault(collection) == 1, $"{collection}: {six.Reads.GetValueOrDefault(collection)} reads for a page of 6");
        }

        // The working calendar is asked once per distinct (person, week) on the page — never twice for the same one.
        Assert.Equal(2, two.WorkingHoursCalls.Count);
        Assert.Equal(6, six.WorkingHoursCalls.Count);
        Assert.Equal(six.WorkingHoursCalls.Count, six.WorkingHoursCalls.Distinct().Count());
    }

    [Fact]
    public async Task Every_mark_kind_and_the_order_on_the_page_are_the_ones_the_single_week_read_computes()
    {
        Host.Calendar.Holidays.Add(Monday.AddDays(1));                                    // Tuesday of W41 is a holiday

        // P41: an 11-hour-plus day (flagged) and time on the holiday. P40: a timer cut at midnight and time outside hours.
        // S41 / S40: clean — and S41 has no Tuesday row, so the shared holiday must NOT show on it.
        var p41 = await SubmitWeekAsync(Person, CurrentWeek, Row(Monday, 705, TaskA), Row(Monday.AddDays(1), 60));
        Host.Clock.UtcNow = Wednesday.AddMinutes(1);
        var p40 = await SubmitWeekAsync(Person, PreviousWeek, Row(PreviousMonday, 120, TaskA));
        Host.Clock.UtcNow = Wednesday.AddMinutes(2);
        var s41 = await SubmitWeekAsync(SecondPerson, CurrentWeek, Row(Monday, 300, category: Category));
        Host.Clock.UtcNow = Wednesday.AddMinutes(3);
        var s40 = await SubmitWeekAsync(SecondPerson, PreviousWeek, Row(PreviousMonday.AddDays(1), 60, category: Category));

        // Staged after the submits (no draft recompute moves a week):
        await SeedSegmentAsync(Person, PreviousMonday.AddDays(1), TimerStopReason.LocalMidnight);   // P40: cut at midnight
        await SeedSegmentAsync(Person, Monday, TimerStopReason.TimerControl);                     // P41: an ordinary stop — no mark
        await SeedSegmentAsync(SecondPerson, PreviousMonday.AddDays(-5), TimerStopReason.LocalMidnight); // W39, not on the page
        var p40Row = Assert.Single(await StoredEntriesAsync(p40));
        await RawSetAsync(PlatformCollections.TimeEntryEntries, p40Row.Id, ("OutsideWorkingMinutes", new BsonInt32(45)));

        var list = await Host.GetAsync("/api/v1/time-entry/approvals?start=0&length=10", ApproverToken(Manager));
        Assert.Equal(HttpStatusCode.OK, list.Status);
        var rows = list.Data.GetProperty("items").EnumerateArray().ToList();

        // The order: submission order (the default), then by total minutes when asked.
        Assert.Equal([p41, p40, s41, s40], rows.Select(r => r.GetProperty("weekId").GetGuid()).ToList());
        var byMinutes = await Host.GetAsync("/api/v1/time-entry/approvals?start=0&length=10&orderBy=totalMinutes&orderDir=desc", ApproverToken(Manager));
        Assert.Equal([p41, s41, p40, s40], byMinutes.Data.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("weekId").GetGuid()).ToList());

        // Every mark kind, stated.
        var expected = new Dictionary<Guid, (string[] Flagged, string[] AutoClosed, int Outside, string[] Holidays)>
        {
            [p41] = (["2026-10-05"], [], 0, ["2026-10-06"]),
            [p40] = ([], ["2026-09-29"], 45, []),
            [s41] = ([], [], 0, []),
            [s40] = ([], [], 0, [])
        };
        foreach (var row in rows)
        {
            var (flagged, autoClosed, outside, holidays) = expected[row.GetProperty("weekId").GetGuid()];
            Assert.Equal(flagged, Dates(row.GetProperty("flaggedDates")));
            Assert.Equal(autoClosed, Dates(row.GetProperty("autoClosedDates")));
            Assert.Equal(outside, row.GetProperty("outsideWorkingMinutes").GetInt32());
            Assert.Equal(holidays, Dates(row.GetProperty("holidayDates")));
        }

        // …and exactly what the single-week read (one week at a time, its own reads) says about each of them, the work
        // item included.
        foreach (var row in rows)
        {
            var weekId = row.GetProperty("weekId").GetGuid();
            var single = await Host.GetAsync($"/api/v1/time-entry/approvals/{weekId}", ApproverToken(Manager));
            Assert.Equal(HttpStatusCode.OK, single.Status);
            Assert.Equal(Marks(single.Data), Marks(row));
            Assert.Equal((await OpenApprovalTaskAsync(weekId)).Id, row.GetProperty("approvalTaskId").GetGuid());
        }

        var labels = new Dictionary<Guid, string> { [p41] = "P41", [p40] = "P40", [s41] = "S41", [s40] = "S40" };
        foreach (var row in rows)
        {
            _output.WriteLine($"{labels[row.GetProperty("weekId").GetGuid()]} {Marks(row)}");
        }
    }

    // ── (2) weekIds — the caller's own queue, narrowed to the listed weeks ───────────────────────────────────────

    [Fact]
    public async Task WeekIds_reads_back_a_week_beyond_the_first_500_of_the_queue_and_it_can_be_approved()
    {
        // 501 older submissions in the manager's queue (cheaply: stored rows, no MOD-0023 instance), then the real ones.
        await SeedQueueFillerAsync(501);
        var clean = await SubmitWeekAsync(Person, CurrentWeek, Row(Monday, 120, TaskA));
        Host.Clock.UtcNow = Wednesday.AddMinutes(1);
        var marked = await SubmitWeekAsync(SecondPerson, CurrentWeek, Row(Monday, 705, category: Category));
        Host.Clock.UtcNow = Wednesday.AddMinutes(2);
        var elsewhere = await SubmitWeekAsync(Manager, CurrentWeek, Row(Monday, 60, category: Category));   // → the grand-manager

        // The old window: the first 500 of the queue hold none of them.
        var window = await Host.GetAsync("/api/v1/time-entry/approvals?start=0&length=500", ApproverToken(Manager));
        Assert.Equal(HttpStatusCode.OK, window.Status);
        Assert.Equal(503, window.Data.GetProperty("total").GetInt32());
        var inWindow = window.Data.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("weekId").GetGuid()).ToHashSet();
        Assert.DoesNotContain(clean, inWindow);
        Assert.DoesNotContain(marked, inWindow);

        // Asked by id: exactly the caller's weeks among those listed, with their work item and their marks.
        var asked = await Host.GetAsync(
            $"/api/v1/time-entry/approvals?start=0&length=100&weekIds={clean},{marked},{elsewhere}", ApproverToken(Manager));
        Assert.Equal(HttpStatusCode.OK, asked.Status);
        var items = asked.Data.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal([clean, marked], items.Select(r => r.GetProperty("weekId").GetGuid()).ToList());   // not `elsewhere`
        Assert.Equal(2, asked.Data.GetProperty("total").GetInt32());
        var cleanRow = items[0];
        var task = await OpenApprovalTaskAsync(clean);
        Assert.Equal(task.Id, cleanRow.GetProperty("approvalTaskId").GetGuid());
        Assert.Equal(task.Version, cleanRow.GetProperty("approvalTaskVersion").GetInt32());
        Assert.Empty(cleanRow.GetProperty("flaggedDates").EnumerateArray());
        Assert.Empty(cleanRow.GetProperty("autoClosedDates").EnumerateArray());
        Assert.Empty(cleanRow.GetProperty("holidayDates").EnumerateArray());
        Assert.Equal(0, cleanRow.GetProperty("outsideWorkingMinutes").GetInt32());
        Assert.Equal(["2026-10-05"], Dates(items[1].GetProperty("flaggedDates")));                 // the marked one stays marked

        // The repeated form of the parameter names the same weeks (never only the first).
        var repeated = await Host.GetAsync($"/api/v1/time-entry/approvals?start=0&length=100&weekIds={clean}&weekIds={marked}", ApproverToken(Manager));
        Assert.Equal(2, repeated.Data.GetProperty("items").GetArrayLength());

        // The week beyond the old window is approvable with exactly what the id read returned (MOD-0023's own command —
        // the one the Task Center's work-item action dispatches to), and then it leaves the queue.
        var approved = await Host.PostAsync(
            $"/api/v1/workflow/tasks/{cleanRow.GetProperty("approvalTaskId").GetGuid()}/approve",
            ApproverToken(Manager),
            new { actorId = Manager.ToString(), reasonCode = "OK", idempotencyKey = Guid.NewGuid().ToString("N"), comment = (string?)null, evidenceRef = (string?)null });
        Assert.Equal(HttpStatusCode.OK, approved.Status);
        var after = await Host.GetAsync($"/api/v1/time-entry/approvals?start=0&length=100&weekIds={clean}", ApproverToken(Manager));
        Assert.Empty(after.Data.GetProperty("items").EnumerateArray());
        Assert.Equal(TimesheetWeekStatus.Approved, (await StoredWeekAsync(clean)).Status);
    }

    [Fact]
    public async Task WeekIds_never_reaches_beyond_the_callers_own_queue()
    {
        var mine = await SubmitWeekAsync(Person, CurrentWeek, Row(Monday, 120, TaskA));
        var managersOwn = await SubmitWeekAsync(Manager, CurrentWeek, Row(Monday, 60, category: Category));

        // The grand-manager's queue holds the manager's week, not the person's; the manager's holds the person's only.
        var grand = await Host.GetAsync($"/api/v1/time-entry/approvals?weekIds={mine},{managersOwn}", ApproverToken(GrandManager));
        Assert.Equal([managersOwn], grand.Data.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("weekId").GetGuid()).ToList());
        var manager = await Host.GetAsync($"/api/v1/time-entry/approvals?weekIds={mine},{managersOwn}", ApproverToken(Manager));
        Assert.Equal([mine], manager.Data.GetProperty("items").EnumerateArray().Select(r => r.GetProperty("weekId").GetGuid()).ToList());

        // Another tenant's approver with the same ids sees nothing.
        var foreign = await Host.GetAsync($"/api/v1/time-entry/approvals?weekIds={mine}", ApproverToken(Manager, OtherTenant));
        Assert.Equal(HttpStatusCode.OK, foreign.Status);
        Assert.Empty(foreign.Data.GetProperty("items").EnumerateArray());
    }

    [Theory]
    [InlineData("weekIds=")]
    [InlineData("weekIds=not-a-guid")]
    [InlineData("weekIds={0},,{1}")]
    [InlineData("weekIds={0}&weekIds=")]
    [InlineData("TOO_MANY")]
    public async Task A_weekIds_list_outside_the_protocol_is_refused_not_guessed(string query)
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var q = query == "TOO_MANY"
            ? "weekIds=" + string.Join(',', Enumerable.Range(0, TimeEntryLimits.ApprovalsMaxWeekIds + 1).Select(_ => Guid.NewGuid()))
            : string.Format(System.Globalization.CultureInfo.InvariantCulture, query, a, b);

        var refused = await Host.GetAsync($"/api/v1/time-entry/approvals?{q}", ApproverToken(Manager));

        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);
        Assert.Equal(TimeEntryReasonCodes.ApprovalsQueryInvalid, refused.ReasonCode);
    }

    [Fact]
    public async Task As_many_weekIds_as_one_bulk_approval_may_carry_are_accepted()
    {
        var ids = string.Join(',', Enumerable.Range(0, TimeEntryLimits.ApprovalsMaxWeekIds).Select(_ => Guid.NewGuid()));

        var answer = await Host.GetAsync($"/api/v1/time-entry/approvals?start=0&length=100&weekIds={ids}", ApproverToken(Manager));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Empty(answer.Data.GetProperty("items").EnumerateArray());
    }

    // ── (3) the week GET asks the task read rule once for the week ───────────────────────────────────────────────

    [Fact]
    public async Task The_week_asks_the_task_read_rule_with_one_watcher_read_whether_it_holds_two_tasks_or_six()
    {
        var tasks = new List<Guid> { TaskA, TaskB };
        for (var i = 0; i < 4; i++)
        {
            var extra = Guid.NewGuid();
            await SeedTaskAsync(Tenant, extra);
            tasks.Add(extra);
        }

        Ok(await SaveFreshAsync(PreviousWeek, tasks.Take(2).Select(t => Row(PreviousMonday, 30, t)).ToArray()));
        Ok(await SaveFreshAsync(CurrentWeek, tasks.Select(t => Row(Monday, 30, t)).ToArray()));

        var two = await MeasureWeekAsync(PreviousWeek);
        var six = await MeasureWeekAsync(CurrentWeek);
        _output.WriteLine($"week of 2 tasks: {Describe(two.Reads)}");
        _output.WriteLine($"week of 6 tasks: {Describe(six.Reads)}");

        Assert.Equal(2, two.Titled);
        Assert.Equal(6, six.Titled);
        Assert.True(two.Reads.GetValueOrDefault(PlatformCollections.TaskWatchers) == 1, $"watchers: {two.Reads.GetValueOrDefault(PlatformCollections.TaskWatchers)} reads for 2 tasks");
        Assert.True(six.Reads.GetValueOrDefault(PlatformCollections.TaskWatchers) == 1, $"watchers: {six.Reads.GetValueOrDefault(PlatformCollections.TaskWatchers)} reads for 6 tasks");
    }

    [Fact]
    public async Task The_week_names_exactly_the_tasks_the_person_may_read_by_every_data_leg()
    {
        // One row the person saved themselves; the others are staged straight into the week (a save would refuse a task
        // the person cannot read, and the point is to see what the READ does with one).
        var asCreator = Guid.NewGuid();
        var asWatcher = Guid.NewGuid();
        var parent = Guid.NewGuid();
        var subtask = Guid.NewGuid();
        var unrelated = Guid.NewGuid();
        await SeedTaskAsync(Tenant, asCreator, assignee: Stranger, creator: Person);
        await SeedTaskAsync(Tenant, asWatcher, assignee: Stranger, creator: Stranger);
        await Collection<Domain.Entities.Tasks.TaskWatcher>(PlatformCollections.TaskWatchers).InsertOneAsync(
            new Domain.Entities.Tasks.TaskWatcher { TenantId = Tenant, TaskItemId = asWatcher, UserId = Person });
        await SeedTaskAsync(Tenant, parent, assignee: Person, creator: Stranger);
        await SeedTaskAsync(Tenant, subtask, assignee: Stranger, creator: Stranger);
        await Collection<Domain.Entities.Tasks.TaskItem>(PlatformCollections.TaskItems).UpdateOneAsync(
            t => t.Id == subtask, Builders<Domain.Entities.Tasks.TaskItem>.Update.Set(t => t.ParentTaskItemId, parent));
        await SeedTaskAsync(Tenant, unrelated, assignee: Stranger, creator: Stranger);

        Ok(await SaveFreshAsync(CurrentWeek, Row(Monday, 30, TaskA)));
        var weekId = (await StoredWeeksAsync()).Single().Id;
        foreach (var task in new[] { asCreator, asWatcher, subtask, unrelated })
        {
            await Collection<Domain.Entities.TimeEntry.TimeEntry>(PlatformCollections.TimeEntryEntries).InsertOneAsync(
                new Domain.Entities.TimeEntry.TimeEntry
                {
                    TenantId = Tenant, TimesheetWeekId = weekId, UserId = Person, WeekKey = CurrentWeek, LocalDate = Monday,
                    DurationMinutes = 30, TaskItemId = task
                });
        }

        var entries = (await GetWeekAsync()).Data.GetProperty("entries").EnumerateArray()
            .ToDictionary(e => e.GetProperty("taskItemId").GetGuid(), e => e.GetProperty("taskTitle"));

        foreach (var readable in new[] { TaskA, asCreator, asWatcher, subtask })
        {
            Assert.Equal(JsonValueKind.String, entries[readable].ValueKind);
        }

        Assert.Equal(JsonValueKind.Null, entries[unrelated].ValueKind);
    }

    // ── World building ───────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>One more person in the person's seat (reports to the manager's), named for the list.</summary>
    private async Task<Guid> ExtraReportAsync()
    {
        var extra = Guid.NewGuid();
        await SeatAsync(extra, PersonSeat);
        Host.Names.Names[extra] = "Deniz Aksoy";
        return extra;
    }

    /// <summary>Saves the rows into <paramref name="weekKey"/> as <paramref name="person"/> and submits; returns the week id.
    /// Somebody with no seat yet is seated in the person's seat first, so the manager is their approver.</summary>
    private async Task<Guid> SubmitWeekAsync(Guid person, string weekKey, params object[] rows)
    {
        if (!await IsSeatedAsync(person))
        {
            await SeatAsync(person, PersonSeat);
        }

        var token = PersonToken(person: person);
        var saved = await SaveAsync(await VersionAsync(weekKey, token), weekKey, token, rows);
        Assert.True(saved.Status == HttpStatusCode.OK, saved.ToString());
        var submitted = await base.SubmitAsync(weekKey, token);
        Assert.True(submitted.Status == HttpStatusCode.OK, submitted.ToString());
        return submitted.Data.GetProperty("weekId").GetGuid();
    }

    private async Task<bool> IsSeatedAsync(Guid person)
        => await Collection<Domain.Entities.Organization.PositionAssignment>(PlatformCollections.PositionAssignments)
            .Find(a => a.TenantId == Tenant && a.UserId == person).AnyAsync();

    private Task SeedSegmentAsync(Guid person, DateOnly day, TimerStopReason reason)
        => Collection<TimerSegment>(PlatformCollections.TimeEntryTimerSegments).InsertOneAsync(new TimerSegment
        {
            TenantId = Tenant,
            UserId = person,
            TaskItemId = TaskA,
            IsRunning = false,
            StartedAtUtc = Wednesday.AddDays(-9),
            StoppedAtUtc = Wednesday.AddDays(-9).AddMinutes(30),
            DurationSeconds = 1800,
            LocalDate = day,
            TimeZoneId = Zone,
            WeekKey = Application.Features.TimeEntry.Services.WeekCalendar.KeyOf(day),
            StartSource = TimerStartSource.TimerControl,
            StopReason = reason
        });

    /// <summary>Submitted weeks of people nobody sees, routed to the manager, submitted before anything the test submits.
    /// No MOD-0023 instance: the decision puller has nothing to ask about them.</summary>
    private Task SeedQueueFillerAsync(int count)
        => Collection<TimesheetWeek>(PlatformCollections.TimeEntryTimesheetWeeks).InsertManyAsync(
            Enumerable.Range(0, count).Select(i =>
            {
                var submittedAt = Wednesday.AddDays(-1).AddSeconds(i);
                return new TimesheetWeek
                {
                    TenantId = Tenant,
                    UserId = Guid.NewGuid(),
                    WeekKey = CurrentWeek,
                    WeekStartDate = Monday,
                    TimeZoneId = Zone,
                    RevisionNumber = 1,
                    Status = TimesheetWeekStatus.Submitted,
                    AssignedApproverUserId = Manager,
                    ApproverCandidateUserIds = [Manager],
                    SubmissionCount = 1,
                    SubmittedAtUtc = submittedAt,
                    SubmittedAtUtcTicks = submittedAt.UtcTicks,
                    TotalMinutes = 60
                };
            }));

    private async Task<ApprovalTask> OpenApprovalTaskAsync(Guid weekId)
    {
        var week = await StoredWeekAsync(weekId);
        return await Collection<ApprovalTask>(PlatformCollections.ApprovalTasks)
            .Find(t => t.WorkflowInstanceId == week.WorkflowInstanceId && t.Status == ApprovalTaskStatus.WaitingApproval)
            .SingleAsync();
    }

    // ── Measuring ────────────────────────────────────────────────────────────────────────────────────────────────

    private sealed record Measured(IReadOnlyList<JsonElement> Items, IReadOnlyDictionary<string, int> Reads, IReadOnlyList<string> WorkingHoursCalls);

    private async Task<Measured> MeasureAsync(string path)
    {
        var token = ApproverToken(Manager);
        _workingHoursCalls.Clear();
        var (result, reads) = await _reads.MeasureAsync(() => Host.GetAsync(path, token));
        Assert.True(result.Status == HttpStatusCode.OK, result.ToString());
        return new Measured(result.Data.GetProperty("items").EnumerateArray().ToList(), reads, _workingHoursCalls.Calls);
    }

    private sealed record MeasuredWeek(int Titled, IReadOnlyDictionary<string, int> Reads);

    private async Task<MeasuredWeek> MeasureWeekAsync(string weekKey)
    {
        var token = PersonToken();
        var (result, reads) = await _reads.MeasureAsync(() => Host.GetAsync($"/api/v1/time-entry/weeks/{weekKey}", token));
        Assert.True(result.Status == HttpStatusCode.OK, result.ToString());
        var titled = result.Data.GetProperty("entries").EnumerateArray()
            .Count(e => e.GetProperty("taskTitle").ValueKind == JsonValueKind.String);
        return new MeasuredWeek(titled, reads);
    }

    private static string Describe(IReadOnlyDictionary<string, int> reads)
        => string.Join(", ", reads.OrderBy(r => r.Key, StringComparer.Ordinal).Select(r => $"{r.Key}={r.Value}"));

    /// <summary>The fields a bulk approval decides on, as one comparable string.</summary>
    private static string Marks(JsonElement row)
        => string.Join(' ',
            JsonSerializer.Serialize(Dates(row.GetProperty("flaggedDates"))),
            JsonSerializer.Serialize(Dates(row.GetProperty("autoClosedDates"))),
            row.GetProperty("outsideWorkingMinutes").GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonSerializer.Serialize(Dates(row.GetProperty("holidayDates"))),
            row.GetProperty("approvalTaskId").ToString(),
            row.GetProperty("approvalTaskVersion").ToString());
}

/// <summary>
/// BL-484 — counts the READ commands a client sends, per collection, while armed. <c>getMore</c> is not a new query (it
/// continues one) and is not counted.
/// </summary>
internal sealed class MongoReadCounter
{
    private readonly ConcurrentDictionary<string, int> _reads = new(StringComparer.Ordinal);
    private volatile bool _armed;

    public void Record(CommandStartedEvent started)
    {
        if (!_armed)
        {
            return;
        }

        var key = started.CommandName switch
        {
            "find" or "aggregate" or "count" or "distinct" => started.CommandName,
            _ => null
        };
        if (key is not null && started.Command.TryGetValue(key, out var collection) && collection.IsString)
        {
            _reads.AddOrUpdate(collection.AsString, 1, (_, n) => n + 1);
        }
    }

    public async Task<(T Result, IReadOnlyDictionary<string, int> Reads)> MeasureAsync<T>(Func<Task<T>> action)
    {
        _reads.Clear();
        _armed = true;
        try
        {
            var result = await action();
            return (result, new Dictionary<string, int>(_reads, StringComparer.Ordinal));
        }
        finally
        {
            _armed = false;
        }
    }
}

/// <summary>Every (person, first day) a working-hours question was asked for, in order.</summary>
internal sealed class CallCounter
{
    private readonly List<string> _calls = [];

    public IReadOnlyList<string> Calls
    {
        get { lock (_calls) { return _calls.ToList(); } }
    }

    public void Add(string call)
    {
        lock (_calls) { _calls.Add(call); }
    }

    public void Clear()
    {
        lock (_calls) { _calls.Clear(); }
    }
}

internal sealed class CountingWorkingHours(IWorkingHoursProvider inner, CallCounter calls) : IWorkingHoursProvider
{
    public Task<WorkingHoursResult> GetWorkingWindowsAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        calls.Add($"{userId:N}:{from:yyyy-MM-dd}:{to:yyyy-MM-dd}");
        return inner.GetWorkingWindowsAsync(userId, from, to, ct);
    }
}
