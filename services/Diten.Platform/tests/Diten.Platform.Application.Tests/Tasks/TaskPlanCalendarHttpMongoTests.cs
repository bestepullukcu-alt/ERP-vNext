using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Platform.API.Controllers;
using Diten.Platform.API.Observability;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Features.WorkAggregation.Calendar;
using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using Diten.Platform.Application.Features.WorkAggregation.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.WorkAggregation.Providers;
using Diten.Platform.Application.Features.WorkAggregation.Queries;
using Diten.Platform.Application.Features.WorkAggregation.Services;
using Diten.Platform.Application.Features.WorkingCalendar.Provider;
using Diten.Platform.Application.Features.WorkingHours;
using Diten.Platform.Application.Tests.Persistence;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// One disposable mongod for the whole class — started on a free port, dropped on the way out. Nothing here touches
/// the shared dev instance on 27017 (WP-TASK-CALENDAR-ENGINE-01: "paylaşılan dev veritabanına yazma").
/// </summary>
public sealed class PlanCalendarMongoFixture : IAsyncLifetime
{
    private DisposableStandaloneMongo? _mongo;

    public IPlatformDbContext DbContext { get; private set; } = null!;
    public IMongoDatabase Database { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        PlatformTestSerializers.Register();
        _mongo = await DisposableStandaloneMongo.StartAsync();

        // Our OWN client with the production Guid representation — the helper's client is left at the driver default,
        // and a Standard serializer against a legacy client finds nothing by id (see PlatformTestSerializers).
        var settings = MongoClientSettings.FromConnectionString($"mongodb://127.0.0.1:{_mongo.Port}/?directConnection=true");
#pragma warning disable CS0618
        settings.GuidRepresentation = GuidRepresentation.Standard;
#pragma warning restore CS0618
        var client = new MongoClient(settings);
        // A FIXED name (DB-010): the whole mongod is thrown away, and tests isolate by TenantId inside it.
        Database = client.GetDatabase("diten_platform_standalone_calendar");
        DbContext = new PlatformDbContext(client, Database);
    }

    public async Task DisposeAsync()
    {
        if (_mongo is not null)
        {
            await _mongo.DisposeAsync();
        }
    }
}

/// <summary>
/// WP-TASK-CALENDAR-ENGINE-01 — the personal plan block, the holder-only rule (BL-449), the conflict rule, the
/// working-hours warnings and the calendar feed, all through the REAL routes (TasksController, WorkItemsController,
/// WorkCalendarController), [HasPermission]/[LoginOnly], JWT tenant resolution and the REAL Mongo repositories on a
/// disposable mongod.
///
/// <para>The tenant is Europe/Istanbul (UTC+3, no DST) with the default 09:00–18:00 window: local 09:00 is 06:00Z,
/// 18:00 is 15:00Z. The working calendar is a stub that closes weekends and one named holiday — the calendar engine
/// has its own suite; what is under test here is how the plan rule and the feed USE the answer.</para>
/// </summary>
public sealed class TaskPlanCalendarHttpMongoTests : IClassFixture<PlanCalendarMongoFixture>, IAsyncLifetime
{
    private static readonly Guid Holder = Guid.NewGuid();
    private static readonly Guid Requester = Guid.NewGuid();
    private static readonly Guid Colleague = Guid.NewGuid();

    // Monday 2026-10-05 … a week of working days; Saturday 10th; Thursday 29th is the stub's holiday.
    private static readonly DateOnly Monday = new(2026, 10, 5);
    private static readonly DateOnly Holiday = new(2026, 10, 29);
    private const string HolidayName = "Cumhuriyet Bayramı";

    private readonly PlanCalendarMongoFixture _fixture;
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _otherTenant = Guid.NewGuid();
    private Host _host = null!;

    public TaskPlanCalendarHttpMongoTests(PlanCalendarMongoFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        var tenants = _fixture.Database.GetCollection<Tenant>(PlatformCollections.Tenants);
        await tenants.InsertOneAsync(NewTenant(_tenant, "Europe/Istanbul"));
        await tenants.InsertOneAsync(NewTenant(_otherTenant, "Europe/Istanbul"));
        _host = new Host(_fixture.DbContext);
    }

    public Task DisposeAsync()
    {
        _host.Dispose();
        return Task.CompletedTask;
    }

    // ── A — day plan and block plan ──────────────────────────────────────────

    [Fact]
    public async Task A_DAY_plan_stores_the_day_only_and_moves_the_task_to_Planned()
    {
        var task = await SeedAsync(Holder);
        var day = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);

        var (status, body) = await _host.PostJsonAsync($"/api/v1/tasks/{task.Id}/plan", HolderToken(),
            new { expectedVersion = task.Version, plannedDate = day });

        Assert.Equal(HttpStatusCode.OK, status);
        var stored = await StoredAsync(task.Id);
        Assert.Equal(TaskLifecycle.Planned, stored.Lifecycle);
        Assert.Equal(day, stored.PlannedDate);
        Assert.Null(stored.PlannedStartAt);
        Assert.Null(stored.PlannedDurationMinutes);
        Assert.Empty(Data(body).GetProperty("warnings").EnumerateArray());
    }

    [Fact]
    public async Task A_BLOCK_plan_stores_start_and_length_and_sets_the_plan_day_to_the_TENANT_LOCAL_day()
    {
        /*
         * 22:00Z on Monday is 01:00 on TUESDAY in Istanbul. A plan day taken from the UTC date would put this block on
         * Monday — one day off for every evening plan in a UTC+ tenant. The day must be the local one.
         */
        var task = await SeedAsync(Holder);
        var start = new DateTimeOffset(2026, 10, 5, 22, 0, 0, TimeSpan.Zero);

        var (status, _) = await _host.PostJsonAsync($"/api/v1/tasks/{task.Id}/plan", HolderToken(),
            new { expectedVersion = task.Version, plannedStartAt = start, durationMinutes = 30 });

        Assert.Equal(HttpStatusCode.OK, status);
        var stored = await StoredAsync(task.Id);
        Assert.Equal(start, stored.PlannedStartAt);
        Assert.Equal(TimeSpan.Zero, stored.PlannedStartAt!.Value.Offset);
        Assert.Equal(30, stored.PlannedDurationMinutes);
        Assert.Equal(new DateOnly(2026, 10, 6), DateOnly.FromDateTime(stored.PlannedDate!.Value.DateTime));
        Assert.Equal(TaskLifecycle.Planned, stored.Lifecycle);
    }

    [Fact]
    public async Task No_duration_takes_the_ESTIMATE_rounded_up_to_a_15_minute_step()
    {
        var task = await SeedAsync(Holder, estimateHours: 1.4m); // 84 min → 90

        var (status, body) = await PlanBlockAsync(task, Local(Monday, 9, 0));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(90, (await StoredAsync(task.Id)).PlannedDurationMinutes);
        Assert.Equal(90, Data(body).GetProperty("plannedDurationMinutes").GetInt32());
    }

    [Fact]
    public async Task No_duration_and_no_estimate_takes_60_minutes()
    {
        var task = await SeedAsync(Holder);

        var (status, _) = await PlanBlockAsync(task, Local(Monday, 9, 0));

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(TaskPlanBlockRules.FallbackDurationMinutes, (await StoredAsync(task.Id)).PlannedDurationMinutes);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(0)]
    public async Task A_length_that_is_not_whole_15_minute_steps_is_400_TASK_PLAN_DURATION_INVALID(int minutes)
    {
        var task = await SeedAsync(Holder);

        var (status, body) = await PlanBlockAsync(task, Local(Monday, 9, 0), minutes);

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(TaskReasonCodes.PlanDurationInvalid, ReasonCode(body));
        Assert.Equal(TaskLifecycle.Open, (await StoredAsync(task.Id)).Lifecycle);
    }

    [Fact]
    public async Task A_block_past_the_end_of_the_working_day_is_CUT_there_and_the_rest_is_ANSWERED_not_stored()
    {
        // 4 h estimate starting 16:00 local → only 2 h fit before 18:00. 120 stored, 120 answered as remaining.
        var task = await SeedAsync(Holder, estimateHours: 4m);

        var (status, body) = await PlanBlockAsync(task, Local(Monday, 16, 0));

        Assert.Equal(HttpStatusCode.OK, status);
        var data = Data(body);
        Assert.Equal(120, data.GetProperty("plannedDurationMinutes").GetInt32());
        Assert.Equal(120, data.GetProperty("remainingMinutes").GetInt32());
        Assert.True(data.GetProperty("truncated").GetBoolean());
        Assert.Equal(120, (await StoredAsync(task.Id)).PlannedDurationMinutes);

        // NOT STORED: the raw document carries no remaining-minutes field of any spelling.
        var raw = await RawAsync(task.Id);
        Assert.DoesNotContain(raw.Names, name => name.Contains("Remaining", StringComparison.OrdinalIgnoreCase));

        // …and the Task Center projection derives the same number from what IS stored.
        var item = await MyItemAsync(HolderToken(), task.Id);
        Assert.Equal(120, item.GetProperty("remainingMinutes").GetInt32());
        Assert.Equal(120, item.GetProperty("plannedDurationMinutes").GetInt32());
    }

    // ── unplan ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Unplan_clears_the_day_and_the_block_returns_the_task_to_Open_and_records_it()
    {
        var task = await SeedAsync(Holder);
        Assert.Equal(HttpStatusCode.OK, (await PlanBlockAsync(task, Local(Monday, 10, 0), 60)).Status);
        var planned = await StoredAsync(task.Id);

        var (status, _) = await _host.PostJsonAsync($"/api/v1/tasks/{task.Id}/unplan", HolderToken(),
            new { expectedVersion = planned.Version });

        Assert.Equal(HttpStatusCode.NoContent, status);
        var stored = await StoredAsync(task.Id);
        Assert.Equal(TaskLifecycle.Open, stored.Lifecycle);
        Assert.Null(stored.PlannedDate);
        Assert.Null(stored.PlannedStartAt);
        Assert.Null(stored.PlannedDurationMinutes);

        var kinds = await _fixture.Database.GetCollection<TaskTransition>(PlatformCollections.TaskTransitions)
            .Find(t => t.TaskItemId == task.Id).ToListAsync();
        Assert.Contains(kinds, t => t.Kind == TaskTransitionKind.Unplanned);
    }

    [Fact]
    public async Task Unplan_of_a_task_that_was_never_planned_is_409_TASK_UNPLAN_NOT_ALLOWED()
    {
        var task = await SeedAsync(Holder);

        var (status, body) = await _host.PostJsonAsync($"/api/v1/tasks/{task.Id}/unplan", HolderToken(),
            new { expectedVersion = task.Version });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal(TaskReasonCodes.UnplanNotAllowed, ReasonCode(body));
    }

    [Fact]
    public async Task Unplan_through_the_Task_Center_action_endpoint_reaches_the_same_handler()
    {
        var task = await SeedAsync(Holder);
        await PlanBlockAsync(task, Local(Monday, 10, 0), 60);

        var (status, _) = await _host.PostActionAsync(task.Id, "unplan", HolderToken(),
            new { expectedVersion = (await StoredAsync(task.Id)).Version });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Null((await StoredAsync(task.Id)).PlannedStartAt);
    }

    // ── B — only the holder plans (BL-449) ──────────────────────────────────

    [Fact]
    public async Task The_REQUESTER_planning_is_403_TASK_PLAN_NOT_HOLDER_and_their_row_offers_no_plan()
    {
        var task = await SeedAsync(Holder, creator: Requester);

        var (status, body) = await PlanBlockAsync(task, Local(Monday, 9, 0), 60, RequesterToken());

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal(TaskReasonCodes.PlanNotHolder, ReasonCode(body));
        var stored = await StoredAsync(task.Id);
        Assert.Equal(TaskLifecycle.Open, stored.Lifecycle);
        Assert.Null(stored.PlannedStartAt);

        // The projection says the same thing the handler does: the requester's (outbox) row has no `plan`…
        var requesterRow = await MyItemAsync(RequesterToken(), task.Id);
        Assert.DoesNotContain(Actions(requesterRow), code => code == "plan");
        // …and the holder's row still does (non-vacuity: the action exists, it just is not the requester's).
        Assert.Contains(Actions(await MyItemAsync(HolderToken(), task.Id)), code => code == "plan");
    }

    [Fact]
    public async Task The_REQUESTER_unplanning_is_403_TASK_PLAN_NOT_HOLDER()
    {
        var task = await SeedAsync(Holder, creator: Requester);
        await PlanBlockAsync(task, Local(Monday, 9, 0), 60);

        var (status, body) = await _host.PostJsonAsync($"/api/v1/tasks/{task.Id}/unplan", RequesterToken(),
            new { expectedVersion = (await StoredAsync(task.Id)).Version });

        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal(TaskReasonCodes.PlanNotHolder, ReasonCode(body));
        Assert.NotNull((await StoredAsync(task.Id)).PlannedStartAt);
    }

    // ── C — conflicts and warnings ──────────────────────────────────────────

    [Fact]
    public async Task Two_blocks_of_the_same_holder_overlapping_is_a_HARD_409_naming_the_other_block()
    {
        var first = await SeedAsync(Holder, title: "Rapor taslağı");
        var second = await SeedAsync(Holder, title: "Tedarikçi denetimi");
        Assert.Equal(HttpStatusCode.OK, (await PlanBlockAsync(first, Local(Monday, 10, 0), 60)).Status);

        var (status, body) = await PlanBlockAsync(second, Local(Monday, 10, 30), 60);

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal(TaskReasonCodes.PlanConflict, ReasonCode(body));
        var conflict = Data(body).GetProperty("conflict");
        Assert.Equal("Rapor taslağı", conflict.GetProperty("title").GetString());
        Assert.Equal(Local(Monday, 10, 0), conflict.GetProperty("startAt").GetDateTimeOffset());
        Assert.Equal(Local(Monday, 11, 0), conflict.GetProperty("endAt").GetDateTimeOffset());
        Assert.Null((await StoredAsync(second.Id)).PlannedStartAt);
        Assert.Equal(TaskLifecycle.Open, (await StoredAsync(second.Id)).Lifecycle);
    }

    [Fact]
    public async Task Back_to_back_blocks_do_not_conflict()
    {
        var first = await SeedAsync(Holder);
        var second = await SeedAsync(Holder);
        await PlanBlockAsync(first, Local(Monday, 10, 0), 60);

        var (status, _) = await PlanBlockAsync(second, Local(Monday, 11, 0), 60);

        Assert.Equal(HttpStatusCode.OK, status);
    }

    // ── CT acceptance (2026-09-29): the two conflict rules no test pinned — both sabotages stayed green ──────────

    [Fact]
    public async Task Moving_a_block_over_its_OWN_previous_hour_is_not_a_conflict()
    {
        // Drag-to-move on the calendar (2b) re-plans the same task; its old block must never be the "other" block.
        var task = await SeedAsync(Holder);
        Assert.Equal(HttpStatusCode.OK, (await PlanBlockAsync(task, Local(Monday, 10, 0), 60)).Status);

        var (status, body) = await PlanBlockAsync(task, Local(Monday, 10, 30), 60);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Null(ReasonCode(body));
        var stored = await StoredAsync(task.Id);
        Assert.Equal(Local(Monday, 10, 30).ToUniversalTime(), stored.PlannedStartAt!.Value.ToUniversalTime());
    }

    [Theory]
    [InlineData(TaskLifecycle.Done)]
    [InlineData(TaskLifecycle.Cancelled)]
    public async Task A_CLOSED_tasks_old_block_does_not_conflict(TaskLifecycle closed)
    {
        // Finished or cancelled work keeps its plan fields as history; it no longer occupies the hour.
        var old = new TaskItem
        {
            TenantId = _tenant,
            Title = "Closed with a block",
            AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
            AssigneeUserId = Holder,
            CreatedByUserId = Holder,
            OrganizationUnitId = Guid.NewGuid(),
            Lifecycle = closed,
            PlannedDate = Local(Monday, 0, 0),
            PlannedStartAt = Local(Monday, 10, 0).ToUniversalTime(),
            PlannedDurationMinutes = 60,
            DueAt = DateTimeOffset.UtcNow.AddDays(30),
            Version = 1
        };
        await _fixture.Database.GetCollection<TaskItem>(PlatformCollections.TaskItems).InsertOneAsync(old);
        var next = await SeedAsync(Holder);

        var (status, body) = await PlanBlockAsync(next, Local(Monday, 10, 0), 60);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Null(ReasonCode(body));
    }

    [Fact]
    public async Task The_Task_Center_action_endpoint_says_when_the_block_was_CUT_at_the_day_end()
    {
        var task = await SeedAsync(Holder);

        var (status, body) = await _host.PostActionAsync(task.Id, "plan", HolderToken(),
            new { expectedVersion = task.Version, plannedStartAt = Local(Monday, 17, 30), plannedDurationMinutes = 90 });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(30, (await StoredAsync(task.Id)).PlannedDurationMinutes);
        Assert.True(Data(body).GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public async Task The_edge_of_the_calendar_is_a_400_not_a_500()
    {
        var (status, body) = await _host.GetAsync("/api/v1/work/calendar?from=9999-12-20&to=9999-12-31", HolderToken());

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(WorkCalendarReasonCodes.RangeInvalid, ReasonCode(body));
    }

    [Fact]
    public async Task Day_only_plans_never_conflict_with_each_other_or_with_a_block()
    {
        var block = await SeedAsync(Holder);
        var dayOne = await SeedAsync(Holder);
        var dayTwo = await SeedAsync(Holder);
        await PlanBlockAsync(block, Local(Monday, 10, 0), 60);
        var day = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.FromHours(3));

        Assert.Equal(HttpStatusCode.OK, (await PlanDayAsync(dayOne, day)).Status);
        Assert.Equal(HttpStatusCode.OK, (await PlanDayAsync(dayTwo, day)).Status);
    }

    [Fact]
    public async Task Another_PERSONs_block_at_the_same_hour_is_not_a_conflict()
    {
        var mine = await SeedAsync(Holder);
        var theirs = await SeedAsync(Colleague);
        Assert.Equal(HttpStatusCode.OK,
            (await PlanBlockAsync(theirs, Local(Monday, 10, 0), 60, ColleagueToken())).Status);

        var (status, _) = await PlanBlockAsync(mine, Local(Monday, 10, 0), 60);

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public async Task Overlapping_an_ACCEPTED_or_PENDING_meeting_saves_the_plan_and_WARNS_naming_the_meeting()
    {
        await SeedMeetingAsync("Haftalık kalite", Local(Monday, 10, 0), Local(Monday, 11, 0), Holder, InvitationResponse.Accepted);
        await SeedMeetingAsync("Tedarik gözden geçirme", Local(Monday, 10, 30), Local(Monday, 11, 30), Holder, InvitationResponse.Pending);
        var task = await SeedAsync(Holder);

        var (status, body) = await PlanBlockAsync(task, Local(Monday, 10, 15), 30);

        Assert.Equal(HttpStatusCode.OK, status);
        var warnings = Warnings(body);
        Assert.Contains(warnings, w => w.Code == TaskPlanWarningCodes.OverlapsMeeting && w.Title == "Haftalık kalite");
        Assert.Contains(warnings, w => w.Code == TaskPlanWarningCodes.OverlapsMeeting && w.Title == "Tedarik gözden geçirme");
        Assert.NotNull((await StoredAsync(task.Id)).PlannedStartAt);
    }

    [Fact]
    public async Task A_DECLINED_meeting_does_not_warn()
    {
        await SeedMeetingAsync("Reddedilen", Local(Monday, 10, 0), Local(Monday, 11, 0), Holder, InvitationResponse.Declined);
        var task = await SeedAsync(Holder);

        var (status, body) = await PlanBlockAsync(task, Local(Monday, 10, 0), 30);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Empty(Warnings(body));
    }

    [Fact]
    public async Task A_block_outside_the_working_window_saves_and_WARNS()
    {
        var evening = await SeedAsync(Holder);
        var saturday = await SeedAsync(Holder);

        var (eveningStatus, eveningBody) = await PlanBlockAsync(evening, Local(Monday, 20, 0), 60);
        var (saturdayStatus, saturdayBody) = await PlanBlockAsync(saturday, Local(Monday.AddDays(5), 10, 0), 60);

        Assert.Equal(HttpStatusCode.OK, eveningStatus);
        Assert.Contains(Warnings(eveningBody), w => w.Code == TaskPlanWarningCodes.OutsideWorkingHours);
        Assert.Equal(60, (await StoredAsync(evening.Id)).PlannedDurationMinutes); // not cut — kept as asked
        Assert.Equal(HttpStatusCode.OK, saturdayStatus);
        Assert.Contains(Warnings(saturdayBody), w => w.Code == TaskPlanWarningCodes.OutsideWorkingHours);
    }

    [Fact]
    public async Task A_block_inside_the_working_window_carries_no_warning()
    {
        var task = await SeedAsync(Holder);

        var (status, body) = await PlanBlockAsync(task, Local(Monday, 9, 0), 60);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Empty(Warnings(body));
    }

    [Fact]
    public async Task The_Task_Center_action_endpoint_carries_the_block_and_passes_the_warnings_back()
    {
        var task = await SeedAsync(Holder);

        var (status, body) = await _host.PostActionAsync(task.Id, "plan", HolderToken(),
            new { expectedVersion = task.Version, plannedStartAt = Local(Monday, 20, 0), plannedDurationMinutes = 45 });

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(45, (await StoredAsync(task.Id)).PlannedDurationMinutes);
        Assert.Contains(Data(body).GetProperty("warnings").EnumerateArray(),
            w => w.GetProperty("code").GetString() == TaskPlanWarningCodes.OutsideWorkingHours);
    }

    [Fact]
    public async Task A_conflict_through_the_Task_Center_action_endpoint_still_names_the_other_block()
    {
        var first = await SeedAsync(Holder, title: "Rapor taslağı");
        var second = await SeedAsync(Holder);
        await PlanBlockAsync(first, Local(Monday, 10, 0), 60);

        var (status, body) = await _host.PostActionAsync(second.Id, "plan", HolderToken(),
            new { expectedVersion = second.Version, plannedStartAt = Local(Monday, 10, 30), plannedDurationMinutes = 30 });

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal(TaskReasonCodes.PlanConflict, ReasonCode(body));
        Assert.Equal("Rapor taslağı", Data(body).GetProperty("conflict").GetProperty("title").GetString());
    }

    // ── E — the calendar feed ───────────────────────────────────────────────

    [Fact]
    public async Task The_feed_returns_my_planned_work_my_meetings_the_windows_and_the_zone()
    {
        var block = await SeedAsync(Holder, title: "Blok iş", estimateHours: 3m);
        var dayPlan = await SeedAsync(Holder, title: "Gün işi");
        await SeedAsync(Holder, title: "Plansız iş");
        await PlanBlockAsync(block, Local(Monday, 9, 0), 60);
        await PlanDayAsync(dayPlan, new DateTimeOffset(2026, 10, 7, 0, 0, 0, TimeSpan.FromHours(3)));
        await SeedMeetingAsync("Kabul", Local(Monday, 14, 0), Local(Monday, 15, 0), Holder, InvitationResponse.Accepted);
        await SeedMeetingAsync("Bekleyen", Local(Monday, 15, 0), Local(Monday, 16, 0), Holder, InvitationResponse.Pending);

        var (status, body) = await _host.GetAsync("/api/v1/work/calendar?from=2026-10-05&to=2026-10-11", HolderToken());

        Assert.Equal(HttpStatusCode.OK, status);
        var data = Data(body);
        Assert.Equal("Europe/Istanbul", data.GetProperty("timeZoneId").GetString());

        var tasks = data.GetProperty("tasks").EnumerateArray().ToList();
        Assert.Equal(["Blok iş", "Gün işi"], tasks.Select(t => t.GetProperty("title").GetString()).ToArray());
        var blockRow = tasks[0];
        Assert.Equal("2026-10-05", blockRow.GetProperty("plannedDate").GetString());
        Assert.Equal(Local(Monday, 9, 0), blockRow.GetProperty("plannedStartAt").GetDateTimeOffset());
        Assert.Equal(Local(Monday, 10, 0), blockRow.GetProperty("plannedEndAt").GetDateTimeOffset());
        Assert.Equal(120, blockRow.GetProperty("remainingMinutes").GetInt32());
        Assert.False(blockRow.GetProperty("conflict").GetBoolean());
        Assert.Equal("2026-10-07", tasks[1].GetProperty("plannedDate").GetString());
        Assert.Equal(JsonValueKind.Null, tasks[1].GetProperty("plannedStartAt").ValueKind);

        var meetings = data.GetProperty("meetings").EnumerateArray()
            .Select(m => (m.GetProperty("title").GetString(), m.GetProperty("response").GetString())).ToList();
        Assert.Contains(("Kabul", "accepted"), meetings);
        Assert.Contains(("Bekleyen", "pending"), meetings);

        var days = data.GetProperty("days").EnumerateArray().ToList();
        Assert.Equal(7, days.Count);
        var monday = days[0];
        Assert.Equal(WorkingDayKinds.WorkingDay, monday.GetProperty("dayKind").GetString());
        Assert.Equal(WorkingHoursSources.TenantDefault, monday.GetProperty("resolvedFrom").GetString());
        var window = Assert.Single(monday.GetProperty("windows").EnumerateArray());
        Assert.Equal(Local(Monday, 9, 0), window.GetProperty("startAt").GetDateTimeOffset());
        Assert.Equal(Local(Monday, 18, 0), window.GetProperty("endAt").GetDateTimeOffset());
        Assert.Equal(WorkingDayKinds.Weekend, days[5].GetProperty("dayKind").GetString());
        Assert.Empty(days[5].GetProperty("windows").EnumerateArray());

        Assert.Equal(1, data.GetProperty("unplannedCount").GetInt32());
        Assert.True(data.GetProperty("pendingInviteCount").GetInt32() >= 0);
    }

    [Fact]
    public async Task A_holiday_is_named_and_has_no_window()
    {
        var (status, body) = await _host.GetAsync("/api/v1/work/calendar?from=2026-10-29&to=2026-10-29", HolderToken());

        Assert.Equal(HttpStatusCode.OK, status);
        var day = Assert.Single(Data(body).GetProperty("days").EnumerateArray());
        Assert.Equal(WorkingDayKinds.Holiday, day.GetProperty("dayKind").GetString());
        Assert.Equal(HolidayName, day.GetProperty("holidayName").GetString());
        Assert.Empty(day.GetProperty("windows").EnumerateArray());
    }

    [Fact]
    public async Task A_DECLINED_meeting_is_not_in_the_feed()
    {
        await SeedMeetingAsync("Kabul edilen", Local(Monday, 10, 0), Local(Monday, 11, 0), Holder, InvitationResponse.Accepted);
        await SeedMeetingAsync("Reddedilen", Local(Monday, 12, 0), Local(Monday, 13, 0), Holder, InvitationResponse.Declined);

        var (_, body) = await _host.GetAsync("/api/v1/work/calendar?from=2026-10-05&to=2026-10-05", HolderToken());

        var titles = Data(body).GetProperty("meetings").EnumerateArray().Select(m => m.GetProperty("title").GetString()).ToList();
        Assert.Contains("Kabul edilen", titles);
        Assert.DoesNotContain("Reddedilen", titles);
    }

    [Fact]
    public async Task Another_persons_planned_work_and_meetings_are_not_in_my_feed()
    {
        var theirs = await SeedAsync(Colleague, title: "Başkasının işi");
        await PlanBlockAsync(theirs, Local(Monday, 9, 0), 60, ColleagueToken());
        await SeedMeetingAsync("Başkasının toplantısı", Local(Monday, 10, 0), Local(Monday, 11, 0), Colleague, InvitationResponse.Accepted);

        var (_, body) = await _host.GetAsync("/api/v1/work/calendar?from=2026-10-05&to=2026-10-05", HolderToken());

        Assert.Empty(Data(body).GetProperty("tasks").EnumerateArray());
        Assert.Empty(Data(body).GetProperty("meetings").EnumerateArray());
    }

    [Fact]
    public async Task ANOTHER_TENANTs_token_sees_zero_records_for_the_same_user_id()
    {
        var task = await SeedAsync(Holder, title: "Kiracı A işi");
        await PlanBlockAsync(task, Local(Monday, 9, 0), 60);
        await SeedMeetingAsync("Kiracı A toplantısı", Local(Monday, 10, 0), Local(Monday, 11, 0), Holder, InvitationResponse.Accepted);

        // Non-vacuity: the owning tenant sees both.
        var (_, own) = await _host.GetAsync("/api/v1/work/calendar?from=2026-10-05&to=2026-10-05", HolderToken());
        Assert.Single(Data(own).GetProperty("tasks").EnumerateArray());
        Assert.Single(Data(own).GetProperty("meetings").EnumerateArray());

        var foreign = _host.Token(Holder, _otherTenant);
        var (status, body) = await _host.GetAsync("/api/v1/work/calendar?from=2026-10-05&to=2026-10-05", foreign);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Empty(Data(body).GetProperty("tasks").EnumerateArray());
        Assert.Empty(Data(body).GetProperty("meetings").EnumerateArray());
        Assert.Equal(0, Data(body).GetProperty("unplannedCount").GetInt32());
        Assert.Equal(0, Data(body).GetProperty("pendingInviteCount").GetInt32());
    }

    [Theory]
    [InlineData("from=2026-10-01&to=2026-11-12")] // 43 days
    [InlineData("from=2026-10-10&to=2026-10-01")] // inverted
    [InlineData("from=2026-10-01")]               // missing end
    public async Task A_range_over_42_days_inverted_or_incomplete_is_400_WORK_CALENDAR_RANGE_INVALID(string query)
    {
        var (status, body) = await _host.GetAsync($"/api/v1/work/calendar?{query}", HolderToken());

        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(WorkCalendarReasonCodes.RangeInvalid, ReasonCode(body));
    }

    [Fact]
    public async Task Exactly_42_days_is_accepted()
    {
        var (status, body) = await _host.GetAsync("/api/v1/work/calendar?from=2026-10-01&to=2026-11-11", HolderToken());

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(42, Data(body).GetProperty("days").GetArrayLength());
    }

    [Fact]
    public async Task The_feed_counts_plans_whose_day_has_passed()
    {
        var passed = await SeedAsync(Holder);
        var today = DateTimeOffset.UtcNow;
        await PlanDayAsync(passed, new DateTimeOffset(today.Year, today.Month, today.Day, 0, 0, 0, TimeSpan.Zero).AddDays(-3));

        var (_, body) = await _host.GetAsync("/api/v1/work/calendar?from=2026-10-05&to=2026-10-05", HolderToken());

        Assert.Equal(1, Data(body).GetProperty("planPassedCount").GetInt32());
    }

    // ── D — the tenant default hours persist (TimeOnly round trip) ──────────

    [Fact]
    public async Task The_tenant_default_hours_round_trip_through_Mongo_and_an_OLD_record_reads_the_defaults()
    {
        var tenants = _fixture.Database.GetCollection<Tenant>(PlatformCollections.Tenants);
        var custom = NewTenant(Guid.NewGuid(), "UTC");
        custom.DefaultWorkdayStart = new TimeOnly(8, 30);
        custom.DefaultWorkdayEnd = new TimeOnly(17, 15);
        await tenants.InsertOneAsync(custom);

        var read = await tenants.Find(t => t.Id == custom.Id).SingleAsync();
        Assert.Equal(new TimeOnly(8, 30), read.DefaultWorkdayStart);
        Assert.Equal(new TimeOnly(17, 15), read.DefaultWorkdayEnd);

        // A record written before the fields existed: strip them and read it back.
        var raw = _fixture.Database.GetCollection<BsonDocument>(PlatformCollections.Tenants);
        await raw.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(custom.Id, GuidRepresentation.Standard)),
            Builders<BsonDocument>.Update.Unset(nameof(Tenant.DefaultWorkdayStart)).Unset(nameof(Tenant.DefaultWorkdayEnd)));
        var old = await tenants.Find(t => t.Id == custom.Id).SingleAsync();
        Assert.Equal(new Tenant { Code = "x", Slug = "x", Name = "x", DisplayName = "x", Domain = "x" }.DefaultWorkdayStart,
            old.DefaultWorkdayStart);
        Assert.Equal(new Tenant { Code = "x", Slug = "x", Name = "x", DisplayName = "x", Domain = "x" }.DefaultWorkdayEnd,
            old.DefaultWorkdayEnd);
    }

    [Fact]
    public async Task Changing_the_tenant_window_moves_the_feed_and_the_plan_rule_together()
    {
        // The seam is real: a later start makes 09:00 OUTSIDE, with no other code change.
        var tenants = _fixture.Database.GetCollection<Tenant>(PlatformCollections.Tenants);
        await tenants.UpdateOneAsync(t => t.Id == _tenant,
            Builders<Tenant>.Update.Set(t => t.DefaultWorkdayStart, new TimeOnly(10, 0)));
        var task = await SeedAsync(Holder);

        var (_, planBody) = await PlanBlockAsync(task, Local(Monday, 9, 0), 30);
        var (_, feedBody) = await _host.GetAsync("/api/v1/work/calendar?from=2026-10-05&to=2026-10-05", HolderToken());

        Assert.Contains(Warnings(planBody), w => w.Code == TaskPlanWarningCodes.OutsideWorkingHours);
        var window = Assert.Single(Data(feedBody).GetProperty("days")[0].GetProperty("windows").EnumerateArray());
        Assert.Equal(Local(Monday, 10, 0), window.GetProperty("startAt").GetDateTimeOffset());
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>A local Istanbul wall-clock time on <paramref name="day"/>, as a UTC instant.</summary>
    private static DateTimeOffset Local(DateOnly day, int hour, int minute)
        => new DateTimeOffset(day.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(3)).ToUniversalTime();

    private static Tenant NewTenant(Guid id, string zone) => new()
    {
        Id = id,
        Code = "T" + id.ToString("N")[..8],
        Slug = "t-" + id.ToString("N")[..8],
        Name = "Plan tenant",
        DisplayName = "Plan tenant",
        Domain = id.ToString("N")[..8] + ".example",
        Country = "TR",
        DefaultTimezone = zone,
        Settings = new TenantSettings { Timezone = zone }
    };

    private string HolderToken() => _host.Token(Holder, _tenant, TaskPermissions.Update, TaskPermissions.Read);
    private string RequesterToken() => _host.Token(Requester, _tenant, TaskPermissions.Update, TaskPermissions.Read);
    private string ColleagueToken() => _host.Token(Colleague, _tenant, TaskPermissions.Update, TaskPermissions.Read);

    private async Task<TaskItem> SeedAsync(
        Guid assignee, Guid? creator = null, string title = "Plan probe", decimal? estimateHours = null)
    {
        var task = new TaskItem
        {
            TenantId = _tenant,
            Title = title,
            AssignmentTarget = creator is null || creator == assignee
                ? TaskAssignmentTarget.SelfAssigned
                : TaskAssignmentTarget.Person,
            AssigneeUserId = assignee,
            CreatedByUserId = creator ?? assignee,
            OrganizationUnitId = Guid.NewGuid(),
            Lifecycle = TaskLifecycle.Open,
            EstimateHours = estimateHours,
            DueAt = DateTimeOffset.UtcNow.AddDays(30),
            Version = 1
        };
        await _fixture.Database.GetCollection<TaskItem>(PlatformCollections.TaskItems).InsertOneAsync(task);
        return task;
    }

    private async Task SeedMeetingAsync(
        string title, DateTimeOffset start, DateTimeOffset end, Guid attendee, InvitationResponse response)
    {
        var meeting = new Meeting
        {
            TenantId = _tenant,
            Title = title,
            MeetingTypeId = Guid.NewGuid(),
            StartAt = start,
            EndAt = end,
            OrganizerUserId = Guid.NewGuid(),
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };
        await _fixture.Database.GetCollection<Meeting>(PlatformCollections.MeetingMeetings).InsertOneAsync(meeting);
        await _fixture.Database.GetCollection<MeetingAttendee>(PlatformCollections.MeetingAttendees).InsertOneAsync(
            new MeetingAttendee { TenantId = _tenant, MeetingId = meeting.Id, UserId = attendee, InvitationResponse = response });
    }

    private Task<(HttpStatusCode Status, string Body)> PlanBlockAsync(
        TaskItem task, DateTimeOffset start, int? minutes = null, string? token = null)
        => PlanWithFreshVersionAsync(task, token ?? HolderToken(), version => minutes is null
            ? new { expectedVersion = version, plannedStartAt = start }
            : (object)new { expectedVersion = version, plannedStartAt = start, durationMinutes = minutes });

    private Task<(HttpStatusCode Status, string Body)> PlanDayAsync(TaskItem task, DateTimeOffset day)
        => PlanWithFreshVersionAsync(task, HolderToken(), version => new { expectedVersion = version, plannedDate = day });

    private async Task<(HttpStatusCode Status, string Body)> PlanWithFreshVersionAsync(
        TaskItem task, string token, Func<int, object> body)
    {
        var version = (await StoredAsync(task.Id)).Version;
        return await _host.PostJsonAsync($"/api/v1/tasks/{task.Id}/plan", token, body(version));
    }

    private Task<TaskItem> StoredAsync(Guid id)
        => _fixture.Database.GetCollection<TaskItem>(PlatformCollections.TaskItems).Find(t => t.Id == id).SingleAsync();

    private Task<BsonDocument> RawAsync(Guid id)
        => _fixture.Database.GetCollection<BsonDocument>(PlatformCollections.TaskItems)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(id, GuidRepresentation.Standard)))
            .SingleAsync();

    private async Task<JsonElement> MyItemAsync(string token, Guid taskId)
    {
        var (status, body) = await _host.GetAsync("/api/v1/work-items/mine", token);
        Assert.Equal(HttpStatusCode.OK, status);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == taskId.ToString())
            .Clone();
    }

    private static IReadOnlyList<string?> Actions(JsonElement item)
        => item.GetProperty("actions").EnumerateArray().Select(a => a.GetProperty("code").GetString()).ToList();

    private static JsonElement Data(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("data").Clone();
    }

    private static IReadOnlyList<(string? Code, string? Title)> Warnings(string body)
        => Data(body).GetProperty("warnings").EnumerateArray()
            .Select(w => (Code: w.GetProperty("code").GetString(),
                Title: w.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String
                    ? title.GetString()
                    : null))
            .ToList();

    private static string? ReasonCode(string body)
    {
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("reason_code", out var code) ? code.GetString() : null;
    }

    /// <summary>Closes weekends and one named holiday; every other day is a working day. Resolved throughout.</summary>
    private sealed class StubWorkingCalendar : IWorkingCalendarProvider
    {
        public Task<WorkingDayResult> IsWorkingDayAsync(DateOnly date, WorkingCalendarScope scope, CancellationToken ct = default)
        {
            var holiday = date == Holiday
                ? new HolidayInfo(Guid.NewGuid(), "CUMHURIYET", HolidayName, date, date, "PublicHoliday", false, false)
                : null;
            var working = holiday is null && date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
            return Task.FromResult(new WorkingDayResult(
                WorkingCalendarResolution.Resolved, working, date, scope.CountryCode, Guid.NewGuid(), null, holiday,
                "stub", Array.Empty<string>()));
        }

        public Task<HolidayLookupResult> GetHolidayAsync(DateOnly date, WorkingCalendarScope scope, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<WorkingDateResult> NextWorkingDayAsync(DateOnly date, WorkingCalendarScope scope, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<WorkingDateResult> AddWorkingDaysAsync(DateOnly start, int days, WorkingCalendarScope scope, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<WorkingDayCountResult> WorkingDaysBetweenAsync(DateOnly from, DateOnly to, WorkingCalendarScope scope, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class Host : IDisposable
    {
        private const string Issuer = "diten-auth-calendar-engine";
        private const string Audience = "diten-platform-calendar-engine";
        private const string Secret = "WP-TASK-CALENDAR-ENGINE-01 signing key, test only, 0123456789abcdef0123";

        private readonly TestServer _server;

        public Host(IPlatformDbContext db)
        {
            var builder = new WebHostBuilder()
                .UseEnvironment("Test")
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                        .AddJwtBearer(options =>
                        {
                            options.MapInboundClaims = false;
                            options.TokenValidationParameters = new TokenValidationParameters
                            {
                                ValidateIssuer = true,
                                ValidateAudience = true,
                                ValidateLifetime = true,
                                ValidateIssuerSigningKey = true,
                                ValidIssuer = Issuer,
                                ValidAudience = Audience,
                                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                                ClockSkew = TimeSpan.Zero
                            };
                        });
                    services.AddAuthorization();
                    services.AddHttpContextAccessor();

                    services.AddScoped<ITenantContext, TenantContext>();
                    services.AddScoped<ICurrentUserContext, CurrentUserContext>();
                    services.AddScoped<ICorrelationContext>(_ =>
                    {
                        var correlation = new CorrelationContext();
                        correlation.SetCorrelationId("corr-calendar");
                        return correlation;
                    });
                    services.AddScoped<IActorPermissionContext>(sp =>
                        new ClaimsActorPermissionContext(sp.GetRequiredService<IHttpContextAccessor>()));

                    // The REAL stores, on the disposable database, in the request's tenant.
                    services.AddScoped<ITaskTransitionRepository>(sp =>
                        new TaskTransitionRepository(db, sp.GetRequiredService<ITenantContext>()));
                    services.AddScoped<ITaskItemRepository>(sp => new TaskItemRepository(
                        db, sp.GetRequiredService<ITenantContext>(), sp.GetRequiredService<ITaskTransitionRepository>()));
                    services.AddScoped<IMeetingRepository>(sp => new MeetingRepository(db, sp.GetRequiredService<ITenantContext>()));
                    services.AddScoped<IMeetingAttendeeRepository>(sp =>
                        new MeetingAttendeeRepository(db, sp.GetRequiredService<ITenantContext>()));
                    services.AddScoped<ITenantRegistryRepository>(sp =>
                        new TenantRegistryRepository(db, sp.GetRequiredService<ITenantContext>()));

                    // The REAL seam and the REAL meeting reader; only the org chart and the calendar are stubs.
                    services.AddScoped<ICalendarMeetingReader>(sp => new CalendarMeetingReader(
                        sp.GetRequiredService<IMeetingAttendeeRepository>(), sp.GetRequiredService<IMeetingRepository>()));
                    services.AddScoped<IWorkingHoursProvider>(sp => new WorkingHoursProvider(
                        sp.GetRequiredService<ITenantRegistryRepository>(),
                        sp.GetRequiredService<ITenantContext>(),
                        new FakePositionAssignmentRepository(),
                        new FakePositionRepository(),
                        new FakeOrganizationUnitRepository(),
                        new StubWorkingCalendar(),
                        Array.Empty<IWorkingHoursRing>()));

                    services.AddScoped<IEnumerable<IWorkItemProvider>>(sp => new IWorkItemProvider[]
                    {
                        new TaskWorkItemProvider(
                            sp.GetRequiredService<ITaskItemRepository>(),
                            new FakePositionAssignmentRepository(),
                            new TaskLifecycleService(),
                            new TaskAssignmentResolver(),
                            new FakeUserDisplayNameResolver(),
                            new FakeChecklistRunRepository(),
                            new FakeTaskApprovalService(),
                            new FakeTaskDependencyRepository(),
                            new FakeTaskCommentRepository(),
                            sp.GetRequiredService<ITaskTransitionRepository>(),
                            new FakeTaskPersonalOverlayRepository(),
                            new FakeTaskWatcherRepository(),
                            sp.GetRequiredService<IActorPermissionContext>(),
                            new FakePositionRepository(),
                            new FakeOrganizationUnitRepository(),
                            SlaForTests.Real(),
                            new FakeTaskFieldDefinitionRepository(),
                            new FakeTaskTypeRepository())
                    });
                    services.AddScoped<IMediator>(sp => new Router(sp));
                    services.AddScoped<IEnumerable<IWorkItemActionDispatcher>>(sp =>
                        new IWorkItemActionDispatcher[] { new TaskWorkItemActionDispatcher(sp.GetRequiredService<IMediator>()) });
                    services.AddControllers().AddApplicationPart(typeof(WorkCalendarController).Assembly);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseTenantResolution();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });

            _server = new TestServer(builder);
        }

        public string Token(Guid user, Guid tenant, params string[] keys)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.ToString()),
                new(JwtRegisteredClaimNames.Email, $"{user:N}@tenant.example"),
                new("tenant_id", tenant.ToString()),
                new("actor_type", "tenant_user")
            };
            claims.AddRange(keys.Select(key => new Claim("permission", key)));

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)), SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public async Task<(HttpStatusCode Status, string Body)> GetAsync(string path, string bearer)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            var response = await _server.CreateClient().SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        public async Task<(HttpStatusCode Status, string Body)> PostJsonAsync(string path, string bearer, object body)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            var response = await _server.CreateClient().SendAsync(request);
            return (response.StatusCode, await response.Content.ReadAsStringAsync());
        }

        public Task<(HttpStatusCode Status, string Body)> PostActionAsync(
            Guid itemId, string actionCode, string bearer, object payload)
            => PostJsonAsync($"/api/v1/work-items/{itemId}/actions/{actionCode}", bearer, new { providerCode = "tasks", payload });

        public void Dispose() => _server.Dispose();
    }

    /// <summary>Routes each request this flow sends to its REAL handler, built over the request's own services.</summary>
    private sealed class Router(IServiceProvider services) : IMediator
    {
        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
        {
            var tasks = services.GetRequiredService<ITaskItemRepository>();
            var currentUser = services.GetRequiredService<ICurrentUserContext>();
            object response = request switch
            {
                PlanTaskItemCommand command => await new PlanTaskItemHandler(
                    tasks, new TaskLifecycleService(), currentUser,
                    services.GetRequiredService<IWorkingHoursProvider>(),
                    services.GetRequiredService<ICalendarMeetingReader>()).Handle(command, ct),
                UnplanTaskItemCommand command => await new UnplanTaskItemHandler(
                    tasks, new TaskLifecycleService(), currentUser).Handle(command, ct),
                GetMyWorkCalendarQuery query => await new GetMyWorkCalendarHandler(
                    tasks, new TaskLifecycleService(),
                    services.GetRequiredService<IWorkingHoursProvider>(),
                    services.GetRequiredService<ICalendarMeetingReader>(),
                    services.GetRequiredService<IMeetingAttendeeRepository>(),
                    services.GetRequiredService<IMeetingRepository>(),
                    currentUser).Handle(query, ct),
                GetMyWorkItemsQuery query => await new GetMyWorkItemsHandler(
                    services.GetRequiredService<IEnumerable<IWorkItemProvider>>(),
                    currentUser,
                    Options.Create(new WorkAggregationResilienceOptions()),
                    NullLogger<GetMyWorkItemsHandler>.Instance).Handle(query, ct),
                _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}.")
            };
            return (TResponse)response;
        }

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => throw new NotSupportedException();
    }
}
