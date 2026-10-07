namespace Diten.CrmService.Application.Features.VisitPlanning;

/// <summary>
/// WP-VP-3B (C1) — which DAY of a week each visit goes to, before any route is drawn. Pure and deterministic.
/// <para><b>Rule.</b> The week's visits are first gathered into institution GROUPS (the same account and the pharmacies
/// linked to it). Groups are taken largest first (total minutes, ties by key) and each goes WHOLE to the working day with
/// the LOWEST fill ratio (load ÷ budget; ties → the earlier date) that still has room for the whole group — so one
/// institution's doctors stay on one day and the week fills evenly instead of "everything on Monday".</para>
/// <para><b>Split rule</b> (a group that fits no single day): its visits, in their given order, fill the day with the MOST
/// minutes left, as many as fit; the rest go on to the next-emptiest day, and so on. The group therefore spreads over as few
/// days as possible. A visit that fits nowhere is OVERFLOW — the caller moves it to a later week.</para>
/// <para>Holidays / weekends have a budget of 0 and never receive a visit. A day's starting load is what is already fixed
/// on it (an approved / kept visit).</para>
/// </summary>
public static class DayBalancer
{
    /// <summary>One visit to place. <see cref="CostMinutes"/> = duration + between-visit buffer.</summary>
    public sealed record Visit(int Id, string GroupKey, int CostMinutes);

    /// <summary>One candidate day: its visiting budget and the minutes already fixed on it.</summary>
    public sealed record Day(DateOnly Date, int BudgetMinutes, int FixedLoadMinutes = 0);

    /// <summary>The day of every placed visit, and the visits no day of the week can hold.</summary>
    public sealed record Result(IReadOnlyDictionary<int, DateOnly> Assigned, IReadOnlyList<int> Overflow,
        IReadOnlyDictionary<DateOnly, int> LoadMinutes);

    public static Result Assign(IReadOnlyList<Day> days, IReadOnlyList<Visit> visits)
    {
        var open = days.Where(d => d.BudgetMinutes > 0).OrderBy(d => d.Date).ToList();
        var load = days.ToDictionary(d => d.Date, d => d.FixedLoadMinutes);
        var assigned = new Dictionary<int, DateOnly>();
        var overflow = new List<int>();

        var groups = visits
            .GroupBy(v => v.GroupKey, StringComparer.Ordinal)
            .Select(g => (Key: g.Key, Visits: g.ToList(), Cost: g.Sum(v => Math.Max(0, v.CostMinutes))))
            .OrderByDescending(g => g.Cost)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        foreach (var group in groups)
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
                continue;
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

        return new Result(assigned, overflow, load);
    }
}
