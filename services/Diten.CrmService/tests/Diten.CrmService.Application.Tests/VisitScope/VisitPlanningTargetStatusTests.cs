using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.AccountRelationship.Queries;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.Segmentation;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Application.Features.VisitPlanning.TargetStatus;
using Diten.CrmService.Application.Tests.PlannedVisit;
using Diten.CrmService.Application.Tests.Segmentation;
using Diten.CrmService.Application.Tests.Territory;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using AccountEntity = Diten.CrmService.Domain.Entities.Account;
using ContactEntity = Diten.CrmService.Domain.Entities.Contact;
using PlanAtom = Diten.CrmService.Domain.Entities.PlannedVisit;
using ReportEntity = Diten.CrmService.Domain.Entities.VisitReport;
using Vfp = Diten.CrmService.Domain.Entities.VisitFrequencyPolicy;

namespace Diten.CrmService.Application.Tests.VisitScope;

/// <summary>
/// WP-VP-3D — the target-status reads (B-6, B-9, D5), measured on the production reader / handlers with in-memory
/// stores that COUNT their round trips: per-doctor period status (done / planned / remaining / last visit / due this
/// week), ownership and tenant isolation, the quick filters and Turkish search of the doctors read, the one-response
/// plan targets, and the bulk related-accounts read.
/// </summary>
public sealed class VisitPlanningTargetStatusTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherTenant = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private const string Rep = "rep-1";
    private const string OtherRep = "rep-2";

    // A fixed 4-week period: Mon 5 Oct 2026 .. Sun 1 Nov 2026; "today" is Wed 14 Oct (week 2).
    private static readonly ContactStatusPeriod October = new(Guid.NewGuid(), "C10", new DateOnly(2026, 10, 5), new DateOnly(2026, 11, 1));
    private static readonly DateOnly Today = new(2026, 10, 14);
    private static readonly DateTimeOffset At = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    // ── CT (merge with WP-VP-3A) — the status read counts the period requirement with the ENGINE rule ─────────────────

    [Fact]
    public void The_requirement_is_the_policy_count_times_its_period_units_and_unknown_is_one_like_the_engine()
    {
        // October period (5 Oct – 1 Nov) touches 2 months and 4 working weeks.
        static VisitFrequencyResolveResult Freq(string status, string? periodType, int? count) => new(status, null, null, null, null, count, null, periodType, null, null, null, null, null, null, Array.Empty<FrequencyCandidatePolicy>(), Array.Empty<string>());
        Assert.Equal(6, ContactPeriodStatusReader.RequiredInPeriod(Freq(FrequencyStatus.Resolved, FrequencyPeriodType.Month, 3), October));
        Assert.Equal(4, ContactPeriodStatusReader.RequiredInPeriod(Freq(FrequencyStatus.Resolved, FrequencyPeriodType.Week, 1), October));
        Assert.Equal(3, ContactPeriodStatusReader.RequiredInPeriod(Freq(FrequencyStatus.Resolved, FrequencyPeriodType.Cycle, 3), October));
        Assert.Equal(1, ContactPeriodStatusReader.RequiredInPeriod(null, October));
        Assert.Equal(1, ContactPeriodStatusReader.RequiredInPeriod(Freq(FrequencyStatus.Unknown, null, null), October));
    }

    // ── 1. done / planned / remaining / last visit ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task One_completed_and_one_future_plan_against_three_required_is_done_1_planned_1_remaining_1()
    {
        var w = new World();
        var doctor = w.Doctor("Ayşe Kaya", required: 3);
        var executed = new DateTimeOffset(2026, 10, 6, 10, 30, 0, TimeSpan.Zero);
        w.Report(w.Plan(doctor, new DateOnly(2026, 10, 6)), VisitExecutionOutcome.Completed, executed);
        w.Plan(doctor, new DateOnly(2026, 10, 20));

        var status = (await w.Reader().ReadAsync(w.Request(doctor), default))[doctor];

        Assert.Equal(3, status.RequiredVisitCount);
        Assert.Equal("resolved", status.FrequencyStatus);
        Assert.Equal(1, status.Done);
        Assert.Equal(1, status.Planned);
        Assert.Equal(1, status.Remaining);
        Assert.Equal(executed, status.LastVisitDate);
        Assert.False(status.NeverVisited);
    }

    // ── 2. ownership + tenant isolation ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Another_reps_visits_never_count_and_another_tenants_visits_are_never_read()
    {
        var w = new World();
        var doctor = w.Doctor("Ayşe Kaya", required: 2);
        // Another rep visited the same doctor (completed) and has a future plan for them.
        w.Report(w.Plan(doctor, new DateOnly(2026, 10, 6), resource: OtherRep), VisitExecutionOutcome.Completed, At.AddDays(-8));
        w.Plan(doctor, new DateOnly(2026, 10, 21), resource: OtherRep);
        // The same rep id + doctor id in ANOTHER tenant.
        var foreign = w.Plan(doctor, new DateOnly(2026, 10, 7), tenant: OtherTenant);
        w.Report(foreign, VisitExecutionOutcome.Completed, At.AddDays(-7), tenant: OtherTenant);

        var status = (await w.Reader().ReadAsync(w.Request(doctor), default))[doctor];

        Assert.Equal(0, status.Done);
        Assert.Equal(0, status.Planned);
        Assert.Equal(2, status.Remaining);
        Assert.Null(status.LastVisitDate);
        Assert.True(status.NeverVisited);
        Assert.All(w.Plans.Tenants, t => Assert.Equal(Tenant, t));
        Assert.All(w.Reports.Tenants, t => Assert.Equal(Tenant, t));
    }

    // ── 3. cancelled / archived never count; missed is not done ────────────────────────────────────────────────────

    [Fact]
    public async Task Cancelled_and_archived_plans_never_count_and_a_missed_visit_is_not_done()
    {
        var w = new World();
        var doctor = w.Doctor("Ayşe Kaya", required: 3);
        w.Report(w.Plan(doctor, new DateOnly(2026, 10, 6), status: PlannedVisitStatus.Cancelled), VisitExecutionOutcome.Completed, At.AddDays(-8));
        w.Report(w.Plan(doctor, new DateOnly(2026, 10, 7), status: PlannedVisitStatus.Archived), VisitExecutionOutcome.Completed, At.AddDays(-7));
        w.Plan(doctor, new DateOnly(2026, 10, 22), status: PlannedVisitStatus.Cancelled);
        w.Report(w.Plan(doctor, new DateOnly(2026, 10, 8)), VisitExecutionOutcome.Missed, At.AddDays(-6));

        var status = (await w.Reader().ReadAsync(w.Request(doctor), default))[doctor];

        Assert.Equal(0, status.Done);
        Assert.Equal(0, status.Planned);
        Assert.Equal(3, status.Remaining);
        Assert.True(status.NeverVisited);
        Assert.Null(status.LastVisitDate);
    }

    // ── 4. dueThisWeek boundaries + the quick filters ──────────────────────────────────────────────────────────────

    [Theory]
    // required 2 in 4 weeks ⇒ stride 2: no visit yet ⇒ due; visited week 1 ⇒ week 2 not due, week 3 due.
    [InlineData(2, 2, null, "2026-10-14", true)]
    [InlineData(2, 1, "2026-10-06", "2026-10-14", false)]
    [InlineData(2, 1, "2026-10-06", "2026-10-19", true)]
    // required 4 ⇒ stride 1: visited last week ⇒ due; visited this week ⇒ not due.
    [InlineData(4, 3, "2026-10-08", "2026-10-14", true)]
    [InlineData(4, 3, "2026-10-12", "2026-10-14", false)]
    // nothing remaining / frequency unknown / outside the period ⇒ never due.
    [InlineData(2, 0, null, "2026-10-14", false)]
    [InlineData(null, null, null, "2026-10-14", false)]
    [InlineData(2, 2, null, "2026-11-02", false)]
    public void DueThisWeek_follows_the_even_distribution_stride(
        int? required, int? remaining, string? lastInPeriod, string today, bool expected)
    {
        var last = lastInPeriod is null ? (DateOnly?)null : DateOnly.Parse(lastInPeriod);
        Assert.Equal(expected, ContactPeriodStatusReader.IsDueThisWeek(remaining, required, October, last, DateOnly.Parse(today)));
    }

    [Fact]
    public async Task Quick_due_never_and_all_filter_the_doctors_and_an_unknown_value_is_400()
    {
        var w = new World(livePeriod: true);
        var account = w.Account("Memorial Şişli");
        var seenThisWeek = w.Doctor("Ahmet Demir", required: 2, account: account);
        var never = w.Doctor("Burak Çelik", required: 2, account: account);
        var seenLongAgo = w.Doctor("Cem Yılmaz", required: 2, account: account);
        var now = DateTimeOffset.UtcNow;
        w.Report(w.Plan(seenThisWeek, DateOnly.FromDateTime(now.UtcDateTime)), VisitExecutionOutcome.Completed, now);
        w.Report(w.Plan(seenLongAgo, DateOnly.FromDateTime(now.UtcDateTime).AddDays(-120)), VisitExecutionOutcome.Completed, now.AddDays(-120));

        async Task<IReadOnlyList<Guid>> Ids(string? quick)
            => (await w.Doctors(new GetAccountDoctorsQuery(account, Quick: quick), default)).Data!.Items.Select(i => i.ContactId).ToList();

        Assert.Equal(new[] { never, seenLongAgo }, await Ids("due"));
        Assert.Equal(new[] { never }, await Ids("never"));
        Assert.Equal(new[] { seenThisWeek, never, seenLongAgo }, await Ids(null));
        Assert.Equal(new[] { seenThisWeek, never, seenLongAgo }, await Ids("ALL"));

        var bad = await w.Doctors(new GetAccountDoctorsQuery(account, Quick: "soon"), default);
        Assert.Equal(400, bad.StatusCode);
        Assert.Contains(TargetStatusQuickFilters.InvalidQuickCode, bad.Errors!);
    }

    // ── 5. the read count does not grow with the doctor count ──────────────────────────────────────────────────────

    [Fact]
    public async Task Reading_5_or_50_doctors_costs_the_same_number_of_store_reads()
    {
        async Task<int[]> Reads(int doctors)
        {
            var w = new World();
            var ids = Enumerable.Range(0, doctors).Select(i =>
            {
                var d = w.Doctor("Doktor " + i, required: 2);
                w.Report(w.Plan(d, new DateOnly(2026, 10, 6)), VisitExecutionOutcome.Completed, At.AddDays(-8));
                w.Plan(d, new DateOnly(2026, 10, 21));
                return d;
            }).ToList();
            await w.Reader().ReadAsync(w.Request(ids.ToArray()), default);
            return new[] { w.Plans.ContactSetReads, w.Plans.OtherReads, w.Reports.BulkReads, w.Reports.OtherReads,
                w.Contacts.IdReads, w.Segments.Calls, w.Policies.Reads, w.Consent.Calls };
        }

        var five = await Reads(5);
        var fifty = await Reads(50);

        Assert.Equal(new[] { 1, 0, 1, 0, 1, 1, 1, 1 }, five);
        Assert.Equal(five, fifty);
    }

    [Fact]
    public async Task Segment_badges_cost_a_fixed_number_of_reads_per_segment_not_per_doctor()
    {
        async Task<(int Targets, int Subjects, int SegmentLists, IReadOnlyList<Guid> First)> Run(int doctors)
        {
            var segments = new CountingSegments();
            var targets = new CountingTargets();
            var candidates = new FakeCandidateSource();
            var ids = Enumerable.Range(0, doctors).Select(_ => Guid.NewGuid()).ToList();
            var vip = segments.Add("VIP", SegmentTypes.Static);
            segments.Add("Dynamic", SegmentTypes.Dynamic);
            targets.Rows.Add(new TargetCustomer
            {
                Id = Guid.NewGuid(), TenantId = Tenant, SegmentId = vip, SubjectType = SegmentSubjectTypes.Contact,
                SubjectId = ids[0], MembershipMode = SegmentMembershipModes.ManualInclude, EffectiveFrom = At.AddYears(-1)
            });
            var resolver = new SegmentMembershipResolver(
                candidates,
                new SegmentAttributeSourceReader(candidates, new FakeConsentBulkReader(), new FakeTerritoryCoverageReader(),
                    new FakeConceptAffinityReader()),
                targets);

            var set = await new ContactSegmentSetReader(segments, resolver).ReadAsync(Tenant, ids, At, default);
            return (targets.SegmentReads, candidates.LoadSubjectsCalls, segments.ListCalls, set.For(ids[0]));
        }

        var five = await Run(5);
        var fifty = await Run(50);

        Assert.Equal((five.Targets, five.Subjects, five.SegmentLists), (fifty.Targets, fifty.Subjects, fifty.SegmentLists));
        Assert.Equal(1, five.SegmentLists);
        Assert.Single(five.First);
    }

    // ── 6. plan targets in one response; a foreign plan is 404 ─────────────────────────────────────────────────────

    [Fact]
    public async Task Session_targets_carry_institution_pharmacy_and_doctor_names_with_statuses_and_a_foreign_plan_is_404()
    {
        var w = new World();
        var hospital = w.Account("Memorial Şişli", "hospital");
        var pharmacy = w.Account("Şifa Eczanesi", "pharmacy");
        var doctor = w.Doctor("Sadakat Özdil", required: 2, account: hospital);
        var executed = new DateTimeOffset(2026, 10, 5, 11, 0, 0, TimeSpan.Zero);
        w.Report(w.Plan(doctor, new DateOnly(2026, 10, 5)), VisitExecutionOutcome.Completed, executed);
        var mine = w.Session(Rep, new[] { hospital }, new[] { pharmacy }, (doctor, hospital));
        var theirs = w.Session(OtherRep, new[] { hospital }, Array.Empty<Guid>(), (doctor, hospital));

        var response = await w.Targets(new GetSessionTargetsQuery(mine), default);

        Assert.True(response.IsSuccessful);
        var data = response.Data!;
        Assert.Equal("Memorial Şişli", Assert.Single(data.Accounts).AccountName);
        Assert.Equal("pharmacy", Assert.Single(data.Pharmacies).AccountType);
        var d = Assert.Single(data.Doctors);
        Assert.Equal("Sadakat Özdil", d.DisplayName);
        Assert.Equal(1, d.Status.Done);
        Assert.Equal(executed, d.Status.LastVisitDate);
        Assert.Equal(1, w.Accounts.ListByIdsCalls);
        Assert.Equal(0, w.Accounts.GetByIdCalls);

        Assert.Equal(404, (await w.Targets(new GetSessionTargetsQuery(theirs), default)).StatusCode);
    }

    // ── 7. related accounts in bulk; > 100 ids is 400 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Related_accounts_for_three_institutions_come_in_one_read_and_more_than_100_ids_is_400()
    {
        var w = new World();
        var h1 = w.Account("Hastane 1", "hospital");
        var h2 = w.Account("Hastane 2", "hospital");
        var h3 = w.Account("Hastane 3", "hospital");
        var p1 = w.Account("Eczane 1", "pharmacy");
        var p2 = w.Account("Eczane 2", "pharmacy");
        var lab = w.Account("Lab", "laboratory");
        var rels = new CountingRelationships();
        rels.Link(h1, p1, "preferred-pharmacy");
        rels.Link(p2, h2, "served-by");
        rels.Link(h3, lab, "refers-to");
        var handler = new ListRelatedAccountsBulkQueryHandler(Ctx(Tenant), w.Accounts, rels, new Metadata());

        var response = await handler.Handle(
            new ListRelatedAccountsBulkQuery($"{h1},{h2},{h3}", "pharmacy"), default);

        Assert.True(response.IsSuccessful);
        var groups = response.Data!.Groups.ToDictionary(g => g.AccountId);
        Assert.Equal(p1, Assert.Single(groups[h1].Items).RelatedAccountId);
        Assert.Equal(p2, Assert.Single(groups[h2].Items).RelatedAccountId);
        Assert.Empty(groups[h3].Items);
        Assert.Equal(1, rels.BulkReads);
        Assert.Equal(0, rels.PerAccountReads);

        var tooMany = string.Join(",", Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()));
        var refused = await handler.Handle(new ListRelatedAccountsBulkQuery(tooMany), default);
        Assert.Equal(400, refused.StatusCode);
        Assert.Contains(RelatedAccountsBulkCodes.TooManyIds, refused.Errors!);
    }

    // ── 8. Turkish-insensitive search ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_sirin_finds_SIRIN()
    {
        var w = new World(livePeriod: true);
        var account = w.Account("Memorial Şişli");
        var sirin = w.Doctor("ŞİRİN AKTAŞ", required: 2, account: account);
        w.Doctor("Mehmet Öz", required: 2, account: account);

        var response = await w.Doctors(new GetAccountDoctorsQuery(account, Search: "şirin"), default);

        Assert.Equal(sirin, Assert.Single(response.Data!.Items).ContactId);
    }

    // ═══ world ═════════════════════════════════════════════════════════════════════════════════════════════════════

    private static TenantContext Ctx(Guid tenant) => SegmentTestDoubles.Tenant(tenant);

    private sealed class World
    {
        private readonly bool _livePeriod;

        public World(bool livePeriod = false) => _livePeriod = livePeriod;

        public CountingPlans Plans { get; } = new();
        public CountingReports Reports { get; } = new();
        public InMemoryContacts Contacts { get; } = new();
        public InMemoryAccountContactLinks Links { get; } = new();
        public FakeAccountRepository Accounts { get; } = new();
        public CountingSegmentSet Segments { get; } = new();
        public CountingPolicies Policies { get; } = new();
        public FakeConsentBulkReader Consent { get; } = new();
        public List<PlanningSession> Sessions { get; } = new();
        public Guid PeriodId { get; } = Guid.NewGuid();

        private ContactStatusPeriod Period => _livePeriod ? LivePeriod() : October;

        private static ContactStatusPeriod LivePeriod()
        {
            var start = ContactPeriodStatusReader.WeekStart(DateOnly.FromDateTime(DateTime.UtcNow)).AddDays(-7);
            return new ContactStatusPeriod(null, "LIVE", start, start.AddDays(27));
        }

        public Guid Account(string name, string type = "hospital")
        {
            var a = new AccountEntity { Id = Guid.NewGuid(), TenantId = Tenant, AccountName = name, AccountCode = name, AccountType = type, Status = "active", CityRef = "TR-34-ISTANBUL" };
            Accounts.Items.Add(a);
            return a.Id;
        }

        public Guid Doctor(string name, int required, Guid? account = null)
        {
            var c = new ContactEntity { Id = Guid.NewGuid(), TenantId = Tenant, DisplayName = name, Status = "active", Specialty = "cardiology" };
            Contacts.Items.Add(c);
            if (account is { } a)
            {
                Links.Items.Add(new AccountContactLink { Id = Guid.NewGuid(), TenantId = Tenant, AccountId = a, ContactId = c.Id, Status = "active", IsPrimary = true });
            }

            Policies.Items.Add(new Vfp
            {
                Id = Guid.NewGuid(), TenantId = Tenant, PolicyCode = "P-" + name, PolicyName = name,
                TargetType = FrequencyTargetType.Contact, TargetId = c.Id, FrequencyType = FrequencyType.Monthly,
                RequiredVisitCount = required, PeriodType = FrequencyPeriodType.Cycle, // "required" = the visits the whole period needs (cycle unit = 1; CT fix on merge with WP-VP-3A)
                EffectiveFrom = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), Priority = 10,
                Source = FrequencySource.Manual, Status = FrequencyPolicyStatus.Active
            });
            return c.Id;
        }

        public Guid Plan(Guid doctor, DateOnly date, string resource = Rep, string status = PlannedVisitStatus.Planned, Guid? tenant = null)
        {
            var p = new PlanAtom
            {
                Id = Guid.NewGuid(), TenantId = tenant ?? Tenant, VisitCode = "V-" + Guid.NewGuid().ToString("N")[..8],
                TargetType = PlannedVisitTargetType.Contact, TargetId = doctor, ContactId = doctor, PlannedDate = date,
                PlanStatus = status, Resource = new PlannedVisitResourceRef { ResourceId = resource, ResourceType = "user" }
            };
            Plans.Inner.Items.Add(p);
            return p.Id;
        }

        public void Report(Guid plan, string outcome, DateTimeOffset executedAt, Guid? tenant = null)
            => Reports.Items.Add(new ReportEntity
            {
                Id = Guid.NewGuid(), TenantId = tenant ?? Tenant, PlannedVisitId = plan, ExecutionOutcome = outcome,
                ReportStatus = VisitReportStatus.Submitted, ExecutedAt = executedAt, ReportedByResourceId = Rep
            });

        public Guid Session(string resource, Guid[] accounts, Guid[] pharmacies, params (Guid Contact, Guid Account)[] doctors)
        {
            var s = new PlanningSession
            {
                Id = Guid.NewGuid(), TenantId = Tenant, CyclePeriodId = PeriodId, ResourceId = resource,
                Selection = new PlanningSessionSelection
                {
                    SelectedAccountIds = accounts.ToList(),
                    SelectedPharmacyIds = pharmacies.ToList(),
                    SelectedContacts = doctors.Select(d => new PlanningSessionSelectedContact { ContactId = d.Contact, AccountId = d.Account }).ToList()
                }
            };
            Sessions.Add(s);
            return s.Id;
        }

        public ContactPeriodStatusReader Reader()
            => new(Plans, Reports, Contacts, Segments, Policies, Consent);

        public ContactPeriodStatusRequest Request(params Guid[] doctors)
            => new(Tenant, Rep, October, doctors, Today, At);

        private ICyclePeriodReader PeriodReader() => new FixedPeriod(PeriodId, Period);

        public Task<Diten.CrmService.Application.Common.Models.Response<SessionTargetsDto>> Targets(
            GetSessionTargetsQuery query, CancellationToken ct)
            => new GetSessionTargetsQueryHandler(
                Ctx(Tenant), new TestCallerScope(Rep), new Sessions(this.Sessions), PeriodReader(), Accounts, Contacts, Reader())
                .Handle(query, ct);

        public Task<Diten.CrmService.Application.Common.Models.Response<AccountDoctorsDto>> Doctors(
            GetAccountDoctorsQuery query, CancellationToken ct)
            => new GetAccountDoctorsQueryHandler(
                Ctx(Tenant), new TestCallerScope(Rep), new Sessions(this.Sessions), PeriodReader(), Accounts, Links, Contacts,
                new FakeTerritoryResourceAssignmentRepo(), new FakeTerritoryNodeRepo(), new FakeTerritoryModelRepo(),
                new FakeAccountTerritoryAssignmentRepo(), Reader())
                .Handle(query, ct);
    }

    /// <summary>Counts the contact-set read; delegates it to the interface's DEFAULT body (production narrowing rule:
    /// tenant + resource + contact) over the FU01 fake. Records every tenant it was asked about.</summary>
    private sealed class CountingPlans : IPlannedVisitRepository
    {
        public FakePlannedVisitRepository Inner { get; } = new();
        public int ContactSetReads { get; private set; }
        public int OtherReads { get; private set; }
        public List<Guid> Tenants { get; } = new();

        public Task<IReadOnlyList<PlanAtom>> ListByResourceAndContactsAsync(
            Guid tenantId, string resourceId, IReadOnlyCollection<Guid> contactIds, CancellationToken ct)
        {
            ContactSetReads++;
            Tenants.Add(tenantId);
            return ((IPlannedVisitRepository)new DefaultBody(Inner)).ListByResourceAndContactsAsync(tenantId, resourceId, contactIds, ct);
        }

        public Task<PlanAtom?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) { OtherReads++; return Inner.GetByIdAsync(tenantId, id, ct); }
        public Task<IReadOnlyList<PlanAtom>> ListAsync(Guid tenantId, CancellationToken ct) { OtherReads++; return Inner.ListAsync(tenantId, ct); }
        public Task<IReadOnlyList<PlanAtom>> ListByCodeAsync(Guid tenantId, string visitCode, CancellationToken ct) { OtherReads++; return Inner.ListByCodeAsync(tenantId, visitCode, ct); }
        public Task<IReadOnlyList<PlanAtom>> ListByResourceAndDateAsync(Guid tenantId, string resourceId, DateOnly plannedDate, CancellationToken ct) { OtherReads++; return Inner.ListByResourceAndDateAsync(tenantId, resourceId, plannedDate, ct); }
        public Task<IReadOnlyList<PlanAtom>> ListByTargetAndDateAsync(Guid tenantId, Guid targetId, DateOnly plannedDate, CancellationToken ct) { OtherReads++; return Inner.ListByTargetAndDateAsync(tenantId, targetId, plannedDate, ct); }
        public Task<IReadOnlyList<PlanAtom>> ListFromDateByContentPathsAsync(Guid tenantId, IReadOnlyCollection<Guid> pathIds, DateOnly fromDate, CancellationToken ct) { OtherReads++; return Inner.ListFromDateByContentPathsAsync(tenantId, pathIds, fromDate, ct); }
        public Task InsertAsync(PlanAtom entity, CancellationToken ct) => throw new NotSupportedException("read-only");
        public Task<bool> ReplaceAsync(PlanAtom entity, int expectedVersion, CancellationToken ct) => throw new NotSupportedException("read-only");

        /// <summary>The fake WITHOUT an override, so the interface's default body runs.</summary>
        private sealed class DefaultBody(FakePlannedVisitRepository inner) : IPlannedVisitRepository
        {
            public Task<PlanAtom?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => inner.GetByIdAsync(tenantId, id, ct);
            public Task<IReadOnlyList<PlanAtom>> ListAsync(Guid tenantId, CancellationToken ct) => inner.ListAsync(tenantId, ct);
            public Task<IReadOnlyList<PlanAtom>> ListByCodeAsync(Guid tenantId, string visitCode, CancellationToken ct) => inner.ListByCodeAsync(tenantId, visitCode, ct);
            public Task<IReadOnlyList<PlanAtom>> ListByResourceAndDateAsync(Guid tenantId, string resourceId, DateOnly plannedDate, CancellationToken ct) => inner.ListByResourceAndDateAsync(tenantId, resourceId, plannedDate, ct);
            public Task<IReadOnlyList<PlanAtom>> ListByTargetAndDateAsync(Guid tenantId, Guid targetId, DateOnly plannedDate, CancellationToken ct) => inner.ListByTargetAndDateAsync(tenantId, targetId, plannedDate, ct);
            public Task<IReadOnlyList<PlanAtom>> ListFromDateByContentPathsAsync(Guid tenantId, IReadOnlyCollection<Guid> pathIds, DateOnly fromDate, CancellationToken ct) => inner.ListFromDateByContentPathsAsync(tenantId, pathIds, fromDate, ct);
            public Task InsertAsync(PlanAtom entity, CancellationToken ct) => throw new NotSupportedException();
            public Task<bool> ReplaceAsync(PlanAtom entity, int expectedVersion, CancellationToken ct) => throw new NotSupportedException();
        }
    }

    private sealed class CountingReports : IVisitReportRepository
    {
        public List<ReportEntity> Items { get; } = new();
        public int BulkReads { get; private set; }
        public int OtherReads { get; private set; }
        public List<Guid> Tenants { get; } = new();

        public Task<IReadOnlyList<ReportEntity>> ListByPlannedVisitIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
        {
            BulkReads++;
            Tenants.Add(tenantId);
            return Task.FromResult<IReadOnlyList<ReportEntity>>(
                Items.Where(r => r.TenantId == tenantId && !r.IsDeleted && ids.Contains(r.PlannedVisitId)).ToList());
        }

        public Task<ReportEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) { OtherReads++; return Task.FromResult<ReportEntity?>(null); }
        public Task<ReportEntity?> GetByPlannedVisitIdAsync(Guid tenantId, Guid plannedVisitId, CancellationToken ct) { OtherReads++; return Task.FromResult<ReportEntity?>(null); }
        public Task<IReadOnlyList<ReportEntity>> ListAsync(Guid tenantId, CancellationToken ct) { OtherReads++; return Task.FromResult<IReadOnlyList<ReportEntity>>(Array.Empty<ReportEntity>()); }
        public Task InsertAsync(ReportEntity entity, CancellationToken ct) => throw new NotSupportedException("read-only");
        public Task<bool> ReplaceAsync(ReportEntity entity, int expectedVersion, CancellationToken ct) => throw new NotSupportedException("read-only");
    }

    private sealed class CountingSegmentSet : IContactSegmentSetReader
    {
        public int Calls { get; private set; }

        public Task<ContactSegmentSet> ReadAsync(Guid tenantId, IReadOnlyCollection<Guid> contactIds, DateTimeOffset at, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(ContactSegmentSet.Empty);
        }
    }

    private sealed class CountingPolicies : IVisitFrequencyPolicyRepository
    {
        public List<Vfp> Items { get; } = new();
        public int Reads { get; private set; }

        public Task<IReadOnlyList<Vfp>> ListActiveByTargetsAsync(Guid t, IReadOnlyCollection<Guid> targetIds, CancellationToken ct)
        {
            Reads++;
            return Task.FromResult<IReadOnlyList<Vfp>>(Items.Where(p =>
                p.TenantId == t && !p.IsDeleted && p.Status == FrequencyPolicyStatus.Active && targetIds.Contains(p.TargetId)).ToList());
        }

        public Task<Vfp?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Vfp>> ListAsync(Guid t, CancellationToken ct) => throw new NotSupportedException();
        public Task<Vfp?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct) => throw new NotSupportedException();
        public Task InsertAsync(Vfp policy, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(Vfp policy, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Sessions(List<PlanningSession> items) : IPlanningSessionRepository
    {
        public Task<PlanningSession?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(items.FirstOrDefault(s => s.TenantId == tenantId && s.Id == id));
        public Task<IReadOnlyList<PlanningSession>> ListAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlanningSession>> ListByPeriodAndResourceAsync(Guid tenantId, Guid cyclePeriodId, string resourceId, CancellationToken ct) => throw new NotSupportedException();
        public Task InsertAsync(PlanningSession entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ReplaceAsync(PlanningSession entity, int expectedVersion, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FixedPeriod(Guid id, ContactStatusPeriod period) : ICyclePeriodReader
    {
        private CyclePeriodSnapshot Snapshot => new(
            id, period.CycleCode ?? "C", "Cycle", period.Start.Year, 1,
            new DateTimeOffset(period.Start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            new DateTimeOffset(period.End.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            "active", "tenant", null, null, null, null);

        public Task<CyclePeriodResolution> ResolveActiveAsync(DateTimeOffset at, string? country, Guid? legalEntityId, string? businessUnitId, CancellationToken ct)
            => Task.FromResult(new CyclePeriodResolution(CyclePeriodResolutionOutcomes.Resolved, Snapshot, new[] { id }, null, "tenant"));
        public Task<CyclePeriodSnapshot?> GetByIdAsync(Guid cyclePeriodId, CancellationToken ct)
            => Task.FromResult(cyclePeriodId == id ? Snapshot : null);
        public Task<IReadOnlyList<CyclePeriodSnapshot>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<CyclePeriodSnapshot>> ListByYearAsync(int year, string? scopeType, string? scopeRef, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class CountingRelationships : IAccountRelationshipRepository
    {
        private readonly List<AccountRelationship> _rows = new();
        public int BulkReads { get; private set; }
        public int PerAccountReads { get; private set; }

        public void Link(Guid source, Guid target, string type) => _rows.Add(new AccountRelationship
        {
            Id = Guid.NewGuid(), TenantId = Tenant, SourceAccountId = source, TargetAccountId = target, RelationshipType = type, Status = "active"
        });

        public Task<IReadOnlyList<AccountRelationship>> ListByAccountIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> accountIds, CancellationToken ct)
        {
            BulkReads++;
            return Task.FromResult<IReadOnlyList<AccountRelationship>>(_rows.Where(r => r.TenantId == tenantId && !r.IsDeleted
                && (accountIds.Contains(r.SourceAccountId) || accountIds.Contains(r.TargetAccountId))).ToList());
        }

        public Task<IReadOnlyList<AccountRelationship>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken ct)
        {
            PerAccountReads++;
            return Task.FromResult<IReadOnlyList<AccountRelationship>>(_rows.Where(r => r.TenantId == tenantId
                && (r.SourceAccountId == accountId || r.TargetAccountId == accountId)).ToList());
        }

        public Task<AccountRelationship?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ExistsActivePairAsync(Guid tenantId, Guid s, Guid t, string type, bool rev, Guid? ex, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<AccountRelationship>> ListAllAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task InsertAsync(AccountRelationship relationship, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(AccountRelationship relationship, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Metadata : IReferenceMetadataReader
    {
        public Task<IReadOnlyDictionary<string, string>?> GetValueAttributesAsync(string setCode, string value, CancellationToken ct)
            => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                new Dictionary<string, string> { ["direction"] = "directional", ["inverseLabelCode"] = value + "-inverse" });
    }

    private sealed class CountingSegments : ISegmentRepository
    {
        private readonly List<Segment> _items = new();
        public int ListCalls { get; private set; }

        public Guid Add(string name, string type)
        {
            var s = new Segment
            {
                Id = Guid.NewGuid(), TenantId = Tenant, SegmentCode = name, SegmentName = name, SegmentType = type,
                SubjectType = SegmentSubjectTypes.Contact, SegmentStatus = SegmentStatuses.Active, EffectiveFrom = At.AddYears(-1)
            };
            _items.Add(s);
            return s.Id;
        }

        public Task<IReadOnlyList<Segment>> ListAsync(Guid tenantId, CancellationToken ct) { ListCalls++; return Task.FromResult<IReadOnlyList<Segment>>(_items); }
        public Task<Segment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => throw new NotSupportedException("per-segment get must not be used");
        public Task<IReadOnlyList<Segment>> ListByLineageAsync(Guid tenantId, Guid lineageId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Segment>> ListByCodeAsync(Guid tenantId, string segmentCode, CancellationToken ct) => throw new NotSupportedException();
        public Task InsertAsync(Segment entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ReplaceAsync(Segment entity, int expectedVersion, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class CountingTargets : ITargetCustomerRepository
    {
        public List<TargetCustomer> Rows { get; } = new();
        public int SegmentReads { get; private set; }

        public Task<IReadOnlyList<TargetCustomer>> ListBySegmentAsync(Guid tenantId, Guid segmentId, CancellationToken ct)
        {
            SegmentReads++;
            return Task.FromResult<IReadOnlyList<TargetCustomer>>(Rows.Where(t => t.TenantId == tenantId && t.SegmentId == segmentId).ToList());
        }

        public Task<IReadOnlyList<TargetCustomer>> ListBySubjectAsync(Guid tenantId, string subjectType, Guid subjectId, CancellationToken ct)
            => throw new NotSupportedException("per-subject read must not be used in bulk");
        public Task<TargetCustomer?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task InsertAsync(TargetCustomer entity, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ReplaceAsync(TargetCustomer entity, int expectedVersion, CancellationToken ct) => throw new NotSupportedException();
    }
}
