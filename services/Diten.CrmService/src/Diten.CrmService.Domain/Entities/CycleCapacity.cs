namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0155 FU06 — <b>CycleCapacity</b>: the visit-capacity model pinned to ONE <see cref="CyclePeriod"/>. It answers
/// exactly one question — <i>"during this period, how many visits can the field force make in each month?"</i> — and
/// the answer is an <b>ESTIMATE</b>, never a quota or a commitment.
/// <para><b>What it is not.</b> It is not a period: <see cref="CyclePeriod"/> (MOD-0165 FU06/FU07) owns which period a
/// plan belongs to, and this aggregate only PINS one by id. It is not a working calendar: whether a DAY is a working
/// day belongs to the platform working-calendar capability (CAND-CAP-0008), which this reads over HTTP and never
/// writes. It is not a frequency policy: how often a target SHOULD be visited stays
/// <see cref="VisitFrequencyPolicy"/> (MOD-0165 FU03) — "can" and "should" are different questions. It is not a plan:
/// MicroTarget rows are MOD-0155 FU05 and are never born here. It is not an HR record: the FTE is an interim
/// configured average, not an establishment count.</para>
/// <para><b>The computed number is NEVER stored.</b> There is deliberately no TotalVisitNumber field anywhere on this
/// class or on <see cref="CycleCapacityMonth"/>. Working calendars change — a holiday is published, a tenant override
/// is added — and a persisted figure would start lying silently the moment they do. Only the INPUTS live here; the
/// figure is recomputed on every read.</para>
/// <para><b>The month model is EXPLICIT.</b> Every row in <see cref="Months"/> carries its own
/// <see cref="CycleCapacityMonth.Year"/> and <see cref="CycleCapacityMonth.MonthNumber"/>. There is no positional
/// twelve-element array, no magic row id and no legacy-system coupling: a period crossing new year's eve
/// (Dec 2026 – Jan 2027) is representable here and is not representable in a positional array.</para>
/// <para><b>No lifecycle of its own.</b> There is no status field. Whether a capacity may be edited derives from the
/// pinned period: a <c>closed</c> period freezes it. A second state machine would be a second source of truth.</para>
/// <para>Tenant-owned (<see cref="EntityBase"/>); TenantId is server-resolved and never accepted from a payload.</para>
/// </summary>
public sealed class CycleCapacity : EntityBase
{
    /// <summary>
    /// The pinned period. <b>Immutable after creation</b> and unique among the tenant's non-deleted rows (1:1): a
    /// capacity belongs to one period for its whole life, and re-pointing it at another period would silently rewrite
    /// what a past estimate was an estimate OF.
    /// <para>Only the id is kept. The period's code, name, window and scope are re-read through
    /// <c>ICyclePeriodReader</c> on every projection — copying them here would go stale the moment the period is
    /// renamed.</para>
    /// </summary>
    public Guid CyclePeriodId { get; set; }

    /// <summary>
    /// The country whose working calendar answers "how many working days?" — an ISO alpha-2 code from the governed
    /// MOD-0048 reference set, upper-cased.
    /// <para><b>This is a working-calendar QUERY PARAMETER, not a scope (D-COUNTRY = B).</b> It is not part of the
    /// aggregate's identity, plays no part in uniqueness, and takes no part in any precedence or resolution: the
    /// capacity's ADDRESS is entirely the pinned period's. It exists because the working calendar always needs a
    /// country while a <see cref="CyclePeriod"/> scoped <c>tenant</c> — the common default — has none to derive.</para>
    /// <para>When the pinned period IS country-scoped the value is derived from it server-side and the caller's own
    /// value is ignored, so the two can never disagree.</para>
    /// </summary>
    public string CalendarCountryCode { get; set; } = string.Empty;

    /// <summary>Minutes in a field working day (8 h × 60 = 480 by default). Stored rather than read from config at
    /// calculation time: a configured default that changes tomorrow must not silently change what an old capacity
    /// meant.</summary>
    public int DailyWorkMinutes { get; set; }

    /// <summary>Minutes spent on promoted products in ONE visit.</summary>
    public int PromoProductTime { get; set; }

    /// <summary>Minutes spent on non-promoted products in ONE visit. Together with
    /// <see cref="PromoProductTime"/> this forms the divisor, and their sum must be greater than zero — a rule enforced
    /// on the write path so the calculator can never divide by zero.</summary>
    public int NonPromoProductTime { get; set; }

    /// <summary>Minutes spent travelling on a field DAY (not per visit).</summary>
    public int TravelingTime { get; set; }

    /// <summary>Minutes spent reporting on a field DAY.</summary>
    public int ReportDuration { get; set; }

    /// <summary>Minutes spent on quizzes/knowledge checks on a field DAY.</summary>
    public int QuizDuration { get; set; }

    /// <summary>
    /// MOD-0155 FU06B — the buffer left between two CONSECUTIVE visits when a field day is packed. NEW in FU06B; FU06
    /// carried <see cref="TravelingTime"/> but never a between-visit tampon.
    /// <para><b>It is NOT part of a single visit's duration</b> and takes no part in
    /// <see cref="ActivityTimeBudgetCalculator"/> (see MOD-0155 FU05 packing, which applies it BETWEEN visits). It is
    /// stored here only so the packing engine can read one configured value.</para>
    /// <para><b>Nullable on purpose (read-time migration, D-MIGRATION).</b> A document written before FU06B has no such
    /// element and deserializes as <c>null</c>, which <see cref="EnsureBetweenVisitTime"/> fills with the configured
    /// default — while an author who deliberately set <c>0</c> keeps it, because <c>0</c> and "absent" are different
    /// answers. There is nothing to backfill: only this one new field needs a default, and the reused FU06 fields are
    /// already present on every row.</para>
    /// </summary>
    public int? BetweenVisitTimeMinutes { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// FU07 — whatever the stored document carries that this class no longer declares.
    /// <para>It exists for exactly ONE reason: FU06 kept a single root <c>Fte</c>, and FU07 moved the FTE onto each
    /// month. A row written by FU06 still has that root value in Mongo, and <see cref="EnsureMonthlyFte"/> copies it
    /// onto every month so the old row keeps producing the number it always produced. Without this hook the value
    /// would be unreadable and an old capacity would silently switch to today's configured default.</para>
    /// <para>A plain dictionary rather than a BSON type: the domain has no Mongo dependency and must not gain one.
    /// The class map points its extra-elements member here, which is a persistence concern and is declared there.</para>
    /// </summary>
    public Dictionary<string, object?>? LegacyElements { get; set; }

    /// <summary>
    /// The month rows, one per calendar month the pinned period touches. Identified by
    /// (<see cref="CycleCapacityMonth.Year"/>, <see cref="CycleCapacityMonth.MonthNumber"/>) and ordered by them —
    /// never by array position.
    /// </summary>
    public List<CycleCapacityMonth> Months { get; set; } = new();

    /// <summary>Soft archive. Archiving hides a capacity from the working list without deleting the inputs an old
    /// estimate was made from.</summary>
    public bool IsArchived { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    /// <summary>Sum of the three per-day minute charges. Kept here so the validator and the calculator read one
    /// definition.</summary>
    public int DailySpendMinutes() => TravelingTime + ReportDuration + QuizDuration;

    /// <summary>Minutes one visit consumes. The write path guarantees this is greater than zero.</summary>
    public int MinutesPerVisit() => PromoProductTime + NonPromoProductTime;

    /// <summary>Month rows in calendar order — over integers only, never over a DateTimeOffset (the parallel-array
    /// trap), and never over list position.</summary>
    public IReadOnlyList<CycleCapacityMonth> OrderedMonths()
        => Months.OrderBy(m => m.Year).ThenBy(m => m.MonthNumber).ToList();

    /// <summary>
    /// FU07 — gives every month an FTE when the stored row predates the per-month field.
    /// <para><b>This is a read-time normalisation, not a migration.</b> Nothing is written back here and no backfill
    /// script exists; the value is persisted only when the row is next written for its own reasons. It is the same
    /// device <c>CyclePeriod.EnsureScopeType()</c> uses, for the same reason: a schema move that needs no operator
    /// step and cannot half-apply.</para>
    /// <para>The FU06 root value wins over the configured default, so <b>an old capacity keeps producing exactly the
    /// figure it produced before</b>. Falling back to today's average would silently change what a saved estimate
    /// means.</para>
    /// </summary>
    public CycleCapacity EnsureMonthlyFte(decimal configuredDefault)
    {
        var legacyFte = LegacyRootFte();

        foreach (var month in Months)
        {
            if (month.Fte > 0m)
            {
                continue;
            }

            month.Fte = legacyFte ?? configuredDefault;
            month.FteSource = CycleCapacityFteSources.InterimDefault;
        }

        return this;
    }

    /// <summary>
    /// MOD-0155 FU06B — gives the capacity a between-visit buffer when the stored row predates the field.
    /// <para><b>Read-time normalisation, not a migration</b> — the same device as <see cref="EnsureMonthlyFte"/> and
    /// <c>CyclePeriod.EnsureScopeType()</c>: nothing is written back, no backfill script exists, and the value is
    /// persisted only when the row is next written for its own reasons. A missing element (an FU06/FU07 row) reads as
    /// <c>null</c> and takes the configured default; an explicit <c>0</c> is left alone, because <c>0</c> and "absent"
    /// are different answers.</para>
    /// <para>It touches ONLY this new field. The reused FU06 duration fields
    /// (<see cref="PromoProductTime"/>/<see cref="NonPromoProductTime"/>/<see cref="ReportDuration"/>) already exist on
    /// every row, so there is nothing to seed — and the FU06 <c>TotalVisitNumber</c> is unaffected, this field never
    /// entering the capacity arithmetic.</para>
    /// </summary>
    public CycleCapacity EnsureBetweenVisitTime(int configuredDefault)
    {
        BetweenVisitTimeMinutes ??= configuredDefault;
        return this;
    }

    /// <summary>
    /// The FU06 root <c>Fte</c>, read out of the stored document's extra elements. Null when the row was written by
    /// FU07 (which has no root FTE) or when the value cannot be read as a positive number — in which case the caller
    /// falls back to the configured default rather than guessing.
    /// </summary>
    public decimal? LegacyRootFte()
    {
        if (LegacyElements is null || !LegacyElements.TryGetValue("Fte", out var raw) || raw is null)
        {
            return null;
        }

        return raw switch
        {
            decimal value when value > 0m => value,
            double value when value > 0d => (decimal)value,
            int value when value > 0 => value,
            long value when value > 0L => value,
            string value when decimal.TryParse(value, out var parsed) && parsed > 0m => parsed,
            _ => null
        };
    }
}

/// <summary>
/// One month of a <see cref="CycleCapacity"/>. <b>Explicitly addressed</b> by (<see cref="Year"/>,
/// <see cref="MonthNumber"/>): the legacy model's twelve positional columns could not express a period that crosses a
/// year boundary, and its magic row id made two rows for the same month indistinguishable.
/// <para>It carries only DEDUCTIONS and the micro-targeting charge. It does <b>not</b> carry the month's working-day
/// count — that comes from the working calendar at read time — and it does not carry the resulting visit number.</para>
/// </summary>
public sealed class CycleCapacityMonth
{
    public int Year { get; set; }

    /// <summary>1–12.</summary>
    public int MonthNumber { get; set; }

    /// <summary>Whole working days lost to meetings in this month.</summary>
    public int MeetingDays { get; set; }

    /// <summary>Whole working days lost to training in this month.</summary>
    public int TrainingDays { get; set; }

    /// <summary>Whole working days lost to leave in this month.</summary>
    public int VacationDays { get; set; }

    /// <summary>How many days of this month carry a micro-targeting charge.</summary>
    public int MicroTargetingDayCount { get; set; }

    /// <summary>Minutes the micro-targeting charge costs on one such day. Together with
    /// <see cref="MicroTargetingDayCount"/> it is a MONTHLY minute pool, not a per-day rate — which is why the
    /// calculation is expressed at month level.</summary>
    public int MicroTargetingDuration { get; set; }

    /// <summary>
    /// FU07 — the full-time-equivalent field force for THIS month.
    /// <para>It lives on the month rather than on the capacity because a field force is seasonal: one number for a
    /// whole cycle cannot say that August is thinner than March. Moving it here is what makes that sayable. The value
    /// itself is still an <b>INTERIM configured average</b>, still written by the server, and still rendered DISABLED;
    /// when an HR source arrives the field is already in the right place and only the lock comes off (F-FTE-HR).</para>
    /// </summary>
    public decimal Fte { get; set; }

    /// <summary>Where <see cref="Fte"/> came from — <see cref="CycleCapacityFteSources"/>. Documentation, never
    /// identity.</summary>
    public string FteSource { get; set; } = CycleCapacityFteSources.InterimDefault;

    /// <summary>Whole days deducted from the calendar's working days before any minute arithmetic.</summary>
    public int DeductedDays() => MeetingDays + TrainingDays + VacationDays;

    /// <summary>The monthly micro-targeting minute pool.</summary>
    public int MicroTargetingMinutes() => MicroTargetingDayCount * MicroTargetingDuration;
}

/// <summary>Where a stored <see cref="CycleCapacityMonth.Fte"/> came from. In-domain and fail-closed, like every other
/// structural vocabulary in this service.</summary>
public static class CycleCapacityFteSources
{
    /// <summary>The configured interim average. The only value this FU ever writes.</summary>
    public const string InterimDefault = "interim-default";

    /// <summary>Reserved for the day an HR source or an explicit author supplies it (F-FTE-HR). Never written here —
    /// published so a consumer can already branch on provenance rather than assuming every value is an average.</summary>
    public const string Authored = "authored";

    public static readonly IReadOnlyList<string> All = new[] { InterimDefault, Authored };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);
}

/// <summary>
/// Why a capacity calculation could — or could not — be answered.
/// <para>This exists because a bare number would be a lie. If no working calendar has been published for the country
/// and year, an <c>int</c> has to say something, and the reader then plans against a figure nobody ever stated. The
/// result carries the resolution instead, and the value is <c>null</c> whenever it is not <see cref="Resolved"/>.
/// <b><c>null</c> and <c>0</c> are different answers</b>: <c>0</c> means "no time is left for visits this month",
/// <c>null</c> means "the calendar did not speak and we do not know".</para>
/// </summary>
public static class CycleCapacityResolutions
{
    /// <summary>Every month resolved; the figures are usable.</summary>
    public const string Resolved = "resolved";

    /// <summary>The working calendar could not answer for at least one month (missing calendar, missing year, unknown
    /// country, or an unreachable dependency). NO partial table is produced.</summary>
    public const string CalendarUnresolved = "calendar_unresolved";

    /// <summary>The working calendar refused the caller (403). Deliberately distinct from
    /// <see cref="CalendarUnresolved"/>: the calendar may well exist and we were simply not allowed to read it, which
    /// is an RBAC problem with a different fix (follow-up F-RBAC-WC).</summary>
    public const string CalendarForbidden = "calendar_forbidden";

    public static readonly IReadOnlyList<string> All = new[] { Resolved, CalendarUnresolved, CalendarForbidden };
}

/// <summary>Machine-readable outcome codes, so a UI and a smoke script can branch without parsing prose.</summary>
public static class CycleCapacityReasonCodes
{
    public const string CapacityOk = "capacity_ok";
    public const string CalendarUnresolved = "calendar_unresolved";
    public const string CalendarForbidden = "calendar_forbidden";
    public const string CountryUnderivable = "country_underivable";
    public const string MonthOutOfPeriod = "month_out_of_period";
    public const string DuplicateCapacity = "duplicate_capacity";
    public const string VisitMinutesZero = "visit_minutes_zero";
    public const string PeriodClosed = "period_closed";
    public const string PeriodNotFound = "cycle_capacity_period_not_found";
    public const string PinImmutable = "cycle_capacity_pin_immutable";
    public const string CountryRequired = "cycle_capacity_country_required";
    public const string CountryUnknown = "cycle_capacity_country_unknown";
    public const string ReferenceSetUnpublished = "cycle_capacity_reference_set_unpublished";
    public const string DailyWorkMinutesInvalid = "cycle_capacity_daily_work_minutes_invalid";
    public const string ActivityMinutesInvalid = "cycle_capacity_activity_minutes_invalid";
    public const string DailySpendExceedsDay = "cycle_capacity_daily_spend_exceeds_day";
    public const string MonthsRequired = "cycle_capacity_months_required";
    public const string MonthInvalid = "cycle_capacity_month_invalid";
    public const string MonthDuplicate = "cycle_capacity_month_duplicate";
    public const string DeductionInvalid = "cycle_capacity_deduction_invalid";
    public const string MonthFteInvalid = "cycle_capacity_month_fte_invalid";
    public const string DescriptionInvalid = "cycle_capacity_description_invalid";

    /// <summary>MOD-0155 FU06B — the between-visit buffer is outside its published range.</summary>
    public const string BetweenVisitTimeInvalid = "cycle_capacity_between_visit_time_invalid";

    public const string ConcurrencyConflict = "cycle_capacity_concurrency_conflict";
    public const string NotFound = "cycle_capacity_not_found";

    public static readonly IReadOnlyList<string> All = new[]
    {
        CapacityOk, CalendarUnresolved, CalendarForbidden, CountryUnderivable, MonthOutOfPeriod, DuplicateCapacity,
        VisitMinutesZero, PeriodClosed, PeriodNotFound, PinImmutable, CountryRequired, CountryUnknown,
        ReferenceSetUnpublished, DailyWorkMinutesInvalid, ActivityMinutesInvalid, DailySpendExceedsDay, MonthsRequired,
        MonthInvalid, MonthDuplicate, DeductionInvalid, MonthFteInvalid, DescriptionInvalid, BetweenVisitTimeInvalid,
        ConcurrencyConflict, NotFound
    };
}

/// <summary>Published ceilings, so the editor enforces exactly the numbers the runtime does.</summary>
public static class CycleCapacityLimits
{
    /// <summary>A day has 1440 minutes; nothing measured in minutes-per-day may exceed it.</summary>
    public const int MaxMinutesPerDay = 1440;

    /// <summary>A single visit longer than eight hours is a data-entry error, not a visit.</summary>
    public const int MaxMinutesPerVisit = 480;

    /// <summary>MOD-0155 FU06B — the between-visit buffer. A four-hour gap between two visits is a scheduling error,
    /// not a buffer, so the ceiling is four hours.</summary>
    public const int MaxBufferMinutes = 240;

    public const int MinDailyWorkMinutes = 1;
    public const int MaxDailyWorkMinutes = 1440;

    public const int MinYear = 2000;
    public const int MaxYear = 2100;

    public const int MinMonthNumber = 1;
    public const int MaxMonthNumber = 12;

    /// <summary>A month cannot lose more than 31 whole days to anything.</summary>
    public const int MaxDeductionDays = 31;

    /// <summary>A period is a planning cycle, not a decade: twelve month rows is already a full year.</summary>
    public const int MaxMonths = 24;

    public const int MaxDescriptionLength = 1000;

    /// <summary>ISO alpha-2, so exactly two characters.</summary>
    public const int CalendarCountryCodeLength = 2;

    public const decimal MinFte = 0.01m;
    public const decimal MaxFte = 9999m;
}
