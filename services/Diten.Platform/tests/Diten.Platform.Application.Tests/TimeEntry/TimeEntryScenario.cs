using System.Net;
using System.Text.Json;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;
using TimeEntryRow = Diten.Platform.Domain.Entities.TimeEntry.TimeEntry;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// The world every MOD-0280-FU01 HTTP test starts from: two tenants (so isolation is always testable), and inside the
/// main one an org chart — person → manager → grand-manager, all in one unit of one legal entity — plus a time-admin
/// pool seat, a category and two tasks. Every test gets FRESH tenant ids, so tests never see each other's rows even
/// though they share one disposable mongod.
///
/// <para>The clock is Wednesday 2026-10-07 12:00 in Istanbul (09:00Z): the current ISO week is 2026-W41 (Monday
/// 2026-10-05); the edit window is W37…W41.</para>
/// </summary>
public abstract class TimeEntryScenario : IAsyncLifetime
{
    protected const string Zone = "Europe/Istanbul";
    protected const string CurrentWeek = "2026-W41";
    protected static readonly DateOnly Monday = new(2026, 10, 5);
    protected static readonly DateTimeOffset Wednesday = new(2026, 10, 7, 9, 0, 0, TimeSpan.Zero);
    protected const string Category = "ADMINISTRATION";

    protected readonly TimeEntryMongoFixture Fixture;
    protected TimeEntryHost Host = null!;

    protected readonly Guid Tenant = Guid.NewGuid();
    protected readonly Guid OtherTenant = Guid.NewGuid();
    protected readonly Guid Person = Guid.NewGuid();
    protected readonly Guid Manager = Guid.NewGuid();
    protected readonly Guid GrandManager = Guid.NewGuid();
    protected readonly Guid PoolAdmin = Guid.NewGuid();
    protected readonly Guid SecondPerson = Guid.NewGuid();
    protected readonly Guid Stranger = Guid.NewGuid();
    protected readonly Guid LegalEntity = Guid.NewGuid();
    protected readonly Guid Unit = Guid.NewGuid();
    protected readonly Guid PersonSeat = Guid.NewGuid();
    protected readonly Guid ManagerSeat = Guid.NewGuid();
    protected readonly Guid GrandSeat = Guid.NewGuid();
    protected readonly Guid PoolSeat = Guid.NewGuid();
    protected readonly Guid TaskA = Guid.NewGuid();
    protected readonly Guid TaskB = Guid.NewGuid();

    protected TimeEntryScenario(TimeEntryMongoFixture fixture) => Fixture = fixture;

    public virtual async Task InitializeAsync()
    {
        Host = new TimeEntryHost(HostDbContext, ConfigureHost);
        Host.Clock.UtcNow = Wednesday;
        Host.Names.Names[Person] = "Ayşe Yılmaz";
        Host.Names.Names[Manager] = "Mehmet Kaya";

        await Collection<Tenant>(PlatformCollections.Tenants).InsertManyAsync(
        [
            NewTenant(Tenant, Zone), NewTenant(OtherTenant, Zone)
        ]);
        await SeedOrgAsync();
        await SeedCategoryAsync(Tenant, Category);
        await SeedTaskAsync(Tenant, TaskA);
        await SeedTaskAsync(Tenant, TaskB);
    }

    /// <summary>BL-484 — the store the host talks to. The same disposable database; a test may hand the host a client that
    /// counts its commands (<see cref="TimeEntryMongoFixture.ObservedDbContext"/>).</summary>
    protected virtual Infrastructure.Persistence.IPlatformDbContext HostDbContext => Fixture.DbContext;

    /// <summary>T2a — a test's extra wiring, applied after the module's own registration.</summary>
    protected virtual void ConfigureHost(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
    }

    public Task DisposeAsync()
    {
        Host.Dispose();
        return Task.CompletedTask;
    }

    // ── World building ───────────────────────────────────────────────────────────────────────────────────────────

    protected virtual async Task SeedOrgAsync()
    {
        await Collection<OrganizationUnit>(PlatformCollections.OrganizationUnits).InsertOneAsync(new OrganizationUnit
        {
            Id = Unit, TenantId = Tenant, Code = "U" + Unit.ToString("N")[..6], Name = "Engineering", LegalEntityId = LegalEntity
        });

        await SeedPositionAsync(GrandSeat, reportsTo: null);
        await SeedPositionAsync(ManagerSeat, reportsTo: GrandSeat);
        await SeedPositionAsync(PersonSeat, reportsTo: ManagerSeat);
        await SeedPositionAsync(PoolSeat, reportsTo: null);

        await SeatAsync(Person, PersonSeat);
        await SeatAsync(Manager, ManagerSeat);
        await SeatAsync(GrandManager, GrandSeat);
        await SeatAsync(PoolAdmin, PoolSeat);
    }

    protected Task SeedPositionAsync(Guid id, Guid? reportsTo, Guid? tenant = null)
        => Collection<Position>(PlatformCollections.Positions).InsertOneAsync(new Position
        {
            Id = id,
            TenantId = tenant ?? Tenant,
            Code = "P" + id.ToString("N")[..8],
            Name = "Seat " + id.ToString("N")[..4],
            OrganizationUnitId = Unit,
            ReportsToPositionId = reportsTo
        });

    protected Task SeatAsync(Guid user, Guid position, Guid? tenant = null, AssignmentType type = AssignmentType.Primary)
        => Collection<PositionAssignment>(PlatformCollections.PositionAssignments).InsertOneAsync(new PositionAssignment
        {
            TenantId = tenant ?? Tenant,
            PositionId = position,
            UserId = user,
            EffectiveFrom = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            AssignmentType = type
        });

    protected Task UnseatAllAsync(Guid position)
        => Collection<PositionAssignment>(PlatformCollections.PositionAssignments).UpdateManyAsync(
            a => a.PositionId == position && a.TenantId == Tenant,
            Builders<PositionAssignment>.Update.Set(a => a.IsCancelled, true));

    protected Task SetReportsToAsync(Guid position, Guid? reportsTo)
        => Collection<Position>(PlatformCollections.Positions).UpdateOneAsync(
            p => p.Id == position,
            Builders<Position>.Update.Set(p => p.ReportsToPositionId, reportsTo));

    protected Task SeedCategoryAsync(Guid tenant, string code, bool active = true)
        => Collection<WorkCategory>(PlatformCollections.TimeEntryWorkCategories).InsertOneAsync(new WorkCategory
        {
            TenantId = tenant, Code = code, LabelText = code, CountsAsWork = true, SortOrder = 10, IsActive = active
        });

    protected Task SeedTaskAsync(
        Guid tenant, Guid id, Guid? assignee = null, Guid? creator = null, TaskLifecycle lifecycle = TaskLifecycle.InProgress,
        decimal spentHoursDecoy = 0m)
        => Collection<TaskItem>(PlatformCollections.TaskItems).InsertOneAsync(new TaskItem
        {
            Id = id,
            TenantId = tenant,
            Title = "Task " + id.ToString("N")[..4],
            AssignmentTarget = TaskAssignmentTarget.Person,
            AssigneeUserId = assignee ?? Person,
            CreatedByUserId = creator ?? Person,
            OrganizationUnitId = Unit,
            Lifecycle = lifecycle,
            DueAt = Wednesday.AddDays(30),
            // A DECOY for the D7 read-site tests: nothing writes this field in production, so any read site that still
            // reads it shows this number instead of the approved total.
            SpentHours = spentHoursDecoy,
            Version = 1
        });

    protected async Task SetPoolAsync(Guid? poolPosition)
    {
        var result = await Host.PutAsync("/api/v1/time-entry/settings", AdminToken(), new
        {
            expectedVersion = await SettingsVersionAsync(), timeAdminPoolPositionId = poolPosition
        });
        Assert.Equal(HttpStatusCode.OK, result.Status);
    }

    private async Task<int> SettingsVersionAsync()
        => (await Host.GetAsync("/api/v1/time-entry/settings", AdminToken())).Data.GetProperty("version").GetInt32();

    private static Tenant NewTenant(Guid id, string zone) => new()
    {
        Id = id,
        Code = "T" + id.ToString("N")[..8],
        Slug = "t-" + id.ToString("N")[..8],
        Name = "Time tenant",
        DisplayName = "Time tenant",
        Domain = id.ToString("N")[..8] + ".example",
        Country = "TR",
        DefaultTimezone = zone,
        Settings = new TenantSettings { Timezone = zone }
    };

    protected Task SetTenantZoneAsync(string zone)
        => Collection<Tenant>(PlatformCollections.Tenants).UpdateOneAsync(
            t => t.Id == Tenant,
            Builders<Tenant>.Update.Set(t => t.DefaultTimezone, zone).Set(t => t.Settings.Timezone, zone));

    // ── Tokens ───────────────────────────────────────────────────────────────────────────────────────────────────

    protected string PersonToken(Guid? tenant = null, Guid? person = null)
        => Host.Token(person ?? Person, tenant ?? Tenant, TimeEntryPermissions.TimesheetsRead, TimeEntryPermissions.TimesheetsUpdate);

    protected string ApproverToken(Guid approver, Guid? tenant = null)
        => Host.Token(approver, tenant ?? Tenant,
            TimeEntryPermissions.ApprovalsRead, WorkflowPermissions.TasksApprove, WorkflowPermissions.TasksReject);

    protected string AdminToken(Guid? tenant = null)
        => Host.Token(PoolAdmin, tenant ?? Tenant,
            TimeEntryPermissions.SettingsManage, TimeEntryPermissions.CategoriesManage, TimeEntryPermissions.WeeksReopen,
            TimeEntryPermissions.TimesheetsRead);

    // ── Operations ───────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>One row of a save. Every row names its source (v2 F1); a person-typed row by default.</summary>
    protected static object Row(DateOnly date, int minutes, Guid? task = null, string? category = null, string? note = null,
        string source = "Manual", string? sourceRef = null)
        => new
        {
            localDate = date.ToString("yyyy-MM-dd"), taskItemId = task, categoryCode = task is null ? category ?? Category : null,
            durationMinutes = minutes, note, source, sourceRef
        };

    protected Task<ApiResult> GetWeekAsync(string weekKey = CurrentWeek, string? token = null)
        => Host.GetAsync($"/api/v1/time-entry/weeks/{weekKey}", token ?? PersonToken());

    protected Task<ApiResult> SaveAsync(int expectedVersion, string weekKey = CurrentWeek, string? token = null, params object[] rows)
        => Host.PutAsync($"/api/v1/time-entry/weeks/{weekKey}/entries", token ?? PersonToken(),
            new { expectedVersion, entries = rows });

    protected async Task<ApiResult> SaveFreshAsync(string weekKey = CurrentWeek, params object[] rows)
        => await SaveAsync(await VersionAsync(weekKey), weekKey, null, rows);

    protected async Task<int> VersionAsync(string weekKey = CurrentWeek, string? token = null)
    {
        var week = await GetWeekAsync(weekKey, token);
        Assert.Equal(HttpStatusCode.OK, week.Status);
        return week.Data.GetProperty("version").GetInt32();
    }

    protected async Task<ApiResult> SubmitAsync(string weekKey = CurrentWeek, string? token = null)
        => await Host.PostAsync($"/api/v1/time-entry/weeks/{weekKey}/submit", token ?? PersonToken(),
            new { expectedVersion = await VersionAsync(weekKey, token) });

    protected async Task<ApiResult> WithdrawAsync(string weekKey = CurrentWeek)
        => await Host.PostAsync($"/api/v1/time-entry/weeks/{weekKey}/withdraw", PersonToken(),
            new { expectedVersion = await VersionAsync(weekKey) });

    /// <summary>Saves one row and submits — the shortest way to a submitted week. Returns the week id.</summary>
    protected async Task<Guid> SubmittedWeekAsync(params object[] rows)
    {
        var saved = await SaveFreshAsync(CurrentWeek, rows.Length == 0 ? [Row(Monday, 120, TaskA)] : rows);
        Assert.Equal(HttpStatusCode.OK, saved.Status);
        var submitted = await SubmitAsync();
        Assert.True(submitted.Status == HttpStatusCode.OK, submitted.ToString());
        return submitted.Data.GetProperty("weekId").GetGuid();
    }

    /// <summary>The approver acts on MOD-0023's OWN route — this module has no approve endpoint.</summary>
    protected async Task<ApiResult> DecideAsync(Guid approver, Guid weekId, bool approve, string? comment = null)
    {
        var week = await StoredWeekAsync(weekId);
        Assert.NotNull(week.WorkflowInstanceId);
        return await DecideInstanceAsync(approver, week.WorkflowInstanceId!.Value, approve, comment);
    }

    /// <summary>Decides a MOD-0023 instance directly — also one no week records (the F1 stray instance).</summary>
    protected async Task<ApiResult> DecideInstanceAsync(Guid approver, Guid instanceId, bool approve, string? comment = null)
    {
        var task = await Collection<ApprovalTask>(PlatformCollections.ApprovalTasks)
            .Find(t => t.WorkflowInstanceId == instanceId && t.Status == ApprovalTaskStatus.WaitingApproval)
            .SingleAsync();
        return await Host.PostAsync(
            $"/api/v1/workflow/tasks/{task.Id}/{(approve ? "approve" : "reject")}",
            ApproverToken(approver),
            new { actorId = approver.ToString(), reasonCode = approve ? "OK" : "NOT_OK", idempotencyKey = Guid.NewGuid().ToString("N"), comment, evidenceRef = (string?)null });
    }

    /// <summary>F4 — a time admin reopens (person, week); 0 when the week has no revision yet.</summary>
    protected Task<ApiResult> ReopenAsync(Guid person, string weekKey, int expectedVersion, string? reason, string? token = null)
        => Host.PostAsync("/api/v1/time-entry/admin/weeks/reopen", token ?? AdminToken(),
            new { userId = person, weekKey, expectedVersion, reason });

    /// <summary>Sets fields on a stored document by its id, bypassing every rule — how a test stages a state MOD-0023 or a
    /// crash would leave behind. Raw BSON, so it compiles whether or not a field exists on the C# type yet.</summary>
    protected Task RawSetAsync(string collection, Guid id, params (string Field, BsonValue Value)[] sets)
        => Fixture.Database.GetCollection<BsonDocument>(collection).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(id, GuidRepresentation.Standard)),
            Builders<BsonDocument>.Update.Combine(sets.Select(x => Builders<BsonDocument>.Update.Set(x.Field, x.Value))));

    protected Task RawUnsetAsync(string collection, Guid id, string field)
        => Fixture.Database.GetCollection<BsonDocument>(collection).UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(id, GuidRepresentation.Standard)),
            Builders<BsonDocument>.Update.Unset(field));

    // ── Reading the store directly (the assertion side) ──────────────────────────────────────────────────────────

    protected IMongoCollection<T> Collection<T>(string name) => Fixture.Database.GetCollection<T>(name);

    protected Task<TimesheetWeek> StoredWeekAsync(Guid id)
        => Collection<TimesheetWeek>(PlatformCollections.TimeEntryTimesheetWeeks).Find(w => w.Id == id).SingleAsync();

    protected Task<List<TimesheetWeek>> StoredWeeksAsync(Guid? tenant = null)
        => Collection<TimesheetWeek>(PlatformCollections.TimeEntryTimesheetWeeks)
            .Find(w => w.TenantId == (tenant ?? Tenant)).SortBy(w => w.RevisionNumber).ToListAsync();

    protected Task<List<TimeEntryRow>> StoredEntriesAsync(Guid weekId)
        => Collection<TimeEntryRow>(PlatformCollections.TimeEntryEntries).Find(e => e.TimesheetWeekId == weekId && !e.IsDeleted).ToListAsync();

    protected async Task<int?> ApprovedMinutesAsync(Guid taskId)
        => (await Collection<TaskTimeTotal>(PlatformCollections.TimeEntryTaskTotals)
            .Find(t => t.TenantId == Tenant && t.TaskItemId == taskId).FirstOrDefaultAsync())?.ApprovedMinutes;

    protected Task<WorkflowInstance> StoredInstanceAsync(Guid id)
        => Collection<WorkflowInstance>(PlatformCollections.WorkflowInstances).Find(i => i.Id == id).SingleAsync();

    protected static IReadOnlyList<string> Dates(JsonElement array)
        => array.EnumerateArray().Select(d => d.GetString()!).ToList();
}
