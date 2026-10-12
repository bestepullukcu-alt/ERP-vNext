using System.Net;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
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
