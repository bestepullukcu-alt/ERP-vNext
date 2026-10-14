namespace Diten.CrmService.Application.Features.VisitPlanning;

/// <summary>
/// WP-VW-W2 (BE-b) — one day's TIME pins: the in-day counterpart of <see cref="DayBalancer.AssignAroundPins"/>. The route
/// optimizer has already ordered the day (its geographic sequence); this pure step re-times it around the visits the rep
/// pinned to a start time.
/// <list type="number">
/// <item><b>Time-pinned visits</b> sit at their time, earliest pin first (a tie: the pin given first). One that overlaps a
/// visit already sitting there (its between-visit buffer included), the lunch break or the working window moves to the
/// NEAREST free start on the <see cref="Step"/>-minute grid (the later one on a tie) — the engine reports it as
/// <c>pin_time_conflict</c>. No free start at all: it is not placed (the day is full for it).</item>
/// <item><b>The other visits</b> keep the route's order and fill the day from its start: each starts after the previous
/// one (its buffer and the travel between them), honours its availability windows and the lunch break, and goes BEFORE
/// a pinned visit only when it ends in time to travel there; otherwise it goes after it (travelling from the pinned
/// visit). One that no longer fits the working window is not placed (the day ran out), one whose windows cannot hold it
/// any more neither (no feasible window) — the engine tries it on another day, as for any route miss.</item>
/// </list>
/// Times are minutes from midnight. Deterministic: the same stops give the same day.
/// </summary>
public static class DayTimePins
{
    /// <summary>The start-time grid (the mockup's 15-minute calendar rows); a pin time must sit on it.</summary>
    public const int Step = 15;

    /// <param name="Key">The caller's id of the stop.</param>
    /// <param name="PinMinute">The pinned start (minutes from midnight), null for a visit without a time pin.</param>
    /// <param name="PinOrder">Which pin came first (a tie of two pins at the same time).</param>
    /// <param name="Windows">The visit's availability windows ON THIS DAY (null / empty = the working window only).</param>
    public sealed record Stop(
        int Key,
        int DurationMinutes,
        double Lat,
        double Long,
        int? PinMinute = null,
        int PinOrder = 0,
        IReadOnlyList<(int From, int To)>? Windows = null);

    public sealed record Slot(int Key, int Start, int End);

    /// <param name="Placed">Every placed stop, in time order.</param>
    /// <param name="DayFull">Stops the working window could not hold any more.</param>
    /// <param name="NoWindow">Stops whose availability windows could not hold them any more.</param>
    public sealed record Result(IReadOnlyList<Slot> Placed, IReadOnlyList<int> DayFull, IReadOnlyList<int> NoWindow);

    public static Result Place(
        IReadOnlyList<Stop> routeOrder,
        int dayStart,
        int dayEnd,
        int lunchStart,
        int lunchEnd,
        int buffer,
        Func<Stop, Stop, int> travel)
    {
        var blocks = new List<(Stop Stop, int Start, int End)>();
        var dayFull = new List<int>();
        var noWindow = new List<int>();
        bool InLunch(int s, int e) => lunchEnd > lunchStart && s < lunchEnd && e > lunchStart;

        // 1 · the time pins, earliest first; a taken time moves to the nearest free start of the grid.
        foreach (var pin in routeOrder.Where(s => s.PinMinute is not null).OrderBy(s => s.PinMinute).ThenBy(s => s.PinOrder).ThenBy(s => s.Key))
        {
            var want = pin.PinMinute!.Value;
            var dur = Math.Max(1, pin.DurationMinutes);
            bool Free(int s) => s >= dayStart && s + dur <= dayEnd && !InLunch(s, s + dur)
                                && blocks.All(b => s + dur + buffer <= b.Start || s >= b.End + buffer);
            int? at = null;
            for (var delta = 0; at is null && delta <= dayEnd - dayStart; delta += Step)
            {
                if (Free(want + delta))
                {
                    at = want + delta;
                }
                else if (delta > 0 && Free(want - delta))
                {
                    at = want - delta;
                }
            }

            if (at is { } a)
            {
                blocks.Add((pin, a, a + dur));
            }
            else
            {
                dayFull.Add(pin.Key);
            }
        }

        blocks.Sort((x, y) => x.Start.CompareTo(y.Start));
        var placed = blocks.Select(b => new Slot(b.Stop.Key, b.Start, b.End)).ToList();

        // 2 · the others, in the route's order, before / between / after the pinned visits.
        var cursor = dayStart;   // the earliest the next visit may start (the previous one's end + buffer)
        Stop? origin = null;     // where the rep travels from
        foreach (var stop in routeOrder.Where(s => s.PinMinute is null))
        {
            var dur = Math.Max(1, stop.DurationMinutes);
            var from = cursor;
            var at = cursor + (origin is null ? 0 : travel(origin, stop));
            var failed = false;
            for (var guard = 0; guard < 4 * (blocks.Count + 4); guard++)
            {
                var before = at;
                if (stop.Windows is { Count: > 0 } windows)
                {
                    var start = at;
                    var fit = windows.Where(w => Math.Max(start, w.From) + dur <= w.To).Select(w => (int?)Math.Max(start, w.From)).Min();
                    if (fit is null)
                    {
                        noWindow.Add(stop.Key);
                        failed = true;
                        break;
                    }

                    at = fit.Value;
                }

                if (InLunch(at, at + dur))
                {
                    at = lunchEnd;
                }

                // a pinned visit not yet passed that this one would run into (or not reach in time from): go after it
                var hit = blocks.FirstOrDefault(b => b.End + buffer > from
                                                     && at + dur + buffer + travel(stop, b.Stop) > b.Start
                                                     && at < b.End + buffer + travel(b.Stop, stop));
                if (hit.Stop is not null)
                {
                    at = hit.End + buffer + travel(hit.Stop, stop);
                    from = hit.End + buffer;
                }

                if (at == before)
                {
                    break;
                }
            }

            if (failed)
            {
                continue;
            }

            if (at + dur > dayEnd)
            {
                dayFull.Add(stop.Key);
                continue;
            }

            placed.Add(new Slot(stop.Key, at, at + dur));
            cursor = at + dur + buffer;
            origin = stop;
        }

        return new Result(placed.OrderBy(s => s.Start).ThenBy(s => s.Key).ToList(), dayFull, noWindow);
    }

    /// <summary>"HH:mm" on the <see cref="Step"/> grid → minutes from midnight; null for anything else.</summary>
    public static int? ParseGridTime(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length != 5 || text[2] != ':'
            || !int.TryParse(text.AsSpan(0, 2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var h)
            || !int.TryParse(text.AsSpan(3, 2), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var m)
            || h > 23 || m > 59)
        {
            return null;
        }

        return h * 60 + m;
    }
}
