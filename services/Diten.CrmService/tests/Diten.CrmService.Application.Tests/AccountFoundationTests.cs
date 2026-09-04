using System.Reflection;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Account;
using Diten.CrmService.Application.Features.Account.Commands;
using Diten.CrmService.Application.Features.Account.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.Account.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.Account.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;

namespace Diten.CrmService.Application.Tests;

public sealed class AccountFoundationTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    [Fact]
    public void AccountCodeGenerator_Format_Is_Acc_Year_Padded()
    {
        Assert.Equal("ACC-2026-000001", AccountCodeGenerator.Format(2026, 1));
        Assert.Equal("ACC-2026-000042", AccountCodeGenerator.Format(2026, 42));
    }

    [Fact]
    public async Task Create_AutoGenerates_AccountCode_When_Blank()
    {
        var accounts = new FakeAccountRepo();
        var handler = new CreateAccountHandler(
            Tenant(TenantA), accounts, new FakeExternalRefRepo(),
            new AccountCodeGenerator(new FakeSequenceRepo(), accounts, () => new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            new FakeReferenceValidator(ReferenceValidationStatus.Valid), new NoopAudit());

        var response = await handler.Handle(NewCreate(accountCode: null), default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(201, response.StatusCode);
        var stored = accounts.Items.Single();
        Assert.Equal("ACC-2026-000001", stored.AccountCode);
        Assert.Equal(TenantA, stored.TenantId);
    }

    [Fact]
    public async Task Create_Manual_Duplicate_AccountCode_Returns_409()
    {
        var accounts = new FakeAccountRepo();
        accounts.Items.Add(new Account { TenantId = TenantA, AccountCode = "ACC-X", AccountName = "Existing" });
        var handler = new CreateAccountHandler(
            Tenant(TenantA), accounts, new FakeExternalRefRepo(),
            new AccountCodeGenerator(new FakeSequenceRepo(), accounts),
            new FakeReferenceValidator(ReferenceValidationStatus.Valid), new NoopAudit());

        var response = await handler.Handle(NewCreate(accountCode: "ACC-X"), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
    }

    [Fact]
    public async Task Create_With_Unpublished_Required_Set_Returns_400()
    {
        var accounts = new FakeAccountRepo();
        var handler = new CreateAccountHandler(
            Tenant(TenantA), accounts, new FakeExternalRefRepo(),
            new AccountCodeGenerator(new FakeSequenceRepo(), accounts),
            new FakeReferenceValidator(ReferenceValidationStatus.SetMissing), new NoopAudit());

        var response = await handler.Handle(NewCreate(accountCode: null), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
        Assert.Empty(accounts.Items);
    }

    [Fact]
    public async Task GetById_CrossTenant_Returns_404()
    {
        var accounts = new FakeAccountRepo();
        var owned = new Account { TenantId = TenantB, AccountCode = "ACC-1", AccountName = "OtherTenant" };
        accounts.Items.Add(owned);

        var handler = new GetAccountByIdHandler(Tenant(TenantA), accounts, new FakeExternalRefRepo(), new FakeAttrRepo());
        var response = await handler.Handle(new GetAccountByIdQuery(owned.Id), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task LinkParent_Circular_Returns_400()
    {
        var accounts = new FakeAccountRepo();
        var a = new Account { TenantId = TenantA, AccountCode = "A", AccountName = "A" };
        var b = new Account { TenantId = TenantA, AccountCode = "B", AccountName = "B" };
        accounts.Items.Add(a);
        accounts.Items.Add(b);
        accounts.CycleAnswer = true; // simulate b is a descendant of a

        var handler = new LinkParentAccountHandler(Tenant(TenantA), accounts, new NoopAudit());
        var response = await handler.Handle(new LinkParentAccountCommand(a.Id, b.Id), default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task Create_Duplicate_ExternalReference_Returns_409()
    {
        var accounts = new FakeAccountRepo();
        var externals = new FakeExternalRefRepo { Exists = true };
        var handler = new CreateAccountHandler(
            Tenant(TenantA), accounts, externals,
            new AccountCodeGenerator(new FakeSequenceRepo(), accounts),
            new FakeReferenceValidator(ReferenceValidationStatus.Valid), new NoopAudit());

        var command = NewCreate(accountCode: null) with { ExternalReference = new ExternalReferenceInput("EXT-1", "OldCRM", "WorkPlace", null) };
        var response = await handler.Handle(command, default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Empty(accounts.Items);
    }

    [Fact]
    public async Task Create_With_CrossTenant_Parent_Returns_404()
    {
        var accounts = new FakeAccountRepo();
        var otherTenantParent = new Account { TenantId = TenantB, AccountCode = "P", AccountName = "Parent" };
        accounts.Items.Add(otherTenantParent);

        var handler = new CreateAccountHandler(
            Tenant(TenantA), accounts, new FakeExternalRefRepo(),
            new AccountCodeGenerator(new FakeSequenceRepo(), accounts),
            new FakeReferenceValidator(ReferenceValidationStatus.Valid), new NoopAudit());

        var command = NewCreate(accountCode: null) with { ParentAccountId = otherTenantParent.Id };
        var response = await handler.Handle(command, default);

        Assert.False(response.IsSuccessful);
        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task Delete_SoftDeletes_And_Reload_Returns_404()
    {
        var accounts = new FakeAccountRepo();
        var acc = new Account { TenantId = TenantA, AccountCode = "ACC-D", AccountName = "ToDelete" };
        accounts.Items.Add(acc);

        var delete = new DeleteAccountHandler(Tenant(TenantA), accounts, new NoopAudit());
        var deleteResponse = await delete.Handle(new DeleteAccountCommand(acc.Id), default);
        Assert.True(deleteResponse.IsSuccessful);
        Assert.True(acc.IsDeleted);
        Assert.NotNull(acc.DeletedAt);

        var read = new GetAccountByIdHandler(Tenant(TenantA), accounts, new FakeExternalRefRepo(), new FakeAttrRepo());
        var readResponse = await read.Handle(new GetAccountByIdQuery(acc.Id), default);
        Assert.False(readResponse.IsSuccessful);
        Assert.Equal(404, readResponse.StatusCode);
    }

    [Fact]
    public void Account_Entity_Has_No_Zone_Or_Territory_Fields()
    {
        var props = typeof(Account).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToList();
        foreach (var forbidden in new[] { "ZoneId", "MicroZoneId", "TerritoryId", "SalesRepId" })
        {
            Assert.DoesNotContain(forbidden, props);
        }
    }

    private static CreateAccountCommand NewCreate(string? accountCode) => new(
        AccountName: "Acme Hospital", AccountCode: accountCode, AccountType: "hospital", AccountCategory: null,
        ParentAccountId: null, Status: "active", CountryRef: null, CityRef: null, DistrictRef: null,
        AddressLine: null, Latitude: null, Longitude: null, ResponsiblePersonName: null,
        ResponsiblePersonPhone: null, ResponsiblePersonEmail: null, Notes: null, ExternalReference: null);

    // ---- in-memory fakes ----

    private sealed class FakeAccountRepo : IAccountRepository
    {
        public List<Account> Items { get; } = new();
        public bool CycleAnswer { get; set; }

        public Task<Account?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(a => a.TenantId == tenantId && a.Id == id && !a.IsDeleted));

        public Task<Account?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(a => a.TenantId == tenantId && a.AccountCode == code && !a.IsDeleted));

        public Task<bool> ExistsByCodeAsync(Guid tenantId, string code, Guid? excludeId, CancellationToken ct)
            => Task.FromResult(Items.Any(a => a.TenantId == tenantId && !a.IsDeleted && a.AccountCode == code && a.Id != excludeId));

        public Task<(IReadOnlyList<Account> Items, long Total, long UnfilteredTotal)> ListAsync(Guid tenantId, string? search, int page, int pageSize, string? sortBy, string? sortDir, IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes, IReadOnlyCollection<Guid>? accountIdScope, CancellationToken ct)
        {
            var q = Items.Where(a => a.TenantId == tenantId && !a.IsDeleted).ToList();
            return Task.FromResult(((IReadOnlyList<Account>)q, (long)q.Count, (long)q.Count));
        }

        public Task<IReadOnlyList<Account>> GetChildrenAsync(Guid tenantId, Guid parentId, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Account>)Items.Where(a => a.TenantId == tenantId && a.ParentAccountId == parentId && !a.IsDeleted).ToList());

        public Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid accountId, Guid candidateParentId, CancellationToken ct)
            => Task.FromResult(CycleAnswer);

        public Task InsertAsync(Account account, CancellationToken ct) { Items.Add(account); return Task.CompletedTask; }

        public Task UpdateAsync(Account account, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeExternalRefRepo : IAccountExternalReferenceRepository
    {
        public bool Exists { get; set; }
        public Task<bool> ExistsBySourceExternalAsync(Guid tenantId, string sourceSystem, string externalId, Guid? excludeId, CancellationToken ct) => Task.FromResult(Exists);
        public Task<IReadOnlyList<AccountExternalReference>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken ct) => Task.FromResult((IReadOnlyList<AccountExternalReference>)new List<AccountExternalReference>());
        public Task InsertAsync(AccountExternalReference reference, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeAttrRepo : IAccountAttributeValueRepository
    {
        public Task<IReadOnlyList<AccountAttributeValue>> ListByAccountAsync(Guid tenantId, Guid accountId, CancellationToken ct) => Task.FromResult((IReadOnlyList<AccountAttributeValue>)new List<AccountAttributeValue>());
        public Task UpsertAsync(AccountAttributeValue attribute, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeSequenceRepo : IAccountCodeSequenceRepository
    {
        private long _n;
        public Task<long> NextAsync(Guid tenantId, int year, CancellationToken ct) => Task.FromResult(++_n);
    }

    private sealed class FakeReferenceValidator : IReferenceDataValidator
    {
        private readonly ReferenceValidationStatus _status;
        public FakeReferenceValidator(ReferenceValidationStatus status) => _status = status;
        public Task<ReferenceValidationResult> ValidateAsync(string setCode, string value, CancellationToken ct)
            => Task.FromResult(new ReferenceValidationResult(_status, setCode, value));
    }

    private sealed class NoopAudit : IAccountAuditPublisher
    {
        public Task PublishAsync(string eventName, Guid tenantId, Guid accountId, string? detail, CancellationToken ct) => Task.CompletedTask;
    }
}
