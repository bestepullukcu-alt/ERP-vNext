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
public sealed class VisitPlanningTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 0, 0, 0, TimeSpan.Zero);
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

    // ── AC-SELECT — segment filters, consent gate is excluded-not-dropped ────────────────────────────────────────

    [Fact]
    public async Task Segment_non_member_is_not_offered()
    {
        var env = Env.WithTwoDoctors();
        env.Session.Selection.SegmentId = Id(77);
        env.Segments.Member = false; // neither doctor is a member

        var preview = (await env.Engine.PreviewAsync(env.Session, env.Options(), default)).Preview!;
        Assert.Empty(preview.Content); // segment NARROWED the universe to nobody
        Assert.Empty(preview.Scheduled);
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
        var frequency = new FakeFrequencyResolver { RequiredVisitCount = 10 };
        var planner = new FrequencyExtendPlanner(frequency);

        var result = await planner.ResolveWeeksAsync(
            PlannedVisitTargetType.Contact, Id(1), null, null, Now, weekCount: 3, default);

        Assert.All(result.WeekIndices, w => Assert.InRange(w, 0, 2));
        Assert.Contains(0, result.WeekIndices);
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

        public VisitPlanningEngine Engine { get; }
        public PlanningSession Session { get; }

        private Env()
        {
            Periods = new FakeCyclePeriodReader(CyclePeriodId);
            var tenant = TenantOf(Tenant);
            var actor = new NullActorContext();

            var resolver = new VisitContentSequenceResolver(
                tenant, new FakeStrategyReader(), Segments, new FakeJourneyReader(),
                new FakeContentReader(), Capacities);

            var estimator = new CycleCapacityEstimator(new FakeCountryResolver(), new FakeWorkingDayCounter());
            var selector = new EligibleContactSelector(tenant, Segments, Consent, Availabilities);
            var extend = new FrequencyExtendPlanner(Frequency);
            var territoryGate = new TerritoryGate(tenant, Territory);

            var journeyProbe = new PlannedVisitJourneyProbe(new FakeJourneyReader());
            var frequencyProbe = new PlannedVisitFrequencyProbe(Frequency);
            var consentProbe = new PlannedVisitConsentProbe(Consent);
            var availabilityProbe = new PlannedVisitAvailabilityProbe(tenant, Availabilities);

            Engine = new VisitPlanningEngine(
                tenant, actor, Periods, Capacities, estimator, resolver, Optimizer, selector, extend,
                territoryGate, Accounts, Contacts, PlannedVisits, journeyProbe, frequencyProbe, consentProbe, availabilityProbe);

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
        }

        public static Env WithTwoDoctors() => new();

        public VisitPlanGenerationOptions Options() => new(EffectiveAt: Now);

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

        public Task<ConsentEvaluationResult> EvaluateAsync(
            ConsentEvaluationRequest request, CancellationToken ct)
            => Task.FromResult(new ConsentEvaluationResult(
                Block ? ConsentEligibilityStatus.Blocked : ConsentEligibilityStatus.Allowed,
                Block ? ConsentDecision.ConsentBlocked : ConsentDecision.ConsentGranted,
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

        public Task<VisitFrequencyResolveResult> ResolveAsync(
            ResolveVisitFrequencyPolicyQuery request, CancellationToken ct)
            => Task.FromResult(new VisitFrequencyResolveResult(
                RequiredVisitCount is null ? FrequencyStatus.Unknown : FrequencyStatus.Resolved,
                RequiredVisitCount is null ? null : Id(60), "F1", "Freq", "reason",
                RequiredVisitCount, "per-cycle", "cycle", null, null, null, null, 1, "manual",
                Array.Empty<FrequencyCandidatePolicy>(), Array.Empty<string>()));
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
    }

    // Content-resolution seams — the engine passes no strategy/segment id by default, so ResolveBindings returns null
    // (NoStrategy) and these are never actually queried; they exist only to satisfy the resolver's constructor.
    private sealed class FakeStrategyReader : IStrategyTemplateReader
    {
        public Task<StrategyTemplateBindingSet?> GetActiveBindingsAsync(Guid templateId, DateTimeOffset at, CancellationToken ct)
            => Task.FromResult<StrategyTemplateBindingSet?>(null);

        public Task<IReadOnlyList<StrategyTemplateSummary>> ListBySegmentAsync(Guid segmentId, DateTimeOffset at, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<StrategyTemplateSummary>>(Array.Empty<StrategyTemplateSummary>());
    }

    private sealed class FakeJourneyReader : IContentEngagementJourneyReader
    {
        public Task<IReadOnlyList<ContentEngagementJourneyDto>> ResolvePublishedJourneysAsync(
            ContentEngagementJourneyCriteria criteria, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ContentEngagementJourneyDto>>(Array.Empty<ContentEngagementJourneyDto>());

        public Task<IReadOnlyList<ContentEngagementJourneyStageDto>> GetOrderedStagesAsync(
            Guid journeyId, DateTimeOffset at, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ContentEngagementJourneyStageDto>>(Array.Empty<ContentEngagementJourneyStageDto>());
    }

    private sealed class FakeContentReader : IKnowledgeContentLinkageReader
    {
        public Task<IReadOnlyList<KnowledgeContentDto>> ResolvePublishedContentAsync(
            KnowledgeContentLinkageCriteria criteria, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<KnowledgeContentDto>>(Array.Empty<KnowledgeContentDto>());
    }

    private sealed class FakeCountryResolver : ICycleCapacityCountryResolver
    {
        public CycleCapacityCountryResolution Resolve(CyclePeriodSnapshot period, string? authoredCountryCode)
            => throw new NotSupportedException("capacity is null in these tests, so the estimator is never invoked");
    }

    private sealed class FakeWorkingDayCounter : IWorkingDayCounter
    {
        public Task<WorkingDayCountResult> CountAsync(
            string countryCode, Guid? legalEntityId, DateOnly from, DateOnly to, CancellationToken ct)
            => throw new NotSupportedException("capacity is null in these tests, so the estimator is never invoked");
    }
}
