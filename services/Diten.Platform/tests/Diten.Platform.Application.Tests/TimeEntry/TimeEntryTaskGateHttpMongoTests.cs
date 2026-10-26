using System.Net;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// BL-484 (CT review L1) — the batched task read rule is also the WRITE gate: a save names a task only if the person may
/// read it (F5), and since BL-484 that question is asked through <c>ITaskReadAccessPolicy.ReadableTaskIdsAsync</c>. Every
/// leg a person can read a task by — not only the data legs the week GET tests — must still admit the task on a save,
/// and a task the person cannot read must still be refused with the existing reason code. Measured on the wire, with
/// MOD-0024's real scope and team resolvers; only the data-scope SOURCE is the test's (as in production it is Auth's).
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class TimeEntryTaskGateHttpMongoTests : TimeEntryScenario
{
    private readonly PersonScopes _scopes = new();

    public TimeEntryTaskGateHttpMongoTests(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    protected override void ConfigureHost(IServiceCollection services)
    {
        _scopes.Person = Person;
        services.AddSingleton<IDataScopeResolver>(_scopes);
    }

    [Fact]
    public async Task A_task_the_person_may_read_only_through_their_scope_can_be_saved()
    {
        var grantedUnit = Guid.NewGuid();
        await Collection<OrganizationUnit>(PlatformCollections.OrganizationUnits).InsertOneAsync(new OrganizationUnit
        {
            Id = grantedUnit, TenantId = Tenant, Code = "G" + grantedUnit.ToString("N")[..6], Name = "Granted", LegalEntityId = Guid.NewGuid()
        });
        var task = await ForeignTaskAsync(unit: grantedUnit);
        _scopes.Scopes.Add(new EntitlementDataScope(EntitlementDataScopeKind.OrgUnit, grantedUnit, "granted"));

        var saved = await SaveFreshAsync(CurrentWeek, Row(Monday, 60, task));

        Assert.True(saved.Status == HttpStatusCode.OK, saved.ToString());
    }

    [Fact]
    public async Task A_task_the_person_may_read_only_because_a_subordinate_holds_it_can_be_saved()
    {
        // The person's seat has a seat below it; the task is held by the person in that seat. The scope names the
        // person's OWN position only, so the scope leg answers no and the team leg is the one that admits.
        var subSeat = Guid.NewGuid();
        await SeedPositionAsync(subSeat, reportsTo: PersonSeat);
        await SeatAsync(Stranger, subSeat);
        var task = await ForeignTaskAsync();
        _scopes.Scopes.Add(new EntitlementDataScope(EntitlementDataScopeKind.Position, PersonSeat, "own seat"));

        var saved = await SaveFreshAsync(CurrentWeek, Row(Monday, 60, task));

        Assert.True(saved.Status == HttpStatusCode.OK, saved.ToString());
    }

    [Fact]
    public async Task A_task_the_person_may_read_only_through_ReadAll_can_be_saved()
    {
        var task = await ForeignTaskAsync();
        var token = Host.Token(Person, Tenant,
            TimeEntryPermissions.TimesheetsRead, TimeEntryPermissions.TimesheetsUpdate, TaskPermissions.ReadAll);

        var saved = await SaveAsync(await VersionAsync(CurrentWeek, token), CurrentWeek, token, Row(Monday, 60, task));

        Assert.True(saved.Status == HttpStatusCode.OK, saved.ToString());
    }

    [Fact]
    public async Task A_task_the_person_may_not_read_is_refused_as_unknown()
    {
        var task = await ForeignTaskAsync();

        var refused = await SaveFreshAsync(CurrentWeek, Row(Monday, 60, task));

        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);
        Assert.Equal(TimeEntryReasonCodes.TargetInvalid, refused.ReasonCode);
        Assert.Empty(await StoredWeeksAsync());                                   // nothing was written
    }

    // ── WP-WCN-TASK-REQUEST-01 FIX2 — new time on a task is a write on it ──────────────────────────────────────────

    /// <summary>The person a request waits on READS it (to answer it) but puts no time on it before answering; once
    /// they hold it, they do.</summary>
    [Fact]
    public async Task The_person_a_request_waits_on_cannot_put_time_on_it_before_answering()
    {
        var task = Guid.NewGuid();
        await SeedTaskAsync(Tenant, task, creator: Stranger, lifecycle: TaskLifecycle.Open);
        await Collection<TaskItem>(PlatformCollections.TaskItems).UpdateOneAsync(t => t.Id == task,
            Builders<TaskItem>.Update
                .Set(t => t.AssignmentTarget, TaskAssignmentTarget.Offered)
                .Set(t => t.AssigneeUserId, null)
                .Set(t => t.OfferedToUserId, Person)
                .Set(t => t.IsRequest, true)
                .Set(t => t.RequestOriginUnitId, Unit));

        var pending = await SaveFreshAsync(CurrentWeek, Row(Monday, 60, task));
        Assert.Equal(HttpStatusCode.BadRequest, pending.Status);
        Assert.Equal(TimeEntryReasonCodes.TargetInvalid, pending.ReasonCode);

        // Accepted: the holder records their time.
        await Collection<TaskItem>(PlatformCollections.TaskItems).UpdateOneAsync(t => t.Id == task,
            Builders<TaskItem>.Update
                .Set(t => t.AssignmentTarget, TaskAssignmentTarget.Person)
                .Set(t => t.AssigneeUserId, Person)
                .Set(t => t.OfferedToUserId, null)
                .Set(t => t.Lifecycle, TaskLifecycle.InProgress));
        var held = await SaveFreshAsync(CurrentWeek, Row(Monday, 60, task));
        Assert.True(held.Status == HttpStatusCode.OK, held.ToString());
    }

    /// <summary>The sender of an accepted request reads its holder's work (they watch it) but logs no time on it.</summary>
    [Fact]
    public async Task The_sender_of_an_accepted_request_cannot_put_time_on_its_holders_task()
    {
        var task = Guid.NewGuid();
        await SeedTaskAsync(Tenant, task, assignee: Stranger, creator: Person);
        await Collection<TaskItem>(PlatformCollections.TaskItems).UpdateOneAsync(t => t.Id == task,
            Builders<TaskItem>.Update.Set(t => t.IsRequest, true).Set(t => t.RequestOriginUnitId, Unit));

        var saved = await SaveFreshAsync(CurrentWeek, Row(Monday, 60, task));

        Assert.Equal(HttpStatusCode.BadRequest, saved.Status);
        Assert.Equal(TimeEntryReasonCodes.TargetInvalid, saved.ReasonCode);

        // Non-vacuity: the same task as an ORDINARY one created by the person (they are its requester) takes their time.
        await Collection<TaskItem>(PlatformCollections.TaskItems).UpdateOneAsync(t => t.Id == task,
            Builders<TaskItem>.Update.Set(t => t.IsRequest, false));
        var ordinary = await SaveFreshAsync(CurrentWeek, Row(Monday, 60, task));
        Assert.True(ordinary.Status == HttpStatusCode.OK, ordinary.ToString());
    }

    // ── WP-WCN-TASK-REQUEST-01 FIX3 ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// MUTATION TARGET (item 6 — the time gateway asks MOD-0024's write gate, the one shared decision). A watcher READS the
    /// task (the save's read check admits it — the refusal is the write rule's, not "unknown task") and puts no new time
    /// on it: watching is visibility, not work. The copy of the gate's rules the gateway used to keep knew nothing of this.
    /// </summary>
    [Fact]
    public async Task A_watcher_reads_a_task_but_puts_no_new_time_on_it()
    {
        var task = await ForeignTaskAsync();
        await Collection<TaskWatcher>(PlatformCollections.TaskWatchers).InsertOneAsync(
            new TaskWatcher { TenantId = Tenant, TaskItemId = task, UserId = Person, CreatedBy = "fix3" });

        var refused = await SaveFreshAsync(CurrentWeek, Row(Monday, 60, task));

        Assert.Equal(HttpStatusCode.BadRequest, refused.Status);
        Assert.Equal(TimeEntryReasonCodes.TargetInvalid, refused.ReasonCode);
        Assert.Contains("Time cannot be recorded on this task.", refused.Body);        // read, then refused as a write
        Assert.Empty(await StoredWeeksAsync());
    }

    /// <summary>
    /// MUTATION TARGET (G3 — SaveTimeEntriesHandler, "already in the draft" is the same task on the SAME day). A row the
    /// draft already holds stays saveable after the task stops taking the person's time; it does not spread to a new day.
    /// </summary>
    [Fact]
    public async Task A_row_already_in_the_draft_keeps_its_day_but_does_not_spread_to_a_new_one()
    {
        var task = Guid.NewGuid();
        await SeedTaskAsync(Tenant, task);                                            // the person's own: time is theirs to put
        Assert.Equal(HttpStatusCode.OK, (await SaveFreshAsync(CurrentWeek, Row(Monday, 60, task))).Status);

        // Then it becomes a request the person SENT and somebody else accepted: they read it, and log no new time on it.
        await Collection<TaskItem>(PlatformCollections.TaskItems).UpdateOneAsync(t => t.Id == task,
            Builders<TaskItem>.Update.Set(t => t.IsRequest, true).Set(t => t.RequestOriginUnitId, Unit)
                .Set(t => t.AssigneeUserId, Stranger));

        var spread = await SaveFreshAsync(CurrentWeek, Row(Monday, 60, task), Row(Monday.AddDays(1), 30, task));
        Assert.Equal(HttpStatusCode.BadRequest, spread.Status);
        Assert.Equal(TimeEntryReasonCodes.TargetInvalid, spread.ReasonCode);

        var kept = await SaveFreshAsync(CurrentWeek, Row(Monday, 90, task));
        Assert.True(kept.Status == HttpStatusCode.OK, kept.ToString());
    }

    /// <summary>A task nobody relates the person to by data: held and created by someone else, no watcher, no parent.</summary>
    private async Task<Guid> ForeignTaskAsync(Guid? unit = null)
    {
        var task = Guid.NewGuid();
        await SeedTaskAsync(Tenant, task, assignee: Stranger, creator: Stranger);
        if (unit is { } unitId)
        {
            await Collection<TaskItem>(PlatformCollections.TaskItems).UpdateOneAsync(
                t => t.Id == task, Builders<TaskItem>.Update.Set(t => t.OrganizationUnitId, unitId));
        }

        return task;
    }

    /// <summary>The data scopes Auth would hand the person (and nobody else) for the tasks module.</summary>
    private sealed class PersonScopes : IDataScopeResolver
    {
        public Guid Person { get; set; }

        public List<EntitlementDataScope> Scopes { get; } = [];

        public Task<IReadOnlyList<EntitlementDataScope>> ResolveAsync(
            Guid tenantId, Guid userId, string moduleCode, string? featureCode, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<EntitlementDataScope>>(userId == Person ? Scopes.ToList() : []);
    }
}
