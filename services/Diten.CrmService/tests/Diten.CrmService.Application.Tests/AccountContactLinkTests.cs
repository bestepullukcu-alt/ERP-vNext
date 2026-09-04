using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.AccountContact.Commands;
using Diten.CrmService.Application.Features.AccountContact.Handlers;
using Diten.CrmService.Application.Features.AccountContact.Queries;
using Diten.CrmService.Application.Features.Contact;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;

namespace Diten.CrmService.Application.Tests;

public sealed class AccountContactLinkTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    private sealed class Fixture
    {
        public FakeAccountRepo Accounts { get; } = new();
        public FakeContactRepo Contacts { get; } = new();
        public FakeLinkRepo Links { get; } = new();
        public ReferenceValidationStatus RoleStatus { get; set; } = ReferenceValidationStatus.Valid;
        public Account Account { get; }
        public Contact Contact { get; }

        public Fixture(Guid tenant)
        {
            Account = new Account { TenantId = tenant, AccountName = "Hospital", AccountCode = "ACC-1", AccountType = "hospital", Status = "active" };
            Contact = new Contact { TenantId = tenant, FirstName = "Dr", LastName = "X", DisplayName = "Dr X", ContactType = "doctor", Status = "active" };
            Accounts.Items.Add(Account);
            Contacts.Items.Add(Contact);
        }

        public LinkContactToAccountHandler Link(Guid tenant) =>
            new(Tenant(tenant), Accounts, Contacts, Links, new FakeValidator(RoleStatus), new NoopAudit());
    }

    private static LinkContactToAccountCommand Cmd(Guid accountId, Guid contactId, string role = "decision-maker", bool primary = false) =>
        new(accountId, contactId, role, primary, null, null, "note");

    [Fact]
    public async Task Link_Success_Returns_201()
    {
        var f = new Fixture(TenantA);
        var r = await f.Link(TenantA).Handle(Cmd(f.Account.Id, f.Contact.Id), default);
        Assert.True(r.IsSuccessful);
        Assert.Equal(201, r.StatusCode);
        Assert.Single(f.Links.Items);
    }

    [Fact]
    public async Task Link_Missing_Account_Returns_404()
    {
        var f = new Fixture(TenantA);
        var r = await f.Link(TenantA).Handle(Cmd(Guid.NewGuid(), f.Contact.Id), default);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Link_Missing_Contact_Returns_404()
    {
        var f = new Fixture(TenantA);
        var r = await f.Link(TenantA).Handle(Cmd(f.Account.Id, Guid.NewGuid()), default);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Link_SoftDeleted_Account_Returns_404()
    {
        var f = new Fixture(TenantA);
        f.Account.IsDeleted = true;
        var r = await f.Link(TenantA).Handle(Cmd(f.Account.Id, f.Contact.Id), default);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Link_SoftDeleted_Contact_Returns_404()
    {
        var f = new Fixture(TenantA);
        f.Contact.IsDeleted = true;
        var r = await f.Link(TenantA).Handle(Cmd(f.Account.Id, f.Contact.Id), default);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task Link_Invalid_Role_Returns_400()
    {
        var f = new Fixture(TenantA) { RoleStatus = ReferenceValidationStatus.InvalidValue };
        var r = await f.Link(TenantA).Handle(Cmd(f.Account.Id, f.Contact.Id, role: "not-real"), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Link_Missing_RoleSet_Returns_400()
    {
        var f = new Fixture(TenantA) { RoleStatus = ReferenceValidationStatus.SetMissing };
        var r = await f.Link(TenantA).Handle(Cmd(f.Account.Id, f.Contact.Id), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Link_Duplicate_Active_Returns_409()
    {
        var f = new Fixture(TenantA);
        await f.Link(TenantA).Handle(Cmd(f.Account.Id, f.Contact.Id), default);
        var r = await f.Link(TenantA).Handle(Cmd(f.Account.Id, f.Contact.Id), default);
        Assert.Equal(409, r.StatusCode);
        Assert.Single(f.Links.Items);
    }

    [Fact]
    public async Task Link_Second_Primary_Same_Account_Role_Returns_409()
    {
        var f = new Fixture(TenantA);
        var contact2 = new Contact { TenantId = TenantA, FirstName = "Dr", LastName = "Y", DisplayName = "Dr Y", ContactType = "doctor", Status = "active" };
        f.Contacts.Items.Add(contact2);
        await f.Link(TenantA).Handle(Cmd(f.Account.Id, f.Contact.Id, primary: true), default);
        var r = await f.Link(TenantA).Handle(Cmd(f.Account.Id, contact2.Id, primary: true), default);
        Assert.Equal(409, r.StatusCode);
    }

    [Fact]
    public async Task List_By_Account_Returns_Related_Contacts()
    {
        var f = new Fixture(TenantA);
        await f.Link(TenantA).Handle(Cmd(f.Account.Id, f.Contact.Id, primary: true), default);
        var h = new ListContactsForAccountHandler(Tenant(TenantA), f.Accounts, f.Contacts, f.Links);
        var r = await h.Handle(new ListContactsForAccountQuery(f.Account.Id), default);
        Assert.True(r.IsSuccessful);
        Assert.Single(r.Data!);
        Assert.Equal("Dr X", r.Data![0].DisplayName);
        Assert.True(r.Data![0].IsPrimary);
    }

    [Fact]
    public async Task List_By_Contact_Returns_Linked_Accounts()
    {
        var f = new Fixture(TenantA);
        await f.Link(TenantA).Handle(Cmd(f.Account.Id, f.Contact.Id), default);
        var h = new ListAccountsForContactHandler(Tenant(TenantA), f.Accounts, f.Contacts, f.Links);
        var r = await h.Handle(new ListAccountsForContactQuery(f.Contact.Id), default);
        Assert.True(r.IsSuccessful);
        Assert.Single(r.Data!);
        Assert.Equal("ACC-1", r.Data![0].AccountCode);
    }

    [Fact]
    public async Task Delete_SoftDeletes_And_List_Excludes()
    {
        var f = new Fixture(TenantA);
        await f.Link(TenantA).Handle(Cmd(f.Account.Id, f.Contact.Id), default);
        var linkId = f.Links.Items[0].Id;
        var del = new DeleteAccountContactLinkHandler(Tenant(TenantA), f.Links, new NoopAudit());
        var dr = await del.Handle(new DeleteAccountContactLinkCommand(f.Account.Id, linkId), default);
        Assert.True(dr.IsSuccessful);
        Assert.True(f.Links.Items[0].IsDeleted);

        var h = new ListContactsForAccountHandler(Tenant(TenantA), f.Accounts, f.Contacts, f.Links);
        var r = await h.Handle(new ListContactsForAccountQuery(f.Account.Id), default);
        Assert.Empty(r.Data!);
    }

    [Fact]
    public async Task Get_CrossTenant_Link_Returns_404()
    {
        var f = new Fixture(TenantA);
        await f.Link(TenantA).Handle(Cmd(f.Account.Id, f.Contact.Id), default);
        var linkId = f.Links.Items[0].Id;
        var h = new GetAccountContactLinkByIdHandler(Tenant(TenantB), f.Links);
        var r = await h.Handle(new GetAccountContactLinkByIdQuery(f.Account.Id, linkId), default);
        Assert.Equal(404, r.StatusCode);
    }

    [Fact]
    public async Task End_Link_Then_Relink_Same_Key_Allowed_History_Preserved()
    {
        var f = new Fixture(TenantA);
        await f.Link(TenantA).Handle(Cmd(f.Account.Id, f.Contact.Id, role: "doctor"), default);
        var linkId = f.Links.Items[0].Id;

        // End the link (historical lifecycle): Status=ended + ValidTo, NOT deleted.
        var upd = new UpdateAccountContactLinkHandler(Tenant(TenantA), f.Links, new FakeValidator(ReferenceValidationStatus.Valid), new NoopAudit());
        var er = await upd.Handle(new UpdateAccountContactLinkCommand(f.Account.Id, linkId, "doctor", false, null, DateTimeOffset.UtcNow, "moved hospital", "ended"), default);
        Assert.True(er.IsSuccessful);
        Assert.Equal("ended", f.Links.Items[0].Status);
        Assert.False(f.Links.Items[0].IsDeleted); // history preserved, not destroyed

        // Re-link the SAME contact+account+role → allowed because the old one is ended (not active).
        var rr = await f.Link(TenantA).Handle(Cmd(f.Account.Id, f.Contact.Id, role: "doctor"), default);
        Assert.Equal(201, rr.StatusCode);
        Assert.Equal(2, f.Links.Items.Count); // both rows kept
        Assert.Equal("active", f.Links.Items[1].Status);
    }

    [Fact]
    public async Task Link_ValidFrom_After_ValidTo_Returns_400()
    {
        var f = new Fixture(TenantA);
        var cmd = new LinkContactToAccountCommand(f.Account.Id, f.Contact.Id, "doctor", false, DateTimeOffset.UtcNow.AddDays(10), DateTimeOffset.UtcNow, "n");
        var r = await f.Link(TenantA).Handle(cmd, default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task Ended_Primary_Does_Not_Block_New_Primary_Same_Role()
    {
        var f = new Fixture(TenantA);
        var contact2 = new Contact { TenantId = TenantA, FirstName = "Dr", LastName = "Y", DisplayName = "Dr Y", ContactType = "doctor", Status = "active" };
        f.Contacts.Items.Add(contact2);
        await f.Link(TenantA).Handle(Cmd(f.Account.Id, f.Contact.Id, role: "doctor", primary: true), default);
        var linkId = f.Links.Items[0].Id;
        var upd = new UpdateAccountContactLinkHandler(Tenant(TenantA), f.Links, new FakeValidator(ReferenceValidationStatus.Valid), new NoopAudit());
        await upd.Handle(new UpdateAccountContactLinkCommand(f.Account.Id, linkId, "doctor", true, null, DateTimeOffset.UtcNow, null, "ended"), default);

        // New primary for the same account+role now allowed (the old primary is ended).
        var rr = await f.Link(TenantA).Handle(Cmd(f.Account.Id, contact2.Id, role: "doctor", primary: true), default);
        Assert.Equal(201, rr.StatusCode);
    }

    // ---- MOD-0150 in-account hierarchy (ReportsTo) ----

    private static LinkContactToAccountCommand LinkWithReportsTo(Guid accountId, Guid contactId, Guid? reportsTo, string role = "doctor") =>
        new(accountId, contactId, role, false, null, null, "n", null, reportsTo);

    [Fact]
    public async Task ReportsTo_Valid_Manager_LinkedToSameAccount_Succeeds()
    {
        var f = new Fixture(TenantA);
        var manager = new Contact { TenantId = TenantA, FirstName = "Mgr", LastName = "One", DisplayName = "Mgr One", ContactType = "doctor", Status = "active" };
        f.Contacts.Items.Add(manager);
        // Manager must already be linked to the account.
        await f.Link(TenantA).Handle(Cmd(f.Account.Id, manager.Id, role: "decision-maker"), default);

        var r = await f.Link(TenantA).Handle(LinkWithReportsTo(f.Account.Id, f.Contact.Id, manager.Id), default);
        Assert.Equal(201, r.StatusCode);
        Assert.Equal(manager.Id, f.Links.Items.Single(l => l.ContactId == f.Contact.Id).ReportsToContactId);
    }

    [Fact]
    public async Task ReportsTo_Self_Returns_400()
    {
        var f = new Fixture(TenantA);
        var r = await f.Link(TenantA).Handle(LinkWithReportsTo(f.Account.Id, f.Contact.Id, f.Contact.Id), default);
        Assert.Equal(400, r.StatusCode);
        Assert.Empty(f.Links.Items);
    }

    [Fact]
    public async Task ReportsTo_Manager_NotLinkedToAccount_Returns_400()
    {
        var f = new Fixture(TenantA);
        var stranger = new Contact { TenantId = TenantA, FirstName = "Str", LastName = "Anger", DisplayName = "Str Anger", ContactType = "doctor", Status = "active" };
        f.Contacts.Items.Add(stranger); // exists but NOT linked to the account
        var r = await f.Link(TenantA).Handle(LinkWithReportsTo(f.Account.Id, f.Contact.Id, stranger.Id), default);
        Assert.Equal(400, r.StatusCode);
    }

    [Fact]
    public async Task ReportsTo_Cycle_Returns_400()
    {
        var f = new Fixture(TenantA);
        var b = new Contact { TenantId = TenantA, FirstName = "B", LastName = "B", DisplayName = "B B", ContactType = "doctor", Status = "active" };
        f.Contacts.Items.Add(b);
        // A and B both linked; A reports to B.
        await f.Link(TenantA).Handle(Cmd(f.Account.Id, b.Id, role: "decision-maker"), default);
        await f.Link(TenantA).Handle(LinkWithReportsTo(f.Account.Id, f.Contact.Id, b.Id, role: "doctor"), default);
        // Now try to make B report to A → cycle.
        var bLinkId = f.Links.Items.Single(l => l.ContactId == b.Id).Id;
        var upd = new UpdateAccountContactLinkHandler(Tenant(TenantA), f.Links, new FakeValidator(ReferenceValidationStatus.Valid), new NoopAudit());
        var r = await upd.Handle(new UpdateAccountContactLinkCommand(f.Account.Id, bLinkId, "decision-maker", false, null, null, "n", null, null, f.Contact.Id), default);
        Assert.Equal(400, r.StatusCode);
    }

    // ---- fakes ----

    private sealed class FakeAccountRepo : IAccountRepository
    {
        public List<Account> Items { get; } = new();
        public Task<Account?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.TenantId == t && a.Id == id && !a.IsDeleted));
        public Task<Account?> GetByCodeAsync(Guid t, string code, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(a => a.TenantId == t && a.AccountCode == code && !a.IsDeleted));
        public Task<bool> ExistsByCodeAsync(Guid t, string c, Guid? ex, CancellationToken ct) => Task.FromResult(false);
        public Task<(IReadOnlyList<Account> Items, long Total, long UnfilteredTotal)> ListAsync(Guid t, string? s, int p, int ps, string? sortBy, string? sortDir, IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes, IReadOnlyCollection<Guid>? accountIdScope, CancellationToken ct) => Task.FromResult(((IReadOnlyList<Account>)new List<Account>(), 0L, 0L));
        public Task<IReadOnlyList<Account>> GetChildrenAsync(Guid t, Guid p, CancellationToken ct) => Task.FromResult((IReadOnlyList<Account>)new List<Account>());
        public Task<bool> WouldCreateCycleAsync(Guid t, Guid a, Guid c, CancellationToken ct) => Task.FromResult(false);
        public Task InsertAsync(Account a, CancellationToken ct) { Items.Add(a); return Task.CompletedTask; }
        public Task UpdateAsync(Account a, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeContactRepo : IContactRepository
    {
        public List<Contact> Items { get; } = new();
        public Task<Contact?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == t && c.Id == id && !c.IsDeleted));
    public Task<IReadOnlyList<Contact>> ListByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Contact>>(Array.Empty<Contact>());

        public Task<(IReadOnlyList<Contact> Items, long Total, long UnfilteredTotal)> ListAsync(Guid t, string? s, int p, int ps, string? sortBy, string? sortDir, IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? contactTypes, CancellationToken ct) => Task.FromResult(((IReadOnlyList<Contact>)new List<Contact>(), 0L, 0L));
        public Task<IReadOnlyList<Contact>> ListAllAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<Contact>)Items.Where(c => c.TenantId == t && !c.IsDeleted).ToList());
        public Task InsertAsync(Contact c, CancellationToken ct) { Items.Add(c); return Task.CompletedTask; }
        public Task UpdateAsync(Contact c, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeLinkRepo : IAccountContactLinkRepository
    {
        public List<AccountContactLink> Items { get; } = new();
        public Task<AccountContactLink?> GetByIdAsync(Guid t, Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(l => l.TenantId == t && l.Id == id && !l.IsDeleted));
        public Task<bool> ExistsActiveAsync(Guid t, Guid a, Guid c, string r, Guid? ex, CancellationToken ct) => Task.FromResult(Items.Any(l => l.TenantId == t && !l.IsDeleted && !RelationshipLifecycle.IsClosed(l.Status) && l.AccountId == a && l.ContactId == c && l.RoleCode == r && l.Id != ex));
        public Task<bool> ExistsPrimaryAsync(Guid t, Guid a, string r, Guid? ex, CancellationToken ct) => Task.FromResult(Items.Any(l => l.TenantId == t && !l.IsDeleted && !RelationshipLifecycle.IsClosed(l.Status) && l.AccountId == a && l.RoleCode == r && l.IsPrimary && l.Id != ex));
        public Task<IReadOnlyList<AccountContactLink>> ListByAccountAsync(Guid t, Guid a, CancellationToken ct) => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.Where(l => l.TenantId == t && !l.IsDeleted && l.AccountId == a).ToList());
        public Task<IReadOnlyList<AccountContactLink>> ListByContactAsync(Guid t, Guid c, CancellationToken ct) => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.Where(l => l.TenantId == t && !l.IsDeleted && l.ContactId == c).ToList());
        public Task<IReadOnlyList<AccountContactLink>> ListAllAsync(Guid t, CancellationToken ct) => Task.FromResult((IReadOnlyList<AccountContactLink>)Items.Where(l => l.TenantId == t && !l.IsDeleted).ToList());
        public Task InsertAsync(AccountContactLink l, CancellationToken ct) { Items.Add(l); return Task.CompletedTask; }
        public Task UpdateAsync(AccountContactLink l, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeValidator : IReferenceDataValidator
    {
        private readonly ReferenceValidationStatus _s;
        public FakeValidator(ReferenceValidationStatus s) => _s = s;
        public Task<ReferenceValidationResult> ValidateAsync(string setCode, string value, CancellationToken ct) => Task.FromResult(new ReferenceValidationResult(_s, setCode, value));
    }

    private sealed class NoopAudit : IContactAuditPublisher
    {
        public Task PublishAsync(string e, Guid t, Guid c, string? d, CancellationToken ct) => Task.CompletedTask;
    }
}
