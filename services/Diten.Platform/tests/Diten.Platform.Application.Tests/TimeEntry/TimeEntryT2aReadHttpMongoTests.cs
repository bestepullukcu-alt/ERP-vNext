using System.Net;
using System.Text.Json;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Application.Features.TimeEntry.Adapters;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>Every call the week and timer reads make to the task port, as the test sees them.</summary>
public sealed class TaskPortCallLog
{
    private readonly List<(string Method, int Ids)> _calls = [];

    public IReadOnlyList<(string Method, int Ids)> Calls
    {
        get { lock (_calls) { return _calls.ToList(); } }
    }

    public void Add(string method, int ids)
    {
        lock (_calls)
        {
            _calls.Add((method, ids));
        }
    }

    public void Clear()
    {
        lock (_calls)
        {
            _calls.Clear();
        }
    }
}

/// <summary>The REAL task port, counted: every call goes through to <see cref="TaskGatewayAdapter"/> unchanged.</summary>
public sealed class CountingTaskGateway(TaskGatewayAdapter inner, TaskPortCallLog log) : ITimeEntryTaskGateway
{
    public Task<IReadOnlyDictionary<Guid, TimeEntryTaskFacts>> TaskFactsAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default)
        => inner.TaskFactsAsync(taskIds, ct);

    public Task<DateTimeOffset?> InvalidatedAtAsync(Guid taskItemId, Guid holderUserId, DateTimeOffset since, CancellationToken ct = default)
        => inner.InvalidatedAtAsync(taskItemId, holderUserId, since, ct);

    public Task<IReadOnlyList<TimeEntryPlannedBlock>> PlannedBlocksAsync(Guid userId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default)
        => inner.PlannedBlocksAsync(userId, fromUtc, toUtc, ct);

    public Task<IReadOnlySet<Guid>> ReadableTaskIdsAsync(Guid userId, IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default)
        => inner.ReadableTaskIdsAsync(userId, taskIds, ct);

    public Task<IReadOnlyDictionary<Guid, TimeEntryTaskSummary>> ReadableTaskSummariesAsync(
        Guid userId, IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default)
    {
        log.Add(nameof(ReadableTaskSummariesAsync), taskIds.Count);
        return inner.ReadableTaskSummariesAsync(userId, taskIds, ct);
    }

    public Task<IReadOnlyList<TimeEntryTaskSummary>> OwnOpenTasksAsync(Guid userId, CancellationToken ct = default)
    {
        log.Add(nameof(OwnOpenTasksAsync), 0);
        return inner.OwnOpenTasksAsync(userId, ct);
    }
}

/// <summary>
/// MOD-0280-FU01 T2a (CT v2 decisions 1–5) — what the My Timesheet screen and the top-bar chip read, measured on the
/// wire against the disposable mongod: task titles from ONE batched task-port read per week, a null title for a task the
/// person can no longer read, the approval history names (the ONE assigned approver, never the candidate list), and the
/// "+ Task row" picker under the same read rule a save uses.
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TimeEntryT2aReadHttpMongoTests : TimerScenario
{
    private const string PreviousWeek = "2026-W40";
    private static readonly DateOnly PreviousMonday = new(2026, 9, 28);

    private readonly TaskPortCallLog _portCalls = new();

    public TimeEntryT2aReadHttpMongoTests(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    protected override void ConfigureHost(IServiceCollection services)
    {
        services.AddScoped<TaskGatewayAdapter>();
        services.AddScoped<ITimeEntryTaskGateway>(sp => new CountingTaskGateway(sp.GetRequiredService<TaskGatewayAdapter>(), _portCalls));
    }

    // ── 3 · Task titles on the week ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Week_rows_carry_their_task_title_from_one_batched_port_read()
    {
        await TitleAsync(TaskA, "Batch record review");
        await TitleAsync(TaskB, "Supplier audit plan");
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek,
            Row(Monday, 60, TaskA), Row(Monday.AddDays(1), 30, TaskB), Row(Monday.AddDays(2), 45, TaskA), Row(Monday, 15))).Status);
        _portCalls.Clear();

        var week = await GetWeekAsync();

        Assert.Equal(HttpStatusCode.OK, week.Status);
        var titles = Entries(week).ToDictionary(
            e => (e.GetProperty("localDate").GetString(), e.GetProperty("taskItemId").ValueKind == JsonValueKind.Null ? null : e.GetProperty("taskItemId").GetString()),
            e => e.GetProperty("taskTitle").ValueKind == JsonValueKind.Null ? null : e.GetProperty("taskTitle").GetString());
        Assert.Equal("Batch record review", titles[("2026-10-05", TaskA.ToString())]);
        Assert.Equal("Supplier audit plan", titles[("2026-10-06", TaskB.ToString())]);
        Assert.Equal("Batch record review", titles[("2026-10-07", TaskA.ToString())]);
        Assert.Null(titles[("2026-10-05", null)]); // a category row has no task title

        var titleReads = _portCalls.Calls.Where(c => c.Method == nameof(ITimeEntryTaskGateway.ReadableTaskSummariesAsync)).ToList();
        Assert.Single(titleReads);
        Assert.Equal(2, titleReads[0].Ids); // two distinct tasks, one call — never one call per task or per row
    }

    [Fact]
    public async Task A_task_the_person_can_no_longer_read_keeps_its_row_but_its_title_never_leaves_the_server()
    {
        await TitleAsync(TaskA, "Confidential merger task");
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 60, TaskA))).Status);
        await HandTaskToStrangerAsync(TaskA);

        var week = await GetWeekAsync();

        Assert.Equal(HttpStatusCode.OK, week.Status);
        var row = Assert.Single(Entries(week));
        Assert.Equal(TaskA.ToString(), row.GetProperty("taskItemId").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("taskTitle").ValueKind);
        Assert.DoesNotContain("Confidential merger task", week.Body);
    }

    // ── 4 · Task title on the running timer ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_running_timer_names_its_task_and_a_category_timer_has_no_title()
    {
        await EnableTimerAsync();
        await TitleAsync(TaskA, "Deviation DEV-17 investigation");
        Ok(await StartTimerAsync(TaskA));

        var timer = await GetTimerAsync();

        Assert.Equal(HttpStatusCode.OK, timer.Status);
        Assert.Equal("Deviation DEV-17 investigation", timer.Data.GetProperty("running").GetProperty("taskTitle").GetString());

        Ok(await StartTimerAsync(category: Category));
        var switched = await GetTimerAsync();
        Assert.Equal(JsonValueKind.Null, switched.Data.GetProperty("running").GetProperty("taskTitle").ValueKind);
    }

    // ── 1 + 2 · Approval history ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_week_names_who_submitted_who_it_waits_on_who_rejected_and_who_approved()
    {
        var weekId = await SubmittedWeekAsync();

        var submitted = (await GetWeekAsync()).Data;
        Assert.Equal(Person.ToString(), submitted.GetProperty("submittedByUserId").GetString());
        Assert.Equal("Ayşe Yılmaz", submitted.GetProperty("submittedByDisplayName").GetString());
        Assert.Equal(Manager.ToString(), submitted.GetProperty("assignedApproverUserId").GetString());
        Assert.Equal("Mehmet Kaya", submitted.GetProperty("assignedApproverDisplayName").GetString());
        Assert.Equal(JsonValueKind.Null, submitted.GetProperty("approvedByUserId").ValueKind);

        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(Manager, weekId, approve: false, comment: "Tuesday is missing")).Status);
        var rejected = (await GetWeekAsync()).Data;
        Assert.Equal("Draft", rejected.GetProperty("status").GetString());
        Assert.Equal(Manager.ToString(), rejected.GetProperty("lastRejectedByUserId").GetString());
        Assert.Equal("Mehmet Kaya", rejected.GetProperty("lastRejectedByDisplayName").GetString());

        Assert.Equal(HttpStatusCode.OK, (await SubmitAsync()).Status);
        var resubmitted = await StoredWeekAsync(weekId);
        Assert.Equal(HttpStatusCode.OK, (await DecideAsync(Manager, resubmitted.Id, approve: true)).Status);
        var approved = (await GetWeekAsync()).Data;
        Assert.Equal("Approved", approved.GetProperty("status").GetString());
        Assert.Equal(Manager.ToString(), approved.GetProperty("approvedByUserId").GetString());
        Assert.Equal("Mehmet Kaya", approved.GetProperty("approvedByDisplayName").GetString());
    }

    [Fact]
    public async Task The_assigned_approver_is_the_one_MOD_0023_assigned_never_a_candidate_from_the_list()
    {
        Host.Names.Names[GrandManager] = "Selin Öztürk";
        var weekId = await SubmittedWeekAsync();
        // MOD-0023 assigned somebody OTHER than the stored first candidate (its own resolution, a delegation): the week
        // must say who actually holds the approval.
        await RawSetAsync(PlatformCollections.TimeEntryTimesheetWeeks, weekId,
            ("AssignedApproverUserId", new BsonBinaryData(GrandManager, GuidRepresentation.Standard)));
        Assert.Equal([Manager], (await StoredWeekAsync(weekId)).ApproverCandidateUserIds);

        var week = await GetWeekAsync();

        Assert.Equal(GrandManager.ToString(), week.Data.GetProperty("assignedApproverUserId").GetString());
        Assert.Equal("Selin Öztürk", week.Data.GetProperty("assignedApproverDisplayName").GetString());
        Assert.False(week.Data.TryGetProperty("approverCandidateUserIds", out _));
        Assert.DoesNotContain("Mehmet Kaya", week.Body);
    }

    [Fact]
    public async Task A_name_the_resolver_cannot_give_is_null_never_the_id_in_its_place()
    {
        Host.Names.Names.Remove(Manager);
        await SubmittedWeekAsync();

        var week = await GetWeekAsync();

        Assert.Equal(Manager.ToString(), week.Data.GetProperty("assignedApproverUserId").GetString());
        Assert.Equal(JsonValueKind.Null, week.Data.GetProperty("assignedApproverDisplayName").ValueKind);
    }

    // ── 5 · The task picker ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_picker_offers_own_open_tasks_and_recent_readable_ones_and_nothing_else()
    {
        var planned = Guid.NewGuid();
        var waiting = Guid.NewGuid();
        var ownDone = Guid.NewGuid();
        var someoneElses = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var recentReadable = Guid.NewGuid();
        var recentHidden = Guid.NewGuid();
        await SeedTaskAsync(Tenant, planned, lifecycle: TaskLifecycle.Planned);
        await SeedTaskAsync(Tenant, waiting, lifecycle: TaskLifecycle.Waiting);
        await SeedTaskAsync(Tenant, ownDone, lifecycle: TaskLifecycle.Done);
        await SeedTaskAsync(Tenant, someoneElses, assignee: Stranger, creator: Stranger);
        await SeedTaskAsync(OtherTenant, otherTenant);
        await SeedTaskAsync(Tenant, recentReadable);
        await SeedTaskAsync(Tenant, recentHidden);

        // Time recorded last week on two tasks while the person held them…
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(await VersionAsync(PreviousWeek), PreviousWeek, null,
            Row(PreviousMonday, 60, recentReadable), Row(PreviousMonday, 30, recentHidden))).Status);
        // …then one moved on but stays readable (the person created it), the other went to a team the person cannot read.
        await Collection<TaskItem>(PlatformCollections.TaskItems).UpdateOneAsync(t => t.Id == recentReadable,
            Builders<TaskItem>.Update.Set(t => t.AssigneeUserId, SecondPerson).Set(t => t.Lifecycle, TaskLifecycle.Done));
        await HandTaskToStrangerAsync(recentHidden);

        var options = await TaskOptionsAsync();

        Assert.Equal(HttpStatusCode.OK, options.Status);
        var ids = Ids(options);
        Assert.Contains(TaskA, ids);
        Assert.Contains(TaskB, ids);
        Assert.Contains(planned, ids);
        Assert.Contains(waiting, ids);
        Assert.Contains(recentReadable, ids);
        Assert.DoesNotContain(ownDone, ids);
        Assert.DoesNotContain(someoneElses, ids);
        Assert.DoesNotContain(otherTenant, ids);
        Assert.DoesNotContain(recentHidden, ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());

        var first = options.Data.EnumerateArray().First(o => o.GetProperty("taskItemId").GetGuid() == TaskA);
        Assert.Equal("InProgress", first.GetProperty("status").GetString());
        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("title").GetString()));
    }

    [Fact]
    public async Task Someone_elses_task_is_never_offered_even_when_its_title_matches_the_search()
    {
        var theirs = Guid.NewGuid();
        await SeedTaskAsync(Tenant, theirs, assignee: SecondPerson, creator: SecondPerson);
        await TitleAsync(theirs, "Validation protocol");
        await TitleAsync(TaskA, "Validation report");

        var options = await TaskOptionsAsync("validation");

        Assert.Equal([TaskA], Ids(options));
        Assert.DoesNotContain("Validation protocol", options.Body);
    }

    [Fact]
    public async Task The_picker_filters_by_title_ignoring_case_and_answers_at_most_fifty()
    {
        await TitleAsync(TaskA, "CAPA follow-up");
        await TitleAsync(TaskB, "Line clearance");
        for (var i = 0; i < 55; i++)
        {
            await SeedTaskAsync(Tenant, Guid.NewGuid(), lifecycle: TaskLifecycle.Open);
        }

        Assert.Equal([TaskA], Ids(await TaskOptionsAsync("  capa ")));
        Assert.Equal(TimeEntryLimits.TaskOptionsMax, Ids(await TaskOptionsAsync()).Count);
        Assert.Empty(Ids(await TaskOptionsAsync("no task is called this")));
    }

    [Fact]
    public async Task The_picker_stays_inside_the_callers_tenant_and_needs_the_update_key()
    {
        var elsewhere = await TaskOptionsAsync(token: PersonToken(OtherTenant));
        Assert.Equal(HttpStatusCode.OK, elsewhere.Status);
        Assert.DoesNotContain(TaskA, Ids(elsewhere));
        Assert.DoesNotContain(TaskB, Ids(elsewhere));

        var readOnly = await TaskOptionsAsync(token: Host.Token(Person, Tenant, TimeEntryPermissions.TimesheetsRead));
        Assert.Equal(HttpStatusCode.Forbidden, readOnly.Status);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    private Task<ApiResult> TaskOptionsAsync(string? search = null, string? token = null)
        => Host.GetAsync(
            "/api/v1/time-entry/task-options" + (search is null ? "" : "?search=" + Uri.EscapeDataString(search)),
            token ?? PersonToken());

    private static List<Guid> Ids(ApiResult result)
        => result.Data.EnumerateArray().Select(o => o.GetProperty("taskItemId").GetGuid()).ToList();

    private static IEnumerable<JsonElement> Entries(ApiResult week)
        => week.Data.GetProperty("entries").EnumerateArray();

    private Task TitleAsync(Guid taskId, string title)
        => Collection<TaskItem>(PlatformCollections.TaskItems).UpdateOneAsync(
            t => t.Id == taskId, Builders<TaskItem>.Update.Set(t => t.Title, title));

    /// <summary>The task leaves every leg of the person's read rule: someone else holds it and someone else created it.</summary>
    private Task HandTaskToStrangerAsync(Guid taskId)
        => Collection<TaskItem>(PlatformCollections.TaskItems).UpdateOneAsync(
            t => t.Id == taskId,
            Builders<TaskItem>.Update.Set(t => t.AssigneeUserId, Stranger).Set(t => t.CreatedByUserId, Stranger));
}
