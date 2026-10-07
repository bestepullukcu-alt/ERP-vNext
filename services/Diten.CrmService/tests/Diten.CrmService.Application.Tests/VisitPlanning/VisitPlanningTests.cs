using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.ConsentPreference.Evaluation;
using Diten.CrmService.Application.Features.CycleCapacity.Read;
using Diten.CrmService.Application.Features.CycleCapacity.Services;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.Knowledge;
using Diten.CrmService.Application.Features.Knowledge.Content;
using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney;
using Diten.CrmService.Application.Features.Segmentation;
using Diten.CrmService.Application.Features.PlannedVisit.Provenance;
using Diten.CrmService.Application.Features.RouteOptimization;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.VisitContentSequence;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Queries;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;
using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Application.Features.VisitPlanning.Commands;
using Diten.CrmService.Application.Features.VisitPlanning.Handlers.CommandHandlers;
using Diten.CrmService.Application.Tests.VisitContentSequence;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using AccountEntity = Diten.CrmService.Domain.Entities.Account;
using CapacityEntity = Diten.CrmService.Domain.Entities.CycleCapacity;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Tests.VisitPlanning;

/// <summary>
/// MOD-0155 FU05 — MicroTarget Visit Planning Engine. PURE unit tests over in-memory fakes of every consumed seam (no
/// Mongo, no MongoIntegrationHarness): the status machine (no reverse), the orchestration flow (select → content → route
/// → frequency-extend → supply/demand), preview (persists nothing), apply (builds atoms with Slot/Source=route-plan/
/// SelectionMode=recommended + is all-or-nothing through the unit of work), re-plan (subset in place), territory=warn,
/// supply-demand=warning-not-block, and the selection helpers.
/// </summary>
public sealed partial class VisitPlanningTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Saturday5Sep = new(2026, 9, 5, 8, 0, 0, TimeSpan.Zero);
    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");

    // ── AC-SESSION-2 — the status machine has NO reverse transition ──────────────────────────────────────────────

    [Theory]
    [InlineData(PlanningSessionStatus.Draft, PlanningSessionStatus.Generated, true)]
    [InlineData(PlanningSessionStatus.Generated, PlanningSessionStatus.Committed, true)]
    [InlineData(PlanningSessionStatus.Committed, PlanningSessionStatus.Archived, true)]
    [InlineData(PlanningSessionStatus.Generated, PlanningSessionStatus.Generated, true)] // re-preview is allowed
    [InlineData(PlanningSessionStatus.Committed, PlanningSessionStatus.Draft, false)]    // no reverse
    [InlineData(PlanningSessionStatus.Generated, PlanningSessionStatus.Draft, false)]    // no reverse
    [InlineData(PlanningSessionStatus.Archived, PlanningSessionStatus.Committed, false)] // terminal
    [InlineData(PlanningSessionStatus.Draft, PlanningSessionStatus.Draft, false)]        // same-rank draft not allowed
    public void Status_machine_is_forward_only(string from, string to, bool expected)
        => Assert.Equal(expected, PlanningSessionStatus.CanTransition(from, to));

    [Fact]
    public void Draft_may_reach_committed_directly_via_apply()
        => Assert.True(PlanningSessionStatus.CanTransition(PlanningSessionStatus.Draft, PlanningSessionStatus.Committed));

    // ── AC-FLOW / AC-APPLY — orchestration + preview vs apply ────────────────────────────────────────────────────

    [Fact]
    public async Task Preview_calls_route_optimizer_and_persists_nothing()
    {
        var env = Env.WithTwoDoctors();
        var outcome = await env.Engine.PreviewAsync(env.Session, env.Options(), default);

        Assert.True(outcome.Success);
        Assert.NotNull(outcome.Preview);
        Assert.Equal(2, outcome.Preview!.Scheduled.Count);       // both doctors placed by the fake optimizer
        Assert.True(env.Optimizer.Calls >= 1);                    // FU03 was CALLED, not re-implemented
        Assert.Equal(0, env.UnitOfWork.ApplyCalls);               // preview persisted nothing
        Assert.Empty(env.PlannedVisits.Inserted);
    }

    [Fact]
    public async Task Apply_builds_atoms_with_slot_source_route_plan_and_recommended_selection()
    {
        var env = Env.WithTwoDoctors();
        var build = await env.Engine.BuildApplyAsync(env.Session, env.Options(), default);

        Assert.True(build.Success);
        Assert.Equal(2, build.Atoms.Count);
        foreach (var atom in build.Atoms)
        {
            Assert.Equal(PlannedVisitSource.RoutePlan, atom.Source);                 // FU05 is the route-plan producer
            Assert.Equal(PlannedVisitSelectionMode.Recommended, atom.Selection!.SelectionMode);
            Assert.True(atom.Slot.SequenceOrder is not null);                        // the motor packed a slot
            Assert.False(string.IsNullOrWhiteSpace(atom.Slot.SlotStartTime));
            Assert.Equal(PlannedVisitStatus.Planned, atom.PlanStatus);
            Assert.NotNull(atom.Consent);                                            // derived provenance filled
            Assert.NotNull(atom.Frequency);
        }
    }

    [Fact]
    public async Task Apply_writes_atoms_and_commits_session_atomically()
    {
        var env = Env.WithTwoDoctors();
        var build = await env.Engine.BuildApplyAsync(env.Session, env.Options(), default);

        // Simulate what the handler does with the build result.
        env.Session.Status = PlanningSessionStatus.Committed;
        env.Session.CommittedPlannedVisitIds = build.Atoms.Select(a => a.Id).ToList();
        var committed = await env.UnitOfWork.ApplyAsync(env.Session, env.Session.Version, build.Atoms, default);

        Assert.True(committed);
        Assert.Equal(1, env.UnitOfWork.ApplyCalls);
        Assert.Equal(2, env.UnitOfWork.WrittenAtoms.Count);
        Assert.Equal(PlanningSessionStatus.Committed, env.UnitOfWork.CommittedSession!.Status);
        Assert.Equal(2, env.UnitOfWork.CommittedSession.CommittedPlannedVisitIds.Count);
    }

    [Fact]
    public async Task Apply_is_all_or_nothing_a_failed_write_leaves_no_atoms_and_no_commit()
    {
        var env = Env.WithTwoDoctors();
        env.UnitOfWork.ThrowOnApply = true; // a mid-apply failure
        var build = await env.Engine.BuildApplyAsync(env.Session, env.Options(), default);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await env.UnitOfWork.ApplyAsync(env.Session, env.Session.Version, build.Atoms, default));

        Assert.Empty(env.UnitOfWork.WrittenAtoms);          // nothing committed
        Assert.Null(env.UnitOfWork.CommittedSession);       // session NOT flipped
    }

    // ── AC-WARN — supply-vs-demand is a warning, never a block ───────────────────────────────────────────────────

    [Fact]
    public async Task Unschedulable_visit_is_a_warning_not_a_block_and_apply_is_still_allowed()
    {
        var env = Env.WithTwoDoctors();
        env.Optimizer.UnscheduleCount = 1; // the optimizer cannot fit one visit

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(), default)).Preview!;
        Assert.Single(preview.Unscheduled);
        Assert.Equal(PlanningSessionSupplyDemandStatus.OverPlanned, preview.SupplyDemand.Status);

        // Over-plan never blocks: apply still builds atoms for the visits that DID fit.
        var build = await env.Engine.BuildApplyAsync(env.Session, env.Options(), default);
        Assert.True(build.Success);
        Assert.Single(build.Atoms);
    }

    // ── AC-SELECT — WP-VP-2 (K-4): no segment filter any more; consent gate is excluded-not-dropped ──────────────

    [Fact]
    public async Task A_doctor_outside_the_stored_segment_is_no_longer_dropped()
    {
        var env = Env.WithTwoDoctors();
        env.Session.Selection.SegmentId = Id(77); // an older record's stored segment — read, never used
        env.Segments.Member = false;              // neither doctor is a member of anything

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(), default)).Preview!;
        Assert.Equal(2, preview.Content.Count);   // both doctors are assessed
        Assert.Equal(2, preview.Scheduled.Count); // and placed
    }

    [Fact]
    public async Task Consent_blocked_doctor_is_excluded_not_dropped_with_a_reason()
    {
        var env = Env.WithTwoDoctors();
        env.Consent.Block = true;

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(), default)).Preview!;
        Assert.Equal(2, preview.Content.Count);                 // still surfaced (not dropped)
        Assert.All(preview.Content, c => Assert.True(c.ConsentBlocked));
        Assert.All(preview.Content, c => Assert.False(string.IsNullOrWhiteSpace(c.ConsentReason)));
    }

    // ── AC-EXTEND — frequency-extend weeks 2..n ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Frequency_two_per_period_places_a_second_week()
    {
        var env = Env.WithTwoDoctors();
        env.Frequency.RequiredVisitCount = 2; // cadence = twice in the period

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(), default)).Preview!;
        Assert.True(preview.WeekCount >= 2);
        Assert.Contains(preview.Scheduled, s => s.WeekNumber >= 1); // a doctor repeats into a later week
    }

    [Fact]
    public async Task Frequency_unknown_places_only_the_base_week()
    {
        var env = Env.WithTwoDoctors();
        env.Frequency.RequiredVisitCount = null; // no policy → base week only (default never invented)

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(), default)).Preview!;
        Assert.All(preview.Scheduled, s => Assert.Equal(0, s.WeekNumber));
    }

    // ── AC-REPLAN — subset in place ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Replan_updates_only_the_affected_atoms()
    {
        var env = Env.WithTwoDoctors();
        // Seed two committed atoms (one per doctor) as if a prior apply ran.
        var atomA = env.SeedCommittedAtom(env.DoctorA);
        var atomB = env.SeedCommittedAtom(env.DoctorB);
        env.Session.Status = PlanningSessionStatus.Committed;
        env.Session.CommittedPlannedVisitIds = new List<Guid> { atomA.Id, atomB.Id };

        var build = await env.Engine.BuildReplanAsync(
            env.Session, new[] { env.DoctorA }, env.Options(), default);

        Assert.True(build.Success);
        Assert.Single(build.UpdatedAtoms);                       // only doctor A's atom
        Assert.Equal(atomA.Id, build.UpdatedAtoms[0].Id);
    }

    // ── AC-BOUNDARY — territory is a WARN, not a filter ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Out_of_territory_account_warns_but_is_still_planned()
    {
        var env = Env.WithTwoDoctors();
        env.Session.Selection.SelectedAccountIds = new List<Guid> { env.AccountA }; // no territory assignment seeded

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(), default)).Preview!;
        Assert.Contains(preview.TerritoryWarnings, w => w.AccountId == env.AccountA); // warned
        Assert.Equal(2, preview.Scheduled.Count);                                     // still planned (not filtered)
    }

    // ── WP-SB-3b — per-product content, pending projection, frozen items, mobile contract ─────────────────────────

    [Fact]
    public async Task Two_consecutive_plans_of_a_doctor_tell_stage_0_then_stage_1()
    {
        var kit = new VisitContentTestKit();
        kit.AddProduct("A");
        var env = Env.WithTwoDoctors(kit);
        env.Frequency.RequiredVisitCount = 2; // the doctor recurs in a later week of the SAME run

        var build = await env.Engine.BuildApplyAsync(env.Session, env.Options(), default);
        var doctorA = build.Atoms.Where(a => a.ContactId == env.DoctorA).OrderBy(a => a.PlannedDate).ToList();

        Assert.Equal(2, doctorA.Count);
        Assert.Equal(new[] { 0, 1 }, doctorA.Select(a => a.ContentItems.Single().StageIndex));
        Assert.Equal(new int?[] { 0, 1 }, doctorA.Select(a => a.Content!.StageIndex)); // the singular ref follows
    }

    [Fact]
    public async Task A_stored_pending_plan_before_the_window_projects_the_stage_and_a_cancelled_one_does_not()
    {
        var kit = new VisitContentTestKit();
        var a = kit.AddProduct("A");
        var env = Env.WithTwoDoctors(kit);
        env.SeedPlanWithContent(env.DoctorA, new DateOnly(2026, 8, 25), a, PlannedVisitStatus.Planned);
        env.SeedPlanWithContent(env.DoctorA, new DateOnly(2026, 8, 26), a, PlannedVisitStatus.Cancelled);

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(), default)).Preview!;

        Assert.Equal(1, preview.Content.Single(c => c.ContactId == env.DoctorA).Items!.Single().StageIndex);
        Assert.Equal(0, preview.Content.Single(c => c.ContactId == env.DoctorB).Items!.Single().StageIndex);
        Assert.Equal(1, preview.Scheduled.Single(s => s.ContactId == env.DoctorA).ContentItems!.Single().StageIndex);
    }

    [Fact]
    public async Task Content_items_are_frozen_at_plan_time_and_the_singular_content_is_the_first_promo_item()
    {
        var kit = new VisitContentTestKit();
        kit.AddProduct("N1", role: StrategyProductLineRoles.NonPromo, sortOrder: 5);
        var a = kit.AddProduct("A", sortOrder: 10);
        var env = Env.WithTwoDoctors(kit);

        var atom = (await env.Engine.BuildApplyAsync(env.Session, env.Options(), default)).Atoms
            .First(x => x.ContactId == env.DoctorA);
        var lead = atom.ContentItems.First(i => i.Role == StrategyProductLineRoles.Promo);

        Assert.Equal(2, atom.ContentItems.Count);
        Assert.Equal(a.ProductId, lead.ProductId);
        Assert.Equal((lead.JourneyId, lead.StageId, lead.StageIndex),
            (atom.Content!.JourneyId!.Value, atom.Content.StageId!.Value, atom.Content.StageIndex!.Value));

        // The path is re-released and its steps change afterwards — the plan does not.
        a.Paths[0].PathStatus = KnowledgePathStatuses.Inactive;
        kit.AddPath(a.Paths[0].PathCode, "2.0", publishedAt: VisitContentTestKit.Past.AddDays(5));
        a.Paths[0].Steps.Clear();
        Assert.Equal(("1.0", a.Paths[0].Id, 2), (lead.PathVersion, lead.PathId, lead.Steps.Count));
    }

    [Fact]
    public async Task The_planned_visit_dto_keeps_its_content_ref_and_adds_content_items()
    {
        var kit = new VisitContentTestKit();
        kit.AddProduct("A");
        var env = Env.WithTwoDoctors(kit);
        var atom = (await env.Engine.BuildApplyAsync(env.Session, env.Options(), default)).Atoms[0];

        var detail = Diten.CrmService.Application.Features.PlannedVisit.PlannedVisitMapper.ToDetail(atom);
        var item = Diten.CrmService.Application.Features.PlannedVisit.PlannedVisitMapper.ToListItem(atom);

        Assert.NotNull(detail.Content);
        Assert.Equal(atom.ContentItems[0].JourneyId, detail.Content!.JourneyId);
        Assert.Equal(atom.ContentItems[0].JourneyId, detail.ContentEngagementJourneyId);
        Assert.Equal(atom.ContentItems[0].PathId, detail.ContentItems!.Single().PathId);
        Assert.Single(item.ContentItems!);
    }

    [Fact]
    public void The_mobile_planned_visit_contract_only_gains_trailing_fields()
    {
        static string[] Shape(Type t) => t.GetConstructors().Single().GetParameters().Select(p => p.Name!).ToArray();

        Assert.Equal(new[]
        {
            "JourneyId", "StageId", "StageIndex", "StageCode", "ContentSource", "IsOverridden", "StrategyTemplateId",
            "JourneyDisplayName", "StageDisplayName", "ResolvedAt"
        }, Shape(typeof(Diten.CrmService.Application.Features.PlannedVisit.PlannedVisitContentRefDto)));

        var detail = Shape(typeof(Diten.CrmService.Application.Features.PlannedVisit.PlannedVisitDetailDto));
        // WP-SB-3b added ContentItems; WP-VP-2 (B-8) appended the four read-time name fields after it.
        Assert.Equal(50, detail.Length);
        Assert.Equal(new[] { "ContentItems", "TargetDisplayName", "AccountDisplayName", "ContactDisplayName", "TargetInactive" },
            detail[^5..]);
        Assert.Equal(new[] { "Content", "Selection", "Availability", "Version", "CreatedAt", "CreatedBy", "UpdatedAt", "UpdatedBy" },
            detail[37..45]);

        var list = Shape(typeof(Diten.CrmService.Application.Features.PlannedVisit.PlannedVisitListItemDto));
        Assert.Equal(new[]
        {
            "Version", "CreatedAt", "UpdatedAt", "ContentItems",
            "TargetDisplayName", "AccountDisplayName", "ContactDisplayName", "TargetInactive"
        }, list[^8..]);
    }

    [Fact]
    public async Task Preview_carries_the_doctor_items()
    {
        var kit = new VisitContentTestKit();
        var a = kit.AddProduct("A");
        var env = Env.WithTwoDoctors(kit);

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(), default)).Preview!;

        Assert.All(preview.Content, c => Assert.Equal(a.ProductId, c.Items!.Single().ProductId));
        Assert.All(preview.Scheduled, s => Assert.Equal(a.ProductId, s.ContentItems!.Single().ProductId));
        Assert.Empty(env.PlannedVisits.Inserted);
    }

    // ── helper unit: TerritoryGate + FrequencyExtendPlanner in isolation ─────────────────────────────────────────

    [Fact]
    public async Task TerritoryGate_warns_only_uncovered_accounts()
    {
        var tenant = TenantOf(Tenant);
        var assignments = new FakeAccountTerritoryAssignmentRepository();
        assignments.Covered.Add(Id(1));
        var gate = new TerritoryGate(tenant, assignments);

        var warnings = await gate.WarnAsync(new[] { Id(1), Id(2) }, default);

        Assert.Single(warnings);
        Assert.Equal(Id(2), warnings[0].AccountId);
    }

    [Fact]
    public async Task FrequencyExtend_caps_weeks_at_period_length()
    {
        // WP-VP-3A — the cadence is now "visits in the whole period"; spreading 10 over 3 draft weeks never leaves them
        // (a week takes a second visit only because 10 > 3) and always starts in the first one.
        var frequency = new FakeFrequencyResolver { RequiredVisitCount = 10 };
        var planner = new FrequencyExtendPlanner(frequency);
        var frame = new PlanningPeriodFrame(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 28), Array.Empty<DateOnly>());

        var requirement = await planner.ResolveRequirementAsync(PlannedVisitTargetType.Contact, Id(1), Now, frame, default);
        var weeks = FrequencyExtendPlanner.Distribute(requirement.RequiredInPeriod, weekCount: 3);

        Assert.Equal(10, weeks.Count);
        Assert.All(weeks, w => Assert.InRange(w, 0, 2));
        Assert.Contains(0, weeks);
    }

    // ── WP-VP-FIX-1 — list count, committed lock, working days + calendar (production engine + real FU03 optimizer) ──

    [Fact]
    public async Task List_item_carries_the_pharmacy_count_next_to_the_doctor_count()
    {
        var env = Env.WithTwoDoctors();
        env.Session.Selection.SelectedPharmacyIds = new List<Guid> { Id(71), Id(72), Id(73) };
        var handler = new Diten.CrmService.Application.Features.VisitPlanning.Handlers.QueryHandlers.ListPlanningSessionsHandler(
            TenantOf(Tenant), new FakePlanningSessionRepository(env.Session), Diten.CrmService.Application.Tests.VisitScope.TestCallerScope.Unrestricted());

        var list = await handler.Handle(
            new Diten.CrmService.Application.Features.VisitPlanning.Queries.ListPlanningSessionsQuery(), default);

        var item = Assert.Single(list.Data!.Items);
        Assert.Equal(2, item.SelectedContactCount);
        Assert.Equal(3, item.SelectedPharmacyCount);
    }

    [Fact]
    public async Task A_second_apply_of_a_committed_plan_is_refused_with_409_and_a_machine_code()
    {
        var env = Env.WithTwoDoctors();
        var repository = new FakePlanningSessionRepository(env.Session);
        var handler = new ApplyPlanningSessionHandler(
            TenantOf(Tenant), new NullActorContext(), repository, env.UnitOfWork, env.Engine,
            Diten.CrmService.Application.Tests.VisitScope.TestCallerScope.Unrestricted());
        var command = new ApplyPlanningSessionCommand(env.Session.Id, null, null, null, null, null);

        var first = await handler.Handle(command, default);
        Assert.True(first.IsSuccessful);
        Assert.Equal(PlanningSessionStatus.Committed, env.Session.Status);

        var second = await handler.Handle(command, default);

        Assert.False(second.IsSuccessful);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal(PlanningSessionErrorCodes.AlreadyCommitted, second.Errors![0]);
        Assert.Equal(1, env.UnitOfWork.ApplyCalls); // nothing was written the second time
    }

    [Fact]
    public async Task A_week_that_contains_a_weekend_gets_no_visit_on_saturday_or_sunday()
    {
        // The window opens on Saturday 2026-09-05, so the greedy optimizer's FIRST candidate day is a weekend day.
        // WP-VP-3A — the window now opens on TODAY (TargetWeekStart no longer restricts generation): today = that Saturday.
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-05");

        var outcome = await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default);

        Assert.True(outcome.Success);
        var dates = outcome.Preview!.Scheduled.Select(s => DateOnly.Parse(s.PlannedDate)).ToList();
        Assert.NotEmpty(dates);
        Assert.All(dates, d => Assert.DoesNotContain(d.DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }));
        Assert.Equal(new DateOnly(2026, 9, 7), dates.Min()); // the first working day of the window (Monday)
        Assert.Equal(PlanningCalendarStatuses.Resolved, outcome.Preview.CalendarStatus!.Status);
        Assert.Contains("2026-09-05", outcome.Preview.NonWorkingDates!);
        Assert.Contains("2026-09-06", outcome.Preview.NonWorkingDates!);
    }

    [Fact]
    public async Task A_weekday_holiday_from_the_calendar_gets_no_visit()
    {
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-05");
        env.WorkingDays.Holidays.Add(new DateOnly(2026, 9, 7)); // Monday is a public holiday

        var outcome = await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default);

        Assert.True(outcome.Success);
        var dates = outcome.Preview!.Scheduled.Select(s => DateOnly.Parse(s.PlannedDate)).ToList();
        Assert.NotEmpty(dates);
        Assert.DoesNotContain(new DateOnly(2026, 9, 7), dates);
        Assert.Equal(new DateOnly(2026, 9, 8), dates.Min());
        Assert.Contains("2026-09-07", outcome.Preview.NonWorkingDates!);
        Assert.Equal(PlanningCalendarStatuses.Resolved, outcome.Preview.CalendarStatus!.Status);
        Assert.All(env.WorkingDays.Asked, a => Assert.Equal("TR", a.Country)); // the period's country, asked per day
        Assert.Equal(env.WorkingDays.Asked.Count, env.WorkingDays.Asked.Select(a => a.Date).Distinct().Count()); // once each
    }

    [Fact]
    public async Task An_unreadable_calendar_falls_back_to_saturday_and_sunday_and_says_unresolved()
    {
        var env = Env.WithRealRoute(targetWeekStart: "2026-09-05");
        env.WorkingDays.Holidays.Add(new DateOnly(2026, 9, 7)); // never seen: the calendar refuses
        env.WorkingDays.Refuse = true;

        var outcome = await env.Engine.PreviewAsync(env.Session, env.Options(Saturday5Sep), default);

        Assert.True(outcome.Success);
        var preview = outcome.Preview!;
        Assert.Equal(PlanningCalendarStatuses.Unresolved, preview.CalendarStatus!.Status);
        Assert.Equal(CycleCapacityReasonCodes.CalendarForbidden, preview.CalendarStatus.ReasonCode);
        var dates = preview.Scheduled.Select(s => DateOnly.Parse(s.PlannedDate)).ToList();
        Assert.NotEmpty(dates);
        Assert.All(dates, d => Assert.DoesNotContain(d.DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }));
        Assert.Equal(new DateOnly(2026, 9, 7), dates.Min()); // the unknown holiday is NOT invented
        Assert.Single(env.WorkingDays.Asked); // the first refusal stops the asking
        Assert.All(preview.NonWorkingDates!, d => Assert.Contains(DateOnly.Parse(d).DayOfWeek, new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }));
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────
    // Test environment + in-memory fakes
    // ─────────────────────────────────────────────────────────────────────────────────────────────────────────────

    private sealed class Env
    {
        public Guid DoctorA { get; } = Id(11);
        public Guid DoctorB { get; } = Id(12);
        public Guid AccountA { get; } = Id(21);
        public Guid AccountB { get; } = Id(22);
        public Guid CyclePeriodId { get; } = Id(30);

        public FakeRouteOptimizer Optimizer { get; } = new();
        public FakeConsentEvaluator Consent { get; } = new();
        public FakeSegmentReader Segments { get; } = new();
        public FakeFrequencyResolver Frequency { get; } = new();
        public FakeCyclePeriodReader Periods { get; }
        public FakeCycleCapacityRepository Capacities { get; } = new();
        public FakeAccountRepository Accounts { get; } = new();
        public PlannedVisit.FakeContactRepository Contacts { get; } = new();
        public FakePlannedVisitRepository PlannedVisits { get; } = new();
        public FakeContactAvailabilityRepository Availabilities { get; } = new();
        public FakeAccountRelationshipRepository Relationships { get; } = new();
        public FakeAccountTerritoryAssignmentRepository Territory { get; } = new();
        public FakeApplyUnitOfWork UnitOfWork { get; } = new();
        public FakeWorkingDayChecker WorkingDays { get; } = new();

        /// <summary>WP-VP-3A — visit reports ("done" = a completed report; a reported visit survives a week reopen).</summary>
        public Diten.CrmService.Application.Tests.VisitReport.FakeVisitReportRepository Reports { get; } = new();

        /// <summary>WP-VP-2 (B-3) — the server-derived play / campaign (default: none).</summary>
        public Diten.CrmService.Application.Tests.VisitScope.FixedProvenanceDeriver Deriver { get; } = new();

        public VisitPlanningEngine Engine { get; }
        public PlanningSession Session { get; }

        /// <summary>WP-SB-3b — the content world (play, journeys, paths, progress). Without a product line the resolver
        /// answers no-strategy / no-journey exactly as before.</summary>
        public VisitContentTestKit Kit { get; }

        private Env(VisitContentTestKit? kit, bool realRoute = false)
        {
            Periods = new FakeCyclePeriodReader(CyclePeriodId);
            var tenant = TenantOf(Tenant);
            var actor = new NullActorContext();

            Kit = kit ?? new VisitContentTestKit();
            var resolver = Kit.CreateResolver(tenant, Segments, Capacities);

            var estimator = new CycleCapacityEstimator(new FakeCountryResolver(), new FakeWorkingDayCounter());
            var selector = new EligibleContactSelector(tenant, Consent, Availabilities);
            var extend = new FrequencyExtendPlanner(Frequency);
            var territoryGate = new TerritoryGate(tenant, Territory);

            var journeyProbe = new PlannedVisitJourneyProbe(Kit.Journeys);
            var frequencyProbe = new PlannedVisitFrequencyProbe(Frequency);
            var consentProbe = new PlannedVisitConsentProbe(Consent);
            var availabilityProbe = new PlannedVisitAvailabilityProbe(tenant, Availabilities);

            // WP-VP-FIX-1 — the run's working days come from the (fake) platform calendar for the period's country.
            var calendar = new PlanningWorkingCalendar(new FixedCountryResolver("TR"), WorkingDays);
            IRouteOptimizer optimizer = realRoute
                ? new GreedyTimeWindowRouteOptimizer(new DefaultRouteSettings())
                : Optimizer;

            Engine = new VisitPlanningEngine(
                tenant, actor, Periods, Capacities, estimator, resolver, optimizer, selector, extend,
                territoryGate, Accounts, Contacts, PlannedVisits, journeyProbe, frequencyProbe, consentProbe, availabilityProbe,
                calendar, Deriver, Reports);

            Session = new PlanningSession
            {
                Id = Id(50),
                TenantId = Tenant,
                CyclePeriodId = CyclePeriodId,
                ResourceId = "rep-1",
                ResourceType = PlanningSessionResourceTypes.Person,
                Status = PlanningSessionStatus.Draft,
                Selection = new PlanningSessionSelection
                {
                    SelectedContacts = new List<PlanningSessionSelectedContact>
                    {
                        new() { ContactId = DoctorA, AccountId = AccountA },
                        new() { ContactId = DoctorB, AccountId = AccountB }
                    }
                }
            };

            Accounts.Rows[AccountA] = Account(AccountA, "Clinic A", 41.0, 29.0);
            Accounts.Rows[AccountB] = Account(AccountB, "Clinic B", 41.1, 29.1);

            // WP-SB-3b — a kit's play is the session's play (the default kit has no product line).
            if (kit is not null)
            {
                Session.Provenance.StrategyTemplateId = kit.StrategyId;
                // WP-VP-2 (B-3) — the play now reaches the resolver through the doctor-derived provenance.
                Deriver.Play = new DerivedPlay(kit.StrategyId, null, Array.Empty<Guid>(), null);
            }
        }

        public static Env WithTwoDoctors(VisitContentTestKit? kit = null) => new(kit);

        /// <summary>WP-VP-FIX-1 — the production FU03 optimizer (real day enumeration) over the chosen week.</summary>
        public static Env WithRealRoute(string targetWeekStart)
        {
            var env = new Env(null, realRoute: true);
            env.Session.TargetWeekStart = targetWeekStart;
            return env;
        }

        /// <summary>WP-SB-3b — a stored plan of the doctor that already tells <paramref name="product"/>.</summary>
        public PlannedVisitEntity SeedPlanWithContent(
            Guid contactId, DateOnly date, VisitContentTestKit.Product product, string status)
        {
            var atom = SeedCommittedAtom(contactId);
            atom.PlannedDate = date;
            atom.PlanStatus = status;
            atom.ContentItems.Add(new PlannedVisitContentItem
            {
                ProductId = product.ProductId, JourneyId = product.JourneyId, Role = StrategyProductLineRoles.Promo,
                StageId = product.Stages[0].StageId, StageIndex = 0
            });
            return atom;
        }

        public VisitPlanGenerationOptions Options() => new(EffectiveAt: Now);

        /// <summary>WP-VP-3A — "today" pinned (the horizon starts at today's week).</summary>
        public VisitPlanGenerationOptions Options(DateTimeOffset today) => new(EffectiveAt: today);

        public PlannedVisitEntity SeedCommittedAtom(Guid contactId)
        {
            var atom = new PlannedVisitEntity
            {
                Id = Guid.NewGuid(),
                TenantId = Tenant,
                VisitCode = $"VP-{contactId:N}"[..12],
                TargetType = PlannedVisitTargetType.Contact,
                TargetId = contactId,
                ContactId = contactId,
                AccountId = contactId == DoctorA ? AccountA : AccountB,
                PlannedDate = new DateOnly(2026, 9, 1),
                PlanStatus = PlannedVisitStatus.Planned,
                Source = PlannedVisitSource.RoutePlan
            };
            PlannedVisits.Seeded.Add(atom);
            return atom;
        }

        private static AccountEntity Account(Guid id, string name, double lat, double lng) => new()
        {
            Id = id,
            TenantId = Tenant,
            AccountName = name,
            AccountType = "clinic",
            Latitude = lat,
            Longitude = lng
        };
    }

    private static TenantContext TenantOf(Guid tenantId)
    {
        var t = new TenantContext();
        t.SetTenant(tenantId);
        return t;
    }

    // ── fakes ────────────────────────────────────────────────────────────────────────────────────────────────────

    private sealed class FakeRouteOptimizer : IRouteOptimizer
    {
        public int Calls { get; private set; }
        public int UnscheduleCount { get; set; }

        public RouteOptimizationOutput Optimize(RouteOptimizationInput input)
        {
            Calls++;
            var scheduled = new List<ScheduledVisit>();
            var unscheduled = new List<UnscheduledVisit>();
            var order = 1;
            foreach (var visit in input.Visits)
            {
                if (unscheduled.Count < UnscheduleCount)
                {
                    unscheduled.Add(new UnscheduledVisit(visit.VisitId, RouteUnscheduledReasonCodes.PeriodExhausted));
                    continue;
                }

                scheduled.Add(new ScheduledVisit(
                    visit.VisitId, input.Period.DateFrom, "09:00", "09:30", 10, order++));
            }

            return new RouteOptimizationOutput(scheduled, unscheduled);
        }
    }

    private sealed class FakeCyclePeriodReader : ICyclePeriodReader
    {
        private readonly Guid _periodId;
        public FakeCyclePeriodReader(Guid periodId) => _periodId = periodId;

        public Task<CyclePeriodResolution> ResolveActiveAsync(
            DateTimeOffset at, string? country, Guid? legalEntityId, string? businessUnitId, CancellationToken ct)
            => Task.FromResult(new CyclePeriodResolution("none", null, Array.Empty<Guid>(), null, null));

        public Task<CyclePeriodSnapshot?> GetByIdAsync(Guid cyclePeriodId, CancellationToken ct)
            => Task.FromResult<CyclePeriodSnapshot?>(cyclePeriodId == _periodId
                ? new CyclePeriodSnapshot(
                    _periodId, "C1", "Cycle 1", 2026, 1,
                    new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 9, 28, 0, 0, 0, TimeSpan.Zero),
                    "active", "tenant", null, null, null, null)
                : null);

        public Task<IReadOnlyList<CyclePeriodSnapshot>> GetByIdsAsync(
            IReadOnlyCollection<Guid> ids, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CyclePeriodSnapshot>>(Array.Empty<CyclePeriodSnapshot>());

        public Task<IReadOnlyList<CyclePeriodSnapshot>> ListByYearAsync(
            int year, string? scopeType, string? scopeRef, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CyclePeriodSnapshot>>(Array.Empty<CyclePeriodSnapshot>());
    }

    private sealed class FakeConsentEvaluator : IConsentPreferenceEvaluator
    {
        public bool Block { get; set; }

        /// <summary>WP-VP-3B — per-subject verdicts (blocked / unknown) on top of the global <see cref="Block"/>.</summary>
        public HashSet<Guid> Blocked { get; } = new();
        public HashSet<Guid> UnknownSubjects { get; } = new();

        public Task<ConsentEvaluationResult> EvaluateAsync(
            ConsentEvaluationRequest request, CancellationToken ct)
            => Task.FromResult(new ConsentEvaluationResult(
                Block || Blocked.Contains(request.SubjectId) ? ConsentEligibilityStatus.Blocked
                    : UnknownSubjects.Contains(request.SubjectId) ? ConsentEligibilityStatus.Unknown
                    : ConsentEligibilityStatus.Allowed,
                Block || Blocked.Contains(request.SubjectId) ? ConsentDecision.ConsentBlocked : ConsentDecision.ConsentGranted,
                request.SubjectType, request.SubjectId, request.Channel, request.Purpose, null, null, Now,
                null, Array.Empty<Guid>(), new[] { "reason" }, "selection reason",
                Array.Empty<CandidateConsent>(), Array.Empty<CandidatePreference>(),
                ConsentEvaluationResult.CurrentEvaluatorVersion, Now));
    }

    private sealed class FakeSegmentReader : ISegmentMembershipReader
    {
        public bool Member { get; set; } = true;

        public Task<SegmentMembershipVerdict> IsMemberAsync(
            Guid segmentId, string subjectType, Guid subjectId, DateTimeOffset at, CancellationToken ct)
            => Task.FromResult(new SegmentMembershipVerdict(
                segmentId, 1, subjectType, subjectId,
                Member ? SegmentMembershipVerdicts.Member : SegmentMembershipVerdicts.NotMember,
                Array.Empty<string>(), at));

        public Task<SegmentResolutionResult> ResolveAsync(
            Guid segmentId, DateTimeOffset at, int limit, int offset, CancellationToken ct)
            => Task.FromResult(new SegmentResolutionResult(
                segmentId, 1, "contact", false, at, 0, 0, Array.Empty<SegmentMemberDto>()));
    }

    private sealed class FakeFrequencyResolver : IVisitFrequencyPolicyResolver
    {
        public int? RequiredVisitCount { get; set; }

        /// <summary>WP-VP-3A — the policy's PeriodType (default: the cycle, i.e. "per period").</summary>
        public string PeriodType { get; set; } = "cycle";

        /// <summary>WP-VP-3A — targets with no policy even when <see cref="RequiredVisitCount"/> is set.</summary>
        public HashSet<Guid> Unknown { get; } = new();

        public List<ResolveVisitFrequencyPolicyQuery> Asked { get; } = new();

        public Task<VisitFrequencyResolveResult> ResolveAsync(
            ResolveVisitFrequencyPolicyQuery request, CancellationToken ct)
        {
            Asked.Add(request);
            var count = Unknown.Contains(request.TargetId) ? null : RequiredVisitCount;
            return Task.FromResult(new VisitFrequencyResolveResult(
                count is null ? FrequencyStatus.Unknown : FrequencyStatus.Resolved,
                count is null ? null : Id(60), "F1", "Freq", "reason",
                count, "per-cycle", PeriodType, null, null, null, null, 1, "manual",
                Array.Empty<FrequencyCandidatePolicy>(), Array.Empty<string>()));
        }
    }

    private sealed class FakeCycleCapacityRepository : ICycleCapacityRepository
    {
        public CapacityEntity? Capacity { get; set; }

        public Task<CapacityEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult<CapacityEntity?>(null);

        public Task<CapacityEntity?> GetByCyclePeriodAsync(Guid tenantId, Guid cyclePeriodId, CancellationToken ct)
            => Task.FromResult(Capacity);

        public Task<IReadOnlyList<CapacityEntity>> ListAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CapacityEntity>>(Array.Empty<CapacityEntity>());

        public Task InsertAsync(CapacityEntity entity, CancellationToken ct) => Task.CompletedTask;

        public Task<bool> ReplaceAsync(CapacityEntity entity, int expectedVersion, CancellationToken ct)
            => Task.FromResult(true);
    }

    private sealed class FakeAccountRepository : IAccountRepository
    {
        public Dictionary<Guid, AccountEntity> Rows { get; } = new();

        public Task<AccountEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Rows.TryGetValue(id, out var a) ? a : null);

        public Task<AccountEntity?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct)
            => Task.FromResult<AccountEntity?>(null);

        public Task<bool> ExistsByCodeAsync(Guid tenantId, string code, Guid? excludeId, CancellationToken ct)
            => Task.FromResult(false);

        public Task<(IReadOnlyList<AccountEntity> Items, long Total, long UnfilteredTotal)> ListAsync(
            Guid tenantId, string? search, int page, int pageSize, string? sortBy, string? sortDir,
            IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes,
            IReadOnlyCollection<Guid>? accountIdScope, CancellationToken ct)
            => Task.FromResult(((IReadOnlyList<AccountEntity>)Array.Empty<AccountEntity>(), 0L, 0L));

        public Task<IReadOnlyList<AccountEntity>> GetChildrenAsync(Guid tenantId, Guid parentId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AccountEntity>>(Array.Empty<AccountEntity>());

        public Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid accountId, Guid candidateParentId, CancellationToken ct)
            => Task.FromResult(false);

        public Task InsertAsync(AccountEntity account, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(AccountEntity account, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakePlannedVisitRepository : IPlannedVisitRepository
    {
        public List<PlannedVisitEntity> Seeded { get; } = new();
        public List<PlannedVisitEntity> Inserted { get; } = new();

        public Task<PlannedVisitEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Seeded.FirstOrDefault(p => p.Id == id));

        public Task<IReadOnlyList<PlannedVisitEntity>> ListAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PlannedVisitEntity>>(Seeded.ToList());

        public Task<IReadOnlyList<PlannedVisitEntity>> ListByCodeAsync(Guid tenantId, string code, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PlannedVisitEntity>>(Array.Empty<PlannedVisitEntity>());

        public Task<IReadOnlyList<PlannedVisitEntity>> ListByResourceAndDateAsync(
            Guid tenantId, string resourceId, DateOnly plannedDate, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PlannedVisitEntity>>(Array.Empty<PlannedVisitEntity>());

        public Task<IReadOnlyList<PlannedVisitEntity>> ListByTargetAndDateAsync(
            Guid tenantId, Guid targetId, DateOnly plannedDate, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PlannedVisitEntity>>(Array.Empty<PlannedVisitEntity>());

        public Task<IReadOnlyList<PlannedVisitEntity>> ListFromDateByContentPathsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> pathIds, DateOnly fromDate, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PlannedVisitEntity>>(Seeded
                .Where(x => x.TenantId == tenantId && x.PlannedDate >= fromDate
                            && x.ContentItems.Any(item => pathIds.Contains(item.PathId)))
                .ToList());

        public Task InsertAsync(PlannedVisitEntity entity, CancellationToken ct)
        {
            Inserted.Add(entity);
            return Task.CompletedTask;
        }

        public Task<bool> ReplaceAsync(PlannedVisitEntity entity, int expectedVersion, CancellationToken ct)
            => Task.FromResult(true);
    }

    private sealed class FakeContactAvailabilityRepository : IContactAvailabilityRepository
    {
        public Task<ContactAvailability?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult<ContactAvailability?>(null);

        public Task<IReadOnlyList<ContactAvailability>> ListByLinkAsync(Guid tenantId, Guid linkId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ContactAvailability>>(Array.Empty<ContactAvailability>());

        public Task<IReadOnlyList<ContactAvailability>> ListByContactAsync(Guid tenantId, Guid contactId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ContactAvailability>>(Array.Empty<ContactAvailability>());

        public Task<IReadOnlyList<ContactAvailability>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ContactAvailability>>(Array.Empty<ContactAvailability>());

        public Task InsertAsync(ContactAvailability availability, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(ContactAvailability availability, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeAccountRelationshipRepository : IAccountRelationshipRepository
    {
        public Task<AccountRelationship?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult<AccountRelationship?>(null);

        public Task<bool> ExistsActivePairAsync(
            Guid tenantId, Guid sourceAccountId, Guid targetAccountId, string relationshipType,
            bool includeReverse, Guid? excludeId, CancellationToken ct)
            => Task.FromResult(false);

        public Task<IReadOnlyList<AccountRelationship>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AccountRelationship>>(Array.Empty<AccountRelationship>());

        public Task<IReadOnlyList<AccountRelationship>> ListAllAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AccountRelationship>>(Array.Empty<AccountRelationship>());

        public Task InsertAsync(AccountRelationship relationship, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(AccountRelationship relationship, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeAccountTerritoryAssignmentRepository : IAccountTerritoryAssignmentRepository
    {
        public HashSet<Guid> Covered { get; } = new();

        public Task<AccountTerritoryAssignment?> GetByIdAsync(Guid tenantId, Guid modelId, Guid id, CancellationToken ct)
            => Task.FromResult<AccountTerritoryAssignment?>(null);

        public Task<IReadOnlyList<AccountTerritoryAssignment>> ListByModelAsync(Guid tenantId, Guid modelId, CancellationToken ct)
            => Empty();

        public Task<IReadOnlyList<AccountTerritoryAssignment>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken ct)
            => Empty();

        public Task<IReadOnlyList<AccountTerritoryAssignment>> ListActiveByAccountIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> accountIds, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AccountTerritoryAssignment>>(
                accountIds.Where(Covered.Contains)
                    .Select(id => new AccountTerritoryAssignment { AccountId = id, AssignmentStatus = "active" })
                    .ToList());

        public Task<IReadOnlyList<AccountTerritoryAssignment>> ListActiveByNodesAsync(
            Guid tenantId, IReadOnlyCollection<Guid> nodeIds, CancellationToken ct) => Empty();

        public Task<IReadOnlyList<AccountTerritoryAssignment>> ListActiveByModelIdsAsync(
            Guid tenantId, IReadOnlyCollection<Guid> modelIds, CancellationToken ct) => Empty();

        public Task InsertManyAsync(IReadOnlyCollection<AccountTerritoryAssignment> assignments, CancellationToken ct)
            => Task.CompletedTask;

        public Task UpdateManyAsync(IReadOnlyCollection<AccountTerritoryAssignment> assignments, CancellationToken ct)
            => Task.CompletedTask;

        public Task UpdateAsync(AccountTerritoryAssignment assignment, CancellationToken ct) => Task.CompletedTask;

        public Task CommitApplyAsync(
            IReadOnlyCollection<AccountTerritoryAssignment> ended,
            IReadOnlyCollection<AccountTerritoryAssignment> created, CancellationToken ct) => Task.CompletedTask;

        private static Task<IReadOnlyList<AccountTerritoryAssignment>> Empty()
            => Task.FromResult<IReadOnlyList<AccountTerritoryAssignment>>(Array.Empty<AccountTerritoryAssignment>());
    }

    private sealed class FakeApplyUnitOfWork : IPlanningSessionApplyUnitOfWork
    {
        public int ApplyCalls { get; private set; }
        public bool ThrowOnApply { get; set; }
        public List<PlannedVisitEntity> WrittenAtoms { get; } = new();
        public PlanningSession? CommittedSession { get; private set; }

        public Task<bool> ApplyAsync(
            PlanningSession session, int expectedVersion, IReadOnlyList<PlannedVisitEntity> atoms, CancellationToken ct)
        {
            if (ThrowOnApply)
            {
                throw new InvalidOperationException("simulated mid-apply failure");
            }

            ApplyCalls++;
            WrittenAtoms.AddRange(atoms);
            CommittedSession = session;
            return Task.FromResult(true);
        }

        public Task ReplanAsync(IReadOnlyList<PlannedVisitEntity> atoms, CancellationToken ct) => Task.CompletedTask;

        public int ReopenCalls { get; private set; }
        public List<PlannedVisitEntity> CancelledAtoms { get; } = new();

        public Task<bool> ReopenWeekAsync(
            PlanningSession session, int expectedVersion, IReadOnlyList<PlannedVisitEntity> cancelledAtoms, CancellationToken ct)
        {
            ReopenCalls++;
            CancelledAtoms.AddRange(cancelledAtoms);
            return Task.FromResult(true);
        }
    }

    private sealed class FakeCountryResolver : ICycleCapacityCountryResolver
    {
        public CycleCapacityCountryResolution Resolve(CyclePeriodSnapshot period, string? authoredCountryCode)
            => throw new NotSupportedException("capacity is null in these tests, so the estimator is never invoked");
    }

    private sealed class FixedCountryResolver(string country) : ICycleCapacityCountryResolver
    {
        public CycleCapacityCountryResolution Resolve(CyclePeriodSnapshot period, string? authoredCountryCode)
            => new(country, IsDerived: true, null, null);
    }

    /// <summary>WP-VP-FIX-1 — the platform calendar: Sat/Sun + <see cref="Holidays"/> are non-working; <see cref="Refuse"/>
    /// answers like a 403 (calendar_forbidden).</summary>
    private sealed class FakeWorkingDayChecker : IWorkingDayChecker
    {
        public HashSet<DateOnly> Holidays { get; } = new();
        public bool Refuse { get; set; }
        public List<(string Country, DateOnly Date)> Asked { get; } = new();

        public Task<WorkingDayCheckResult> IsWorkingDayAsync(
            string countryCode, Guid? legalEntityId, DateOnly date, CancellationToken cancellationToken)
        {
            Asked.Add((countryCode, date));
            if (Refuse)
            {
                return Task.FromResult(new WorkingDayCheckResult(
                    CycleCapacityResolutions.CalendarForbidden, null,
                    new[] { CycleCapacityReasonCodes.CalendarForbidden }, "forbidden"));
            }

            var working = date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !Holidays.Contains(date);
            return Task.FromResult(new WorkingDayCheckResult(
                CycleCapacityResolutions.Resolved, working, new[] { CycleCapacityReasonCodes.CapacityOk }, "ok",
                IsHalfDay: working && HalfDays.Contains(date)));
        }

        /// <summary>WP-VP-3B — working days the platform marks as half days.</summary>
        public HashSet<DateOnly> HalfDays { get; } = new();
    }

    private sealed class DefaultRouteSettings : IRouteOptimizationDefaultsProvider
    {
        public RouteOptimizationDefaultsSet Current => RouteOptimizationDefaults.Set;
    }

    private sealed class FakePlanningSessionRepository(PlanningSession session) : IPlanningSessionRepository
    {
        public Task<PlanningSession?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
            => Task.FromResult<PlanningSession?>(tenantId == session.TenantId && id == session.Id ? session : null);

        public Task<IReadOnlyList<PlanningSession>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PlanningSession>>(new[] { session });

        public Task<IReadOnlyList<PlanningSession>> ListByPeriodAndResourceAsync(
            Guid tenantId, Guid cyclePeriodId, string resourceId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<PlanningSession>>(new[] { session });

        public Task InsertAsync(PlanningSession entity, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> ReplaceAsync(PlanningSession entity, int expectedVersion, CancellationToken cancellationToken)
            => Task.FromResult(true);
    }

    private sealed class FakeWorkingDayCounter : IWorkingDayCounter
    {
        public Task<WorkingDayCountResult> CountAsync(
            string countryCode, Guid? legalEntityId, DateOnly from, DateOnly to, CancellationToken ct)
            => throw new NotSupportedException("capacity is null in these tests, so the estimator is never invoked");
    }
}
