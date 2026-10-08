using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;
using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Application.Features.VisitPlanning.Commands;
using Diten.CrmService.Application.Features.VisitPlanning.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.VisitPlanning.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitPlanning.Queries;
using Diten.CrmService.Application.Features.VisitPlanning.TargetStatus;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.VisitPlanning;

/// <summary>
/// WP-VP-4L (4L-BE) — on the PRODUCTION engine, update handler and status reader: an unknown frequency is ONE visit per
/// working week (the same helper for the engine and the 3D reader), and the rep's per-week EXTRA visits (weekExtras on the
/// existing session update) join their week, never move, and are written as extra on approval (Acceptance 1–4).
/// </summary>
public sealed partial class VisitPlanningTests
{
    private const string Week14Sep = "2026-09-14";

    private static UpdatePlanningSessionSelectionCommand Extras(Env env, WeekExtrasInput? extras)
        => new(env.Session.Id, null, null, null, null, null, null, null, null, WeekExtras: extras);

    private static WeekExtrasInput ExtraFor(string week, params Guid[] doctors)
        => new(week, doctors.Select(d => new WeekExtraInput("contact", d, d, null)).ToList());

    // ── 1 · unknown frequency = one visit per working week; the engine and the 3D reader count the same ─────────────

    [Fact]
    public async Task An_unknown_frequency_needs_one_visit_per_working_week_and_the_engine_and_the_status_read_agree()
    {
        var env = Env.WithTwoDoctors();
        env.Frequency.RequiredVisitCount = null; // no policy
        env.Periods.Start = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero); // 13 Monday-weeks: 5 Oct … 28 Dec
        env.Periods.End = new DateTimeOffset(2027, 1, 3, 0, 0, 0, TimeSpan.Zero);

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero)), default)).Preview!;

        foreach (var doctor in new[] { env.DoctorA, env.DoctorB })
        {
            var slots = preview.Scheduled.Where(s => s.ContactId == doctor).ToList();
            Assert.Equal(13, slots.Count);                                     // one in each of the 13 draft weeks
            Assert.Equal(13, slots.Select(s => s.WeekNumber).Distinct().Count());
            Assert.All(slots, s => Assert.Equal((FrequencyStatus.Unknown, 13, FrequencyDefaults.Weekly), (s.FrequencyStatus, s.RequiredVisitCount, s.FrequencyDefault)));
            Assert.Equal(FrequencyDefaults.Weekly, preview.Content.Single(c => c.ContactId == doctor).FrequencyDefault);
        }

        // the 3D status read counts the SAME number for the same period (one helper)
        var period = new ContactStatusPeriod(Guid.NewGuid(), "C1", new DateOnly(2026, 10, 5), new DateOnly(2027, 1, 3));
        Assert.Equal(13, ContactPeriodStatusReader.RequiredInPeriod(null, period));
        Assert.Equal(13, FrequencyDefaults.UnknownRequiredInPeriod(period.Frame));
        Assert.Equal(1, FrequencyDefaults.UnknownPerWeek);
    }

    [Fact]
    public async Task A_weekly_default_visit_its_week_cannot_hold_is_skipped_never_shifted_into_a_chain()
    {
        // The week of 14 Sep: only Monday, a half day, and the rep starts ~154 km away: the visit does not fit that week.
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        env.Frequency.RequiredVisitCount = null;
        env.WorkingDays.HalfDays.Add(new DateOnly(2026, 9, 14));
        foreach (var d in new[] { 15, 16, 17, 18 })
        {
            env.WorkingDays.Holidays.Add(new DateOnly(2026, 9, d));
        }

        env.Session.Selection.SelectedContacts.Clear();
        var (_, far) = AddInstitution(env, "Uzak", 1, 41.0 - 1.385, 29.0);

        var preview = (await env.Engine.PreviewAsync(
            env.Session, env.Options(Saturday5Sep) with { StartLat = 41.0, StartLong = 29.0 }, default)).Preview!;

        var skipped = Assert.Single(preview.Unscheduled, u => u.TargetId == far[0]);
        Assert.Equal(FrequencyDefaults.WeekFullSkipped, skipped.Reason);
        Assert.DoesNotContain(preview.Shifted!, s => s.TargetId == far[0]); // no shift
        var weeks = preview.Scheduled.Where(s => s.ContactId == far[0]).Select(s => s.WeekStart).ToList();
        Assert.Equal(weeks.Distinct().Count(), weeks.Count);                 // never two in one later week
        Assert.DoesNotContain(Week14Sep, weeks);
    }

    // ── 2 · a defined frequency is untouched ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_defined_frequency_is_not_the_weekly_default()
    {
        var env = Env.WithTwoDoctors();
        env.Frequency.RequiredVisitCount = 2;
        env.Frequency.PeriodType = FrequencyPeriodType.Month; // twice a month; 1–28 Sep touches one month ⇒ 2

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Wed2Sep), default)).Preview!;

        var slots = preview.Scheduled.Where(s => s.ContactId == env.DoctorA).ToList();
        Assert.Equal(2, slots.Count);
        Assert.All(slots, s => Assert.Equal((FrequencyStatus.Resolved, 2, (string?)null), (s.FrequencyStatus, s.RequiredVisitCount, s.FrequencyDefault)));
        Assert.Null(preview.Content.Single(c => c.ContactId == env.DoctorA).FrequencyDefault);
    }

    // ── 3 · extra visits: written on a draft week only, of a plan target; joined to the week; approved as extra ─────

    [Fact]
    public async Task Extra_visits_are_written_for_a_plan_target_on_a_draft_week_only()
    {
        var env = Env.WithTwoDoctors();
        var handler = PinHandler(env, Saturday5Sep);

        var notInPlan = await handler.Handle(Extras(env, ExtraFor(Week14Sep, Guid.NewGuid())), default);
        Assert.Equal((400, PlanningWeekExtras.ExtraTargetNotInPlan), (notInPlan.StatusCode, notInPlan.Errors![0]));

        var past = await PinHandler(env, new DateTimeOffset(2026, 9, 21, 8, 0, 0, TimeSpan.Zero))
            .Handle(Extras(env, ExtraFor(Week14Sep, env.DoctorA)), default); // on 21 Sep the week of 14 Sep is over
        Assert.Equal((409, PlanningSessionErrorCodes.WeekInPast), (past.StatusCode, past.Errors![0]));

        env.Session.Weeks.Add(new PlanningWeek { WeekStart = "2026-09-21", Status = PlanningWeekStatus.Approved });
        var approved = await handler.Handle(Extras(env, ExtraFor("2026-09-21", env.DoctorA)), default);
        Assert.Equal(409, approved.StatusCode); // an approved week takes no extra
        Assert.Equal((409, PlanningSessionErrorCodes.WeekAlreadyApproved), (approved.StatusCode, approved.Errors![0]));

        var invalid = await handler.Handle(Extras(env, ExtraFor("2026-09-15", env.DoctorA)), default); // not a Monday
        Assert.Equal((400, PlanningSessionErrorCodes.InvalidWeek), (invalid.StatusCode, invalid.Errors![0]));
        Assert.Empty(env.Session.WeekExtras); // nothing written by a refusal

        var ok = await handler.Handle(Extras(env, ExtraFor(Week14Sep, env.DoctorA, env.DoctorA)), default);
        Assert.Equal(200, ok.StatusCode);
        var stored = Assert.Single(env.Session.WeekExtras); // the same target once
        Assert.Equal((Week14Sep, "contact", env.DoctorA, env.DoctorA, env.AccountA),
            (stored.WeekStart, stored.TargetType, stored.TargetId, stored.ContactId, stored.AccountId)); // the institution from the selection
    }

    [Fact]
    public async Task An_extra_joins_its_week_on_the_preview_and_is_written_as_extra_when_the_week_is_approved()
    {
        var env = Env.WithTwoDoctors(); // once in the period: both doctors in the week of 7 Sep
        env.Session.WeekExtras.Add(new PlanningWeekExtra { WeekStart = Week14Sep, TargetType = "contact", TargetId = env.DoctorA, ContactId = env.DoctorA, AccountId = env.AccountA });

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;

        var slots = preview.Scheduled.Where(s => s.ContactId == env.DoctorA).ToList();
        Assert.Equal((Week7Sep, false), (slots.Single(s => !s.IsExtra).WeekStart, false));
        Assert.Equal(Week14Sep, slots.Single(s => s.IsExtra).WeekStart);
        Assert.All(preview.Scheduled.Where(s => s.ContactId == env.DoctorB), s => Assert.False(s.IsExtra));
        var week = preview.Weeks!.Single(w => w.WeekStart == Week14Sep);
        Assert.Equal(env.DoctorA, Assert.Single(week.ExtraTargets!).ContactId);
        Assert.Empty(preview.Weeks!.Single(w => w.WeekStart == Week7Sep).ExtraTargets!);

        // approval writes it as a planned visit flagged extra; the preview then shows it fixed and still extra
        var written = await ApproveAndStoreAsync(env, Week14Sep, Saturday5Sep);
        var atom = Assert.Single(written);
        Assert.Equal((env.DoctorA, true), (atom.ContactId, atom.Selection!.Extra));
        var after = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;
        var fixedExtra = after.Scheduled.Single(s => s.VisitRef == atom.Id);
        Assert.True(fixedExtra.IsFixed && fixedExtra.IsExtra);
    }

    [Fact]
    public async Task An_extra_never_doubles_a_planned_visit_skips_a_consent_blocked_doctor_and_never_moves_when_it_does_not_fit()
    {
        // already planned that week: no second visit, said on the preview
        var env = Env.WithTwoDoctors();
        env.Session.WeekExtras.Add(new PlanningWeekExtra { WeekStart = Week7Sep, TargetType = "contact", TargetId = env.DoctorA, ContactId = env.DoctorA });
        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;
        Assert.Single(preview.Scheduled, s => s.ContactId == env.DoctorA && s.WeekStart == Week7Sep);
        var warning = Assert.Single(preview.PinWarnings!, w => w.Code == PlanningWeekExtras.ExtraAlreadyPlanned);
        Assert.Equal((Week7Sep, env.DoctorA), (warning.WeekStart, warning.TargetId));

        // consent blocked: never planned, not even as an extra
        env.Session.WeekExtras.Clear();
        env.Consent.Blocked.Add(env.DoctorB);
        env.Session.WeekExtras.Add(new PlanningWeekExtra { WeekStart = Week14Sep, TargetType = "contact", TargetId = env.DoctorB, ContactId = env.DoctorB });
        preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default)).Preview!;
        Assert.DoesNotContain(preview.Scheduled, s => s.ContactId == env.DoctorB);

        // no room in its week (half-day Monday only, a far start): extra_no_room, never moved to another week
        var far = Env.WithRealRoute(targetWeekStart: "2026-09-07");
        far.WorkingDays.HalfDays.Add(new DateOnly(2026, 9, 14));
        foreach (var d in new[] { 15, 16, 17, 18 })
        {
            far.WorkingDays.Holidays.Add(new DateOnly(2026, 9, d));
        }

        far.Session.Selection.SelectedContacts.Clear();
        var (_, doctors) = AddInstitution(far, "Uzak", 1, 41.0 - 1.385, 29.0);
        far.Session.WeekExtras.Add(new PlanningWeekExtra { WeekStart = Week14Sep, TargetType = "contact", TargetId = doctors[0], ContactId = doctors[0] });
        var farPreview = (await far.Engine.PreviewAsync(
            far.Session, far.Options(Saturday5Sep) with { StartLat = 41.0, StartLong = 29.0 }, default)).Preview!;
        Assert.Single(farPreview.Unscheduled, u => u.TargetId == doctors[0] && u.Reason == PlanningWeekExtras.ExtraNoRoom);
        Assert.DoesNotContain(farPreview.Scheduled, s => s.ContactId == doctors[0] && s.IsExtra); // not moved elsewhere
        Assert.DoesNotContain(farPreview.Shifted!, s => s.TargetId == doctors[0]);
    }

    // ── 4 · weekExtras absent keeps, [] clears (that week only); the detail lists them ───────────────────────────

    [Fact]
    public async Task Week_extras_null_keeps_them_and_an_empty_list_clears_only_that_week()
    {
        var env = Env.WithTwoDoctors();
        var handler = PinHandler(env, Saturday5Sep);
        env.Session.WeekExtras.Add(new PlanningWeekExtra { WeekStart = "2026-09-21", TargetType = "contact", TargetId = env.DoctorB, ContactId = env.DoctorB });
        Assert.Equal(200, (await handler.Handle(Extras(env, ExtraFor(Week14Sep, env.DoctorA)), default)).StatusCode);
        Assert.Equal(2, env.Session.WeekExtras.Count);

        Assert.Equal(200, (await handler.Handle(Extras(env, null), default)).StatusCode);
        Assert.Equal(2, env.Session.WeekExtras.Count); // null = keep

        Assert.Equal(200, (await handler.Handle(Extras(env, new WeekExtrasInput(Week14Sep, Array.Empty<WeekExtraInput>())), default)).StatusCode);
        var left = Assert.Single(env.Session.WeekExtras); // [] = clear that week; the other week's extra stays
        Assert.Equal("2026-09-21", left.WeekStart);

        var dto = (await DetailHandler(env, Saturday5Sep).Handle(new GetPlanningSessionByIdQuery(env.Session.Id), default)).Data!;
        Assert.Equal(env.DoctorB, Assert.Single(dto.Weeks!.Single(w => w.WeekStart == "2026-09-21").ExtraTargets!).ContactId);
        Assert.Empty(dto.Weeks!.Single(w => w.WeekStart == Week14Sep).ExtraTargets!);
    }

    [Fact]
    public void The_status_read_says_extra_this_week_only_when_a_week_is_asked()
    {
        var doctor = Guid.NewGuid();
        var session = new PlanningSession();
        session.WeekExtras.Add(new PlanningWeekExtra { WeekStart = Week14Sep, TargetType = "contact", TargetId = doctor, ContactId = doctor });
        var status = new ContactPeriodStatusDto(doctor, 13, FrequencyStatus.Unknown, null, 0, 0, 13, null, true, false,
            Array.Empty<string>(), null, false);

        Assert.Null(TargetStatusPeriods.WithExtraWeek(status, session, null).ExtraThisWeek);
        Assert.True(TargetStatusPeriods.WithExtraWeek(status, session, Week14Sep).ExtraThisWeek);
        Assert.False(TargetStatusPeriods.WithExtraWeek(status, session, "2026-09-21").ExtraThisWeek);
    }
}
