namespace Diten.Web.Models.CRM;

// ---------------------------------------------------------------------------------------------------------------
// WP-CYC-UI-2 — what the capacity SCREENS make of the CRM's answer: the live summary, the waterfall, supply vs demand,
// the form's limits and the preview request. In a file of their OWN, away from the form view model, so the DataTable
// verifier (which reads the LAST same-named property in the form model's file) is never shadowed by these shapes.
//
// THE RULE OF THIS FILE: nothing here recomputes the capacity. Every figure is copied from the CRM's calculation DTO
// (months, totals, divisor, fixed minutes); the builders only SELECT, ORDER and COMPARE what the CRM already said. A
// figure computed here would eventually disagree with the one the CRM stores, and the author would trust the wrong one.
// ---------------------------------------------------------------------------------------------------------------

/// <summary>
/// The form's input ceilings (E9). Taken from the CRM contract's <c>limits</c>; when the contract cannot be read the
/// CRM's own published constants are used, so the form never invents a looser bound than the runtime enforces.
/// </summary>
public sealed class CycleCapacityFormLimits
{
    // The CRM's CycleCapacityLimits, mirrored ONLY as the fallback for an unreadable contract.
    public const int FallbackMaxMinutesPerDay = 1440;
    public const int FallbackMaxMinutesPerVisit = 480;
    public const int FallbackMaxBufferMinutes = 240;
    public const int FallbackMaxDeductionDays = 31;

    /// <summary>SB-3a products per visit. Not published by the contract (CRM constant 1..10).</summary>
    public const int MinProductsPerVisit = 1;
    public const int MaxProductsPerVisit = 10;

    /// <summary>The month FTE the UI accepts (K-5: 0–1, step 0.05).</summary>
    public const decimal MaxFte = 1m;
    public const decimal FteStep = 0.05m;

    public int MinDailyWorkMinutes { get; set; } = 1;
    public int MaxDailyWorkMinutes { get; set; } = FallbackMaxMinutesPerDay;
    public int MaxMinutesPerDay { get; set; } = FallbackMaxMinutesPerDay;
    public int MaxMinutesPerVisit { get; set; } = FallbackMaxMinutesPerVisit;
    public int MaxBufferMinutes { get; set; } = FallbackMaxBufferMinutes;
    public int MaxDeductionDays { get; set; } = FallbackMaxDeductionDays;

    /// <summary>True when the numbers came from the contract rather than the fallback.</summary>
    public bool FromContract { get; set; }

    public static CycleCapacityFormLimits From(CycleCapacityContractLimitsApiModel? limits)
    {
        if (limits is null || limits.MaxMinutesPerDay <= 0)
        {
            return new CycleCapacityFormLimits();
        }

        return new CycleCapacityFormLimits
        {
            MinDailyWorkMinutes = limits.MinDailyWorkMinutes > 0 ? limits.MinDailyWorkMinutes : 1,
            MaxDailyWorkMinutes = limits.MaxDailyWorkMinutes > 0 ? limits.MaxDailyWorkMinutes : limits.MaxMinutesPerDay,
            MaxMinutesPerDay = limits.MaxMinutesPerDay,
            MaxMinutesPerVisit = limits.MaxMinutesPerVisit > 0 ? limits.MaxMinutesPerVisit : FallbackMaxMinutesPerVisit,
            MaxBufferMinutes = limits.MaxBufferMinutes > 0 ? limits.MaxBufferMinutes : FallbackMaxBufferMinutes,
            MaxDeductionDays = limits.MaxDeductionDays > 0 ? limits.MaxDeductionDays : FallbackMaxDeductionDays,
            FromContract = true
        };
    }
}

/// <summary>
/// WP-CAP-MODEL — the typical-visit triple. ONE definition of "complete" for the save payload, the preview payload and
/// the summary's blockers, so the three can never disagree about which model a set of inputs means.
/// </summary>
public static class CycleCapacityTypicalVisit
{
    /// <summary>All three present. Only then is the triple sent — anything less is sent as nothing.</summary>
    public static bool IsComplete(int? promoCount, int? nonPromoCount, int? reportMinutesPerVisit)
        => promoCount.HasValue && nonPromoCount.HasValue && reportMinutesPerVisit.HasValue;

    /// <summary>One or two of the three: the shape the CRM refuses as <c>typical_visit_incomplete</c>.</summary>
    public static bool IsPartial(int? promoCount, int? nonPromoCount, int? reportMinutesPerVisit)
    {
        var count = (promoCount.HasValue ? 1 : 0) + (nonPromoCount.HasValue ? 1 : 0) + (reportMinutesPerVisit.HasValue ? 1 : 0);
        return count is 1 or 2;
    }
}

/// <summary>The live-preview request as the FORM sends it to the Web proxy. The proxy turns it into the CRM's preview
/// body (dropping what the CRM does not take, and the typical triple unless complete) and keeps the rest — the product
/// ceilings, whether the record is new — to judge the blockers.</summary>
public sealed class CycleCapacityPreviewInput
{
    public Guid? CyclePeriodId { get; set; }
    public string? CalendarCountryCode { get; set; }
    public int? DailyWorkMinutes { get; set; }
    public int? PromoProductTime { get; set; }
    public int? NonPromoProductTime { get; set; }
    public int? TravelingTime { get; set; }
    public int? ReportDuration { get; set; }
    public int? QuizDuration { get; set; }
    public int? TypicalPromoCount { get; set; }
    public int? TypicalNonPromoCount { get; set; }
    public int? ReportMinutesPerVisit { get; set; }
    public int? MaxPromoProducts { get; set; }
    public int? MaxNonPromoProducts { get; set; }

    /// <summary>True on the create page: a NEW capacity is born on the typical model, so an empty triple blocks.</summary>
    public bool IsNew { get; set; }

    public List<CycleCapacityPreviewMonthInput> Months { get; set; } = [];
}

public sealed class CycleCapacityPreviewMonthInput
{
    public int Year { get; set; }
    public int MonthNumber { get; set; }
    public int? MeetingDays { get; set; }
    public int? TrainingDays { get; set; }
    public int? VacationDays { get; set; }
    public int? MicroTargetingDayCount { get; set; }
    public int? MicroTargetingDuration { get; set; }
    public decimal? Fte { get; set; }
}

/// <summary>One coded line of the summary (a blocker or a warning). The page localises <see cref="Code"/>; a month
/// is attached when the line is about one.</summary>
public sealed class CycleCapacitySummaryNote
{
    public string Code { get; set; } = string.Empty;
    public int? Year { get; set; }
    public int? MonthNumber { get; set; }
}

/// <summary>
/// The right-hand summary of the form and the KPI row of the detail page.
/// <para><b>K-4.</b> <see cref="Visits"/> and every day / minute figure are <c>null</c> unless the calendar RESOLVED:
/// an unresolved calendar yields no number at all — never a weekday estimate, never a zero.</para>
/// </summary>
public sealed class CycleCapacitySummary
{
    public const string Resolved = "resolved";
    public const string Unavailable = "unavailable";

    /// <summary><c>resolved</c> | <c>calendar_unresolved</c> | <c>calendar_forbidden</c> | <c>unavailable</c>.</summary>
    public string CalendarStatus { get; set; } = Unavailable;

    public int? Visits { get; set; }
    public int? WorkingDays { get; set; }
    public int? DeductedDays { get; set; }
    public int? FieldDays { get; set; }
    public int? AvailableMinutes { get; set; }
    public int? RemainingMinutes { get; set; }
    public decimal? AverageFte { get; set; }

    /// <summary>The divisor and the per-day charge are inputs-derived and known even when the calendar is not.</summary>
    public int? TypicalVisitMinutes { get; set; }

    public int? DailyFixedMinutes { get; set; }
    public string? VisitModel { get; set; }

    /// <summary>WP-CYC-UI-FIX-2 — the resx key that names <see cref="DailyFixedMinutes"/> for this visit model
    /// (<see cref="CycleCapacityWaterfall.DailyFixedLabelKey"/>).</summary>
    public string DailyFixedLabelKey => CycleCapacityWaterfall.DailyFixedLabelKey(VisitModel);

    /// <summary>The CRM's reason codes, for the "why is there no number?" line.</summary>
    public List<string> ReasonCodes { get; set; } = [];

    public List<CycleCapacitySummaryNote> Blocks { get; set; } = [];
    public List<CycleCapacitySummaryNote> Warnings { get; set; } = [];

    public bool HasNumber => Visits.HasValue;

    /// <summary>The block / warning codes this builder can emit. The page localises each (7 languages).</summary>
    public static readonly IReadOnlyList<string> NoteCodes =
    [
        "typical_visit_required", "typical_visit_incomplete", "typical_visit_empty", "typical_count_exceeds_max",
        "visit_minutes_zero", "cycle_capacity_daily_spend_exceeds_day", "month_zeroed", "micro_targeting_clipped"
    ];

    /// <summary>
    /// Builds the summary from the CRM's calculation and — on the form — the inputs it was asked about.
    /// <para>Blockers are the shapes the CRM would refuse on save (or the UI's own rule that a NEW capacity uses the
    /// typical model). While any blocker stands no number is shown: the inputs do not describe a capacity that can
    /// exist.</para>
    /// </summary>
    public static CycleCapacitySummary Build(CycleCapacityCalculationViewModel? calc, CycleCapacityPreviewInput? input = null)
    {
        var summary = new CycleCapacitySummary();
        if (calc is not null)
        {
            summary.CalendarStatus = string.IsNullOrWhiteSpace(calc.Resolution) ? Unavailable : calc.Resolution;
            summary.ReasonCodes = calc.ReasonCodes.Where(c => !string.IsNullOrWhiteSpace(c) && c != "capacity_ok").ToList();
            summary.TypicalVisitMinutes = calc.TypicalVisitMinutes;
            summary.DailyFixedMinutes = calc.DailyFixedMinutes;
            summary.VisitModel = calc.VisitModel;
        }

        if (input is not null)
        {
            AddBlocks(summary, calc, input);
        }

        // K-4 — no resolved calendar, no figure. The totals are null on an unresolved answer and nothing is substituted.
        if (calc is null || !calc.IsResolved || calc.Totals is not { } totals)
        {
            return summary;
        }

        foreach (var month in calc.Months)
        {
            if (month.DeductedDays > month.WorkingDays)
            {
                summary.Warnings.Add(new CycleCapacitySummaryNote { Code = "month_zeroed", Year = month.Year, MonthNumber = month.MonthNumber });
            }

            // E8 — the CRM clipped micro-targeting days the month could not hold. Read off the CRM's own row: more days
            // asked for than field days left.
            if (month.MicroTargetingDayCount > month.FieldDays && month.MicroTargetingDuration > 0)
            {
                summary.Warnings.Add(new CycleCapacitySummaryNote { Code = "micro_targeting_clipped", Year = month.Year, MonthNumber = month.MonthNumber });
            }
        }

        if (summary.Blocks.Count > 0)
        {
            return summary;
        }

        summary.Visits = totals.Visits;
        summary.WorkingDays = totals.WorkingDays;
        summary.DeductedDays = totals.DeductedDays;
        summary.FieldDays = totals.FieldDays;
        summary.AvailableMinutes = totals.AvailableMinutes;
        summary.RemainingMinutes = totals.RemainingMinutes;
        summary.AverageFte = totals.AverageFte;
        return summary;
    }

    private static void AddBlocks(CycleCapacitySummary summary, CycleCapacityCalculationViewModel? calc, CycleCapacityPreviewInput input)
    {
        var promo = input.TypicalPromoCount;
        var nonPromo = input.TypicalNonPromoCount;
        var report = input.ReportMinutesPerVisit;

        if (CycleCapacityTypicalVisit.IsPartial(promo, nonPromo, report))
        {
            summary.Blocks.Add(new CycleCapacitySummaryNote { Code = "typical_visit_incomplete" });
        }
        else if (CycleCapacityTypicalVisit.IsComplete(promo, nonPromo, report))
        {
            if (promo == 0 && nonPromo == 0)
            {
                summary.Blocks.Add(new CycleCapacitySummaryNote { Code = "typical_visit_empty" });
            }

            var maxPromo = input.MaxPromoProducts ?? CycleCapacityEditViewModel.DefaultMaxProductsPerVisit;
            var maxNonPromo = input.MaxNonPromoProducts ?? CycleCapacityEditViewModel.DefaultMaxProductsPerVisit;
            if (promo > maxPromo || nonPromo > maxNonPromo)
            {
                summary.Blocks.Add(new CycleCapacitySummaryNote { Code = "typical_count_exceeds_max" });
            }
        }
        else if (input.IsNew)
        {
            // A NEW capacity is born on the typical model (K-1); the legacy arithmetic exists only for old rows.
            summary.Blocks.Add(new CycleCapacitySummaryNote { Code = "typical_visit_required" });
        }

        if (calc is not null && calc.TypicalVisitMinutes <= 0
            && summary.Blocks.All(b => b.Code is not ("typical_visit_empty" or "typical_visit_required" or "typical_visit_incomplete")))
        {
            summary.Blocks.Add(new CycleCapacitySummaryNote { Code = "visit_minutes_zero" });
        }

        // The CRM's own per-day fixed charge against the working day the author typed.
        if (calc is not null && input.DailyWorkMinutes is int workMinutes && workMinutes > 0 && calc.DailyFixedMinutes >= workMinutes)
        {
            summary.Blocks.Add(new CycleCapacitySummaryNote { Code = "cycle_capacity_daily_spend_exceeds_day" });
        }
    }
}

/// <summary>Every code the screens localise (key <c>Code_{code}</c>): the summary's blockers and warnings, the CRM's
/// calendar reasons, and <c>unknown</c> for anything else. One list, so the bridge and the 7-language test agree.</summary>
public static class CycleCapacityL10nCodes
{
    public static readonly IReadOnlyList<string> CalendarReasons =
    [
        "calendar_unresolved", "calendar_forbidden", "country_underivable", "cycle_capacity_months_required",
        "cycle_capacity_period_not_found"
    ];

    public static readonly IReadOnlyList<string> All =
        CycleCapacitySummary.NoteCodes.Concat(CalendarReasons).Append("unknown").ToList();
}

/// <summary>One step of the detail page's calculation waterfall.</summary>
public sealed class CycleCapacityWaterfallStep
{
    /// <summary>The resx label key of the step.</summary>
    public string LabelKey { get; set; } = string.Empty;

    public int Value { get; set; }

    /// <summary><c>days</c> or <c>minutes</c>.</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>True for a deduction (rendered with a minus sign).</summary>
    public bool IsDeduction { get; set; }

    /// <summary>True for a subtotal the steps above it arrive at.</summary>
    public bool IsSubtotal { get; set; }

    /// <summary>Bar length relative to the step's own scale (days to working days, minutes to available minutes).
    /// Presentation only.</summary>
    public int Percent { get; set; }
}

/// <summary>
/// The waterfall (mockup 06), read straight off the CRM's <c>totals</c>: working days → deducted → field days →
/// available minutes → fixed daily charges → micro-targeting → left for visits → ÷ typical visit × average FTE →
/// visits. <c>null</c> when the calendar did not resolve (K-4).
/// </summary>
public sealed class CycleCapacityWaterfall
{
    public List<CycleCapacityWaterfallStep> Steps { get; set; } = [];
    public int TypicalVisitMinutes { get; set; }
    public decimal AverageFte { get; set; }
    public int Visits { get; set; }

    /// <summary>The step labels, in order. The page localises each (7 languages).</summary>
    public static readonly IReadOnlyList<string> StepLabelKeys =
    [
        "WfWorkingDays", "WfDeductedDays", "WfFieldDays", "WfAvailableMinutes", "WfDailyFixedMinutes",
        "WfMicroTargetingMinutes", "WfRemainingMinutes"
    ];

    /// <summary>The fixed-daily-charge label of the LEGACY model, where the report is charged per day as well.</summary>
    public const string DailyFixedLegacyLabelKey = "WfDailyFixedMinutesLegacy";

    /// <summary>
    /// WP-CYC-UI-FIX-2 — what the fixed daily charge is made of depends on the visit model: on the typical model it is
    /// travel + quiz (the report is charged per VISIT); on the legacy model the report is charged per DAY too, so the
    /// label must say "travel, report, quiz". One rule for the waterfall and the edit form's live summary.
    /// </summary>
    public static string DailyFixedLabelKey(string? visitModel)
        => string.Equals(visitModel, "legacy", StringComparison.OrdinalIgnoreCase) ? DailyFixedLegacyLabelKey : StepLabelKeys[4];

    public static CycleCapacityWaterfall? From(CycleCapacityCalculationViewModel? calc)
    {
        if (calc is null || !calc.IsResolved || calc.Totals is not { } t)
        {
            return null;
        }

        static int Pct(int value, int scale) => scale <= 0 ? 0 : (int)Math.Round(Math.Clamp(value * 100.0 / scale, 0, 100));

        return new CycleCapacityWaterfall
        {
            TypicalVisitMinutes = calc.TypicalVisitMinutes,
            AverageFte = t.AverageFte,
            Visits = t.Visits,
            Steps =
            [
                new() { LabelKey = StepLabelKeys[0], Value = t.WorkingDays, Unit = "days", Percent = Pct(t.WorkingDays, t.WorkingDays) },
                new() { LabelKey = StepLabelKeys[1], Value = t.DeductedDays, Unit = "days", IsDeduction = true, Percent = Pct(t.DeductedDays, t.WorkingDays) },
                new() { LabelKey = StepLabelKeys[2], Value = t.FieldDays, Unit = "days", IsSubtotal = true, Percent = Pct(t.FieldDays, t.WorkingDays) },
                new() { LabelKey = StepLabelKeys[3], Value = t.AvailableMinutes, Unit = "minutes", Percent = Pct(t.AvailableMinutes, t.AvailableMinutes) },
                new() { LabelKey = DailyFixedLabelKey(calc.VisitModel), Value = t.DailyFixedMinutes, Unit = "minutes", IsDeduction = true, Percent = Pct(t.DailyFixedMinutes, t.AvailableMinutes) },
                new() { LabelKey = StepLabelKeys[5], Value = t.MicroTargetingMinutes, Unit = "minutes", IsDeduction = true, Percent = Pct(t.MicroTargetingMinutes, t.AvailableMinutes) },
                new() { LabelKey = StepLabelKeys[6], Value = t.RemainingMinutes, Unit = "minutes", IsSubtotal = true, Percent = Pct(t.RemainingMinutes, t.AvailableMinutes) }
            ]
        };
    }
}

// ── period usage (WP-CYC-UI-1 §Sözleşme: GET /api/crm/cycle-periods/{id}/usage) ──────────────────────────────────

public sealed class CyclePeriodUsageApiModel
{
    public CyclePeriodUsageCapacityApiModel? Capacity { get; set; }
    public List<CyclePeriodUsageCampaignApiModel> Campaigns { get; set; } = [];
    public List<CyclePeriodUsageSessionApiModel> PlanningSessions { get; set; } = [];
    public CyclePeriodUsagePlannedVisitsApiModel? PlannedVisits { get; set; }
    public List<CyclePeriodUsageDemandApiModel> DemandByMonth { get; set; } = [];
}

public sealed class CyclePeriodUsageCapacityApiModel
{
    public Guid CycleCapacityId { get; set; }
    public bool IsArchived { get; set; }
}

public sealed class CyclePeriodUsageCampaignApiModel
{
    public Guid CampaignId { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Status { get; set; }
}

public sealed class CyclePeriodUsageSessionApiModel
{
    public Guid PlanningSessionId { get; set; }
    public string? Name { get; set; }
    public string? OwnerDisplayName { get; set; }
    public string? Status { get; set; }
    public int CommittedVisitCount { get; set; }
}

public sealed class CyclePeriodUsagePlannedVisitsApiModel
{
    public int Total { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = [];
}

public sealed class CyclePeriodUsageDemandApiModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int PlannedVisits { get; set; }
}

/// <summary>One month of supply vs demand (per rep, K-7).</summary>
public sealed class CycleCapacitySupplyDemandMonth
{
    public int Year { get; set; }
    public int MonthNumber { get; set; }

    /// <summary>The CRM's estimate for the month; <c>null</c> when the calendar did not resolve (K-4).</summary>
    public int? Supply { get; set; }

    /// <summary>Planned visits from the period's planning sessions (<c>usage.demandByMonth</c>).</summary>
    public int Demand { get; set; }

    public bool IsOver => Supply.HasValue && Demand > Supply.Value;
    public int Overage => Supply.HasValue ? Math.Max(0, Demand - Supply.Value) : 0;
}

/// <summary>
/// Supply vs demand (K-7): the capacity's monthly visits (per rep) against the planned visits the period's planning
/// sessions committed. A pure comparison of two numbers the CRM published; team size is NOT part of it (follow-up).
/// </summary>
public sealed class CycleCapacitySupplyDemand
{
    /// <summary>False when the usage read failed — the card then says so instead of drawing an empty comparison.</summary>
    public bool UsageAvailable { get; set; }

    /// <summary>False when the calendar did not resolve: demand is still shown, supply is not.</summary>
    public bool SupplyAvailable { get; set; }

    public List<CycleCapacitySupplyDemandMonth> Months { get; set; } = [];
    public List<CyclePeriodUsageSessionApiModel> Sessions { get; set; } = [];
    public int? TotalSupply { get; set; }
    public int TotalDemand { get; set; }

    /// <summary>Demand as a share of supply, rounded; null without supply.</summary>
    public int? Percent { get; set; }

    public int? FreeCapacity { get; set; }

    public IEnumerable<CycleCapacitySupplyDemandMonth> OverMonths => Months.Where(m => m.IsOver);

    public static CycleCapacitySupplyDemand From(CycleCapacityCalculationViewModel? calc, CyclePeriodUsageApiModel? usage)
    {
        var resolved = calc is { IsResolved: true, Totals: not null };
        var result = new CycleCapacitySupplyDemand { UsageAvailable = usage is not null, SupplyAvailable = resolved };
        if (usage is null)
        {
            return result;
        }

        var demand = usage.DemandByMonth
            .GroupBy(d => (d.Year, d.Month))
            .ToDictionary(g => g.Key, g => g.Sum(x => Math.Max(0, x.PlannedVisits)));

        var keys = new SortedSet<(int Year, int Month)>(demand.Keys);
        if (resolved)
        {
            foreach (var m in calc!.Months)
            {
                keys.Add((m.Year, m.MonthNumber));
            }
        }

        var supply = resolved
            ? calc!.Months.ToDictionary(m => (m.Year, m.MonthNumber), m => m.TotalVisitNumber)
            : new Dictionary<(int, int), int>();

        result.Months = keys
            .Select(k => new CycleCapacitySupplyDemandMonth
            {
                Year = k.Year,
                MonthNumber = k.Month,
                Supply = resolved ? (supply.TryGetValue(k, out var s) ? s : 0) : null,
                Demand = demand.TryGetValue(k, out var d) ? d : 0
            })
            .ToList();

        result.Sessions = usage.PlanningSessions;
        result.TotalDemand = result.Months.Sum(m => m.Demand);
        if (resolved)
        {
            result.TotalSupply = calc!.Totals!.Visits;
            result.FreeCapacity = Math.Max(0, result.TotalSupply.Value - result.TotalDemand);
            result.Percent = result.TotalSupply.Value <= 0
                ? null
                : (int)Math.Round(result.TotalDemand * 100.0 / result.TotalSupply.Value, MidpointRounding.AwayFromZero);
        }

        return result;
    }
}

/// <summary>
/// WP-CYC-UI-FIX-2 — a planning session's status as the capacity detail page names it: the CRM's vocabulary
/// (PlanningSessionStatus: draft / generated / committed / archived) mapped to resx keys; anything else is
/// "unknown" — a raw code is never shown to the reader.
/// </summary>
public static class CycleCapacitySessionStatus
{
    public static readonly IReadOnlyList<string> Known = ["draft", "generated", "committed", "archived"];

    public const string UnknownKey = "SessionStatus_unknown";

    public static string LabelKey(string? status)
    {
        var normalized = (status ?? string.Empty).Trim().ToLowerInvariant();
        return Known.Contains(normalized) ? "SessionStatus_" + normalized : UnknownKey;
    }
}
