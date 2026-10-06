using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Account.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.Account.Queries;
using Diten.CrmService.Application.Features.PlannedVisit;
using Diten.CrmService.Application.Features.PlannedVisit.Commands;
using Diten.CrmService.Application.Features.PlannedVisit.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.PlannedVisit.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.PlannedVisit.Provenance;
using Diten.CrmService.Application.Features.PlannedVisit.Queries;
using Diten.CrmService.Application.Features.Resources;
using Diten.CrmService.Application.Features.Segmentation;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.VisitPlanning;
using Diten.CrmService.Application.Features.VisitPlanning.Commands;
using Diten.CrmService.Application.Features.VisitPlanning.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.VisitPlanning.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitPlanning.MyAccounts;
using Diten.CrmService.Application.Features.VisitPlanning.Queries;
using Diten.CrmService.Application.Features.VisitReport.Commands;
using Diten.CrmService.Application.Features.VisitReport.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.VisitReport.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.VisitReport.Queries;
using Diten.CrmService.Application.Tests.PlannedVisit;
using Diten.CrmService.Application.Tests.Territory;
using Diten.CrmService.Application.Tests.VisitReport;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using AccountEntity = Diten.CrmService.Domain.Entities.Account;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;
using VisitReportEntity = Diten.CrmService.Domain.Entities.VisitReport;

namespace Diten.CrmService.Application.Tests.VisitScope;

/// <summary>
/// WP-VP-2 — visit planning phase 2 on the PRODUCTION handlers / services (only the stores are in-memory):
/// B-8 read-time names (bulk, never per row; unknown → null; passive target flagged), B-1 ownership (no read-all ⇒ own
/// records only, foreign ⇒ 404; read-all ⇒ tenant-wide; a foreign resource on create ⇒ 403 resource_not_caller; an empty
/// one ⇒ the caller; resources/me carries the display name), B-2 territory (subtree coverage + "my accounts" exact vs
/// subtree, validity window, unassigned ⇒ every account) and B-3 server derivation (play from the doctor's own segments,
/// deterministic multiple-plays, the client's campaign ignored, provenance kept on update).
/// </summary>
public sealed class VisitPlanningPhase2Tests
{
    private static readonly Guid Tenant = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private const string Me = "11111111-1111-1111-1111-111111111111";
    private const string Other = "22222222-2222-2222-2222-222222222222";
    private const string PvReadAll = "crm.planned-visit.read-all";
    private const string VpReadAll = "crm.visit-plan.read-all";
    private static DateOnly Future => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));

    private static TenantContext TenantCtx()
    {
        var t = new TenantContext();
        t.SetTenant(Tenant);
        return t;
    }

    private static ICallerScope Rep(string resourceId = Me) => new TestCallerScope(resourceId);

    private static ICallerScope Manager() => new TestCallerScope(Me, PvReadAll, VpReadAll);

    // ═════════════════════════════════ 1 · B-8 names on the planned-visit list / detail ═════════════════════════════════

    [Fact]
    public async Task List_names_are_read_in_one_bulk_read_per_master_unknown_is_null_and_passive_is_flagged()
    {
        var store = new FakePlannedVisitRepository();
        var accounts = new FakeAccountRepository();
        var contacts = new FakeContactRepository();
        var unknownContact = Guid.NewGuid();
        for (var i = 0; i < 5; i++)
        {
            var account = Account($"Klinik {i}");
            accounts.Items.Add(account);
            var contact = Contact($"Dr. {i}", status: i == 0 ? "inactive" : "active");
            contacts.Items.Add(contact);
            store.Items.Add(Plan(Me, PlannedVisitTargetType.Contact, contact.Id, account.Id, contact.Id));
        }

        store.Items.Add(Plan(Me, PlannedVisitTargetType.Contact, unknownContact, null, unknownContact));
        var pharmacy = Account("Eczane A");
        accounts.Items.Add(pharmacy);
        store.Items.Add(Plan(Me, PlannedVisitTargetType.Pharmacy, pharmacy.Id, pharmacy.Id, null));

        var list = await new ListPlannedVisitsHandler(TenantCtx(), store, Rep(), new VisitTargetNameReader(accounts, contacts))
            .Handle(new ListPlannedVisitsQuery(), default);

        Assert.Equal(7, list.Data!.Items.Count);
        Assert.Equal(1, accounts.ListByIdsCalls); // N rows → ONE account read
        Assert.Equal(1, contacts.ListByIdsCalls); // and ONE contact read
        Assert.Equal(0, accounts.GetByIdCalls);   // never per row

        var first = list.Data.Items.Single(i => i.TargetDisplayName == "Dr. 0");
        Assert.Equal("Klinik 0", first.AccountDisplayName);
        Assert.Equal("Dr. 0", first.ContactDisplayName);
        Assert.True(first.TargetInactive);
        Assert.False(list.Data.Items.Single(i => i.TargetDisplayName == "Dr. 1").TargetInactive);

        var unknown = list.Data.Items.Single(i => i.TargetId == unknownContact);
        Assert.Null(unknown.TargetDisplayName); // not found → null, the id stays
        Assert.Equal("Eczane A", list.Data.Items.Single(i => i.TargetId == pharmacy.Id).TargetDisplayName);
    }

    [Fact]
    public async Task Detail_carries_the_target_names()
    {
        var store = new FakePlannedVisitRepository();
        var accounts = new FakeAccountRepository();
        var contacts = new FakeContactRepository();
        var account = Account("Hastane B");
        var contact = Contact("Dr. Ayşe");
        accounts.Items.Add(account);
        contacts.Items.Add(contact);
        var plan = Plan(Me, PlannedVisitTargetType.Contact, contact.Id, account.Id, contact.Id);
        store.Items.Add(plan);

        var detail = await new GetPlannedVisitByIdHandler(TenantCtx(), store, Rep(), new VisitTargetNameReader(accounts, contacts))
            .Handle(new GetPlannedVisitByIdQuery(plan.Id), default);

        Assert.Equal("Dr. Ayşe", detail.Data!.TargetDisplayName);
        Assert.Equal("Hastane B", detail.Data.AccountDisplayName);
        Assert.Equal("Dr. Ayşe", detail.Data.ContactDisplayName);
    }

    // ═════════════════════════════════ 2 · B-8 names on the session detail ═════════════════════════════════════════════

    [Fact]
    public async Task Session_detail_carries_name_arrays_next_to_the_unchanged_id_arrays()
    {
        var accounts = new FakeAccountRepository();
        var contacts = new FakeContactRepository();
        var clinic = Account("Klinik C");
        var pharmacy = Account("Eczane D");
        var doctor = Contact("Dr. Mehmet");
        accounts.Items.AddRange(new[] { clinic, pharmacy });
        contacts.Items.Add(doctor);
        var session = Session(Me);
        session.Selection.SelectedAccountIds.Add(clinic.Id);
        session.Selection.SelectedPharmacyIds.Add(pharmacy.Id);
        session.Selection.SelectedContacts.Add(new PlanningSessionSelectedContact { ContactId = doctor.Id, AccountId = clinic.Id });

        var r = await new GetPlanningSessionByIdHandler(
                TenantCtx(), new SessionStore(session), Rep(), new VisitTargetNameReader(accounts, contacts))
            .Handle(new GetPlanningSessionByIdQuery(session.Id), default);

        var dto = r.Data!;
        Assert.Equal(new[] { clinic.Id }, dto.SelectedAccountIds);
        Assert.Equal("Klinik C", Assert.Single(dto.SelectedAccounts!).DisplayName);
        Assert.Equal("Eczane D", Assert.Single(dto.SelectedPharmacies!).DisplayName);
        var c = Assert.Single(dto.SelectedContacts);
        Assert.Equal("Dr. Mehmet", c.ContactDisplayName);
        Assert.Equal("Klinik C", c.AccountDisplayName);
        Assert.Equal(1, accounts.ListByIdsCalls);
    }

    // ═════════════════════════════════ 3 · B-1 ownership ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Without_read_all_another_reps_planned_visit_is_unlisted_and_every_action_on_it_is_404()
    {
        var store = new FakePlannedVisitRepository();
        var mine = Plan(Me, PlannedVisitTargetType.Account, Guid.NewGuid(), null, null);
        var theirs = Plan(Other, PlannedVisitTargetType.Account, Guid.NewGuid(), null, null);
        store.Items.AddRange(new[] { mine, theirs });
        var names = new VisitTargetNameReader(new FakeAccountRepository(), new FakeContactRepository());

        var list = await new ListPlannedVisitsHandler(TenantCtx(), store, Rep(), names).Handle(new ListPlannedVisitsQuery(), default);
        Assert.Equal(new[] { mine.Id }, list.Data!.Items.Select(i => i.PlannedVisitId));

        Assert.Equal(404, (await new GetPlannedVisitByIdHandler(TenantCtx(), store, Rep(), names)
            .Handle(new GetPlannedVisitByIdQuery(theirs.Id), default)).StatusCode);
        Assert.Equal(404, (await new CancelPlannedVisitHandler(TenantCtx(), new NullActorContext(), store, Rep())
            .Handle(new CancelPlannedVisitCommand(theirs.Id, "x", null), default)).StatusCode);
        Assert.Equal(404, (await new ArchivePlannedVisitHandler(TenantCtx(), new NullActorContext(), store, Rep())
            .Handle(new ArchivePlannedVisitCommand(theirs.Id, null), default)).StatusCode);
        Assert.Equal(404, (await new ConfirmPlannedVisitHandler(
                TenantCtx(), new NullActorContext(), store, new PlannedVisitConsentProbe(new FakeConsentEvaluator()), Rep())
            .Handle(new ConfirmPlannedVisitCommand(theirs.Id, null), default)).StatusCode);
        Assert.Equal(404, (await UpdateHandler(store, Rep()).Handle(UpdateCmd(theirs.Id, Other), default)).StatusCode);
        Assert.Equal(0, store.ReplaceCount); // nothing was written

        // read-all sees the whole tenant.
        var all = await new ListPlannedVisitsHandler(TenantCtx(), store, Manager(), names).Handle(new ListPlannedVisitsQuery(), default);
        Assert.Equal(2, all.Data!.Items.Count);
        Assert.Equal(200, (await new GetPlannedVisitByIdHandler(TenantCtx(), store, Manager(), names)
            .Handle(new GetPlannedVisitByIdQuery(theirs.Id), default)).StatusCode);
    }

    [Fact]
    public async Task Without_read_all_another_reps_session_is_unlisted_and_404()
    {
        var mine = Session(Me);
        var theirs = Session(Other);
        var store = new SessionStore(mine, theirs);
        var names = new VisitTargetNameReader(new FakeAccountRepository(), new FakeContactRepository());

        var list = await new ListPlanningSessionsHandler(TenantCtx(), store, Rep()).Handle(new ListPlanningSessionsQuery(), default);
        Assert.Equal(new[] { mine.Id }, list.Data!.Items.Select(i => i.PlanningSessionId));
        Assert.Equal(404, (await new GetPlanningSessionByIdHandler(TenantCtx(), store, Rep(), names)
            .Handle(new GetPlanningSessionByIdQuery(theirs.Id), default)).StatusCode);
        Assert.Equal(404, (await new UpdatePlanningSessionSelectionHandler(TenantCtx(), new NullActorContext(), store, Rep())
            .Handle(new UpdatePlanningSessionSelectionCommand(theirs.Id, null, null, null, null, null, null, null, null), default)).StatusCode);
        Assert.Equal(0, store.Replaced);

        var all = await new ListPlanningSessionsHandler(TenantCtx(), store, Manager()).Handle(new ListPlanningSessionsQuery(), default);
        Assert.Equal(2, all.Data!.Items.Count);
    }

    [Fact]
    public async Task Without_read_all_a_report_on_another_reps_visit_cannot_be_read_listed_or_recorded()
    {
        var plans = new FakePlannedVisitRepository();
        var mine = Plan(Me, PlannedVisitTargetType.Account, Guid.NewGuid(), null, null);
        var theirs = Plan(Other, PlannedVisitTargetType.Account, Guid.NewGuid(), null, null);
        plans.Items.AddRange(new[] { mine, theirs });
        var reports = new FakeVisitReportRepository();
        var myReport = new VisitReportEntity { Id = Guid.NewGuid(), TenantId = Tenant, PlannedVisitId = mine.Id, ReportedByResourceId = Me };
        var theirReport = new VisitReportEntity { Id = Guid.NewGuid(), TenantId = Tenant, PlannedVisitId = theirs.Id, ReportedByResourceId = Other };
        reports.Items.AddRange(new[] { myReport, theirReport });

        var list = await new ListVisitReportsHandler(TenantCtx(), reports, plans, Rep()).Handle(new ListVisitReportsQuery(), default);
        Assert.Equal(new[] { myReport.Id }, list.Data!.Items.Select(i => i.VisitReportId));
        Assert.Equal(404, (await new GetVisitReportByIdHandler(TenantCtx(), reports, plans, Rep())
            .Handle(new GetVisitReportByIdQuery(theirReport.Id), default)).StatusCode);
        Assert.Equal(404, (await new RecordVisitOutcomeHandler(TenantCtx(), new NullActorContext(), reports, plans, Rep())
            .Handle(new RecordVisitOutcomeCommand(theirs.Id, "done", null, null, null, null, null, null), default)).StatusCode);

        Assert.Equal(2, (await new ListVisitReportsHandler(TenantCtx(), reports, plans, Manager())
            .Handle(new ListVisitReportsQuery(), default)).Data!.Items.Count);
    }

    // ═════════════════════════════════ 4 · B-1 resource from the caller ════════════════════════════════════════════════

    [Fact]
    public async Task A_planned_visit_for_another_resource_is_403_resource_not_caller_and_an_empty_one_is_the_caller()
    {
        var store = new FakePlannedVisitRepository();
        var accounts = new FakeAccountRepository();
        var account = Account("Klinik E");
        accounts.Items.Add(account);
        var names = new FixedUserNames(Me, "Beste Pullukçu");

        var foreign = await CreateHandler(store, accounts, Rep(), names)
            .Handle(CreateCmd(account.Id, resourceId: Other), default);
        Assert.Equal(403, foreign.StatusCode);
        Assert.Contains(VisitOwnership.ResourceNotCaller, foreign.Errors!);
        Assert.Empty(store.Items);

        var empty = await CreateHandler(store, accounts, Rep(), names).Handle(CreateCmd(account.Id, resourceId: ""), default);
        Assert.Equal(201, empty.StatusCode);
        var row = Assert.Single(store.Items);
        Assert.Equal(Me, row.Resource.ResourceId);
        Assert.Equal("Beste Pullukçu", row.Resource.DisplayName); // from the user directory, not the client

        // read-all may plan for another rep (a second target: one active plan per target, day and type).
        var second = Account("Klinik E2");
        accounts.Items.Add(second);
        var managed = await CreateHandler(store, accounts, Manager(), names)
            .Handle(CreateCmd(second.Id, resourceId: Other, code: "PV-2"), default);
        Assert.Equal(201, managed.StatusCode);
        Assert.Equal(Other, store.Items.Single(v => v.TargetId == second.Id).Resource.ResourceId);
    }

    [Fact]
    public async Task A_session_for_another_resource_is_403_and_an_empty_one_is_the_caller()
    {
        var store = new SessionStore();
        var handler = new CreatePlanningSessionHandler(
            TenantCtx(), new NullActorContext(), store, Rep(), new FixedUserNames(Me, "Beste"));

        var foreign = await handler.Handle(SessionCmd(Other), default);
        Assert.Equal(403, foreign.StatusCode);
        Assert.Equal(VisitOwnership.ResourceNotCaller, foreign.Errors![0]);

        var ok = await handler.Handle(SessionCmd(""), default);
        Assert.Equal(201, ok.StatusCode);
        var s = Assert.Single(store.Items);
        Assert.Equal(Me, s.ResourceId);
        Assert.Equal("Beste", s.ResourceDisplayName);
        // B-3 — the request's segment / campaign / strategy are never stored.
        Assert.Null(s.Selection.SegmentId);
        Assert.Null(s.Selection.CampaignId);
        Assert.Null(s.Provenance.StrategyTemplateId);
    }

    [Fact]
    public async Task Resources_me_carries_the_display_name()
    {
        var r = await new GetMyResourcesQueryHandler(TenantCtx(), new FixedUserNames(Me, "Beste Pullukçu"))
            .Handle(new GetMyResourcesQuery(Me), default);

        var item = Assert.Single(r.Data!.Items);
        Assert.Equal(Me, item.ResourceId);
        Assert.Equal("Beste Pullukçu", item.DisplayName);
    }

    // ═════════════════════════════════ 5 · B-2 territory ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task A_province_filter_finds_the_accounts_assigned_to_its_districts()
    {
        var t = new TerritoryWorld();
        var districtAccount = t.Account("Şişli Klinik", t.Sisli);
        var provinceAccount = t.Account("İl Klinik", t.Istanbul);
        t.Account("Ankara Klinik", t.Ankara);

        var handler = new GetAccountListHandler(TenantCtx(), t.Accounts, t.AccountAssignments, t.Models, t.Nodes);
        var page = await handler.Handle(new GetAccountListQuery(null, 1, 25, TerritoryNodeId: t.Istanbul.ToString()), default);

        Assert.Equal(
            new[] { districtAccount, provinceAccount }.OrderBy(x => x).ToArray(),
            page.Data!.Items.Select(i => i.Id).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task My_accounts_exact_assignment_covers_the_node_only_and_subtree_covers_its_descendants()
    {
        var t = new TerritoryWorld();
        var province = t.Account("İl Klinik", t.Istanbul);
        var district = t.Account("Şişli Klinik", t.Sisli);
        t.Account("Ankara Klinik", t.Ankara);

        t.AssignRep(Me, t.Istanbul, TerritoryCoverageScopes.ExactTerritory);
        var exact = await t.MyAccounts(Rep()).Handle(new GetMyAccountsQuery(), default);
        Assert.Equal(MyTerritoryStatuses.Assigned, exact.Data!.TerritoryStatus);
        Assert.Equal(new[] { province }, exact.Data.Items.Select(i => i.AccountId));

        t.ResourceAssignments.Items.Clear();
        t.AssignRep(Me, t.Istanbul, TerritoryCoverageScopes.TerritorySubtree);
        var subtree = await t.MyAccounts(Rep()).Handle(new GetMyAccountsQuery(), default);
        Assert.Equal(new[] { district, province }.OrderBy(x => x), subtree.Data!.Items.Select(i => i.AccountId).OrderBy(x => x));
        Assert.Equal("İstanbul", Assert.Single(subtree.Data.Territories).Name);
    }

    [Fact]
    public async Task My_accounts_ignores_an_assignment_outside_its_validity_window_and_unassigned_returns_everything()
    {
        var t = new TerritoryWorld();
        t.Account("İl Klinik", t.Istanbul);
        t.Account("Ankara Klinik", t.Ankara);
        t.AssignRep(Me, t.Istanbul, TerritoryCoverageScopes.ExactTerritory,
            validFrom: DateTimeOffset.UtcNow.AddYears(-2), validTo: DateTimeOffset.UtcNow.AddYears(-1)); // ended

        var r = await t.MyAccounts(Rep()).Handle(new GetMyAccountsQuery(), default);

        Assert.Equal(MyTerritoryStatuses.Unassigned, r.Data!.TerritoryStatus); // K-5
        Assert.Equal(2, r.Data.Items.Count);
        Assert.Empty(r.Data.Territories);

        var foreign = await t.MyAccounts(Rep()).Handle(new GetMyAccountsQuery(ResourceId: Other), default);
        Assert.Equal(403, foreign.StatusCode);
    }

    // ═════════════════════════════════ 6 · B-3 server derivation ═══════════════════════════════════════════════════════

    [Fact]
    public async Task The_play_is_derived_from_the_doctors_own_active_segments()
    {
        var d = new DerivationWorld();
        var segment = d.Segment();
        var play = d.Play(segment, "PLAY-A", 1);
        d.Members.Add(segment);

        var derived = await d.Deriver().DerivePlayAsync(d.Doctor, DateTimeOffset.UtcNow, default);

        Assert.Equal(play, derived.StrategyTemplateId); // resolved instead of no-strategy
        Assert.Equal(segment, derived.SegmentId);
        Assert.Null(derived.ReasonCode);

        var none = await new DerivationWorld().Deriver().DerivePlayAsync(Guid.NewGuid(), DateTimeOffset.UtcNow, default);
        Assert.Null(none.StrategyTemplateId);
        Assert.Equal(VisitProvenanceDeriver.NoStrategy, none.ReasonCode);
    }

    [Fact]
    public async Task Two_plays_pick_by_code_then_version_and_say_multiple_plays()
    {
        var d = new DerivationWorld();
        var s1 = d.Segment();
        var s2 = d.Segment();
        d.Play(s1, "PLAY-B", 1);
        var expected = d.Play(s2, "PLAY-A", 2);
        d.Play(s2, "PLAY-A", 3);
        d.Members.AddRange(new[] { s1, s2 });

        var derived = await d.Deriver().DerivePlayAsync(d.Doctor, DateTimeOffset.UtcNow, default);

        Assert.Equal(expected, derived.StrategyTemplateId);
        Assert.Equal(VisitProvenanceDeriver.MultiplePlays, derived.ReasonCode);
    }

    [Fact]
    public async Task The_campaign_is_the_active_one_targeting_the_doctor_earliest_start_first()
    {
        var d = new DerivationWorld();
        var late = d.Campaign("C-2", DateTimeOffset.UtcNow.AddDays(-5), d.Doctor);
        var early = d.Campaign("C-1", DateTimeOffset.UtcNow.AddDays(-30), d.Doctor);
        d.Campaign("C-0", DateTimeOffset.UtcNow.AddDays(-60), Guid.NewGuid()); // another doctor
        _ = late;

        var campaign = await d.Deriver().DeriveCampaignAsync(d.Doctor, null, Future, default);

        Assert.Equal(early, campaign);
        Assert.Null(await d.Deriver().DeriveCampaignAsync(Guid.NewGuid(), null, Future, default));
    }

    [Fact]
    public async Task An_update_without_provenance_keeps_the_derived_provenance()
    {
        var store = new FakePlannedVisitRepository();
        var accounts = new FakeAccountRepository();
        var account = Account("Klinik F");
        accounts.Items.Add(account);
        var deriver = new FixedProvenanceDeriver
        {
            Play = new DerivedPlay(Guid.NewGuid(), Guid.NewGuid(), Array.Empty<Guid>(), null),
            Campaign = Guid.NewGuid()
        };
        Assert.Equal(201, (await CreateHandler(store, accounts, Rep(), new FixedUserNames(Me, "B"), deriver)
            .Handle(CreateCmd(account.Id, resourceId: Me), default)).StatusCode);
        var row = Assert.Single(store.Items);

        // The client sends NO campaign / play / segment (and could not change them if it did).
        var update = await UpdateHandler(store, Rep(), accounts, deriver)
            .Handle(UpdateCmd(row.Id, Me, account.Id, campaignId: Guid.NewGuid()), default);

        Assert.Equal(200, update.StatusCode);
        Assert.Equal(deriver.Campaign, row.CampaignId);
        Assert.Equal(deriver.Campaign, row.Selection!.CampaignId);
        Assert.Equal(deriver.Play.StrategyTemplateId, row.Selection.StrategyTemplateId);
        Assert.Equal(deriver.Play.SegmentId, row.Selection.SegmentId);
    }

    // ═════════════════════════════════ helpers ═════════════════════════════════════════════════════════════════════════

    private static AccountEntity Account(string name, string status = "active")
        => new() { Id = Guid.NewGuid(), TenantId = Tenant, AccountName = name, AccountType = "clinic", Status = status };

    private static Contact Contact(string name, string status = "active")
        => new() { Id = Guid.NewGuid(), TenantId = Tenant, DisplayName = name, Status = status };

    private static PlannedVisitEntity Plan(string resource, string targetType, Guid targetId, Guid? accountId, Guid? contactId)
        => new()
        {
            Id = Guid.NewGuid(),
            TenantId = Tenant,
            VisitCode = "PV-" + Guid.NewGuid().ToString("N")[..6],
            TargetType = targetType,
            TargetId = targetId,
            AccountId = accountId,
            ContactId = contactId,
            PlannedDate = Future,
            PlanStatus = PlannedVisitStatus.Planned,
            Source = PlannedVisitSource.Manual,
            VisitPurpose = PlannedVisitPurpose.MedicalVisit,
            VisitType = PlannedVisitType.FieldVisit,
            Resource = new PlannedVisitResourceRef { ResourceId = resource, ResourceType = PlannedVisitResourceTypes.User }
        };

    private static PlanningSession Session(string resource) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = Tenant,
        CyclePeriodId = Guid.NewGuid(),
        ResourceId = resource,
        ResourceType = PlanningSessionResourceTypes.User,
        Status = PlanningSessionStatus.Draft
    };

    private static CreatePlanningSessionCommand SessionCmd(string resourceId) => new(
        Guid.NewGuid(), resourceId, "user", "Client Name", null, null, null,
        SegmentId: Guid.NewGuid(), CampaignId: Guid.NewGuid(), StrategyTemplateId: Guid.NewGuid());

    private static CreatePlannedVisitHandler CreateHandler(
        FakePlannedVisitRepository store, FakeAccountRepository accounts, ICallerScope caller, IUserDisplayNameResolver names,
        IVisitProvenanceDeriver? deriver = null)
        => new(TenantCtx(), new NullActorContext(), store,
            new PlannedVisitWriteGuards(accounts, new FakeContactRepository(), new FakeLinkRepository(), new FakeCampaignRepository()),
            new PlannedVisitJourneyProbe(new FakeJourneyReader()), new PlannedVisitFrequencyProbe(new FakeFrequencyResolver()),
            new PlannedVisitConsentProbe(new FakeConsentEvaluator()),
            new PlannedVisitAvailabilityProbe(TenantCtx(), new FakeAvailabilityRepository()),
            caller, names, deriver ?? new FixedProvenanceDeriver());

    private static UpdatePlannedVisitHandler UpdateHandler(
        FakePlannedVisitRepository store, ICallerScope caller, FakeAccountRepository? accounts = null,
        IVisitProvenanceDeriver? deriver = null)
        => new(TenantCtx(), new NullActorContext(), store,
            new PlannedVisitWriteGuards(accounts ?? new FakeAccountRepository(), new FakeContactRepository(),
                new FakeLinkRepository(), new FakeCampaignRepository()),
            new PlannedVisitJourneyProbe(new FakeJourneyReader()), new PlannedVisitFrequencyProbe(new FakeFrequencyResolver()),
            new PlannedVisitConsentProbe(new FakeConsentEvaluator()),
            new PlannedVisitAvailabilityProbe(TenantCtx(), new FakeAvailabilityRepository()),
            caller, new NullUserDisplayNameResolver(), deriver ?? new FixedProvenanceDeriver());

    private static CreatePlannedVisitCommand CreateCmd(Guid accountId, string resourceId, string code = "PV-1") => new(
        code, PlannedVisitTargetType.Account, accountId, Future.ToString("yyyy-MM-dd"), null, null, null,
        resourceId, "", "Client Name", null, null,
        PlannedVisitPurpose.MedicalVisit, PlannedVisitType.FieldVisit, null, null, null, null, null,
        Guid.NewGuid(), null, null, PlannedVisitStatus.Planned, null, null, Guid.NewGuid(), Guid.NewGuid());

    private static UpdatePlannedVisitCommand UpdateCmd(Guid id, string resourceId, Guid? accountId = null, Guid? campaignId = null) => new(
        id, PlannedVisitTargetType.Account, accountId ?? Guid.NewGuid(), Future.ToString("yyyy-MM-dd"), null, null, null,
        resourceId, "", null, null, null,
        PlannedVisitPurpose.MedicalVisit, PlannedVisitType.FieldVisit, null, null, null, null, null,
        campaignId, null, null, null, null, null, null);

    private sealed class FixedUserNames(string userId, string name) : IUserDisplayNameResolver
    {
        public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, string>>(userIds.Contains(Guid.Parse(userId))
                ? new Dictionary<Guid, string> { [Guid.Parse(userId)] = name }
                : new Dictionary<Guid, string>());
    }

    private sealed class SessionStore(params PlanningSession[] sessions) : IPlanningSessionRepository
    {
        public List<PlanningSession> Items { get; } = sessions.ToList();
        public int Replaced { get; private set; }

        public Task<PlanningSession?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(s => s.TenantId == tenantId && s.Id == id));

        public Task<IReadOnlyList<PlanningSession>> ListAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PlanningSession>>(Items.Where(s => s.TenantId == tenantId).ToList());

        public Task<IReadOnlyList<PlanningSession>> ListByPeriodAndResourceAsync(
            Guid tenantId, Guid cyclePeriodId, string resourceId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<PlanningSession>>(Items.ToList());

        public Task InsertAsync(PlanningSession entity, CancellationToken ct) { Items.Add(entity); return Task.CompletedTask; }

        public Task<bool> ReplaceAsync(PlanningSession entity, int expectedVersion, CancellationToken ct) { Replaced++; return Task.FromResult(true); }
    }

    /// <summary>A model with İstanbul (zone root) → Şişli (district), and Ankara beside it; accounts assigned to nodes.</summary>
    private sealed class TerritoryWorld
    {
        public FakeTerritoryModelRepo Models { get; } = new();
        public FakeTerritoryNodeRepo Nodes { get; } = new();
        public FakeAccountTerritoryAssignmentRepo AccountAssignments { get; } = new();
        public FakeTerritoryResourceAssignmentRepo ResourceAssignments { get; } = new();
        public ScopedAccounts Accounts { get; } = new();
        public Guid ModelId { get; } = Guid.NewGuid();
        public Guid Istanbul { get; } = Guid.NewGuid();
        public Guid Sisli { get; } = Guid.NewGuid();
        public Guid Ankara { get; } = Guid.NewGuid();

        public TerritoryWorld()
        {
            var from = DateTimeOffset.UtcNow.AddYears(-1);
            Models.Items.Add(new TerritoryModel { Id = ModelId, TenantId = Tenant, ModelCode = "TR", Status = "active", EffectiveFrom = from });
            Nodes.Items.Add(Node(Istanbul, null, "IST", "İstanbul", from));
            Nodes.Items.Add(Node(Sisli, Istanbul, "IST-SISLI", "Şişli", from));
            Nodes.Items.Add(Node(Ankara, null, "ANK", "Ankara", from));
        }

        private TerritoryNode Node(Guid id, Guid? parent, string code, string name, DateTimeOffset from) => new()
        {
            Id = id, TenantId = Tenant, ModelId = ModelId, ParentTerritoryId = parent, TerritoryCode = code, Name = name,
            Status = "active", EffectiveFrom = from
        };

        public Guid Account(string name, Guid node)
        {
            var a = new AccountEntity { Id = Guid.NewGuid(), TenantId = Tenant, AccountName = name, AccountType = "clinic", Status = "active" };
            Accounts.Items.Add(a);
            AccountAssignments.Items.Add(new AccountTerritoryAssignment
            {
                Id = Guid.NewGuid(), TenantId = Tenant, AccountId = a.Id, TerritoryModelId = ModelId, TerritoryNodeId = node,
                TerritoryNodeCode = "N", TerritoryNodeName = "N", AssignmentStatus = "active",
                EffectiveFrom = DateTimeOffset.UtcNow.AddYears(-1)
            });
            return a.Id;
        }

        public void AssignRep(string resource, Guid node, string scope, DateTimeOffset? validFrom = null, DateTimeOffset? validTo = null)
            => ResourceAssignments.Items.Add(new TerritoryResourceAssignment
            {
                Id = Guid.NewGuid(), TenantId = Tenant, ModelId = ModelId, TerritoryId = node,
                Resource = new TerritoryResourceRef { ResourceId = resource, ResourceType = "user" },
                CoverageScope = scope, Status = "active",
                ValidFrom = validFrom ?? DateTimeOffset.UtcNow.AddMonths(-1), ValidTo = validTo ?? DateTimeOffset.UtcNow.AddMonths(6)
            });

        public GetMyAccountsQueryHandler MyAccounts(ICallerScope caller)
            => new(TenantCtx(), caller, ResourceAssignments, Nodes, Models, AccountAssignments, Accounts);
    }

    /// <summary>An account store that honours the coverage scope / search / type the handlers pass.</summary>
    private sealed class ScopedAccounts : IAccountRepository
    {
        public List<AccountEntity> Items { get; } = new();

        public Task<AccountEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(a => a.Id == id));
        public Task<AccountEntity?> GetByCodeAsync(Guid tenantId, string accountCode, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ExistsByCodeAsync(Guid tenantId, string accountCode, Guid? excludeId, CancellationToken ct) => throw new NotSupportedException();

        public Task<(IReadOnlyList<AccountEntity> Items, long Total, long UnfilteredTotal)> ListAsync(
            Guid tenantId, string? search, int page, int pageSize, string? sortBy, string? sortDir,
            IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes,
            IReadOnlyCollection<Guid>? accountIdScope, CancellationToken ct)
        {
            var q = Items.Where(a => a.TenantId == tenantId
                                     && (accountIdScope is null || accountIdScope.Contains(a.Id))
                                     && (search is null || a.AccountName.Contains(search, StringComparison.OrdinalIgnoreCase))
                                     && (accountTypes is null || accountTypes.Contains(a.AccountType)))
                .ToList();
            return Task.FromResult<(IReadOnlyList<AccountEntity>, long, long)>(
                (q.Skip((page - 1) * pageSize).Take(pageSize).ToList(), q.Count, Items.Count));
        }

        public Task<IReadOnlyList<AccountEntity>> GetChildrenAsync(Guid tenantId, Guid parentId, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid accountId, Guid candidateParentId, CancellationToken ct) => throw new NotSupportedException();
        public Task InsertAsync(AccountEntity account, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(AccountEntity account, CancellationToken ct) => throw new NotSupportedException();
    }

    /// <summary>Segments, memberships, plays and campaigns for the real <see cref="VisitProvenanceDeriver"/>.</summary>
    private sealed class DerivationWorld
    {
        public Guid Doctor { get; } = Guid.NewGuid();
        public List<Guid> Members { get; } = new();
        private readonly List<Segment> _segments = new();
        private readonly List<(Guid Segment, StrategyTemplateSummary Play)> _plays = new();
        private readonly List<Domain.Entities.Campaign> _campaigns = new();
        private readonly List<CampaignTarget> _targets = new();

        public Guid Segment()
        {
            var s = new Segment
            {
                Id = Guid.NewGuid(), TenantId = Tenant, SegmentCode = "S", SubjectType = SegmentSubjectTypes.Contact,
                SegmentStatus = SegmentStatuses.Active
            };
            _segments.Add(s);
            return s.Id;
        }

        public Guid Play(Guid segment, string code, int version)
        {
            var id = Guid.NewGuid();
            _plays.Add((segment, new StrategyTemplateSummary(id, code, code, "active", version, DateTimeOffset.MinValue, null)));
            return id;
        }

        public Guid Campaign(string code, DateTimeOffset start, Guid doctor)
        {
            var c = new Domain.Entities.Campaign
            {
                Id = Guid.NewGuid(), TenantId = Tenant, CampaignCode = code, CampaignStatus = CampaignStatuses.Active, StartDate = start
            };
            _campaigns.Add(c);
            _targets.Add(new CampaignTarget
            {
                Id = Guid.NewGuid(), TenantId = Tenant, CampaignId = c.Id, TargetType = CampaignTargetTypes.Contact, TargetId = doctor,
                TargetStatus = CampaignTargetStatuses.Active, EffectiveFrom = start
            });
            return c.Id;
        }

        public VisitProvenanceDeriver Deriver() => new(
            TenantCtx(), new Segments(_segments), new Membership(Members), new Plays(_plays),
            new Campaigns(_campaigns), new Targets(_targets));

        private sealed class Segments(List<Segment> items) : ISegmentRepository
        {
            public Task<Segment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => Task.FromResult(items.FirstOrDefault(s => s.Id == id));
            public Task<IReadOnlyList<Segment>> ListAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<Segment>>(items);
            public Task<IReadOnlyList<Segment>> ListByLineageAsync(Guid tenantId, Guid lineageId, CancellationToken ct) => throw new NotSupportedException();
            public Task<IReadOnlyList<Segment>> ListByCodeAsync(Guid tenantId, string segmentCode, CancellationToken ct) => throw new NotSupportedException();
            public Task InsertAsync(Segment entity, CancellationToken ct) => throw new NotSupportedException();
            public Task<bool> ReplaceAsync(Segment entity, int expectedVersion, CancellationToken ct) => throw new NotSupportedException();
        }

        private sealed class Membership(List<Guid> members) : ISegmentMembershipReader
        {
            public Task<SegmentMembershipVerdict> IsMemberAsync(
                Guid segmentId, string subjectType, Guid subjectId, DateTimeOffset at, CancellationToken ct)
                => Task.FromResult(new SegmentMembershipVerdict(segmentId, 1, subjectType, subjectId,
                    members.Contains(segmentId) ? SegmentMembershipVerdicts.Member : SegmentMembershipVerdicts.NotMember,
                    Array.Empty<string>(), at));

            public Task<SegmentResolutionResult> ResolveAsync(Guid segmentId, DateTimeOffset at, int limit, int offset, CancellationToken ct)
                => throw new NotSupportedException();
        }

        private sealed class Plays(List<(Guid Segment, StrategyTemplateSummary Play)> plays) : IStrategyTemplateReader
        {
            public Task<StrategyTemplateBindingSet?> GetActiveBindingsAsync(Guid templateId, DateTimeOffset at, CancellationToken ct)
                => Task.FromResult<StrategyTemplateBindingSet?>(null);

            public Task<IReadOnlyList<StrategyTemplateSummary>> ListBySegmentAsync(Guid segmentId, DateTimeOffset at, CancellationToken ct)
                => Task.FromResult<IReadOnlyList<StrategyTemplateSummary>>(plays.Where(p => p.Segment == segmentId).Select(p => p.Play).ToList());
        }

        private sealed class Campaigns(List<Domain.Entities.Campaign> items) : ICampaignRepository
        {
            public Task<Domain.Entities.Campaign?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => throw new NotSupportedException();
            public Task<IReadOnlyList<Domain.Entities.Campaign>> ListAsync(Guid tenantId, CancellationToken ct)
                => Task.FromResult<IReadOnlyList<Domain.Entities.Campaign>>(items);
            public Task<Domain.Entities.Campaign?> GetActiveByCodeAsync(Guid tenantId, string campaignCode, CancellationToken ct) => throw new NotSupportedException();
            public Task<Domain.Entities.Campaign?> FindByExternalReferenceAsync(Guid tenantId, string sourceSystem, string externalId, CancellationToken ct)
                => throw new NotSupportedException();
            public Task InsertAsync(Domain.Entities.Campaign campaign, CancellationToken ct) => throw new NotSupportedException();
            public Task UpdateAsync(Domain.Entities.Campaign campaign, CancellationToken ct) => throw new NotSupportedException();
        }

        private sealed class Targets(List<CampaignTarget> items) : ICampaignTargetRepository
        {
            public Task<CampaignTarget?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct) => throw new NotSupportedException();
            public Task<IReadOnlyList<CampaignTarget>> ListByCampaignAsync(Guid tenantId, Guid campaignId, CancellationToken ct) => throw new NotSupportedException();
            public Task<CampaignTarget?> FindActiveByTargetAsync(Guid tenantId, Guid campaignId, string targetType, Guid targetId, CancellationToken ct)
                => Task.FromResult(items.FirstOrDefault(t => t.CampaignId == campaignId && t.TargetType == targetType && t.TargetId == targetId));
            public Task InsertAsync(CampaignTarget target, CancellationToken ct) => throw new NotSupportedException();
            public Task UpdateAsync(CampaignTarget target, CancellationToken ct) => throw new NotSupportedException();
        }
    }
}
