using Diten.CrmService.Application.Features.RouteOptimization;

namespace Diten.CrmService.Application.Features.VisitPlanning;

/// <summary>
/// WP-VP-3B (C1) + WP-VP-4E — which DAY of a week each visit goes to, before any route is drawn. Pure and deterministic.
/// <para><b>Groups.</b> The week's visits are first gathered into institution GROUPS (the same account and the pharmacies
/// linked to it); a group's place is the cost-weighted centre of its located visits.</para>
/// <para><b>Geography (4E, <see cref="Assign"/>).</b>
/// (a) SEEDS — as many groups as the week has open days without a centre yet (a pinned day already has one): the largest
/// group, then repeatedly the group FARTHEST from every seed so far (ties → larger, then key; a pinned day's centre does not
/// take part, so a pin never reshuffles the rest of the week); each seed opens the
/// earliest centre-less day it fits whole.
/// (b) Every other group, largest first, goes WHOLE to the day whose centre is NEAREST among the days that hold it and are
/// NEAR (travel to the centre ≤ <see cref="NearTravelMinutes"/>) — ties → lower fill, then date; a centre-less day takes
/// it when no near day does.
/// (c) The day's centre moves to the cost-weighted mean of what it holds.
/// (d) GAP FILL / SPLIT — a group no near day holds whole is split over the NEAR days: its doctors nearest the day's
/// centre first, as many as fit, the rest on the next near day; its linked pharmacies (followers) go to the day holding
/// most of its doctors (else its other day). What still does not fit is OVERFLOW (the caller's next-week rule).
/// A FAR group never fills a day's leftover minutes: the time stays idle (the caller reports it) and the group goes to a
/// later week — except in the LAST draft week (<c>farFill</c>), where nothing comes after it: there a far group takes the
/// 3B rule (lowest fill, then split) rather than drop out of the period.</para>
/// <para><b>No location.</b> A group without any located visit is placed by the 3B rule (<see cref="AssignByLoad"/>): whole
/// on the lowest-fill day, else split over the emptiest days. It is never tied to a cluster.</para>
/// <para>Holidays / weekends have a budget of 0 and never receive a visit. A day's starting load is what is already fixed
/// on it (an approved / kept visit, a pinned visit). The budget is never exceeded.</para>
/// </summary>
public static class DayBalancer
{
    /// <summary>WP-VP-4E — "near" for clustering and gap filling: at most this many minutes of travel (the route's own
    /// haversine travel model, road factor 1.3 at 40 km/h ≈ 9.8 km straight line) from a day's centre. About twice an
    /// in-district hop between two institutions (≈ 10–12 min); two districts across the city (Kadıköy ↔ Bakırköy
    /// ≈ 28 min) are far.</summary>
    public const double NearTravelMinutes = 25;

    /// <summary>One visit to place. <see cref="CostMinutes"/> = duration + between-visit buffer. <see cref="Lat"/> /
    /// <see cref="Lng"/> its place (null = unknown); <see cref="Follower"/> = a pharmacy linked to the group's institution
    /// (it stays with the institution's doctors when the group is split).</summary>
    public sealed record Visit(int Id, string GroupKey, int CostMinutes, double? Lat = null, double? Lng = null, bool Follower = false);

    /// <summary>One candidate day: its visiting budget, the minutes already fixed on it and — WP-VP-4E — the centre of
    /// what is already pinned on it (null = none; <see cref="CenterWeight"/> its weight in minutes).</summary>
    public sealed record Day(DateOnly Date, int BudgetMinutes, int FixedLoadMinutes = 0,
        double? CenterLat = null, double? CenterLng = null, int CenterWeight = 0);

    /// <summary>The day of every placed visit, and the visits no day of the week can hold.</summary>
    public sealed record Result(IReadOnlyDictionary<int, DateOnly> Assigned, IReadOnlyList<int> Overflow,
        IReadOnlyDictionary<DateOnly, int> LoadMinutes);

    private sealed class Center
    {
        public double Lat;
        public double Lng;
        public double Weight;
    }

    private sealed record Group(string Key, List<Visit> Visits, int Cost, (double Lat, double Lng)? Point);

    /// <summary>WP-VP-4E — the geography-aware assignment (see the type summary).</summary>
    public static Result Assign(
        IReadOnlyList<Day> days, IReadOnlyList<Visit> visits, ITravelModel? travel = null, bool farFill = false)
    {
        travel ??= new HaversineTravelModel(RouteOptimizationDefaults.RoadFactor, RouteOptimizationDefaults.AssumedSpeedKmPerMin);
        var open = days.Where(d => d.BudgetMinutes > 0).OrderBy(d => d.Date).ToList();
        var load = days.ToDictionary(d => d.Date, d => d.FixedLoadMinutes);
        var centers = days.ToDictionary(d => d.Date, d => d.CenterLat is { } la && d.CenterLng is { } ln
            ? new Center { Lat = la, Lng = ln, Weight = Math.Max(1, d.CenterWeight) }
            : null);
        var assigned = new Dictionary<int, DateOnly>();
        var overflow = new List<int>();

        double Travel((double Lat, double Lng) a, (double Lat, double Lng) b)
            => travel.TravelMinutes(new GeoPoint(a.Lat, a.Lng), new GeoPoint(b.Lat, b.Lng));
        double? DistTo(Day d, (double Lat, double Lng) p) => centers[d.Date] is { } c ? Travel((c.Lat, c.Lng), p) : null;
        bool Near(Day d, (double Lat, double Lng) p) => DistTo(d, p) is { } m && m <= NearTravelMinutes;
        bool Fits(Day d, int cost) => load[d.Date] + cost <= d.BudgetMinutes;
        double Fill(Day d) => (double)load[d.Date] / d.BudgetMinutes;

        void Put(Visit v, Day d)
        {
            assigned[v.Id] = d.Date;
            load[d.Date] += v.CostMinutes;
            if (v.Lat is { } la && v.Lng is { } ln)
            {
                var c = centers[d.Date] ??= new Center { Lat = la, Lng = ln, Weight = 0 };
                var w = Math.Max(1, v.CostMinutes);
                c.Lat = ((c.Lat * c.Weight) + (la * w)) / (c.Weight + w);
                c.Lng = ((c.Lng * c.Weight) + (ln * w)) / (c.Weight + w);
                c.Weight += w;
            }
        }

        var groups = Groups(visits);
        var located = groups.Where(g => g.Point is not null).ToList();

        // (a) seeds — the farthest-apart groups, one per centre-less open day.
        var seeds = new List<Group>();
        // The seeds are chosen among the free groups only (a pinned day keeps its own centre but does not reorder them),
        // so adding a pin does not reshuffle the rest of the week.
        var references = new List<(double Lat, double Lng)>();
        var seedSlots = open.Count(d => centers[d.Date] is null);
        var pool = new List<Group>(located);
        while (seeds.Count < seedSlots && pool.Count > 0)
        {
            var next = references.Count == 0
                ? pool[0]
                : pool.Select((g, i) => (g, i, d: references.Min(r => Travel(r, g.Point!.Value))))
                    .OrderByDescending(x => Math.Round(x.d, 6)).ThenBy(x => x.i).First().g; // ties (to 1e-6 min) → the larger group
            seeds.Add(next);
            pool.Remove(next);
            references.Add(next.Point!.Value);
        }

        var done = new HashSet<string>(StringComparer.Ordinal);
        foreach (var seed in seeds)
        {
            var day = open.FirstOrDefault(d => centers[d.Date] is null && Fits(d, seed.Cost));
            if (day is null)
            {
                continue; // placed below like any other group
            }

            seed.Visits.ForEach(v => Put(v, day));
            done.Add(seed.Key);
        }

        foreach (var group in groups.Where(g => !done.Contains(g.Key)))
        {
            if (group.Point is not { } point)
            {
                PlaceByLoad(group, open, load, assigned, overflow);
                continue;
            }

            // (b) whole on the nearest NEAR day that holds it.
            var whole = open.Where(d => Fits(d, group.Cost) && Near(d, point))
                .OrderBy(d => DistTo(d, point)).ThenBy(Fill).ThenBy(d => d.Date)
                .FirstOrDefault();
            if (whole is not null)
            {
                group.Visits.ForEach(v => Put(v, whole));
                continue;
            }

            // (d) GAP FILL — a near day still has minutes left but not for the whole group: as many of its doctors as fit
            // go there (nearest first), the rest together onto ONE near / new-cluster day if one holds them, else spread.
            var nearWithRoom = open
                .Where(d => Near(d, point) && load[d.Date] < d.BudgetMinutes)
                .OrderBy(d => DistTo(d, point)).ThenBy(d => d.Date)
                .ToList();
            if (nearWithRoom.Count > 0)
            {
                Split(group, point, nearWithRoom);
                continue;
            }

            // (b') a new cluster: whole on a day without a centre (lowest fill).
            whole = open.Where(d => Fits(d, group.Cost) && centers[d.Date] is null)
                        .OrderBy(Fill).ThenBy(d => d.Date)
                        .FirstOrDefault()
                    ?? (farFill
                        ? open.Where(d => Fits(d, group.Cost)).OrderBy(Fill).ThenBy(d => d.Date).FirstOrDefault()
                        : null);
            if (whole is not null)
            {
                group.Visits.ForEach(v => Put(v, whole));
                continue;
            }

            // Too large for any one day it may go to: split over the new-cluster days (any day in the last week).
            Split(group, point, open
                .Where(d => load[d.Date] < d.BudgetMinutes && (centers[d.Date] is null || farFill))
                .OrderBy(d => d.Date)
                .ToList());
        }

        // Splits one group: its doctors fill <paramref name="first"/> in order (nearest to the day's centre first), the rest
        // go whole onto one near / new-cluster day that holds them (any day in the last week), else on in order; what is
        // still left is OVERFLOW. Its linked pharmacies follow the day holding most of its doctors (4E — 4c).
        void Split(Group group, (double Lat, double Lng) point, List<Day> first)
        {
            var doctors = group.Visits.Where(v => !v.Follower).ToList();
            var followers = group.Visits.Where(v => v.Follower).ToList();
            if (doctors.Count == 0)
            {
                (doctors, followers) = (followers, new List<Visit>());
            }

            void TakeOn(Day day)
            {
                var c = centers[day.Date];
                foreach (var v in doctors
                             .OrderBy(v => c is not null && v.Lat is { } la && v.Lng is { } ln ? Travel((c.Lat, c.Lng), (la, ln)) : 0)
                             .ToList())
                {
                    if (Fits(day, v.CostMinutes))
                    {
                        Put(v, day);
                        doctors.Remove(v);
                    }
                }
            }

            bool Allowed(Day d) => Near(d, point) || centers[d.Date] is null || farFill;

            if (first.Count > 0)
            {
                TakeOn(first[0]);
            }

            if (doctors.Count > 0)
            {
                var rest = doctors.Sum(v => v.CostMinutes);
                var together = open
                    .Where(d => Fits(d, rest) && Allowed(d))
                    .OrderBy(d => centers[d.Date] is null ? 1 : 0).ThenBy(d => DistTo(d, point) ?? 0).ThenBy(d => d.Date)
                    .FirstOrDefault();
                if (together is not null)
                {
                    doctors.ToList().ForEach(v => { Put(v, together); doctors.Remove(v); });
                }
            }

            foreach (var day in first.Skip(1)
                         .Concat(open.Where(d => !first.Contains(d) && Allowed(d)).OrderBy(d => d.Date)))
            {
                if (doctors.Count == 0)
                {
                    break;
                }

                TakeOn(day);
            }

            overflow.AddRange(doctors.Select(v => v.Id));

            var groupDays = group.Visits.Where(v => assigned.ContainsKey(v.Id))
                .GroupBy(v => assigned[v.Id])
                .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                .Select(g => open.First(d => d.Date == g.Key))
                .ToList();
            foreach (var f in followers)
            {
                var day = groupDays.Concat(open.Where(Allowed)).FirstOrDefault(d => Fits(d, f.CostMinutes));
                if (day is null)
                {
                    overflow.Add(f.Id);
                }
                else
                {
                    Put(f, day);
                }
            }
        }

        return new Result(assigned, overflow, load);
    }

    /// <summary>WP-VP-3B — the load-only rule (no geography): groups largest first, each WHOLE to the working day with the
    /// LOWEST fill ratio that still holds it (ties → the earlier date); a group no day holds is split — the day with the
    /// MOST minutes left takes as many of its visits (in order) as fit, then the next. Used for groups without a location,
    /// and kept as the 3B baseline.</summary>
    public static Result AssignByLoad(IReadOnlyList<Day> days, IReadOnlyList<Visit> visits)
    {
        var open = days.Where(d => d.BudgetMinutes > 0).OrderBy(d => d.Date).ToList();
        var load = days.ToDictionary(d => d.Date, d => d.FixedLoadMinutes);
        var assigned = new Dictionary<int, DateOnly>();
        var overflow = new List<int>();
        foreach (var group in Groups(visits))
        {
            PlaceByLoad(group, open, load, assigned, overflow);
        }

        return new Result(assigned, overflow, load);
    }

    private static List<Group> Groups(IReadOnlyList<Visit> visits)
        => visits
            .GroupBy(v => v.GroupKey, StringComparer.Ordinal)
            .Select(g =>
            {
                var list = g.ToList();
                var placedOnes = list.Where(v => v.Lat is not null && v.Lng is not null).ToList();
                (double, double)? point = null;
                if (placedOnes.Count > 0)
                {
                    var w = placedOnes.Sum(v => (double)Math.Max(1, v.CostMinutes));
                    point = (placedOnes.Sum(v => v.Lat!.Value * Math.Max(1, v.CostMinutes)) / w,
                        placedOnes.Sum(v => v.Lng!.Value * Math.Max(1, v.CostMinutes)) / w);
                }

                return new Group(g.Key, list, list.Sum(v => Math.Max(0, v.CostMinutes)), point);
            })
            .OrderByDescending(g => g.Cost)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

    private static void PlaceByLoad(
        Group group, List<Day> open, Dictionary<DateOnly, int> load, Dictionary<int, DateOnly> assigned, List<int> overflow)
    {
        var whole = open
            .Where(d => load[d.Date] + group.Cost <= d.BudgetMinutes)
            .OrderBy(d => (double)load[d.Date] / d.BudgetMinutes)
            .ThenBy(d => d.Date)
            .FirstOrDefault();
        if (whole is not null)
        {
            foreach (var v in group.Visits)
            {
                assigned[v.Id] = whole.Date;
            }

            load[whole.Date] += group.Cost;
            return;
        }

        // Split: fill the emptiest day first, as many visits (in order) as fit, then the next emptiest.
        var rest = new List<Visit>(group.Visits);
        while (rest.Count > 0)
        {
            var day = open
                .Where(d => rest.Any(v => load[d.Date] + v.CostMinutes <= d.BudgetMinutes))
                .OrderByDescending(d => d.BudgetMinutes - load[d.Date])
                .ThenBy(d => d.Date)
                .FirstOrDefault();
            if (day is null)
            {
                overflow.AddRange(rest.Select(v => v.Id));
                break;
            }

            foreach (var v in rest.ToList())
            {
                if (load[day.Date] + v.CostMinutes <= day.BudgetMinutes)
                {
                    assigned[v.Id] = day.Date;
                    load[day.Date] += v.CostMinutes;
                    rest.Remove(v);
                }
            }
        }
    }
}
