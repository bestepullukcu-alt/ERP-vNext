using Diten.CrmService.Application.Features.CycleCapacity;
using Diten.CrmService.Application.Features.CycleCapacity.Commands;
using Diten.CrmService.Application.Features.CycleCapacity.Queries;
using Diten.CrmService.Application.Features.CycleCapacity.Rules;
using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Domain.Entities;
using Xunit;
using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;

namespace Diten.CrmService.Application.Tests.CycleCapacity;

/// <summary>
/// WP-CAP-MODEL — the single visit-duration model (K-1), the authored FTE (K-5), the micro-targeting clip (E8) and the
/// calculation's waterfall / totals. Shares the FU06 harness (fakes and builders) of <see cref="CycleCapacityRuntimeTests"/>.
/// <para>The two sabotage targets of the pack: (1) charging the report per DAY as well on a typical row →
/// <see cref="CM01_Typical_Model_Report_Is_Per_Visit_Not_Per_Day"/> and <see cref="CM03_Typical_Month_Matches_The_Mockup_Example"/>
/// go red; (2) routing a legacy row through the new formula → <see cref="CM04_Legacy_Row_Keeps_Todays_Figures"/> and the
/// FU06 golden T01 go red.</para>
/// </summary>
public sealed partial class CycleCapacityRuntimeTests
{
    private static readonly DateTimeOffset Mar31 = new(2026, 3, 31, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The mockup's typical capacity: 2 promo × 12 + 1 non-promo × 5 + 5 report = 34 minutes; travel 90 + quiz 15.</summary>
    private static CapacityEntity TypicalCapacity(params CycleCapacityMonth[] months)
    {
        var capacity = new CapacityEntity
        {
            DailyWorkMinutes = 480,
            PromoProductTime = 12,
            NonPromoProductTime = 5,
            TravelingTime = 90,
            ReportDuration = 0,
            QuizDuration = 15,
            TypicalPromoCount = 2,
            TypicalNonPromoCount = 1,
            ReportMinutesPerVisit = 5
        };
        capacity.Months.AddRange(months);
        return capacity;
    }

    private static CycleCapacityCalculator.ResolvedMonth March(int workingDays)
        => new(new CycleCapacityMonthRules.MonthWindow(2026, 3, Mar1, Mar31), workingDays);

    private static CreateCycleCapacityCommand TypicalCreate(Guid periodId, IReadOnlyList<CycleCapacityMonthInput>? months = null)
        => CreateCommand(periodId, promo: 12, nonPromo: 5, traveling: 90, report: 30, quiz: 15, months: months) with
        {
            TypicalPromoCount = 2,
            TypicalNonPromoCount = 1,
            ReportMinutesPerVisit = 5
        };

    private static UpdateCycleCapacityCommand UpdateFrom(
        Guid capacityId,
        IReadOnlyList<CycleCapacityMonthInput>? months = null,
        int report = 30,
        int? typicalPromo = null,
        int? typicalNonPromo = null,
        int? reportPerVisit = null)
        => new(capacityId, "TR", 480, 12, 5, 90, report, 15, null, months ?? TwoMonths(), null,
            TypicalPromoCount: typicalPromo, TypicalNonPromoCount: typicalNonPromo, ReportMinutesPerVisit: reportPerVisit);

    // ── K-1 — the domain formula ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CM01_Typical_Model_Report_Is_Per_Visit_Not_Per_Day()
    {
        var capacity = TypicalCapacity();

        Assert.Equal(CycleCapacityVisitModels.Typical, capacity.VisitModel());
        Assert.Equal(34, capacity.TypicalVisitMinutes());      // 2 × 12 + 1 × 5 + 5
        Assert.Equal(105, capacity.DailyFixedMinutes());       // 90 + 15 — the report is NOT charged per day
        Assert.Equal(5, capacity.ReportMinutesForVisit());
        Assert.Equal(34, capacity.MinutesPerVisit());          // the published DTO name reads the same divisor
        Assert.Equal(105, capacity.DailySpendMinutes());
    }

    [Fact]
    public void CM02_VisitDuration_Uses_The_One_Formula_In_Both_Models()
    {
        var typical = TypicalCapacity();
        Assert.Equal(3 * 12 + 2 * 5 + 5, ActivityTimeBudgetCalculator.VisitDuration(typical, 3, 2));
        Assert.Equal(typical.VisitMinutes(3, 2), ActivityTimeBudgetCalculator.VisitDuration(typical, 3, 2));
        Assert.Equal(5, ActivityTimeBudgetCalculator.VisitDuration(typical, 0, 0));
        Assert.Equal(5, ActivityTimeBudgetCalculator.VisitDuration(typical, -4, -1)); // negative counts clamp to 0

        // Legacy row: today's SB-3b answer exactly — count × time + the (per-day) ReportDuration once.
        var legacy = new CapacityEntity { PromoProductTime = 15, NonPromoProductTime = 10, ReportDuration = 30 };
        Assert.Equal(2 * 15 + 1 * 10 + 30, ActivityTimeBudgetCalculator.VisitDuration(legacy, 2, 1));
        Assert.Equal(30, ActivityTimeBudgetCalculator.VisitDuration(legacy, 0, 0));
    }

    [Fact]
    public void CM03_Typical_Month_Matches_The_Mockup_Example()
    {
        var capacity = TypicalCapacity(new CycleCapacityMonth
        {
            Year = 2026, MonthNumber = 3, MeetingDays = 2, TrainingDays = 1, VacationDays = 1,
            MicroTargetingDayCount = 2, MicroTargetingDuration = 60, Fte = 1m
        });

        var result = CycleCapacityCalculator.Calculate(capacity, new[] { March(21) });
        var month = Assert.Single(result.Months);

        Assert.Equal(17, month.FieldDays);             // 21 − 4
        Assert.Equal(8160, month.AvailableMinutes);    // 480 × 17
        Assert.Equal(1785, month.DailyFixedMinutes);   // (90 + 15) × 17 — no per-day report
        Assert.Equal(120, month.MicroTargetingMinutes);
        Assert.Equal(6255, month.RemainingMinutes);    // 8160 − 1785 − 120
        Assert.Equal(month.VisitMinutes, month.RemainingMinutes);
        Assert.Equal(34, month.TypicalVisitMinutes);
        Assert.Equal(184, month.TotalVisitNumber);     // round(6255 ÷ 34 × 1) = round(183.97)

        Assert.Equal(CycleCapacityVisitModels.Typical, result.VisitModel);
        Assert.Equal(34, result.TypicalVisitMinutes);
        Assert.Equal(34, result.MinutesPerVisit);
        Assert.Equal(105, result.DailyFixedMinutes);
    }

    [Fact]
    public void CM04_Legacy_Row_Keeps_Todays_Figures()
    {
        // The FU06 golden inputs, no typical fields.
        var legacy = new CapacityEntity
        {
            DailyWorkMinutes = 480, PromoProductTime = 15, NonPromoProductTime = 10,
            TravelingTime = 60, ReportDuration = 30, QuizDuration = 10,
            Months = { new CycleCapacityMonth { Year = 2026, MonthNumber = 3, MeetingDays = 1, TrainingDays = 1, VacationDays = 2, MicroTargetingDayCount = 3, MicroTargetingDuration = 45, Fte = 12.00m } }
        };

        Assert.False(legacy.UsesTypicalVisitModel());
        Assert.Equal(CycleCapacityVisitModels.Legacy, legacy.VisitModel());
        Assert.Equal(25, legacy.TypicalVisitMinutes());   // promo + non-promo, as before
        Assert.Equal(100, legacy.DailyFixedMinutes());    // travel + report + quiz, as before
        Assert.Equal(30, legacy.ReportMinutesForVisit());

        var result = CycleCapacityCalculator.Calculate(legacy, new[] { March(21) });
        var month = Assert.Single(result.Months);
        Assert.Equal(1835, month.SpendMinutes);
        Assert.Equal(1700, month.DailyFixedMinutes);
        Assert.Equal(3036, month.TotalVisitNumber);       // the FU06 golden figure
        Assert.Equal(CycleCapacityVisitModels.Legacy, result.VisitModel);
        Assert.Equal(25, result.MinutesPerVisit);
    }

    [Fact]
    public void CM05_A_Partial_Triple_On_A_Row_Is_Still_Legacy()
    {
        var capacity = TypicalCapacity();
        capacity.ReportMinutesPerVisit = null;
        capacity.ReportDuration = 7;

        Assert.Equal(CycleCapacityVisitModels.Legacy, capacity.VisitModel());
        Assert.Equal(17, capacity.TypicalVisitMinutes());
        Assert.Equal(112, capacity.DailyFixedMinutes());
    }

    // ── VisitPlanningEngine.DefaultDuration ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void CM06_Planning_Default_Duration_Is_The_Typical_Visit_On_A_Typical_Row()
    {
        Assert.Equal(34, VisitPlanningEngine.DefaultDuration(TypicalCapacity()));

        // Legacy: today's value — the report charge only (min 1), and 30 when no capacity is pinned.
        Assert.Equal(30, VisitPlanningEngine.DefaultDuration(
            new CapacityEntity { PromoProductTime = 15, NonPromoProductTime = 10, ReportDuration = 30 }));
        Assert.Equal(1, VisitPlanningEngine.DefaultDuration(
            new CapacityEntity { PromoProductTime = 15, NonPromoProductTime = 10, ReportDuration = 0 }));
        Assert.Equal(30, VisitPlanningEngine.DefaultDuration(null));
    }

    // ── E8 — micro-targeting clip ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CM07_Micro_Targeting_Days_Are_Clipped_To_Field_Days()
    {
        var capacity = TypicalCapacity(new CycleCapacityMonth
        {
            Year = 2026, MonthNumber = 3, MicroTargetingDayCount = 10, MicroTargetingDuration = 60, Fte = 1m
        });

        var month = Assert.Single(CycleCapacityCalculator.Calculate(capacity, new[] { March(3) }).Months);

        Assert.Equal(3, month.FieldDays);
        Assert.Equal(180, month.MicroTargetingMinutes);   // min(10, 3) × 60, not 600
        Assert.Equal(1440 - 315 - 180, month.RemainingMinutes);

        // A month with no field days charges nothing at all.
        var empty = Assert.Single(CycleCapacityCalculator.Calculate(capacity, new[] { March(0) }).Months);
        Assert.Equal(0, empty.MicroTargetingMinutes);
        Assert.Equal(0, empty.TotalVisitNumber);
    }

    // ── Totals / unresolved calendar ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CM08_Totals_Are_The_Sum_Of_The_Months()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));
        h.Calendar.ByMonth[(2026, 4)] = (CycleCapacityResolutions.Resolved, 20);
        var months = new[]
        {
            new CycleCapacityMonthInput(2026, 3, 2, 1, 1, 2, 60, 1m),
            new CycleCapacityMonthInput(2026, 4, 0, 0, 5, 1, 30, 0.9m)
        };
        var created = await h.Create.Handle(TypicalCreate(periodId, months), CancellationToken.None);
        Assert.True(created.IsSuccessful, string.Join(" | ", created.Errors ?? Array.Empty<string>()));

        var dto = (await h.Calculation.Handle(new GetCycleCapacityCalculationQuery(created.Data), CancellationToken.None)).Data!;
        var totals = Assert.IsType<CycleCapacityCalculationTotalsDto>(dto.Totals);

        Assert.Equal(dto.Months.Sum(m => m.WorkingDays), totals.WorkingDays);
        Assert.Equal(dto.Months.Sum(m => m.DeductedDays), totals.DeductedDays);
        Assert.Equal(dto.Months.Sum(m => m.FieldDays), totals.FieldDays);
        Assert.Equal(dto.Months.Sum(m => m.AvailableMinutes), totals.AvailableMinutes);
        Assert.Equal(dto.Months.Sum(m => m.DailyFixedMinutes), totals.DailyFixedMinutes);
        Assert.Equal(dto.Months.Sum(m => m.MicroTargetingMinutes), totals.MicroTargetingMinutes);
        Assert.Equal(dto.Months.Sum(m => m.RemainingMinutes), totals.RemainingMinutes);
        Assert.Equal(dto.Months.Sum(m => m.TotalVisitNumber), totals.Visits);
        Assert.Equal(dto.TotalVisitNumber, totals.Visits);
        Assert.Equal(0.95m, totals.AverageFte);
        Assert.Equal(184, dto.Months[0].TotalVisitNumber);  // March = the mockup example

        Assert.Equal(CycleCapacityVisitModels.Typical, dto.VisitModel);
        Assert.Equal(34, dto.TypicalVisitMinutes);
        Assert.Equal(105, dto.DailyFixedMinutes);
    }

    [Fact]
    public async Task CM09_Unresolved_Calendar_Still_Gives_No_Numbers()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));
        var created = await h.Create.Handle(TypicalCreate(periodId), CancellationToken.None);
        h.Calendar.ByMonth[(2026, 4)] = (CycleCapacityResolutions.CalendarUnresolved, null);

        var response = await h.Calculation.Handle(new GetCycleCapacityCalculationQuery(created.Data), CancellationToken.None);

        Assert.Equal(503, response.StatusCode);
        Assert.Null(response.Data!.TotalVisitNumber);
        Assert.Null(response.Data.Totals);
        Assert.Empty(response.Data.Months);
    }

    // ── Write path — create / update / preview ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CM10_Typical_Create_Stores_The_Triple_And_A_Zero_Per_Day_Report()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        var created = await h.Create.Handle(TypicalCreate(periodId), CancellationToken.None);

        Assert.Equal(201, created.StatusCode);
        var stored = Assert.Single(h.Repo.Items);
        Assert.Equal((2, 1, 5), (stored.TypicalPromoCount, stored.TypicalNonPromoCount, stored.ReportMinutesPerVisit));
        Assert.Equal(0, stored.ReportDuration);   // the request's per-day 30 is not stored on the typical model

        var detail = (await h.GetById.Handle(new GetCycleCapacityByIdQuery(created.Data), CancellationToken.None)).Data!;
        Assert.Equal(CycleCapacityVisitModels.Typical, detail.VisitModel);
        Assert.Equal(34, detail.TypicalVisitMinutes);
        Assert.Equal(105, detail.DailyFixedMinutes);
        Assert.Equal(5, detail.ReportMinutesPerVisit);
        Assert.True(detail.FteIsEditable);
    }

    [Fact]
    public async Task CM11_Legacy_Create_Is_Unchanged()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        var created = await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);

        var stored = Assert.Single(h.Repo.Items);
        Assert.Equal(30, stored.ReportDuration);
        Assert.Null(stored.TypicalPromoCount);
        var detail = (await h.GetById.Handle(new GetCycleCapacityByIdQuery(created.Data), CancellationToken.None)).Data!;
        Assert.Equal(CycleCapacityVisitModels.Legacy, detail.VisitModel);
        Assert.Equal(25, detail.MinutesPerVisit);
        Assert.Equal(100, detail.DailySpendMinutes);
    }

    [Theory]
    [InlineData(2, null, null, CycleCapacityReasonCodes.TypicalVisitIncomplete)]
    [InlineData(null, 1, 5, CycleCapacityReasonCodes.TypicalVisitIncomplete)]
    [InlineData(0, 0, 5, CycleCapacityReasonCodes.TypicalVisitEmpty)]
    [InlineData(4, 1, 5, CycleCapacityReasonCodes.TypicalCountExceedsMax)]
    [InlineData(1, 4, 5, CycleCapacityReasonCodes.TypicalCountExceedsMax)]
    public async Task CM12_Typical_Visit_Is_Validated(int? promo, int? nonPromo, int? report, string code)
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        var response = await h.Create.Handle(
            TypicalCreate(periodId) with { TypicalPromoCount = promo, TypicalNonPromoCount = nonPromo, ReportMinutesPerVisit = report },
            CancellationToken.None);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(code, response.Errors!);
        Assert.Empty(h.Repo.Items);
    }

    [Fact]
    public async Task CM13_Typical_Visit_Longer_Than_480_Minutes_Is_Refused()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        var response = await h.Create.Handle(
            CreateCommand(periodId, promo: 30, nonPromo: 20) with
            {
                MaxPromoProducts = 10, MaxNonPromoProducts = 10,
                TypicalPromoCount = 10, TypicalNonPromoCount = 10, ReportMinutesPerVisit = 0
            },
            CancellationToken.None);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(CycleCapacityReasonCodes.ActivityMinutesInvalid, response.Errors!);
    }

    [Fact]
    public async Task CM14_Typical_Day_Budget_Ignores_The_Per_Day_Report()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        // travel 400 + quiz 70 = 470 < 480: fine on the typical model, although 400 + 30 + 70 would exhaust the day.
        var typical = await h.Create.Handle(
            TypicalCreate(periodId) with { TravelingTime = 400, QuizDuration = 70 }, CancellationToken.None);
        Assert.Equal(201, typical.StatusCode);

        var otherPeriod = Guid.NewGuid();
        h.Periods.Periods.Add(Period(otherPeriod));
        var legacy = await h.Create.Handle(
            CreateCommand(otherPeriod, traveling: 400, report: 30, quiz: 70), CancellationToken.None);
        Assert.Contains(CycleCapacityReasonCodes.DailySpendExceedsDay, legacy.Errors!);

        // …and the typical model's own day rule still holds.
        var exhausted = await h.Create.Handle(
            TypicalCreate(Guid.NewGuid()) with { CyclePeriodId = otherPeriod, TravelingTime = 410, QuizDuration = 70 },
            CancellationToken.None);
        Assert.Contains(CycleCapacityReasonCodes.DailySpendExceedsDay, exhausted.Errors!);
    }

    [Fact]
    public async Task CM15_Update_Keeps_The_Stored_Model_When_No_Typical_Field_Is_Sent_And_Refuses_A_Mix()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));
        var created = await h.Create.Handle(TypicalCreate(periodId), CancellationToken.None);

        // Today's form: knows nothing about the triple and still posts a per-day report.
        var kept = await h.Update.Handle(UpdateFrom(created.Data, report: 20), CancellationToken.None);
        Assert.True(kept.IsSuccessful, string.Join(" | ", kept.Errors ?? Array.Empty<string>()));
        var stored = Assert.Single(h.Repo.Items);
        Assert.Equal((2, 1, 5), (stored.TypicalPromoCount, stored.TypicalNonPromoCount, stored.ReportMinutesPerVisit));
        Assert.Equal(0, stored.ReportDuration);

        var mixed = await h.Update.Handle(UpdateFrom(created.Data, typicalPromo: 1), CancellationToken.None);
        Assert.Equal(400, mixed.StatusCode);
        Assert.Contains(CycleCapacityReasonCodes.TypicalVisitIncomplete, mixed.Errors!);
    }

    [Fact]
    public async Task CM16_Updating_A_Legacy_Row_With_The_Triple_Switches_It_To_Typical()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));
        var created = await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);

        var updated = await h.Update.Handle(
            UpdateFrom(created.Data, typicalPromo: 2, typicalNonPromo: 1, reportPerVisit: 5), CancellationToken.None);

        Assert.True(updated.IsSuccessful);
        var stored = Assert.Single(h.Repo.Items);
        Assert.Equal(CycleCapacityVisitModels.Typical, stored.VisitModel());
        Assert.Equal(0, stored.ReportDuration);
        Assert.Equal(34, stored.TypicalVisitMinutes());
    }

    [Fact]
    public async Task CM17_Preview_Uses_The_Same_Model_And_Figure_As_The_Save()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));
        var created = await h.Create.Handle(TypicalCreate(periodId), CancellationToken.None);
        var saved = (await h.Calculation.Handle(new GetCycleCapacityCalculationQuery(created.Data), CancellationToken.None)).Data!;

        var preview = (await h.Preview.Handle(
            PreviewQuery(periodId, promo: 12, nonPromo: 5, traveling: 90, report: 30, quiz: 15) with
            {
                TypicalPromoCount = 2, TypicalNonPromoCount = 1, ReportMinutesPerVisit = 5
            },
            CancellationToken.None)).Data!;

        Assert.Equal(CycleCapacityVisitModels.Typical, preview.VisitModel);
        Assert.Equal(saved.TotalVisitNumber, preview.TotalVisitNumber);
        Assert.Equal(saved.Totals, preview.Totals);
    }

    // ── K-5 — authored FTE ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CM18_Authored_Fte_Is_Stored_As_Authored_Zero_Included_And_Kept_When_Omitted()
    {
        var h = Build(TenantA);   // configured interim default 12.00
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));
        var months = new[]
        {
            new CycleCapacityMonthInput(2026, 3, 1, 1, 2, 3, 45, 0m),     // vacant position
            new CycleCapacityMonthInput(2026, 4, 1, 1, 2, 3, 45, 0.75m)
        };

        var created = await h.Create.Handle(CreateCommand(periodId, months: months), CancellationToken.None);
        Assert.Equal(201, created.StatusCode);

        var stored = Assert.Single(h.Repo.Items).OrderedMonths();
        Assert.Equal((0m, CycleCapacityFteSources.Authored), (stored[0].Fte, stored[0].FteSource));
        Assert.Equal((0.75m, CycleCapacityFteSources.Authored), (stored[1].Fte, stored[1].FteSource));

        // The read-time normaliser must not "repair" an authored 0 with the default.
        var normalised = Assert.Single(h.Repo.Items).EnsureMonthlyFte(12.00m).OrderedMonths();
        Assert.Equal(0m, normalised[0].Fte);

        // An edit that omits the FTE keeps the authored values.
        var updated = await h.Update.Handle(
            new UpdateCycleCapacityCommand(created.Data, "TR", 480, 15, 10, 60, 30, 10, null, TwoMonths(), null),
            CancellationToken.None);
        Assert.True(updated.IsSuccessful);
        var kept = Assert.Single(h.Repo.Items).OrderedMonths();
        Assert.Equal((0m, CycleCapacityFteSources.Authored), (kept[0].Fte, kept[0].FteSource));
        Assert.Equal((0.75m, CycleCapacityFteSources.Authored), (kept[1].Fte, kept[1].FteSource));

        // The calculation multiplies by the authored value: a vacant month yields 0 visits.
        var dto = (await h.Calculation.Handle(new GetCycleCapacityCalculationQuery(created.Data), CancellationToken.None)).Data!;
        Assert.Equal(0, dto.Months[0].TotalVisitNumber);
        Assert.Equal(0.38m, dto.Totals!.AverageFte);   // (0 + 0.75) ÷ 2 = 0.375 → AwayFromZero
    }

    [Fact]
    public async Task CM19_Omitted_Fte_On_A_New_Row_Is_The_Interim_Default()
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));

        await h.Create.Handle(CreateCommand(periodId), CancellationToken.None);

        Assert.All(Assert.Single(h.Repo.Items).Months, m =>
            Assert.Equal((12.00m, CycleCapacityFteSources.InterimDefault), (m.Fte, m.FteSource)));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(10000)]
    public async Task CM20_Authored_Fte_Outside_The_Range_Is_Refused(double fte)
    {
        var h = Build(TenantA);
        var periodId = Guid.NewGuid();
        h.Periods.Periods.Add(Period(periodId));
        var months = new[] { new CycleCapacityMonthInput(2026, 3, 0, 0, 0, 0, 0, (decimal)fte) };

        var response = await h.Create.Handle(CreateCommand(periodId, months: months), CancellationToken.None);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(CycleCapacityReasonCodes.MonthFteInvalid, response.Errors!);
    }

    // ── class map ────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CM21_Typical_Fields_Round_Trip_And_An_Old_Document_Reads_Legacy()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();

        var doc = MongoDB.Bson.BsonExtensionMethods.ToBsonDocument(TypicalCapacity(new CycleCapacityMonth
        {
            Year = 2026, MonthNumber = 3, Fte = 0m, FteSource = CycleCapacityFteSources.Authored
        }));
        var back = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<CapacityEntity>(doc);
        Assert.Equal((2, 1, 5), (back.TypicalPromoCount, back.TypicalNonPromoCount, back.ReportMinutesPerVisit));
        Assert.Equal(34, back.TypicalVisitMinutes());
        Assert.Equal(CycleCapacityFteSources.Authored, Assert.Single(back.Months).FteSource);

        doc.Remove(nameof(CapacityEntity.TypicalPromoCount));
        doc.Remove(nameof(CapacityEntity.TypicalNonPromoCount));
        doc.Remove(nameof(CapacityEntity.ReportMinutesPerVisit));
        var old = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<CapacityEntity>(doc);
        Assert.Equal(CycleCapacityVisitModels.Legacy, old.VisitModel());
    }
}
