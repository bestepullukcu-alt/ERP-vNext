namespace Diten.CrmService.Application.Features.RouteOptimization;

/// <summary>
/// MOD-0155 FU03 — the v1 greedy time-window insertion heuristic (pack §4.5). <b>Pure</b>: a static function of
/// (visits, working day, seed, period, buffer, travel model) with NO I/O — no <c>HttpClient</c>, no repository, no
/// <c>DateTime.UtcNow</c>, no tenant context — mirroring <c>VisitFrequencyResolveEngine</c>. Deterministic: identical
/// input yields byte-identical output, the tie-break ending on <c>visitId</c> (D-TIEBREAK).
/// <para>Routing with time windows (VRPTW) is NP-hard; this is a fast constructive heuristic, not an optimal solver.
/// Optimality is NOT claimed — the <c>unscheduled</c> list makes any shortfall honest and visible (D-UNSCHEDULED). A
/// production solver can swap behind <see cref="IRouteOptimizer"/> with no contract change (F-SOLVER).</para>
/// </summary>
public static class TimeWindowInsertionEngine
{
    public static RouteOptimizationOutput Schedule(
        IReadOnlyList<RouteVisitInput> visits,
        WorkingDayHours workingDay,
        GeoPoint? startLocation,
        OptimizationPeriod period,
        int betweenVisitMinutes,
        ITravelModel travel)
    {
        var scheduled = new List<ScheduledVisit>();
        var unscheduled = new List<UnscheduledVisit>();

        // Resolve the working-day window once. A malformed working day falls back to the documented default rather than
        // throwing — the engine never 500s on bad input, it degrades to a schedule.
        var wStart = RouteTime.ParseMinutes(workingDay.Start) ?? RouteTime.ParseMinutes(RouteOptimizationDefaults.WorkDayStart)!.Value;
        var wEnd = RouteTime.ParseMinutes(workingDay.End) ?? RouteTime.ParseMinutes(RouteOptimizationDefaults.WorkDayEnd)!.Value;
        var lunchStart = RouteTime.ParseMinutes(workingDay.LunchStart) ?? RouteTime.ParseMinutes(RouteOptimizationDefaults.LunchStart)!.Value;
        var lunchEnd = RouteTime.ParseMinutes(workingDay.LunchEnd) ?? RouteTime.ParseMinutes(RouteOptimizationDefaults.LunchEnd)!.Value;
        if (wEnd <= wStart)
        {
            (wStart, wEnd) = (
                RouteTime.ParseMinutes(RouteOptimizationDefaults.WorkDayStart)!.Value,
                RouteTime.ParseMinutes(RouteOptimizationDefaults.WorkDayEnd)!.Value);
        }

        var maxContiguous = MaxContiguousWorkMinutes(wStart, wEnd, lunchStart, lunchEnd);

        // --- Pre-processing: split the given set into the schedulable pool vs. shape-rejected visits. ---
        var pool = new List<VisitState>();
        foreach (var v in visits ?? Array.Empty<RouteVisitInput>())
        {
            if (v.DurationMinutes <= 0)
            {
                unscheduled.Add(new UnscheduledVisit(v.VisitId, RouteUnscheduledReasonCodes.InvalidInput));
                continue;
            }

            if (!RouteTime.IsValidCoordinate(v.Lat, v.Long))
            {
                unscheduled.Add(new UnscheduledVisit(v.VisitId, RouteUnscheduledReasonCodes.MissingLocation));
                continue;
            }

            if (v.DurationMinutes > maxContiguous)
            {
                unscheduled.Add(new UnscheduledVisit(v.VisitId, RouteUnscheduledReasonCodes.DurationExceedsWorkingDay));
                continue;
            }

            pool.Add(new VisitState(v, new GeoPoint(v.Lat, v.Long), BuildWindows(v, wStart, wEnd)));
        }

        var dates = EnumerateDates(period);
        if (pool.Count == 0 || dates.Count == 0)
        {
            // Nothing schedulable (empty / all shape-rejected) or no days: whatever survived pre-processing that still
            // has demand is period-exhausted. Empty input ⇒ both lists empty, never an error.
            foreach (var v in pool)
            {
                unscheduled.Add(new UnscheduledVisit(v.Input.VisitId, LeftoverReason(v, dates)));
            }

            return Order(scheduled, unscheduled);
        }

        // --- Day departure (D-DAYSEED, A): a fixed home base (startLocation) when the rep returns home each night, so
        // every day departs from it; otherwise each day starts near the REMAINING visits' OWN cluster (nearest their
        // centroid), NOT the previous day's last stop — which used to strand a far cluster into a whole-morning jump.
        // (B backlog: an explicit rep home/office input + an overnight "continue from last" intercity mode.)
        foreach (var date in dates)
        {
            if (pool.Count == 0)
            {
                break;
            }

            var weekday = RouteTime.WeekdayFromDate(date);
            var readyTime = wStart;                 // when the rep can depart the current location
            var currentLoc = startLocation ?? NearestToCentroid(pool);
            var placedToday = new List<(ScheduledVisit Visit, GeoPoint Loc)>();
            var sequence = 1;

            while (pool.Count > 0)
            {
                Candidate? best = null;
                foreach (var v in pool)
                {
                    var travelMinutes = TravelIntMinutes(travel, currentLoc, v.Location);
                    // No home base ⇒ no commute TO the day's first visit — it begins at that visit at wStart. The
                    // centroid seed still RANKS which visit opens the day (travelMinutes, below), it just adds no phantom
                    // drive-time. A real home base (B) makes the first arrival wStart + travel(home → first visit).
                    var arrival = (startLocation is null && sequence == 1) ? readyTime : readyTime + travelMinutes;
                    var placement = EarliestFeasibleStart(v, weekday, arrival, wStart, wEnd, lunchStart, lunchEnd);
                    if (placement is not { } p)
                    {
                        continue;
                    }

                    var candidate = new Candidate(v, travelMinutes, p.Start, p.WindowStart);
                    if (best is null || IsBetter(candidate, best.Value))
                    {
                        best = candidate;
                    }
                }

                if (best is not { } chosen)
                {
                    break; // no feasible visit fits the remainder of this day
                }

                var end = chosen.Start + chosen.Visit.Input.DurationMinutes;
                var placed = new ScheduledVisit(
                    chosen.Visit.Input.VisitId, date,
                    RouteTime.Format(chosen.Start), RouteTime.Format(end),
                    0, sequence);
                placedToday.Add((placed, chosen.Visit.Location));

                pool.Remove(chosen.Visit);
                readyTime = end + betweenVisitMinutes;
                currentLoc = chosen.Visit.Location;
                sequence++;
            }

            // Fill travelToNext for the day (raw visit→visit travel; 0 for the last of the day), then roll the seed.
            for (var i = 0; i < placedToday.Count; i++)
            {
                var travelToNext = i < placedToday.Count - 1
                    ? TravelIntMinutes(travel, placedToday[i].Loc, placedToday[i + 1].Loc)
                    : 0;
                scheduled.Add(placedToday[i].Visit with { TravelToNextMinutes = travelToNext });
            }
        }

        // --- Whatever is left never fit anywhere in the period: the supply-vs-demand warning, materialised. ---
        foreach (var v in pool)
        {
            unscheduled.Add(new UnscheduledVisit(v.Input.VisitId, LeftoverReason(v, dates)));
        }

        return Order(scheduled, unscheduled);
    }

    /// <summary>
    /// MOD-0155 — MANUAL-ORDER variant (F-SOLVER). Places the visit set in the caller's given target-id order instead of
    /// the greedy pick, but reuses the SAME feasibility: each visit is placed at its earliest feasible start honoring the
    /// contact's availability windows (HARD), working hours, lunch, travel from the previous placed visit, and a multi-day
    /// advance (roll to the next working day when the current one can no longer hold it). A visit with no feasible slot in
    /// the period lands on the <c>unscheduled</c> list, exactly as the greedy does. Frequency-across-weeks is untouched —
    /// the caller runs this per week over that week's visit set only.
    /// </summary>
    public static RouteOptimizationOutput ScheduleInOrder(
        IReadOnlyList<RouteVisitInput> visits,
        WorkingDayHours workingDay,
        GeoPoint? startLocation,
        OptimizationPeriod period,
        int betweenVisitMinutes,
        ITravelModel travel,
        IReadOnlyList<Guid> orderedTargetIds)
    {
        var scheduled = new List<ScheduledVisit>();
        var unscheduled = new List<UnscheduledVisit>();

        var wStart = RouteTime.ParseMinutes(workingDay.Start) ?? RouteTime.ParseMinutes(RouteOptimizationDefaults.WorkDayStart)!.Value;
        var wEnd = RouteTime.ParseMinutes(workingDay.End) ?? RouteTime.ParseMinutes(RouteOptimizationDefaults.WorkDayEnd)!.Value;
        var lunchStart = RouteTime.ParseMinutes(workingDay.LunchStart) ?? RouteTime.ParseMinutes(RouteOptimizationDefaults.LunchStart)!.Value;
        var lunchEnd = RouteTime.ParseMinutes(workingDay.LunchEnd) ?? RouteTime.ParseMinutes(RouteOptimizationDefaults.LunchEnd)!.Value;
        if (wEnd <= wStart)
        {
            (wStart, wEnd) = (RouteTime.ParseMinutes(RouteOptimizationDefaults.WorkDayStart)!.Value, RouteTime.ParseMinutes(RouteOptimizationDefaults.WorkDayEnd)!.Value);
        }

        var maxContiguous = MaxContiguousWorkMinutes(wStart, wEnd, lunchStart, lunchEnd);

        var pool = new List<VisitState>();
        foreach (var v in visits ?? Array.Empty<RouteVisitInput>())
        {
            if (v.DurationMinutes <= 0) { unscheduled.Add(new UnscheduledVisit(v.VisitId, RouteUnscheduledReasonCodes.InvalidInput)); continue; }
            if (!RouteTime.IsValidCoordinate(v.Lat, v.Long)) { unscheduled.Add(new UnscheduledVisit(v.VisitId, RouteUnscheduledReasonCodes.MissingLocation)); continue; }
            if (v.DurationMinutes > maxContiguous) { unscheduled.Add(new UnscheduledVisit(v.VisitId, RouteUnscheduledReasonCodes.DurationExceedsWorkingDay)); continue; }
            pool.Add(new VisitState(v, new GeoPoint(v.Lat, v.Long), BuildWindows(v, wStart, wEnd)));
        }

        var dates = EnumerateDates(period);
        if (pool.Count == 0 || dates.Count == 0)
        {
            foreach (var v in pool) unscheduled.Add(new UnscheduledVisit(v.Input.VisitId, LeftoverReason(v, dates)));
            return Order(scheduled, unscheduled);
        }

        // Order the schedulable pool by the caller's target-id sequence; unknown / null targets keep their original
        // relative order at the end (OrderBy is stable).
        var orderIndex = new Dictionary<Guid, int>();
        for (var i = 0; i < (orderedTargetIds?.Count ?? 0); i++) { var id = orderedTargetIds![i]; if (!orderIndex.ContainsKey(id)) orderIndex[id] = i; }
        var ordered = pool.OrderBy(v => v.Input.TargetId is { } tid && orderIndex.TryGetValue(tid, out var ix) ? ix : int.MaxValue).ToList();

        // Day-1 seed (D-DAYSEED): explicit startLocation, else the visit nearest the visit-set centroid.
        var seed = startLocation ?? NearestToCentroid(pool);
        var placedByDate = new List<(ScheduledVisit Visit, GeoPoint Loc, DateOnly Date)>();

        var dateIdx = 0; var readyTime = wStart; var currentLoc = seed; var seqInDay = 1; GeoPoint? lastLoc = null;
        foreach (var v in ordered)
        {
            var placed = false;
            var d = dateIdx; var rt = readyTime; var cl = currentLoc; var sq = seqInDay;
            while (d < dates.Count)
            {
                var weekday = RouteTime.WeekdayFromDate(dates[d]);
                // No home base ⇒ no commute to the day's FIRST visit (it begins at wStart); a fixed home base makes it
                // wStart + travel(home → first). Later visits always travel from the previous one.
                var travelMinutes = (startLocation is null && sq == 1) ? 0 : TravelIntMinutes(travel, cl, v.Location);
                var arrival = rt + travelMinutes;
                var placement = EarliestFeasibleStart(v, weekday, arrival, wStart, wEnd, lunchStart, lunchEnd);
                if (placement is { } p)
                {
                    var end = p.Start + v.Input.DurationMinutes;
                    placedByDate.Add((new ScheduledVisit(v.Input.VisitId, dates[d], RouteTime.Format(p.Start), RouteTime.Format(end), 0, sq), v.Location, dates[d]));
                    dateIdx = d; readyTime = end + betweenVisitMinutes; currentLoc = v.Location; seqInDay = sq + 1; lastLoc = v.Location;
                    placed = true;
                    break;
                }

                // Not feasible today → roll to the next working day and retry the SAME visit. A fixed home base
                // (startLocation) departs each new day; without one the manual sequence continues from the previous
                // day's last placed location (the rep's chosen order dictates the path here, so no centroid re-seed).
                d++; rt = wStart; cl = startLocation ?? lastLoc ?? seed; sq = 1;
            }

            if (!placed) unscheduled.Add(new UnscheduledVisit(v.Input.VisitId, LeftoverReason(v, dates))); // cursor unchanged
        }

        // TravelToNext — raw visit→visit within a day (0 for the last of a day); placedByDate is in placement (date-asc) order.
        for (var i = 0; i < placedByDate.Count; i++)
        {
            var toNext = (i + 1 < placedByDate.Count && placedByDate[i + 1].Date == placedByDate[i].Date)
                ? TravelIntMinutes(travel, placedByDate[i].Loc, placedByDate[i + 1].Loc)
                : 0;
            scheduled.Add(placedByDate[i].Visit with { TravelToNextMinutes = toNext });
        }

        return Order(scheduled, unscheduled);
    }

    // ---- deterministic tie-break (D-TIEBREAK): lowest added travel → earliest window start → lowest visitId ----
    private static bool IsBetter(Candidate a, Candidate b)
    {
        if (a.TravelMinutes != b.TravelMinutes) return a.TravelMinutes < b.TravelMinutes;
        if (a.WindowStart != b.WindowStart) return a.WindowStart < b.WindowStart;
        return a.Visit.Input.VisitId.CompareTo(b.Visit.Input.VisitId) < 0;
    }

    private static RouteOptimizationOutput Order(List<ScheduledVisit> scheduled, List<UnscheduledVisit> unscheduled)
        => new(
            scheduled
                .OrderBy(s => s.AssignedDate)
                .ThenBy(s => s.SequenceOrder)
                .ToList(),
            unscheduled
                .OrderBy(u => u.VisitId)
                .ToList());

    /// <summary>Earliest feasible start (minutes) for a visit on a weekday given the earliest arrival, or null when it
    /// cannot be placed that day. Honors working hours, lunch and — HARD — the per-contact availability windows.</summary>
    private static Placement? EarliestFeasibleStart(
        VisitState v, string weekday, int arrival, int wStart, int wEnd, int lunchStart, int lunchEnd)
    {
        IReadOnlyList<Window> windows = v.WindowsByDay.TryGetValue(weekday, out var dayWindows)
            ? dayWindows
            : (v.HasAnyWindows ? Array.Empty<Window>() : new[] { new Window(wStart, wEnd) });

        Placement? best = null;
        foreach (var window in windows)
        {
            // Split the window by lunch into contiguous work segments; a visit may never straddle lunch.
            foreach (var (segStart, segEnd) in Segments(window.Start, window.End, lunchStart, lunchEnd))
            {
                var start = Math.Max(arrival, segStart);
                if (start + v.Input.DurationMinutes <= segEnd)
                {
                    if (best is null || start < best.Value.Start
                        || (start == best.Value.Start && window.Start < best.Value.WindowStart))
                    {
                        best = new Placement(start, window.Start);
                    }

                    break; // earliest feasible segment of this window found
                }
            }
        }

        return best;
    }

    /// <summary>The contiguous work segments of [start,end) once lunch is removed, in chronological order.</summary>
    private static IEnumerable<(int Start, int End)> Segments(int start, int end, int lunchStart, int lunchEnd)
    {
        if (lunchEnd <= lunchStart || lunchEnd <= start || lunchStart >= end)
        {
            // No lunch overlap with this window.
            if (end > start) yield return (start, end);
            yield break;
        }

        var before = (start, Math.Min(end, lunchStart));
        if (before.Item2 > before.Item1) yield return before;

        var after = (Math.Max(start, lunchEnd), end);
        if (after.Item2 > after.Item1) yield return after;
    }

    private static int MaxContiguousWorkMinutes(int wStart, int wEnd, int lunchStart, int lunchEnd)
    {
        var max = 0;
        foreach (var (s, e) in Segments(wStart, wEnd, lunchStart, lunchEnd))
        {
            max = Math.Max(max, e - s);
        }

        return max;
    }

    /// <summary>Can this visit EVER fit some availability window on some date in the period, ignoring capacity? Used to
    /// distinguish an availability dead-end from plain period exhaustion.</summary>
    private static bool CanEverFit(VisitState v, IReadOnlyList<DateOnly> dates)
    {
        if (!v.HasAnyWindows)
        {
            return true; // only working hours bound it, and it already passed the working-day size gate
        }

        foreach (var date in dates)
        {
            var weekday = RouteTime.WeekdayFromDate(date);
            if (!v.WindowsByDay.TryGetValue(weekday, out var windows))
            {
                continue;
            }

            foreach (var window in windows)
            {
                if (window.End - window.Start >= v.Input.DurationMinutes)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string LeftoverReason(VisitState v, IReadOnlyList<DateOnly> dates)
        => v.HasAnyWindows && !CanEverFit(v, dates)
            ? RouteUnscheduledReasonCodes.NoFeasibleAvailabilityWindow
            : RouteUnscheduledReasonCodes.PeriodExhausted;

    /// <summary>The visit whose location is nearest the geometric centroid of the pool (deterministic; ties by visitId).</summary>
    private static GeoPoint NearestToCentroid(IReadOnlyList<VisitState> pool)
    {
        var centroid = new GeoPoint(pool.Average(p => p.Location.Lat), pool.Average(p => p.Location.Long));
        VisitState? nearest = null;
        var bestKm = double.MaxValue;
        foreach (var v in pool.OrderBy(p => p.Input.VisitId))
        {
            var km = HaversineTravelModel.HaversineKm(centroid, v.Location);
            if (km < bestKm)
            {
                bestKm = km;
                nearest = v;
            }
        }

        return nearest!.Location;
    }

    private static IReadOnlyList<DateOnly> EnumerateDates(OptimizationPeriod period)
    {
        var dates = new List<DateOnly>();
        if (period.DateTo < period.DateFrom)
        {
            return dates;
        }

        for (var d = period.DateFrom; d <= period.DateTo; d = d.AddDays(1))
        {
            dates.Add(d);
        }

        return dates;
    }

    /// <summary>Availability windows grouped by weekday, each intersected with working hours. Malformed windows are
    /// dropped rather than throwing.</summary>
    private static Dictionary<string, List<Window>> BuildWindows(RouteVisitInput v, int wStart, int wEnd)
    {
        var byDay = new Dictionary<string, List<Window>>(StringComparer.OrdinalIgnoreCase);
        if (v.AvailabilityWindows is null)
        {
            return byDay;
        }

        foreach (var w in v.AvailabilityWindows)
        {
            var day = w.Day?.Trim().ToLowerInvariant();
            var start = RouteTime.ParseMinutes(w.Start);
            var end = RouteTime.ParseMinutes(w.End);
            if (day is null || !RouteTime.Weekdays.Contains(day) || start is null || end is null || end <= start)
            {
                continue;
            }

            var clampedStart = Math.Max(start.Value, wStart);
            var clampedEnd = Math.Min(end.Value, wEnd);
            if (clampedEnd <= clampedStart)
            {
                continue; // window lies entirely outside working hours
            }

            if (!byDay.TryGetValue(day, out var list))
            {
                list = new List<Window>();
                byDay[day] = list;
            }

            list.Add(new Window(clampedStart, clampedEnd));
        }

        foreach (var list in byDay.Values)
        {
            list.Sort((a, b) => a.Start.CompareTo(b.Start));
        }

        return byDay;
    }

    private static int TravelIntMinutes(ITravelModel travel, GeoPoint from, GeoPoint to)
        => (int)Math.Ceiling(Math.Max(0, travel.TravelMinutes(from, to)));

    private sealed class VisitState
    {
        public RouteVisitInput Input { get; }
        public GeoPoint Location { get; }
        public Dictionary<string, List<Window>> WindowsByDay { get; }
        public bool HasAnyWindows { get; }

        public VisitState(RouteVisitInput input, GeoPoint location, Dictionary<string, List<Window>> windowsByDay)
        {
            Input = input;
            Location = location;
            WindowsByDay = windowsByDay;
            HasAnyWindows = (input.AvailabilityWindows?.Count ?? 0) > 0;
        }
    }

    private readonly record struct Window(int Start, int End);

    private readonly record struct Placement(int Start, int WindowStart);

    private readonly record struct Candidate(VisitState Visit, int TravelMinutes, int Start, int WindowStart);
}
