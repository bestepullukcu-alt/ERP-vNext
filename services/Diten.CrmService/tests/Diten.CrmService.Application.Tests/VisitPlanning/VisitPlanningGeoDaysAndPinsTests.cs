using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.RouteOptimization;
using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Application.Features.VisitPlanning.Commands;
using Diten.CrmService.Application.Features.VisitPlanning.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.VisitPlanning.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitPlanning.Queries;
using Diten.CrmService.Application.Tests.VisitScope;
using Diten.CrmService.Domain.Entities;
using Xunit;
using AccountEntity = Diten.CrmService.Domain.Entities.Account;
using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;

namespace Diten.CrmService.Application.Tests.VisitPlanning;

/// <summary>
/// WP-VP-4E — geography-aware day assignment and the rep's day pins on the PRODUCTION engine, day balancer, update and
/// read handlers (only stores / calendar / clock are fakes; the route is the production optimizer where the order and the
/// minutes matter). "Today" is Saturday 5 Sep, so the first draft week is 7 Sep (Mon) … 11 Sep (Fri); no capacity ⇒ a day
/// holds 480 minutes and a visit is 30.
/// <para>Places: Kadıköy ≈ (40.990, 29.030) and Bakırköy ≈ (40.980, 28.860) — ≈ 28 minutes apart by the route's travel
/// model, beyond <see cref="DayBalancer.NearTravelMinutes"/>; points inside one district are a few minutes apart.</para>
/// </summary>
public sealed partial class VisitPlanningTests
{
    private const double KadikoyLat = 40.990, KadikoyLng = 29.030, BakirkoyLat = 40.980, BakirkoyLng = 28.860;
    private static readonly DateOnly Mon7 = new(2026, 9, 7), Tue8 = new(2026, 9, 8), Wed9 = new(2026, 9, 9),
        Thu10 = new(2026, 9, 10), Fri11 = new(2026, 9, 11);

    // ── 1 · two far clusters → two days; the week travels less than by load ─────────────────────────────────────────

    [Fact]
    public async Task Two_far_clusters_get_one_day_each()
    {
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        foreach (var d in new[] { Wed9, Thu10, Fri11 })
        {
            env.WorkingDays.Holidays.Add(d); // a two-day week: Monday + Tuesday
        }

        env.Session.Selection.SelectedContacts.Clear();
        var kadikoy = Enumerable.Range(0, 3).Select(i => AddInstitution(env, "Kadıköy " + i, 2, KadikoyLat + i * 0.004, KadikoyLng + i * 0.004).Account).ToHashSet();
        var bakirkoy = Enumerable.Range(0, 3).Select(i => AddInstitution(env, "Bakırköy " + i, 2, BakirkoyLat + i * 0.004, BakirkoyLng + i * 0.004).Account).ToHashSet();

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;

        var slots = preview.Scheduled.Where(s => !s.IsFixed).ToList();
        Assert.Equal(12, slots.Count);
        var kadikoyDays = slots.Where(s => kadikoy.Contains(s.AccountId!.Value)).Select(s => s.PlannedDate).Distinct().ToList();
        var bakirkoyDays = slots.Where(s => bakirkoy.Contains(s.AccountId!.Value)).Select(s => s.PlannedDate).Distinct().ToList();
        Assert.Single(kadikoyDays);
        Assert.Single(bakirkoyDays);
        Assert.NotEqual(kadikoyDays[0], bakirkoyDays[0]);
    }

    [Fact]
    public void Clustering_travels_less_than_the_load_only_rule_on_the_same_week_and_keeps_the_load_bound()
    {
        var days = new[] { Mon7, Tue8 }.Select(d => new DayBalancer.Day(d, 480)).ToList();
        var visits = new List<DayBalancer.Visit>();
        var place = new Dictionary<int, (double Lat, double Lng)>();
        var id = 0;
        for (var i = 0; i < 3; i++)
        {
            foreach (var (lat, lng, name) in new[] { (KadikoyLat, KadikoyLng, "k"), (BakirkoyLat, BakirkoyLng, "b") })
            {
                for (var k = 0; k < 2; k++)
                {
                    place[id] = (lat + i * 0.004, lng + i * 0.004);
                    visits.Add(new DayBalancer.Visit(id++, name + i, 30, lat + i * 0.004, lng + i * 0.004));
                }
            }
        }

        var geo = DayBalancer.Assign(days, visits);
        var byLoad = DayBalancer.AssignByLoad(days, visits);

        var geoTravel = WeekTravelMinutes(geo, place);
        var loadTravel = WeekTravelMinutes(byLoad, place);
        Assert.True(geoTravel < loadTravel, $"geo {geoTravel} min vs 3B {loadTravel} min");

        // Every visit placed, no budget exceeded, and the two days differ by no more than the 3B bound (the largest group).
        Assert.Empty(geo.Overflow);
        Assert.All(days, d => Assert.True(geo.LoadMinutes[d.Date] <= d.BudgetMinutes));
        Assert.True(Math.Abs(geo.LoadMinutes[Mon7] - geo.LoadMinutes[Tue8]) <= 60);
    }

    // ── 2 · the 3B constraints hold under clustering ───────────────────────────────────────────────────────────────

    [Fact]
    public void Clustering_keeps_budget_half_day_holiday_and_one_day_per_institution()
    {
        var days = new List<DayBalancer.Day>
        {
            new(Mon7, 480), new(Tue8, 0) /* holiday */, new(Wed9, 240) /* half day */, new(Thu10, 480)
        };
        var visits = new List<DayBalancer.Visit>();
        var id = 0;
        foreach (var (n, i) in new[] { 8, 6, 5, 4, 3, 2 }.Select((n, i) => (n, i)))
        {
            for (var k = 0; k < n; k++)
            {
                visits.Add(new DayBalancer.Visit(id++, "g" + i, 30, KadikoyLat + i * 0.003, KadikoyLng));
            }
        }

        var result = DayBalancer.Assign(days, visits);

        Assert.DoesNotContain(Tue8, result.Assigned.Values);
        Assert.All(days, d => Assert.True(result.LoadMinutes[d.Date] <= d.BudgetMinutes, d.Date.ToString()));
        foreach (var g in visits.GroupBy(v => v.GroupKey))
        {
            Assert.Single(g.Where(v => result.Assigned.ContainsKey(v.Id)).Select(v => result.Assigned[v.Id]).Distinct());
        }
    }

    // ── 3 · a visit pin: Tuesday → Thursday, the institution's others stay ──────────────────────────────────────────

    [Fact]
    public async Task A_visit_pin_moves_only_that_doctor_to_the_pinned_day()
    {
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        env.Session.Selection.SelectedContacts.Clear();
        var (x, xDoctors) = AddInstitution(env, "X", 4, KadikoyLat, KadikoyLng);
        AddInstitution(env, "Y", 2, KadikoyLat + 0.004, KadikoyLng + 0.004);

        var before = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;
        string DayOf(VisitPlanPreview p, Guid doctor) => p.Scheduled.Single(s => s.ContactId == doctor).PlannedDate;
        var target = DayOf(before, xDoctors[0]) == "2026-09-10" ? "2026-09-08" : "2026-09-10";

        env.Session.DayPins.Add(new PlanningDayPin
        {
            WeekStart = "2026-09-07", TargetType = PlannedVisitTargetType.Contact, TargetId = xDoctors[0],
            ContactId = xDoctors[0], Date = target, Scope = PlanningDayPinScopes.Visit
        });
        var after = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;

        var pinned = after.Scheduled.Single(s => s.ContactId == xDoctors[0]);
        Assert.Equal(target, pinned.PlannedDate);
        Assert.True(pinned.IsPinned);
        Assert.False(pinned.AutoPinned);
        Assert.Equal(x.ToString("N"), pinned.GroupKey);
        // scope = visit: the institution's other doctors are not pulled along.
        Assert.Equal(DayOf(before, xDoctors[1]), DayOf(after, xDoctors[1]));
        Assert.Equal(DayOf(before, xDoctors[2]), DayOf(after, xDoctors[2]));
        Assert.Equal(DayOf(before, xDoctors[3]), DayOf(after, xDoctors[3]));
        Assert.All(after.Scheduled.Where(s => s.ContactId != xDoctors[0]), s => Assert.False(s.IsPinned));
        Assert.Empty(after.PinWarnings!);
    }

    // ── 4b · institution scope: hospital + doctors + linked pharmacy move together; a visit pin wins ───────────────

    [Fact]
    public async Task An_institution_pin_moves_the_hospital_with_its_linked_pharmacy_and_a_visit_pin_wins()
    {
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        env.Session.Selection.SelectedContacts.Clear();
        var (hospital, doctors) = AddInstitution(env, "Hastane", 3, KadikoyLat, KadikoyLng);
        AddInstitution(env, "Diğer", 4, KadikoyLat + 0.004, KadikoyLng);
        var pharmacy = AddLinkedPharmacy(env, hospital, KadikoyLat + 0.001, KadikoyLng + 0.001);

        env.Session.DayPins.AddRange(new[]
        {
            new PlanningDayPin
            {
                WeekStart = "2026-09-07", TargetType = PlannedVisitTargetType.Account, TargetId = hospital,
                Date = "2026-09-10", Scope = PlanningDayPinScopes.Institution
            },
            new PlanningDayPin
            {
                WeekStart = "2026-09-07", TargetType = PlannedVisitTargetType.Contact, TargetId = doctors[0],
                ContactId = doctors[0], Date = "2026-09-08", Scope = PlanningDayPinScopes.Visit
            }
        });

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;

        Assert.Equal("2026-09-08", preview.Scheduled.Single(s => s.ContactId == doctors[0]).PlannedDate); // the visit pin wins
        Assert.All(doctors.Skip(1), d => Assert.Equal("2026-09-10", preview.Scheduled.Single(s => s.ContactId == d).PlannedDate));
        var pharmacySlot = preview.Scheduled.Single(s => s.TargetId == pharmacy);
        Assert.Equal("2026-09-10", pharmacySlot.PlannedDate);              // the linked pharmacy rides with the hospital
        Assert.True(pharmacySlot.IsPinned);
        Assert.Equal(hospital.ToString("N"), pharmacySlot.GroupKey);        // one group: hospital + doctors + pharmacy
        Assert.All(preview.Scheduled.Where(s => s.AccountId == hospital), s => Assert.Equal(hospital.ToString("N"), s.GroupKey));
    }

    // ── 4 · a pin that does not fit: keep what fits (route order), the rest to the next working day ─────────────────

    [Fact]
    public async Task A_pinned_day_keeps_what_fits_in_route_order_and_moves_the_rest_to_the_next_working_day()
    {
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        env.Session.Selection.SelectedContacts.Clear();
        var (a, _) = AddInstitution(env, "A", 14, KadikoyLat, KadikoyLng);                        // 7 h
        var (b, _) = AddInstitution(env, "B", 10, KadikoyLat + 0.003, KadikoyLng + 0.003);        // + 10 doctors
        AddLinkedPharmacy(env, b, KadikoyLat + 0.0035, KadikoyLng + 0.0035);                      // + 2 pharmacies
        AddLinkedPharmacy(env, b, KadikoyLat + 0.004, KadikoyLng + 0.004);
        foreach (var institution in new[] { a, b })
        {
            env.Session.DayPins.Add(new PlanningDayPin
            {
                WeekStart = "2026-09-07", TargetType = PlannedVisitTargetType.Account, TargetId = institution,
                Date = "2026-09-10", Scope = PlanningDayPinScopes.Institution
            });
        }

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;

        var thursday = preview.Scheduled.Where(s => s.PlannedDate == "2026-09-10").ToList();
        var friday = preview.Scheduled.Where(s => s.PlannedDate == "2026-09-11").ToList();
        Assert.Equal(26, thursday.Count + friday.Count);
        Assert.InRange(thursday.Count, 14, 16);
        Assert.All(thursday, s => Assert.True(s.IsPinned && !s.AutoPinned));
        Assert.All(friday, s => Assert.True(s.IsPinned && s.AutoPinned));
        // Never past the end of the working window, never over the day's budget.
        Assert.All(preview.Scheduled, s => Assert.True(string.CompareOrdinal(s.EndTime, "18:00") <= 0, s.EndTime));
        Assert.True(thursday.Sum(s => s.DurationMinutes) <= 480);
        // The moved visits are listed: from Thursday to Friday, the day was full.
        Assert.Equal(friday.Count, preview.PinOverflow!.Count);
        Assert.All(preview.PinOverflow, o =>
        {
            Assert.Equal(("2026-09-10", "2026-09-11"), (o.FromDate, o.ToDate));
            Assert.Equal(PlanningDayPins.PinDayFull, o.Reason);
        });
        var thursdayDay = preview.Days!.Single(d => d.Date == "2026-09-10");
        Assert.False(thursdayDay.OverCapacity);
    }

    [Fact]
    public async Task With_no_room_left_in_the_week_a_pinned_visit_moves_to_the_next_draft_week_as_pin_overflow()
    {
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        env.Session.Selection.SelectedContacts.Clear();
        var (a, _) = AddInstitution(env, "A", 14, KadikoyLat, KadikoyLng);
        var (b, _) = AddInstitution(env, "B", 10, KadikoyLat + 0.003, KadikoyLng + 0.003);
        foreach (var institution in new[] { a, b })
        {
            env.Session.DayPins.Add(new PlanningDayPin
            {
                WeekStart = "2026-09-07", TargetType = PlannedVisitTargetType.Account, TargetId = institution,
                Date = "2026-09-11", Scope = PlanningDayPinScopes.Institution // Friday: no later day in the week
            });
        }

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;

        var friday = preview.Scheduled.Where(s => s.PlannedDate == "2026-09-11").ToList();
        var moved = preview.PinOverflow!.Where(o => o.Reason == PlanningDayPins.PinOverflow).ToList();
        Assert.NotEmpty(moved);
        Assert.Equal(24, friday.Count + moved.Count);
        Assert.All(moved, o =>
        {
            Assert.Equal("2026-09-11", o.FromDate);
            Assert.NotNull(o.ToDate);
            Assert.True(string.CompareOrdinal(o.ToDate, "2026-09-14") >= 0, o.ToDate); // landed in a later draft week
        });
        Assert.Equal(moved.Count, preview.Shifted!.Count(s => s.Reason == PlanningDayPins.PinOverflow));
        Assert.All(preview.Scheduled, s => Assert.True(string.CompareOrdinal(s.EndTime, "18:00") <= 0, s.EndTime));
    }

    // ── 4c · the system split keeps linked pharmacies with most of the institution's doctors ──────────────────────

    [Fact]
    public void A_split_institution_keeps_its_linked_pharmacies_on_the_day_with_most_of_its_doctors()
    {
        var days = new[] { Mon7, Tue8 }.Select(d => new DayBalancer.Day(d, 480)).ToList();
        DayBalancer.Visit Doc(int id, int minutes) => new(id, "h", minutes, KadikoyLat, KadikoyLng);
        DayBalancer.Visit Pharm(int id) => new(id, "h", 30, KadikoyLat + 0.001, KadikoyLng, Follower: true);

        // 200 + 200 + 150 doctors and two 30-minute pharmacies: no day holds the 610 minutes.
        var room = DayBalancer.Assign(days, new[] { Pharm(10), Pharm(11), Doc(1, 200), Doc(2, 200), Doc(3, 150) });
        Assert.Equal(Mon7, room.Assigned[1]);
        Assert.Equal(Mon7, room.Assigned[2]);
        Assert.Equal(Tue8, room.Assigned[3]);
        Assert.Equal(Mon7, room.Assigned[10]); // the day of most of its doctors
        Assert.Equal(Mon7, room.Assigned[11]);

        // When that day is full, the pharmacies go to the institution's second day — never to a day without it.
        var full = DayBalancer.Assign(days, new[] { Pharm(10), Pharm(11), Doc(1, 240), Doc(2, 240), Doc(3, 100) });
        Assert.Equal(Mon7, full.Assigned[1]);
        Assert.Equal(Tue8, full.Assigned[3]);
        Assert.Equal(Tue8, full.Assigned[10]);
        Assert.Equal(Tue8, full.Assigned[11]);
    }

    // ── 4d · gap filling: near small groups fill the day, a near institution is split, a far one never fills ───────

    [Fact]
    public void A_days_idle_hour_takes_a_near_single_doctor()
    {
        var days = new List<DayBalancer.Day> { new(Mon7, 480, 420, KadikoyLat, KadikoyLng, 420), new(Tue8, 480, 300, BakirkoyLat, BakirkoyLng, 300) };
        var result = DayBalancer.Assign(days, new[] { new DayBalancer.Visit(1, "single", 30, KadikoyLat + 0.002, KadikoyLng) });

        Assert.Equal(Mon7, result.Assigned[1]);
        Assert.Equal(450, result.LoadMinutes[Mon7]);
    }

    [Fact]
    public void A_near_institution_that_does_not_fit_the_gap_is_split_and_its_pharmacy_follows_most_of_its_doctors()
    {
        // Monday: busy until 17:00 (1 h left); Tuesday: a near day with 2 h left. Neither holds the 3 h institution whole.
        var days = new List<DayBalancer.Day>
        {
            new(Mon7, 480, 420, KadikoyLat, KadikoyLng, 420), new(Tue8, 480, 360, KadikoyLat + 0.01, KadikoyLng, 360)
        };
        var visits = Enumerable.Range(1, 5)
            .Select(i => new DayBalancer.Visit(i, "near", 30, KadikoyLat + 0.003, KadikoyLng + 0.003))
            .Append(new DayBalancer.Visit(9, "near", 30, KadikoyLat + 0.0031, KadikoyLng + 0.003, Follower: true))
            .ToList();

        var result = DayBalancer.Assign(days, visits);

        Assert.Equal(2, visits.Count(v => v.Id < 9 && result.Assigned[v.Id] == Mon7)); // what fits the idle hour
        Assert.Equal(3, visits.Count(v => v.Id < 9 && result.Assigned[v.Id] == Tue8)); // the rest the next day
        Assert.Equal(Tue8, result.Assigned[9]);                                         // the pharmacy with the majority
        Assert.Equal(480, result.LoadMinutes[Mon7]);
        Assert.Equal(480, result.LoadMinutes[Tue8]);
    }

    [Fact]
    public async Task A_far_group_never_fills_the_idle_hour_which_stays_idle()
    {
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        foreach (var d in new[] { Tue8, Wed9, Thu10, Fri11 })
        {
            env.WorkingDays.Holidays.Add(d); // only Monday is open in the week
        }

        env.Session.Selection.SelectedContacts.Clear();
        var (a, _) = AddInstitution(env, "Kadıköy", 14, KadikoyLat, KadikoyLng);            // 7 h on Monday
        var (_, far) = AddInstitution(env, "Bakırköy", 1, BakirkoyLat, BakirkoyLng);          // 30 min, far
        env.Session.DayPins.Add(new PlanningDayPin
        {
            WeekStart = "2026-09-07", TargetType = PlannedVisitTargetType.Account, TargetId = a,
            Date = "2026-09-07", Scope = PlanningDayPinScopes.Institution
        });

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;

        Assert.DoesNotContain(preview.Scheduled, s => s.ContactId == far[0] && s.PlannedDate == "2026-09-07");
        var monday = preview.Days!.Single(d => d.Date == "2026-09-07");
        Assert.Equal((420, 60), (monday.PlannedMinutes, monday.IdleMinutes));
        // It goes to a later draft week instead (where it opens its own day).
        Assert.Contains(preview.Scheduled, s => s.ContactId == far[0] && string.CompareOrdinal(s.PlannedDate, "2026-09-14") >= 0);
    }

    // ── 5 · write rules: only a draft week, a working day of it; an absent target is ignored + warned ──────────────

    [Fact]
    public async Task A_pin_is_refused_on_an_approved_week_a_past_week_and_a_holiday()
    {
        var env = Env.WithTwoDoctors();
        env.WorkingDays.Holidays.Add(Wed9);
        var handler = PinHandler(env, Saturday5Sep);
        DayPinsInput Pin(string week, string date) => new(week, new[] { new DayPinInput("contact", env.DoctorA, null, date, "visit") });

        var holiday = await handler.Handle(Update(env, Pin("2026-09-07", "2026-09-09")), default);
        Assert.Equal((400, PlanningDayPins.PinNotWorkingDay), (holiday.StatusCode, holiday.Errors![0]));
        var weekend = await handler.Handle(Update(env, Pin("2026-09-07", "2026-09-12")), default);
        Assert.Equal((400, PlanningDayPins.PinNotWorkingDay), (weekend.StatusCode, weekend.Errors![0]));
        var past = await PinHandler(env, new DateTimeOffset(2026, 9, 14, 8, 0, 0, TimeSpan.Zero))
            .Handle(Update(env, Pin("2026-09-07", "2026-09-10")), default); // on 14 Sep the week of 7 Sep is over
        Assert.Equal((409, PlanningSessionErrorCodes.WeekInPast), (past.StatusCode, past.Errors![0]));

        env.Session.Weeks.Add(new PlanningWeek { WeekStart = "2026-09-14", Status = PlanningWeekStatus.Approved });
        var approved = await handler.Handle(Update(env, Pin("2026-09-14", "2026-09-15")), default);
        Assert.Equal((409, PlanningSessionErrorCodes.WeekAlreadyApproved), (approved.StatusCode, approved.Errors![0]));
        Assert.Empty(env.Session.DayPins); // nothing written by a refusal

        var ok = await handler.Handle(Update(env, Pin("2026-09-07", "2026-09-10")), default);
        Assert.Equal(200, ok.StatusCode);
        var stored = Assert.Single(env.Session.DayPins);
        Assert.Equal(("2026-09-07", "contact", env.DoctorA, "2026-09-10", "visit"),
            (stored.WeekStart, stored.TargetType, stored.TargetId, stored.Date, stored.Scope));
    }

    [Fact]
    public async Task A_pin_whose_target_has_no_visit_that_week_is_ignored_with_a_warning()
    {
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        var stranger = Guid.NewGuid();
        env.Session.DayPins.Add(new PlanningDayPin
        {
            WeekStart = "2026-09-07", TargetType = PlannedVisitTargetType.Contact, TargetId = stranger, ContactId = stranger,
            Date = "2026-09-10", Scope = PlanningDayPinScopes.Visit
        });

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;

        var warning = Assert.Single(preview.PinWarnings!);
        Assert.Equal(("2026-09-07", stranger, PlanningDayPins.PinTargetNotInWeek), (warning.WeekStart, warning.TargetId, warning.Code));
        Assert.All(preview.Scheduled, s => Assert.False(s.IsPinned));
    }

    // ── 6 · null keeps, [] clears (that week only) ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Day_pins_null_keeps_them_and_an_empty_list_clears_only_that_week()
    {
        var env = Env.WithTwoDoctors();
        var handler = PinHandler(env, Saturday5Sep);
        env.Session.DayPins.Add(new PlanningDayPin { WeekStart = "2026-09-14", TargetType = "contact", TargetId = env.DoctorB, Date = "2026-09-15" });
        await handler.Handle(Update(env, new DayPinsInput("2026-09-07",
            new[] { new DayPinInput("contact", env.DoctorA, null, "2026-09-10", null) })), default);
        Assert.Equal(2, env.Session.DayPins.Count);

        Assert.Equal(200, (await handler.Handle(Update(env, null), default)).StatusCode);
        Assert.Equal(2, env.Session.DayPins.Count); // null = keep

        Assert.Equal(200, (await handler.Handle(Update(env, new DayPinsInput("2026-09-07", Array.Empty<DayPinInput>())), default)).StatusCode);
        var left = Assert.Single(env.Session.DayPins); // [] = clear that week; the other week's pin stays
        Assert.Equal("2026-09-14", left.WeekStart);
    }

    // ── 7 · approval writes the pinned days; reopening keeps the pins ─────────────────────────────────────────────

    [Fact]
    public async Task Approving_the_week_writes_the_pinned_day_and_reopening_keeps_the_pins()
    {
        var env = Env.WithTwoDoctors();
        env.Session.DayPins.Add(new PlanningDayPin
        {
            WeekStart = Week7Sep, TargetType = PlannedVisitTargetType.Contact, TargetId = env.DoctorA, ContactId = env.DoctorA,
            Date = "2026-09-10", Scope = PlanningDayPinScopes.Visit
        });

        var written = await ApproveAndStoreAsync(env, Week7Sep, Saturday5Sep);
        Assert.Equal(Thu10, written.Single(a => a.ContactId == env.DoctorA).PlannedDate);

        var reopened = await ReopenHandler(env, Saturday5Sep)
            .Handle(new ReopenPlanningWeekCommand(env.Session.Id, Week7Sep, "Doktor izinde, hafta yeniden planlanacak", null), default);
        Assert.True(reopened.IsSuccessful, string.Join(" / ", reopened.Errors ?? Array.Empty<string>()));
        var pin = Assert.Single(env.Session.DayPins);
        Assert.Equal(("2026-09-10", env.DoctorA), (pin.Date, pin.TargetId));

        // The detail lists the week's pins (4D / 4F).
        var dto = (await DetailHandler(env, Saturday5Sep).Handle(new GetPlanningSessionByIdQuery(env.Session.Id), default)).Data!;
        var weekPins = dto.Weeks!.Single(w => w.WeekStart == Week7Sep).DayPins!;
        Assert.Equal(("2026-09-10", env.DoctorA, "visit"), (weekPins.Single().Date, weekPins.Single().TargetId, weekPins.Single().Scope));
        Assert.Empty(dto.Weeks!.Single(w => w.WeekStart == "2026-09-14").DayPins!);
    }

    // ── 8 · a group without a location is placed by load and never joins a cluster ───────────────────────────────

    [Fact]
    public void A_group_without_a_location_goes_to_the_emptiest_day_and_is_not_tied_to_a_cluster()
    {
        var days = new[] { Mon7, Tue8 }.Select(d => new DayBalancer.Day(d, 480)).ToList();
        var visits = Enumerable.Range(0, 10).Select(i => new DayBalancer.Visit(i, "kadikoy", 30, KadikoyLat, KadikoyLng))      // 300
            .Concat(Enumerable.Range(30, 8).Select(i => new DayBalancer.Visit(i, "bakirkoy", 30, BakirkoyLat, BakirkoyLng)))  // 240
            .Concat(Enumerable.Range(10, 4).Select(i => new DayBalancer.Visit(i, "nowhere", 30)))                           // 120
            .Concat(Enumerable.Range(20, 2).Select(i => new DayBalancer.Visit(i, "near", 30, KadikoyLat + 0.002, KadikoyLng)))
            .ToList();

        var result = DayBalancer.Assign(days, visits);

        Assert.All(Enumerable.Range(0, 10), i => Assert.Equal(Mon7, result.Assigned[i]));
        Assert.All(Enumerable.Range(30, 8), i => Assert.Equal(Tue8, result.Assigned[i]));
        Assert.All(Enumerable.Range(10, 4), i => Assert.Equal(Tue8, result.Assigned[i])); // the lowest fill (3B), 240 < 300
        // The unlocated group did not move Tuesday's centre: the Kadıköy group still joins Monday's cluster.
        Assert.All(Enumerable.Range(20, 2), i => Assert.Equal(Mon7, result.Assigned[i]));
    }

    // ── 9 · the visit model (4C §37): preview + detail, from the period capacity; none without one ───────────────

    [Fact]
    public async Task The_preview_and_the_detail_carry_the_capacity_visit_model_and_none_without_a_capacity()
    {
        var env = Env.WithTwoDoctors();
        var none = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!.VisitModel!;
        Assert.Equal(new VisitModelDto(null, null, null, null, null, VisitModelDto.None), none);

        // The detail reads the period capacity. (This test kit's engine refuses to estimate a capacity, so the preview's
        // capacity case is the same VisitModelDto.From the engine calls — checked directly.)
        env.Capacities.Capacity = new CapacityEntity
        {
            TenantId = Tenant, CyclePeriodId = env.Session.CyclePeriodId, DailyWorkMinutes = 480, TravelingTime = 60,
            PromoProductTime = 12, NonPromoProductTime = 5, MaxPromoProducts = 2, MaxNonPromoProducts = 4,
            TypicalPromoCount = 1, TypicalNonPromoCount = 1, ReportMinutesPerVisit = 6
        };
        var model = new VisitModelDto(2, 4, 12, 5, 6, VisitModelDto.FromCycleCapacity);
        Assert.Equal(model, VisitModelDto.From(env.Capacities.Capacity));
        var dto = (await DetailHandler(env, Saturday5Sep).Handle(new GetPlanningSessionByIdQuery(env.Session.Id), default)).Data!;
        Assert.Equal(model, dto.VisitModel);

        // A legacy-model capacity (no typical visit): the report duration is the per-visit report; the limits default to 3.
        Assert.Equal(new VisitModelDto(3, 3, 10, 0, 8, VisitModelDto.FromCycleCapacity),
            VisitModelDto.From(new CapacityEntity { PromoProductTime = 10, ReportDuration = 8 }));

        // Without a capacity the detail says so too.
        env.Capacities.Capacity = null;
        var bare = (await DetailHandler(env, Saturday5Sep).Handle(new GetPlanningSessionByIdQuery(env.Session.Id), default)).Data!;
        Assert.Equal(VisitModelDto.None, bare.VisitModel!.Source);
    }

    // ── class map: the pins' ids are strings; an older document without pins reads ────────────────────────────────

    [Fact]
    public void Day_pins_are_stored_with_string_ids_and_an_older_session_without_them_reads()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var doctor = Guid.NewGuid();
        var session = new PlanningSession
        {
            Id = Guid.NewGuid(), TenantId = Tenant, CyclePeriodId = Id(30), ResourceId = "rep-1",
            DayPins = new List<PlanningDayPin>
            {
                new()
                {
                    WeekStart = Week7Sep, TargetType = PlannedVisitTargetType.Contact, TargetId = doctor, ContactId = doctor,
                    Date = "2026-09-10", Scope = PlanningDayPinScopes.Institution
                }
            }
        };

        var doc = MongoDB.Bson.BsonExtensionMethods.ToBsonDocument(session);
        var stored = doc["DayPins"].AsBsonArray[0].AsBsonDocument;
        Assert.Equal(doctor.ToString(), stored["TargetId"].AsString);
        Assert.Equal(doctor.ToString(), stored["ContactId"].AsString);
        var back = MongoDB.Bson.Serialization.BsonSerializer.Deserialize<PlanningSession>(doc);
        var pin = Assert.Single(back.DayPins);
        Assert.Equal((doctor, "2026-09-10", PlanningDayPinScopes.Institution), (pin.TargetId, pin.Date, pin.Scope));

        doc.Remove("DayPins"); // a document written before WP-VP-4E
        Assert.Empty(MongoDB.Bson.Serialization.BsonSerializer.Deserialize<PlanningSession>(doc).DayPins);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The week's travel minutes when each day of <paramref name="result"/> is routed by the production optimizer.</summary>
    private static int WeekTravelMinutes(DayBalancer.Result result, IReadOnlyDictionary<int, (double Lat, double Lng)> place)
    {
        var optimizer = new GreedyTimeWindowRouteOptimizer(new DefaultRouteSettings());
        var window = PlanningDayBudget.From(null, RouteOptimizationDefaults.WorkingDay)
            .WindowFor(PlanningDayKinds.Working, RouteOptimizationDefaults.WorkingDay);
        var total = 0;
        foreach (var day in result.Assigned.GroupBy(kv => kv.Value))
        {
            var output = optimizer.Optimize(new RouteOptimizationInput(
                day.Select(kv => new RouteVisitInput(Guid.NewGuid(), place[kv.Key].Lat, place[kv.Key].Lng, 30)).ToList(),
                new RepWorkingHours(window), new OptimizationPeriod(day.Key, day.Key), 0, new TravelModelSpec()));
            Assert.Empty(output.Unscheduled);
            total += output.Scheduled.Sum(s => s.TravelToNextMinutes);
        }

        return total;
    }

    private static Guid AddLinkedPharmacy(Env env, Guid institution, double lat, double lng)
    {
        var pharmacy = Guid.NewGuid();
        env.Accounts.Rows[pharmacy] = new AccountEntity
        {
            Id = pharmacy, TenantId = Tenant, AccountName = "Eczane " + pharmacy.ToString("N")[..4], AccountType = "pharmacy",
            Latitude = lat, Longitude = lng
        };
        env.Session.Selection.SelectedPharmacyIds.Add(pharmacy);
        env.Relationships.Rows.Add(new AccountRelationship
        {
            Id = Guid.NewGuid(), TenantId = Tenant, SourceAccountId = institution, TargetAccountId = pharmacy,
            RelationshipType = "supplies", Status = "active"
        });
        return pharmacy;
    }

    private UpdatePlanningSessionSelectionHandler PinHandler(Env env, DateTimeOffset now)
        => new(TenantOf(Tenant), new NullActorContext(), new FakePlanningSessionRepository(env.Session),
            TestCallerScope.Unrestricted("rep-1"), products: null, periods: env.Periods,
            calendar: new PlanningWorkingCalendar(new FixedCountryResolver("TR"), env.WorkingDays),
            capacities: env.Capacities, clock: new PinnedClock(now));

    private static UpdatePlanningSessionSelectionCommand Update(Env env, DayPinsInput? pins)
        => new(env.Session.Id, null, null, null, null, null, null, null, null, DayPins: pins);

    private GetPlanningSessionByIdHandler DetailHandler(Env env, DateTimeOffset now)
        => new(TenantOf(Tenant), new FakePlanningSessionRepository(env.Session), TestCallerScope.Unrestricted("rep-1"),
            new Features.PlannedVisit.VisitTargetNameReader(env.Accounts, env.Contacts), env.Periods, new PinnedClock(now),
            env.PlannedVisits, env.Capacities);
}
