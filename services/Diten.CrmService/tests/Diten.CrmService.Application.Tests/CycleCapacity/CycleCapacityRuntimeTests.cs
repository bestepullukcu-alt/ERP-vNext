using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.CycleCapacity;
using Diten.CrmService.Application.Features.CycleCapacity.Commands;
using Diten.CrmService.Application.Features.CycleCapacity.Contract;
using Diten.CrmService.Application.Features.CycleCapacity.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.CycleCapacity.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.CycleCapacity.Queries;
using Diten.CrmService.Application.Features.CycleCapacity.Read;
using Diten.CrmService.Application.Features.CycleCapacity.Rules;
using Diten.CrmService.Application.Features.CycleCapacity.Services;
using Diten.CrmService.Application.Features.CyclePeriod;
using Diten.CrmService.Application.Features.CyclePeriod.Contract;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;

namespace Diten.CrmService.Application.Tests.CycleCapacity;

/// <summary>
/// MOD-0155 FU06 — the boundary and behaviour tests.
/// <para>Grouped by the claim each one defends rather than by class, because the claims are what the pack promises:
/// the formula, the fail-closed calendar, the 1:1 pin, the closed-period lock, the never-persisted projection, and the
/// CyclePeriod contract staying exactly where FU07 left it.</para>
/// </summary>
public sealed class CycleCapacityRuntimeTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid LegalEntityX = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Mar1 = new(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Apr30 = new(2026, 4, 30, 0, 0, 0, TimeSpan.Zero);

    // ── fakes ────────────────────────────────────────────────────────────────────────────────────────────────────

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class FakeCapacityRepo : ICycleCapacityRepository
    {
        public List<CapacityEntity> Items { get; } = new();

        private IReadOnlyList<CapacityEntity> Scope(Guid tenantId)
            => Items.Where(x => x.TenantId == tenantId && !x.IsDeleted).ToList();

        /// <summary>
        /// Hands back a COPY, exactly as Mongo hands back a freshly deserialized document. Returning the stored
        /// reference would let a REJECTED write mutate the store in place — something no real repository can do, and
        /// it would quietly hide a concurrency defect instead of exposing one.
        /// </summary>
        private static CapacityEntity? Copy(CapacityEntity? source)
            => source is null
                ? null
                : new CapacityEntity
                {
                    Id = source.Id,
                    TenantId = source.TenantId,
                    CyclePeriodId = source.CyclePeriodId,
                    CalendarCountryCode = source.CalendarCountryCode,
                    DailyWorkMinutes = source.DailyWorkMinutes,
                    PromoProductTime = source.PromoProductTime,
                    NonPromoProductTime = source.NonPromoProductTime,
                    TravelingTime = source.TravelingTime,
                    ReportDuration = source.ReportDuration,
                    QuizDuration = source.QuizDuration,
                    Description = source.Description,
                    IsArchived = source.IsArchived,
                    Version = source.Version,
                    IsDeleted = source.IsDeleted,
                    DeletedAt = source.DeletedAt,
                    CreatedAt = source.CreatedAt,
                    CreatedBy = source.CreatedBy,
                    UpdatedAt = source.UpdatedAt,
                    UpdatedBy = source.UpdatedBy,
                    Months = source.Months.Select(m => new CycleCapacityMonth
                    {
                        Year = m.Year,
                        MonthNumber = m.MonthNumber,
                        MeetingDays = m.MeetingDays,
                        TrainingDays = m.TrainingDays,
                        VacationDays = m.VacationDays,
                        MicroTargetingDayCount = m.MicroTargetingDayCount,
                        MicroTargetingDuration = m.MicroTargetingDuration,
                        Fte = m.Fte,
                        FteSource = m.FteSource
                    }).ToList()
                };

        public Task<CapacityEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Copy(Scope(tenantId).FirstOrDefault(x => x.Id == id)));

        public Task<CapacityEntity?> GetByCyclePeriodAsync(Guid tenantId, Guid cyclePeriodId, CancellationToken ct)
            => Task.FromResult(Copy(Scope(tenantId).FirstOrDefault(x => x.CyclePeriodId == cyclePeriodId && !x.IsArchived)));

        public Task<IReadOnlyList<CapacityEntity>> ListAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult(Scope(tenantId));

        public Task InsertAsync(CapacityEntity entity, CancellationToken ct)
        {
            Items.Add(entity);
            return Task.CompletedTask;
        }

        public Task<bool> ReplaceAsync(CapacityEntity entity, int expectedVersion, CancellationToken ct)
        {
            var existing = Items.FirstOrDefault(x => x.Id == entity.Id && x.TenantId == entity.TenantId);
            if (existing is null || existing.Version != expectedVersion)
            {
                return Task.FromResult(false);
            }

            entity.Version = expectedVersion + 1;
            Items[Items.IndexOf(existing)] = entity;
            return Task.FromResult(true);
        }
    }

    /// <summary>A read-only period seam. It has no write path at all — which is the structural half of "FU06 never
    /// writes to CyclePeriod".</summary>
    private sealed class FakePeriodReader : ICyclePeriodReader
    {
        public List<CyclePeriodSnapshot> Periods { get; } = new();

        public Task<CyclePeriodResolution> ResolveActiveAsync(
            DateTimeOffset at, string? country, Guid? legalEntityId, string? businessUnitId, CancellationToken ct)
            => Task.FromResult(new CyclePeriodResolution(
                CyclePeriodResolutionOutcomes.None, null, Array.Empty<Guid>(), null, null));

        public Task<CyclePeriodSnapshot?> GetByIdAsync(Guid cyclePeriodId, CancellationToken ct)
            => Task.FromResult(Periods.FirstOrDefault(p => p.CyclePeriodId == cyclePeriodId));

        public Task<IReadOnlyList<CyclePeriodSnapshot>> GetByIdsAsync(
            IReadOnlyCollection<Guid> ids, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CyclePeriodSnapshot>>(
                Periods.Where(p => ids.Contains(p.CyclePeriodId)).ToList());

        public Task<IReadOnlyList<CyclePeriodSnapshot>> ListByYearAsync(
            int year, string? scopeType, string? scopeRef, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CyclePeriodSnapshot>>(
                Periods.Where(p => p.Year == year).ToList());
    }

    private sealed class FakeReferences : IReferenceDataValidator
    {
        public ReferenceValidationStatus Status { get; set; } = ReferenceValidationStatus.Valid;
        public List<string> AskedSets { get; } = new();
        public int Calls { get; private set; }

        public Task<ReferenceValidationResult> ValidateAsync(string setCode, string value, CancellationToken ct)
        {
            Calls++;
            AskedSets.Add(setCode);
            return Task.FromResult(new ReferenceValidationResult(Status, setCode, value));
        }
    }

    private sealed class FakeWorkingDayCounter : IWorkingDayCounter
    {
        public string Resolution { get; set; } = CycleCapacityResolutions.Resolved;
        public int WorkingDays { get; set; } = 21;

        /// <summary>Per-month overrides, so a test can make exactly ONE month fail.</summary>
        public Dictionary<(int Year, int Month), (string Resolution, int? Days)> ByMonth { get; } = new();

        public List<(string Country, Guid? LegalEntityId, DateOnly From, DateOnly To)> Calls { get; } = new();

        public Task<WorkingDayCountResult> CountAsync(
            string countryCode, Guid? legalEntityId, DateOnly from, DateOnly to, CancellationToken ct)
        {
            Calls.Add((countryCode, legalEntityId, from, to));

            if (ByMonth.TryGetValue((from.Year, from.Month), out var custom))
            {
                return Task.FromResult(new WorkingDayCountResult(
                    custom.Resolution, custom.Days, new[] { "test" }, "test"));
            }

            return Task.FromResult(new WorkingDayCountResult(
                Resolution,
                Resolution == CycleCapacityResolutions.Resolved ? WorkingDays : (int?)null,
                new[] { "test" },
                "test"));
        }
    }

    private sealed class FakeDefaults : ICycleCapacityDefaultsProvider
    {
        public FakeDefaults(int dailyWorkMinutes = 480, decimal fte = 12.00m)
            => Current = new CycleCapacityDefaults(dailyWorkMinutes, fte);

        public CycleCapacityDefaults Current { get; }
    }

    // ── builders ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static CyclePeriodSnapshot Period(
        Guid id,
        string status = CyclePeriodStatuses.Active,
        string scopeType = CyclePeriodScopeTypes.Tenant,
        string? country = null,
        Guid? legalEntityId = null,
        string? businessUnitId = null,
        DateTimeOffset? start = null,
        DateTimeOffset? end = null)
        => new(
            id, "c-2026-03", "2026 / cycle 3", 2026, 3,
            start ?? Mar1, end ?? Apr30, status, scopeType,
            scopeType switch
            {
                CyclePeriodScopeTypes.Country => country,
                CyclePeriodScopeTypes.LegalEntity => legalEntityId?.ToString("D"),
                CyclePeriodScopeTypes.BusinessUnit => businessUnitId,
                _ => null
            },
            country, legalEntityId, businessUnitId);

    private static IReadOnlyList<CycleCapacityMonthInput> TwoMonths(
        int meeting = 1, int training = 1, int vacation = 2, int microDays = 3, int microMinutes = 45)
        => new[]
        {
            new CycleCapacityMonthInput(2026, 3, meeting, training, vacation, microDays, microMinutes),
            new CycleCapacityMonthInput(2026, 4, meeting, training, vacation, microDays, microMinutes)
        };

    private static CreateCycleCapacityCommand CreateCommand(
        Guid periodId,
        string? country = "TR",
        int dailyWorkMinutes = 480,
        int promo = 15,
        int nonPromo = 10,
        int traveling = 60,
        int report = 30,
        int quiz = 10,
        IReadOnlyList<CycleCapacityMonthInput>? months = null)
        => new(periodId, country, dailyWorkMinutes, promo, nonPromo, traveling, report, quiz, null,
            months ?? TwoMonths());

    private sealed record Harness(
        FakeCapacityRepo Repo,
        FakePeriodReader Periods,
        FakeReferences References,
        FakeWorkingDayCounter Calendar,
        FakeDefaults Defaults,
        CreateCycleCapacityHandler Create,
        UpdateCycleCapacityHandler Update,
        ArchiveCycleCapacityHandler Archive,
        GetCycleCapacityCalculationHandler Calculation,
        PreviewCycleCapacityCalculationHandler Preview,
        GetCycleCapacityByIdHandler GetById,
        GetCycleCapacityListHandler List);

    private static Harness Build(Guid tenantId, decimal fte = 12.00m)
    {
        var tenant = Tenant(tenantId);
        var actor = new NullActorContext();
        var repo = new FakeCapacityRepo();
        var periods = new FakePeriodReader();
        var references = new FakeReferences();
        var calendar = new FakeWorkingDayCounter();
        var defaults = new FakeDefaults(fte: fte);
        var countries = new CycleCapacityCountryResolver();
        var writes = new CycleCapacityWriteValidator(periods, countries, references, defaults);
        // ONE estimator instance for both surfaces, exactly as the DI container wires it — so a test that passes for
        // the saved capacity and fails for the preview would be a real divergence rather than a fixture artefact.
        var estimator = new CycleCapacityEstimator(countries, calendar);

        return new Harness(
            repo, periods, references, calendar, defaults,
            new CreateCycleCapacityHandler(tenant, actor, repo, writes, defaults),
            new UpdateCycleCapacityHandler(tenant, actor, repo, writes),
            new ArchiveCycleCapacityHandler(tenant, actor, repo, periods),
            new GetCycleCapacityCalculationHandler(tenant, repo, periods, estimator),
            new PreviewCycleCapacityCalculationHandler(tenant, periods, estimator, defaults),
            new GetCycleCapacityByIdHandler(tenant, repo, periods, countries),
            new GetCycleCapacityListHandler(tenant, repo, periods));
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // AC-C — the formula
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>AC-C-1 — the pack's golden example, to the visit.</summary>
    [Fact]
    public void T01_Golden_Example_Produces_The_Documented_Figure()
    {
        var capacity = new CapacityEntity
        {
            DailyWorkMinutes = 480,
            PromoProductTime = 15,
            NonPromoProductTime = 10,
            TravelingTime = 60,
            ReportDuration = 30,
            QuizDuration = 10,
            Months = { new CycleCapacityMonth { Year = 2026, MonthNumber = 3, MeetingDays = 1, TrainingDays = 1, VacationDays = 2, MicroTargetingDayCount = 3, MicroTargetingDuration = 45, Fte = 12.00m } }
        };

        var window = new CycleCapacityMonthRules.MonthWindow(2026, 3, Mar1, new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero));
        var result = CycleCapacityCalculator.Calculate(
            capacity, new[] { new CycleCapacityCalculator.ResolvedMonth(window, 21) });

        var month = Assert.Single(result.Months);
        Assert.Equal(17, month.FieldDays);          // 21 − (1 + 1 + 2)
        Assert.Equal(8160, month.AvailableMinutes); // 480 × 17
        Assert.Equal(1835, month.SpendMinutes);     // 100 × 17 + 3 × 45
        Assert.Equal(6325, month.VisitMinutes);
        Assert.Equal(25, result.MinutesPerVisit);
        Assert.Equal(3036, month.TotalVisitNumber); // round(6325 ÷ 25 × 12)
        Assert.Equal(3036, result.TotalVisitNumber);
        Assert.True(result.IsEstimate);
    }

    /// <summary>
    /// AC-C-2 — weekends and public holidays are NOT subtracted twice. The calculator only ever sees a working-day
    /// COUNT, so there is no weekend input for it to double-count: feeding the same count with wildly different
    /// underlying calendars must produce the identical answer.
    /// </summary>
    [Fact]
    public void T02_Weekends_And_Holidays_Are_Never_Deducted_Twice()
    {
        var capacity = new CapacityEntity
        {
            DailyWorkMinutes = 480, PromoProductTime = 20, NonPromoProductTime = 10,
            TravelingTime = 60, ReportDuration = 30, QuizDuration = 0,
            Months = { new CycleCapacityMonth { Year = 2026, MonthNumber = 3, Fte = 1m } }
        };
        var window = new CycleCapacityMonthRules.MonthWindow(2026, 3, Mar1, new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero));

        // 22 working days in a 31-day month: nine days are weekend/holiday, and none of that is visible here.
        var result = CycleCapacityCalculator.Calculate(
            capacity, new[] { new CycleCapacityCalculator.ResolvedMonth(window, 22) });

        var month = Assert.Single(result.Months);
        Assert.Equal(22, month.FieldDays);              // no deductions were authored, so field days == working days
        Assert.Equal(22 * 480, month.AvailableMinutes); // 31 never appears anywhere
        Assert.Equal(22 * 90, month.SpendMinutes);
    }

    /// <summary>AC-C-3 — a month fully consumed by leave estimates zero visits, and does not throw or produce NaN.</summary>
    [Fact]
    public void T03_Fully_Deducted_Month_Estimates_Zero_Without_Throwing()
    {
        var capacity = new CapacityEntity
        {
            DailyWorkMinutes = 480, PromoProductTime = 15, NonPromoProductTime = 10,
            TravelingTime = 60, ReportDuration = 30, QuizDuration = 10,
            Months = { new CycleCapacityMonth { Year = 2026, MonthNumber = 3, VacationDays = 40, Fte = 12m } }
        };
        var window = new CycleCapacityMonthRules.MonthWindow(2026, 3, Mar1, new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero));

        var result = CycleCapacityCalculator.Calculate(
            capacity, new[] { new CycleCapacityCalculator.ResolvedMonth(window, 21) });

        var month = Assert.Single(result.Months);
        Assert.Equal(0, month.FieldDays);            // clamped, never negative
        Assert.Equal(0, month.TotalVisitNumber);
        Assert.Equal(0, result.TotalVisitNumber);    // 0 is an ANSWER, and it is not null
        Assert.NotNull(result.TotalVisitNumber);
    }

    /// <summary>AC-C-4 — a zero divisor never reaches the arithmetic: the write path refuses it.</summary>
    [Fact]
    public async Task T04_Zero_Visit_Minutes_Is_Refused_At_Write_Time()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        var response = await h.Create.Handle(
            CreateCommand(periodId, promo: 0, nonPromo: 0), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains(CycleCapacityReasonCodes.VisitMinutesZero, response.Errors!);
        Assert.Empty(h.Repo.Items);
    }

    /// <summary>AC-C-5 — the first and last months are queried over the CLIPPED range, not the whole month.</summary>
    [Fact]
    public async Task T05_Partial_Months_Are_Counted_Over_The_Clipped_Range()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(
            periodId,
            start: new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero),
            end: new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero)));

        var created = await h.Create.Handle(
            CreateCommand(periodId, months: new[]
            {
                new CycleCapacityMonthInput(2026, 3, 0, 0, 0, 0, 0),
                new CycleCapacityMonthInput(2026, 4, 0, 0, 0, 0, 0),
                new CycleCapacityMonthInput(2026, 5, 0, 0, 0, 0, 0)
            }),
            CancellationToken.None);
        Assert.True(created.IsSuccessful);

        await h.Calculation.Handle(new GetCycleCapacityCalculationQuery(created.Data), CancellationToken.None);

        Assert.Equal(3, h.Calendar.Calls.Count);
        Assert.Equal((new DateOnly(2026, 3, 15), new DateOnly(2026, 3, 31)), (h.Calendar.Calls[0].From, h.Calendar.Calls[0].To));
        Assert.Equal((new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30)), (h.Calendar.Calls[1].From, h.Calendar.Calls[1].To));
        Assert.Equal((new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 10)), (h.Calendar.Calls[2].From, h.Calendar.Calls[2].To));
    }

    /// <summary>
    /// AC-C-6 — a period crossing new year's eve produces rows in TWO years. This is the case a positional
    /// twelve-element array cannot express, which is why the month model is explicit.
    /// </summary>
    [Fact]
    public void T06_Year_Crossing_Period_Produces_Rows_In_Two_Years()
    {
        var windows = CycleCapacityMonthRules.Derive(
            new DateTimeOffset(2026, 12, 10, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2027, 1, 20, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(2, windows.Count);
        Assert.Equal((2026, 12), (windows[0].Year, windows[0].MonthNumber));
        Assert.Equal((2027, 1), (windows[1].Year, windows[1].MonthNumber));
        Assert.Equal(new DateOnly(2026, 12, 10), windows[0].FromDate());
        Assert.Equal(new DateOnly(2027, 1, 20), windows[1].ToDate());
    }

    /// <summary>AC-C-7 — no computed value is ever written onto the aggregate.</summary>
    [Fact]
    public async Task T07_Computed_Values_Are_Never_Persisted()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        var created = await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);
        await h.Calculation.Handle(new GetCycleCapacityCalculationQuery(created.Data), CancellationToken.None);

        var stored = Assert.Single(h.Repo.Items);

        // A property-level proof rather than a hopeful comment: the aggregate declares no visit/working-day member at
        // all, so nothing could persist one even by accident.
        var forbidden = typeof(CapacityEntity).GetProperties()
            .Concat(typeof(CycleCapacityMonth).GetProperties())
            .Select(p => p.Name)
            .Where(n => n.Contains("Visit", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("WorkingDay", StringComparison.OrdinalIgnoreCase)
                        || n.Contains("FieldDay", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Empty(forbidden);

        // FU07 — the FTE lives on the MONTH now. A root one would mean two multipliers and a silent question about
        // which of them a figure was built on.
        Assert.DoesNotContain("Fte", typeof(CapacityEntity).GetProperties().Select(x => x.Name));
        Assert.Contains("Fte", typeof(CycleCapacityMonth).GetProperties().Select(x => x.Name));
        Assert.Equal(0, stored.Version);
    }

    /// <summary>The month rounding is AwayFromZero and happens ONCE, at the end.</summary>
    [Fact]
    public void T08_Rounding_Is_AwayFromZero_And_Applied_Once()
    {
        var capacity = new CapacityEntity
        {
            DailyWorkMinutes = 100, PromoProductTime = 4, NonPromoProductTime = 0,
            TravelingTime = 0, ReportDuration = 0, QuizDuration = 0,
            Months = { new CycleCapacityMonth { Year = 2026, MonthNumber = 3, Fte = 0.5m } }
        };
        var window = new CycleCapacityMonthRules.MonthWindow(2026, 3, Mar1, Apr30);

        // 1 field day → 100 minutes → 25 visits → × 0.5 = 12.5 → 13 away from zero.
        var result = CycleCapacityCalculator.Calculate(
            capacity, new[] { new CycleCapacityCalculator.ResolvedMonth(window, 1) });

        Assert.Equal(13, Assert.Single(result.Months).TotalVisitNumber);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // AC-W — the fail-closed working calendar
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>AC-W-5 — ONE unresolved month makes the WHOLE estimate unresolved, with no partial table.</summary>
    [Fact]
    public async Task T09_One_Unresolved_Month_Unresolves_The_Whole_Estimate()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        var created = await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);
        h.Calendar.ByMonth[(2026, 4)] = (CycleCapacityResolutions.CalendarUnresolved, null);

        var response = await h.Calculation.Handle(
            new GetCycleCapacityCalculationQuery(created.Data), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(503, response.StatusCode);
        Assert.Equal(CycleCapacityResolutions.CalendarUnresolved, response.Data!.Resolution);
        Assert.Null(response.Data.TotalVisitNumber);   // null, NOT zero
        Assert.Empty(response.Data.Months);            // no partial table
    }

    /// <summary>AC-W-9 — a 403 is reported as its own resolution, never flattened into "no calendar".</summary>
    [Fact]
    public async Task T10_Calendar_Forbidden_Is_Distinct_From_Calendar_Unresolved()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        var created = await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);
        h.Calendar.Resolution = CycleCapacityResolutions.CalendarForbidden;

        var response = await h.Calculation.Handle(
            new GetCycleCapacityCalculationQuery(created.Data), CancellationToken.None);

        Assert.Equal(CycleCapacityResolutions.CalendarForbidden, response.Data!.Resolution);
        Assert.NotEqual(CycleCapacityResolutions.CalendarUnresolved, response.Data.Resolution);
        Assert.Null(response.Data.TotalVisitNumber);
    }

    /// <summary>AC-W-8 — an unreachable calendar does NOT block authoring. The inputs are valid on their own.</summary>
    [Fact]
    public async Task T11_Unreachable_Calendar_Does_Not_Block_The_Write_Path()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));
        h.Calendar.Resolution = CycleCapacityResolutions.CalendarUnresolved;

        var created = await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal(201, created.StatusCode);
        Assert.Single(h.Repo.Items);
        Assert.Empty(h.Calendar.Calls); // the write path never asks the calendar at all
    }

    /// <summary>No default month length is ever invented: an unresolved answer carries a null count, not a guess.</summary>
    [Fact]
    public async Task T12_No_Default_Working_Day_Count_Is_Invented()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        var created = await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);
        h.Calendar.Resolution = CycleCapacityResolutions.CalendarUnresolved;

        var response = await h.Calculation.Handle(
            new GetCycleCapacityCalculationQuery(created.Data), CancellationToken.None);

        Assert.Null(response.Data!.TotalVisitNumber);
        Assert.DoesNotContain(response.Data.Months, m => m.WorkingDays > 0);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // AC-B — the pin, the 1:1 rule and tenant isolation
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>AC-B-2 — one period carries at most one non-archived capacity.</summary>
    [Fact]
    public async Task T13_Second_Capacity_For_The_Same_Period_Is_Refused()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        Assert.True((await h.Create.Handle(CreateCommand(periodId), CancellationToken.None)).IsSuccessful);
        var second = await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);

        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
        Assert.Contains(CycleCapacityReasonCodes.DuplicateCapacity, second.Errors!);
        Assert.Single(h.Repo.Items);
    }

    /// <summary>Archiving FREES the period — the deliberate, narrow way to redo a capacity.</summary>
    [Fact]
    public async Task T14_Archiving_Frees_The_Period_For_A_New_Capacity()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        var first = await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);
        Assert.True((await h.Archive.Handle(
            new ArchiveCycleCapacityCommand(first.Data, null), CancellationToken.None)).IsSuccessful);

        var second = await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);

        Assert.True(second.IsSuccessful);
        Assert.Equal(2, h.Repo.Items.Count);            // nothing was deleted
        Assert.True(h.Repo.Items[0].IsArchived);
    }

    /// <summary>AC-B-3 — an unknown period answers 404, and nothing is written.</summary>
    [Fact]
    public async Task T15_Unknown_Cycle_Period_Answers_404_And_Writes_Nothing()
    {
        var h = Build(TenantA);

        var response = await h.Create.Handle(CreateCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(404, response.StatusCode);
        Assert.Contains(CycleCapacityReasonCodes.PeriodNotFound, response.Errors!);
        Assert.Empty(h.Repo.Items);
    }

    /// <summary>Another tenant's capacity answers 404 rather than 403 — the endpoint never confirms it exists.</summary>
    [Fact]
    public async Task T16_Cross_Tenant_Read_Answers_404()
    {
        var a = Build(TenantA);
        var periodId = Guid.NewGuid();
        a.Periods.Periods.Add(Period(periodId));
        var created = await a.Create.Handle(CreateCommand(periodId), CancellationToken.None);

        var b = Build(TenantB);
        b.Repo.Items.AddRange(a.Repo.Items);            // same store, different caller

        var response = await b.GetById.Handle(
            new GetCycleCapacityByIdQuery(created.Data), CancellationToken.None);

        Assert.Equal(404, response.StatusCode);
    }

    /// <summary>The pin cannot be moved: the update command carries no CyclePeriodId to move it WITH.</summary>
    [Fact]
    public void T17_Update_Command_Cannot_Express_A_Pin_Change()
    {
        var names = typeof(UpdateCycleCapacityCommand).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("CyclePeriodId", names);
        Assert.DoesNotContain("Fte", names);            // the FTE is server-stamped for the same reason
        Assert.DoesNotContain("TenantId", names);
        Assert.DoesNotContain("Status", names);         // this aggregate has no lifecycle of its own
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // AC-L — the closed-period lock and concurrency
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>AC-L-1 — a closed period freezes its capacity, while reads keep working.</summary>
    [Fact]
    public async Task T18_Closed_Period_Freezes_Writes_But_Not_Reads()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        var created = await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);

        // The period closes.
        h.Periods.Periods.Clear();
        h.Periods.Periods.Add(Period(periodId, status: CyclePeriodStatuses.Closed));

        var update = await h.Update.Handle(
            new UpdateCycleCapacityCommand(created.Data, "TR", 480, 15, 10, 60, 30, 10, null, TwoMonths(), null),
            CancellationToken.None);
        Assert.Equal(409, update.StatusCode);
        Assert.Contains(CycleCapacityReasonCodes.PeriodClosed, update.Errors!);

        var archive = await h.Archive.Handle(
            new ArchiveCycleCapacityCommand(created.Data, null), CancellationToken.None);
        Assert.Equal(409, archive.StatusCode);

        var read = await h.GetById.Handle(new GetCycleCapacityByIdQuery(created.Data), CancellationToken.None);
        Assert.True(read.IsSuccessful);
        Assert.False(read.Data!.IsEditable);            // derived, not stored
    }

    /// <summary>AC-L-2 — the aggregate has no status of its own; editability is DERIVED.</summary>
    [Fact]
    public void T19_Capacity_Has_No_Lifecycle_Of_Its_Own()
    {
        var names = typeof(CapacityEntity).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("Status", names);
        Assert.DoesNotContain("CapacityStatus", names);
        Assert.DoesNotContain("ApprovedAt", names);
        Assert.Contains("IsArchived", names);           // archive is a flag, not a state machine
    }

    /// <summary>AC-L-4 — a stale version answers 409 and overwrites nothing.</summary>
    [Fact]
    public async Task T20_Stale_Version_Answers_409_Without_Overwriting()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));
        var created = await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);

        // A first edit moves the version to 1.
        Assert.True((await h.Update.Handle(
            new UpdateCycleCapacityCommand(created.Data, "TR", 480, 15, 10, 60, 30, 10, "first", TwoMonths(), 0),
            CancellationToken.None)).IsSuccessful);

        var stale = await h.Update.Handle(
            new UpdateCycleCapacityCommand(created.Data, "TR", 480, 15, 10, 60, 30, 10, "second", TwoMonths(), 0),
            CancellationToken.None);

        Assert.Equal(409, stale.StatusCode);
        Assert.Contains(CycleCapacityReasonCodes.ConcurrencyConflict, stale.Errors!);
        Assert.Equal("first", h.Repo.Items[0].Description);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // D-COUNTRY = B — the calendar country is a parameter, not a scope
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A country-scoped period DERIVES the code, and the caller's own value is ignored.</summary>
    [Fact]
    public async Task T21_Country_Scoped_Period_Derives_The_Calendar_Country()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId, scopeType: CyclePeriodScopeTypes.Country, country: "DE"));

        var created = await h.Create.Handle(CreateCommand(periodId, country: "TR"), CancellationToken.None);

        Assert.True(created.IsSuccessful);
        Assert.Equal("DE", h.Repo.Items[0].CalendarCountryCode);
        Assert.Equal(0, h.References.Calls);   // a derived code needs no second vocabulary check
    }

    /// <summary>A tenant-scoped period cannot derive one, so an authored code is REQUIRED — the case strict derivation
    /// would have made unusable.</summary>
    [Fact]
    public async Task T22_Tenant_Scoped_Period_Requires_An_Authored_Country()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId, scopeType: CyclePeriodScopeTypes.Tenant));

        var missing = await h.Create.Handle(CreateCommand(periodId, country: null), CancellationToken.None);
        Assert.Equal(400, missing.StatusCode);
        Assert.Contains(CycleCapacityReasonCodes.CountryRequired, missing.Errors!);

        var authored = await h.Create.Handle(CreateCommand(periodId, country: "tr"), CancellationToken.None);
        Assert.True(authored.IsSuccessful);
        Assert.Equal("TR", h.Repo.Items[0].CalendarCountryCode);   // normalised
    }

    /// <summary>An authored code is validated against the SAME governed set CyclePeriod uses — one vocabulary, not two.</summary>
    [Fact]
    public async Task T23_Authored_Country_Is_Validated_Against_The_CyclePeriod_Reference_Set()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));
        h.References.Status = ReferenceValidationStatus.InvalidValue;

        var response = await h.Create.Handle(CreateCommand(periodId, country: "ZZ"), CancellationToken.None);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(CycleCapacityReasonCodes.CountryUnknown, response.Errors!);
        Assert.Equal(CyclePeriodReferenceSets.CountrySet, Assert.Single(h.References.AskedSets));
        Assert.Empty(h.Repo.Items);
    }

    /// <summary>An unpublished SET is a different failure from an unknown VALUE: one is fixed by an operator, the other
    /// by retyping.</summary>
    [Fact]
    public async Task T24_Unpublished_Reference_Set_Is_Reported_Separately()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));
        h.References.Status = ReferenceValidationStatus.SetMissing;

        var response = await h.Create.Handle(CreateCommand(periodId, country: "TR"), CancellationToken.None);

        Assert.Contains(CycleCapacityReasonCodes.ReferenceSetUnpublished, response.Errors!);
        Assert.DoesNotContain(CycleCapacityReasonCodes.CountryUnknown, response.Errors!);
    }

    /// <summary>A legal-entity-scoped period passes its id through to the calendar — free precision, no extra call.</summary>
    [Fact]
    public async Task T25_Legal_Entity_Scope_Narrows_The_Calendar()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(
            periodId, scopeType: CyclePeriodScopeTypes.LegalEntity, legalEntityId: LegalEntityX));

        var created = await h.Create.Handle(CreateCommand(periodId, country: "TR"), CancellationToken.None);
        await h.Calculation.Handle(new GetCycleCapacityCalculationQuery(created.Data), CancellationToken.None);

        Assert.All(h.Calendar.Calls, call => Assert.Equal(LegalEntityX, call.LegalEntityId));
    }

    /// <summary>
    /// A business unit is NEVER mapped onto the calendar's organization unit: one is a MOD-0048 value code, the other
    /// an organization-unit GUID, and coercing them would silently select the wrong calendar (F-WC-ORG-UNIT).
    /// </summary>
    [Fact]
    public async Task T26_Business_Unit_Never_Narrows_The_Calendar()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(
            periodId, scopeType: CyclePeriodScopeTypes.BusinessUnit, businessUnitId: "alpha"));

        var created = await h.Create.Handle(CreateCommand(periodId, country: "TR"), CancellationToken.None);
        await h.Calculation.Handle(new GetCycleCapacityCalculationQuery(created.Data), CancellationToken.None);

        Assert.NotEmpty(h.Calendar.Calls);
        Assert.All(h.Calendar.Calls, call => Assert.Null(call.LegalEntityId));
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // D-FTE — interim, server-stamped, stored
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The FTE is written by the server from configuration and STORED, so the estimate stays reproducible.</summary>
    [Fact]
    public async Task T27_Fte_Is_Server_Stamped_And_Stored()
    {
        var h = Build(TenantA, fte: 7.50m);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);

        Assert.All(h.Repo.Items[0].Months, m => Assert.Equal(7.50m, m.Fte));
        Assert.All(h.Repo.Items[0].Months, m => Assert.Equal(CycleCapacityFteSources.InterimDefault, m.FteSource));
        Assert.DoesNotContain("Fte", typeof(CreateCycleCapacityCommand).GetProperties().Select(p => p.Name));
    }

    /// <summary>An edit never disturbs the stored FTE, so an old capacity keeps producing the same figure.</summary>
    [Fact]
    public async Task T28_Update_Preserves_The_Stored_Fte()
    {
        var h = Build(TenantA, fte: 7.50m);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));
        var created = await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);

        await h.Update.Handle(
            new UpdateCycleCapacityCommand(created.Data, "TR", 480, 20, 10, 30, 30, 10, "edited", TwoMonths(), null),
            CancellationToken.None);

        Assert.All(h.Repo.Items[0].Months, m => Assert.Equal(7.50m, m.Fte));
        Assert.Equal("edited", h.Repo.Items[0].Description);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // Month rows
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>A month outside the period's window is refused, naming the month.</summary>
    [Fact]
    public async Task T29_Month_Outside_The_Period_Window_Is_Refused()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));   // Mar–Apr 2026

        var response = await h.Create.Handle(
            CreateCommand(periodId, months: new[] { new CycleCapacityMonthInput(2026, 9, 0, 0, 0, 0, 0) }),
            CancellationToken.None);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(CycleCapacityReasonCodes.MonthOutOfPeriod, response.Errors!);
    }

    /// <summary>(Year, MonthNumber) is the identity, so the same month cannot appear twice.</summary>
    [Fact]
    public async Task T30_Duplicate_Month_Row_Is_Refused()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        var response = await h.Create.Handle(
            CreateCommand(periodId, months: new[]
            {
                new CycleCapacityMonthInput(2026, 3, 0, 0, 0, 0, 0),
                new CycleCapacityMonthInput(2026, 3, 1, 0, 0, 0, 0)
            }),
            CancellationToken.None);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(CycleCapacityReasonCodes.MonthDuplicate, response.Errors!);
    }

    /// <summary>
    /// Deductions larger than the month's working days are NOT a validation error — the working-day count is unknown at
    /// write time, so judging it there would require guessing. The calculator clamps instead.
    /// </summary>
    [Fact]
    public async Task T31_Over_Deducted_Month_Is_Accepted_And_Clamped_At_Read_Time()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        var created = await h.Create.Handle(
            CreateCommand(periodId, months: new[]
            {
                new CycleCapacityMonthInput(2026, 3, 10, 10, 10, 0, 0),
                new CycleCapacityMonthInput(2026, 4, 0, 0, 0, 0, 0)
            }),
            CancellationToken.None);

        Assert.True(created.IsSuccessful);

        var response = await h.Calculation.Handle(
            new GetCycleCapacityCalculationQuery(created.Data), CancellationToken.None);

        var march = response.Data!.Months.Single(m => m.MonthNumber == 3);
        Assert.Equal(0, march.FieldDays);
        Assert.Equal(0, march.TotalVisitNumber);
    }

    /// <summary>A day already consumed by its fixed charges leaves no room for a visit — a modelling error, refused.</summary>
    [Fact]
    public async Task T32_Daily_Spend_Consuming_The_Whole_Day_Is_Refused()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        var response = await h.Create.Handle(
            CreateCommand(periodId, dailyWorkMinutes: 480, traveling: 300, report: 120, quiz: 60),
            CancellationToken.None);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(CycleCapacityReasonCodes.DailySpendExceedsDay, response.Errors!);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // AC-V — the contract, and CyclePeriod staying untouched
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC-V-1 — the CyclePeriod contract is EXACTLY where FU07 left it. In particular
    /// <c>SupportsWorkingCalendarIntegration</c> and <c>SupportsWorkingDayCount</c> stay <c>false</c>: the integration
    /// belongs to CycleCapacity, not to the period master.
    /// </summary>
    [Fact]
    public void T33_CyclePeriod_Contract_Flags_Are_Unchanged()
    {
        var flags = CyclePeriodFeatureFlags.Current;

        Assert.False(flags.SupportsWorkingCalendarIntegration);
        Assert.False(flags.SupportsWorkingDayCount);
        Assert.False(flags.SupportsMicroTargetGeneration);
        Assert.False(flags.SupportsCampaignBinding);
        Assert.False(flags.SupportsCycleAutoClose);
        Assert.False(flags.SupportsCyclePeriodVersioning);
        Assert.True(flags.SupportsCyclePeriod);

        // Guarded by NAME, not by count: a raw property count also picks up compiler-generated record members, and it
        // would not say WHICH flag appeared. This set is FU07's, verbatim — adding one has to be a declared act.
        Assert.Equal(
            new[]
            {
                "SupportsActiveCycleResolution", "SupportsBulkDelete", "SupportsBusinessUnitScopedCycles",
                "SupportsCampaignBinding", "SupportsCountryScopedCycles", "SupportsCrossScopeOverlapBan",
                "SupportsCycleAutoClose", "SupportsCycleCalendarHierarchy", "SupportsCycleOverlap",
                "SupportsCyclePeriod", "SupportsCyclePeriodLifecycle", "SupportsCyclePeriodVersioning",
                "SupportsCycleReschedule", "SupportsFrequencyPolicyBackReference", "SupportsFrequencyPolicyWrite",
                "SupportsHardDelete", "SupportsLegalEntityScopedCycles", "SupportsMicroTargetGeneration",
                "SupportsOrganizationUnitScopedCycles", "SupportsScopeInheritance", "SupportsScopeMerge",
                "SupportsScopePrecedenceResolution", "SupportsScopeTypeMutation", "SupportsStrategyApply",
                "SupportsTerritorySourcedBusinessUnits", "SupportsWorkingCalendarIntegration",
                "SupportsWorkingDayCount"
            },
            typeof(CyclePeriodFeatureFlags).GetProperties()
                .Where(p => p.PropertyType == typeof(bool))
                .Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray());
    }

    /// <summary>AC-V-3 — every capability this FU does NOT open is denied out loud, and the estimate flag is true.</summary>
    [Fact]
    public void T34_Forbidden_Capacity_Flags_Are_Absent()
    {
        var flags = CycleCapacityFeatureFlags.Current;

        Assert.True(flags.SupportsCycleCapacity);
        Assert.True(flags.IsEstimate);
        Assert.True(flags.SupportsFailClosedCalendar);
        Assert.True(flags.SupportsCalendarCountryParameter);

        Assert.False(flags.SupportsComputedValuePersistence);
        Assert.False(flags.SupportsMultipleCapacitiesPerPeriod);
        Assert.False(flags.SupportsCyclePeriodMutation);
        Assert.False(flags.SupportsWorkingCalendarWrite);
        Assert.False(flags.SupportsOrganizationUnitCalendarNarrowing);
        Assert.False(flags.SupportsBusinessUnitCalendarNarrowing);
        Assert.False(flags.SupportsMicroTargetGeneration);
        Assert.False(flags.SupportsVisitDistribution);
        Assert.False(flags.SupportsRoutePlanning);
        Assert.False(flags.SupportsFrequencyPolicyWrite);
        Assert.False(flags.SupportsCampaignBinding);
        Assert.False(flags.SupportsHrFteIntegration);
        Assert.False(flags.SupportsPerBusinessUnitFte);
        Assert.False(flags.SupportsCapacityApproval);
        Assert.False(flags.SupportsCapacityLifecycle);
        Assert.False(flags.SupportsScenarioComparison);
        Assert.False(flags.SupportsActualsComparison);
        Assert.False(flags.SupportsHardDelete);
        Assert.False(flags.SupportsBulkDelete);
    }

    /// <summary>There is no delete anywhere: the repository seam does not expose one.</summary>
    [Fact]
    public void T35_Repository_Exposes_No_Delete()
    {
        var methods = typeof(ICycleCapacityRepository).GetMethods().Select(m => m.Name).ToList();

        Assert.DoesNotContain(methods, m => m.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(methods, m => m.Contains("Remove", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Only the two documented keys exist; there is deliberately no <c>.calculate</c>.</summary>
    [Fact]
    public void T36_Permission_Keys_Are_Exactly_Read_And_Manage()
    {
        Assert.Equal(
            new[] { "crm.cycle-capacity.read", "crm.cycle-capacity.manage" },
            CycleCapacityPermissions.All);
        Assert.DoesNotContain(CycleCapacityPermissions.All, k => k.EndsWith(".calculate", StringComparison.Ordinal));
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // Projection and listing
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The list projects the period fresh and never copies it: a renamed period shows its new name at once.</summary>
    [Fact]
    public async Task T37_List_Projects_The_Period_Rather_Than_Copying_It()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));
        await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);

        h.Periods.Periods.Clear();
        h.Periods.Periods.Add(new CyclePeriodSnapshot(
            periodId, "c-2026-03", "RENAMED", 2026, 3, Mar1, Apr30,
            CyclePeriodStatuses.Active, CyclePeriodScopeTypes.Tenant, null, null, null, null));

        var response = await h.List.Handle(
            new GetCycleCapacityListQuery(null, null, false, null), CancellationToken.None);

        Assert.Equal("RENAMED", Assert.Single(response.Data!.Items).CycleName);
    }

    /// <summary>A capacity whose period cannot be read stays visible — its own inputs are still the tenant's — but is
    /// reported as NOT editable, which is the fail-closed direction.</summary>
    [Fact]
    public async Task T38_Unreadable_Period_Leaves_The_Row_Visible_But_Not_Editable()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));
        await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);

        h.Periods.Periods.Clear();

        var response = await h.List.Handle(
            new GetCycleCapacityListQuery(null, null, false, null), CancellationToken.None);

        var row = Assert.Single(response.Data!.Items);
        Assert.Null(row.CycleCode);
        Assert.False(row.IsEditable);
    }

    /// <summary>Archived rows are hidden unless asked for — a view choice, not a storage one.</summary>
    [Fact]
    public async Task T39_Archived_Rows_Are_Hidden_Unless_Requested()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));
        var created = await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);
        await h.Archive.Handle(new ArchiveCycleCapacityCommand(created.Data, null), CancellationToken.None);

        var hidden = await h.List.Handle(
            new GetCycleCapacityListQuery(null, null, false, null), CancellationToken.None);
        var shown = await h.List.Handle(
            new GetCycleCapacityListQuery(null, null, true, null), CancellationToken.None);

        Assert.Empty(hidden.Data!.Items);
        Assert.Single(shown.Data!.Items);
    }

    /// <summary>Archive is idempotent: a retried request is not an error.</summary>
    [Fact]
    public async Task T40_Archive_Is_Idempotent()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));
        var created = await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);

        Assert.True((await h.Archive.Handle(new ArchiveCycleCapacityCommand(created.Data, null), CancellationToken.None)).IsSuccessful);
        Assert.True((await h.Archive.Handle(new ArchiveCycleCapacityCommand(created.Data, null), CancellationToken.None)).IsSuccessful);
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // The LIVE preview — same rule, same number, and nothing persisted
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private static PreviewCycleCapacityCalculationQuery PreviewQuery(
        Guid periodId,
        string? country = "TR",
        int dailyWorkMinutes = 480,
        int promo = 15,
        int nonPromo = 10,
        int traveling = 60,
        int report = 30,
        int quiz = 10,
        IReadOnlyList<CycleCapacityMonthInput>? months = null)
        => new(periodId, country, dailyWorkMinutes, promo, nonPromo, traveling, report, quiz,
            months ?? TwoMonths());

    /// <summary>
    /// The whole reason the preview exists in the runtime rather than in JavaScript: for the SAME inputs it must
    /// produce the SAME figure the saved record reports. A number computed in the browser would eventually drift from
    /// this one, and the author would trust the wrong one.
    /// </summary>
    [Fact]
    public async Task T42_Preview_Matches_The_Saved_Calculation_Exactly()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        var created = await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);
        var saved = await h.Calculation.Handle(
            new GetCycleCapacityCalculationQuery(created.Data), CancellationToken.None);

        var preview = await h.Preview.Handle(PreviewQuery(periodId), CancellationToken.None);

        Assert.True(preview.IsSuccessful);
        Assert.Equal(saved.Data!.TotalVisitNumber, preview.Data!.TotalVisitNumber);
        Assert.Equal(saved.Data.MinutesPerVisit, preview.Data.MinutesPerVisit);
        Assert.Equal(
            saved.Data.Months.Select(m => (m.Year, m.MonthNumber, m.FieldDays, m.Fte, m.TotalVisitNumber)),
            preview.Data.Months.Select(m => (m.Year, m.MonthNumber, m.FieldDays, m.Fte, m.TotalVisitNumber)));
    }

    /// <summary>The preview is TRANSIENT: it writes nothing, and the handler has no repository to write with.</summary>
    [Fact]
    public async Task T43_Preview_Persists_Nothing()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        for (var i = 0; i < 3; i++)
        {
            var response = await h.Preview.Handle(PreviewQuery(periodId), CancellationToken.None);
            Assert.True(response.IsSuccessful);
        }

        Assert.Empty(h.Repo.Items);

        // Structural, not hopeful: the handler cannot reach a store even by mistake.
        var dependencies = typeof(PreviewCycleCapacityCalculationHandler)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType)
            .ToList();
        Assert.DoesNotContain(typeof(ICycleCapacityRepository), dependencies);
    }

    /// <summary>The preview answer carries no id: there is nothing to mistake for a saved record.</summary>
    [Fact]
    public async Task T44_Preview_Answer_Carries_No_Capacity_Id()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        var response = await h.Preview.Handle(PreviewQuery(periodId), CancellationToken.None);

        Assert.Equal(Guid.Empty, response.Data!.CycleCapacityId);
        Assert.Equal(periodId, response.Data.CyclePeriodId);
    }

    /// <summary>The FTE is server-stamped on the preview too, so it cannot be built on a different number from the
    /// save. The query has no FTE to send.</summary>
    [Fact]
    public async Task T45_Preview_Uses_The_Configured_Fte_And_Cannot_Be_Told_Otherwise()
    {
        var h = Build(TenantA, fte: 3.25m);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        var response = await h.Preview.Handle(PreviewQuery(periodId), CancellationToken.None);

        Assert.All(response.Data!.Months, m => Assert.Equal(3.25m, m.Fte));
        Assert.DoesNotContain(
            "Fte", typeof(PreviewCycleCapacityCalculationQuery).GetProperties().Select(p => p.Name));
    }

    /// <summary>Fail-closed applies to the preview identically: one unresolved month, no partial table, null total.</summary>
    [Fact]
    public async Task T46_Preview_Is_Fail_Closed_Like_The_Saved_Calculation()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));
        h.Calendar.ByMonth[(2026, 4)] = (CycleCapacityResolutions.CalendarUnresolved, null);

        var response = await h.Preview.Handle(PreviewQuery(periodId), CancellationToken.None);

        Assert.False(response.IsSuccessful);
        Assert.Equal(503, response.StatusCode);
        Assert.Null(response.Data!.TotalVisitNumber);
        Assert.Empty(response.Data.Months);
    }

    /// <summary>A 403 stays distinguishable on the preview surface as well (F-RBAC-WC).</summary>
    [Fact]
    public async Task T47_Preview_Reports_Calendar_Forbidden_Distinctly()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));
        h.Calendar.Resolution = CycleCapacityResolutions.CalendarForbidden;

        var response = await h.Preview.Handle(PreviewQuery(periodId), CancellationToken.None);

        Assert.Equal(CycleCapacityResolutions.CalendarForbidden, response.Data!.Resolution);
        Assert.Null(response.Data.TotalVisitNumber);
    }

    /// <summary>A period the caller's tenant does not own answers 404 — a preview is not a way around the pin.</summary>
    [Fact]
    public async Task T48_Preview_Refuses_An_Unknown_Cycle_Period()
    {
        var h = Build(TenantA);

        var response = await h.Preview.Handle(PreviewQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(404, response.StatusCode);
        Assert.Contains(CycleCapacityReasonCodes.PeriodNotFound, response.Errors!);
        Assert.Empty(h.Calendar.Calls);   // no period, no window, no calendar traffic
    }

    /// <summary>A country-scoped period derives the calendar country on the preview path too, ignoring the payload.</summary>
    [Fact]
    public async Task T49_Preview_Derives_The_Calendar_Country_From_A_Country_Scoped_Period()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId, scopeType: CyclePeriodScopeTypes.Country, country: "DE"));

        var response = await h.Preview.Handle(PreviewQuery(periodId, country: "TR"), CancellationToken.None);

        Assert.True(response.IsSuccessful);
        Assert.Equal("DE", response.Data!.CalendarCountryCode);
        Assert.All(h.Calendar.Calls, call => Assert.Equal("DE", call.Country));
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // FU07 — per-month FTE, the derived non-working-day column, and the read-time migration
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>AC-F-2 — two months with DIFFERENT FTEs produce different figures. This is the whole reason the FTE
    /// moved: one number for a cycle cannot say that August is thinner than March.</summary>
    [Fact]
    public void T50_Per_Month_Fte_Actually_Varies_The_Result()
    {
        var capacity = new CapacityEntity
        {
            DailyWorkMinutes = 480, PromoProductTime = 15, NonPromoProductTime = 10,
            TravelingTime = 0, ReportDuration = 0, QuizDuration = 0,
            Months =
            {
                new CycleCapacityMonth { Year = 2026, MonthNumber = 3, Fte = 1m },
                new CycleCapacityMonth { Year = 2026, MonthNumber = 4, Fte = 2m }
            }
        };

        var march = new CycleCapacityMonthRules.MonthWindow(
            2026, 3, Mar1, new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero));
        var april = new CycleCapacityMonthRules.MonthWindow(
            2026, 4, new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), Apr30);

        var result = CycleCapacityCalculator.Calculate(capacity, new[]
        {
            new CycleCapacityCalculator.ResolvedMonth(march, 20),
            new CycleCapacityCalculator.ResolvedMonth(april, 20)
        });

        var m3 = result.Months.Single(m => m.MonthNumber == 3);
        var m4 = result.Months.Single(m => m.MonthNumber == 4);

        Assert.Equal(1m, m3.Fte);
        Assert.Equal(2m, m4.Fte);
        Assert.Equal(m3.TotalVisitNumber * 2, m4.TotalVisitNumber);
    }

    /// <summary>AC-N-1 — the non-working column is a DERIVATION of the clipped range, not a second measurement.</summary>
    [Fact]
    public void T51_NonWorkingDays_Is_Derived_From_The_Clipped_Range()
    {
        var capacity = new CapacityEntity
        {
            DailyWorkMinutes = 480, PromoProductTime = 15, NonPromoProductTime = 10,
            Months = { new CycleCapacityMonth { Year = 2026, MonthNumber = 3, Fte = 1m } }
        };

        // 15–31 March inclusive = 17 calendar days; the calendar says 12 of them are working days.
        var window = new CycleCapacityMonthRules.MonthWindow(
            2026, 3,
            new DateTimeOffset(2026, 3, 15, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero));

        var month = Assert.Single(CycleCapacityCalculator
            .Calculate(capacity, new[] { new CycleCapacityCalculator.ResolvedMonth(window, 12) }).Months);

        Assert.Equal(17, month.CalendarDays);   // the CLIPPED range, not the whole month
        Assert.Equal(12, month.WorkingDays);
        Assert.Equal(5, month.NonWorkingDays);
    }

    /// <summary>AC-N-2 — an unresolved month produces no non-working figure either: fail-closed does not split.</summary>
    [Fact]
    public async Task T52_NonWorkingDays_Is_Absent_When_The_Calendar_Cannot_Answer()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));
        var created = await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);
        h.Calendar.Resolution = CycleCapacityResolutions.CalendarUnresolved;

        var response = await h.Calculation.Handle(
            new GetCycleCapacityCalculationQuery(created.Data), CancellationToken.None);

        Assert.Empty(response.Data!.Months);
        Assert.Null(response.Data.TotalVisitNumber);
    }

    /// <summary>AC-N-4 — the column costs NOTHING: still one working-calendar call per month.</summary>
    [Fact]
    public async Task T53_NonWorkingDays_Adds_No_Working_Calendar_Calls()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));   // Mar–Apr: two months
        var created = await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);

        await h.Calculation.Handle(new GetCycleCapacityCalculationQuery(created.Data), CancellationToken.None);

        // Two months, two calls — the derived column asked the calendar nothing extra.
        Assert.Equal(2, h.Calendar.Calls.Count);
        Assert.Equal(
            new[] { (2026, 3), (2026, 4) },
            h.Calendar.Calls.Select(c => (c.From.Year, c.From.Month)).ToArray());
    }

    /// <summary>
    /// AC-F-5 — the read-time migration. A row written by FU06 carries ONE root FTE and no per-month value; reading it
    /// copies that value onto every month, so the capacity keeps producing <b>exactly the figure it produced before</b>
    /// rather than silently adopting today's configured average.
    /// </summary>
    [Fact]
    public void T54_Legacy_Root_Fte_Is_Copied_To_Every_Month_On_Read()
    {
        // Precisely what Mongo hands back for a document written before the field moved: the root value lands in extra
        // elements, and the months have no FTE of their own.
        var legacy = new CapacityEntity
        {
            DailyWorkMinutes = 480, PromoProductTime = 15, NonPromoProductTime = 10,
            LegacyElements = new Dictionary<string, object?> { ["Fte"] = 12.00m },
            Months =
            {
                new CycleCapacityMonth { Year = 2026, MonthNumber = 3 },
                new CycleCapacityMonth { Year = 2026, MonthNumber = 4 }
            }
        };

        legacy.EnsureMonthlyFte(configuredDefault: 1.00m);

        Assert.All(legacy.Months, m => Assert.Equal(12.00m, m.Fte));
        Assert.All(legacy.Months, m => Assert.Equal(CycleCapacityFteSources.InterimDefault, m.FteSource));
    }

    /// <summary>AC-F-5 — the migrated row produces the SAME number FU06 produced. The golden example, replayed.</summary>
    [Fact]
    public void T55_Migrated_Legacy_Row_Reproduces_The_Fu06_Figure()
    {
        var legacy = new CapacityEntity
        {
            DailyWorkMinutes = 480, PromoProductTime = 15, NonPromoProductTime = 10,
            TravelingTime = 60, ReportDuration = 30, QuizDuration = 10,
            LegacyElements = new Dictionary<string, object?> { ["Fte"] = 12.00m },
            Months =
            {
                new CycleCapacityMonth
                {
                    Year = 2026, MonthNumber = 3,
                    MeetingDays = 1, TrainingDays = 1, VacationDays = 2,
                    MicroTargetingDayCount = 3, MicroTargetingDuration = 45
                }
            }
        };

        // A configured default that is NOT the legacy value: if the migration were to use it, the assertion below
        // would fail — which is the point of choosing a different number here.
        legacy.EnsureMonthlyFte(configuredDefault: 1.00m);

        var window = new CycleCapacityMonthRules.MonthWindow(
            2026, 3, Mar1, new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero));
        var result = CycleCapacityCalculator.Calculate(
            legacy, new[] { new CycleCapacityCalculator.ResolvedMonth(window, 21) });

        Assert.Equal(3036, result.TotalVisitNumber);   // the FU06 golden figure, unchanged
    }

    /// <summary>A row that already has per-month FTEs is left alone — the normalisation only FILLS, never overwrites.</summary>
    [Fact]
    public void T56_EnsureMonthlyFte_Never_Overwrites_An_Existing_Value()
    {
        var capacity = new CapacityEntity
        {
            LegacyElements = new Dictionary<string, object?> { ["Fte"] = 99m },
            Months = { new CycleCapacityMonth { Year = 2026, MonthNumber = 3, Fte = 4m } }
        };

        capacity.EnsureMonthlyFte(configuredDefault: 1m);

        Assert.Equal(4m, capacity.Months[0].Fte);
    }

    /// <summary>With neither a per-month value nor a legacy root one, the configured default is used — the only case
    /// where today's average is the honest answer.</summary>
    [Fact]
    public void T57_EnsureMonthlyFte_Falls_Back_To_The_Configured_Default()
    {
        var capacity = new CapacityEntity
        {
            Months = { new CycleCapacityMonth { Year = 2026, MonthNumber = 3 } }
        };

        capacity.EnsureMonthlyFte(configuredDefault: 2.5m);

        Assert.Equal(2.5m, capacity.Months[0].Fte);
    }

    /// <summary>The cycle-wide FTE is GONE from the published calculation: there is no such number to publish.</summary>
    [Fact]
    public void T58_Calculation_Publishes_No_Cycle_Wide_Fte()
    {
        var names = typeof(CycleCapacityCalculator.CapacityCalculation).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain("Fte", names);
        Assert.Contains("Fte", typeof(CycleCapacityCalculator.MonthCalculation).GetProperties().Select(p => p.Name));
    }

    /// <summary>The month windows are derived over NORMALISED days: a period authored at a non-UTC offset must not slip
    /// into the previous month (the documented CRM DateTimeOffset trap).</summary>
    [Fact]
    public void T41_Month_Derivation_Reduces_To_Utc_Days()
    {
        // 1 March 00:00+03:00 is 28 February 21:00 UTC — the stored day is February.
        var windows = CycleCapacityMonthRules.Derive(
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.FromHours(3)),
            new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(2, windows.Count);
        Assert.Equal((2026, 2), (windows[0].Year, windows[0].MonthNumber));
        Assert.True(CycleCapacityMonthRules.Intersects(
            2026, 2,
            new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.FromHours(3)),
            new DateTimeOffset(2026, 3, 31, 0, 0, 0, TimeSpan.Zero)));
    }
}
