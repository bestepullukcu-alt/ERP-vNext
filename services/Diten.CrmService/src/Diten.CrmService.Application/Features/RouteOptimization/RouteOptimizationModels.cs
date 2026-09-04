namespace Diten.CrmService.Application.Features.RouteOptimization;

/// <summary>
/// MOD-0155 FU03 — the normative input/output contract for the <see cref="IRouteOptimizer"/> seam (pack §4). The whole
/// surface is a <b>pure function over a supplied set</b>: every id, coordinate, duration and window arrives as INPUT
/// and the output is RETURNED, never written. Times are <c>"HH:mm"</c> local wall-time and dates are
/// <see cref="DateOnly"/> — deliberately NOT <c>DateTimeOffset</c>, which keeps the CRM parallel-arrays / instant-vs-date
/// traps (FU01 §4.8) out even of the DTO shape.
/// </summary>
public sealed record RouteOptimizationInput(
    IReadOnlyList<RouteVisitInput> Visits,
    RepWorkingHours RepWorkingHours,
    OptimizationPeriod Period,
    int BetweenVisitMinutes,
    TravelModelSpec TravelModel,
    // Optional MANUAL sequence (target ids, first→last). When present the optimizer schedules the set in THIS order using
    // the same feasibility (availability windows + working hours + lunch + travel + multi-day) instead of the greedy pick
    // (F-SOLVER seam). Null/empty ⇒ the greedy default is byte-identical. Additive; the contract is otherwise unchanged.
    IReadOnlyList<Guid>? OrderedTargetIds = null);

/// <summary>
/// One visit in the GIVEN set. The caller (FU05) already selected it; FU04 already produced its
/// <see cref="DurationMinutes"/>. FU03 computes neither — it only orders and slots what it is handed.
/// </summary>
/// <param name="VisitId">Correlates the output back to the caller's PlannedVisit; opaque to the engine.</param>
/// <param name="Lat">HCP latitude, supplied (FU03 never geocodes). NaN / out-of-range ⇒ <c>missing_location</c>.</param>
/// <param name="Long">HCP longitude, supplied. NaN / out-of-range ⇒ <c>missing_location</c>.</param>
/// <param name="DurationMinutes">GIVEN (FU04 via FU06B calc). Must be &gt; 0; FU03 never computes it (D-DURATION).</param>
/// <param name="AvailabilityWindows">Per-contact HARD constraint (D-AVAIL). Empty ⇒ only working hours bound the visit.</param>
/// <param name="TargetId">Passed through for the caller; the engine never resolves it.</param>
public sealed record RouteVisitInput(
    Guid VisitId,
    double Lat,
    double Long,
    int DurationMinutes,
    IReadOnlyList<AvailabilityWindow>? AvailabilityWindows = null,
    Guid? TargetId = null);

/// <summary>
/// A per-contact availability window (D-AVAIL, HARD). <see cref="Day"/> is a <b>WEEKDAY</b>
/// (<c>monday…sunday</c>), matching MOD-0150 <c>ContactAvailability</c>'s per-weekday model — the engine maps each
/// weekday onto every concrete date in the period (D-AVAIL-DAY, LOCKED). It is NOT a specific date. <see cref="Start"/>
/// / <see cref="End"/> are <c>"HH:mm"</c> local wall-time.
/// </summary>
public sealed record AvailabilityWindow(string Day, string Start, string End);

/// <summary>
/// The rep's working window for the period. v1 is a SINGLE per-day window applied to every day (a per-weekday / per-rep
/// table is the additive MOD-0288 / HR seam, not built here). <see cref="PerDay"/> may be null so the config default
/// (09:00–18:00, lunch 13:00–14:00) fills it (D-WORKINGHOURS-SOURCE = config). <see cref="StartLocation"/> is the
/// OPTIONAL day-1 geographic seed (D-DAYSEED); when absent the engine seeds day 1 from the visit nearest the visit-set
/// centroid.
/// </summary>
public sealed record RepWorkingHours(
    WorkingDayHours? PerDay = null,
    GeoPoint? StartLocation = null);

/// <summary>A single working-day window plus its lunch break — all <c>"HH:mm"</c> local wall-time.</summary>
public sealed record WorkingDayHours(string Start, string End, string LunchStart, string LunchEnd);

/// <summary>A latitude/longitude pair.</summary>
public sealed record GeoPoint(double Lat, double Long);

/// <summary>The inclusive window of days available for assignment.</summary>
public sealed record OptimizationPeriod(DateOnly DateFrom, DateOnly DateTo);

/// <summary>
/// The travel-cost model to use. v1 is <c>{ kind: "haversine", roadFactor: 1.3 }</c> — in-house ONLY, no external
/// routing/map/geocoding API (D-TRAVEL). <see cref="RoadFactor"/> and <see cref="AssumedSpeedKmPerMin"/> are nullable
/// so the config default (via the defaults provider) applies when the caller omits them (D-SPEED = config-with-default).
/// </summary>
public sealed record TravelModelSpec(
    string Kind = TravelModelKinds.Haversine,
    double? RoadFactor = null,
    double? AssumedSpeedKmPerMin = null);

/// <summary>The travel-model kinds this FU understands. v1 ships exactly one; a solver-provided matrix is F-SOLVER.</summary>
public static class TravelModelKinds
{
    public const string Haversine = "haversine";
}

/// <summary>The optimizer result — the placed visits plus the supply-vs-demand warning, materialised.</summary>
public sealed record RouteOptimizationOutput(
    IReadOnlyList<ScheduledVisit> Scheduled,
    IReadOnlyList<UnscheduledVisit> Unscheduled);

/// <summary>One placed visit. <c>endTime = startTime + durationMinutes</c>; honors working hours, lunch and availability.</summary>
public sealed record ScheduledVisit(
    Guid VisitId,
    DateOnly AssignedDate,
    string StartTime,
    string EndTime,
    int TravelToNextMinutes,
    int SequenceOrder);

/// <summary>One visit that could not be feasibly placed. <see cref="Reason"/> ∈ <see cref="RouteUnscheduledReasonCodes"/>.
/// This is a WARNING the planner resolves (drop / reschedule / extend period / override), never a hard block (D-UNSCHEDULED).</summary>
public sealed record UnscheduledVisit(Guid VisitId, string Reason);

/// <summary>
/// The <c>unscheduled[].reason</c> vocabulary — engine-internal, in-domain, fail-closed (the FU01 / FU06B in-domain-vocab
/// precedent). No MOD-0048 publish is a runtime precondition; these are not an operator-published reference set.
/// </summary>
public static class RouteUnscheduledReasonCodes
{
    /// <summary>The period ran out of feasible days/hours before this visit could be placed (supply &lt; demand).</summary>
    public const string PeriodExhausted = "period_exhausted";

    /// <summary>No availability window on any concrete date in the period can hold the visit's duration.</summary>
    public const string NoFeasibleAvailabilityWindow = "no_feasible_availability_window";

    /// <summary>The visit is longer than the largest contiguous working-day segment (lunch splits the day).</summary>
    public const string DurationExceedsWorkingDay = "duration_exceeds_working_day";

    /// <summary>Latitude/longitude is missing or invalid; the visit cannot be routed (never a crash).</summary>
    public const string MissingLocation = "missing_location";

    /// <summary>The visit itself is malformed (e.g. non-positive duration); it cannot be scheduled.</summary>
    public const string InvalidInput = "invalid_input";

    public static readonly IReadOnlyList<string> All = new[]
    {
        PeriodExhausted, NoFeasibleAvailabilityWindow, DurationExceedsWorkingDay, MissingLocation, InvalidInput
    };
}

/// <summary>
/// MOD-0155 FU03 permission keys (PKS-001: lowercase-dotted, at least 3 segments, kebab-case). <b>DEFINITION ONLY</b> —
/// this file seeds NOTHING: no DB write, no role template, no grant. The dry-run preview endpoint guards on the new key
/// <see cref="Preview"/>; its catalog row + grant are a separate operator step (F-RBAC). Unlike some sibling FUs this
/// key is NOT aliased onto a territory fallback — the pack (§14) puts the real key on the endpoint, so the endpoint
/// answers 403 until the key is granted, which is the intended fail-closed behaviour.
/// </summary>
public static class RouteOptimizationPermissions
{
    public const string Preview = "crm.visit-route.preview";

    public static readonly IReadOnlyList<string> All = new[] { Preview };
}
