using Diten.PlanningService.Application.Features.DemandPlanning.Cycles;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

// An explicit loopback replica-set URI is required; no operational Mongo fallback.
public sealed class PlanningCycleMongoIntegrationTests
{
    private const string DatabaseName = "mod0188_tests";
    private static readonly DateOnly FirstWeek = new(2026, 10, 5);
    private static readonly DateOnly AsOf = new(2026, 10, 2);

    private static (DemandPlanningMongoContext Context, PlanningCycleMongoStore Store) Open()
    {
        var uri = Environment.GetEnvironmentVariable("MOD0188_TEST_MONGO_URI")
            ?? throw new InvalidOperationException("Explicit test Mongo URI is required.");
        var url = new MongoUrl(uri);
        var servers = url.Servers.ToArray();
        if (servers.Length != 1 || servers[0].Host != "127.0.0.1" ||
            servers[0].Port is < 31994 or > 39994 || url.ReplicaSetName != "rs-mod0188" ||
            url.Username is not null || url.Password is not null)
            throw new InvalidOperationException("Only the explicit loopback MOD-0188 test replica set is allowed.");
        var settings = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = uri,
                ["Mongo:SupplyChainDatabaseName"] = DatabaseName
            }).Build();
        var context = new DemandPlanningMongoContext(settings);
        return (context, new PlanningCycleMongoStore(context));
    }

    private static CreatePlanningCycleCommand Command(Guid tenant, Guid actor, Guid legalEntity,
        string key, DateOnly? asOf = null) =>
        new(tenant, actor, legalEntity, asOf ?? AsOf, FirstWeek, key);

    private static CreatePlanningCycleCommandHandler Handler(PlanningCycleMongoStore store,
        Guid tenant, params Guid[] legalEntities) =>
        new(new FixtureAuthority(tenant, legalEntities), store);

    private sealed class FixtureAuthority(Guid tenant, IReadOnlyCollection<Guid> legalEntities)
        : IPlanningCycleAuthority
    {
        public Task<Guid?> ResolveSelectedAsync(Guid tenantId, Guid actorId,
            Guid selectedLegalEntityHint, CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(tenantId == tenant && actorId != Guid.Empty &&
                legalEntities.Contains(selectedLegalEntityHint) ? selectedLegalEntityHint : null);

        public Task<AuthorizedPlanningContext?> ResolveAsync(Guid tenantId, Guid legalEntityId,
            DateOnly asOfDate, DateOnly firstWeekStart, CancellationToken cancellationToken) =>
            Task.FromResult<AuthorizedPlanningContext?>(tenantId == tenant &&
                legalEntities.Contains(legalEntityId)
                ? new(legalEntityId, "Asia/Baku", "ISO-8601", "fixture-v1",
                    PlanningCalendarKind.Iso8601Fallback, null)
                : null);
    }

    [ManualDraftMongoFact]
    public async Task SameScopedRequest_RetriesOneDurableCycle_ChangedAsOfConflicts()
    {
        var (context, store) = Open();
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var handler = Handler(store, tenant, legalEntity);
        var request = Command(tenant, actor, legalEntity, "same-key");

        var created = await handler.Handle(request, default);
        var retry = await handler.Handle(request, default);
        var changed = await handler.Handle(request with
        { AsOfDate = AsOf.AddDays(1) }, default);

        Assert.Equal(201, created.StatusCode);
        Assert.Equal(200, retry.StatusCode);
        Assert.Equal(created.Data!.PlanningCycleId, retry.Data!.PlanningCycleId);
        Assert.Equal(409, changed.StatusCode);
        var saved = await context.PlanningCycles.Find(x => x.TenantId == tenant).ToListAsync();
        var cycle = Assert.Single(saved);
        Assert.Equal(created.Data.PlanningCycleId, cycle.Id);
        Assert.Equal(AsOf, cycle.AsOfDate);
        Assert.Equal("2026-10-05", cycle.PlanningPeriodKey);
        Assert.Equal("fixture-v1", cycle.CalendarVersion);
        Assert.Equal("Asia/Baku", cycle.TimeZoneId);
        Assert.Equal(52, cycle.Weeks.Count);
        Assert.Equal(FirstWeek, cycle.Weeks[0].WeekStart);
        Assert.Equal(FirstWeek.AddDays(363), cycle.Weeks[^1].WeekEnd);
    }

    [ManualDraftMongoFact]
    public async Task ConcurrentIdenticalRequests_CreateAtMostOnePhysicalCycle()
    {
        var (context, store) = Open();
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var handler = Handler(store, tenant, legalEntity);
        var request = Command(tenant, actor, legalEntity, "race-key");

        var results = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => handler.Handle(request, default)));

        Assert.Single(results, x => x.StatusCode == 201);
        Assert.Equal(11, results.Count(x => x.StatusCode == 200));
        Assert.Single(results.Select(x => x.Data!.PlanningCycleId).Distinct());
        Assert.Equal(1, await context.PlanningCycles.CountDocumentsAsync(x => x.TenantId == tenant));
    }

    [ManualDraftMongoFact]
    public async Task TenantAndLegalEntityScopes_NeverReplayEachOthersCycle()
    {
        var (context, store) = Open();
        var firstTenant = Guid.NewGuid();
        var secondTenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var firstLegal = Guid.NewGuid();
        var secondLegal = Guid.NewGuid();
        var firstHandler = Handler(store, firstTenant, firstLegal, secondLegal);
        var secondHandler = Handler(store, secondTenant, firstLegal);
        var firstRequest = Command(firstTenant, actor, firstLegal, "shared-key");
        var otherLegalRequest = Command(firstTenant, actor, secondLegal, "shared-key");
        var otherTenantRequest = Command(secondTenant, actor, firstLegal, "shared-key");

        var first = await firstHandler.Handle(firstRequest, default);
        var otherLegal = await firstHandler.Handle(otherLegalRequest, default);
        var otherTenant = await secondHandler.Handle(otherTenantRequest, default);
        Assert.All(new[] { first, otherLegal, otherTenant }, x => Assert.Equal(201, x.StatusCode));
        Assert.Equal(3, new[] { first, otherLegal, otherTenant }
            .Select(x => x.Data!.PlanningCycleId).Distinct().Count());
        Assert.Equal(first.Data!.PlanningCycleId,
            (await firstHandler.Handle(firstRequest, default)).Data!.PlanningCycleId);
        Assert.Equal(otherLegal.Data!.PlanningCycleId,
            (await firstHandler.Handle(otherLegalRequest, default)).Data!.PlanningCycleId);
        Assert.Equal(otherTenant.Data!.PlanningCycleId,
            (await secondHandler.Handle(otherTenantRequest, default)).Data!.PlanningCycleId);
        Assert.Equal(2, await context.PlanningCycles.CountDocumentsAsync(x => x.TenantId == firstTenant));
        Assert.Equal(1, await context.PlanningCycles.CountDocumentsAsync(x => x.TenantId == secondTenant));
        Assert.Equal(404, (await secondHandler.Handle(firstRequest, default)).StatusCode);
    }

    [ManualDraftMongoFact]
    public async Task SameBusinessPeriod_AllowsDistinctCyclesWithoutPublishingBaseline()
    {
        var (context, store) = Open();
        var tenant = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var legalEntity = Guid.NewGuid();
        var handler = Handler(store, tenant, legalEntity);

        var first = await handler.Handle(Command(tenant, actor, legalEntity, "cycle-a"), default);
        var second = await handler.Handle(Command(tenant, actor, legalEntity,
            "cycle-b", AsOf.AddDays(1)), default);

        Assert.Equal(201, first.StatusCode);
        Assert.Equal(201, second.StatusCode);
        Assert.NotEqual(first.Data!.PlanningCycleId, second.Data!.PlanningCycleId);
        Assert.Equal(first.Data.PlanningPeriodKey, second.Data.PlanningPeriodKey);
        var saved = await context.PlanningCycles.Find(x => x.TenantId == tenant).ToListAsync();
        Assert.Equal(2, saved.Count);
        Assert.All(saved, x => Assert.Equal("2026-10-05", x.PlanningPeriodKey));
        // The one-current-Published rule belongs to revision publication, not cycle creation.
    }
}
