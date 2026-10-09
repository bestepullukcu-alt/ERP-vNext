using System.Net;
using System.Text;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.CycleCapacity.Read;
using Diten.CrmService.Application.Features.CycleCapacity.Services;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.PlannedVisit;
using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Application.Features.VisitReport.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitWorkspace;
using Diten.CrmService.Application.Features.VisitWorkspace.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitWorkspace.Queries;
using Diten.CrmService.Application.Tests.PlannedVisit;
using Diten.CrmService.Application.Tests.VisitScope;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Infrastructure.CycleCapacity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;
using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;
using PlanAtom = Diten.CrmService.Domain.Entities.PlannedVisit;
using VisitReportEntity = Diten.CrmService.Domain.Entities.VisitReport;

namespace Diten.CrmService.Application.Tests.VisitReport;

/// <summary>
/// W2-BE-c — on the PRODUCTION cache, handlers and working-calendar client: C1 the draft preview is computed once per
/// key (tenant + session + version + UTC day + written-visit stamp) and recomputed when any part changes, shared by the
/// calendar and the reschedule options; C2 accountDisplayName on written and draft cards; C3 days[].holidayName from the
/// platform calendar's holiday name; C4 weeks[].unplaced[] (named, = unplacedCount) and weeks[].sessionVersion.
/// </summary>
public sealed class VisitWorkspaceW2cTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherTenant = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PeriodId = Guid.Parse("30000000-0000-0000-0000-000000000030");
    private static readonly Guid SessionId = Guid.Parse("50000000-0000-0000-0000-000000000050");
    private static readonly Guid Institution = Guid.Parse("a0000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Doctor = Guid.Parse("d0000000-0000-0000-0000-0000000000d1");
    private const string Rep = "rep-1";
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeVisitReportRepository _reports = new();
    private readonly FakePlannedVisitReadRepository _plans = new();
    private readonly Sessions _sessions = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeContactRepository _contacts = new();
    private readonly WorkingDays _workingDays = new();
    private readonly CountingPreview _engine;
    private readonly PlanningSession _session;

    public VisitWorkspaceW2cTests()
    {
        _session = new PlanningSession
        {
            Id = SessionId, TenantId = Tenant, CyclePeriodId = PeriodId, ResourceId = Rep, Status = PlanningSessionStatus.Draft,
            Version = 7,
            Selection = new PlanningSessionSelection
            {
                SelectedContacts = new List<PlanningSessionSelectedContact> { new() { ContactId = Doctor, AccountId = Institution } }
            }
        };
        _sessions.Items.Add(_session);
        _accounts.Items.Add(new Account { Id = Institution, TenantId = Tenant, AccountName = "Şehir Hastanesi", AccountCode = "A1", AccountType = "hospital", Status = "active" });
        _contacts.Items.Add(new Contact { Id = Doctor, TenantId = Tenant, DisplayName = "Dr. Ayşe Kaya", ContactType = "hcp", Status = "active" });
        _engine = new CountingPreview(Preview());
        _workingDays.Holidays[new DateOnly(2026, 10, 29)] = "Cumhuriyet Bayramı";
    }

    private static TenantContext TenantCtx(Guid? id = null)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id ?? Tenant);
        return ctx;
    }

    private static TimeProvider Clock(DateTimeOffset at) => new FixedClock(at);

    private static ICallerScope Caller() => new TestCallerScope(Rep, VisitPlanningPermissions.Apply, VisitPlanningPermissions.PlannedVisitManage);

    private CachedWorkspacePlanPreviewSource Cached(WorkspacePreviewCache cache, DateTimeOffset? at = null, Guid? tenant = null)
        => new(_engine, cache, TenantCtx(tenant), _plans, _reports, Clock(at ?? Now));

    private VisitWorkspaceDays Days()
        => new(TenantCtx(), _sessions, new Periods(), new NoCapacity(), new PlanningWorkingCalendar(new FixedCountry(), _workingDays), _plans);

    private GetWorkspaceCalendarHandler Calendar(IWorkspacePlanPreviewSource previews)
    {
        var names = new VisitTargetNameReader(_accounts, _contacts);
        var w1 = new GetVisitCalendarHandler(TenantCtx(), _plans, _reports, Caller(), names, null, Clock(Now));
        return new GetWorkspaceCalendarHandler(TenantCtx(), Caller(), w1, _plans, _reports, _sessions, Days(), names, previews, Clock(Now));
    }

    private PlanAtom Written(DateOnly date)
    {
        var atom = new PlanAtom
        {
            Id = Guid.NewGuid(), TenantId = Tenant, VisitCode = "W-" + _plans.Items.Count, TargetType = PlannedVisitTargetType.Contact,
            TargetId = Doctor, ContactId = Doctor, AccountId = Institution, PlannedDate = date, PlanStatus = PlannedVisitStatus.Planned,
            Resource = new PlannedVisitResourceRef { ResourceId = Rep, ResourceType = "user" }
        };
        _plans.Items.Add(atom);
        return atom;
    }

    // ═══ C1 · the preview cache ═════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task C1_the_same_key_asks_the_engine_once_and_a_changed_session_asks_again()
    {
        var cache = new WorkspacePreviewCache();
        await Cached(cache).PreviewAsync(_session, default);
        await Cached(cache).PreviewAsync(_session, default); // a second request (a new scoped source), same key
        Assert.Equal(1, _engine.Calls);

        _session.Version++;                                   // any plan change is a version-checked session write
        await Cached(cache).PreviewAsync(_session, default);
        Assert.Equal(2, _engine.Calls);
        await Cached(cache).PreviewAsync(_session, default);
        Assert.Equal(2, _engine.Calls);
    }

    [Fact]
    public async Task C1_a_written_visit_or_its_report_changing_asks_again()
    {
        var cache = new WorkspacePreviewCache();
        var atom = Written(new DateOnly(2026, 10, 14));
        await Cached(cache).PreviewAsync(_session, default);
        Assert.Equal(1, _engine.Calls);

        atom.PlanStatus = PlannedVisitStatus.Cancelled; atom.Version++;  // a cancel
        await Cached(cache).PreviewAsync(_session, default);
        Assert.Equal(2, _engine.Calls);

        _reports.Items.Add(new VisitReportEntity
        {
            Id = Guid.NewGuid(), TenantId = Tenant, PlannedVisitId = atom.Id, ExecutionOutcome = VisitExecutionOutcome.Missed,
            ReportStatus = VisitReportStatus.Draft, ReportedByResourceId = Rep
        });
        await Cached(cache).PreviewAsync(_session, default);             // an outcome
        Assert.Equal(3, _engine.Calls);

        Written(new DateOnly(2026, 10, 15));                              // an unplanned / rescheduled visit
        await Cached(cache).PreviewAsync(_session, default);
        Assert.Equal(4, _engine.Calls);

        // another rep's visits are not in this rep's stamp
        _plans.Items.Add(new PlanAtom { Id = Guid.NewGuid(), TenantId = Tenant, PlannedDate = new DateOnly(2026, 10, 14), PlanStatus = PlannedVisitStatus.Planned, Resource = new PlannedVisitResourceRef { ResourceId = "rep-2" } });
        await Cached(cache).PreviewAsync(_session, default);
        Assert.Equal(4, _engine.Calls);
    }

    [Fact]
    public async Task C1_a_new_utc_day_the_ttl_and_another_tenant_never_reuse_an_entry()
    {
        var cache = new WorkspacePreviewCache();
        await Cached(cache).PreviewAsync(_session, default);
        await Cached(cache, Now.AddMinutes(9)).PreviewAsync(_session, default);      // inside the TTL
        Assert.Equal(1, _engine.Calls);
        await Cached(cache, Now.AddMinutes(11)).PreviewAsync(_session, default);     // past the 10-minute TTL
        Assert.Equal(2, _engine.Calls);

        await Cached(cache, Now.AddMinutes(11), OtherTenant).PreviewAsync(_session, default); // same session id, other tenant
        Assert.Equal(3, _engine.Calls);

        var lateEvening = new DateTimeOffset(2026, 10, 15, 23, 55, 0, TimeSpan.Zero);
        await Cached(cache, lateEvening).PreviewAsync(_session, default);
        Assert.Equal(4, _engine.Calls);
        await Cached(cache, lateEvening.AddMinutes(6)).PreviewAsync(_session, default); // 00:01Z: a new UTC day, inside the TTL
        Assert.Equal(5, _engine.Calls);
        Assert.StartsWith(Tenant.ToString("N") + "|", CachedWorkspacePlanPreviewSource.KeyOf(Tenant, _session, new DateOnly(2026, 10, 15), "x"));
    }

    [Fact]
    public void C1_the_cache_is_bounded()
    {
        var cache = new WorkspacePreviewCache();
        for (var i = 0; i < WorkspacePreviewCache.MaxEntries + 40; i++)
        {
            cache.Set("k" + i, Preview(), Now.AddSeconds(i));
        }

        Assert.True(cache.Count <= WorkspacePreviewCache.MaxEntries);
        Assert.False(cache.TryGet("k0", Now.AddSeconds(300), out _));   // the oldest went first
        Assert.True(cache.TryGet("k" + (WorkspacePreviewCache.MaxEntries + 39), Now.AddSeconds(300), out _));
    }

    [Fact]
    public async Task C1_the_calendar_and_the_reschedule_options_share_one_engine_run()
    {
        var cache = new WorkspacePreviewCache();
        var atom = Written(new DateOnly(2026, 10, 15));
        var calendar = await Calendar(Cached(cache)).Handle(new GetWorkspaceCalendarQuery("2026-10-12", "2026-10-25"), default);
        Assert.True(calendar.IsSuccessful, string.Join(",", calendar.Errors ?? []));
        var options = await new GetRescheduleOptionsHandler(TenantCtx(), _plans, Caller(), Days(), Clock(Now), _sessions, Cached(cache))
            .Handle(new GetRescheduleOptionsQuery(atom.Id), default);
        Assert.True(options.IsSuccessful);
        await Calendar(Cached(cache)).Handle(new GetWorkspaceCalendarQuery("2026-10-12", "2026-10-25"), default);
        Assert.Equal(1, _engine.Calls);
    }

    // ═══ C2 + C3 + C4 · the read fields ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task C2_C3_C4_account_names_holiday_name_unplaced_list_and_session_version()
    {
        Written(new DateOnly(2026, 10, 14));
        var res = await Calendar(Cached(new WorkspacePreviewCache())).Handle(new GetWorkspaceCalendarQuery("2026-10-12", "2026-11-01"), default);
        Assert.True(res.IsSuccessful, string.Join(",", res.Errors ?? []));
        var dto = res.Data!;

        // C2 — written and draft cards carry the institution name
        var written = Assert.Single(dto.Visits, v => v.PlannedVisitId is not null);
        Assert.Equal("Şehir Hastanesi", written.AccountDisplayName);
        var draft = Assert.Single(dto.Visits, v => v.WorkStatus == VisitWorkspaceLimits.DraftWorkStatus);
        Assert.Equal("Şehir Hastanesi", draft.AccountDisplayName);

        // C3 — the platform's holiday name on the holiday, nothing on a working day
        var holiday = dto.Days.Single(d => d.Date == "2026-10-29");
        Assert.True(holiday.IsHoliday);
        Assert.Equal("Cumhuriyet Bayramı", holiday.HolidayName);
        Assert.Null(dto.Days.Single(d => d.Date == "2026-10-28").HolidayName);

        // C4 — the draft week's unplaced visits, named; count = unplacedCount; the session version
        var week = dto.Weeks.Single(w => w.WeekStart == "2026-10-19");
        Assert.Equal(7, week.SessionVersion);
        var unplaced = Assert.Single(week.Unplaced!);
        Assert.Equal(week.UnplacedCount, week.Unplaced!.Count);
        Assert.Equal((PlannedVisitTargetType.Contact, Doctor, "Dr. Ayşe Kaya", "Şehir Hastanesi", "capacity_full"),
            (unplaced.TargetType, unplaced.TargetId, unplaced.DisplayName, unplaced.AccountDisplayName, unplaced.Reason));
        var noPlan = dto.Weeks.Single(w => w.WeekStart == "2026-10-26");
        Assert.Equal(WorkspaceWeekStates.None, noPlan.State);
        Assert.Null(noPlan.SessionVersion);
        Assert.Empty(noPlan.Unplaced!);
    }

    [Fact]
    public async Task C3_the_working_calendar_client_reads_the_platform_holiday_name()
    {
        var json = "{\"isSuccessful\":true,\"data\":{\"resolution\":\"resolved\",\"isWorkingDay\":false,\"selectionReason\":\"holiday\","
                   + "\"reasonCodes\":[\"public_holiday\"],\"holiday\":{\"dayName\":\"Cumhuriyet Bayramı\",\"isHalfDay\":false}}}";
        var client = new WorkingCalendarWorkingDayCounter(
            new HttpClient(new Json(json)),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://gw.test" }).Build(),
            new HttpContextAccessor(),
            TenantCtx());

        var answer = await client.IsWorkingDayAsync("TR", null, new DateOnly(2026, 10, 29), default);

        Assert.Equal(false, answer.IsWorkingDay);
        Assert.Equal("Cumhuriyet Bayramı", answer.HolidayName);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    private static VisitPlanPreview Preview()
        => new(
            SessionId, PeriodId, Rep, "2026-10-01", "2026-11-30", 2,
            new[]
            {
                new PlannedSlotPreview(Guid.NewGuid(), 1, PlannedVisitTargetType.Contact, Doctor, Institution, Doctor, null,
                    "2026-10-20", "09:00", "09:30", 1, 30, null, null, null, 1, 0, "resolved", WeekStart: "2026-10-19")
            },
            new[] { new UnscheduledPreview(1, PlannedVisitTargetType.Contact, Doctor, Doctor, "capacity_full") },
            Array.Empty<DoctorContentPreview>(), Array.Empty<TerritoryWarning>(),
            new SupplyDemandSummary(null, 0, 1, 1, "ok", Array.Empty<string>()), Now,
            Weeks: new[]
            {
                new PlanningWeekDto("2026-10-12", 42, "2026-10-12", "2026-10-18", PlanningWeekDisplayStatus.Approved, 1),
                new PlanningWeekDto("2026-10-19", 43, "2026-10-19", "2026-10-25", PlanningWeekDisplayStatus.Draft, 1)
            });

    private sealed class CountingPreview(VisitPlanPreview preview) : IWorkspacePlanPreviewSource
    {
        public int Calls { get; private set; }

        public Task<VisitPlanPreview?> PreviewAsync(PlanningSession session, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<VisitPlanPreview?>(preview);
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class Json(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }

    private sealed class Sessions : IPlanningSessionRepository
    {
        public List<PlanningSession> Items { get; } = new();

        public Task<PlanningSession?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(s => s.Id == id && s.TenantId == tenantId));

        public Task<IReadOnlyList<PlanningSession>> ListAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PlanningSession>>(Items.Where(s => s.TenantId == tenantId).ToList());

        public Task<IReadOnlyList<PlanningSession>> ListByPeriodAndResourceAsync(Guid tenantId, Guid cyclePeriodId, string resourceId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PlanningSession>>(Items.Where(s => s.CyclePeriodId == cyclePeriodId && s.ResourceId == resourceId).ToList());

        public Task InsertAsync(PlanningSession entity, CancellationToken ct) => Task.CompletedTask;

        public Task<bool> ReplaceAsync(PlanningSession entity, int expectedVersion, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class Periods : ICyclePeriodReader
    {
        public Task<CyclePeriodResolution> ResolveActiveAsync(DateTimeOffset at, string? country, Guid? legalEntityId, string? businessUnitId, CancellationToken ct)
            => Task.FromResult(new CyclePeriodResolution("none", null, Array.Empty<Guid>(), null, null));

        public Task<CyclePeriodSnapshot?> GetByIdAsync(Guid cyclePeriodId, CancellationToken ct)
            => Task.FromResult<CyclePeriodSnapshot?>(cyclePeriodId == PeriodId
                ? new CyclePeriodSnapshot(PeriodId, "C10", "Oct-Nov", 2026, 10,
                    new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 30, 0, 0, 0, TimeSpan.Zero),
                    "active", "tenant", null, null, null, null)
                : null);

        public Task<IReadOnlyList<CyclePeriodSnapshot>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CyclePeriodSnapshot>>(Array.Empty<CyclePeriodSnapshot>());

        public Task<IReadOnlyList<CyclePeriodSnapshot>> ListByYearAsync(int year, string? scopeType, string? scopeRef, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CyclePeriodSnapshot>>(Array.Empty<CyclePeriodSnapshot>());
    }

    private sealed class NoCapacity : ICycleCapacityRepository
    {
        public Task<CapacityEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => Task.FromResult<CapacityEntity?>(null);

        public Task<CapacityEntity?> GetByCyclePeriodAsync(Guid tenantId, Guid cyclePeriodId, CancellationToken ct) => Task.FromResult<CapacityEntity?>(null);

        public Task<IReadOnlyList<CapacityEntity>> ListAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CapacityEntity>>(Array.Empty<CapacityEntity>());

        public Task InsertAsync(CapacityEntity entity, CancellationToken ct) => Task.CompletedTask;

        public Task<bool> ReplaceAsync(CapacityEntity entity, int expectedVersion, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class FixedCountry : ICycleCapacityCountryResolver
    {
        public CycleCapacityCountryResolution Resolve(CyclePeriodSnapshot period, string? authoredCountryCode) => new("TR", IsDerived: true, null, null);
    }

    private sealed class WorkingDays : IWorkingDayChecker
    {
        public Dictionary<DateOnly, string> Holidays { get; } = new();

        public Task<WorkingDayCheckResult> IsWorkingDayAsync(string countryCode, Guid? legalEntityId, DateOnly date, CancellationToken cancellationToken)
        {
            if (Holidays.TryGetValue(date, out var name))
            {
                return Task.FromResult(new WorkingDayCheckResult(
                    CycleCapacityResolutions.Resolved, false, new[] { "public_holiday" }, "holiday", HolidayName: name));
            }

            var working = date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
            return Task.FromResult(new WorkingDayCheckResult(
                CycleCapacityResolutions.Resolved, working, working ? new[] { "ok" } : new[] { "weekend" }, "ok"));
        }
    }
}
