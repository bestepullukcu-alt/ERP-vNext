using System.Reflection;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.ConsentPreference;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Application.Features.Contact.Commands;
using Diten.CrmService.Application.Features.Contact.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.Contact.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.Contact.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Diten.CrmService.Infrastructure.ConsentPreference;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.CrmService.Application.Tests;

public sealed class ContactFoundationTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private static CreateContactCommand NewCreate(
        string first = "Ahmet", string last = "Yilmaz", string? display = null,
        string type = "doctor", string status = "active", ContactExternalReferenceInput? ext = null) => new(
        FirstName: first, LastName: last, DisplayName: display, ContactType: type, Status: status,
        ProfessionalTitle: null, Specialty: null, Department: null, Phone: null, Email: null, Notes: null,
        ExternalReference: ext);

    private static CreateContactHandler CreateHandler(
        FakeContactRepo contacts, FakeContactRefRepo? refs = null, IReferenceDataValidator? validator = null)
        => new(Tenant(TenantA), contacts, refs ?? new FakeContactRefRepo(),
            validator ?? Valid(), new NoopAudit());

    private static IReferenceDataValidator Valid() => new FakeReferenceValidator();

    [Fact]
    public async Task Create_Success_AutoDerives_DisplayName()
    {
        var contacts = new FakeContactRepo();
        var response = await CreateHandler(contacts).Handle(NewCreate(display: null), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        var stored = contacts.Items.Single();
        Assert.Equal("Ahmet Yilmaz", stored.DisplayName);
        Assert.Equal(TenantA, stored.TenantId);
    }

    [Fact]
    public async Task Create_Respects_Supplied_DisplayName()
    {
        var contacts = new FakeContactRepo();
        await CreateHandler(contacts).Handle(NewCreate(display: "Dr. Ahmet Y."), default);
        Assert.Equal("Dr. Ahmet Y.", contacts.Items.Single().DisplayName);
    }

    [Fact]
    public async Task Create_Invalid_ContactType_Returns_400()
    {
        var contacts = new FakeContactRepo();
        var validator = new FakeReferenceValidator { ["contact-type"] = ReferenceValidationStatus.InvalidValue };
        var response = await CreateHandler(contacts, validator: validator).Handle(NewCreate(type: "not-real"), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Empty(contacts.Items);
    }

    [Fact]
    public async Task Create_Invalid_ContactStatus_Returns_400()
    {
        var contacts = new FakeContactRepo();
        var validator = new FakeReferenceValidator { ["contact-status"] = ReferenceValidationStatus.InvalidValue };
        var response = await CreateHandler(contacts, validator: validator).Handle(NewCreate(status: "not-real"), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Create_Deprecated_ReferenceValue_Returns_400()
    {
        // A deprecated value is reported by the validator as InvalidValue (not selectable for new records).
        var contacts = new FakeContactRepo();
        var validator = new FakeReferenceValidator { ["contact-type"] = ReferenceValidationStatus.InvalidValue };
        var response = await CreateHandler(contacts, validator: validator).Handle(NewCreate(), default);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Create_With_Unpublished_Required_Set_Returns_400()
    {
        var contacts = new FakeContactRepo();
        var validator = new FakeReferenceValidator { Default = ReferenceValidationStatus.SetMissing };
        var response = await CreateHandler(contacts, validator: validator).Handle(NewCreate(), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Empty(contacts.Items);
    }

    [Fact]
    public async Task Create_Duplicate_ExternalReference_Returns_409()
    {
        var contacts = new FakeContactRepo();
        var refs = new FakeContactRefRepo { Exists = true };
        var command = NewCreate(ext: new ContactExternalReferenceInput("EXT-1", "OldCRM", "Doctor", null));
        var response = await CreateHandler(contacts, refs).Handle(command, default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Empty(contacts.Items);
    }

    [Fact]
    public async Task Update_Success()
    {
        var contacts = new FakeContactRepo();
        var c = new Contact { TenantId = TenantA, FirstName = "A", LastName = "B", DisplayName = "A B", ContactType = "doctor", Status = "active" };
        contacts.Items.Add(c);

        var handler = new UpdateContactHandler(Tenant(TenantA), contacts, Valid(), new NoopAudit());
        var response = await handler.Handle(new UpdateContactCommand(c.Id, "Ali", "Veli", null, "pharmacist", "inactive", null, null, null, null, null, null), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal("Ali Veli", c.DisplayName);
        Assert.Equal("pharmacist", c.ContactType);
    }

    [Fact]
    public async Task Delete_SoftDeletes_And_Reload_Returns_404()
    {
        var contacts = new FakeContactRepo();
        var c = new Contact { TenantId = TenantA, FirstName = "Del", LastName = "Me", DisplayName = "Del Me", ContactType = "doctor", Status = "active" };
        contacts.Items.Add(c);

        var del = await new DeleteContactHandler(Tenant(TenantA), contacts, new NoopAudit()).Handle(new DeleteContactCommand(c.Id), default);
        Assert.True(del.IsSuccessful);
        Assert.True(c.IsDeleted);
        Assert.NotNull(c.DeletedAt);

        var read = await new GetContactByIdHandler(Tenant(TenantA), contacts, new FakeContactRefRepo()).Handle(new GetContactByIdQuery(c.Id), default);
        Assert.False(read.IsSuccessful);
        Assert.Equal(404, read.StatusCode);
    }

    [Fact]
    public async Task GetById_CrossTenant_Returns_404()
    {
        var contacts = new FakeContactRepo();
        var owned = new Contact { TenantId = TenantB, FirstName = "Other", LastName = "Tenant", DisplayName = "Other Tenant", ContactType = "doctor", Status = "active" };
        contacts.Items.Add(owned);

        var response = await new GetContactByIdHandler(Tenant(TenantA), contacts, new FakeContactRefRepo()).Handle(new GetContactByIdQuery(owned.Id), default);
        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task List_Excludes_SoftDeleted()
    {
        var contacts = new FakeContactRepo();
        contacts.Items.Add(new Contact { TenantId = TenantA, DisplayName = "Visible", FirstName = "V", LastName = "1", ContactType = "doctor", Status = "active" });
        contacts.Items.Add(new Contact { TenantId = TenantA, DisplayName = "Hidden", FirstName = "H", LastName = "2", ContactType = "doctor", Status = "active", IsDeleted = true });

        var response = await new ListContactsHandler(
                Tenant(TenantA), contacts, new FakeLinkRepo(),
                new Territory.FakeAccountTerritoryAssignmentRepo(), new Territory.FakeTerritoryModelRepo())
            .Handle(new ListContactsQuery(null, 1, 25), default);
        Assert.True(response.IsSuccessful);
        Assert.Single(response.Data!.Items);
        Assert.Equal("Visible", response.Data!.Items[0].DisplayName);
    }

    private static GetContactOverviewHandler OverviewHandler(
        Guid tenant, FakeContactRepo contacts, IContactConsentPreferenceReader? reader = null)
        => new(Tenant(tenant), contacts, new FakeContactRefRepo(), new FakeLinkRepo(), new FakeAccountRepo(),
            reader ?? new NullContactConsentPreferenceReader(),
            new Territory.FakeAccountTerritoryAssignmentRepo(), new Territory.FakeTerritoryModelRepo(),
            NullLogger<GetContactOverviewHandler>.Instance);

    [Fact]
    public async Task Overview_Has_Empty_LinkedAccounts_In_FU01()
    {
        var contacts = new FakeContactRepo();
        var c = new Contact { TenantId = TenantA, DisplayName = "X Y", FirstName = "X", LastName = "Y", ContactType = "doctor", Status = "active" };
        contacts.Items.Add(c);

        var response = await OverviewHandler(TenantA, contacts).Handle(new GetContactOverviewQuery(c.Id), default);
        Assert.True(response.IsSuccessful);
        Assert.Empty(response.Data!.LinkedAccounts);
    }

    // ---- FU05 consent/preference seam ----

    [Fact]
    public async Task Overview_Returns_NoOp_Consent_Summary_When_Mod0164_Unavailable()
    {
        var contacts = new FakeContactRepo();
        var c = new Contact { TenantId = TenantA, DisplayName = "X Y", ContactType = "doctor", Status = "active" };
        contacts.Items.Add(c);

        // Caller HAS consent permission → reader is consulted; NullReader yields the no-op not-available summary.
        var response = await OverviewHandler(TenantA, contacts)
            .Handle(new GetContactOverviewQuery(c.Id, CanReadConsent: true, CanReadPreference: true), default);

        var summary = response.Data!.ConsentPreferenceSummary;
        Assert.False(summary.ConsentAvailable);
        Assert.False(summary.PreferenceAvailable);
        Assert.Equal("not-available", summary.ConsentStatus);
        Assert.Equal("MOD-0164", summary.Source);
        Assert.Empty(summary.Channels);
    }

    [Fact]
    public async Task Overview_Masks_Consent_Summary_When_Caller_Lacks_Permission()
    {
        var contacts = new FakeContactRepo();
        var c = new Contact { TenantId = TenantA, DisplayName = "X Y", ContactType = "doctor", Status = "active" };
        contacts.Items.Add(c);
        var reader = new ThrowingConsentReader(); // must NOT be called when unauthorized

        var response = await OverviewHandler(TenantA, contacts, reader)
            .Handle(new GetContactOverviewQuery(c.Id, CanReadConsent: false, CanReadPreference: false), default);

        var summary = response.Data!.ConsentPreferenceSummary;
        Assert.Equal("not-authorized", summary.ConsentStatus);
        Assert.False(summary.ConsentAvailable);
        Assert.Empty(summary.Channels);
        Assert.False(reader.WasCalled); // no reader call → no data leak path
    }

    [Fact]
    public async Task Overview_Is_Failsoft_When_Consent_Reader_Throws()
    {
        var contacts = new FakeContactRepo();
        var c = new Contact { TenantId = TenantA, DisplayName = "X Y", ContactType = "doctor", Status = "active" };
        contacts.Items.Add(c);

        var response = await OverviewHandler(TenantA, contacts, new ThrowingConsentReader())
            .Handle(new GetContactOverviewQuery(c.Id, CanReadConsent: true), default);

        Assert.True(response.IsSuccessful); // 360 stays up despite the seam error
        Assert.Equal("not-available", response.Data!.ConsentPreferenceSummary.ConsentStatus);
    }

    [Fact]
    public void NullConsentReader_Fabricates_No_Consent_State()
    {
        var summary = ContactConsentPreferenceSummaryDto.NotAvailable(Guid.NewGuid());
        Assert.False(summary.ConsentAvailable);
        Assert.False(summary.PreferenceAvailable);
        Assert.DoesNotContain(summary.ConsentStatus, new[] { "granted", "denied" });
        Assert.DoesNotContain(summary.PreferenceStatus, new[] { "granted", "denied" });
        Assert.Empty(summary.Channels);
    }

    [Fact]
    public void Contact_Create_Command_Has_No_Consent_Capture_Fields()
    {
        var props = typeof(CreateContactCommand).GetProperties().Select(p => p.Name).ToList();
        foreach (var forbidden in new[] { "Consent", "ConsentState", "Preference", "PreferenceState", "Channels" })
        {
            Assert.DoesNotContain(forbidden, props);
        }
    }

    [Fact]
    public void Contact_Entity_Has_No_Account_Or_Zone_Fields()
    {
        var props = typeof(Contact).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToList();
        foreach (var forbidden in new[] { "AccountId", "ZoneId", "MicroZoneId", "TerritoryId", "SalesRepId", "RoleCode", "IsPrimary" })
        {
            Assert.DoesNotContain(forbidden, props);
        }
    }

    // ---- in-memory fakes ----

    private sealed class FakeContactRepo : IContactRepository
    {
        public List<Contact> Items { get; } = new();

        public Task<Contact?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id && !c.IsDeleted));

    public Task<IReadOnlyList<Contact>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Contact>>(Array.Empty<Contact>());

        public Task<(IReadOnlyList<Contact> Items, long Total, long UnfilteredTotal)> ListAsync(
            Guid tenantId, string? search, int page, int pageSize, string? sortBy, string? sortDir,
            IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? contactTypes, CancellationToken ct)
        {
            var tenant = Items.Where(c => c.TenantId == tenantId && !c.IsDeleted).ToList();
            IEnumerable<Contact> q = tenant;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                q = q.Where(c =>
                    (c.DisplayName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (c.FirstName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (c.LastName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (c.Email?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
            }
            if (statuses is { Count: > 0 }) q = q.Where(c => statuses.Contains(c.Status));
            if (contactTypes is { Count: > 0 }) q = q.Where(c => contactTypes.Contains(c.ContactType));

            var descending = string.Equals(sortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase);
            q = (sortBy?.Trim().ToLowerInvariant()) switch
            {
                "contacttype" => descending ? q.OrderByDescending(c => c.ContactType) : q.OrderBy(c => c.ContactType),
                _ => descending ? q.OrderByDescending(c => c.DisplayName) : q.OrderBy(c => c.DisplayName)
            };

            var filtered = q.ToList();
            var page1 = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(((IReadOnlyList<Contact>)page1, (long)filtered.Count, (long)tenant.Count));
        }

        public Task<IReadOnlyList<Contact>> ListAllAsync(Guid tenantId, CancellationToken ct) => Task.FromResult((IReadOnlyList<Contact>)Items.Where(c => c.TenantId == tenantId && !c.IsDeleted).ToList());
        public Task InsertAsync(Contact contact, CancellationToken ct) { Items.Add(contact); return Task.CompletedTask; }
        public Task UpdateAsync(Contact contact, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeContactRefRepo : IContactExternalReferenceRepository
    {
        public bool Exists { get; set; }
        public Task<bool> ExistsBySourceExternalAsync(Guid tenantId, string sourceSystem, string externalId, Guid? excludeId, CancellationToken ct) => Task.FromResult(Exists);
        public Task<ContactExternalReference?> GetBySourceExternalAsync(Guid tenantId, string sourceSystem, string externalId, CancellationToken ct) => Task.FromResult<ContactExternalReference?>(null);
        public Task<IReadOnlyList<ContactExternalReference>> ListByContactAsync(Guid tenantId, Guid contactId, CancellationToken ct) => Task.FromResult((IReadOnlyList<ContactExternalReference>)new List<ContactExternalReference>());
        public Task<IReadOnlyList<ContactExternalReference>> ListAllAsync(Guid tenantId, CancellationToken ct) => Task.FromResult((IReadOnlyList<ContactExternalReference>)new List<ContactExternalReference>());
        public Task InsertAsync(ContactExternalReference reference, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeReferenceValidator : IReferenceDataValidator
    {
        private readonly Dictionary<string, ReferenceValidationStatus> _map = new();
        public ReferenceValidationStatus Default { get; set; } = ReferenceValidationStatus.Valid;
        public ReferenceValidationStatus this[string setCode] { set => _map[setCode] = value; }

        public Task<ReferenceValidationResult> ValidateAsync(string setCode, string value, CancellationToken ct)
            => Task.FromResult(new ReferenceValidationResult(_map.TryGetValue(setCode, out var s) ? s : Default, setCode, value));
    }

    private sealed class NoopAudit : IContactAuditPublisher
    {
        public Task PublishAsync(string eventName, Guid tenantId, Guid contactId, string? detail, CancellationToken ct) => Task.CompletedTask;
    }

    /// <summary>Simulates an erroring MOD-0164 seam; records whether it was invoked (masking must skip it entirely).</summary>
    private sealed class ThrowingConsentReader : IContactConsentPreferenceReader
    {
        public bool WasCalled { get; private set; }
        public Task<ContactConsentPreferenceSummaryDto> GetSummaryAsync(Guid tenantId, Guid contactId, CancellationToken ct)
        {
            WasCalled = true;
            throw new InvalidOperationException("MOD-0164 unavailable");
        }
    }

    private sealed class FakeLinkRepo : IAccountContactLinkRepository
    {
        public Task<AccountContactLink?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult<AccountContactLink?>(null);
        public Task<bool> ExistsActiveAsync(Guid t, Guid a, Guid c, string r, Guid? ex, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> ExistsPrimaryAsync(Guid t, Guid a, string r, Guid? ex, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyList<AccountContactLink>> ListByAccountAsync(Guid t, Guid a, CancellationToken ct) => Task.FromResult((IReadOnlyList<AccountContactLink>)new List<AccountContactLink>());
        public Task<IReadOnlyList<AccountContactLink>> ListByContactAsync(Guid t, Guid c, CancellationToken ct) => Task.FromResult((IReadOnlyList<AccountContactLink>)new List<AccountContactLink>());
        public Task<IReadOnlyList<AccountContactLink>> ListAllAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<AccountContactLink>)new List<AccountContactLink>());
        public Task InsertAsync(AccountContactLink l, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(AccountContactLink l, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeAccountRepo : IAccountRepository
    {
        public Task<Account?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult<Account?>(null);
        public Task<Account?> GetByCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult<Account?>(null);
        public Task<bool> ExistsByCodeAsync(Guid t, string c, Guid? ex, CancellationToken ct) => Task.FromResult(false);
        public Task<(IReadOnlyList<Account> Items, long Total, long UnfilteredTotal)> ListAsync(Guid t, string? s, int p, int ps, string? sortBy, string? sortDir, IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes, IReadOnlyCollection<Guid>? accountIdScope, CancellationToken ct) => Task.FromResult(((IReadOnlyList<Account>)new List<Account>(), 0L, 0L));
        public Task<IReadOnlyList<Account>> GetChildrenAsync(Guid t, Guid p, CancellationToken ct) => Task.FromResult((IReadOnlyList<Account>)new List<Account>());
        public Task<bool> WouldCreateCycleAsync(Guid t, Guid a, Guid c, CancellationToken ct) => Task.FromResult(false);
        public Task InsertAsync(Account a, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(Account a, CancellationToken ct) => Task.CompletedTask;
    }
}
