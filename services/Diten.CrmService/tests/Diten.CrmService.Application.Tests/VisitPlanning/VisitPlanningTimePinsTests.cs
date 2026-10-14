using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Application.Features.VisitPlanning.Commands;
using Diten.CrmService.Application.Features.VisitPlanning.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.VisitPlanning.Queries;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitPlanning;

/// <summary>
/// WP-VW-W2 (BE-b) — TIME pins (a day + a start time) on the PRODUCTION engine, its pure in-day step and the EXISTING
/// selection update (Acceptance 1–5): the pinned visit sits at its time, the day's other visits go before / after it in
/// the route's order; a time outside the working hours or off the 15-minute grid is refused; two pins on one time → the
/// second moves to the nearest free start (pin_time_conflict); a pin without a time is the 4E day pin; an approved week
/// takes no pin (409).
/// </summary>
public sealed partial class VisitPlanningTests
{
    private static int Minutes(string hhmm) => int.Parse(hhmm[..2]) * 60 + int.Parse(hhmm[3..]);

    // ── 1 · a 10:30 pin: the visit at 10:30, the others before and after it, in a stable order ─────────────────────

    [Fact]
    public async Task A_time_pin_puts_the_visit_at_its_time_and_the_days_other_visits_before_and_after_it_in_route_order()
    {
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        env.Session.Selection.SelectedContacts.Clear();
        // eight single-doctor practices a few hundred metres apart (a defined geographic order), the rep's day for all of
        // them on Wednesday (4E day pins, no time)
        var doctors = Enumerable.Range(0, 8)
            .Select(i => AddInstitution(env, "P" + i, 1, KadikoyLat + 0.003 * i, KadikoyLng + 0.002 * (i % 3)).Doctors[0])
            .ToList();
        const string day = "2026-09-09";
        PlanningDayPin pin = null!;
        foreach (var d in doctors)
        {
            var p = new PlanningDayPin
            {
                WeekStart = "2026-09-07", TargetType = PlannedVisitTargetType.Contact, TargetId = d, ContactId = d,
                Date = day, Scope = PlanningDayPinScopes.Visit
            };
            env.Session.DayPins.Add(p);
            if (d == doctors[5]) pin = p;
        }

        // without a time: the route's order of that day
        var dayPinned = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;
        var routeOrder = dayPinned.Scheduled.Where(s => s.PlannedDate == day && s.ContactId != doctors[5])
            .OrderBy(s => s.StartTime).Select(s => s.ContactId).ToList();

        pin.StartTime = "10:30";
        var after = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;

        var pinned = after.Scheduled.Single(s => s.ContactId == doctors[5]);
        Assert.Equal((day, "10:30", "10:30", true), (pinned.PlannedDate, pinned.StartTime, pinned.PinnedTime, pinned.IsPinned));
        var others = after.Scheduled.Where(s => s.PlannedDate == day && s.ContactId != doctors[5]).OrderBy(s => s.StartTime).ToList();
        Assert.Equal(7, others.Count);
        Assert.Contains(others, s => Minutes(s.EndTime!) <= Minutes("10:30"));   // before it …
        Assert.Contains(others, s => Minutes(s.StartTime!) >= Minutes(pinned.EndTime!)); // … and after it
        Assert.All(others, s => Assert.True(
            Minutes(s.EndTime!) <= Minutes(pinned.StartTime!) || Minutes(s.StartTime!) >= Minutes(pinned.EndTime!), "overlaps the pin"));
        // the route's (geographic) order of the others is kept: the same as on the day pin without a time
        Assert.Equal(routeOrder, others.Select(s => s.ContactId).ToList());
        Assert.All(others, s => Assert.Null(s.PinnedTime));
        // the day's sequence follows the times
        var sequence = after.Scheduled.Where(s => s.PlannedDate == day).OrderBy(s => s.SequenceOrder).Select(s => s.StartTime).ToList();
        Assert.Equal(sequence.OrderBy(t => t, StringComparer.Ordinal).ToList(), sequence);
        Assert.DoesNotContain(after.PinOverflow!, m => m.Reason == PlanningDayPins.PinTimeConflict);

        // stable: the same input gives the same day
        var again = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;
        string Times(VisitPlanPreview p) => string.Join(",", p.Scheduled.OrderBy(s => s.ContactId).Select(s => s.ContactId + "@" + s.PlannedDate + " " + s.StartTime));
        Assert.Equal(Times(after), Times(again));
    }

    [Fact]
    public void The_day_step_fills_before_the_pin_only_what_ends_in_time_to_travel_there_and_keeps_lunch_and_windows()
    {
        // 09:00–18:00, lunch 13:00–14:00, 10 min buffer; 30 min visits; 20 min travel between any two places
        var stops = new List<DayTimePins.Stop>
        {
            new(0, 30, 1, 1), new(1, 30, 2, 2),
            new(2, 30, 3, 3, PinMinute: 10 * 60 + 30),
            new(3, 30, 4, 4), new(4, 30, 5, 5, Windows: new[] { (15 * 60, 16 * 60) })
        };
        var result = DayTimePins.Place(stops, 9 * 60, 18 * 60, 13 * 60, 14 * 60, 10, (a, b) => a.Key == b.Key ? 0 : 20);

        string At(int key) => FormatMinutes(result.Placed.Single(s => s.Key == key).Start);
        Assert.Equal("09:00", At(0));
        Assert.Equal("10:30", At(2));
        // 09:30 + 10 buffer + 20 travel = 10:00 → 10:30, but then 10 buffer + 20 travel to the pin would miss 10:30: after it
        Assert.Equal("11:30", At(1)); // 11:00 + 10 buffer + 20 travel from the pinned visit
        Assert.Equal("12:30", At(3)); // ends 13:00, the lunch break's start
        Assert.Equal("15:00", At(4)); // its availability window
        Assert.Empty(result.DayFull);
        Assert.Empty(result.NoWindow);
        Assert.Equal(new[] { 0, 2, 1, 3, 4 }, result.Placed.Select(s => s.Key).ToArray()); // the others keep the route order

        // a visit that would run into lunch starts after it
        var lunch = DayTimePins.Place(new List<DayTimePins.Stop> { new(0, 30, 1, 1, PinMinute: 12 * 60), new(1, 45, 1, 1) },
            9 * 60, 18 * 60, 13 * 60, 14 * 60, 0, (_, _) => 0);
        Assert.Equal((9 * 60, 12 * 60), (lunch.Placed.Single(s => s.Key == 1).Start, lunch.Placed.Single(s => s.Key == 0).Start));
        var full = DayTimePins.Place(new List<DayTimePins.Stop> { new(0, 240, 1, 1, PinMinute: 14 * 60), new(1, 250, 1, 1) },
            9 * 60, 18 * 60, 13 * 60, 14 * 60, 0, (_, _) => 0);
        Assert.Equal(new[] { 1 }, full.DayFull); // no room left in the working window
    }

    private static string FormatMinutes(int m) => $"{m / 60:D2}:{m % 60:D2}";

    // ── 2 · write rules: outside the working hours / off the 15-minute grid → 400 ──────────────────────────────────

    [Fact]
    public async Task A_pin_time_outside_the_working_hours_or_off_the_grid_is_refused_and_a_valid_one_is_stored()
    {
        var env = Env.WithTwoDoctors();
        var handler = PinHandler(env, Saturday5Sep);
        DayPinsInput Pin(string? time, string scope = "visit")
            => new("2026-09-07", new[] { new DayPinInput("contact", env.DoctorA, null, "2026-09-10", scope, time) });

        foreach (var off in new[] { "10:10", "10:07", "9:30", "25:00", "10.30", "ten" })
        {
            var r = await handler.Handle(Update(env, Pin(off)), default);
            Assert.Equal((400, PlanningDayPins.PinTimeInvalid), (r.StatusCode, r.Errors![0]));
        }

        var institution = await handler.Handle(Update(env, Pin("10:30", "institution")), default);
        Assert.Equal((400, PlanningDayPins.PinTimeInvalid), (institution.StatusCode, institution.Errors![0])); // a visit pin only

        foreach (var outside in new[] { "08:45", "13:00", "13:45", "18:00", "21:00" })
        {
            var r = await handler.Handle(Update(env, Pin(outside)), default);
            Assert.Equal((400, PlanningDayPins.PinTimeOutsideHours), (r.StatusCode, r.Errors![0]));
        }

        Assert.Empty(env.Session.DayPins); // nothing written by a refusal

        Assert.Equal(200, (await handler.Handle(Update(env, Pin("09:00")), default)).StatusCode);
        Assert.Equal(200, (await handler.Handle(Update(env, Pin("17:45")), default)).StatusCode);
        Assert.Equal(200, (await handler.Handle(Update(env, Pin("14:00")), default)).StatusCode);
        var stored = Assert.Single(env.Session.DayPins);
        Assert.Equal(("2026-09-10", "visit", "14:00"), (stored.Date, stored.Scope, stored.StartTime));
        var dto = (await DetailHandler(env, Saturday5Sep).Handle(new GetPlanningSessionByIdQuery(env.Session.Id), default)).Data!;
        Assert.Equal("14:00", dto.Weeks!.Single(w => w.WeekStart == Week7Sep).DayPins!.Single().StartTime); // read back
    }

    // ── 3 · two pins on one time: the second moves to the nearest free start, pin_time_conflict ─────────────────────

    [Fact]
    public async Task Two_pins_on_one_time_the_second_moves_to_the_nearest_free_start_with_pin_time_conflict()
    {
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        env.Session.Selection.SelectedContacts.Clear();
        var (_, doctors) = AddInstitution(env, "X", 4, KadikoyLat, KadikoyLng);
        foreach (var d in new[] { doctors[1], doctors[2] })
        {
            env.Session.DayPins.Add(new PlanningDayPin
            {
                WeekStart = "2026-09-07", TargetType = PlannedVisitTargetType.Contact, TargetId = d, ContactId = d,
                Date = "2026-09-09", Scope = PlanningDayPinScopes.Visit, StartTime = "10:30"
            });
        }

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;

        var first = preview.Scheduled.Single(s => s.ContactId == doctors[1]);
        var second = preview.Scheduled.Single(s => s.ContactId == doctors[2]);
        Assert.Equal(("2026-09-09", "10:30", "10:30"), (first.PlannedDate, first.StartTime, first.PinnedTime));
        Assert.Equal(("2026-09-09", "10:30"), (second.PlannedDate, second.PinnedTime));
        Assert.NotEqual("10:30", second.StartTime);
        Assert.Equal(0, Minutes(second.StartTime!) % DayTimePins.Step); // on the grid
        Assert.True(Minutes(second.StartTime!) >= Minutes(first.EndTime!) || Minutes(second.EndTime!) <= Minutes(first.StartTime!));
        // same-place doctors (equal travel — the optimizer's ties): the same day every time
        var again = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;
        string Day(VisitPlanPreview p) => string.Join(",", p.Scheduled.OrderBy(s => s.PlannedDate).ThenBy(s => s.StartTime).Select(s => s.ContactId + " " + s.StartTime));
        Assert.Equal(Day(preview), Day(again));
        var move = Assert.Single(preview.PinOverflow!, m => m.Reason == PlanningDayPins.PinTimeConflict);
        Assert.Equal((doctors[2], "2026-09-09", "2026-09-09"), (move.ContactId, move.FromDate, move.ToDate));

        // the pure step: nearest free start, the later one on a tie
        var pure = DayTimePins.Place(new List<DayTimePins.Stop>
        {
            new(0, 30, 1, 1, PinMinute: 10 * 60 + 30, PinOrder: 1),
            new(1, 30, 1, 1, PinMinute: 10 * 60 + 30, PinOrder: 2)
        }, 9 * 60, 18 * 60, 13 * 60, 14 * 60, 0, (_, _) => 0);
        Assert.Equal((10 * 60 + 30, 11 * 60), (pure.Placed.Single(s => s.Key == 0).Start, pure.Placed.Single(s => s.Key == 1).Start));
    }

    // ── 3b · a valid late time whose visit would end after the working day: its own reason (CT, user 2026-10-09) ────────

    [Fact]
    public async Task A_late_pin_whose_visit_would_end_after_the_day_moves_earlier_with_pin_time_past_day_end()
    {
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        env.Session.Selection.SelectedContacts.Clear();
        var (_, doctors) = AddInstitution(env, "X", 4, KadikoyLat, KadikoyLng);
        env.Session.DayPins.Add(new PlanningDayPin
        {
            WeekStart = "2026-09-07", TargetType = PlannedVisitTargetType.Contact, TargetId = doctors[1], ContactId = doctors[1],
            Date = "2026-09-09", Scope = PlanningDayPinScopes.Visit, StartTime = "17:45"
        });

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;

        var late = preview.Scheduled.Single(s => s.ContactId == doctors[1]);
        Assert.Equal(("2026-09-09", "17:45"), (late.PlannedDate, late.PinnedTime));
        Assert.True(Minutes(late.EndTime!) <= 18 * 60, $"ends {late.EndTime} — never past the working day");
        Assert.NotEqual("17:45", late.StartTime);
        var move = Assert.Single(preview.PinOverflow!, m => m.ContactId == doctors[1]);
        Assert.Equal((PlanningDayPins.PinTimePastDayEnd, "2026-09-09", "2026-09-09"), (move.Reason, move.FromDate, move.ToDate));
        Assert.DoesNotContain(preview.PinOverflow!, m => m.Reason == PlanningDayPins.PinTimeConflict);
    }

    // ── 4 · a pin without a time is the 4E day pin ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_pin_without_a_time_stays_a_day_pin_with_no_pinned_time()
    {
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        env.Session.Selection.SelectedContacts.Clear();
        var (_, doctors) = AddInstitution(env, "X", 4, KadikoyLat, KadikoyLng);
        env.Session.DayPins.Add(new PlanningDayPin
        {
            WeekStart = "2026-09-07", TargetType = PlannedVisitTargetType.Contact, TargetId = doctors[0], ContactId = doctors[0],
            Date = "2026-09-10", Scope = PlanningDayPinScopes.Visit
        });

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;

        var pinned = preview.Scheduled.Single(s => s.ContactId == doctors[0]);
        Assert.Equal(("2026-09-10", true, (string?)null), (pinned.PlannedDate, pinned.IsPinned, pinned.PinnedTime));
        Assert.All(preview.Scheduled, s => Assert.Null(s.PinnedTime));
        Assert.DoesNotContain(preview.PinOverflow!, m => m.Reason == PlanningDayPins.PinTimeConflict);
    }

    // ── 5 · an approved week takes no pin, timed or not (409) ──────────────────────────────────────────────────────

    [Fact]
    public async Task A_time_pin_on_an_approved_week_is_refused_with_409()
    {
        var env = Env.WithTwoDoctors();
        env.Session.Weeks.Add(new PlanningWeek { WeekStart = "2026-09-14", Status = PlanningWeekStatus.Approved });
        var handler = PinHandler(env, Saturday5Sep);

        var refused = await handler.Handle(Update(env, new DayPinsInput("2026-09-14",
            new[] { new DayPinInput("contact", env.DoctorA, null, "2026-09-15", "visit", "10:30") })), default);

        Assert.Equal((409, PlanningSessionErrorCodes.WeekAlreadyApproved), (refused.StatusCode, refused.Errors![0]));
        Assert.Empty(env.Session.DayPins);
    }

    // ── storage: the time is stored; an older pin (no time) reads as a day pin ────────────────────────────────────

    [Fact]
    public void A_pin_time_is_stored_and_an_older_pin_without_it_reads_as_a_day_pin()
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
                    Date = "2026-09-10", Scope = PlanningDayPinScopes.Visit, StartTime = "10:30"
                }
            }
        };

        var doc = MongoDB.Bson.BsonExtensionMethods.ToBsonDocument(session);
        var stored = doc["DayPins"].AsBsonArray[0].AsBsonDocument;
        Assert.Equal("10:30", stored["StartTime"].AsString);
        Assert.Equal("10:30", Assert.Single(MongoDB.Bson.Serialization.BsonSerializer.Deserialize<PlanningSession>(doc).DayPins).StartTime);

        stored.Remove("StartTime"); // a pin written before WP-VW-W2
        Assert.Null(Assert.Single(MongoDB.Bson.Serialization.BsonSerializer.Deserialize<PlanningSession>(doc).DayPins).StartTime);
    }
}
