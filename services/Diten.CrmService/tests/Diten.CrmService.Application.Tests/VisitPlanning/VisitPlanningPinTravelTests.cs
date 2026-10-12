using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitPlanning;

/// <summary>
/// WP-VP-4I (4I-BE) — on the PRODUCTION day balancer and engine: a pinned day counts the trip between its pinned cluster
/// and a FAR free cluster (the live case of plan 23b1706a, week 41), and a visit the day's route cannot hold because of
/// the travel says <c>no_near_day</c>; <c>capacity_full</c> only when no day has room for the visit itself.
/// <para>Places (the live institutions): 01 NOLU Konya (37.90, 32.49), 03 NOLU Konya (37.90, 32.46), 018 KLİNİK
/// İstanbul (41.06, 28.99), 06 NOLU TOKİ Şanlıurfa (37.19, 38.78).</para>
/// </summary>
public sealed partial class VisitPlanningTests
{
    private const double Konya01Lat = 37.90, Konya01Lng = 32.49, Konya03Lat = 37.90, Konya03Lng = 32.46;
    private const double IstanbulLat = 41.06, IstanbulLng = 28.99, UrfaLat = 37.19, UrfaLng = 38.78;

    /// <summary>Thursday + Friday open. 01 NOLU (1 = SERHAT, 2, 3) and 03 NOLU (4, 5) in Konya, TOKİ (6) in Şanlıurfa,
    /// 018 KLİNİK (7, 8) in İstanbul. The base layout: Thursday Konya, Friday TOKİ.</summary>
    private static (List<DayBalancer.Day> Days, List<DayBalancer.Visit> Visits) LiveWeek41()
        => (new List<DayBalancer.Day> { new(Thu10, 480), new(Fri11, 480) },
            new List<DayBalancer.Visit>
            {
                new(1, "01NOLU", 30, Konya01Lat, Konya01Lng), new(2, "01NOLU", 30, Konya01Lat, Konya01Lng),
                new(3, "01NOLU", 30, Konya01Lat, Konya01Lng),
                new(4, "03NOLU", 30, Konya03Lat, Konya03Lng), new(5, "03NOLU", 30, Konya03Lat, Konya03Lng),
                new(6, "TOKI", 30, UrfaLat, UrfaLng),
                new(7, "018KLINIK", 30, IstanbulLat, IstanbulLng), new(8, "018KLINIK", 30, IstanbulLat, IstanbulLng)
            });

    // ── 1 · the live case: SERHAT (Konya) pinned to Friday → TOKİ cannot stay with the trip → no_near_day ────────

    [Fact]
    public void A_konya_pin_on_tokis_friday_sends_toki_out_with_no_near_day_and_thursday_keeps_its_doctors()
    {
        var (days, visits) = LiveWeek41();
        var baseline = DayBalancer.Assign(days, visits);
        Assert.All(new[] { 1, 2, 3, 4, 5 }, id => Assert.Equal(Thu10, baseline.Assigned[id]));
        Assert.Equal(Fri11, baseline.Assigned[6]); // the base layout of the live plan

        // "Only this doctor": SERHAT KOKULU to Friday.
        var result = DayBalancer.AssignAroundPins(days, visits, new Dictionary<int, DateOnly> { [1] = Fri11 }, new HashSet<int>());

        Assert.False(result.Assigned.ContainsKey(6)); // Konya → Şanlıurfa does not fit Friday (nor Thursday: Konya too)
        Assert.Contains(6, result.Overflow);
        Assert.Contains(6, result.NoNearDay!); // the days had minutes left: not "the week is full"
        Assert.All(new[] { 2, 3, 4, 5 }, id => Assert.Equal(Thu10, result.Assigned[id])); // Thursday's others stay (4G)
        Assert.All(result.LoadMinutes, kv => Assert.True(kv.Value <= 480));
    }

    [Fact]
    public async Task On_the_engine_the_live_case_shifts_toki_with_no_near_day_and_leaves_the_konya_doctors_in_place()
    {
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        foreach (var d in new[] { Mon7, Tue8, Wed9 })
        {
            env.WorkingDays.Holidays.Add(d);
        }

        env.Session.Selection.SelectedContacts.Clear();
        var (_, konya01) = AddInstitution(env, "01 NOLU", 3, Konya01Lat, Konya01Lng);
        var (_, konya03) = AddInstitution(env, "03 NOLU", 2, Konya03Lat, Konya03Lng);
        var (_, toki) = AddInstitution(env, "06 NOLU TOKİ", 1, UrfaLat, UrfaLng);
        string? DayOf(VisitPlanPreview p, Guid doctor) => p.Scheduled.SingleOrDefault(s => s.ContactId == doctor)?.PlannedDate;

        var before = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;
        Assert.Equal("2026-09-11", DayOf(before, toki[0])); // Thursday Konya, Friday TOKİ
        Assert.Equal("2026-09-10", DayOf(before, konya01[0]));

        env.Session.DayPins.Add(new PlanningDayPin
        {
            WeekStart = "2026-09-07", TargetType = PlannedVisitTargetType.Contact, TargetId = konya01[0],
            ContactId = konya01[0], Date = "2026-09-11", Scope = PlanningDayPinScopes.Visit
        });
        var after = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;

        Assert.Equal("2026-09-11", DayOf(after, konya01[0]));
        foreach (var doctor in konya01.Skip(1).Concat(konya03))
        {
            Assert.Equal(DayOf(before, doctor), DayOf(after, doctor));
        }

        Assert.NotEqual("2026-09-11", DayOf(after, toki[0]));
        var shift = Assert.Single(after.Shifted!, s => s.ContactId == toki[0]);
        Assert.Equal(PlanningShiftReasons.NoNearDay, shift.Reason);
    }

    // ── 2 · a free group NEAR the pinned cluster stays on the day — no trip is added ──────────────────────────

    [Fact]
    public void A_free_group_near_the_pinned_cluster_stays_and_adds_no_trip()
    {
        var (days, visits) = LiveWeek41();

        // SERHAT pinned onto his own Thursday: 01 NOLU's others and 03 NOLU (≈ 2.6 km) are near the pin.
        var result = DayBalancer.AssignAroundPins(days, visits, new Dictionary<int, DateOnly> { [1] = Thu10 }, new HashSet<int>());

        Assert.All(new[] { 2, 3, 4, 5 }, id => Assert.Equal(Thu10, result.Assigned[id]));
        Assert.Equal(5 * 30, result.LoadMinutes[Thu10]); // the visits' minutes only — no inter-cluster trip booked
        Assert.Equal(Fri11, result.Assigned[6]); // TOKİ keeps its own Friday (no pin there)
    }

    // ── 3 · the route: travel overflow → no_near_day; a really full week → capacity_full ─────────────────────

    [Fact]
    public async Task A_visit_the_days_route_cannot_hold_for_the_travel_shifts_with_no_near_day()
    {
        // Week 1: only Monday, a HALF day (09:00-13:00). The rep starts in Konya; the doctor is ~154 km south (~300 min
        // of driving): the visit's 30 minutes fit Monday's budget, the drive does not. Week 2 (full days) holds it.
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        env.WorkingDays.HalfDays.Add(Mon7);
        foreach (var d in new[] { Tue8, Wed9, Thu10, Fri11 })
        {
            env.WorkingDays.Holidays.Add(d);
        }

        env.Session.Selection.SelectedContacts.Clear();
        var (_, far) = AddInstitution(env, "Uzak", 1, Konya01Lat - 1.385, Konya01Lng);

        var preview = (await env.Engine.PreviewAsync(
            env.Session, env.Options(Saturday5Sep) with { StartLat = Konya01Lat, StartLong = Konya01Lng }, default)).Preview!;

        Assert.DoesNotContain(preview.Scheduled, s => s.ContactId == far[0] && s.PlannedDate == "2026-09-07");
        var shift = Assert.Single(preview.Shifted!, s => s.ContactId == far[0]);
        Assert.Equal(PlanningShiftReasons.NoNearDay, shift.Reason); // not half_day / capacity_full: Monday had the minutes
    }

    [Fact]
    public async Task A_really_full_week_still_shifts_with_capacity_full()
    {
        // A whole working week (no holiday) and far more doctors in one place than its days hold.
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        env.Session.Selection.SelectedContacts.Clear();
        AddInstitution(env, "01 NOLU", 120, Konya01Lat, Konya01Lng);

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;

        var shifted = preview.Shifted!.Where(s => s.FromWeek == preview.Shifted!.Min(x => x.FromWeek)).ToList();
        Assert.NotEmpty(shifted);
        Assert.All(shifted, s => Assert.Equal(PlanningShiftReasons.CapacityFull, s.Reason));
    }

    // ── 4 · deterministic ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_pinned_day_trip_rule_gives_the_same_days_twice()
    {
        var (days, visits) = LiveWeek41();
        var pins = new Dictionary<int, DateOnly> { [1] = Fri11 };
        var a = DayBalancer.AssignAroundPins(days, visits, pins, new HashSet<int>());
        var b = DayBalancer.AssignAroundPins(days, visits, pins, new HashSet<int>());

        Assert.Equal(a.Assigned.OrderBy(kv => kv.Key), b.Assigned.OrderBy(kv => kv.Key));
        Assert.Equal(a.Overflow.OrderBy(x => x), b.Overflow.OrderBy(x => x));
        Assert.Equal(a.NoNearDay!.OrderBy(x => x), b.NoNearDay!.OrderBy(x => x));
        Assert.Equal(a.LoadMinutes.OrderBy(kv => kv.Key), b.LoadMinutes.OrderBy(kv => kv.Key));
    }
}
