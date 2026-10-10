using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.CycleCapacity.Read;
using Diten.CrmService.Application.Features.CycleCapacity.Services;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.PlannedVisit;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Application.Features.VisitReport.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitWorkspace;
using Diten.CrmService.Application.Features.VisitWorkspace.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitWorkspace.Queries;
using Diten.CrmService.Application.Tests.PlannedVisit;
using Diten.CrmService.Application.Tests.VisitScope;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;
using PlanAtom = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Tests.VisitReport;

/// <summary>
/// W2-BE-d — the workspace calendar's panel data on the PRODUCTION handler: (1) periodName; (2) specialtyCode +
/// specialtyLabel from the medical-specialty reference set in the request language (label_tr when present, else the
/// display name — never a label in code); (3) badges = the 3D segment badges; (4) accountAddress = line · district, city
/// (reference labels; a code without a label is left out); (5) pinMoveReason on a draft visit the engine moved (a time
/// pin conflict); (6) the cache stamp reads the rep's plans by resource, not the whole tenant.
/// </summary>
public sealed class VisitWorkspaceW2dTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PeriodId = Guid.Parse("30000000-0000-0000-0000-000000000030");
    private static readonly Guid SessionId = Guid.Parse("50000000-0000-0000-0000-000000000050");
    private static readonly Guid Institution = Guid.Parse("a0000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Doctor = Guid.Parse("d0000000-0000-0000-0000-0000000000d1");
    private static readonly Guid Doctor2 = Guid.Parse("d0000000-0000-0000-0000-0000000000d2");
    private static readonly Guid Segment = Guid.Parse("5e000000-0000-0000-0000-0000000000e1");
    private const string Rep = "rep-1";
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeVisitReportRepository _reports = new();
    private readonly FakePlannedVisitReadRepository _plans = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeContactRepository _contacts = new();
    private readonly Sessions _sessions = new();
    private readonly Catalog _catalog = new();

    public VisitWorkspaceW2dTests()
    {
        _sessions.Items.Add(new PlanningSession
        {
            Id = SessionId, TenantId = Tenant, CyclePeriodId = PeriodId, ResourceId = Rep, Status = PlanningSessionStatus.Draft, Version = 3
        });
        _accounts.Items.Add(new Account
        {
            Id = Institution, TenantId = Tenant, AccountName = "Şehir Hastanesi", AccountCode = "A1", AccountType = "hospital", Status = "active",
            AddressLine = "Atatürk Cad. 12", CityRef = "TR-10-BALIKESIR", DistrictRef = "TR-10-BANDIRMA"
        });
        _contacts.Items.Add(new Contact { Id = Doctor, TenantId = Tenant, DisplayName = "Dr. Ayşe Kaya", ContactType = "hcp", Status = "active", Specialty = "cardiology" });
        _contacts.Items.Add(new Contact { Id = Doctor2, TenantId = Tenant, DisplayName = "Dr. Can Er", ContactType = "hcp", Status = "active", Specialty = "dermatology" });

        _catalog.Sets["medical-specialty"] = new[]
        {
            Value("cardiology", "Cardiology", ("label_tr", "Kardiyoloji")),
            Value("dermatology", "Dermatology") // no label_tr → the display name
        };
        _catalog.Sets["city"] = new[] { Value("TR-10-BALIKESIR", "Balıkesir") };
        _catalog.Sets["district"] = new[] { Value("TR-10-BANDIRMA", "Bandırma") };

        _plans.Items.Add(new PlanAtom
        {
            Id = Guid.NewGuid(), TenantId = Tenant, VisitCode = "W-1", TargetType = PlannedVisitTargetType.Contact, TargetId = Doctor,
            ContactId = Doctor, AccountId = Institution, PlannedDate = new DateOnly(2026, 10, 14), PlanStatus = PlannedVisitStatus.Planned,
            Resource = new PlannedVisitResourceRef { ResourceId = Rep, ResourceType = "user" }
        });
    }

    private static ReferenceValueSnapshot Value(string code, string name, params (string Key, string Value)[] attrs)
        => new(code, name, null, true, false, attrs.ToDictionary(a => a.Key, a => a.Value));

    private static TenantContext TenantCtx()
    {
        var ctx = new TenantContext();
        ctx.SetTenant(Tenant);
        return ctx;
    }

    private static ICallerScope Caller() => new TestCallerScope(Rep);

    private GetWorkspaceCalendarHandler Calendar(IReferenceDataCatalogReader? catalog = null)
    {
        var names = new VisitTargetNameReader(_accounts, _contacts);
        var w1 = new GetVisitCalendarHandler(TenantCtx(), _plans, _reports, Caller(), names, null, new FixedClock(Now));
        var days = new VisitWorkspaceDays(TenantCtx(), _sessions, new Periods(), new NoCapacity(),
            new PlanningWorkingCalendar(new FixedCountry(), new WorkingDays()), _plans);
        return new GetWorkspaceCalendarHandler(
            TenantCtx(), Caller(), w1, _plans, _reports, _sessions, days, names, new FixedPreview(Preview()), new FixedClock(Now),
            new Segments(), catalog ?? _catalog, new Periods());
    }

    private async Task<WorkspaceCalendarDto> Read(string? lang)
    {
        var res = await Calendar().Handle(new GetWorkspaceCalendarQuery("2026-10-12", "2026-10-25", null, lang), default);
        Assert.True(res.IsSuccessful, string.Join(",", res.Errors ?? []));
        return res.Data!;
    }

    // ═══ 1 · period name ══════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task D1_the_calendar_names_its_period()
        => Assert.Equal("Ekim–Kasım 2026", (await Read("tr")).PeriodName);

    // ═══ 2 · specialty label by language ═════════════════════════════════════════════════════════════════

    [Fact]
    public async Task D2_the_specialty_label_is_label_tr_when_present_else_the_display_name()
    {
        var tr = await Read("tr-TR,tr;q=0.9");
        var written = Assert.Single(tr.Visits, v => v.PlannedVisitId is not null);
        Assert.Equal(("cardiology", "Kardiyoloji"), (written.SpecialtyCode, written.SpecialtyLabel));
        var draft = Assert.Single(tr.Visits, v => v.ContactId == Doctor2);
        Assert.Equal(("dermatology", "Dermatology"), (draft.SpecialtyCode, draft.SpecialtyLabel)); // no label_tr

        var en = await Read("en");
        Assert.Equal("Cardiology", en.Visits.Single(v => v.PlannedVisitId is not null).SpecialtyLabel);

        // an unpublished set: the code stays, no label is invented
        var down = (await Calendar(new Catalog()).Handle(new GetWorkspaceCalendarQuery("2026-10-12", "2026-10-25", null, "tr"), default)).Data!;
        var w = down.Visits.Single(v => v.PlannedVisitId is not null);
        Assert.Equal("cardiology", w.SpecialtyCode);
        Assert.Null(w.SpecialtyLabel);
    }

    // ═══ 3 + 4 · badges + address ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task D3_D4_segment_badges_and_the_institution_address()
    {
        var dto = await Read("tr");
        var written = dto.Visits.Single(v => v.PlannedVisitId is not null);
        Assert.Equal(new[] { "A Potansiyel" }, written.Badges);
        Assert.Empty(dto.Visits.Single(v => v.ContactId == Doctor2).Badges!);
        Assert.Equal("Atatürk Cad. 12 · Bandırma, Balıkesir", written.AccountAddress);

        // a city / district code without a published label is left out, never shown raw
        var bare = (await Calendar(new Catalog()).Handle(new GetWorkspaceCalendarQuery("2026-10-12", "2026-10-25", null, "tr"), default)).Data!;
        Assert.Equal("Atatürk Cad. 12", bare.Visits.Single(v => v.PlannedVisitId is not null).AccountAddress);
    }

    // ═══ 5 · pin move reason ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task D5_a_draft_visit_moved_by_a_time_pin_conflict_says_why()
    {
        var dto = await Read("tr");
        var moved = dto.Visits.Single(v => v.ContactId == Doctor2);
        Assert.Equal(PlanningDayPins.PinTimeConflict, moved.PinMoveReason);
        Assert.Null(dto.Visits.Single(v => v.PlannedVisitId is not null).PinMoveReason);
    }

    // ═══ 6 · the stamp reads by resource ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task D6_the_cache_stamp_reads_the_reps_plans_by_resource()
    {
        var repo = new RecordingPlans(_plans);
        var source = new CachedWorkspacePlanPreviewSource(
            new FixedPreview(Preview()), new WorkspacePreviewCache(), TenantCtx(), repo, _reports, new FixedClock(Now));
        await source.PreviewAsync(_sessions.Items[0], default);

        Assert.Equal(new[] { Rep }, repo.ByResource);
        Assert.Equal(0, repo.WholeTenantReads);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    private static VisitPlanPreview Preview()
        => new(
            SessionId, PeriodId, Rep, "2026-10-01", "2026-11-30", 2,
            new[]
            {
                new PlannedSlotPreview(Guid.NewGuid(), 1, PlannedVisitTargetType.Contact, Doctor2, Institution, Doctor2, null,
                    "2026-10-20", "10:45", "11:15", 1, 30, null, null, null, 1, 0, "resolved", WeekStart: "2026-10-19", IsPinned: true)
            },
            Array.Empty<UnscheduledPreview>(), Array.Empty<DoctorContentPreview>(), Array.Empty<TerritoryWarning>(),
            new SupplyDemandSummary(null, 0, 1, 0, "ok", Array.Empty<string>()), Now,
            Weeks: new[]
            {
                new PlanningWeekDto("2026-10-12", 42, "2026-10-12", "2026-10-18", PlanningWeekDisplayStatus.Approved, 1),
                new PlanningWeekDto("2026-10-19", 43, "2026-10-19", "2026-10-25", PlanningWeekDisplayStatus.Draft, 1)
            },
            PinOverflow: new[]
            {
                new PinOverflowPreview(PlannedVisitTargetType.Contact, Doctor2, Doctor2, null, "2026-10-20", "2026-10-20", PlanningDayPins.PinTimeConflict)
            });

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FixedPreview(VisitPlanPreview preview) : IWorkspacePlanPreviewSource
    {
        public Task<VisitPlanPreview?> PreviewAsync(PlanningSession session, CancellationToken cancellationToken)
            => Task.FromResult<VisitPlanPreview?>(preview);
    }

    private sealed class Catalog : IReferenceDataCatalogReader
    {
        public Dictionary<string, ReferenceValueSnapshot[]> Sets { get; } = new();

        public Task<ReferenceSetSnapshot> GetPublishedValuesAsync(string setCode, CancellationToken cancellationToken)
            => Task.FromResult(Sets.TryGetValue(setCode, out var values)
                ? new ReferenceSetSnapshot(setCode, true, values)
                : ReferenceSetSnapshot.NotPublished(setCode));
    }

    private sealed class Segments : IContactSegmentSetReader
    {
        public Task<ContactSegmentSet> ReadAsync(Guid tenantId, IReadOnlyCollection<Guid> contactIds, DateTimeOffset effectiveAt, CancellationToken cancellationToken)
            => Task.FromResult(new ContactSegmentSet(
                contactIds.ToDictionary(id => id, id => (IReadOnlyList<Guid>)(id == Doctor ? new[] { Segment } : Array.Empty<Guid>())),
                new Dictionary<Guid, string> { [Segment] = "A Potansiyel" }));
    }

    /// <summary>Counts how the stamp reads plans: by resource (the W2-BE-d read) or the whole tenant.</summary>
    private sealed class RecordingPlans(FakePlannedVisitReadRepository inner) : IPlannedVisitRepository
    {
        public List<string> ByResource { get; } = new();
        public int WholeTenantReads { get; private set; }

        public Task<IReadOnlyList<PlanAtom>> ListByResourceAsync(Guid tenantId, string resourceId, CancellationToken ct)
        {
            ByResource.Add(resourceId);
            return Task.FromResult<IReadOnlyList<PlanAtom>>(inner.Items.Where(p => p.TenantId == tenantId && p.Resource.ResourceId == resourceId).ToList());
        }

        public Task<IReadOnlyList<PlanAtom>> ListAsync(Guid tenantId, CancellationToken ct)
        {
            WholeTenantReads++;
            return inner.ListAsync(tenantId, ct);
        }

        public Task<PlanAtom?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => inner.GetByIdAsync(tenantId, id, ct);
        public Task<IReadOnlyList<PlanAtom>> ListByCodeAsync(Guid tenantId, string visitCode, CancellationToken ct) => inner.ListByCodeAsync(tenantId, visitCode, ct);
        public Task<IReadOnlyList<PlanAtom>> ListByResourceAndDateAsync(Guid tenantId, string resourceId, DateOnly plannedDate, CancellationToken ct) => inner.ListByResourceAndDateAsync(tenantId, resourceId, plannedDate, ct);
        public Task<IReadOnlyList<PlanAtom>> ListByTargetAndDateAsync(Guid tenantId, Guid targetId, DateOnly plannedDate, CancellationToken ct) => inner.ListByTargetAndDateAsync(tenantId, targetId, plannedDate, ct);
        public Task<IReadOnlyList<PlanAtom>> ListFromDateByContentPathsAsync(Guid tenantId, IReadOnlyCollection<Guid> pathIds, DateOnly fromDate, CancellationToken ct) => inner.ListFromDateByContentPathsAsync(tenantId, pathIds, fromDate, ct);
        public Task InsertAsync(PlanAtom entity, CancellationToken ct) => inner.InsertAsync(entity, ct);
        public Task<bool> ReplaceAsync(PlanAtom entity, int expectedVersion, CancellationToken ct) => inner.ReplaceAsync(entity, expectedVersion, ct);
    }

    private sealed class Sessions : IPlanningSessionRepository
    {
        public List<PlanningSession> Items { get; } = new();

        public Task<PlanningSession?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(s => s.Id == id));
        public Task<IReadOnlyList<PlanningSession>> ListAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<PlanningSession>>(Items.ToList());
        public Task<IReadOnlyList<PlanningSession>> ListByPeriodAndResourceAsync(Guid tenantId, Guid cyclePeriodId, string resourceId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PlanningSession>>(Items.ToList());
        public Task InsertAsync(PlanningSession entity, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ReplaceAsync(PlanningSession entity, int expectedVersion, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class Periods : ICyclePeriodReader
    {
        public Task<CyclePeriodResolution> ResolveActiveAsync(DateTimeOffset at, string? country, Guid? legalEntityId, string? businessUnitId, CancellationToken ct)
            => Task.FromResult(new CyclePeriodResolution("none", null, Array.Empty<Guid>(), null, null));

        public Task<CyclePeriodSnapshot?> GetByIdAsync(Guid cyclePeriodId, CancellationToken ct)
            => Task.FromResult<CyclePeriodSnapshot?>(cyclePeriodId == PeriodId
                ? new CyclePeriodSnapshot(PeriodId, "C10", "Ekim–Kasım 2026", 2026, 10,
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
        public Task<IReadOnlyList<CapacityEntity>> ListAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<CapacityEntity>>(Array.Empty<CapacityEntity>());
        public Task InsertAsync(CapacityEntity entity, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ReplaceAsync(CapacityEntity entity, int expectedVersion, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class FixedCountry : ICycleCapacityCountryResolver
    {
        public CycleCapacityCountryResolution Resolve(CyclePeriodSnapshot period, string? authoredCountryCode) => new("TR", IsDerived: true, null, null);
    }

    private sealed class WorkingDays : IWorkingDayChecker
    {
        public Task<WorkingDayCheckResult> IsWorkingDayAsync(string countryCode, Guid? legalEntityId, DateOnly date, CancellationToken cancellationToken)
        {
            var working = date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
            return Task.FromResult(new WorkingDayCheckResult(
                CycleCapacityResolutions.Resolved, working, working ? new[] { "ok" } : new[] { "weekend" }, "ok"));
        }
    }
}
