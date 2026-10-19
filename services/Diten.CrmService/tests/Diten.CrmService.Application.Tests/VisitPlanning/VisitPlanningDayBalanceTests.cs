using Diten.CrmService.Application.Features.VisitPlanning;
using Xunit;
using Xunit.Abstractions;

namespace Diten.CrmService.Application.Tests.VisitPlanning;

/// <summary>
/// W2-BE-c (C5) — the day-balance pass of the PRODUCTION <see cref="DayBalancer"/>: one institution with 20 doctors
/// (400 min of a 480-min day) and 15 doctors spread over the city. Before W2-BE-c the institution kept one day full while
/// the other days took a doctor or two (live: Mon 29 visits / 425 min, Tue 3 / 43 min). Now no day exceeds its budget,
/// the fullest minus the emptiest day stays ≤ 50 % of the budget, the institution spans at most two days, the result is
/// stable, and a group under 60 % of the budget is never split.
/// </summary>
public sealed class VisitPlanningDayBalanceTests
{
    private const int Budget = 480;
    private static readonly DateOnly Monday = new(2026, 10, 19);
    private readonly ITestOutputHelper _out;

    public VisitPlanningDayBalanceTests(ITestOutputHelper output) => _out = output;

    private static List<DayBalancer.Day> Week() => Enumerable.Range(0, 5).Select(i => new DayBalancer.Day(Monday.AddDays(i), Budget)).ToList();

    /// <summary>20 doctors of ONE institution (20 min each, a few metres apart) + 15 lone doctors around the city.</summary>
    private static List<DayBalancer.Visit> Scenario()
    {
        var visits = new List<DayBalancer.Visit>();
        for (var i = 0; i < 20; i++)
        {
            visits.Add(new DayBalancer.Visit(i + 1, "INSTITUTION", 20, 41.0100 + (i * 0.0002), 28.9800 + (i % 4) * 0.0002));
        }

        // 15 lone doctors on a ring ~ 6–14 km from the institution (some near each other, all far from it)
        for (var i = 0; i < 15; i++)
        {
            var angle = i * (2 * Math.PI / 15);
            var radius = 0.06 + (i % 3) * 0.03;
            visits.Add(new DayBalancer.Visit(100 + i, "LONE-" + i.ToString("00"), 20,
                41.0100 + (radius * Math.Sin(angle)), 28.9800 + (radius * Math.Cos(angle))));
        }

        return visits;
    }

    [Fact]
    public void C5_one_institution_of_20_and_15_scattered_doctors_balance_over_the_week()
    {
        var result = DayBalancer.Assign(Week(), Scenario());
        var load = Enumerable.Range(0, 5).Select(i => result.LoadMinutes[Monday.AddDays(i)]).ToList();
        var counts = Enumerable.Range(0, 5).Select(i => result.Assigned.Count(a => a.Value == Monday.AddDays(i))).ToList();
        _out.WriteLine("minutes: " + string.Join(" / ", load) + " · visits: " + string.Join(" / ", counts));

        Assert.Empty(result.Overflow);
        Assert.All(load, l => Assert.True(l <= Budget, $"a day over budget: {l}"));
        Assert.True(load.Max() - load.Min() <= Budget * DayBalancer.BalanceGapShare, $"gap {load.Max() - load.Min()} min");
        var institutionDays = result.Assigned.Where(a => a.Key <= 20).Select(a => a.Value).Distinct().Count();
        Assert.InRange(institutionDays, 1, 2);
        Assert.Equal(2, institutionDays); // 400 min > 60 % of 480 and the week was unbalanced: it IS split

        // stable: the same input gives the same days
        var again = DayBalancer.Assign(Week(), Scenario());
        Assert.Equal(result.Assigned.OrderBy(a => a.Key), again.Assigned.OrderBy(a => a.Key));
    }

    [Fact]
    public void C5_the_split_keeps_one_geographic_side_together_and_never_touches_a_group_under_60_percent()
    {
        var result = DayBalancer.Assign(Week(), Scenario());
        var institution = result.Assigned.Where(a => a.Key <= 20).GroupBy(a => a.Value).ToList();
        // the moved part is one side of the institution's longer (latitude) axis: the moved ids are the highest ones
        var kept = institution.Single(g => g.Any(a => a.Key == 1)).Select(a => a.Key).ToList();
        var moved = institution.Single(g => g.All(a => a.Key != 1)).Select(a => a.Key).ToList();
        Assert.True(moved.Min() > kept.Max(), "one geographic side moves together");

        // a 280-min group (58 %) on a crowded week is NOT split
        var small = Enumerable.Range(1, 14).Select(i => new DayBalancer.Visit(i, "SMALL", 20, 41.01 + i * 0.0002, 28.98)).ToList();
        var lone = new DayBalancer.Visit(99, "LONE", 20, 41.20, 29.20);
        var r2 = DayBalancer.Assign(Week(), small.Append(lone).ToList());
        Assert.Single(r2.Assigned.Where(a => a.Key <= 14).Select(a => a.Value).Distinct());
    }

    [Fact]
    public void C5_fixed_load_is_never_moved_and_counts_in_the_balance()
    {
        // Monday already carries 300 min of pinned / approved visits (fixed): only free visits move.
        var days = Week().Select(d => d.Date == Monday ? d with { FixedLoadMinutes = 300 } : d).ToList();
        var result = DayBalancer.Assign(days, Scenario());
        Assert.All(result.LoadMinutes.Values, l => Assert.True(l <= Budget));
        Assert.True(result.LoadMinutes[Monday] >= 300);
        Assert.InRange(result.Assigned.Where(a => a.Key <= 20).Select(a => a.Value).Distinct().Count(), 1, 2);
    }
}
