using System.Security.Claims;
using System.Text.Json;
using Diten.PlanningService.Api.Features.DemandPlanning;
using Diten.PlanningService.Application.Features.DemandPlanning.Cycles;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Infrastructure.Features.DemandPlanning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed class PlanningCycleTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherTenant = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Actor = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid LegalEntity = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid OtherLegalEntity = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly DateOnly Monday = new(2026, 10, 5);
    private static readonly DateOnly AsOf = new(2026, 10, 2);

    private static AuthorizedPlanningContext Iso(string version = "1") =>
        new(LegalEntity, "Asia/Baku", "ISO-8601", version,
            PlanningCalendarKind.Iso8601Fallback, null);

    private static CreatePlanningCycleCommand Command(
        string key = "request-1", DateOnly? asOf = null,
        DateOnly? first = null, Guid? tenant = null, Guid? legalEntityHint = null) =>
        new(tenant ?? Tenant, Actor, legalEntityHint ?? LegalEntity,
            asOf ?? AsOf, first ?? Monday, key);

    [Fact]
    public async Task IsoCycleLocks52WeeksAndDerivesLocalPeriodOnServer()
    {
        var store = new MemoryStore();
        var result = await new CreatePlanningCycleCommandHandler(new FakeAuthority(Iso()), store)
            .Handle(Command(), default);
        Assert.True(result.IsSuccessful);
        Assert.Equal(201, result.StatusCode);
        var cycle = Assert.IsType<PlanningCycleResult>(result.Data);
        Assert.NotEqual(Guid.Empty, cycle.PlanningCycleId);
        Assert.Equal(Tenant, cycle.TenantId);
        Assert.Equal(LegalEntity, cycle.LegalEntityId);
        Assert.Equal("Asia/Baku", cycle.TimeZoneId);
        Assert.Equal("2026-10-05", cycle.PlanningPeriodKey);
        Assert.Equal(52, cycle.Weeks.Count);
        Assert.Equal(Monday, cycle.HorizonStart);
        Assert.Equal(Monday.AddDays(363), cycle.HorizonEnd);
        Assert.Equal(Monday.AddDays(357), cycle.Weeks[51].WeekStart);
        Assert.Equal(Monday.AddDays(363), cycle.Weeks[51].WeekEnd);
    }

    [Fact]
    public async Task SameFirstWeekDifferentAsOfAndCalendarVersionShareBusinessPeriod()
    {
        var store = new MemoryStore();
        var first = await new CreatePlanningCycleCommandHandler(new FakeAuthority(Iso("1")), store)
            .Handle(Command("one"), default);
        var second = await new CreatePlanningCycleCommandHandler(new FakeAuthority(Iso("2")), store)
            .Handle(Command("two", new DateOnly(2026, 10, 3)), default);
        Assert.NotEqual(first.Data!.PlanningCycleId, second.Data!.PlanningCycleId);
        Assert.Equal(first.Data.PlanningPeriodKey, second.Data.PlanningPeriodKey);
        Assert.Equal("2", second.Data.CalendarVersion);
    }

    [Fact]
    public async Task IdempotentRetryReturnsOriginalCycleAndChangedPayloadConflicts()
    {
        var store = new MemoryStore();
        var handler = new CreatePlanningCycleCommandHandler(new FakeAuthority(Iso()), store);
        var first = await handler.Handle(Command(), default);
        var retry = await handler.Handle(Command(), default);
        var conflict = await handler.Handle(Command(asOf: new DateOnly(2026, 10, 3)), default);
        Assert.Equal(201, first.StatusCode);
        Assert.Equal(200, retry.StatusCode);
        Assert.Equal(first.Data!.PlanningCycleId, retry.Data!.PlanningCycleId);
        Assert.Equal(409, conflict.StatusCode);
        Assert.Single(store.Cycles);
    }

    [Fact]
    public async Task ConcurrentSameKeyCreatesOnlyOneCycle()
    {
        var store = new MemoryStore();
        var handler = new CreatePlanningCycleCommandHandler(new FakeAuthority(Iso()), store);
        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => handler.Handle(Command(), default)));
        Assert.Single(store.Cycles);
        Assert.Single(results.Select(r => r.Data!.PlanningCycleId).Distinct());
        Assert.Single(results, r => r.StatusCode == 201);
    }

    [Fact]
    public async Task TenantAndLegalEntityScopeAreRequiredAndDoNotCross()
    {
        var store = new MemoryStore();
        var authority = new FakeAuthority(Iso(), permittedTenant: Tenant);
        var handler = new CreatePlanningCycleCommandHandler(authority, store);
        var allowed = await handler.Handle(Command(), default);
        var denied = await handler.Handle(Command(tenant: OtherTenant), default);
        var noActor = await handler.Handle(Command() with { ActorId = Guid.Empty }, default);
        Assert.Equal(201, allowed.StatusCode);
        Assert.Equal(404, denied.StatusCode);
        Assert.Equal(403, noActor.StatusCode);
        Assert.Single(store.Cycles);
        Assert.Equal(OtherTenant, authority.LastTenant);
    }

    [Fact]
    public async Task AuthorizedPlannerCanSwitchBetweenTwoAssignedLegalEntities()
    {
        var store = new MemoryStore();
        var authority = new FakeAuthority(Iso(), permittedTenant: Tenant,
            secondContext: Iso() with { LegalEntityId = OtherLegalEntity });
        var handler = new CreatePlanningCycleCommandHandler(authority, store);
        var first = await handler.Handle(Command("first"), default);
        var second = await handler.Handle(Command("second", legalEntityHint: OtherLegalEntity), default);
        Assert.Equal(201, first.StatusCode);
        Assert.Equal(201, second.StatusCode);
        Assert.Equal(LegalEntity, first.Data!.LegalEntityId);
        Assert.Equal(OtherLegalEntity, second.Data!.LegalEntityId);
        Assert.Equal(2, store.Cycles.Count);
    }

    [Fact]
    public async Task DifferentResolvedLegalEntityFailsClosedEvenWhenBothEntitiesAreAssigned()
    {
        var store = new MemoryStore();
        var authority = new FakeAuthority(Iso(), permittedTenant: Tenant,
            secondContext: Iso() with { LegalEntityId = OtherLegalEntity })
        { ResolvedOverride = OtherLegalEntity };
        var result = await new CreatePlanningCycleCommandHandler(authority, store)
            .Handle(Command(legalEntityHint: LegalEntity), default);
        Assert.Equal(503, result.StatusCode);
        Assert.Empty(store.Cycles);
    }

    [Fact]
    public async Task UnassignedOrMissingLegalEntitySelectionCannotCreateCycle()
    {
        var store = new MemoryStore();
        var handler = new CreatePlanningCycleCommandHandler(
            new FakeAuthority(Iso(), permittedTenant: Tenant), store);
        var unassigned = await handler.Handle(Command(legalEntityHint: OtherLegalEntity), default);
        var missing = await handler.Handle(Command() with { SelectedLegalEntityHint = Guid.Empty }, default);
        Assert.Equal(404, unassigned.StatusCode);
        Assert.Equal(400, missing.StatusCode);
        Assert.Empty(store.Cycles);
    }

    [Fact]
    public async Task MissingAuthorityAndInvalidTimezoneFailClosed()
    {
        var store = new MemoryStore();
        var missing = await new CreatePlanningCycleCommandHandler(new FakeAuthority(null), store)
            .Handle(Command(), default);
        var outage = await new CreatePlanningCycleCommandHandler(new FakeAuthority(Iso(), outage: true), store)
            .Handle(Command(), default);
        var invalid = await new CreatePlanningCycleCommandHandler(
            new FakeAuthority(Iso() with { TimeZoneId = "Invalid/Zone" }), store)
            .Handle(Command(), default);
        Assert.Equal(404, missing.StatusCode);
        Assert.Equal(503, outage.StatusCode);
        Assert.Equal(422, invalid.StatusCode);
        Assert.Empty(store.Cycles);
    }

    [Fact]
    public async Task UnconfiguredProductionAssignmentAdapterNeverCreatesCycle()
    {
        var store = new MemoryStore();
        var result = await new CreatePlanningCycleCommandHandler(
            new UnconfiguredPlanningCycleAuthority(), store).Handle(Command(), default);
        Assert.Equal(503, result.StatusCode);
        Assert.Empty(store.Cycles);
    }

    [Fact]
    public async Task CalendarStartAndWeekShapeAreValidated()
    {
        var store = new MemoryStore();
        var iso = await new CreatePlanningCycleCommandHandler(new FakeAuthority(Iso()), store)
            .Handle(Command(first: Monday.AddDays(1)), default);
        var shortCalendar = new AuthorizedPlanningContext(LegalEntity, "Asia/Baku",
            "corporate", "7", PlanningCalendarKind.VerifiedCorporate,
            [new PlanningWeek { Number = 1, WeekStart = Monday, WeekEnd = Monday.AddDays(6) }]);
        var corporate = await new CreatePlanningCycleCommandHandler(new FakeAuthority(shortCalendar), store)
            .Handle(Command("corporate"), default);
        Assert.Equal(422, iso.StatusCode);
        Assert.Equal(422, corporate.StatusCode);
        Assert.Empty(store.Cycles);
    }

    [Fact]
    public async Task VerifiedCorporateCalendarIsSnapshottedWithoutReplacingItWithIso()
    {
        var first = new DateOnly(2026, 10, 7);
        var weeks = Enumerable.Range(0, 52).Select(i => new PlanningWeek
        {
            Number = i + 1, WeekStart = first.AddDays(i * 7),
            WeekEnd = first.AddDays(i * 7 + 6)
        }).ToList();
        var context = new AuthorizedPlanningContext(LegalEntity, "Asia/Baku",
            "corporate", "7", PlanningCalendarKind.VerifiedCorporate, weeks);
        var result = await new CreatePlanningCycleCommandHandler(new FakeAuthority(context), new MemoryStore())
            .Handle(Command(first: first), default);
        weeks[0].WeekStart = first.AddDays(1);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal("2026-10-07", result.Data!.PlanningPeriodKey);
        Assert.Equal(first, result.Data.Weeks[0].WeekStart);
        Assert.Equal("corporate", result.Data.CalendarId);
    }

    [Fact]
    public void ApiRejectsClientSuppliedPeriodOrLegalEntity()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreatePlanningCycleRequest>(
            """{"asOfDate":"2026-10-02","firstWeekStart":"2026-10-05","planningPeriodKey":"2026-10-05"}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreatePlanningCycleRequest>(
            """{"asOfDate":"2026-10-02","firstWeekStart":"2026-10-05","legalEntityId":"44444444-4444-4444-4444-444444444444"}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void CycleRouteMatchesPackV2Draft()
    {
        var route = Assert.Single(typeof(PlanningCyclesController)
            .GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>());
        Assert.Equal("api/v2/demand/planning-cycles", route.Template);
    }
    [Fact]
    public async Task TenantMiddlewareRejectsClaimHeaderMismatch()
    {
        var invoked = false;
        var middleware = new DemandTenantMiddleware(_ => { invoked = true; return Task.CompletedTask; });
        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.Path = "/api/v2/demand/planning-cycles";
        http.Request.Headers["X-Tenant-Id"] = OtherTenant.ToString();
        http.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("tenant_id", Tenant.ToString())], "test"));
        await middleware.InvokeAsync(http);
        Assert.Equal(404, http.Response.StatusCode);
        Assert.False(invoked);
    }

    [Fact]
    public async Task MongoSerializationPreservesFrozenCycleDatesAndWeeks()
    {
        var store = new MemoryStore();
        var result = await new CreatePlanningCycleCommandHandler(new FakeAuthority(Iso()), store)
            .Handle(Command(), default);
        Assert.Equal(201, result.StatusCode);
        var document = store.Cycles.Single().ToBsonDocument();
        var restored = BsonSerializer.Deserialize<PlanningCycle>(document);
        Assert.Equal("2026-10-05", restored.PlanningPeriodKey);
        Assert.Equal(Monday, restored.HorizonStart);
        Assert.Equal(52, restored.Weeks.Count);
        Assert.Equal(Monday.AddDays(363), restored.Weeks[^1].WeekEnd);
    }
    private sealed class FakeAuthority : IPlanningCycleAuthority
    {
        private readonly Dictionary<Guid, AuthorizedPlanningContext> _assignedContexts = [];
        private readonly Guid? _permittedTenant;
        private readonly bool _outage;
        public Guid LastTenant { get; private set; }
        public Guid? ResolvedOverride { get; set; }
        public FakeAuthority(AuthorizedPlanningContext? context,
            Guid? permittedTenant = null, bool outage = false,
            AuthorizedPlanningContext? secondContext = null)
        {
            if (context is not null) _assignedContexts.Add(context.LegalEntityId, context);
            if (secondContext is not null)
                _assignedContexts.Add(secondContext.LegalEntityId, secondContext);
            _permittedTenant = permittedTenant;
            _outage = outage;
        }
        public Task<Guid?> ResolveSelectedAsync(Guid tenantId, Guid actorId,
            Guid selectedLegalEntityHint, CancellationToken cancellationToken)
        {
            LastTenant = tenantId;
            if (_outage) throw new InvalidOperationException("assignment source unavailable");
            return Task.FromResult<Guid?>(actorId == Actor &&
                (!_permittedTenant.HasValue || _permittedTenant == tenantId) &&
                _assignedContexts.ContainsKey(selectedLegalEntityHint)
                ? ResolvedOverride ?? _assignedContexts[selectedLegalEntityHint].LegalEntityId : null);
        }
        public Task<AuthorizedPlanningContext?> ResolveAsync(Guid tenantId, Guid legalEntityId,
            DateOnly asOfDate, DateOnly firstWeekStart, CancellationToken cancellationToken)
        {
            if (_outage) throw new InvalidOperationException("source unavailable");
            return Task.FromResult(_assignedContexts.GetValueOrDefault(legalEntityId));
        }
    }

    private sealed class MemoryStore : IPlanningCycleStore
    {
        private readonly object _gate = new();
        public List<PlanningCycle> Cycles { get; } = [];
        public Task<PlanningCycle?> ReadAsync(Guid tenantId, Guid legalEntityId,
            Guid cycleId, CancellationToken cancellationToken) =>
            Task.FromResult(Cycles.SingleOrDefault(x => x.TenantId == tenantId &&
                x.LegalEntityId == legalEntityId && x.Id == cycleId && !x.IsDeleted));
        public Task<CycleInsertResult> InsertOrGetAsync(PlanningCycle cycle,
            CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                var existing = Cycles.SingleOrDefault(x =>
                    x.TenantId == cycle.TenantId && x.LegalEntityId == cycle.LegalEntityId &&
                    x.CreatedByActorId == cycle.CreatedByActorId &&
                    x.IdempotencyKey == cycle.IdempotencyKey);
                if (existing is not null)
                    return Task.FromResult(existing.RequestFingerprint == cycle.RequestFingerprint
                        ? new CycleInsertResult(CycleInsertOutcome.Existing, existing)
                        : new CycleInsertResult(CycleInsertOutcome.Conflict, null));
                Cycles.Add(cycle);
                return Task.FromResult(new CycleInsertResult(CycleInsertOutcome.Created, cycle));
            }
        }
    }
}




