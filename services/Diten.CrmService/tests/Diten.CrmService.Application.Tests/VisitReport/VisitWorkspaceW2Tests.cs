using System.Reflection;
using Diten.CrmService.Api.Controllers.CRM;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.CycleCapacity.Read;
using Diten.CrmService.Application.Features.CycleCapacity.Services;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.PlannedVisit;
using Diten.CrmService.Application.Features.PlannedVisit.Commands;
using Diten.CrmService.Application.Features.PlannedVisit.Contract;
using Diten.CrmService.Application.Features.PlannedVisit.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Application.Features.VisitReport;
using Diten.CrmService.Application.Features.VisitReport.Commands;
using Diten.CrmService.Application.Features.VisitReport.Contract;
using Diten.CrmService.Application.Features.VisitReport.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.VisitReport.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitWorkspace;
using Diten.CrmService.Application.Features.VisitWorkspace.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitWorkspace.Queries;
using Diten.CrmService.Application.Tests.PlannedVisit;
using Diten.CrmService.Application.Tests.VisitScope;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Infrastructure.Authorization;
using Xunit;
using PlanAtom = Diten.CrmService.Domain.Entities.PlannedVisit;
using VisitReportEntity = Diten.CrmService.Domain.Entities.VisitReport;
using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;

namespace Diten.CrmService.Application.Tests.VisitReport;

/// <summary>
/// WP-VW-W2 · W2-BE-a — on the PRODUCTION handlers (in-memory stores, fixed clock, the REAL Platform reason catalog
/// behind the reference seams): A2 reasons from the reference set · A3 cancel · A4 reschedule = a new visit on submit ·
/// A6 the unified calendar · A7 reschedule options · the reason list read · the endpoint keys. (Acceptance 1 = Platform
/// <c>CrmVisitReferenceCatalogTests</c>; 5 = <c>PlannedVisitRuntimeTests</c>.)
/// </summary>
public sealed class VisitWorkspaceW2Tests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PeriodId = Guid.Parse("30000000-0000-0000-0000-000000000030");
    private const string Rep = "rep-1";

    // Today = Thursday 15 Oct 2026, 10:00Z. The period runs 1 Oct – 30 Nov; Friday 23 Oct is a holiday.
    private static readonly DateOnly Today = new(2026, 10, 15);
    private static readonly DateTimeOffset Now = new(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Holiday = new(2026, 10, 23);

    private readonly FakeVisitReportRepository _reports = new();
    private readonly FakePlannedVisitReadRepository _plans = new();
    private readonly FakeVisitReasonSet _reasons = FakeVisitReasonSet.FromCatalog();
    private readonly Sessions _sessions = new();
    private readonly Periods _periods = new();
    private readonly WorkingDays _workingDays = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeContactRepository _contacts = new();

    public VisitWorkspaceW2Tests()
    {
        _workingDays.Holidays.Add(Holiday);
        _sessions.Items.Add(new PlanningSession
        {
            Id = Guid.Parse("50000000-0000-0000-0000-000000000050"), TenantId = Tenant, CyclePeriodId = PeriodId,
            ResourceId = Rep, Status = PlanningSessionStatus.Draft
        });
    }

    // ── wiring ──────────────────────────────────────────────────────────────────────────────────────────────

    private static TenantContext TenantCtx()
    {
        var ctx = new TenantContext();
        ctx.SetTenant(Tenant);
        return ctx;
    }

    private static ICallerScope RepCaller(string resource = Rep)
        => new TestCallerScope(resource, VisitPlanningPermissions.Apply, VisitPlanningPermissions.PlannedVisitManage);

    private static TimeProvider Clock(DateTimeOffset? at = null) => new FixedClock(at ?? Now);

    private VisitWorkspaceDays Days()
        => new(TenantCtx(), _sessions, _periods, new NoCapacity(),
            new PlanningWorkingCalendar(new FixedCountry(), _workingDays), _plans);

    private RecordVisitOutcomeHandler Outcome(ICallerScope? caller = null)
        => new(TenantCtx(), new NullActorContext(), _reports, _plans, caller ?? RepCaller(), Clock(),
            _reasons.Validator(), Days());

    private SubmitVisitReportHandler Submit(FakeVisitRescheduleUnitOfWork? uow = null, DateTimeOffset? at = null)
        => new(TenantCtx(), new NullActorContext(), _reports, _plans, RepCaller(), Clock(at),
            uow ?? new FakeVisitRescheduleUnitOfWork(_reports, _plans), Days());

    private AmendVisitReportHandler Amend()
        => new(TenantCtx(), new NullActorContext(), _reports, _plans, RepCaller());

    private CancelPlannedVisitHandler Cancel()
        => new(TenantCtx(), new NullActorContext(), _plans, RepCaller(), _reasons.Validator(), Clock());

    private GetWorkspaceCalendarHandler Calendar(ICallerScope caller, IWorkspacePlanPreviewSource? previews = null)
    {
        var names = new VisitTargetNameReader(_accounts, _contacts);
        var w1 = new GetVisitCalendarHandler(TenantCtx(), _plans, _reports, caller, names, null, Clock());
        return new GetWorkspaceCalendarHandler(
            TenantCtx(), caller, w1, _plans, _reports, _sessions, Days(), names, previews, Clock());
    }

    private PlanAtom Seed(DateOnly date, string status = PlannedVisitStatus.Planned, string resource = Rep, int? minutes = 30)
    {
        var atom = new PlanAtom
        {
            Id = Guid.NewGuid(), TenantId = Tenant, VisitCode = "VW2-" + (_plans.Items.Count + 1).ToString("000"),
            TargetType = PlannedVisitTargetType.Contact, TargetId = Guid.NewGuid(), AccountId = Guid.NewGuid(),
            PlannedDate = date, PlannedStartTime = "10:30", PlannedDurationMinutes = minutes, PlanStatus = status,
            VisitPurpose = "medical-visit", VisitType = "field-visit",
            Resource = new PlannedVisitResourceRef { ResourceId = resource, ResourceType = "user", DisplayName = "Rep One" },
            ContentItems = new List<PlannedVisitContentItem>
            {
                new() { ProductId = Guid.NewGuid(), ProductCode = "TUTUKON", ProductName = "Tutukon 10 mg", Role = "promo", Order = 1 },
                new() { ProductId = Guid.NewGuid(), ProductCode = "ALFA", ProductName = "Alfa Forte", Role = "reminder", Order = 2 }
            }
        };
        atom.ContactId = atom.TargetId;
        _plans.Items.Add(atom);
        return atom;
    }

    private static RecordVisitOutcomeCommand MissedCmd(Guid plan, string? reason, string? note = null)
        => new(plan, VisitExecutionOutcome.Missed, null, reason, null, null, null, null, note);

    private static RecordVisitOutcomeCommand RescheduleCmd(Guid plan, string? date, string reason = "doctor_unavailable", string? note = null)
        => new(plan, VisitExecutionOutcome.Rescheduled, null, reason, date, null, null, null, note);

    private static SubmitVisitReportCommand FinaliseCmd(Guid plan, string outcome)
        => new(plan, null, null, null, null, null, null, outcome);

    // ═══ A2 · reasons come from the reference set (not the legacy constant list) ═════════════════════════════════

    [Fact]
    public async Task A2_a_set_code_unknown_to_the_legacy_constant_list_is_accepted()
    {
        Assert.DoesNotContain("meeting_conflict", VisitReportReasonCodes.All);
        var plan = Seed(new DateOnly(2026, 10, 14));

        var res = await Outcome().Handle(MissedCmd(plan.Id, "meeting_conflict"), default);

        Assert.True(res.IsSuccessful, string.Join(",", res.Errors ?? []));
        Assert.Equal("meeting_conflict", _reports.Items.Single().ReasonCode);
        Assert.True(_reasons.ValidateCalls > 0);
    }

    [Theory]
    [InlineData("rescheduled_by_doctor")] // legacy constant, INACTIVE in the set
    [InlineData("no_such_reason")]
    public async Task A2_an_inactive_or_unknown_code_is_400_visit_reason_invalid(string code)
    {
        var plan = Seed(new DateOnly(2026, 10, 14));
        var res = await Outcome().Handle(MissedCmd(plan.Id, code), default);
        Assert.Equal(400, res.StatusCode);
        Assert.Contains(VisitWorkspaceErrorCodes.ReasonInvalid, res.Errors!);
        Assert.Empty(_reports.Items);
    }

    [Fact]
    public async Task A2_a_reason_that_does_not_apply_to_the_action_is_400()
    {
        // target_inactive applies to cancel / missed, not to reschedule
        var plan = Seed(new DateOnly(2026, 10, 14));
        Assert.True((await Outcome().Handle(MissedCmd(plan.Id, "target_inactive"), default)).IsSuccessful);

        var other = Seed(Today);
        var res = await Outcome().Handle(RescheduleCmd(other.Id, "2026-10-20", "target_inactive"), default);
        Assert.Equal(400, res.StatusCode);
        Assert.Contains(VisitWorkspaceErrorCodes.ReasonInvalid, res.Errors!);
    }

    [Fact]
    public async Task A2_other_needs_a_note_and_the_note_is_kept()
    {
        var plan = Seed(new DateOnly(2026, 10, 14));
        var bare = await Outcome().Handle(MissedCmd(plan.Id, "other"), default);
        Assert.Equal(400, bare.StatusCode);
        Assert.Contains(VisitWorkspaceErrorCodes.ReasonNoteRequired, bare.Errors!);

        var withNote = await Outcome().Handle(MissedCmd(plan.Id, "other", "Kongre"), default);
        Assert.True(withNote.IsSuccessful);
        Assert.Equal("Kongre", _reports.Items.Single().ReasonNote);
    }

    [Fact]
    public async Task A2_an_unreadable_set_is_503_and_nothing_is_written()
    {
        _reasons.Unavailable = true;
        var plan = Seed(new DateOnly(2026, 10, 14));
        var res = await Outcome().Handle(MissedCmd(plan.Id, "doctor_unavailable"), default);
        Assert.Equal(503, res.StatusCode);
        Assert.Contains(VisitWorkspaceErrorCodes.ReferenceDataUnavailable, res.Errors!);
        Assert.Empty(_reports.Items);
    }

    // ═══ A3 · cancel ══════════════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task A3_cancel_stores_the_reason_code_and_note()
    {
        var plan = Seed(Today.AddDays(1));
        var res = await Cancel().Handle(new CancelPlannedVisitCommand(plan.Id, null, null, "other", "Kongre"), default);

        Assert.True(res.IsSuccessful, string.Join(",", res.Errors ?? []));
        Assert.Equal(PlannedVisitStatus.Cancelled, plan.PlanStatus);
        Assert.Equal("other", plan.CancellationReasonCode);
        Assert.Equal("Kongre", plan.CancellationNote);
    }

    [Fact]
    public async Task A3_cancel_reason_rules_note_and_applies_to()
    {
        var plan = Seed(Today);
        var noNote = await Cancel().Handle(new CancelPlannedVisitCommand(plan.Id, null, null, "other"), default);
        Assert.Equal(400, noNote.StatusCode);
        Assert.Contains(VisitWorkspaceErrorCodes.ReasonNoteRequired, noNote.Errors!);

        var legacy = await Cancel().Handle(new CancelPlannedVisitCommand(plan.Id, null, null, "rescheduled_by_rep"), default);
        Assert.Equal(400, legacy.StatusCode);
        Assert.Contains(VisitWorkspaceErrorCodes.ReasonInvalid, legacy.Errors!);
        Assert.Equal(PlannedVisitStatus.Planned, plan.PlanStatus);
    }

    [Fact]
    public async Task A3_a_past_day_is_409_visit_cancel_past_day()
    {
        var plan = Seed(Today.AddDays(-1));
        var res = await Cancel().Handle(new CancelPlannedVisitCommand(plan.Id, null, null, "doctor_unavailable"), default);
        Assert.Equal(409, res.StatusCode);
        Assert.Contains(VisitWorkspaceErrorCodes.CancelPastDay, res.Errors!);
        Assert.Equal(PlannedVisitStatus.Planned, plan.PlanStatus);
    }

    [Fact]
    public async Task A3_the_legacy_free_text_cancel_still_works_for_one_release()
    {
        var plan = Seed(Today);
        var res = await Cancel().Handle(new CancelPlannedVisitCommand(plan.Id, "doctor called", null), default);
        Assert.True(res.IsSuccessful);
        Assert.Equal("doctor called", plan.CancellationReason);
        Assert.Null(plan.CancellationReasonCode);
        Assert.Equal(0, _reasons.ValidateCalls);
    }

    // ═══ A4 · reschedule = a new planned visit when the rescheduled report is SUBMITTED ═══════════════════════

    [Fact]
    public async Task A4_the_draft_creates_no_visit_and_the_submit_creates_exactly_one_linked_copy()
    {
        var plan = Seed(Today);
        var outcome = await Outcome().Handle(RescheduleCmd(plan.Id, "2026-10-20"), default);
        Assert.True(outcome.IsSuccessful, string.Join(",", outcome.Errors ?? []));
        Assert.Single(_plans.Items); // the draft creates nothing
        Assert.Equal(VisitReportStatus.Draft, _reports.Items.Single().ReportStatus);

        var uow = new FakeVisitRescheduleUnitOfWork(_reports, _plans);
        var submitted = await Submit(uow).Handle(FinaliseCmd(plan.Id, "rescheduled"), default);
        Assert.True(submitted.IsSuccessful, string.Join(",", submitted.Errors ?? []));

        var report = _reports.Items.Single();
        Assert.Equal(VisitReportStatus.Submitted, report.ReportStatus);
        var created = _plans.Items.Single(p => p.Id != plan.Id);
        Assert.Equal(report.RescheduledToPlannedVisitId, created.Id);
        Assert.Equal(plan.Id, created.RescheduledFromPlannedVisitId);
        Assert.Equal(PlannedVisitSource.Reschedule, created.Source);
        Assert.Equal(new DateOnly(2026, 10, 20), created.PlannedDate);
        Assert.Null(created.PlannedStartTime);
        Assert.Equal(PlannedVisitStatus.Planned, created.PlanStatus);
        Assert.Equal((plan.TargetType, plan.TargetId, plan.AccountId, plan.ContactId, plan.Resource.ResourceId),
            (created.TargetType, created.TargetId, created.AccountId, created.ContactId, created.Resource.ResourceId));
        Assert.Equal(plan.ContentItems.Select(i => (i.ProductId, i.ProductName, i.Role)),
            created.ContentItems.Select(i => (i.ProductId, i.ProductName, i.Role)));
        Assert.NotEqual(plan.VisitCode, created.VisitCode);

        // the same submit again (inside the correction window): no second visit
        var again = await Submit(uow).Handle(FinaliseCmd(plan.Id, "rescheduled"), default);
        Assert.True(again.IsSuccessful);
        Assert.Equal(2, _plans.Items.Count);
        Assert.Equal(1, uow.Calls);

        // and it cannot be turned into a completed report
        var completed = await Submit(uow).Handle(new SubmitVisitReportCommand(plan.Id, null, null,
            new VisitReportFeedbackInput("ok", "detaylama-tamamlandi", false, null), null, null, null), default);
        Assert.Equal(409, completed.StatusCode);
        Assert.Contains(VisitWorkspaceErrorCodes.RescheduleAlreadyApplied, completed.Errors!);
    }

    [Theory]
    [InlineData("2026-10-23")] // holiday
    [InlineData("2026-10-24")] // Saturday
    [InlineData("2026-12-02")] // after the period
    [InlineData("2026-10-15")] // today (must be AFTER today)
    public async Task A4_a_holiday_weekend_today_or_out_of_period_date_is_400(string date)
    {
        var plan = Seed(Today);
        var res = await Outcome().Handle(RescheduleCmd(plan.Id, date), default);
        Assert.Equal(400, res.StatusCode);
        Assert.Contains(VisitWorkspaceErrorCodes.RescheduleDateInvalid, res.Errors!);
        Assert.Empty(_reports.Items);
    }

    [Fact]
    public async Task A4_after_the_new_visit_exists_an_amendment_cannot_change_the_date()
    {
        var plan = Seed(Today);
        await Outcome().Handle(RescheduleCmd(plan.Id, "2026-10-20"), default);
        await Submit().Handle(FinaliseCmd(plan.Id, "rescheduled"), default);
        var report = _reports.Items.Single();

        var moved = await Amend().Handle(new AmendVisitReportCommand(
            report.Id, "wrong day", null, null, null, null, null, "2026-10-21"), default);
        Assert.Equal(409, moved.StatusCode);
        Assert.Contains(VisitWorkspaceErrorCodes.RescheduleAlreadyApplied, moved.Errors!);

        var same = await Amend().Handle(new AmendVisitReportCommand(
            report.Id, "note", null, null, null, null, null, "2026-10-20"), default);
        Assert.True(same.IsSuccessful, string.Join(",", same.Errors ?? []));
        Assert.Equal(2, _plans.Items.Count);
    }

    [Fact]
    public async Task A4_when_the_visit_write_fails_the_report_is_not_finalised_and_no_visit_is_kept()
    {
        _reports.CloneOnRead = true; // reads hand out copies, as a database does
        var plan = Seed(Today);
        await Outcome().Handle(RescheduleCmd(plan.Id, "2026-10-20"), default);
        var uow = new FakeVisitRescheduleUnitOfWork(_reports, _plans) { FailVisitInsert = true };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => Submit(uow).Handle(FinaliseCmd(plan.Id, "rescheduled"), default));

        var stored = _reports.Items.Single();
        Assert.Equal(VisitReportStatus.Draft, stored.ReportStatus);
        Assert.Null(stored.RescheduledToPlannedVisitId);
        Assert.Single(_plans.Items);
        Assert.Equal(0, _reports.ReplaceCount); // the unit of work is the only write path
    }

    [Fact]
    public async Task A4_submit_needs_the_recorded_outcome_and_a_missed_submit_finalises_not_done()
    {
        var plan = Seed(new DateOnly(2026, 10, 14));
        var none = await Submit().Handle(FinaliseCmd(plan.Id, "missed"), default);
        Assert.Equal(409, none.StatusCode);
        Assert.Contains(VisitReportErrorCodes.InvalidTransition, none.Errors!);

        await Outcome().Handle(MissedCmd(plan.Id, "doctor_unavailable"), default);
        var done = await Submit().Handle(FinaliseCmd(plan.Id, "missed"), default);
        Assert.True(done.IsSuccessful);
        var report = _reports.Items.Single();
        Assert.Equal(VisitReportStatus.Submitted, report.ReportStatus);
        Assert.Equal(VisitWorkStatus.NotDone, VisitWorkStatus.Derive(plan, report, Now));
        Assert.Single(_plans.Items);
    }

    // ═══ A6 · the unified calendar ════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task A6b_the_calendar_carries_the_pinned_time_of_a_written_and_a_draft_visit()
    {
        // CT wiring after W2-BE-b: a written visit's day pin with a start time, and a draft preview slot's pinned time.
        var written = Seed(new DateOnly(2026, 10, 14));
        _sessions.Items[0].DayPins.Add(new PlanningDayPin
        {
            WeekStart = "2026-10-12", TargetType = written.TargetType, TargetId = written.TargetId,
            Date = "2026-10-14", Scope = PlanningDayPinScopes.Visit, StartTime = "10:30"
        });
        var pinnedDraft = Slot("2026-10-20", 1, isFixed: false) with { IsPinned = true, PinnedTime = "11:15" };
        var preview = new FixedPreview(PreviewWith(
            weeks: new[] { ("2026-10-12", PlanningWeekDisplayStatus.Approved), ("2026-10-19", PlanningWeekDisplayStatus.Draft) },
            scheduled: new[] { pinnedDraft, Slot("2026-10-21", 1, isFixed: false) },
            unscheduledInWeek: 0));

        var res = await Calendar(RepCaller(), preview).Handle(new GetWorkspaceCalendarQuery("2026-10-12", "2026-10-25"), default);
        Assert.True(res.IsSuccessful, string.Join(",", res.Errors ?? []));

        var w = Assert.Single(res.Data!.Visits, v => v.PlannedVisitId == written.Id);
        Assert.Equal((true, "10:30"), (w.IsPinned, w.PinnedTime));
        var drafts = res.Data.Visits.Where(v => v.WorkStatus == VisitWorkspaceLimits.DraftWorkStatus).ToList();
        Assert.Equal((true, "11:15"), (drafts.Single(d => d.PlannedDate == "2026-10-20").IsPinned, drafts.Single(d => d.PlannedDate == "2026-10-20").PinnedTime));
        Assert.Null(drafts.Single(d => d.PlannedDate == "2026-10-21").PinnedTime);
        // CT (live E4) — the ISO week number, not the index inside the window
        Assert.Equal(new[] { 42, 43 }, res.Data.Weeks.OrderBy(x => x.WeekStart).Select(x => x.WeekNumber));
    }

    [Fact]
    public async Task A6_written_visits_and_the_draft_week_preview_together_with_week_states_unplaced_and_holiday()
    {
        // week 1 (12 Oct) approved — a written visit with a submitted report; week 2 (19 Oct) draft — two preview visits.
        var written = Seed(new DateOnly(2026, 10, 14));
        _reports.Items.Add(new VisitReportEntity
        {
            Id = Guid.NewGuid(), TenantId = Tenant, PlannedVisitId = written.Id,
            ExecutionOutcome = VisitExecutionOutcome.Completed, ReportStatus = VisitReportStatus.Submitted,
            ReportedByResourceId = Rep, SubmittedAt = Now.AddDays(-1)
        });
        var preview = new FixedPreview(PreviewWith(
            weeks: new[] { ("2026-10-12", PlanningWeekDisplayStatus.Approved), ("2026-10-19", PlanningWeekDisplayStatus.Draft) },
            scheduled: new[] { Slot("2026-10-20", 1, isFixed: false), Slot("2026-10-21", 1, isFixed: false), Slot("2026-10-14", 0, isFixed: true) },
            unscheduledInWeek: 1));

        var res = await Calendar(RepCaller(), preview).Handle(new GetWorkspaceCalendarQuery("2026-10-12", "2026-10-25"), default);
        Assert.True(res.IsSuccessful, string.Join(",", res.Errors ?? []));
        var dto = res.Data!;

        var w = Assert.Single(dto.Visits, v => v.PlannedVisitId == written.Id);
        Assert.Equal(VisitWorkStatus.Reported, w.WorkStatus);
        Assert.Equal(PlannedVisitSource.Manual, w.Source);
        var drafts = dto.Visits.Where(v => v.WorkStatus == VisitWorkspaceLimits.DraftWorkStatus).ToList();
        Assert.Equal(new[] { "2026-10-20", "2026-10-21" }, drafts.Select(d => d.PlannedDate));
        Assert.All(drafts, d => Assert.Null(d.PlannedVisitId));
        Assert.All(drafts, d => Assert.StartsWith("2026-10-19|contact|", d.PreviewKey));
        Assert.Equal(2, drafts.Select(d => d.PreviewKey).Distinct().Count());

        var w1 = dto.Weeks.Single(x => x.WeekStart == "2026-10-12");
        var w2 = dto.Weeks.Single(x => x.WeekStart == "2026-10-19");
        Assert.Equal(WorkspaceWeekStates.Approved, w1.State);
        Assert.True(w1.CanReopen);
        Assert.False(w1.CanApprove);
        Assert.Equal(WorkspaceWeekStates.Draft, w2.State);
        Assert.True(w2.CanApprove);
        Assert.Equal(1, w2.UnplacedCount);

        var holiday = dto.Days.Single(d => d.Date == "2026-10-23");
        Assert.True(holiday.IsHoliday);
        Assert.Equal(0, holiday.CapacityMinutes);
        var wednesday = dto.Days.Single(d => d.Date == "2026-10-14");
        Assert.Equal(480, wednesday.CapacityMinutes);
        Assert.Equal(30, wednesday.PlannedMinutes);
        Assert.Equal(450, wednesday.FreeMinutes);
    }

    [Fact]
    public async Task A6_a_43_day_window_is_400_and_another_rep_is_403_unless_read_all()
    {
        var tooLong = await Calendar(RepCaller()).Handle(new GetWorkspaceCalendarQuery("2026-10-01", "2026-11-12"), default);
        Assert.Equal(400, tooLong.StatusCode);
        Assert.Contains(VisitReportErrorCodes.CalendarRangeInvalid, tooLong.Errors!);
        Assert.True((await Calendar(RepCaller()).Handle(new GetWorkspaceCalendarQuery("2026-10-01", "2026-11-11"), default)).IsSuccessful);

        Seed(new DateOnly(2026, 10, 14), resource: "rep-2");
        var other = await Calendar(RepCaller()).Handle(new GetWorkspaceCalendarQuery("2026-10-12", "2026-10-18", "rep-2"), default);
        Assert.Equal(403, other.StatusCode);
        Assert.Contains(VisitOwnership.ResourceNotCaller, other.Errors!);

        var manager = await Calendar(TestCallerScope.Unrestricted("manager-1"))
            .Handle(new GetWorkspaceCalendarQuery("2026-10-12", "2026-10-18", "rep-2"), default);
        Assert.True(manager.IsSuccessful);
        Assert.Single(manager.Data!.Visits);
    }

    [Fact]
    public async Task A6_a_rescheduled_pair_and_a_coded_cancel_carry_their_links_and_reason()
    {
        var plan = Seed(Today);
        await Outcome().Handle(RescheduleCmd(plan.Id, "2026-10-20"), default);
        await Submit().Handle(FinaliseCmd(plan.Id, "rescheduled"), default);
        var cancelled = Seed(Today.AddDays(1));
        await Cancel().Handle(new CancelPlannedVisitCommand(cancelled.Id, null, null, "clinic_closed"), default);

        var dto = (await Calendar(RepCaller()).Handle(new GetWorkspaceCalendarQuery("2026-10-12", "2026-10-25"), default)).Data!;
        var old = dto.Visits.Single(v => v.PlannedVisitId == plan.Id);
        var created = dto.Visits.Single(v => v.RescheduledFromPlannedVisitId == plan.Id);
        Assert.Equal(VisitWorkStatus.Rescheduled, old.WorkStatus);
        Assert.Equal(created.PlannedVisitId, old.RescheduledToPlannedVisitId);
        Assert.Equal(PlannedVisitSource.Reschedule, created.Source);
        Assert.Equal(new[] { "Tutukon 10 mg", "Alfa Forte" }, created.PlannedContent.Select(c => c.ProductName));
        var c = dto.Visits.Single(v => v.PlannedVisitId == cancelled.Id);
        Assert.Equal("clinic_closed", c.CancellationReasonCode);
        Assert.Equal(VisitWorkStatus.Cancelled, c.WorkStatus);
    }

    // ═══ A7 · reschedule options ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task A7_the_next_8_working_days_skip_the_holiday_and_the_weekend_with_the_load()
    {
        var plan = Seed(Today);
        Seed(new DateOnly(2026, 10, 19));
        Seed(new DateOnly(2026, 10, 19), minutes: 45);
        Seed(new DateOnly(2026, 10, 20), status: PlannedVisitStatus.Cancelled); // cancelled = no load

        var res = await new GetRescheduleOptionsHandler(TenantCtx(), _plans, RepCaller(), Days(), Clock())
            .Handle(new GetRescheduleOptionsQuery(plan.Id), default);

        Assert.True(res.IsSuccessful);
        Assert.Equal(
            new[] { "2026-10-16", "2026-10-19", "2026-10-20", "2026-10-21", "2026-10-22", "2026-10-26", "2026-10-27", "2026-10-28" },
            res.Data!.Days.Select(d => d.Date));
        var monday = res.Data.Days.Single(d => d.Date == "2026-10-19");
        Assert.Equal((2, 75, 480), (monday.PlannedCount, monday.PlannedMinutes, monday.CapacityMinutes));
        Assert.Equal(0, res.Data.Days.Single(d => d.Date == "2026-10-20").PlannedCount);
        Assert.All(res.Data.Days, d => Assert.False(d.IsHoliday));

        // every listed day passes the reschedule rule
        foreach (var day in res.Data.Days)
        {
            Assert.True(await Days().CanRescheduleToAsync(Rep, DateOnly.Parse(day.Date), Today, default), day.Date);
        }

        var foreign = Seed(Today, resource: "rep-2");
        Assert.Equal(404, (await new GetRescheduleOptionsHandler(TenantCtx(), _plans, RepCaller(), Days(), Clock())
            .Handle(new GetRescheduleOptionsQuery(foreign.Id), default)).StatusCode);
    }

    [Fact]
    public async Task A7b_a_draft_week_day_carries_the_draft_plan_load()
    {
        // CT (live E4) — a draft week's day must not look empty: the preview day minutes and the preview visits count.
        var plan = Seed(Today);
        Seed(new DateOnly(2026, 10, 20), status: PlannedVisitStatus.Cancelled); // cancelled = no load
        var preview = PreviewWith(
            weeks: new[] { ("2026-10-19", PlanningWeekDisplayStatus.Draft) },
            scheduled: new[] { Slot("2026-10-20", 1, isFixed: false), Slot("2026-10-20", 1, isFixed: false), Slot("2026-10-20", 1, isFixed: true) },
            unscheduledInWeek: 0) with
        {
            Days = new[] { new PlanningDayPreview("2026-10-20", "2026-10-19", PlanningDayKinds.Working, 480, 300, 180, false) }
        };

        var res = await new GetRescheduleOptionsHandler(
                TenantCtx(), _plans, RepCaller(), Days(), Clock(), _sessions, new FixedPreview(preview))
            .Handle(new GetRescheduleOptionsQuery(plan.Id), default);

        Assert.True(res.IsSuccessful);
        var tuesday = res.Data!.Days.Single(d => d.Date == "2026-10-20");
        Assert.Equal((2, 300, 480), (tuesday.PlannedCount, tuesday.PlannedMinutes, tuesday.CapacityMinutes));
        var friday = res.Data.Days.Single(d => d.Date == "2026-10-16"); // not a draft-week day: the written load only
        Assert.Equal(0, friday.PlannedCount);
    }

    // ═══ reasons read + contract + keys ═════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Reasons_are_the_active_set_values_for_the_action_in_the_requested_language()
    {
        var handler = new GetVisitReasonsHandler(TenantCtx(), _reasons);

        var tr = (await handler.Handle(new GetVisitReasonsQuery("reschedule", "tr-TR,tr;q=0.9"), default)).Data!;
        Assert.Equal("tr", tr.Language);
        Assert.DoesNotContain(tr.Items, i => i.Code == "target_inactive");          // applies to cancel / missed only
        Assert.DoesNotContain(tr.Items, i => i.Code.StartsWith("rescheduled_by")); // inactive legacy
        Assert.Equal("Diğer", tr.Items.Single(i => i.Code == "other").Label);
        Assert.True(tr.Items.Single(i => i.Code == "other").RequiresNote);
        Assert.Equal(7, tr.Items.Count);

        var cancel = (await handler.Handle(new GetVisitReasonsQuery("cancel", "ar"), default)).Data!;
        Assert.Equal(8, cancel.Items.Count);
        Assert.Equal("أخرى", cancel.Items.Single(i => i.Code == "other").Label);
        var en = (await handler.Handle(new GetVisitReasonsQuery("missed", null), default)).Data!;
        Assert.Equal("Other", en.Items.Single(i => i.Code == "other").Label);

        Assert.Equal(400, (await handler.Handle(new GetVisitReasonsQuery("bogus", "tr"), default)).StatusCode);
        _reasons.Unavailable = true;
        var down = await handler.Handle(new GetVisitReasonsQuery("cancel", "tr"), default);
        Assert.Equal(503, down.StatusCode);
        Assert.Contains(VisitWorkspaceErrorCodes.ReferenceDataUnavailable, down.Errors!);
    }

    [Fact]
    public async Task Contract_publishes_draft_the_reason_set_and_the_limits()
    {
        var c = (await new GetVisitWorkspaceContractHandler(TenantCtx()).Handle(new GetVisitWorkspaceContractQuery(), default)).Data!;
        Assert.Equal(VisitWorkStatus.All.Append("draft"), c.WorkStatuses);
        Assert.Equal("visit-outcome-reason", c.ReasonSet);
        Assert.Equal((8, 42), (c.RescheduleOptionDays, c.MaxWindowDays));
        Assert.Contains("reschedule", c.Sources);
        Assert.Contains("unplanned", c.Sources);
        Assert.Contains(VisitWorkspaceErrorCodes.RescheduleAlreadyApplied, c.ErrorCodes);
        Assert.DoesNotContain("draft", VisitWorkStatus.All); // the W1 vocabulary is unchanged
    }

    [Fact]
    public void Every_workspace_endpoint_needs_both_read_keys()
    {
        var actions = typeof(VisitWorkspaceController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.Equal(4, actions.Length);
        Assert.All(actions, a => Assert.Equal(
            new[] { "crm.visit-plan.read", "crm.visit-report.read" },
            a.GetCustomAttributes<HasPermissionAttribute>().Select(p => p.Permission).OrderBy(p => p)));
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    private static VisitPlanPreview PreviewWith(
        (string WeekStart, string Status)[] weeks, PlannedSlotPreview[] scheduled, int unscheduledInWeek)
        => new(
            Guid.Parse("50000000-0000-0000-0000-000000000050"), PeriodId, Rep, "2026-10-01", "2026-11-30", weeks.Length,
            scheduled,
            Enumerable.Range(0, unscheduledInWeek)
                .Select(_ => new UnscheduledPreview(1, PlannedVisitTargetType.Contact, Guid.NewGuid(), null, "capacity_full")).ToList(),
            Array.Empty<DoctorContentPreview>(), Array.Empty<TerritoryWarning>(),
            new SupplyDemandSummary(null, 0, scheduled.Length, unscheduledInWeek, "ok", Array.Empty<string>()), Now,
            Weeks: weeks.Select(w => new PlanningWeekDto(w.WeekStart, 42, w.WeekStart, w.WeekStart, w.Status, null)).ToList());

    private static PlannedSlotPreview Slot(string date, int week, bool isFixed)
    {
        var doctor = Guid.NewGuid();
        return new PlannedSlotPreview(
            Guid.NewGuid(), week, PlannedVisitTargetType.Contact, doctor, Guid.NewGuid(), doctor, null, date, "09:00", "09:30",
            1, 30, null, null, null, 1, 0, "resolved",
            WeekStart: PlanningWeekCalendar.MondayOf(DateOnly.Parse(date)).ToString("yyyy-MM-dd"), IsFixed: isFixed);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FixedPreview(VisitPlanPreview preview) : IWorkspacePlanPreviewSource
    {
        public Task<VisitPlanPreview?> PreviewAsync(PlanningSession session, CancellationToken cancellationToken)
            => Task.FromResult<VisitPlanPreview?>(preview);
    }

    private sealed class Sessions : IPlanningSessionRepository
    {
        public List<PlanningSession> Items { get; } = new();

        public Task<PlanningSession?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(s => s.Id == id));

        public Task<IReadOnlyList<PlanningSession>> ListAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PlanningSession>>(Items.Where(s => s.TenantId == tenantId).ToList());

        public Task<IReadOnlyList<PlanningSession>> ListByPeriodAndResourceAsync(
            Guid tenantId, Guid cyclePeriodId, string resourceId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PlanningSession>>(Items
                .Where(s => s.CyclePeriodId == cyclePeriodId && s.ResourceId == resourceId).ToList());

        public Task InsertAsync(PlanningSession entity, CancellationToken ct) => Task.CompletedTask;

        public Task<bool> ReplaceAsync(PlanningSession entity, int expectedVersion, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class Periods : ICyclePeriodReader
    {
        public Task<CyclePeriodResolution> ResolveActiveAsync(
            DateTimeOffset at, string? country, Guid? legalEntityId, string? businessUnitId, CancellationToken ct)
            => Task.FromResult(new CyclePeriodResolution("none", null, Array.Empty<Guid>(), null, null));

        public Task<CyclePeriodSnapshot?> GetByIdAsync(Guid cyclePeriodId, CancellationToken ct)
            => Task.FromResult<CyclePeriodSnapshot?>(cyclePeriodId == PeriodId
                ? new CyclePeriodSnapshot(PeriodId, "C10", "Oct-Nov", 2026, 10,
                    new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 11, 30, 0, 0, 0, TimeSpan.Zero),
                    "active", "tenant", null, null, null, null)
                : null);

        public Task<IReadOnlyList<CyclePeriodSnapshot>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CyclePeriodSnapshot>>(Array.Empty<CyclePeriodSnapshot>());

        public Task<IReadOnlyList<CyclePeriodSnapshot>> ListByYearAsync(int year, string? scopeType, string? scopeRef, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CyclePeriodSnapshot>>(Array.Empty<CyclePeriodSnapshot>());
    }

    private sealed class NoCapacity : ICycleCapacityRepository
    {
        public Task<CapacityEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => Task.FromResult<CapacityEntity?>(null);

        public Task<CapacityEntity?> GetByCyclePeriodAsync(Guid tenantId, Guid cyclePeriodId, CancellationToken ct)
            => Task.FromResult<CapacityEntity?>(null);

        public Task<IReadOnlyList<CapacityEntity>> ListAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CapacityEntity>>(Array.Empty<CapacityEntity>());

        public Task InsertAsync(CapacityEntity entity, CancellationToken ct) => Task.CompletedTask;

        public Task<bool> ReplaceAsync(CapacityEntity entity, int expectedVersion, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class FixedCountry : ICycleCapacityCountryResolver
    {
        public CycleCapacityCountryResolution Resolve(CyclePeriodSnapshot period, string? authoredCountryCode)
            => new("TR", IsDerived: true, null, null);
    }

    private sealed class WorkingDays : IWorkingDayChecker
    {
        public HashSet<DateOnly> Holidays { get; } = new();

        public Task<WorkingDayCheckResult> IsWorkingDayAsync(
            string countryCode, Guid? legalEntityId, DateOnly date, CancellationToken cancellationToken)
        {
            if (Holidays.Contains(date))
            {
                return Task.FromResult(new WorkingDayCheckResult(
                    CycleCapacityResolutions.Resolved, false, new[] { "public_holiday" }, "Republic Day"));
            }

            var working = date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
            return Task.FromResult(new WorkingDayCheckResult(
                CycleCapacityResolutions.Resolved, working, working ? new[] { "ok" } : new[] { "weekend" }, "ok"));
        }
    }
}
