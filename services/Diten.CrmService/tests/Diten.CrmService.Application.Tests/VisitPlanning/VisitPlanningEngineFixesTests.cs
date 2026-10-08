using System.Net;
using System.Text;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.PlannedVisit.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.PlannedVisit.Queries;
using Diten.CrmService.Application.Features.Resources;
using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Application.Features.VisitPlanning.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitPlanning.Queries;
using Diten.CrmService.Application.Tests.Territory;
using Diten.CrmService.Application.Tests.VisitScope;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Infrastructure.StrategyTemplate;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Tests.VisitPlanning;

/// <summary>
/// WP-VP-4G — the Faz 4 E4 fixes on the PRODUCTION day balancer, engine and read handlers: a far cluster goes to a LIGHT
/// day (F4-1) and the shift says why (capacity_full / no_near_day); a pin moves only what it pins (F4-5, stable layout);
/// product names read in one bulk call and snapshotted on approval (F4-4); the rep's country and name on resources/me
/// (F4-8, F4-10); the report state of a written visit (F4-9).
/// <para>Places: Kadıköy ≈ (40.990, 29.030) and Bakırköy ≈ (40.980, 28.860) — ≈ 28 travel minutes apart (far).</para>
/// </summary>
public sealed partial class VisitPlanningTests
{
    // ── 1 · the live case: Thursday two near institutions, Friday one visit, a far clinic → Friday (light) ─────────

    [Fact]
    public void A_far_clinic_goes_to_the_lightest_day_instead_of_the_next_week()
    {
        // Friday already holds TOKİ (30 min, its own centre); Thursday is opened by 01 NOLU and joined by 03 NOLU.
        var days = new List<DayBalancer.Day>
        {
            new(Thu10, 480),
            new(Fri11, 480, FixedLoadMinutes: 30, CenterLat: 41.150, CenterLng: 29.050, CenterWeight: 30) // TOKİ: far from both
        };
        var visits = new List<DayBalancer.Visit>
        {
            new(1, "01NOLU", 12, KadikoyLat, KadikoyLng), new(2, "01NOLU", 12, KadikoyLat, KadikoyLng),
            new(3, "01NOLU", 12, KadikoyLat, KadikoyLng), new(4, "01NOLU", 12, KadikoyLat, KadikoyLng),
            new(5, "03NOLU", 13, KadikoyLat + 0.003, KadikoyLng), new(6, "03NOLU", 12, KadikoyLat + 0.003, KadikoyLng),
            new(7, "018KLINIK", 20, BakirkoyLat, BakirkoyLng), new(8, "018KLINIK", 20, BakirkoyLat, BakirkoyLng)
        };

        var result = DayBalancer.Assign(days, visits);

        Assert.Empty(result.Overflow);
        Assert.All(new[] { 1, 2, 3, 4, 5, 6 }, id => Assert.Equal(Thu10, result.Assigned[id]));
        Assert.Equal(Fri11, result.Assigned[7]); // the lighter day (30 min) takes the far clinic whole
        Assert.Equal(Fri11, result.Assigned[8]);
        Assert.True(result.LoadMinutes[Fri11] > 30 + 40); // the travel between the two clusters is booked on the day
        Assert.Equal(0.5, DayBalancer.LightDayLoadRatio);
    }

    // ── 2 · a day already more than half full keeps its gap idle ────────────────────────────────────────────────

    [Fact]
    public void A_day_more_than_half_full_never_takes_a_far_group_into_its_gap()
    {
        var days = new List<DayBalancer.Day>
        {
            new(Thu10, 480, FixedLoadMinutes: 300, CenterLat: KadikoyLat, CenterLng: KadikoyLng, CenterWeight: 300),
            new(Fri11, 480, FixedLoadMinutes: 250, CenterLat: KadikoyLat, CenterLng: KadikoyLng, CenterWeight: 250)
        };

        var result = DayBalancer.Assign(days, new List<DayBalancer.Visit> { new(1, "FAR", 30, BakirkoyLat, BakirkoyLng) });

        Assert.Empty(result.Assigned);
        Assert.Equal(new[] { 1 }, result.Overflow);
        Assert.Contains(1, result.NoNearDay!); // there was room — the reason is "no near day", not "full"
        Assert.Equal(300, result.LoadMinutes[Thu10]); // the gap stays idle
    }

    // ── 3 · capacity_full only when no day has room; else no_near_day (also on the shift) ──────────────────────

    [Fact]
    public async Task The_shift_says_no_near_day_when_there_was_room_and_capacity_full_only_when_there_was_none()
    {
        var full = DayBalancer.Assign(
            new List<DayBalancer.Day>
            {
                new(Thu10, 480, FixedLoadMinutes: 470, CenterLat: KadikoyLat, CenterLng: KadikoyLng, CenterWeight: 470)
            },
            new List<DayBalancer.Visit> { new(1, "FAR", 30, BakirkoyLat, BakirkoyLng) });
        Assert.Equal(new[] { 1 }, full.Overflow);
        Assert.Empty(full.NoNearDay!); // no day had room: the week is really full

        // Engine: only Monday open; it is 87 % full with Kadıköy, so the far doctor leaves the week — no_near_day.
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        foreach (var d in new[] { Tue8, Wed9, Thu10, Fri11 })
        {
            env.WorkingDays.Holidays.Add(d);
        }

        env.Session.Selection.SelectedContacts.Clear();
        AddInstitution(env, "Kadıköy", 14, KadikoyLat, KadikoyLng);
        var (_, far) = AddInstitution(env, "Bakırköy", 1, BakirkoyLat, BakirkoyLng);

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;

        var shift = Assert.Single(preview.Shifted!, s => s.ContactId == far[0]);
        Assert.Equal(PlanningShiftReasons.NoNearDay, shift.Reason);
    }

    // ── 4 / 5 / 6 / 7 · stable placement around pins ───────────────────────────────────────────────────────────

    /// <summary>Two far institutions: Y (4 × 30, Kadıköy) is the largest → Thursday; X (3 × 30, Bakırköy) → Friday.</summary>
    private static (List<DayBalancer.Day> Days, List<DayBalancer.Visit> Visits) TwoInstitutionWeek(int budget = 480)
        => (new List<DayBalancer.Day> { new(Thu10, budget), new(Fri11, budget) },
            new List<DayBalancer.Visit>
            {
                new(1, "Y", 30, KadikoyLat, KadikoyLng), new(2, "Y", 30, KadikoyLat, KadikoyLng),
                new(3, "Y", 30, KadikoyLat, KadikoyLng), new(4, "Y", 30, KadikoyLat + 0.01, KadikoyLng + 0.01),
                new(11, "X", 30, BakirkoyLat, BakirkoyLng), new(12, "X", 30, BakirkoyLat, BakirkoyLng),
                new(13, "X", 30, BakirkoyLat, BakirkoyLng)
            });

    [Fact]
    public void A_visit_pin_moves_only_that_doctor_and_its_colleagues_stay_on_their_base_day()
    {
        var (days, visits) = TwoInstitutionWeek();
        var baseline = DayBalancer.Assign(days, visits);
        Assert.Equal((Thu10, Fri11), (baseline.Assigned[1], baseline.Assigned[11]));

        // "Only this doctor": X's first doctor to Thursday.
        var result = DayBalancer.AssignAroundPins(
            days, visits, new Dictionary<int, DateOnly> { [11] = Thu10 }, new HashSet<int>());

        Assert.Equal(Fri11, result.Assigned[12]); // X's other doctors stay on Friday
        Assert.Equal(Fri11, result.Assigned[13]);
        Assert.All(new[] { 1, 2, 3, 4 }, id => Assert.Equal(Thu10, result.Assigned[id])); // Y stays on Thursday
        Assert.False(result.Assigned.ContainsKey(11)); // a pinned visit is the caller's
    }

    [Fact]
    public void Pinning_an_institution_to_a_day_leaves_that_days_others_in_place_when_the_budget_holds()
    {
        var (days, visits) = TwoInstitutionWeek();

        var result = DayBalancer.AssignAroundPins(
            days, visits, new Dictionary<int, DateOnly> { [11] = Thu10, [12] = Thu10, [13] = Thu10 }, new HashSet<int>());

        Assert.All(new[] { 1, 2, 3, 4 }, id => Assert.Equal(Thu10, result.Assigned[id]));
        Assert.Empty(result.Overflow);
    }

    [Fact]
    public void Over_budget_only_the_free_visits_farthest_from_the_days_centre_move_never_a_pinned_one()
    {
        var (days, visits) = TwoInstitutionWeek(budget: 180);

        // X (90 min) pinned onto Thursday where Y (120 min) already sits: 210 > 180 → one Y visit must leave.
        var result = DayBalancer.AssignAroundPins(
            days, visits, new Dictionary<int, DateOnly> { [11] = Thu10, [12] = Thu10, [13] = Thu10 }, new HashSet<int>());

        Assert.All(new[] { 1, 2, 3 }, id => Assert.Equal(Thu10, result.Assigned[id]));
        Assert.Equal(Fri11, result.Assigned[4]); // Y's visit farthest from Thursday's centre moved
        Assert.DoesNotContain(11, result.Assigned.Keys);
        Assert.True(result.LoadMinutes[Thu10] <= 180);
    }

    [Fact]
    public async Task The_same_input_gives_the_same_days_twice()
    {
        var (days, visits) = TwoInstitutionWeek();
        var pins = new Dictionary<int, DateOnly> { [11] = Thu10 };
        var a = DayBalancer.AssignAroundPins(days, visits, pins, new HashSet<int>());
        var b = DayBalancer.AssignAroundPins(days, visits, pins, new HashSet<int>());
        Assert.Equal(a.Assigned.OrderBy(kv => kv.Key), b.Assigned.OrderBy(kv => kv.Key));

        var env = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        env.Session.Selection.SelectedContacts.Clear();
        var (_, xDoctors) = AddInstitution(env, "X", 3, BakirkoyLat, BakirkoyLng);
        AddInstitution(env, "Y", 4, KadikoyLat, KadikoyLng);
        env.Session.DayPins.Add(new PlanningDayPin
        {
            WeekStart = "2026-09-07", TargetType = PlannedVisitTargetType.Contact, TargetId = xDoctors[0],
            ContactId = xDoctors[0], Date = "2026-09-09", Scope = PlanningDayPinScopes.Visit
        });
        string Days(VisitPlanPreview p) => string.Join(",", p.Scheduled.OrderBy(s => s.TargetId).Select(s => s.TargetId + "@" + s.PlannedDate));
        var first = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;
        var second = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;
        Assert.Equal(Days(first), Days(second));
    }

    [Fact]
    public async Task On_the_engine_a_visit_pin_leaves_every_other_visit_on_its_day()
    {
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        env.Session.Selection.SelectedContacts.Clear();
        var (_, xDoctors) = AddInstitution(env, "X", 3, BakirkoyLat, BakirkoyLng);
        var (_, yDoctors) = AddInstitution(env, "Y", 4, KadikoyLat, KadikoyLng);
        var before = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;
        string DayOf(VisitPlanPreview p, Guid doctor) => p.Scheduled.Single(s => s.ContactId == doctor).PlannedDate;
        var target = DayOf(before, yDoctors[0]); // pin one X doctor onto Y's day

        env.Session.DayPins.Add(new PlanningDayPin
        {
            WeekStart = "2026-09-07", TargetType = PlannedVisitTargetType.Contact, TargetId = xDoctors[0],
            ContactId = xDoctors[0], Date = target, Scope = PlanningDayPinScopes.Visit
        });
        var after = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;

        Assert.Equal(target, DayOf(after, xDoctors[0]));
        foreach (var doctor in xDoctors.Skip(1).Concat(yDoctors))
        {
            Assert.Equal(DayOf(before, doctor), DayOf(after, doctor));
        }
    }

    // ── 8 · product names: one bulk read, fail-open ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Product_names_fill_the_preview_and_the_plan_reads_in_one_bulk_call_and_an_unknown_one_stays_null()
    {
        var env = Env.WithTwoDoctors();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        Pick(env, env.DoctorA, (p1, "GP-000000000001", null), (p2, "GP-000000000002", null));
        env.ProductNames.Names[p1] = "TUTUKON";

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Wed2Sep), default)).Preview!;

        Assert.Equal(1, env.ProductNames.Calls); // ONE bulk call for every product of the preview
        var items = preview.Scheduled.Single(s => s.ContactId == env.DoctorA).ContentItems!;
        Assert.Equal("TUTUKON", items.Single(i => i.ProductId == p1).ProductName);
        Assert.Null(items.Single(i => i.ProductId == p2).ProductName); // unknown to the master → the code is shown
        Assert.Equal("TUTUKON", preview.ProductDistribution!.Single(d => d.ProductId == p1).ProductName);
        Assert.Equal("TUTUKON", preview.Content.Single(c => c.ContactId == env.DoctorA).Products!.Single(p => p.ProductId == p1).ProductName);

        // The plan detail: the picked products named, one call.
        var detailNames = new FakeProductNames { Names = { [p1] = "TUTUKON" } };
        var detail = (await new GetPlanningSessionByIdHandler(
                TenantOf(Tenant), new FakePlanningSessionRepository(env.Session), TestCallerScope.Unrestricted("rep-1"),
                new Features.PlannedVisit.VisitTargetNameReader(env.Accounts, env.Contacts), productNames: detailNames)
            .Handle(new GetPlanningSessionByIdQuery(env.Session.Id), default)).Data!;
        var row = detail.SelectedContacts.Single(c => c.ContactId == env.DoctorA).Products!;
        Assert.Equal(("TUTUKON", (string?)null), (row.Single(p => p.ProductId == p1).ProductName, row.Single(p => p.ProductId == p2).ProductName));
        Assert.Equal(1, detailNames.Calls);
    }

    [Fact]
    public async Task The_mdm_name_reader_walks_the_selector_once_and_answers_nothing_but_never_throws_when_mdm_is_down()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var handler = new SelectorPages(
            Page(100, 150, (p1, "TUTUKON")),
            Page(50, 150, (p2, "ALMIBA")));
        var reader = Reader(handler);

        var names = await reader.ReadNamesAsync(new[] { p1, p2 }, default);

        Assert.Equal(("TUTUKON", "ALMIBA"), (names[p1], names[p2]));
        Assert.Equal(2, handler.Requests.Count); // pages of the catalogue, not one call per product
        Assert.All(handler.Requests, r => Assert.Contains("api/global-products/selector?pageNumber=", r));

        var down = new SelectorPages { Status = HttpStatusCode.ServiceUnavailable };
        Assert.Empty(await Reader(down).ReadNamesAsync(new[] { p1 }, default));
    }

    // ── 9 · approval snapshot; an older visit is named at read time ──────────────────────────────────────────

    [Fact]
    public async Task Approval_writes_the_name_snapshot_and_an_older_visit_without_it_is_named_on_read()
    {
        var env = WeeklyEnv();
        var p1 = Guid.NewGuid();
        Pick(env, env.DoctorA, (p1, "GP-1", null));
        env.ProductNames.Names[p1] = "TUTUKON";

        var atoms = await ApproveAndStoreAsync(env, Week7Sep, Wed2Sep);

        var atom = atoms.Single(a => a.ContactId == env.DoctorA);
        Assert.Equal("TUTUKON", atom.ContentItems.Single().ProductName);

        // An older visit (no snapshot) reads its name from the master.
        atom.ContentItems.Single().ProductName = null;
        var plannedVisits = new Diten.CrmService.Application.Tests.PlannedVisit.FakePlannedVisitRepository();
        plannedVisits.Items.Add(atom);
        var names = new FakeProductNames { Names = { [p1] = "TUTUKON" } };
        var dto = (await new GetPlannedVisitByIdHandler(
                TenantOf(Tenant), plannedVisits, TestCallerScope.Unrestricted("rep-1"),
                new Features.PlannedVisit.VisitTargetNameReader(env.Accounts, env.Contacts), names)
            .Handle(new GetPlannedVisitByIdQuery(atom.Id), default)).Data!;
        Assert.Equal("TUTUKON", dto.ContentItems!.Single().ProductName);
        Assert.Equal(1, names.Calls);
    }

    // ── 10 · resources/me country + names ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Me_carries_the_country_of_the_reps_territory_and_null_without_one()
    {
        var models = new FakeTerritoryModelRepo();
        var nodes = new FakeTerritoryNodeRepo();
        var assignments = new FakeTerritoryResourceAssignmentRepo();
        var model = Guid.NewGuid();
        var node = Guid.NewGuid();
        var from = DateTimeOffset.UtcNow.AddYears(-1);
        models.Items.Add(new TerritoryModel { Id = model, TenantId = Tenant, ModelCode = "TR", Status = "active", EffectiveFrom = from, CountryScope = "tr" });
        nodes.Items.Add(new TerritoryNode { Id = node, TenantId = Tenant, ModelId = model, TerritoryCode = "IST", Name = "İstanbul", Status = "active", EffectiveFrom = from, CountryCode = "TR" });
        var rep = Guid.NewGuid();
        assignments.Items.Add(new TerritoryResourceAssignment
        {
            Id = Guid.NewGuid(), TenantId = Tenant, ModelId = model, TerritoryId = node,
            Resource = new TerritoryResourceRef { ResourceId = rep.ToString(), ResourceType = "user" },
            CoverageScope = "exact-territory", Status = "active",
            ValidFrom = DateTimeOffset.UtcNow.AddMonths(-1), ValidTo = DateTimeOffset.UtcNow.AddMonths(6)
        });
        var names = new FixedNames { [rep] = "Beste Yılmaz" };
        var handler = new GetMyResourcesQueryHandler(TenantOf(Tenant), names, assignments, models, nodes);

        var me = Assert.Single((await handler.Handle(new GetMyResourcesQuery(rep.ToString()), default)).Data!.Items);
        Assert.Equal(("tr", "Beste Yılmaz"), (me.CountryCode, me.DisplayName));

        var nobody = Assert.Single((await handler.Handle(new GetMyResourcesQuery(Guid.NewGuid().ToString()), default)).Data!.Items);
        Assert.Null(nobody.CountryCode);
    }

    [Fact]
    public async Task The_plan_list_shows_the_reps_name_and_an_email_only_as_the_last_resort()
    {
        var rep = Guid.NewGuid();
        var other = Guid.NewGuid();
        var store = new SessionList();
        store.Items.Add(new PlanningSession
        {
            Id = Guid.NewGuid(), TenantId = Tenant, CyclePeriodId = Id(30), ResourceId = rep.ToString(),
            ResourceDisplayName = "beste@diten.test"
        });
        store.Items.Add(new PlanningSession
        {
            Id = Guid.NewGuid(), TenantId = Tenant, CyclePeriodId = Id(31), ResourceId = other.ToString(),
            ResourceDisplayName = "Can Demir"
        });
        var list = (await new ListPlanningSessionsHandler(
                TenantOf(Tenant), store, TestCallerScope.Unrestricted(rep.ToString()),
                userNames: new FixedNames { [rep] = "Beste Yılmaz", [other] = "can@diten.test" })
            .Handle(new ListPlanningSessionsQuery(), default)).Data!.Items;

        Assert.Equal("Beste Yılmaz", list.Single(i => i.ResourceId == rep.ToString()).ResourceDisplayName); // name over stored e-mail
        Assert.Equal("Can Demir", list.Single(i => i.ResourceId == other.ToString()).ResourceDisplayName);   // stored name over resolved e-mail
    }

    // ── 11 · report state of a written visit ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_written_visit_says_reported_or_none_and_the_week_counts_its_reported_visits()
    {
        var env = WeeklyEnv();
        var atoms = await ApproveAndStoreAsync(env, Week7Sep, Wed2Sep);
        var reported = atoms.Single(a => a.ContactId == env.DoctorA);
        env.Reports.Items.Add(new Domain.Entities.VisitReport
        {
            Id = Guid.NewGuid(), TenantId = Tenant, PlannedVisitId = reported.Id,
            ExecutionOutcome = VisitExecutionOutcome.Completed, ReportStatus = VisitReportStatus.Submitted,
            ExecutedAt = new DateTimeOffset(2026, 9, 8, 10, 0, 0, TimeSpan.Zero), ReportedByResourceId = "rep-1"
        });

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Wed2Sep), default)).Preview!;

        Assert.Equal(PlannedSlotReportStatuses.Reported, preview.Scheduled.Single(s => s.VisitRef == reported.Id).ReportStatus);
        var other = atoms.Single(a => a.ContactId == env.DoctorB);
        Assert.Equal(PlannedSlotReportStatuses.None, preview.Scheduled.Single(s => s.VisitRef == other.Id).ReportStatus);
        Assert.All(preview.Scheduled.Where(s => !s.IsFixed), s => Assert.Equal(PlannedSlotReportStatuses.None, s.ReportStatus));
        Assert.Equal(1, preview.Weeks!.Single(w => w.WeekStart == Week7Sep).ReportedVisitCount);
    }

    // ── fakes ────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The MDM product names (counted bulk calls).</summary>
    private sealed class FakeProductNames : IProductNameReader
    {
        public Dictionary<Guid, string> Names { get; } = new();
        public int Calls { get; private set; }

        public Task<IReadOnlyDictionary<Guid, string>> ReadNamesAsync(IReadOnlyCollection<Guid> productIds, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult<IReadOnlyDictionary<Guid, string>>(
                productIds.Where(Names.ContainsKey).ToDictionary(id => id, id => Names[id]));
        }
    }

    private sealed class FixedNames : Dictionary<Guid, string>, IUserDisplayNameResolver
    {
        public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, string>>(userIds.Where(ContainsKey).ToDictionary(id => id, id => this[id]));
    }

    private static string Page(int count, int total, params (Guid Id, string Name)[] named)
    {
        var rows = named.Select(n => $"{{\"id\":\"{n.Id}\",\"canonicalCode\":\"GP\",\"globalProductName\":\"{n.Name}\"}}")
            .Concat(Enumerable.Range(0, count - named.Length).Select(_ => $"{{\"id\":\"{Guid.NewGuid()}\",\"canonicalCode\":\"GP\",\"globalProductName\":\"X\"}}"));
        return $"{{\"data\":{{\"items\":[{string.Join(",", rows)}],\"pageNumber\":1,\"pageSize\":100,\"totalCount\":{total}}},\"isSuccessful\":true,\"statusCode\":200}}";
    }

    private static MdmProductNameReader Reader(SelectorPages handler)
        => new(new HttpClient(handler),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gateway.test" }).Build(),
            new HttpContextAccessor(), TenantOf(Tenant));

    private sealed class SelectorPages(params string[] pages) : HttpMessageHandler
    {
        public HttpStatusCode Status { get; init; } = HttpStatusCode.OK;
        public List<string> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!.PathAndQuery.TrimStart('/'));
            var body = Status == HttpStatusCode.OK && Requests.Count <= pages.Length ? pages[Requests.Count - 1] : "{}";
            return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
