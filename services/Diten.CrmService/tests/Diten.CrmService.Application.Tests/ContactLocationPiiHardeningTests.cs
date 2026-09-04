using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.AccountContact.Commands;
using Diten.CrmService.Application.Features.AccountContact.Handlers;
using Diten.CrmService.Application.Features.AccountRelationship.Commands;
using Diten.CrmService.Application.Features.AccountRelationship.Handlers;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Application.Features.Contact.Commands;
using Diten.CrmService.Application.Features.Contact.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.Contact.Validators;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using DomainRel = Diten.CrmService.Domain.Entities.AccountRelationship;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// MOD-0150 Contact Location &amp; PII/KVKK Hardening (2026-07-21). Covers the location model, cross-country controlled
/// relationships and the PII-safe audit contract. No new permission / reference set / seed is exercised.
/// </summary>
public sealed class ContactLocationPiiHardeningTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static TenantContext Ctx()
    {
        var c = new TenantContext();
        c.SetTenant(Tenant);
        return c;
    }

    // ---- PiiMasking ----

    [Fact]
    public void PiiMasking_Redacts_Email_And_Phone_But_Keeps_Guid_And_Country()
    {
        var id = Guid.NewGuid();
        var input = $"account={id} crossCountry=TR->US contact=ahmet.yilmaz@example.com phone=05551234567";
        var masked = PiiMasking.Redact(input);

        Assert.DoesNotContain("ahmet.yilmaz@example.com", masked);
        Assert.DoesNotContain("05551234567", masked);
        Assert.Contains("***@***", masked);
        Assert.Contains("crossCountry=TR->US", masked);   // country codes preserved
        Assert.Contains(id.ToString(), masked!);          // GUID preserved
    }

    [Fact]
    public void PiiMasking_Null_Is_Null()
        => Assert.Null(PiiMasking.Redact(null));

    // ---- CrossCountryPolicy ----

    [Fact]
    public void CrossCountry_SameCountry_Is_Not_Cross()
    {
        var r = CrossCountryPolicy.Evaluate("TR", "TR", null);
        Assert.False(r.IsCrossCountry);
        Assert.False(r.ReasonRequiredButMissing);
    }

    [Fact]
    public void CrossCountry_MissingCountry_Is_Not_Cross()
    {
        Assert.False(CrossCountryPolicy.Evaluate(null, "US", null).IsCrossCountry);
        Assert.False(CrossCountryPolicy.Evaluate("TR", "  ", null).IsCrossCountry);
    }

    [Fact]
    public void CrossCountry_Different_Requires_Reason_And_Audit_Is_NonPii()
    {
        var r = CrossCountryPolicy.Evaluate("TR", "US", null);
        Assert.True(r.IsCrossCountry);
        Assert.True(r.ReasonRequiredButMissing);
        Assert.Equal("crossCountry=TR->US", CrossCountryPolicy.AuditNote(r));

        var withReason = CrossCountryPolicy.Evaluate("TR", "US", "regional distributor");
        Assert.True(withReason.IsCrossCountry);
        Assert.False(withReason.ReasonRequiredButMissing);
    }

    // ---- Contact create: location + PII-safe audit ----

    private static CreateContactCommand NewContact(
        string type = "doctor", string status = "active",
        string? country = "TR", string? phoneCc = "+90", string? lang = "tr") =>
        new(FirstName: "Ahmet", LastName: "Yilmaz", DisplayName: null, ContactType: type, Status: status,
            ProfessionalTitle: null, Specialty: null, Department: null, Phone: "5551234567",
            Email: "ahmet@example.com", Notes: null, ExternalReference: null,
            CountryRef: country, CityRef: null, DistrictRef: null, AddressLine: "Main St 1",
            PostalCode: "34000", PreferredLanguage: lang, PhoneCountryCode: phoneCc);

    [Fact]
    public async Task Create_Persists_Location_And_AutoDerives_DisplayName()
    {
        var contacts = new FakeContactRepo();
        var handler = new CreateContactHandler(Ctx(), contacts, new FakeContactRefRepo(), new ConfigurableValidator(), new CapturingAudit());
        var r = await handler.Handle(NewContact(), default);

        Assert.Equal(201, r.StatusCode);
        var stored = contacts.Items.Single();
        Assert.Equal("Ahmet Yilmaz", stored.DisplayName);
        Assert.Equal("TR", stored.CountryRef);
        Assert.Equal("+90", stored.PhoneCountryCode);
        Assert.Equal("34000", stored.PostalCode);
        Assert.False(stored.CountryRef is null);
    }

    [Fact]
    public async Task Create_Invalid_Country_Code_Returns_400()
    {
        var contacts = new FakeContactRepo();
        var validator = new ConfigurableValidator();
        validator.Set("country", ReferenceValidationStatus.InvalidValue);
        var handler = new CreateContactHandler(Ctx(), contacts, new FakeContactRefRepo(), validator, new CapturingAudit());

        var r = await handler.Handle(NewContact(country: "XX"), default);
        Assert.Equal(400, r.StatusCode);
        Assert.Empty(contacts.Items);
    }

    [Fact]
    public async Task Create_Invalid_Specialty_Reference_Returns_400()
    {
        var contacts = new FakeContactRepo();
        var validator = new ConfigurableValidator();
        validator.Set("medical-specialty", ReferenceValidationStatus.InvalidValue);
        var handler = new CreateContactHandler(Ctx(), contacts, new FakeContactRefRepo(), validator, new CapturingAudit());

        var cmd = NewContact() with { Specialty = "not-a-specialty" };
        var r = await handler.Handle(cmd, default);
        Assert.Equal(400, r.StatusCode);
        Assert.Empty(contacts.Items);
    }

    [Fact]
    public async Task Create_Unpublished_Professional_Set_Is_Tolerated()
    {
        var contacts = new FakeContactRepo();
        var validator = new ConfigurableValidator();
        validator.Set("professional-title", ReferenceValidationStatus.SetMissing); // optional set unpublished
        var handler = new CreateContactHandler(Ctx(), contacts, new FakeContactRefRepo(), validator, new CapturingAudit());

        var cmd = NewContact() with { ProfessionalTitle = "dr" };
        var r = await handler.Handle(cmd, default);
        Assert.Equal(201, r.StatusCode); // SetMissing tolerated for optional professional field
        Assert.Equal("dr", contacts.Items.Single().ProfessionalTitle);
    }

    [Fact]
    public async Task Create_Audit_Detail_Contains_No_Name_Phone_Or_Email()
    {
        var contacts = new FakeContactRepo();
        var audit = new CapturingAudit();
        var handler = new CreateContactHandler(Ctx(), contacts, new FakeContactRefRepo(), new ConfigurableValidator(), audit);
        await handler.Handle(NewContact(), default);

        var createEvent = audit.Events.Single(e => e.Event == ContactAuditEvents.Create);
        Assert.Null(createEvent.Detail); // identity carried by ContactId only
        foreach (var e in audit.Events)
        {
            Assert.DoesNotContain("Ahmet", e.Detail ?? string.Empty);
            Assert.DoesNotContain("Yilmaz", e.Detail ?? string.Empty);
            Assert.DoesNotContain("ahmet@example.com", e.Detail ?? string.Empty);
            Assert.DoesNotContain("5551234567", e.Detail ?? string.Empty);
        }
    }

    // ---- Contact validator: phone country code + preferred language shape ----

    [Fact]
    public void Validator_Rejects_Bad_PhoneCountryCode_And_Language()
    {
        var v = new CreateContactValidator();
        var bad = v.Validate(NewContact(phoneCc: "not-a-code", lang: "türkçe!!"));
        Assert.False(bad.IsValid);

        var good = v.Validate(NewContact(phoneCc: "+90", lang: "en-US"));
        Assert.True(good.IsValid);
    }

    // ---- AccountContactLink cross-country ----

    private static LinkContactToAccountCommand LinkCmd(Guid acc, Guid con, string? reason = null) =>
        new(acc, con, "decision-maker", false, null, null, "note", reason);

    private static (LinkContactToAccountHandler handler, FakeLinkRepo links, CapturingAudit audit) BuildLink(
        Account account, Contact contact)
    {
        var accounts = new FakeAccountRepo(); accounts.Items.Add(account);
        var contacts = new FakeContactRepo(); contacts.Items.Add(contact);
        var links = new FakeLinkRepo();
        var audit = new CapturingAudit();
        var handler = new LinkContactToAccountHandler(Ctx(), accounts, contacts, links, new ConfigurableValidator(), audit);
        return (handler, links, audit);
    }

    [Fact]
    public async Task Link_SameCountry_Allowed()
    {
        var acc = new Account { TenantId = Tenant, AccountName = "H", AccountCode = "A1", AccountType = "hospital", Status = "active", CountryRef = "TR" };
        var con = new Contact { TenantId = Tenant, FirstName = "Dr", LastName = "X", DisplayName = "Dr X", ContactType = "doctor", Status = "active", CountryRef = "TR" };
        var (handler, links, _) = BuildLink(acc, con);

        var r = await handler.Handle(LinkCmd(acc.Id, con.Id), default);
        Assert.Equal(201, r.StatusCode);
        Assert.Single(links.Items);
    }

    [Fact]
    public async Task Link_CrossCountry_Without_Reason_Returns_400()
    {
        var acc = new Account { TenantId = Tenant, AccountName = "H", AccountCode = "A1", AccountType = "hospital", Status = "active", CountryRef = "US" };
        var con = new Contact { TenantId = Tenant, FirstName = "Dr", LastName = "X", DisplayName = "Dr X", ContactType = "doctor", Status = "active", CountryRef = "TR" };
        var (handler, links, _) = BuildLink(acc, con);

        var r = await handler.Handle(LinkCmd(acc.Id, con.Id), default);
        Assert.Equal(400, r.StatusCode);
        Assert.Empty(links.Items);
    }

    [Fact]
    public async Task Link_CrossCountry_With_Reason_Persists_And_Audit_Is_NonPii()
    {
        var acc = new Account { TenantId = Tenant, AccountName = "H", AccountCode = "A1", AccountType = "hospital", Status = "active", CountryRef = "US" };
        var con = new Contact { TenantId = Tenant, FirstName = "Dr", LastName = "X", DisplayName = "Dr X", ContactType = "doctor", Status = "active", CountryRef = "TR" };
        var (handler, links, audit) = BuildLink(acc, con);

        var r = await handler.Handle(LinkCmd(acc.Id, con.Id, reason: "cross-border distributor agreement"), default);
        Assert.Equal(201, r.StatusCode);
        Assert.Equal("cross-border distributor agreement", links.Items.Single().CrossCountryReason);

        var ev = audit.Events.Single(e => e.Event == "account-contact.link");
        Assert.Contains("crossCountry=TR->US", ev.Detail);
        Assert.DoesNotContain("distributor agreement", ev.Detail); // reason text never audited
    }

    [Fact]
    public async Task Link_MissingCountry_Does_Not_Block()
    {
        var acc = new Account { TenantId = Tenant, AccountName = "H", AccountCode = "A1", AccountType = "hospital", Status = "active", CountryRef = null };
        var con = new Contact { TenantId = Tenant, FirstName = "Dr", LastName = "X", DisplayName = "Dr X", ContactType = "doctor", Status = "active", CountryRef = "TR" };
        var (handler, links, _) = BuildLink(acc, con);

        var r = await handler.Handle(LinkCmd(acc.Id, con.Id), default);
        Assert.Equal(201, r.StatusCode);
        Assert.Null(links.Items.Single().CrossCountryReason);
    }

    // ---- AccountRelationship cross-country ----

    private static (CreateAccountRelationshipHandler handler, FakeRelRepo rels) BuildRel(Account a, Account b)
    {
        var accounts = new FakeAccountRepo(); accounts.Items.Add(a); accounts.Items.Add(b);
        var rels = new FakeRelRepo();
        var handler = new CreateAccountRelationshipHandler(Ctx(), accounts, rels, new ConfigurableValidator(), new FakeMetadataReader(), new CapturingAudit());
        return (handler, rels);
    }

    [Fact]
    public async Task Relationship_CrossCountry_Without_Reason_Returns_400()
    {
        var a = new Account { TenantId = Tenant, AccountName = "TR Pharmacy", AccountCode = "A", AccountType = "pharmacy", Status = "active", CountryRef = "TR" };
        var b = new Account { TenantId = Tenant, AccountName = "US Hospital", AccountCode = "B", AccountType = "hospital", Status = "active", CountryRef = "US" };
        var (handler, rels) = BuildRel(a, b);

        var r = await handler.Handle(new CreateAccountRelationshipCommand(a.Id, b.Id, "nearby", "active", null, null, "n"), default);
        Assert.Equal(400, r.StatusCode);
        Assert.Empty(rels.Items);
    }

    [Fact]
    public async Task Relationship_CrossCountry_With_Reason_Allowed_And_Persisted()
    {
        var a = new Account { TenantId = Tenant, AccountName = "TR Pharmacy", AccountCode = "A", AccountType = "pharmacy", Status = "active", CountryRef = "TR" };
        var b = new Account { TenantId = Tenant, AccountName = "US Hospital", AccountCode = "B", AccountType = "hospital", Status = "active", CountryRef = "US" };
        var (handler, rels) = BuildRel(a, b);

        var r = await handler.Handle(new CreateAccountRelationshipCommand(a.Id, b.Id, "served-by", "active", null, null, "n", "global supply agreement"), default);
        Assert.Equal(201, r.StatusCode);
        Assert.Equal("global supply agreement", rels.Items.Single().CrossCountryReason);
    }

    [Fact]
    public async Task Relationship_SameCountry_Allowed_Without_Reason()
    {
        var a = new Account { TenantId = Tenant, AccountName = "TR Pharmacy", AccountCode = "A", AccountType = "pharmacy", Status = "active", CountryRef = "TR" };
        var b = new Account { TenantId = Tenant, AccountName = "TR Hospital", AccountCode = "B", AccountType = "hospital", Status = "active", CountryRef = "TR" };
        var (handler, rels) = BuildRel(a, b);

        var r = await handler.Handle(new CreateAccountRelationshipCommand(a.Id, b.Id, "served-by", "active", null, null, "n"), default);
        Assert.Equal(201, r.StatusCode);
        Assert.Null(rels.Items.Single().CrossCountryReason);
    }

    // ==== fakes ====

    private sealed record AuditEntry(string Event, Guid TenantId, Guid EntityId, string? Detail);

    private sealed class CapturingAudit : IContactAuditPublisher
    {
        public List<AuditEntry> Events { get; } = new();
        public Task PublishAsync(string e, Guid t, Guid c, string? d, CancellationToken ct)
        {
            Events.Add(new AuditEntry(e, t, c, d));
            return Task.CompletedTask;
        }
    }

    /// <summary>Validator returning Valid by default; per-set overrides for negative tests.</summary>
    private sealed class ConfigurableValidator : IReferenceDataValidator
    {
        private readonly Dictionary<string, ReferenceValidationStatus> _map = new();
        public void Set(string setCode, ReferenceValidationStatus s) => _map[setCode] = s;
        public Task<ReferenceValidationResult> ValidateAsync(string setCode, string value, CancellationToken ct)
            => Task.FromResult(new ReferenceValidationResult(_map.TryGetValue(setCode, out var s) ? s : ReferenceValidationStatus.Valid, setCode, value));
    }

    private sealed class FakeContactRepo : IContactRepository
    {
        public List<Contact> Items { get; } = new();
        public Task<Contact?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.Id == id && !c.IsDeleted));
    public Task<IReadOnlyList<Contact>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Contact>>(Array.Empty<Contact>());

        public Task<(IReadOnlyList<Contact> Items, long Total, long UnfilteredTotal)> ListAsync(Guid t, string? s, int p, int ps, string? sortBy, string? sortDir, IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? contactTypes, CancellationToken ct) => Task.FromResult(((IReadOnlyList<Contact>)new List<Contact>(), 0L, 0L));
        public Task<IReadOnlyList<Contact>> ListAllAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<Contact>)Items.ToList());
        public Task InsertAsync(Contact c, CancellationToken ct) { Items.Add(c); return Task.CompletedTask; }
        public Task UpdateAsync(Contact c, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeContactRefRepo : IContactExternalReferenceRepository
    {
        public Task<bool> ExistsBySourceExternalAsync(Guid t, string s, string e, Guid? ex, CancellationToken ct) => Task.FromResult(false);
        public Task<ContactExternalReference?> GetBySourceExternalAsync(Guid t, string s, string e, CancellationToken ct) => Task.FromResult<ContactExternalReference?>(null);
        public Task<IReadOnlyList<ContactExternalReference>> ListByContactAsync(Guid t, Guid c, CancellationToken ct) => Task.FromResult((IReadOnlyList<ContactExternalReference>)new List<ContactExternalReference>());
        public Task<IReadOnlyList<ContactExternalReference>> ListAllAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<ContactExternalReference>)new List<ContactExternalReference>());
        public Task InsertAsync(ContactExternalReference r, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeAccountRepo : IAccountRepository
    {
        public List<Account> Items { get; } = new();
        public Task<Account?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.TenantId == t && a.Id == id && !a.IsDeleted));
        public Task<Account?> GetByCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult<Account?>(null);
        public Task<bool> ExistsByCodeAsync(Guid t, string c, Guid? ex, CancellationToken ct) => Task.FromResult(false);
        public Task<(IReadOnlyList<Account> Items, long Total, long UnfilteredTotal)> ListAsync(Guid t, string? s, int p, int ps, string? sortBy, string? sortDir, IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes, IReadOnlyCollection<Guid>? accountIdScope, CancellationToken ct) => Task.FromResult(((IReadOnlyList<Account>)new List<Account>(), 0L, 0L));
        public Task<IReadOnlyList<Account>> GetChildrenAsync(Guid t, Guid p, CancellationToken ct) => Task.FromResult((IReadOnlyList<Account>)new List<Account>());
        public Task<bool> WouldCreateCycleAsync(Guid t, Guid a, Guid c, CancellationToken ct) => Task.FromResult(false);
        public Task InsertAsync(Account a, CancellationToken ct) { Items.Add(a); return Task.CompletedTask; }
        public Task UpdateAsync(Account a, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeLinkRepo : IAccountContactLinkRepository
    {
        public List<AccountContactLink> Items { get; } = new();
        public Task<AccountContactLink?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(l => l.TenantId == t && l.Id == id && !l.IsDeleted));
        public Task<bool> ExistsActiveAsync(Guid t, Guid a, Guid c, string r, Guid? ex, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> ExistsPrimaryAsync(Guid t, Guid a, string r, Guid? ex, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyList<AccountContactLink>> ListByAccountAsync(Guid t, Guid a, CancellationToken ct) => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.ToList());
        public Task<IReadOnlyList<AccountContactLink>> ListByContactAsync(Guid t, Guid c, CancellationToken ct) => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.ToList());
        public Task<IReadOnlyList<AccountContactLink>> ListAllAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.ToList());
        public Task InsertAsync(AccountContactLink l, CancellationToken ct) { Items.Add(l); return Task.CompletedTask; }
        public Task UpdateAsync(AccountContactLink l, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeRelRepo : IAccountRelationshipRepository
    {
        public List<DomainRel> Items { get; } = new();
        public Task<DomainRel?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(r => r.TenantId == t && r.Id == id && !r.IsDeleted));
        public Task<IReadOnlyList<DomainRel>> ListAllAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<DomainRel>)Items.ToList());
        public Task<bool> ExistsActivePairAsync(Guid t, Guid s, Guid tg, string type, bool includeReverse, Guid? ex, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyList<DomainRel>> ListByAccountAsync(Guid t, Guid a, CancellationToken ct) => Task.FromResult((IReadOnlyList<DomainRel>)Items.ToList());
        public Task InsertAsync(DomainRel r, CancellationToken ct) { Items.Add(r); return Task.CompletedTask; }
        public Task UpdateAsync(DomainRel r, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeMetadataReader : IReferenceMetadataReader
    {
        private readonly Dictionary<string, Dictionary<string, string>> _map = new(StringComparer.OrdinalIgnoreCase)
        {
            ["served-by"] = new() { ["direction"] = "directional", ["inverseLabelCode"] = "serves", ["selfAllowed"] = "false" },
            ["nearby"] = new() { ["direction"] = "bidirectional", ["inverseLabelCode"] = "nearby", ["selfAllowed"] = "false" },
        };
        public Task<IReadOnlyDictionary<string, string>?> GetValueAttributesAsync(string setCode, string value, CancellationToken ct)
            => Task.FromResult(_map.TryGetValue(value, out var m) ? (IReadOnlyDictionary<string, string>?)m : null);
    }
}
