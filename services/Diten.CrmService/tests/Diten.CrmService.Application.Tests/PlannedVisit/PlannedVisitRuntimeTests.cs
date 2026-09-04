using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.ConsentPreference.Evaluation;
using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;
using Diten.CrmService.Application.Features.PlannedVisit;
using Diten.CrmService.Application.Features.PlannedVisit.Commands;
using Diten.CrmService.Application.Features.PlannedVisit.Contract;
using Diten.CrmService.Application.Features.PlannedVisit.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.PlannedVisit.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.PlannedVisit.Provenance;
using Diten.CrmService.Application.Features.PlannedVisit.Queries;
using Diten.CrmService.Domain.Entities;
using Xunit;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Tests.PlannedVisit;

/// <summary>
/// MOD-0155 FU01 — PlannedVisit runtime. Pins down: TenantId is claim-only and a plan is born draft/planned; VisitCode
/// uniqueness survives archive; targets resolve with the derived nav copies and reject mismatch/pharmacy-type; the past-
/// date rule; consent is stored on create but enforced ONLY at confirm (fail-closed, unknown never allowed); the legacy
/// overlap + same-day-type guards; the four read-only probes; the lifecycle transitions; and the list/detail queries.
/// All in-memory (no Mongo, no cross-module mutation).
/// </summary>
public sealed class PlannedVisitRuntimeTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static DateOnly Future => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
    private static string FutureIso => Future.ToString("yyyy-MM-dd");

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class Fixture
    {
        public FakePlannedVisitRepository Repo { get; } = new();
        public FakeAccountRepository Accounts { get; } = new();
        public FakeContactRepository Contacts { get; } = new();
        public FakeLinkRepository Links { get; } = new();
        public FakeCampaignRepository Campaigns { get; } = new();
        public FakeAvailabilityRepository Availability { get; } = new();
        public FakeFrequencyResolver Frequency { get; } = new();
        public FakeConsentEvaluator Consent { get; } = new();
        public FakeJourneyReader Journeys { get; } = new();
        public Guid TenantId { get; }

        public Fixture(Guid? tenant = null) => TenantId = tenant ?? TenantA;

        private PlannedVisitWriteGuards Guards() => new(Accounts, Contacts, Links, Campaigns);
        private PlannedVisitFrequencyProbe FreqProbe() => new(Frequency);
        private PlannedVisitConsentProbe ConsentProbe() => new(Consent);
        private PlannedVisitJourneyProbe JourneyProbe() => new(Journeys);
        private PlannedVisitAvailabilityProbe AvailProbe(Guid tenant) => new(Tenant(tenant), Availability);

        public CreatePlannedVisitHandler Create(Guid? tenant = null)
        {
            var t = tenant ?? TenantId;
            return new(Tenant(t), new NullActorContext(), Repo, Guards(), JourneyProbe(), FreqProbe(),
                ConsentProbe(), AvailProbe(t));
        }

        public UpdatePlannedVisitHandler Update(Guid? tenant = null)
        {
            var t = tenant ?? TenantId;
            return new(Tenant(t), new NullActorContext(), Repo, Guards(), JourneyProbe(), FreqProbe(),
                ConsentProbe(), AvailProbe(t));
        }

        public ConfirmPlannedVisitHandler Confirm(Guid? tenant = null)
            => new(Tenant(tenant ?? TenantId), new NullActorContext(), Repo, ConsentProbe());

        public CancelPlannedVisitHandler Cancel(Guid? tenant = null)
            => new(Tenant(tenant ?? TenantId), new NullActorContext(), Repo);

        public ArchivePlannedVisitHandler Archive(Guid? tenant = null)
            => new(Tenant(tenant ?? TenantId), new NullActorContext(), Repo);

        public ListPlannedVisitsHandler List(Guid? tenant = null) => new(Tenant(tenant ?? TenantId), Repo);
        public GetPlannedVisitByIdHandler Get(Guid? tenant = null) => new(Tenant(tenant ?? TenantId), Repo);
        public GetPlannedVisitContractHandler Contract() => new(Tenant(TenantId));

        public Guid SeedAccount(string type = "clinic")
        {
            var id = Guid.NewGuid();
            Accounts.Items.Add(new Account { Id = id, TenantId = TenantId, AccountName = "Acme", AccountCode = "A1", AccountType = type, Status = "active" });
            return id;
        }

        public Guid SeedContact()
        {
            var id = Guid.NewGuid();
            Contacts.Items.Add(new Contact { Id = id, TenantId = TenantId, DisplayName = "Dr", ContactType = "hcp", Status = "active" });
            return id;
        }

        public Guid SeedLink(Guid accountId, Guid contactId)
        {
            var id = Guid.NewGuid();
            Links.Items.Add(new AccountContactLink { Id = id, TenantId = TenantId, AccountId = accountId, ContactId = contactId, RoleCode = "primary", Status = "active" });
            return id;
        }

        public Guid SeedCampaign()
        {
            var id = Guid.NewGuid();
            Campaigns.Items.Add(new Campaign { Id = id, TenantId = TenantId, CampaignCode = "C1", CampaignName = "Camp", CampaignStatus = "active", CampaignType = "detailing" });
            return id;
        }
    }

    private static CreatePlannedVisitCommand Cmd(
        Guid targetId,
        string code = "PV-1",
        string targetType = "account",
        string? plannedDate = null,
        string? start = null,
        string? end = null,
        int? duration = null,
        string purpose = "medical-visit",
        string visitType = "field-visit",
        string? planStatus = "draft",
        string? source = null,
        Guid? campaignId = null,
        Guid? journeyId = null,
        Guid? stageId = null,
        string? contentSource = null)
        => new(
            code, targetType, targetId, plannedDate ?? FutureIso, start, end, duration,
            "res-1", "person", "Rep One", null, null,
            purpose, visitType, null, null,
            null, null, null, campaignId,
            journeyId, stageId, planStatus, source, contentSource);

    private async Task<Guid> SeedPlanAsync(Fixture f, Guid targetId, string code, string status = "planned",
        string? start = null, string? end = null, string visitType = "field-visit")
    {
        var r = await f.Create().Handle(
            Cmd(targetId, code, planStatus: status, start: start, end: end, visitType: visitType), default);
        return r.Data;
    }

    // ── Create ───────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_valid_returns_201_and_stores_draft_with_claim_tenant()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var r = await f.Create().Handle(Cmd(acc, planStatus: "draft"), default);
        Assert.Equal(201, r.StatusCode);
        var row = Assert.Single(f.Repo.Items);
        Assert.Equal(TenantA, row.TenantId);
        Assert.Equal(PlannedVisitStatus.Draft, row.PlanStatus);
        Assert.Equal(acc, row.TargetId);
        Assert.Equal(acc, row.AccountId);
        Assert.Null(row.ContactId);
    }

    [Fact]
    public async Task Create_without_tenant_is_400()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var r = await f.Create(Guid.Empty).Handle(Cmd(acc), default);
        // Empty tenant still resolves as a tenant context here, so drive the no-tenant path with an unset context.
        var noTenant = new CreatePlannedVisitHandler(new TenantContext(), new NullActorContext(), f.Repo,
            new PlannedVisitWriteGuards(f.Accounts, f.Contacts, f.Links, f.Campaigns),
            new PlannedVisitJourneyProbe(f.Journeys), new PlannedVisitFrequencyProbe(f.Frequency),
            new PlannedVisitConsentProbe(f.Consent), new PlannedVisitAvailabilityProbe(new TenantContext(), f.Availability));
        var r2 = await noTenant.Handle(Cmd(acc), default);
        Assert.Equal(400, r2.StatusCode);
    }

    [Fact]
    public async Task Create_past_date_is_400()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var past = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd");
        var r = await f.Create().Handle(Cmd(acc, plannedDate: past), default);
        Assert.Equal(400, r.StatusCode);
        Assert.Contains(PlannedVisitErrorCodes.DateInPast, r.Errors!);
    }

    [Fact]
    public async Task Create_duplicate_code_among_non_archived_is_409()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        await f.Create().Handle(Cmd(acc, code: "DUP"), default);
        var r = await f.Create().Handle(Cmd(acc, code: "DUP"), default);
        Assert.Equal(409, r.StatusCode);
        Assert.Contains(PlannedVisitErrorCodes.CodeTaken, r.Errors!);
        Assert.Single(f.Repo.Items);
    }

    [Fact]
    public async Task Create_reserved_source_is_400()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var r = await f.Create().Handle(Cmd(acc, source: "campaign"), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Create_target_not_found_is_400()
    {
        var f = new Fixture();
        var r = await f.Create().Handle(Cmd(Guid.NewGuid()), default);
        Assert.Equal(400, r.StatusCode);
        Assert.Contains(PlannedVisitErrorCodes.TargetNotFound, r.Errors!);
    }

    [Fact]
    public async Task Create_target_type_mismatch_is_400()
    {
        var f = new Fixture();
        var contactId = f.SeedContact();
        // TargetType=account but the id is a contact → mismatch.
        var r = await f.Create().Handle(Cmd(contactId, targetType: "account"), default);
        Assert.Equal(400, r.StatusCode);
        Assert.Contains(PlannedVisitErrorCodes.TargetTypeMismatch, r.Errors!);
    }

    [Fact]
    public async Task Create_pharmacy_non_pharmacy_account_is_400()
    {
        var f = new Fixture();
        var clinic = f.SeedAccount("clinic");
        var r = await f.Create().Handle(Cmd(clinic, targetType: "pharmacy"), default);
        Assert.Equal(400, r.StatusCode);
        Assert.Contains(PlannedVisitErrorCodes.TargetTypeMismatch, r.Errors!);
    }

    [Fact]
    public async Task Create_pharmacy_account_resolves_to_account_id()
    {
        var f = new Fixture();
        var pharmacy = f.SeedAccount("pharmacy");
        var r = await f.Create().Handle(Cmd(pharmacy, targetType: "pharmacy"), default);
        Assert.Equal(201, r.StatusCode);
        var row = Assert.Single(f.Repo.Items);
        Assert.Equal(pharmacy, row.AccountId);
        Assert.Null(row.ContactId);
        // D9 - consent for a pharmacy is asked at the account level.
        Assert.Equal(ConsentSubjectType.Account, PlannedVisitValidation.ToConsentSubjectType(row.TargetType));
    }

    [Fact]
    public async Task Create_account_contact_link_derives_account_and_contact_ids()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var contact = f.SeedContact();
        var link = f.SeedLink(acc, contact);
        var r = await f.Create().Handle(Cmd(link, targetType: "account-contact-link"), default);
        Assert.Equal(201, r.StatusCode);
        var row = Assert.Single(f.Repo.Items);
        Assert.Equal(acc, row.AccountId);
        Assert.Equal(contact, row.ContactId);
        Assert.Equal(link, row.AccountContactLinkId);
    }

    [Fact]
    public async Task Create_campaign_not_found_is_400()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var r = await f.Create().Handle(Cmd(acc, campaignId: Guid.NewGuid()), default);
        Assert.Equal(400, r.StatusCode);
        Assert.Contains(PlannedVisitErrorCodes.CampaignNotFound, r.Errors!);
    }

    [Fact]
    public async Task Create_slot_is_null_born_and_selection_is_manual()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        await f.Create().Handle(Cmd(acc), default);
        var row = Assert.Single(f.Repo.Items);
        Assert.False(row.Slot.IsPacked);
        Assert.Null(row.Slot.SequenceOrder);
        Assert.Equal(PlannedVisitSelectionMode.Manual, row.Selection!.SelectionMode);
    }

    // ── Frequency / consent provenance on create ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_stores_unknown_frequency_and_still_creates()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        // default resolver is unknown
        await f.Create().Handle(Cmd(acc), default);
        var row = Assert.Single(f.Repo.Items);
        Assert.Equal(FrequencyStatus.Unknown, row.Frequency!.FrequencyStatus);
    }

    [Fact]
    public async Task Create_stores_resolved_frequency_provenance()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var policyId = Guid.NewGuid();
        f.Frequency.Result = FakeFrequencyResolver.Resolved(policyId);
        await f.Create().Handle(Cmd(acc), default);
        var row = Assert.Single(f.Repo.Items);
        Assert.Equal(FrequencyStatus.Resolved, row.Frequency!.FrequencyStatus);
        Assert.Equal(policyId, row.Frequency.SelectedFrequencyPolicyId);
        Assert.Equal(2, row.Frequency.RequiredVisitCount);
    }

    [Fact]
    public async Task Create_stores_consent_but_does_not_enforce_when_blocked()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        f.Consent.Status = ConsentEligibilityStatus.Blocked;
        var r = await f.Create().Handle(Cmd(acc, planStatus: "planned"), default);
        Assert.Equal(201, r.StatusCode); // blocked does NOT stop create (D6 - only confirm)
        var row = Assert.Single(f.Repo.Items);
        Assert.Equal(ConsentEligibilityStatus.Blocked, row.Consent!.EligibilityStatus);
        Assert.True(row.Consent.FilterApplied);
    }

    // ── Journey probe (content position) ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_with_unpublished_journey_is_400()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        // reader returns no published journeys → the chosen id is not published
        var r = await f.Create().Handle(Cmd(acc, journeyId: Guid.NewGuid()), default);
        Assert.Equal(400, r.StatusCode);
        Assert.Contains(PlannedVisitErrorCodes.JourneyNotPublished, r.Errors!);
    }

    [Fact]
    public async Task Create_with_published_journey_and_stage_stores_content_ref()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var journeyId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        f.Journeys.Journeys.Add(FakeJourneyReader.Journey(journeyId, "Onboarding",
            new[] { FakeJourneyReader.Stage(stageId, 1, "S1", "Intro") }));
        var r = await f.Create().Handle(Cmd(acc, journeyId: journeyId, stageId: stageId), default);
        Assert.Equal(201, r.StatusCode);
        var row = Assert.Single(f.Repo.Items);
        Assert.Equal(journeyId, row.Content!.JourneyId);
        Assert.Equal(stageId, row.Content.StageId);
        Assert.Equal(0, row.Content.StageIndex); // ordinal on the resolved path; FU01 never advances it
        Assert.Equal("S1", row.Content.StageCode);
        Assert.Equal(PlannedVisitContentSource.Manual, row.Content.ContentSource);
    }

    [Fact]
    public async Task Create_with_stage_not_in_journey_is_400()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var journeyId = Guid.NewGuid();
        f.Journeys.Journeys.Add(FakeJourneyReader.Journey(journeyId, "J",
            new[] { FakeJourneyReader.Stage(Guid.NewGuid(), 1, "S1", "Intro") }));
        var r = await f.Create().Handle(Cmd(acc, journeyId: journeyId, stageId: Guid.NewGuid()), default);
        Assert.Equal(400, r.StatusCode);
        Assert.Contains(PlannedVisitErrorCodes.StageNotInJourney, r.Errors!);
    }

    [Fact]
    public async Task Create_without_journey_has_no_content_ref()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        await f.Create().Handle(Cmd(acc), default);
        Assert.Null(Assert.Single(f.Repo.Items).Content);
    }

    [Fact]
    public async Task Create_invalid_content_source_is_400()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var journeyId = Guid.NewGuid();
        f.Journeys.Journeys.Add(FakeJourneyReader.Journey(journeyId, "J", Array.Empty<ContentEngagementJourneyStageDto>()));
        var r = await f.Create().Handle(Cmd(acc, journeyId: journeyId, contentSource: "auto"), default);
        Assert.Equal(400, r.StatusCode);
    }

    // ── Availability probe (warning) ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_with_availability_conflict_still_creates_with_warning_snapshot()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var contact = f.SeedContact();
        var link = f.SeedLink(acc, contact);
        var weekday = Future.DayOfWeek.ToString().ToLowerInvariant();
        f.Availability.Items.Add(new ContactAvailability
        {
            TenantId = TenantA, AccountContactLinkId = link, ContactId = contact, AccountId = acc,
            Weekday = weekday, StartTime = "09:00", EndTime = "12:00", Status = AvailabilityLifecycle.Active
        });
        // planned window 13:00-14:00 is outside 09:00-12:00
        var r = await f.Create().Handle(Cmd(link, targetType: "account-contact-link", start: "13:00", end: "14:00"), default);
        Assert.Equal(201, r.StatusCode); // a warning, never a block (D13)
        var row = Assert.Single(f.Repo.Items);
        Assert.NotNull(row.Availability);
        Assert.False(row.Availability!.WithinAvailableWindow);
        Assert.Contains(PlannedVisitAvailabilityReasonCodes.OutsidePreferredWindow, row.Availability.ReasonCodes);
    }

    [Fact]
    public async Task Create_account_target_has_no_availability_snapshot()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        await f.Create().Handle(Cmd(acc), default);
        Assert.Null(Assert.Single(f.Repo.Items).Availability);
    }

    // ── Legacy planning guards (§21/L5-L6) ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Overlap_of_active_windows_for_one_resource_is_409()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var acc2 = f.SeedAccount();
        await SeedPlanAsync(f, acc, "P1", "planned", "09:00", "10:00");
        var r = await f.Create().Handle(Cmd(acc2, code: "P2", planStatus: "planned", start: "09:30", end: "10:30"), default);
        Assert.Equal(409, r.StatusCode);
        Assert.Contains(PlannedVisitErrorCodes.Overlap, r.Errors!);
    }

    [Fact]
    public async Task Windowless_plans_do_not_enter_overlap_check()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var acc2 = f.SeedAccount();
        await SeedPlanAsync(f, acc, "P1", "planned");
        var r = await f.Create().Handle(Cmd(acc2, code: "P2", planStatus: "planned"), default);
        Assert.Equal(201, r.StatusCode);
    }

    [Fact]
    public async Task Same_target_same_day_same_type_second_active_plan_is_409()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        await SeedPlanAsync(f, acc, "P1", "planned");
        var r = await f.Create().Handle(Cmd(acc, code: "P2", planStatus: "planned"), default);
        Assert.Equal(409, r.StatusCode);
        Assert.Contains(PlannedVisitErrorCodes.DuplicateSameDayType, r.Errors!);
    }

    [Fact]
    public async Task Same_target_same_day_different_type_is_allowed()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        await SeedPlanAsync(f, acc, "P1", "planned", visitType: "field-visit");
        var r = await f.Create().Handle(Cmd(acc, code: "P2", planStatus: "planned", visitType: "phone"), default);
        Assert.Equal(201, r.StatusCode);
    }

    [Fact]
    public async Task Cancelled_plan_is_not_an_overlap_candidate()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var id = await SeedPlanAsync(f, acc, "P1", "planned");
        await f.Cancel().Handle(new CancelPlannedVisitCommand(id, "no longer needed", null), default);
        // A new active plan of the same type on the same day is now fine (the cancelled one holds no slot).
        var r = await f.Create().Handle(Cmd(acc, code: "P2", planStatus: "planned"), default);
        Assert.Equal(201, r.StatusCode);
    }

    // ── Confirm (consent guard D6) ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Confirm_when_allowed_moves_to_confirmed()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var id = await SeedPlanAsync(f, acc, "P1", "planned");
        f.Consent.Status = ConsentEligibilityStatus.Allowed;
        var r = await f.Confirm().Handle(new ConfirmPlannedVisitCommand(id, null), default);
        Assert.Equal(200, r.StatusCode);
        Assert.Equal(PlannedVisitStatus.Confirmed, f.Repo.Items.Single(x => x.Id == id).PlanStatus);
    }

    [Fact]
    public async Task Confirm_when_blocked_is_409_and_stays_planned()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var id = await SeedPlanAsync(f, acc, "P1", "planned");
        f.Consent.Status = ConsentEligibilityStatus.Blocked;
        var r = await f.Confirm().Handle(new ConfirmPlannedVisitCommand(id, null), default);
        Assert.Equal(409, r.StatusCode);
        Assert.Contains(PlannedVisitErrorCodes.BlockedByConsent, r.Errors!);
        Assert.Equal(PlannedVisitStatus.Planned, f.Repo.Items.Single(x => x.Id == id).PlanStatus);
    }

    [Fact]
    public async Task Confirm_when_unknown_is_409()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var id = await SeedPlanAsync(f, acc, "P1", "planned");
        f.Consent.Status = ConsentEligibilityStatus.Unknown;
        var r = await f.Confirm().Handle(new ConfirmPlannedVisitCommand(id, null), default);
        Assert.Equal(409, r.StatusCode);
        Assert.Contains(PlannedVisitErrorCodes.ConsentUnknown, r.Errors!);
    }

    [Fact]
    public async Task Confirm_with_unresolvable_subject_is_filter_not_applied_409()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var id = await SeedPlanAsync(f, acc, "P1", "planned");
        // Force an empty subject id by clearing the derived account id on the stored row.
        f.Repo.Items.Single(x => x.Id == id).AccountId = Guid.Empty;
        f.Repo.Items.Single(x => x.Id == id).TargetId = Guid.Empty;
        var r = await f.Confirm().Handle(new ConfirmPlannedVisitCommand(id, null), default);
        Assert.Equal(409, r.StatusCode);
        Assert.Contains(PlannedVisitErrorCodes.ConsentFilterNotApplied, r.Errors!);
    }

    [Fact]
    public async Task Confirm_from_draft_is_invalid_409()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var id = await SeedPlanAsync(f, acc, "P1", "draft");
        var r = await f.Confirm().Handle(new ConfirmPlannedVisitCommand(id, null), default);
        Assert.Equal(409, r.StatusCode);
        Assert.Contains(PlannedVisitErrorCodes.InvalidTransition, r.Errors!);
    }

    // ── Cancel / Archive ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_requires_a_reason()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var id = await SeedPlanAsync(f, acc, "P1", "planned");
        var r = await f.Cancel().Handle(new CancelPlannedVisitCommand(id, "  ", null), default);
        Assert.Equal(400, r.StatusCode);
        Assert.Contains(PlannedVisitErrorCodes.CancellationReasonRequired, r.Errors!);
    }

    [Fact]
    public async Task Cancel_with_reason_succeeds_and_row_is_not_deleted()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var id = await SeedPlanAsync(f, acc, "P1", "planned");
        var r = await f.Cancel().Handle(new CancelPlannedVisitCommand(id, "reason", null), default);
        Assert.Equal(200, r.StatusCode);
        var row = f.Repo.Items.Single(x => x.Id == id);
        Assert.Equal(PlannedVisitStatus.Cancelled, row.PlanStatus);
        Assert.Equal("reason", row.CancellationReason);
    }

    [Fact]
    public async Task Archive_then_update_is_409()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var id = await SeedPlanAsync(f, acc, "P1", "planned");
        await f.Archive().Handle(new ArchivePlannedVisitCommand(id, null), default);
        var row = f.Repo.Items.Single(x => x.Id == id);
        Assert.Equal(PlannedVisitStatus.Archived, row.PlanStatus);

        var r = await f.Update().Handle(UpdateOf(row), default);
        Assert.Equal(409, r.StatusCode);
        Assert.Contains(PlannedVisitErrorCodes.Archived, r.Errors!);
    }

    [Fact]
    public async Task Archive_twice_is_409()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var id = await SeedPlanAsync(f, acc, "P1", "planned");
        await f.Archive().Handle(new ArchivePlannedVisitCommand(id, null), default);
        var r = await f.Archive().Handle(new ArchivePlannedVisitCommand(id, null), default);
        Assert.Equal(409, r.StatusCode);
    }

    // ── Update (concurrency + past-date rule) ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_with_stale_version_is_409()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var id = await SeedPlanAsync(f, acc, "P1", "draft");
        var row = f.Repo.Items.Single(x => x.Id == id);
        var cmd = UpdateOf(row) with { ExpectedVersion = row.Version + 5 };
        var r = await f.Update().Handle(cmd, default);
        Assert.Equal(409, r.StatusCode);
        Assert.Contains(PlannedVisitErrorCodes.ConcurrencyConflict, r.Errors!);
    }

    [Fact]
    public async Task Update_draft_with_past_date_is_allowed()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var id = await SeedPlanAsync(f, acc, "P1", "draft");
        var row = f.Repo.Items.Single(x => x.Id == id);
        var past = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)).ToString("yyyy-MM-dd");
        var r = await f.Update().Handle(UpdateOf(row) with { PlannedDate = past }, default);
        Assert.Equal(200, r.StatusCode);
    }

    [Fact]
    public async Task Cross_tenant_get_is_404()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var id = await SeedPlanAsync(f, acc, "P1", "planned");
        var r = await f.Get(TenantB).Handle(new GetPlannedVisitByIdQuery(id), default);
        Assert.Equal(404, r.StatusCode);
    }

    // ── Queries ──────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_hides_archived_by_default_and_shows_with_flag()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var id = await SeedPlanAsync(f, acc, "P1", "planned");
        await f.Archive().Handle(new ArchivePlannedVisitCommand(id, null), default);

        var hidden = await f.List().Handle(new ListPlannedVisitsQuery(), default);
        Assert.Empty(hidden.Data!.Items);

        var shown = await f.List().Handle(new ListPlannedVisitsQuery(IncludeArchived: true), default);
        Assert.Single(shown.Data!.Items);
    }

    [Fact]
    public async Task List_filters_by_resource_and_status()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var acc2 = f.SeedAccount();
        await SeedPlanAsync(f, acc, "P1", "planned");
        await SeedPlanAsync(f, acc2, "P2", "draft", visitType: "phone");

        var planned = await f.List().Handle(new ListPlannedVisitsQuery(PlanStatus: "planned"), default);
        Assert.Single(planned.Data!.Items);

        var byResource = await f.List().Handle(new ListPlannedVisitsQuery(ResourceId: "res-1"), default);
        Assert.Equal(2, byResource.Data!.Items.Count);
    }

    [Fact]
    public async Task GetById_returns_detail_with_provenance()
    {
        var f = new Fixture();
        var acc = f.SeedAccount();
        var id = await SeedPlanAsync(f, acc, "P1", "planned");
        var r = await f.Get().Handle(new GetPlannedVisitByIdQuery(id), default);
        Assert.Equal(200, r.StatusCode);
        Assert.Equal("P1", r.Data!.VisitCode);
        Assert.NotNull(r.Data.Consent);
        Assert.NotNull(r.Data.Frequency);
    }

    [Fact]
    public async Task Contract_publishes_ready_with_vocabulary()
    {
        var f = new Fixture();
        var r = await f.Contract().Handle(new GetPlannedVisitContractQuery(), default);
        Assert.Equal(200, r.StatusCode);
        Assert.True(r.Data!.IsReady);
        Assert.Contains("pharmacy", r.Data.Vocabularies.TargetTypes);
        Assert.True(r.Data.Features.SupportsPharmacyTarget);
        Assert.False(r.Data.Features.SupportsBulkDelete);
    }

    // ── Mapper ───────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Mapper_surfaces_date_as_iso_and_provenance_status()
    {
        var plan = new PlannedVisitEntity
        {
            Id = Guid.NewGuid(), TenantId = TenantA, VisitCode = "P1", TargetType = "account",
            TargetId = Guid.NewGuid(), PlannedDate = new DateOnly(2026, 3, 1), PlanStatus = "planned",
            VisitPurpose = "medical-visit", VisitType = "field-visit", Source = "manual",
            Resource = new PlannedVisitResourceRef { ResourceId = "res-1", ResourceType = "person" },
            Consent = new PlannedVisitConsentProvenance { EligibilityStatus = "allowed", FilterApplied = true },
            Frequency = new PlannedVisitFrequencyProvenance { FrequencyStatus = "resolved" }
        };
        var list = PlannedVisitMapper.ToListItem(plan);
        Assert.Equal("2026-03-01", list.PlannedDate);
        Assert.Equal("allowed", list.ConsentStatus);
        Assert.Equal("resolved", list.FrequencyStatus);

        var detail = PlannedVisitMapper.ToDetail(plan);
        Assert.Equal("2026-03-01", detail.PlannedDate);
        Assert.True(detail.IsPlanned);
        Assert.Equal("res-1", detail.Resource.ResourceId);
    }

    // ── helper: build an Update command mirroring a stored row ──────────────────────────────────────────────────────
    private static UpdatePlannedVisitCommand UpdateOf(PlannedVisitEntity row) => new(
        row.Id, row.TargetType, row.TargetId, row.PlannedDate.ToString("yyyy-MM-dd"),
        row.PlannedStartTime, row.PlannedEndTime, row.PlannedDurationMinutes,
        row.Resource.ResourceId, row.Resource.ResourceType, row.Resource.DisplayName, row.PositionCode, row.PositionId,
        row.VisitPurpose, row.VisitType, row.Objective, row.Notes,
        row.BusinessUnit, row.TerritoryNodeId, row.TerritoryModelId, row.CampaignId,
        row.Content?.JourneyId, row.Content?.StageId, row.Version);
}
