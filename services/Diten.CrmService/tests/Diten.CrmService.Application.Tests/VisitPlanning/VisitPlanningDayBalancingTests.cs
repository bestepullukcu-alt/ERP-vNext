using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.CycleCapacity.Read;
using Diten.CrmService.Application.Features.RouteOptimization;
using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Domain.Entities;
using Xunit;
using AccountEntity = Diten.CrmService.Domain.Entities.Account;
using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;

namespace Diten.CrmService.Application.Tests.VisitPlanning;

/// <summary>
/// WP-VP-3B — day balancing on the PRODUCTION engine (only stores / calendar / clock are fakes): the day budget from the
/// cycle capacity and its half / holiday variants, institution groups spread over the emptiest working days, half days,
/// overflow to the next DRAFT week (never an approved one) and period_exhausted at the end, the weekly capacity in minutes,
/// the consent-blocked exclusion, the manual order inside a day, and the calendar cache.
/// <para>Period 2026-09-01 (Tue) … 2026-09-28 (Mon); weeks: 0 = 31 Aug (from 1 Sep), 1 = 7 Sep, 2 = 14 Sep, 3 = 21 Sep,
/// 4 = 28 Sep (one day). Without a capacity the day budget is the default 09:00–18:00 minus lunch = 480 minutes and a
/// visit is 30 minutes ⇒ 16 a day.</para>
/// </summary>
public sealed partial class VisitPlanningTests
{
    private static readonly DateTimeOffset Mon28Sep = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);

    // ── 1 · the day budget ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_day_budget_is_work_minus_fixed_and_the_cap_divides_it_by_visit_plus_buffer()
    {
        // 480 working minutes, 60 fixed (travel), a 20-minute typical visit (1 promo × 15 + 5 report), 10 buffer.
        var capacity = new CapacityEntity
        {
            DailyWorkMinutes = 480, TravelingTime = 60, QuizDuration = 0, PromoProductTime = 15, NonPromoProductTime = 0,
            TypicalPromoCount = 1, TypicalNonPromoCount = 0, ReportMinutesPerVisit = 5, BetweenVisitTimeMinutes = 10
        };

        var budget = PlanningDayBudget.From(capacity, RouteOptimizationDefaults.WorkingDay);

        Assert.Equal(420, budget.BudgetMinutes);
        Assert.Equal(14, budget.CapFor(PlanningDayKinds.Working));
        Assert.Equal(7, budget.CapFor(PlanningDayKinds.Half));
        Assert.Equal(0, budget.CapFor(PlanningDayKinds.Holiday));
        Assert.Equal(0, budget.BudgetFor(PlanningDayKinds.Weekend));
        Assert.Equal(PlanningDayBudget.FromCycleCapacity, budget.Source);
        // The route window: 09:00 + 480 working minutes, lunch excluded as today ⇒ 18:00; a half day ⇒ 13:00.
        Assert.Equal("18:00", budget.WindowFor(PlanningDayKinds.Working, RouteOptimizationDefaults.WorkingDay).End);
        Assert.Equal("13:00", budget.WindowFor(PlanningDayKinds.Half, RouteOptimizationDefaults.WorkingDay).End);

        // Without a capacity: the default hours (09–18 minus lunch).
        var fallback = PlanningDayBudget.From(null, RouteOptimizationDefaults.WorkingDay);
        Assert.Equal(480, fallback.BudgetMinutes);
        Assert.Equal(16, fallback.CapFor(PlanningDayKinds.Working));
        Assert.Equal(PlanningDayBudget.FromDefaultHours, fallback.Source);
    }

    // ── 2 · balancing: institutions stay together, the week fills evenly, no weekend / holiday ──────────────────────

    [Fact]
    public async Task Forty_visits_of_five_institutions_spread_over_the_working_days_with_each_institution_on_one_day()
    {
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        env.WorkingDays.Holidays.Add(new DateOnly(2026, 9, 9)); // Wednesday is a public holiday
        env.Session.Selection.SelectedContacts.Clear();
        var institutions = new[] { 12, 10, 8, 6, 4 }
            .Select((n, i) => AddInstitution(env, "Kurum " + i, n, 41.0 + i * 0.01, 29.0))
            .ToList();

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;

        Assert.Empty(preview.Unscheduled);
        Assert.Empty(preview.Shifted!);
        var slots = preview.Scheduled.Where(s => !s.IsFixed).ToList();
        Assert.Equal(40, slots.Count);
        var dates = slots.Select(s => DateOnly.Parse(s.PlannedDate)).ToList();
        Assert.All(dates, d => Assert.DoesNotContain(d.DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }));
        Assert.DoesNotContain(new DateOnly(2026, 9, 9), dates);

        // Each institution's doctors are on ONE day.
        foreach (var (account, _) in institutions)
        {
            Assert.Single(slots.Where(s => s.AccountId == account).Select(s => s.PlannedDate).Distinct());
        }

        // The daily load differs by at most the largest institution (12 × 30 minutes) — not "everything on Monday".
        var load = slots.GroupBy(s => s.PlannedDate).Select(g => g.Sum(s => s.DurationMinutes)).ToList();
        Assert.Equal(4, load.Count); // all four working days are used
        Assert.True(load.Max() - load.Min() <= 12 * 30, string.Join(",", load));
    }

    // ── 3 · a half day holds half ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_half_day_from_the_calendar_holds_half_the_budget_and_is_listed()
    {
        var env = Env.WithTwoDoctors();
        env.WorkingDays.HalfDays.Add(new DateOnly(2026, 9, 9)); // Wednesday is a half day
        env.Session.Selection.SelectedContacts.Clear();
        for (var i = 0; i < 72; i++)
        {
            AddInstitution(env, "Tek " + i, 1, 41.0, 29.0 + i * 0.001);
        }

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;

        Assert.Contains("2026-09-09", preview.HalfDayDates!);
        var week = preview.WeekCapacity![1];
        Assert.Equal(4, week.WorkingDays);
        Assert.Equal(1, week.HalfDays);
        Assert.Equal((4 * 480) + 240, week.CapacityMinutes);

        var perDay = preview.Scheduled.Where(s => s.WeekNumber == 1).GroupBy(s => s.PlannedDate)
            .ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(8, perDay["2026-09-09"]);  // 240 minutes ÷ 30
        Assert.All(perDay.Where(d => d.Key != "2026-09-09"), d => Assert.Equal(16, d.Value));
    }

    // ── 4 · overflow to the next DRAFT week; period_exhausted at the end ───────────────────────────────────────────

    [Fact]
    public async Task What_the_week_cannot_hold_moves_to_the_next_draft_week_with_the_reason()
    {
        var env = SixtySingleDoctorInstitutions();

        // Today is Wednesday 2 Sep: week 0 has Wed–Fri = 3 days × 16 = 48 visits; the other 12 move to week 1.
        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Wed2Sep), default)).Preview!;

        Assert.Empty(preview.Unscheduled);
        Assert.Equal(48, preview.Scheduled.Count(s => s.WeekNumber == 0));
        Assert.Equal(12, preview.Scheduled.Count(s => s.WeekNumber == 1));
        Assert.Equal(12, preview.Shifted!.Count);
        Assert.All(preview.Shifted, s =>
        {
            Assert.Equal(0, s.FromWeek);
            Assert.Equal(1, s.ToWeek);
            Assert.Equal(PlanningShiftReasons.CapacityFull, s.Reason);
            Assert.NotNull(s.DisplayName);
        });
    }

    [Fact]
    public async Task Overflow_skips_an_approved_week()
    {
        var env = SixtySingleDoctorInstitutions();
        env.Session.Weeks.Add(new PlanningWeek { WeekStart = Week7Sep, Status = PlanningWeekStatus.Approved });

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Wed2Sep), default)).Preview!;

        Assert.Equal(0, preview.Scheduled.Count(s => s.WeekNumber == 1)); // the approved week takes nothing
        Assert.Equal(12, preview.Scheduled.Count(s => s.WeekNumber == 2));
        Assert.All(preview.Shifted!, s => Assert.Equal(2, s.ToWeek));
    }

    [Fact]
    public async Task Past_the_last_draft_week_the_rest_is_period_exhausted()
    {
        var env = Env.WithTwoDoctors();
        env.Session.Selection.SelectedContacts.Clear();
        for (var i = 0; i < 20; i++)
        {
            AddInstitution(env, "Son " + i, 1, 41.0, 29.0 + i * 0.001);
        }

        // Today is the period's last day (Mon 28 Sep, the only day of week 4): 16 fit, 4 cannot go anywhere.
        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Mon28Sep), default)).Preview!;

        Assert.Equal(16, preview.Scheduled.Count);
        Assert.Equal(4, preview.Unscheduled.Count(u => u.Reason == RouteUnscheduledReasonCodes.PeriodExhausted));
        Assert.Empty(preview.Shifted!);
    }

    // ── 5 · weekly capacity and the period in minutes ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Every_week_carries_its_capacity_and_the_period_is_summed_in_minutes()
    {
        var env = SixtySingleDoctorInstitutions();
        env.WorkingDays.Holidays.Add(new DateOnly(2026, 9, 16)); // a holiday in week 2

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Wed2Sep), default)).Preview!;
        var weeks = preview.WeekCapacity!;

        Assert.Equal(5, weeks.Count);
        Assert.Equal(("2026-08-31", 4, 0, 0, 4 * 480, 48 * 30, 48, 16),
            (weeks[0].WeekStart, weeks[0].WorkingDays, weeks[0].HalfDays, weeks[0].Holidays, weeks[0].CapacityMinutes,
                weeks[0].PlannedMinutes, weeks[0].VisitCount, weeks[0].DailyCap));
        Assert.Equal((5, 5 * 480, 12 * 30, 12), (weeks[1].WorkingDays, weeks[1].CapacityMinutes, weeks[1].PlannedMinutes, weeks[1].VisitCount));
        Assert.Equal((4, 1, 4 * 480), (weeks[2].WorkingDays, weeks[2].Holidays, weeks[2].CapacityMinutes));
        Assert.Equal((1, 480), (weeks[4].WorkingDays, weeks[4].CapacityMinutes));

        var period = preview.PeriodCapacity!;
        Assert.Equal(weeks.Sum(w => w.CapacityMinutes), period.CapacityMinutes);
        Assert.Equal(60 * 30, period.PlannedMinutes);
        Assert.Equal(480, period.DailyBudgetMinutes);
        Assert.Equal(16, period.DailyCap);
        Assert.Equal(8, period.HalfDayCap);
        Assert.NotNull(preview.SupplyDemand); // the old summary stays
    }

    // ── 6 · consent ────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_consent_blocked_doctor_is_not_planned_and_an_unknown_one_is_planned_with_a_warning()
    {
        var env = Env.WithTwoDoctors();
        env.Consent.Blocked.Add(env.DoctorA);
        env.Consent.UnknownSubjects.Add(env.DoctorB);

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Wed2Sep), default)).Preview!;

        var blocked = Assert.Single(preview.Unscheduled);
        Assert.Equal((env.DoctorA, PlanningVisitReasons.ConsentBlocked), (blocked.TargetId, blocked.Reason));
        Assert.DoesNotContain(preview.Scheduled, s => s.ContactId == env.DoctorA);
        Assert.Contains(preview.Scheduled, s => s.ContactId == env.DoctorB);
        Assert.Contains(PlanningVisitReasons.ConsentUnknown, preview.Content.Single(c => c.ContactId == env.DoctorB).ReasonCodes);
        Assert.NotEqual(PlanningSessionSupplyDemandStatus.OverPlanned, preview.SupplyDemand.Status); // blocked ≠ over-plan

        var build = await env.Engine.BuildApplyAsync(env.Session, env.Options(Wed2Sep), default);
        Assert.DoesNotContain(build.Atoms, a => a.ContactId == env.DoctorA);
    }

    // ── 7 · the manual order orders a day, never moves a visit to another day ──────────────────────────────────────

    [Fact]
    public async Task The_manual_order_sets_the_order_inside_a_day_and_never_the_day()
    {
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        env.Session.Selection.SelectedContacts.Clear();
        var (x, xDoctors) = AddInstitution(env, "X", 3, 41.00, 29.00);
        var (y, yDoctors) = AddInstitution(env, "Y", 2, 41.01, 29.01);

        var optimum = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;
        var manualOrder = new[] { yDoctors[1], xDoctors[2], yDoctors[0], xDoctors[1], xDoctors[0] };
        var manual = (await env.Engine.PreviewAsync(
            env.Session, new VisitPlanGenerationOptions(EffectiveAt: Saturday5Sep, ManualVisitOrder: manualOrder), default)).Preview!;

        // Same day per visit with or without the manual order.
        Assert.Equal(
            optimum.Scheduled.ToDictionary(s => s.TargetId, s => s.PlannedDate),
            manual.Scheduled.ToDictionary(s => s.TargetId, s => s.PlannedDate));
        Assert.Single(manual.Scheduled.Where(s => s.AccountId == x).Select(s => s.PlannedDate).Distinct());

        // Inside each day the manual sequence holds.
        string Order(VisitPlanPreview p, Guid account) => string.Join(",", p.Scheduled
            .Where(s => s.AccountId == account).OrderBy(s => s.SequenceOrder).Select(s => s.TargetId));
        Assert.Equal(string.Join(",", new[] { xDoctors[2], xDoctors[1], xDoctors[0] }), Order(manual, x));
        Assert.Equal(string.Join(",", new[] { yDoctors[1], yDoctors[0] }), Order(manual, y));
    }

    // ── 4b · the calendar is asked once, then served from the tenant-keyed cache ──────────────────────────────────

    [Fact]
    public async Task The_calendar_cache_answers_a_second_run_without_asking_and_never_crosses_tenants()
    {
        var checker = new FakeWorkingDayChecker();
        checker.HalfDays.Add(new DateOnly(2026, 9, 9));
        var cache = new PlanningCalendarDayCache();
        var period = (await new FakeCyclePeriodReader(Id(30)).GetByIdAsync(Id(30), default))!;
        PlanningWorkingCalendar For(Guid tenant) => new(new FixedCountryResolver("TR"), checker, cache, TenantOf(tenant));
        var from = new DateOnly(2026, 9, 1);
        var to = new DateOnly(2026, 9, 28);

        var first = await For(Tenant).ResolveAsync(period, null, from, to, default);
        Assert.Equal(28, checker.Asked.Count);
        Assert.Equal(PlanningDayKinds.Half, first.KindOf(new DateOnly(2026, 9, 9)));
        Assert.Equal(PlanningDayKinds.Weekend, first.KindOf(new DateOnly(2026, 9, 5)));
        Assert.Equal(new[] { new DateOnly(2026, 9, 9) }, first.HalfDayDates);

        var second = await For(Tenant).ResolveAsync(period, null, from, to, default);
        Assert.Equal(28, checker.Asked.Count);            // nothing asked the second time
        Assert.Equal(first.NonWorkingDates, second.NonWorkingDates);

        await For(Guid.NewGuid()).ResolveAsync(period, null, from, to, default);
        Assert.Equal(56, checker.Asked.Count);            // another tenant is asked afresh

        // A refusal is never cached.
        var refusing = new FakeWorkingDayChecker { Refuse = true };
        var refusedCache = new PlanningCalendarDayCache();
        var calendar = new PlanningWorkingCalendar(new FixedCountryResolver("TR"), refusing, refusedCache, TenantOf(Tenant));
        await calendar.ResolveAsync(period, null, from, to, default);
        await calendar.ResolveAsync(period, null, from, to, default);
        Assert.Equal(2, refusing.Asked.Count);
    }

    [Fact]
    public void A_platform_holiday_is_holiday_and_a_platform_weekend_is_weekend()
    {
        var saturday = new DateOnly(2026, 9, 5);
        var monday = new DateOnly(2026, 9, 7);
        WorkingDayCheckResult Off(params string[] reasons) => new("resolved", false, reasons, "x");

        Assert.Equal(PlanningDayKinds.Holiday, PlanningWorkingCalendar.KindOf(monday, Off("public_holiday")));
        Assert.Equal(PlanningDayKinds.Holiday, PlanningWorkingCalendar.KindOf(saturday, Off("company_closure")));
        Assert.Equal(PlanningDayKinds.Weekend, PlanningWorkingCalendar.KindOf(monday, Off("weekend_from_tenant_override")));
        Assert.Equal(PlanningDayKinds.Weekend, PlanningWorkingCalendar.KindOf(saturday, Off("capacity_ok")));
        Assert.Equal(PlanningDayKinds.Half,
            PlanningWorkingCalendar.KindOf(monday, new WorkingDayCheckResult("resolved", true, new[] { "half_day_treated_as_working" }, "x", IsHalfDay: true)));
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────────

    private static Env SixtySingleDoctorInstitutions()
    {
        var env = Env.WithTwoDoctors();
        env.Session.Selection.SelectedContacts.Clear();
        for (var i = 0; i < 60; i++)
        {
            AddInstitution(env, "Kurum " + i, 1, 41.0, 29.0 + i * 0.001);
        }

        return env;
    }

    /// <summary>An institution with <paramref name="doctors"/> doctors, all selected (and named, for the shift list).</summary>
    private static (Guid Account, List<Guid> Doctors) AddInstitution(Env env, string name, int doctors, double lat, double lng)
    {
        var account = Guid.NewGuid();
        env.Accounts.Rows[account] = new AccountEntity
        {
            Id = account, TenantId = Tenant, AccountName = name, AccountType = "hospital", Latitude = lat, Longitude = lng
        };
        var ids = new List<Guid>();
        for (var i = 0; i < doctors; i++)
        {
            var id = Guid.NewGuid();
            ids.Add(id);
            env.Contacts.Items.Add(new Contact { Id = id, TenantId = Tenant, DisplayName = $"{name} Dr {i}", Status = "active" });
            env.Session.Selection.SelectedContacts.Add(new PlanningSessionSelectedContact { ContactId = id, AccountId = account });
        }

        return (account, ids);
    }
}
