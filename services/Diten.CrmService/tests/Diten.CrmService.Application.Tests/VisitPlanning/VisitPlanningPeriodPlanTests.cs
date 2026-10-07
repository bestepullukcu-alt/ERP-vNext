using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;
using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Application.Features.VisitPlanning.Commands;
using Diten.CrmService.Application.Features.VisitPlanning.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.VisitPlanning.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitPlanning.Queries;
using Diten.CrmService.Application.Tests.VisitScope;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;
using VisitReportEntity = Diten.CrmService.Domain.Entities.VisitReport;

namespace Diten.CrmService.Application.Tests.VisitPlanning;

/// <summary>
/// WP-VP-3A — the period plan on the PRODUCTION engine and handlers (only stores / clock are fakes): one active plan per
/// rep + period, the empty-draft archive, per-week approve (only that week's atoms, not committed, frozen afterwards),
/// the new ReopenPlanningWeek command, the frequency rule (PeriodType units, unknown = 1, pharmacy), the even spread,
/// the week status derivation and the class map of the stored weeks.
/// <para>The shared period is 2026-09-01 (Tue) … 2026-09-28 (Mon): Monday-weeks 31 Aug, 7, 14, 21 and 28 Sep — five
/// weeks, all with working days.</para>
/// </summary>
public sealed partial class VisitPlanningTests
{
    private static readonly DateTimeOffset Wed2Sep = new(2026, 9, 2, 8, 0, 0, TimeSpan.Zero);
    private const string Week7Sep = "2026-09-07";

    private sealed class PinnedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class SessionList : IPlanningSessionRepository
    {
        public List<PlanningSession> Items { get; } = new();

        public Task<PlanningSession?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(s => s.TenantId == tenantId && s.Id == id));

        public Task<IReadOnlyList<PlanningSession>> ListAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PlanningSession>>(Items.Where(s => s.TenantId == tenantId).ToList());

        public Task<IReadOnlyList<PlanningSession>> ListByPeriodAndResourceAsync(
            Guid tenantId, Guid cyclePeriodId, string resourceId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PlanningSession>>(Items
                .Where(s => s.TenantId == tenantId && s.CyclePeriodId == cyclePeriodId && s.ResourceId == resourceId).ToList());

        public Task InsertAsync(PlanningSession entity, CancellationToken ct) { Items.Add(entity); return Task.CompletedTask; }

        public Task<bool> ReplaceAsync(PlanningSession entity, int expectedVersion, CancellationToken ct) => Task.FromResult(true);
    }

    private static CreatePlanningSessionCommand NewPlan(Guid period, string resource = "")
        => new(period, resource, null, null, null, null, null, null, null, null);

    private ApplyPlanningSessionHandler ApplyHandler(Env env, DateTimeOffset now)
        => new(TenantOf(Tenant), new NullActorContext(), new FakePlanningSessionRepository(env.Session), env.UnitOfWork,
            env.Engine, TestCallerScope.Unrestricted("rep-1"), new PinnedClock(now));

    private ReopenPlanningWeekHandler ReopenHandler(Env env, DateTimeOffset now)
        => new(TenantOf(Tenant), new NullActorContext(), new FakePlanningSessionRepository(env.Session), env.PlannedVisits,
            env.Reports, env.Periods, env.UnitOfWork, TestCallerScope.Unrestricted("rep-1"), new PinnedClock(now));

    private static ApplyPlanningSessionCommand ApproveWeek(Env env, string weekStart)
        => new(env.Session.Id, null, null, null, null, null, WeekStart: weekStart);

    /// <summary>Approves <paramref name="weekStart"/> and stores the written atoms (what the unit of work would do).</summary>
    private async Task<List<PlannedVisitEntity>> ApproveAndStoreAsync(Env env, string weekStart, DateTimeOffset now)
    {
        var before = env.UnitOfWork.WrittenAtoms.Count;
        var result = await ApplyHandler(env, now).Handle(ApproveWeek(env, weekStart), default);
        Assert.True(result.IsSuccessful, string.Join(" / ", result.Errors ?? Array.Empty<string>()));
        var atoms = env.UnitOfWork.WrittenAtoms.Skip(before).ToList();
        env.PlannedVisits.Seeded.AddRange(atoms);
        return atoms;
    }

    private static Env WeeklyEnv()
    {
        var env = Env.WithTwoDoctors();
        env.Frequency.RequiredVisitCount = 1;
        env.Frequency.PeriodType = "week"; // once a week ⇒ 5 working weeks ⇒ 5 visits per doctor
        return env;
    }

    // ── 1 · one active plan per rep + period ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_second_plan_for_the_same_rep_and_period_is_409_with_the_existing_id()
    {
        var store = new SessionList();
        var period = Id(30);
        var rep = new CreatePlanningSessionHandler(
            TenantOf(Tenant), new NullActorContext(), store, new TestCallerScope("rep-1"), new NullUserDisplayNameResolver());

        var first = await rep.Handle(NewPlan(period), default);
        Assert.Equal(201, first.StatusCode);

        var second = await rep.Handle(NewPlan(period), default);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal(PlanningSessionErrorCodes.SessionExists, second.Errors![0]);
        Assert.Equal(first.Data, second.Data);
        Assert.Equal(first.Data.ToString(), second.Errors[2]);
        Assert.Single(store.Items);

        // another period, and another rep (a read-all manager planning for rep-2), are not blocked
        Assert.Equal(201, (await rep.Handle(NewPlan(Id(31)), default)).StatusCode);
        var manager = new CreatePlanningSessionHandler(
            TenantOf(Tenant), new NullActorContext(), store, TestCallerScope.Unrestricted("rep-1"), new NullUserDisplayNameResolver());
        Assert.Equal(201, (await manager.Handle(NewPlan(period, "rep-2"), default)).StatusCode);

        // an archived plan does not block a new one
        store.Items.Single(s => s.Id == first.Data).Status = PlanningSessionStatus.Archived;
        Assert.Equal(201, (await rep.Handle(NewPlan(period), default)).StatusCode);
    }

    // ── 2 · only an empty plan may be archived ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Only_a_plan_without_targets_and_without_an_approved_week_can_be_archived()
    {
        var store = new SessionList();
        var empty = new PlanningSession { Id = Guid.NewGuid(), TenantId = Tenant, CyclePeriodId = Id(30), ResourceId = "rep-1" };
        var targeted = new PlanningSession
        {
            Id = Guid.NewGuid(), TenantId = Tenant, CyclePeriodId = Id(31), ResourceId = "rep-1",
            Selection = new PlanningSessionSelection { SelectedPharmacyIds = new List<Guid> { Id(70) } }
        };
        var approved = new PlanningSession
        {
            Id = Guid.NewGuid(), TenantId = Tenant, CyclePeriodId = Id(32), ResourceId = "rep-1",
            Weeks = new List<PlanningWeek> { new() { WeekStart = Week7Sep, Status = PlanningWeekStatus.Approved } }
        };
        store.Items.AddRange(new[] { empty, targeted, approved });
        var handler = new UpdatePlanningSessionSelectionHandler(
            TenantOf(Tenant), new NullActorContext(), store, new TestCallerScope("rep-1"));
        UpdatePlanningSessionSelectionCommand Archive(Guid id)
            => new(id, null, null, null, null, null, null, PlanningSessionStatus.Archived, null);

        Assert.Equal(200, (await handler.Handle(Archive(empty.Id), default)).StatusCode);
        Assert.Equal(PlanningSessionStatus.Archived, empty.Status);

        foreach (var busy in new[] { targeted, approved })
        {
            var refused = await handler.Handle(Archive(busy.Id), default);
            Assert.Equal(409, refused.StatusCode);
            Assert.Equal(PlanningSessionErrorCodes.SessionNotEmpty, refused.Errors![0]);
            Assert.NotEqual(PlanningSessionStatus.Archived, busy.Status);
        }

        var list = (await new ListPlanningSessionsHandler(TenantOf(Tenant), store, new TestCallerScope("rep-1"))
            .Handle(new ListPlanningSessionsQuery(null, null, null), default)).Data!.Items;
        Assert.True(list.Single(i => i.PlanningSessionId == empty.Id).IsEmpty);
        Assert.False(list.Single(i => i.PlanningSessionId == targeted.Id).IsEmpty);
        Assert.Equal(1, list.Single(i => i.PlanningSessionId == approved.Id).ApprovedWeekCount);
    }

    // ── 3 · approve a week ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Approving_a_week_writes_only_that_weeks_atoms_and_does_not_commit_the_plan()
    {
        var env = WeeklyEnv();

        var result = await ApplyHandler(env, Wed2Sep).Handle(ApproveWeek(env, Week7Sep), default);

        Assert.True(result.IsSuccessful);
        var atoms = env.UnitOfWork.WrittenAtoms;
        Assert.Equal(2, atoms.Count); // the two doctors, once each — not the 10 visits of the whole period
        Assert.All(atoms, a => Assert.InRange(a.PlannedDate, new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 13)));
        Assert.NotEqual(PlanningSessionStatus.Committed, env.Session.Status);
        var week = Assert.Single(env.Session.Weeks);
        Assert.Equal(Week7Sep, week.WeekStart);
        Assert.Equal(PlanningWeekStatus.Approved, week.Status);
        Assert.Equal(atoms.Select(a => a.Id).OrderBy(x => x), week.PlannedVisitIds.OrderBy(x => x));
        Assert.Equal(PlanningWeekActions.Approve, Assert.Single(week.History).Action);
        Assert.Equal(Week7Sep, result.Data!.WeekStart);
        Assert.Equal(PlanningWeekStatus.Approved, result.Data.WeekStatus);
    }

    [Theory]
    [InlineData(Week7Sep, "2026-09-02", 409, "week_already_approved")]
    [InlineData(Week7Sep, "2026-09-14", 409, "week_in_past")]
    [InlineData("2026-09-08", "2026-09-02", 400, "invalid_week")]
    [InlineData("2026-10-05", "2026-09-02", 400, "invalid_week")]
    public async Task A_week_approval_is_refused_when_repeated_past_or_not_a_monday_of_the_period(
        string weekStart, string today, int status, string code)
    {
        var env = WeeklyEnv();
        await ApproveAndStoreAsync(env, Week7Sep, Wed2Sep); // week 7 Sep approved first
        var calls = env.UnitOfWork.ApplyCalls;

        var refused = await ApplyHandler(env, DateTimeOffset.Parse(today + "T08:00:00Z"))
            .Handle(ApproveWeek(env, weekStart), default);

        Assert.Equal(status, refused.StatusCode);
        Assert.Equal(code, refused.Errors![0]);
        Assert.Equal(calls, env.UnitOfWork.ApplyCalls); // nothing written
    }

    // ── 4 · an approved week is frozen ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_approved_week_is_frozen_and_counted_after_a_target_change()
    {
        var env = WeeklyEnv();
        var atoms = await ApproveAndStoreAsync(env, Week7Sep, Wed2Sep);

        // Drop doctor B from the targets: the approved week must not change, the rest re-plans.
        env.Session.Selection.SelectedContacts.RemoveAll(c => c.ContactId == env.DoctorB);
        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Wed2Sep), default)).Preview!;

        var week = preview.Scheduled.Where(s => s.WeekStart == Week7Sep).ToList();
        Assert.Equal(atoms.Select(a => a.Id).OrderBy(x => x), week.Select(s => s.VisitRef).OrderBy(x => x));
        Assert.All(week, s => Assert.True(s.IsFixed));
        Assert.Equal(PlanningWeekDisplayStatus.Approved, preview.Weeks!.Single(w => w.WeekStart == Week7Sep).Status);
        Assert.Equal(2, preview.Weeks!.Single(w => w.WeekStart == Week7Sep).VisitCount);

        // Doctor A needs 5 in the period: 1 fixed + 4 new in the other open weeks (31 Aug, 14, 21, 28 Sep).
        var doctorA = preview.Scheduled.Where(s => s.ContactId == env.DoctorA).ToList();
        Assert.Equal(5, doctorA.Count);
        Assert.Equal(4, doctorA.Count(s => !s.IsFixed));
        Assert.DoesNotContain(preview.Scheduled, s => !s.IsFixed && s.ContactId == env.DoctorB);
    }

    // ── 5 · reopen a week ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reopening_cancels_unreported_visits_keeps_reported_ones_and_reapproval_adds_only_the_missing()
    {
        var env = WeeklyEnv();
        var atoms = await ApproveAndStoreAsync(env, Week7Sep, Wed2Sep);
        var reportedA = atoms.Single(a => a.ContactId == env.DoctorA);
        var unreportedB = atoms.Single(a => a.ContactId == env.DoctorB);
        env.Reports.Items.Add(new VisitReportEntity
        {
            Id = Guid.NewGuid(), TenantId = Tenant, PlannedVisitId = reportedA.Id,
            ExecutionOutcome = VisitExecutionOutcome.Completed, ReportStatus = VisitReportStatus.Submitted
        });
        var reopen = ReopenHandler(env, Wed2Sep);

        var shortReason = await reopen.Handle(new ReopenPlanningWeekCommand(env.Session.Id, Week7Sep, "too short", null), default);
        Assert.Equal(400, shortReason.StatusCode);
        Assert.Equal(PlanningSessionErrorCodes.ReopenReasonRequired, shortReason.Errors![0]);

        var notApproved = await reopen.Handle(
            new ReopenPlanningWeekCommand(env.Session.Id, "2026-09-14", "Doktor izinde, hafta yeniden planlanacak", null), default);
        Assert.Equal(409, notApproved.StatusCode);
        Assert.Equal(PlanningSessionErrorCodes.WeekNotApproved, notApproved.Errors![0]);

        var ok = await reopen.Handle(
            new ReopenPlanningWeekCommand(env.Session.Id, Week7Sep, "Doktor izinde, hafta yeniden planlanacak", null), default);

        Assert.Equal(200, ok.StatusCode);
        Assert.Equal(new[] { unreportedB.Id }, ok.Data!.CancelledPlannedVisitIds);
        Assert.Equal(new[] { reportedA.Id }, ok.Data.KeptPlannedVisitIds);
        Assert.Equal(PlannedVisitStatus.Cancelled, unreportedB.PlanStatus);
        Assert.Equal("week_reopened", unreportedB.CancellationReason);
        Assert.NotEqual(PlannedVisitStatus.Cancelled, reportedA.PlanStatus);
        var week = env.Session.WeekOf(Week7Sep)!;
        Assert.Equal(PlanningWeekStatus.Reopened, week.Status);
        Assert.Equal("Doktor izinde, hafta yeniden planlanacak", week.History.Last().Reason);
        Assert.Equal(1, env.UnitOfWork.ReopenCalls);

        // Re-approving the week writes ONLY doctor B's visit; doctor A's reported visit stays and is counted.
        var again = await ApproveAndStoreAsync(env, Week7Sep, Wed2Sep);
        var written = Assert.Single(again);
        Assert.Equal(env.DoctorB, written.ContactId);
        Assert.Equal(new[] { reportedA.Id, written.Id }.OrderBy(x => x), week.PlannedVisitIds.OrderBy(x => x));
        Assert.Equal(new[] { "approve", "reopen", "approve" }, week.History.Select(h => h.Action));
    }

    // ── 6 · apply without weekStart is unchanged ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Apply_without_a_week_still_writes_every_open_week_and_commits()
    {
        var env = WeeklyEnv();

        var result = await ApplyHandler(env, Wed2Sep)
            .Handle(new ApplyPlanningSessionCommand(env.Session.Id, null, null, null, null, null), default);

        Assert.True(result.IsSuccessful);
        Assert.Equal(PlanningSessionStatus.Committed, env.Session.Status);
        Assert.Equal(10, env.UnitOfWork.WrittenAtoms.Count); // 2 doctors × 5 weeks
        Assert.Empty(env.Session.Weeks);
        Assert.Null(result.Data!.WeekStart);
    }

    // ── 7 · visits needed in the period ────────────────────────────────────────────────────────────────────────────

    private static IReadOnlyList<DateOnly> Weekends(DateOnly from, DateOnly to)
        => Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(from.AddDays)
            .Where(d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday).ToList();

    [Theory]
    [InlineData(1, "week", "2026-10-05", "2027-01-03", 13)]   // once a week × 13 working weeks
    [InlineData(2, "month", "2026-10-01", "2026-12-31", 6)]   // twice a month × 3 months
    [InlineData(2, "month", "2026-10-15", "2026-12-10", 6)]   // a partial month counts whole (Oct, Nov, Dec)
    [InlineData(1, "cycle", "2026-10-01", "2026-12-31", 1)]   // once per period
    [InlineData(1, "period", "2026-10-01", "2026-12-31", 1)]  // an unknown PeriodType is the period
    public async Task The_period_needs_the_policy_count_times_its_period_type_units(
        int count, string periodType, string from, string to, int expected)
    {
        var frequency = new FakeFrequencyResolver { RequiredVisitCount = count, PeriodType = periodType };
        var start = DateOnly.Parse(from);
        var end = DateOnly.Parse(to);
        var frame = new PlanningPeriodFrame(start, end, Weekends(start, end));

        var requirement = await new FrequencyExtendPlanner(frequency)
            .ResolveRequirementAsync(PlannedVisitTargetType.Contact, Id(1), Now, frame, default);

        Assert.Equal(expected, requirement.RequiredInPeriod);
        Assert.Equal(FrequencyStatus.Resolved, requirement.FrequencyStatus);
    }

    [Fact]
    public async Task An_unknown_cadence_is_one_visit_flagged_unknown_and_a_pharmacy_is_resolved_as_an_account()
    {
        var frequency = new FakeFrequencyResolver { RequiredVisitCount = null };
        var frame = new PlanningPeriodFrame(new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31), Array.Empty<DateOnly>());
        var planner = new FrequencyExtendPlanner(frequency);

        var doctor = await planner.ResolveRequirementAsync(PlannedVisitTargetType.Contact, Id(1), Now, frame, default);
        var pharmacy = await planner.ResolveRequirementAsync(PlannedVisitTargetType.Pharmacy, Id(70), Now, frame, default);

        Assert.Equal((1, FrequencyStatus.Unknown), (doctor.RequiredInPeriod, doctor.FrequencyStatus));
        Assert.Equal((1, FrequencyStatus.Unknown), (pharmacy.RequiredInPeriod, pharmacy.FrequencyStatus));
        Assert.Equal(FrequencyTargetType.Account, frequency.Asked.Single(q => q.TargetId == Id(70)).TargetType);
    }

    [Fact]
    public async Task In_the_preview_a_pharmacy_without_a_policy_gets_one_visit_and_doctors_carry_their_cadence()
    {
        var env = WeeklyEnv();
        env.Session.Selection.SelectedPharmacyIds.Add(env.AccountA);
        env.Frequency.Unknown.Add(env.AccountA); // no account policy for the pharmacy

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(Wed2Sep), default)).Preview!;

        var pharmacy = Assert.Single(preview.Scheduled, s => s.TargetType == PlannedVisitTargetType.Pharmacy);
        Assert.Equal(FrequencyStatus.Unknown, pharmacy.FrequencyStatus);
        Assert.Equal(1, pharmacy.RequiredVisitCount);
        Assert.All(preview.Scheduled.Where(s => s.ContactId == env.DoctorA), s => Assert.Equal(5, s.RequiredVisitCount));
        Assert.Equal(5, preview.Content.Single(c => c.ContactId == env.DoctorA).RequiredVisitCount);
    }

    // ── 8 · the even spread + the remaining count ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Two_visits_over_thirteen_weeks_are_six_to_seven_weeks_apart_and_a_week_doubles_only_when_needed()
    {
        Assert.Equal(new[] { 0, 6 }, FrequencyExtendPlanner.Distribute(2, 13));
        Assert.Equal(Enumerable.Range(0, 13), FrequencyExtendPlanner.Distribute(13, 13));
        Assert.Equal(new[] { 0, 0, 1 }, FrequencyExtendPlanner.Distribute(3, 2)); // more visits than weeks
        Assert.Empty(FrequencyExtendPlanner.Distribute(0, 13));
    }

    [Fact]
    public async Task The_remaining_visits_subtract_what_is_already_done()
    {
        var env = WeeklyEnv(); // 5 needed per doctor
        var done = new PlannedVisitEntity
        {
            Id = Guid.NewGuid(), TenantId = Tenant, VisitCode = "DONE-1", TargetType = PlannedVisitTargetType.Contact,
            TargetId = env.DoctorA, ContactId = env.DoctorA, AccountId = env.AccountA, PlannedDate = new DateOnly(2026, 9, 1),
            PlanStatus = PlannedVisitStatus.Planned, Source = PlannedVisitSource.Manual,
            Resource = new PlannedVisitResourceRef { ResourceId = "rep-1", ResourceType = "person" }
        };
        env.PlannedVisits.Seeded.Add(done);
        env.Reports.Items.Add(new VisitReportEntity
        {
            Id = Guid.NewGuid(), TenantId = Tenant, PlannedVisitId = done.Id,
            ExecutionOutcome = VisitExecutionOutcome.Completed, ReportStatus = VisitReportStatus.Submitted
        });

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(), default)).Preview!;

        Assert.Equal(4, preview.Scheduled.Count(s => s.ContactId == env.DoctorA)); // 5 − 1 done
        Assert.Equal(5, preview.Scheduled.Count(s => s.ContactId == env.DoctorB));
    }

    // ── 9 · week status ────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_week_is_current_through_its_sunday_and_past_from_the_next_monday_in_utc()
    {
        var periodStart = new DateOnly(2026, 9, 1);
        var periodEnd = new DateOnly(2026, 9, 28);
        Assert.True(PlanningWeekCalendar.TryParseWeek(Week7Sep, periodStart, periodEnd, out var week));
        var approved = new PlanningWeek { WeekStart = Week7Sep, Status = PlanningWeekStatus.Approved };
        var reopened = new PlanningWeek { WeekStart = Week7Sep, Status = PlanningWeekStatus.Reopened };
        var sunday = new DateOnly(2026, 9, 13);
        var monday = new DateOnly(2026, 9, 14);

        Assert.Equal(PlanningWeekDisplayStatus.Approved, PlanningWeekCalendar.Derive(week, sunday, approved, 2));
        Assert.Equal(PlanningWeekDisplayStatus.Draft, PlanningWeekCalendar.Derive(week, sunday, null, 1));
        Assert.Equal(PlanningWeekDisplayStatus.Empty, PlanningWeekCalendar.Derive(week, sunday, null, 0));
        Assert.Equal(PlanningWeekDisplayStatus.Draft, PlanningWeekCalendar.Derive(week, sunday, reopened, 1));
        Assert.Equal(PlanningWeekDisplayStatus.Past, PlanningWeekCalendar.Derive(week, monday, approved, 2));
        Assert.Equal(PlanningWeekDisplayStatus.Past, PlanningWeekCalendar.Derive(week, monday, null, 0));

        // "today" is the UTC day: 23:30 on Sunday in UTC−3 is already Monday in UTC
        Assert.Equal(monday, PlanningWeekCalendar.Today(new DateTimeOffset(2026, 9, 13, 23, 30, 0, TimeSpan.FromHours(-3))));
        Assert.Equal(sunday, PlanningWeekCalendar.Today(new DateTimeOffset(2026, 9, 14, 1, 30, 0, TimeSpan.FromHours(3))));

        // the period's first week starts on the Monday before it; a Tuesday or a Monday outside is not a week
        Assert.True(PlanningWeekCalendar.TryParseWeek("2026-08-31", periodStart, periodEnd, out var first));
        Assert.Equal(periodStart, first.From);
        Assert.False(PlanningWeekCalendar.TryParseWeek("2026-09-08", periodStart, periodEnd, out _));
        Assert.False(PlanningWeekCalendar.TryParseWeek("2026-10-05", periodStart, periodEnd, out _));
        Assert.Equal(5, PlanningWeekCalendar.PeriodWeeks(periodStart, periodEnd).Count);
    }

    [Fact]
    public async Task The_detail_lists_every_week_of_the_period_with_its_status()
    {
        var env = WeeklyEnv();
        await ApproveAndStoreAsync(env, Week7Sep, Wed2Sep);
        var store = new FakePlanningSessionRepository(env.Session);
        var names = new Features.PlannedVisit.VisitTargetNameReader(env.Accounts, env.Contacts);

        var dto = (await new GetPlanningSessionByIdHandler(
                TenantOf(Tenant), store, TestCallerScope.Unrestricted("rep-1"), names, env.Periods, new PinnedClock(Wed2Sep))
            .Handle(new GetPlanningSessionByIdQuery(env.Session.Id), default)).Data!;

        Assert.Equal(new[] { "2026-08-31", "2026-09-07", "2026-09-14", "2026-09-21", "2026-09-28" }, dto.Weeks!.Select(w => w.WeekStart));
        Assert.Equal(PlanningWeekDisplayStatus.Approved, dto.Weeks![1].Status);
        Assert.Equal(2, dto.Weeks[1].VisitCount);
        Assert.Equal(PlanningWeekDisplayStatus.Draft, dto.Weeks[0].Status);
        Assert.Equal(37, dto.Weeks[1].IsoWeek);
    }

    // ── 10 · class map ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_older_session_document_without_weeks_reads_and_saves_and_week_ids_are_strings()
    {
        Diten.CrmService.Persistence.DependencyInjection.EnsureClassMapsForTests();
        var visit = Guid.NewGuid();
        var session = new PlanningSession
        {
            Id = Guid.NewGuid(), TenantId = Tenant, CyclePeriodId = Id(30), ResourceId = "rep-1",
            Weeks = new List<PlanningWeek>
            {
                new()
                {
                    WeekStart = Week7Sep, Status = PlanningWeekStatus.Approved, ApprovedAt = Now, ApprovedBy = "rep",
                    PlannedVisitIds = new List<Guid> { visit }, ManualVisitOrder = new List<Guid> { Id(11) },
                    History = new List<PlanningWeekHistoryEntry> { new() { At = Now, By = "rep", Action = PlanningWeekActions.Approve } }
                }
            }
        };

        var doc = session.ToBsonDocument();
        var storedWeek = doc["Weeks"].AsBsonArray[0].AsBsonDocument;
        Assert.Equal(visit.ToString(), storedWeek["PlannedVisitIds"].AsBsonArray[0].AsString);
        Assert.True(storedWeek["ManualVisitOrder"].AsBsonArray[0].IsString);
        var back = BsonSerializer.Deserialize<PlanningSession>(doc);
        Assert.Equal(visit, Assert.Single(Assert.Single(back.Weeks).PlannedVisitIds));
        Assert.Equal(PlanningWeekActions.Approve, Assert.Single(back.Weeks[0].History).Action);

        doc.Remove("Weeks"); // a document written before WP-VP-3A
        var legacy = BsonSerializer.Deserialize<PlanningSession>(doc);
        Assert.Empty(legacy.Weeks);
        Assert.True(legacy.ToBsonDocument().Contains("Weeks"));
    }
}
