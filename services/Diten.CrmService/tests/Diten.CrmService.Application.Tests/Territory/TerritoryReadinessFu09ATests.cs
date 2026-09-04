using Diten.CrmService.Application.Features.Territory.Readiness;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using Vfp = Diten.CrmService.Domain.Entities.VisitFrequencyPolicy;

namespace Diten.CrmService.Application.Tests.Territory;

public sealed class TerritoryReadinessFu09ATests
{
    private static readonly Guid Tenant = Guid.Parse("97c59330-dbc4-4665-b29c-0c26dbb5cc93");
    private static readonly DateTimeOffset At = new(2026, 8, 3, 10, 0, 0, TimeSpan.Zero); // Monday

    [Fact]
    public async Task Active_Coverage_Returns_ReadinessOk_And_Archived_Model_Does_Not()
    {
        var f = new Fixture();
        var ready = await f.AccountHandler().Handle(new(f.Account.Id, At, "alpha"), default);
        Assert.Equal(TerritoryReadinessReasonCodes.ReadinessOk, Assert.Single(ready.Data!.Items).ReasonCodes.Single());

        f.Model.Status = "archived";
        var blocked = await f.AccountHandler().Handle(new(f.Account.Id, At, "alpha"), default);
        Assert.Contains(TerritoryReadinessReasonCodes.CoverageNotCurrent, Assert.Single(blocked.Data!.Items).ReasonCodes);
    }

    [Fact]
    public async Task Ended_Coverage_Wrong_Bu_Inactive_Account_And_Missing_Location_Report_All_Reasons()
    {
        var f = new Fixture();
        f.Coverage.EffectiveTo = At.AddDays(-1);
        f.Account.Status = "inactive";
        f.Account.AddressLine = f.Account.CityRef = null;
        var result = await f.AccountHandler().Handle(new(f.Account.Id, At, "beta"), default);
        var reasons = Assert.Single(result.Data!.Items).ReasonCodes;
        Assert.Contains(TerritoryReadinessReasonCodes.CoverageNotCurrent, reasons);
        Assert.Contains(TerritoryReadinessReasonCodes.BusinessScopeMismatch, reasons);
        Assert.Contains(TerritoryReadinessReasonCodes.AccountInactive, reasons);
        Assert.Contains(TerritoryReadinessReasonCodes.AccountMissingLocation, reasons);
        Assert.DoesNotContain(TerritoryReadinessReasonCodes.ReadinessOk, reasons);
    }

    [Fact]
    public async Task Contact_Derives_Coverage_Through_Every_Link_And_No_Matching_Weekday_Is_Unknown()
    {
        var f = new Fixture();
        f.Availability.Items.Add(new ContactAvailability
        {
            TenantId = Tenant, AccountContactLinkId = f.Link.Id, ContactId = f.Contact.Id, AccountId = f.Account.Id,
            Weekday = "tuesday", StartTime = "09:00", EndTime = "12:00", Status = "active"
        });

        var result = await f.ContactHandler().Handle(new(f.Contact.Id, At, "alpha", "2026-08-03", "monday"), default);
        var row = Assert.Single(result.Data!.Items);
        Assert.Equal(f.Link.Id, row.AccountContactLinkId);
        Assert.Equal("unknown", row.AvailabilityStatus);
        Assert.Contains(TerritoryReadinessReasonCodes.ContactAvailabilityUnknown, row.ReasonCodes);
        Assert.DoesNotContain(TerritoryReadinessReasonCodes.ContactNotAvailableOnDay, row.ReasonCodes);
    }

    [Fact]
    public async Task Available_Appointment_Is_A_NonBlocking_Warning()
    {
        var f = new Fixture();
        f.Availability.Items.Add(new ContactAvailability
        {
            TenantId = Tenant, AccountContactLinkId = f.Link.Id, ContactId = f.Contact.Id, AccountId = f.Account.Id,
            Weekday = "monday", StartTime = "09:00", EndTime = "12:00", Status = "active",
            Preference = new VisitPreference { AppointmentRequired = true, PreferredVisitStartTime = "09:00", PreferredVisitEndTime = "12:00" }
        });
        var result = await f.ContactHandler().Handle(new(f.Contact.Id, At, "alpha", "2026-08-03", "monday"), default);
        var row = Assert.Single(result.Data!.Items);
        Assert.Equal("available", row.AvailabilityStatus);
        Assert.Equal(TerritoryReadinessStatus.Ready, row.ReadinessStatus);
        Assert.Contains(TerritoryReadinessReasonCodes.AppointmentRequired, row.ReasonCodes);
        Assert.DoesNotContain(TerritoryReadinessReasonCodes.ReadinessOk, row.ReasonCodes);
    }

    [Fact]
    public async Task Explicit_Unavailable_Exception_Is_NotReady()
    {
        var f = new Fixture();
        f.Exceptions.Items.Add(new ContactAvailabilityException
        {
            TenantId = Tenant, AccountContactLinkId = f.Link.Id, ContactId = f.Contact.Id, AccountId = f.Account.Id,
            Date = "2026-08-03", IsAvailable = false, Status = "active", Source = "manual"
        });
        var result = await f.ContactHandler().Handle(new(f.Contact.Id, At, "alpha", "2026-08-03", "monday"), default);
        var row = Assert.Single(result.Data!.Items);
        Assert.Equal("unavailable", row.AvailabilityStatus);
        Assert.Equal(TerritoryReadinessStatus.NotReady, row.ReadinessStatus);
        Assert.Contains(TerritoryReadinessReasonCodes.ContactNotAvailableOnDay, row.ReasonCodes);
    }

    [Fact]
    public async Task Proposed_Resource_Assignment_Is_Not_Current_Owner_And_PositionCode_Is_Canonical_Source()
    {
        var f = new Fixture();
        f.Resources.Items[0].Status = "proposed";
        var handler = new GetResourceCoverageReadinessHandler(TenantFactory.Tenant(Tenant), f.Accounts, f.Contacts,
            f.Links, f.CoverageRepo, f.Models, f.Resources, f.Availability, f.Exceptions, f.Resolver());
        var result = await handler.Handle(new("MR-1", At, "alpha", true), default);
        var row = Assert.Single(result.Data!.Items);
        Assert.Null(row.PositionCode);
        Assert.Contains(TerritoryReadinessReasonCodes.ResourceNotCurrentOwner, row.ReasonCodes);
    }

    [Fact]
    public async Task Route_Candidate_Has_Unknown_Frequency_And_Filter_Preserves_Summary()
    {
        var f = new Fixture();
        var result = await f.RouteHandler().Handle(new(At, "alpha", f.Model.Id, null, null, f.Account.Id,
            null, "2026-08-03", "monday", IncludeNonReady: false), default);
        Assert.Equal(1, result.Data!.TotalCount);
        Assert.Equal(1, result.Data.UnknownCount);
        Assert.Equal(0, result.Data.ReturnedCount);
        var all = await f.RouteHandler().Handle(new(At, "alpha", f.Model.Id, null, null, f.Account.Id,
            null, "2026-08-03", "monday", IncludeNonReady: true), default);
        var row = Assert.Single(all.Data!.Items);
        Assert.Equal("unknown", row.FrequencyStatus);
        Assert.Null(row.SelectedFrequencyPolicyId);
        Assert.Null(row.LastVisitDate);
        Assert.Equal("unknown", row.DueStatus);
        Assert.Contains(TerritoryReadinessReasonCodes.FrequencyUnknown, row.ReasonCodes);
    }

    // ---------------- MOD-0151 FU09B — frequency provider integration ----------------

    private Vfp AccountPolicy(Fixture f, string code, int priority, string status = "active", Guid? target = null)
        => new()
        {
            TenantId = Tenant, PolicyCode = code, PolicyName = code, TargetType = FrequencyTargetType.Account,
            TargetId = target ?? f.Account.Id, FrequencyType = FrequencyType.Monthly, RequiredVisitCount = 2,
            PeriodType = FrequencyPeriodType.Month, EffectiveFrom = At.AddDays(-30), Priority = priority,
            Source = FrequencySource.Manual, Status = status
        };

    [Fact]
    public async Task Route_Candidate_Matching_Policy_Is_Resolved_With_Metadata()
    {
        var f = new Fixture();
        f.Freq.Items.Add(AccountPolicy(f, "RC-1", 500));

        var all = await f.RouteHandler().Handle(new(At, "alpha", f.Model.Id, null, null, f.Account.Id,
            null, "2026-08-03", "monday", IncludeNonReady: true), default);
        var row = Assert.Single(all.Data!.Items);

        Assert.Equal("resolved", row.FrequencyStatus);
        Assert.NotNull(row.SelectedFrequencyPolicyId);
        Assert.Equal("RC-1", row.SelectedFrequencyPolicyCode);
        Assert.Equal(2, row.RequiredVisitCount);
        Assert.Equal("monthly", row.FrequencyType);
        Assert.Equal("month", row.PeriodType);
        Assert.Contains(Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve.FrequencyReasonCodes.FrequencyPolicyResolved, row.FrequencyReasonCodes);
        // readiness is no longer forced to unknown by frequency; nothing else blocks → ready.
        Assert.Equal(TerritoryReadinessStatus.Ready, row.ReadinessStatus);
        Assert.DoesNotContain(TerritoryReadinessReasonCodes.FrequencyUnknown, row.ReasonCodes);
        // DueStatus/LastVisitDate are NOT computed here.
        Assert.Equal("unknown", row.DueStatus);
        Assert.Null(row.LastVisitDate);
    }

    [Fact]
    public async Task Route_Candidate_Priority_Diagnostics_Carry_Losers()
    {
        var f = new Fixture();
        f.Freq.Items.Add(AccountPolicy(f, "HI", 500));
        f.Freq.Items.Add(AccountPolicy(f, "LO", 100));

        var all = await f.RouteHandler().Handle(new(At, "alpha", f.Model.Id, null, null, f.Account.Id,
            null, "2026-08-03", "monday", IncludeNonReady: true), default);
        var row = Assert.Single(all.Data!.Items);

        Assert.Equal("LO", row.SelectedFrequencyPolicyCode);
        Assert.Equal(2, row.FrequencyCandidatePolicies.Count); // winner + eliminated loser both visible
        Assert.Contains(row.FrequencyCandidatePolicies, c => !c.Selected && c.PolicyCode == "HI");
    }

    [Fact]
    public async Task Route_Candidate_Conflict_Is_Surfaced_And_Still_Deterministic()
    {
        var f = new Fixture();
        f.Freq.Items.Add(AccountPolicy(f, "A", 300));
        f.Freq.Items.Add(AccountPolicy(f, "B", 300)); // same priority + target + effective-from → same-band tie

        var all = await f.RouteHandler().Handle(new(At, "alpha", f.Model.Id, null, null, f.Account.Id,
            null, "2026-08-03", "monday", IncludeNonReady: true), default);
        var row = Assert.Single(all.Data!.Items);

        Assert.Equal("conflict", row.FrequencyStatus);
        Assert.NotNull(row.SelectedFrequencyPolicyId); // deterministic pick despite the tie
        Assert.Contains(TerritoryReadinessReasonCodes.FrequencyConflict, row.ReasonCodes);
    }

    [Fact]
    public async Task Route_Candidate_Archived_Policy_Falls_Back_To_Unknown()
    {
        var f = new Fixture();
        f.Freq.Items.Add(AccountPolicy(f, "ARCH", 300, status: "archived"));

        var all = await f.RouteHandler().Handle(new(At, "alpha", f.Model.Id, null, null, f.Account.Id,
            null, "2026-08-03", "monday", IncludeNonReady: true), default);
        var row = Assert.Single(all.Data!.Items);

        Assert.Equal("unknown", row.FrequencyStatus);
        Assert.Null(row.SelectedFrequencyPolicyId);
        Assert.Contains(TerritoryReadinessReasonCodes.FrequencyUnknown, row.ReasonCodes);
    }

    [Fact]
    public async Task Coverage_Readiness_Path_Does_Not_Resolve_Frequency()
    {
        var f = new Fixture();
        f.Freq.Items.Add(AccountPolicy(f, "X", 100));
        // Account coverage readiness passes includeFrequencyBoundary:false — frequency stays not_requested.
        var ready = await f.AccountHandler().Handle(new(f.Account.Id, At, "alpha"), default);
        var row = Assert.Single(ready.Data!.Items);
        Assert.Equal("not_requested", row.FrequencyStatus);
        Assert.Null(row.SelectedFrequencyPolicyId);
        Assert.Empty(row.FrequencyCandidatePolicies);
    }

    [Fact]
    public void Candidate_Contract_Has_No_Consent_Fields()
    {
        var names = typeof(TerritoryRouteCandidateReadModel).GetProperties().Select(p => p.Name.ToLowerInvariant()).ToList();
        foreach (var banned in new[] { "consentallowed", "consentstatus" })
        {
            Assert.DoesNotContain(banned, names);
        }
    }

    [Fact]
    public void Candidate_Contract_Has_No_Planning_Or_Optimization_Fields()
    {
        var forbidden = new[] { "RouteOrder", "SuggestedOrder", "Distance", "TravelTime", "OptimizationScore",
            "DailyPlanId", "VisitPlanId", "RouteId", "Gps", "CheckIn", "Patient" };
        var names = typeof(TerritoryRouteCandidateReadModel).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain(names, n => forbidden.Any(f => n.Contains(f, StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class Fixture
    {
        public Account Account { get; } = new() { TenantId = Tenant, AccountName = "Hospital", AccountCode = "ACC-1", AccountType = "hospital", Status = "active", AddressLine = "Main street" };
        public Contact Contact { get; } = new() { TenantId = Tenant, DisplayName = "Dr Ada", FirstName = "Ada", LastName = "L", ContactType = "doctor", Status = "active" };
        public TerritoryModel Model { get; } = new() { TenantId = Tenant, Name = "2026", ModelCode = "TM-1", Status = "active", EffectiveFrom = At.AddDays(-30) };
        public AccountTerritoryAssignment Coverage { get; }
        public AccountContactLink Link { get; }
        public AccountRepo Accounts { get; } = new();
        public ContactRepo Contacts { get; } = new();
        public LinkRepo Links { get; } = new();
        public FakeTerritoryModelRepo Models { get; } = new();
        public FakeAccountTerritoryAssignmentRepo CoverageRepo { get; } = new();
        public FakeTerritoryResourceAssignmentRepo Resources { get; } = new();
        public AvailabilityRepo Availability { get; } = new();
        public ExceptionRepo Exceptions { get; } = new();
        public FreqRepo Freq { get; } = new();

        // The REAL frequency resolver (repo + MOD-0165 engine) so FU09B tests exercise the true integration path.
        public IVisitFrequencyPolicyResolver Resolver() => new VisitFrequencyPolicyResolver(TenantFactory.Tenant(Tenant), Freq);

        public Fixture()
        {
            Coverage = new AccountTerritoryAssignment { TenantId = Tenant, AccountId = Account.Id, AccountCode = Account.AccountCode,
                AccountDisplayName = Account.AccountName, TerritoryModelId = Model.Id, TerritoryNodeId = Guid.NewGuid(),
                TerritoryNodeCode = "ZONE-1", TerritoryNodeName = "Zone 1", AssignmentStatus = "active", EffectiveFrom = At.AddDays(-10),
                BusinessScopes = [new TerritoryBusinessScope { ScopeType = "business-unit", ScopeCode = "alpha" }] };
            Link = new AccountContactLink { TenantId = Tenant, AccountId = Account.Id, ContactId = Contact.Id, RoleCode = "doctor", Status = "active" };
            Accounts.Items.Add(Account); Contacts.Items.Add(Contact); Links.Items.Add(Link); Models.Items.Add(Model); CoverageRepo.Items.Add(Coverage);
            Resources.Items.Add(new TerritoryResourceAssignment { TenantId = Tenant, ModelId = Model.Id, TerritoryId = Coverage.TerritoryNodeId,
                Resource = new TerritoryResourceRef { ResourceId = "MR-1", DisplayName = "Ayse" },
                Position = new TerritoryPositionRef { PositionCode = "MR", PositionTitle = "Medical Representative" },
                Status = "active", ValidFrom = At.AddDays(-10), IsPrimary = true,
                BusinessScopes = [new TerritoryBusinessScope { ScopeType = "business-unit", ScopeCode = "alpha" }] });
        }

        public GetAccountCoverageReadinessHandler AccountHandler() => new(TenantFactory.Tenant(Tenant), Accounts, Contacts, Links, CoverageRepo, Models, Resources, Availability, Exceptions, Resolver());
        public GetContactTerritoryCoverageHandler ContactHandler() => new(TenantFactory.Tenant(Tenant), Accounts, Contacts, Links, CoverageRepo, Models, Resources, Availability, Exceptions, Resolver());
        public GetRouteCandidatesHandler RouteHandler() => new(TenantFactory.Tenant(Tenant), Accounts, Contacts, Links, CoverageRepo, Models, Resources, Availability, Exceptions, Resolver());
    }

    private sealed class AccountRepo : IAccountRepository
    {
        public List<Account> Items { get; } = [];
        public Task<Account?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<Account?> GetByCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.AccountCode == code));
        public Task<bool> ExistsByCodeAsync(Guid t, string c, Guid? e, CancellationToken ct) => Task.FromResult(false);
        public Task<(IReadOnlyList<Account> Items, long Total, long UnfilteredTotal)> ListAsync(Guid t, string? s, int p, int ps, string? sortBy, string? sortDir, IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes, IReadOnlyCollection<Guid>? accountIdScope, CancellationToken ct) => Task.FromResult(((IReadOnlyList<Account>)Items.Where(x => x.TenantId == t).ToList(), (long)Items.Count, (long)Items.Count));
        public Task<IReadOnlyList<Account>> GetChildrenAsync(Guid t, Guid p, CancellationToken ct) => Task.FromResult<IReadOnlyList<Account>>([]);
        public Task<bool> WouldCreateCycleAsync(Guid t, Guid a, Guid p, CancellationToken ct) => Task.FromResult(false);
        public Task InsertAsync(Account a, CancellationToken ct) => throw new InvalidOperationException("read-only test");
        public Task UpdateAsync(Account a, CancellationToken ct) => throw new InvalidOperationException("read-only test");
    }
    private sealed class ContactRepo : IContactRepository
    {
        public List<Contact> Items { get; } = [];
        public Task<Contact?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
    public Task<IReadOnlyList<Contact>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Contact>>(Array.Empty<Contact>());

        public Task<(IReadOnlyList<Contact> Items, long Total, long UnfilteredTotal)> ListAsync(Guid t, string? s, int p, int ps, string? sortBy, string? sortDir, IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? contactTypes, CancellationToken ct) => Task.FromResult(((IReadOnlyList<Contact>)Items, (long)Items.Count, (long)Items.Count));
        public Task<IReadOnlyList<Contact>> ListAllAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<Contact>)Items);
        public Task InsertAsync(Contact c, CancellationToken ct) => throw new InvalidOperationException("read-only test");
        public Task UpdateAsync(Contact c, CancellationToken ct) => throw new InvalidOperationException("read-only test");
    }
    private sealed class LinkRepo : IAccountContactLinkRepository
    {
        public List<AccountContactLink> Items { get; } = [];
        public Task<AccountContactLink?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task<bool> ExistsActiveAsync(Guid t, Guid a, Guid c, string r, Guid? e, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> ExistsPrimaryAsync(Guid t, Guid a, string r, Guid? e, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyList<AccountContactLink>> ListByAccountAsync(Guid t, Guid a, CancellationToken ct) => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.Where(x => x.TenantId == t && x.AccountId == a).ToList());
        public Task<IReadOnlyList<AccountContactLink>> ListByContactAsync(Guid t, Guid c, CancellationToken ct) => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.Where(x => x.TenantId == t && x.ContactId == c).ToList());
        public Task<IReadOnlyList<AccountContactLink>> ListAllAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.Where(x => x.TenantId == t).ToList());
        public Task InsertAsync(AccountContactLink l, CancellationToken ct) => throw new InvalidOperationException("read-only test");
        public Task UpdateAsync(AccountContactLink l, CancellationToken ct) => throw new InvalidOperationException("read-only test");
    }
    private sealed class AvailabilityRepo : IContactAvailabilityRepository
    {
        public List<ContactAvailability> Items { get; } = [];
        public Task<ContactAvailability?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<ContactAvailability>> ListByLinkAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult((IReadOnlyList<ContactAvailability>)Items.Where(x => x.TenantId == t && x.AccountContactLinkId == id).ToList());
        public Task<IReadOnlyList<ContactAvailability>> ListByContactAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult((IReadOnlyList<ContactAvailability>)Items.Where(x => x.ContactId == id).ToList());
        public Task<IReadOnlyList<ContactAvailability>> ListByAccountAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult((IReadOnlyList<ContactAvailability>)Items.Where(x => x.AccountId == id).ToList());
        public Task InsertAsync(ContactAvailability a, CancellationToken ct) => throw new InvalidOperationException("read-only test");
        public Task UpdateAsync(ContactAvailability a, CancellationToken ct) => throw new InvalidOperationException("read-only test");
    }
    private sealed class ExceptionRepo : IContactAvailabilityExceptionRepository
    {
        public List<ContactAvailabilityException> Items { get; } = [];
        public Task<ContactAvailabilityException?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task<IReadOnlyList<ContactAvailabilityException>> ListByLinkAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult((IReadOnlyList<ContactAvailabilityException>)Items.Where(x => x.TenantId == t && x.AccountContactLinkId == id).ToList());
        public Task<IReadOnlyList<ContactAvailabilityException>> ListByContactAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult((IReadOnlyList<ContactAvailabilityException>)Items.Where(x => x.TenantId == t && x.ContactId == id).ToList());
        public Task<IReadOnlyList<ContactAvailabilityException>> ListByAccountAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult((IReadOnlyList<ContactAvailabilityException>)Items.Where(x => x.TenantId == t && x.AccountId == id).ToList());
        public Task InsertAsync(ContactAvailabilityException e, CancellationToken ct) => throw new InvalidOperationException("read-only test");
        public Task UpdateAsync(ContactAvailabilityException e, CancellationToken ct) => throw new InvalidOperationException("read-only test");
    }
    private sealed class FreqRepo : IVisitFrequencyPolicyRepository
    {
        public List<Vfp> Items { get; } = [];
        public Task<Vfp?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id && !x.IsDeleted));
        public Task<IReadOnlyList<Vfp>> ListAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<Vfp>)Items.Where(x => x.TenantId == t && !x.IsDeleted).ToList());
        public Task<Vfp?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.PolicyCode == code && x.Status != FrequencyPolicyStatus.Archived && !x.IsDeleted));
        public Task<IReadOnlyList<Vfp>> ListActiveByTargetsAsync(Guid t, IReadOnlyCollection<Guid> targetIds, CancellationToken ct) => Task.FromResult((IReadOnlyList<Vfp>)Items.Where(x => x.TenantId == t && !x.IsDeleted && x.Status == FrequencyPolicyStatus.Active && targetIds.Contains(x.TargetId)).ToList());
        public Task InsertAsync(Vfp p, CancellationToken ct) { Items.Add(p); return Task.CompletedTask; }
        public Task UpdateAsync(Vfp p, CancellationToken ct) => Task.CompletedTask;
    }
}
