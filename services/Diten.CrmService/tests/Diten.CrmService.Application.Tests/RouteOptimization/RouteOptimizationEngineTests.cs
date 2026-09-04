using System.Net.Http;
using System.Reflection;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.RouteOptimization;
using Diten.CrmService.Application.Features.RouteOptimization.Handlers;
using Diten.CrmService.Application.Features.RouteOptimization.Queries;
using Xunit;

namespace Diten.CrmService.Application.Tests.RouteOptimization;

/// <summary>
/// MOD-0155 FU03 — Visit Route Optimization. Pins down: the pure greedy time-window insertion heuristic (order, slot,
/// cross-day continuity, lunch avoidance, between-visit + travel spacing), availability windows as a HARD constraint,
/// the unscheduled list as a supply-vs-demand WARNING (never a throw), deterministic tie-break ending on visitId,
/// in-house haversine travel (no HttpClient), the day-1 seed (startLocation | centroid), engine purity, and the dry-run
/// preview handler (200 output == seam output, persists nothing, over-supply → 200, malformed → 400).
/// </summary>
public sealed class RouteOptimizationEngineTests
{
    private static readonly double Speed = RouteOptimizationDefaults.AssumedSpeedKmPerMin;
    private static ITravelModel Travel() => new HaversineTravelModel(RouteOptimizationDefaults.RoadFactor, Speed);

    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    private static string Weekday(DateOnly d) => d.DayOfWeek switch
    {
        DayOfWeek.Monday => "monday",
        DayOfWeek.Tuesday => "tuesday",
        DayOfWeek.Wednesday => "wednesday",
        DayOfWeek.Thursday => "thursday",
        DayOfWeek.Friday => "friday",
        DayOfWeek.Saturday => "saturday",
        _ => "sunday"
    };

    private static WorkingDayHours Day(string start, string end, string lunchStart = "23:00", string lunchEnd = "23:00")
        => new(start, end, lunchStart, lunchEnd);

    private static readonly WorkingDayHours DefaultDay = RouteOptimizationDefaults.WorkingDay;

    private static RouteVisitInput Visit(
        int n, double lat, double lon, int duration = 30, IReadOnlyList<AvailabilityWindow>? windows = null)
        => new(Id(n), lat, lon, duration, windows);

    private static OptimizationPeriod OneDay(DateOnly d) => new(d, d);

    private static readonly DateOnly Anchor = new(2026, 6, 1); // a Monday

    private sealed class TestDefaults : IRouteOptimizationDefaultsProvider
    {
        public RouteOptimizationDefaultsSet Current { get; }
        public TestDefaults(RouteOptimizationDefaultsSet? set = null) => Current = set ?? RouteOptimizationDefaults.Set;
    }

    // ---------------------------------------------------------------- Cluster 1 — travel model
    [Fact]
    public void Haversine_one_degree_of_longitude_at_equator_is_about_111km()
    {
        var km = HaversineTravelModel.HaversineKm(new GeoPoint(0, 0), new GeoPoint(0, 1));
        Assert.InRange(km, 110.0, 112.0);
    }

    [Fact]
    public void Travel_minutes_scale_linearly_with_road_factor()
    {
        var a = new GeoPoint(0, 0);
        var b = new GeoPoint(0, 0.5);
        var single = new HaversineTravelModel(1.3, Speed).TravelMinutes(a, b);
        var doubled = new HaversineTravelModel(2.6, Speed).TravelMinutes(a, b);
        Assert.Equal(single * 2, doubled, 6);
    }

    [Fact]
    public void Travel_is_symmetric()
    {
        var model = Travel();
        var a = new GeoPoint(41.0, 29.0);
        var b = new GeoPoint(40.9, 29.2);
        Assert.Equal(model.TravelMinutes(a, b), model.TravelMinutes(b, a), 9);
    }

    [Fact]
    public void Nonsensical_travel_config_falls_back_to_documented_defaults()
    {
        var model = new HaversineTravelModel(0, -1);
        Assert.Equal(RouteOptimizationDefaults.RoadFactor, model.RoadFactor);
        Assert.Equal(RouteOptimizationDefaults.AssumedSpeedKmPerMin, model.AssumedSpeedKmPerMin);
    }

    [Fact]
    public void Feature_references_no_HttpClient_anywhere()
    {
        foreach (var type in FeatureTypes())
        {
            foreach (var ctor in type.GetConstructors())
            {
                Assert.DoesNotContain(ctor.GetParameters(), p => p.ParameterType == typeof(HttpClient));
            }

            Assert.DoesNotContain(
                type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
                f => f.FieldType == typeof(HttpClient));
        }
    }

    // ---------------------------------------------------------------- Cluster 2 — single-day slotting
    [Fact]
    public void Two_visits_same_day_are_ordered_non_overlapping_and_spaced_by_between_plus_travel()
    {
        var visits = new[] { Visit(1, 0, 0, 60), Visit(2, 0, 0, 60) };
        var result = TimeWindowInsertionEngine.Schedule(
            visits, DefaultDay, new GeoPoint(0, 0), OneDay(Anchor), betweenVisitMinutes: 15, Travel());

        Assert.Equal(2, result.Scheduled.Count);
        var first = result.Scheduled[0];
        var second = result.Scheduled[1];
        Assert.Equal("09:00", first.StartTime);
        Assert.Equal("10:00", first.EndTime);                       // end = start + duration
        Assert.Equal("10:15", second.StartTime);                    // 10:00 + 15 buffer + 0 travel
        Assert.Equal(1, first.SequenceOrder);
        Assert.Equal(2, second.SequenceOrder);
        Assert.True(string.CompareOrdinal(second.StartTime, first.EndTime) >= 0); // non-overlap
    }

    [Fact]
    public void A_visit_that_would_straddle_lunch_is_pushed_after_it()
    {
        var window = new[] { new AvailabilityWindow(Weekday(Anchor), "12:30", "18:00") };
        var visits = new[] { Visit(1, 0, 0, 40, window) };
        var result = TimeWindowInsertionEngine.Schedule(
            visits, DefaultDay, new GeoPoint(0, 0), OneDay(Anchor), 0, Travel());

        var placed = Assert.Single(result.Scheduled);
        Assert.Equal("14:00", placed.StartTime); // 12:30–13:00 (30m) too short for 40m → after lunch
        Assert.Equal("14:40", placed.EndTime);
    }

    // ---------------------------------------------------------------- Cluster 3 — cross-day continuity
    [Fact]
    public void Work_spills_to_the_next_day_when_a_day_is_full()
    {
        var visits = new[] { Visit(1, 0, 0, 30), Visit(2, 0, 0, 30), Visit(3, 0, 0, 30) };
        var period = new OptimizationPeriod(Anchor, Anchor.AddDays(1));
        var result = TimeWindowInsertionEngine.Schedule(
            visits, Day("09:00", "10:00"), new GeoPoint(0, 0), period, 0, Travel());

        Assert.Equal(3, result.Scheduled.Count);
        Assert.Equal(2, result.Scheduled.Count(s => s.AssignedDate == Anchor));
        Assert.Equal(1, result.Scheduled.Count(s => s.AssignedDate == Anchor.AddDays(1)));
    }

    [Fact]
    public void Next_day_seeds_from_the_previous_days_last_location_choosing_the_nearer_visit()
    {
        // Day 1 (09:00–10:00, 25-min visits) fills with the origin cluster; day 2 then chooses P (near the day-1 end)
        // over the far Q — proving the seed rolled over rather than restarting from scratch.
        var f1 = Visit(1, 0, 0.000, 25);
        var f2 = Visit(2, 0, 0.001, 25);
        var p = Visit(3, 0, 0.002, 25);   // near the day-1 end
        var q = Visit(4, 0, 0.500, 25);   // ~55 km away
        var period = new OptimizationPeriod(Anchor, Anchor.AddDays(1));

        var result = TimeWindowInsertionEngine.Schedule(
            new[] { f1, f2, p, q }, Day("09:00", "10:00"), new GeoPoint(0, 0), period, 0, Travel());

        var day1 = result.Scheduled.Where(s => s.AssignedDate == Anchor).Select(s => s.VisitId).ToList();
        var day2 = result.Scheduled.Where(s => s.AssignedDate == Anchor.AddDays(1)).OrderBy(s => s.SequenceOrder).ToList();

        Assert.Equal(new[] { Id(1), Id(2) }, day1);
        Assert.Equal(Id(3), day2[0].VisitId);                          // P chosen from the rolled seed
        Assert.Contains(result.Unscheduled, u => u.VisitId == Id(4));  // far Q never reached
    }

    [Fact]
    public void Travel_to_next_is_zero_for_the_last_visit_of_a_day()
    {
        var visits = new[] { Visit(1, 0, 0, 30), Visit(2, 0, 0.05, 30) };
        var result = TimeWindowInsertionEngine.Schedule(
            visits, DefaultDay, new GeoPoint(0, 0), OneDay(Anchor), 0, Travel());

        Assert.Equal(2, result.Scheduled.Count);
        Assert.True(result.Scheduled[0].TravelToNextMinutes > 0);
        Assert.Equal(0, result.Scheduled[1].TravelToNextMinutes);
    }

    // ---------------------------------------------------------------- Cluster 4 — availability HARD
    [Fact]
    public void A_visit_is_placed_strictly_inside_its_availability_window()
    {
        var window = new[] { new AvailabilityWindow(Weekday(Anchor), "10:00", "12:00") };
        var visits = new[] { Visit(1, 0, 0, 60, window) };
        var result = TimeWindowInsertionEngine.Schedule(
            visits, DefaultDay, new GeoPoint(0, 0), OneDay(Anchor), 0, Travel());

        var placed = Assert.Single(result.Scheduled);
        Assert.True(string.CompareOrdinal(placed.StartTime, "10:00") >= 0);
        Assert.True(string.CompareOrdinal(placed.EndTime, "12:00") <= 0);
    }

    [Fact]
    public void A_visit_whose_window_is_too_small_goes_unscheduled_not_forced()
    {
        var window = new[] { new AvailabilityWindow(Weekday(Anchor), "10:00", "10:30") };
        var visits = new[] { Visit(1, 0, 0, 60, window) };
        var result = TimeWindowInsertionEngine.Schedule(
            visits, DefaultDay, new GeoPoint(0, 0), OneDay(Anchor), 0, Travel());

        Assert.Empty(result.Scheduled);
        var un = Assert.Single(result.Unscheduled);
        Assert.Equal(RouteUnscheduledReasonCodes.NoFeasibleAvailabilityWindow, un.Reason);
    }

    [Fact]
    public void A_window_on_a_weekday_absent_from_the_period_is_never_feasible()
    {
        var tuesday = Weekday(Anchor.AddDays(1));
        var window = new[] { new AvailabilityWindow(tuesday, "10:00", "16:00") };
        var visits = new[] { Visit(1, 0, 0, 60, window) };
        var result = TimeWindowInsertionEngine.Schedule(
            visits, DefaultDay, new GeoPoint(0, 0), OneDay(Anchor), 0, Travel()); // period = Monday only

        Assert.Empty(result.Scheduled);
        Assert.Equal(RouteUnscheduledReasonCodes.NoFeasibleAvailabilityWindow, Assert.Single(result.Unscheduled).Reason);
    }

    // ---------------------------------------------------------------- Cluster 5 — unscheduled = warning
    [Fact]
    public void Over_supply_returns_the_remainder_scheduled_and_the_rest_as_a_warning_never_throws()
    {
        var visits = new[] { Visit(1, 0, 0, 30), Visit(2, 0, 0, 30), Visit(3, 0, 0, 30) };
        var result = TimeWindowInsertionEngine.Schedule(
            visits, Day("09:00", "09:40"), new GeoPoint(0, 0), OneDay(Anchor), 0, Travel());

        Assert.Single(result.Scheduled);
        Assert.Equal(2, result.Unscheduled.Count);
        Assert.All(result.Unscheduled, u => Assert.Equal(RouteUnscheduledReasonCodes.PeriodExhausted, u.Reason));
    }

    [Fact]
    public void Missing_or_invalid_coordinates_go_to_unscheduled_missing_location()
    {
        var visits = new[]
        {
            new RouteVisitInput(Id(1), double.NaN, 0, 30),
            new RouteVisitInput(Id(2), 200, 0, 30)
        };
        var result = TimeWindowInsertionEngine.Schedule(
            visits, DefaultDay, new GeoPoint(0, 0), OneDay(Anchor), 0, Travel());

        Assert.Empty(result.Scheduled);
        Assert.All(result.Unscheduled, u => Assert.Equal(RouteUnscheduledReasonCodes.MissingLocation, u.Reason));
    }

    [Fact]
    public void A_visit_longer_than_the_working_day_goes_to_unscheduled_duration_exceeds_working_day()
    {
        var visits = new[] { Visit(1, 0, 0, 600) }; // > 240-min max contiguous segment of the default day
        var result = TimeWindowInsertionEngine.Schedule(
            visits, DefaultDay, new GeoPoint(0, 0), OneDay(Anchor), 0, Travel());

        Assert.Empty(result.Scheduled);
        Assert.Equal(RouteUnscheduledReasonCodes.DurationExceedsWorkingDay, Assert.Single(result.Unscheduled).Reason);
    }

    [Fact]
    public void A_non_positive_duration_is_invalid_input_and_never_throws()
    {
        var visits = new[] { new RouteVisitInput(Id(1), 0, 0, 0), new RouteVisitInput(Id(2), 0, 0, -5) };
        var result = TimeWindowInsertionEngine.Schedule(
            visits, DefaultDay, new GeoPoint(0, 0), OneDay(Anchor), 0, Travel());

        Assert.All(result.Unscheduled, u => Assert.Equal(RouteUnscheduledReasonCodes.InvalidInput, u.Reason));
    }

    [Fact]
    public void Every_unscheduled_reason_is_a_known_code()
    {
        var visits = new[]
        {
            new RouteVisitInput(Id(1), double.NaN, 0, 30),   // missing_location
            new RouteVisitInput(Id(2), 0, 0, 0),             // invalid_input
            Visit(3, 0, 0, 600),                             // duration_exceeds_working_day
            Visit(4, 0, 0, 30, new[] { new AvailabilityWindow("sunday", "10:00", "10:10") }) // no window
        };
        var result = TimeWindowInsertionEngine.Schedule(
            visits, DefaultDay, new GeoPoint(0, 0), OneDay(Anchor), 0, Travel());

        Assert.All(result.Unscheduled, u => Assert.Contains(u.Reason, RouteUnscheduledReasonCodes.All));
    }

    [Fact]
    public void Empty_visit_set_returns_empty_lists_not_an_error()
    {
        var result = TimeWindowInsertionEngine.Schedule(
            Array.Empty<RouteVisitInput>(), DefaultDay, new GeoPoint(0, 0), OneDay(Anchor), 0, Travel());

        Assert.Empty(result.Scheduled);
        Assert.Empty(result.Unscheduled);
    }

    // ---------------------------------------------------------------- Cluster 6 — purity / determinism
    [Fact]
    public void Identical_input_produces_byte_identical_output()
    {
        var visits = new[] { Visit(3, 0, 0.02, 40), Visit(1, 0, 0.00, 40), Visit(2, 0, 0.01, 40) };
        var period = new OptimizationPeriod(Anchor, Anchor.AddDays(2));

        var a = TimeWindowInsertionEngine.Schedule(visits, DefaultDay, null, period, 5, Travel());
        var b = TimeWindowInsertionEngine.Schedule(visits, DefaultDay, null, period, 5, Travel());

        Assert.True(a.Scheduled.SequenceEqual(b.Scheduled));
        Assert.True(a.Unscheduled.SequenceEqual(b.Unscheduled));
    }

    [Fact]
    public void The_engine_does_not_mutate_the_input_list()
    {
        var visits = new List<RouteVisitInput> { Visit(1, 0, 0, 30), Visit(2, 0, 0, 30) };
        var snapshot = visits.ToArray();
        TimeWindowInsertionEngine.Schedule(visits, DefaultDay, new GeoPoint(0, 0), OneDay(Anchor), 0, Travel());

        Assert.Equal(2, visits.Count);
        Assert.True(visits.SequenceEqual(snapshot));
    }

    [Fact]
    public void Tie_break_of_equal_travel_and_window_resolves_by_lowest_visit_id()
    {
        // Two co-located visits with no windows: equal travel from the seed, equal (pseudo) window start → visitId wins.
        var visits = new[] { Visit(2, 0, 0, 30), Visit(1, 0, 0, 30) };
        var result = TimeWindowInsertionEngine.Schedule(
            visits, DefaultDay, new GeoPoint(0, 0), OneDay(Anchor), 0, Travel());

        Assert.Equal(Id(1), result.Scheduled[0].VisitId);
        Assert.Equal(Id(2), result.Scheduled[1].VisitId);
    }

    [Fact]
    public void The_optimizer_injects_only_the_defaults_provider_no_repository()
    {
        var ctor = Assert.Single(typeof(GreedyTimeWindowRouteOptimizer).GetConstructors());
        var param = Assert.Single(ctor.GetParameters());
        Assert.Equal(typeof(IRouteOptimizationDefaultsProvider), param.ParameterType);
    }

    // ---------------------------------------------------------------- Cluster 7 — day-seed
    [Fact]
    public void With_start_location_day_one_begins_with_the_nearest_feasible_visit_to_it()
    {
        var visits = new[] { Visit(1, 0, 0.05, 20), Visit(2, 0, 0.01, 20), Visit(3, 0, 0.03, 20) };
        var result = TimeWindowInsertionEngine.Schedule(
            visits, DefaultDay, new GeoPoint(0, 0), OneDay(Anchor), 0, Travel());

        Assert.Equal(Id(2), result.Scheduled[0].VisitId); // (0,0.01) is nearest to the start location
    }

    [Fact]
    public void Without_start_location_day_one_seeds_from_the_visit_nearest_the_centroid()
    {
        var visits = new[] { Visit(1, 0, 0.00, 20), Visit(2, 0, 0.02, 20), Visit(3, 0, 0.10, 20) };
        // centroid lon = 0.04 → nearest visit is (0,0.02) = Id(2)
        var result = TimeWindowInsertionEngine.Schedule(
            visits, DefaultDay, null, OneDay(Anchor), 0, Travel());

        Assert.Equal(Id(2), result.Scheduled[0].VisitId);
    }

    // ---------------------------------------------------------------- Cluster 8 — boundary
    [Fact]
    public void Feature_declares_no_duration_selection_or_frequency_symbol()
    {
        var banned = new[] { "ComputeDuration", "SelectTargets", "ExpandFrequency", "GeneratePlans" };
        foreach (var type in FeatureTypes())
        {
            foreach (var b in banned)
            {
                Assert.DoesNotContain(b, type.Name);
                Assert.DoesNotContain(type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly), m => m.Name == b);
            }
        }
    }

    [Fact]
    public void The_seam_is_swappable_a_stub_optimizer_flows_through_the_handler()
    {
        var stub = new StubOptimizer();
        var handler = new PreviewRouteOptimizationHandler(stub);
        var response = handler.Handle(new PreviewRouteOptimizationQuery(MinimalInput()), default).Result;

        Assert.Equal(200, response.StatusCode);
        Assert.Same(stub.Fixed, response.Data);
    }

    // ---------------------------------------------------------------- Cluster 9 — dry-run preview handler
    [Fact]
    public void Handler_returns_200_with_the_same_output_the_seam_returns_for_the_same_input()
    {
        var optimizer = new GreedyTimeWindowRouteOptimizer(new TestDefaults());
        var input = new RouteOptimizationInput(
            new[] { Visit(1, 0, 0, 30), Visit(2, 0, 0.01, 30) },
            new RepWorkingHours(StartLocation: new GeoPoint(0, 0)),
            OneDay(Anchor), 5, new TravelModelSpec());

        var expected = optimizer.Optimize(input);
        var handler = new PreviewRouteOptimizationHandler(optimizer);
        var response = handler.Handle(new PreviewRouteOptimizationQuery(input), default).Result;

        Assert.Equal(200, response.StatusCode);
        Assert.True(response.Data!.Scheduled.SequenceEqual(expected.Scheduled));
        Assert.True(response.Data!.Unscheduled.SequenceEqual(expected.Unscheduled));
    }

    [Fact]
    public void Handler_injects_only_the_optimizer_seam_so_it_cannot_persist()
    {
        var ctor = Assert.Single(typeof(PreviewRouteOptimizationHandler).GetConstructors());
        var param = Assert.Single(ctor.GetParameters());
        Assert.Equal(typeof(IRouteOptimizer), param.ParameterType);
    }

    [Fact]
    public void Handler_returns_200_with_unscheduled_populated_for_an_over_supply_input()
    {
        var optimizer = new GreedyTimeWindowRouteOptimizer(new TestDefaults());
        var input = new RouteOptimizationInput(
            new[] { Visit(1, 0, 0, 30), Visit(2, 0, 0, 30), Visit(3, 0, 0, 30) },
            new RepWorkingHours(new WorkingDayHours("09:00", "09:40", "23:00", "23:00"), new GeoPoint(0, 0)),
            OneDay(Anchor), 0, new TravelModelSpec());

        var response = new PreviewRouteOptimizationHandler(optimizer)
            .Handle(new PreviewRouteOptimizationQuery(input), default).Result;

        Assert.Equal(200, response.StatusCode);
        Assert.NotEmpty(response.Data!.Unscheduled);
    }

    [Theory]
    [InlineData(300)]  // between-visit buffer out of range
    [InlineData(-1)]
    public void Handler_rejects_an_out_of_range_between_visit_buffer_with_400(int between)
    {
        var input = new RouteOptimizationInput(
            Array.Empty<RouteVisitInput>(), new RepWorkingHours(), OneDay(Anchor), between, new TravelModelSpec());
        var response = new PreviewRouteOptimizationHandler(new GreedyTimeWindowRouteOptimizer(new TestDefaults()))
            .Handle(new PreviewRouteOptimizationQuery(input), default).Result;

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public void Handler_rejects_a_reversed_period_with_400()
    {
        var input = new RouteOptimizationInput(
            Array.Empty<RouteVisitInput>(), new RepWorkingHours(),
            new OptimizationPeriod(Anchor.AddDays(2), Anchor), 0, new TravelModelSpec());
        var response = new PreviewRouteOptimizationHandler(new GreedyTimeWindowRouteOptimizer(new TestDefaults()))
            .Handle(new PreviewRouteOptimizationQuery(input), default).Result;

        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public void Handler_rejects_an_unsupported_travel_model_kind_with_400()
    {
        var input = new RouteOptimizationInput(
            Array.Empty<RouteVisitInput>(), new RepWorkingHours(), OneDay(Anchor), 0,
            new TravelModelSpec("google-maps"));
        var response = new PreviewRouteOptimizationHandler(new GreedyTimeWindowRouteOptimizer(new TestDefaults()))
            .Handle(new PreviewRouteOptimizationQuery(input), default).Result;

        Assert.Equal(400, response.StatusCode);
    }

    // ---------------------------------------------------------------- helpers
    private static RouteOptimizationInput MinimalInput()
        => new(Array.Empty<RouteVisitInput>(), new RepWorkingHours(), OneDay(Anchor), 0, new TravelModelSpec());

    private static IEnumerable<Type> FeatureTypes()
        => typeof(IRouteOptimizer).Assembly.GetTypes()
            .Where(t => t.Namespace is { } ns
                        && ns.StartsWith("Diten.CrmService.Application.Features.RouteOptimization", StringComparison.Ordinal));

    private sealed class StubOptimizer : IRouteOptimizer
    {
        public RouteOptimizationOutput Fixed { get; } =
            new(Array.Empty<ScheduledVisit>(), Array.Empty<UnscheduledVisit>());

        public RouteOptimizationOutput Optimize(RouteOptimizationInput input) => Fixed;
    }
}
