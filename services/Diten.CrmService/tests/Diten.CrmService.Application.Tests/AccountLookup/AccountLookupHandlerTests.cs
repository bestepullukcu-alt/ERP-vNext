using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Account;
using Diten.CrmService.Application.Features.Account.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.Account.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;
using Xunit;

namespace Diten.CrmService.Application.Tests.AccountLookup;

/*
 * MOD-0149 §30 DL-1 — U-1 (lookup-validation) and U-3 (lookup) HANDLERS over a recording fake repository.
 * The fake keeps the repository's contract (tenant + !IsDeleted on reads) so the handler is measured, not the fake;
 * the real filters, the escaped search, the filtered total and the sort are measured on a real mongod in
 * AccountLookupRepositoryTests. Field lists are compared on the WIRE shape (System.Text.Json web defaults = the
 * camelCase names MVC writes), against CRM-ACCOUNT-LOOKUP 1.0 §2.2 / §2a.2.
 */
public sealed class AccountLookupHandlerTests
{
    internal static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web);

    internal static readonly string[] U1Fields = ["accountId", "code", "name", "status", "isBillable", "contractVersion"];
    internal static readonly string[] U3ItemFields = ["accountId", "code", "name", "status", "isBillable"];
    internal static readonly string[] U3PageFields = ["items", "page", "pageSize", "total", "contractVersion"];

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly LookupFakeAccountRepository _repo = new();

    // ---------- U-1 ----------

    [Fact]
    public async Task AC_AR_1_active_account_is_200_billable_with_exactly_the_contract_fields()
    {
        var account = _repo.Add(_tenant, "active", name: "Example Hastanesi", code: "ACC-2026-000042");

        var response = await U1(_tenant).Handle(new GetAccountLookupValidationQuery(account.Id), default);

        Assert.Equal(200, response.StatusCode);
        var dto = response.Data!;
        Assert.Equal(account.Id, dto.AccountId);
        Assert.Equal("ACC-2026-000042", dto.Code);
        Assert.Equal("Example Hastanesi", dto.Name);
        Assert.Equal("active", dto.Status);
        Assert.True(dto.IsBillable);
        Assert.Equal("crm-account-lookup/1.0", dto.ContractVersion);

        var json = JsonSerializer.Serialize(dto, Wire);
        Assert.Equal(U1Fields, FieldNames(json));
        foreach (var secret in LookupFakeAccountRepository.SecretValues)
        {
            Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("draft")]
    [InlineData("suspended")]
    [InlineData("archived")]
    [InlineData("closed")] // not in the account-status set (contract §4): still 200, not billable
    [InlineData("Active")] // ordinal comparison: case matters
    public async Task AC_AR_2_every_non_active_status_is_200_and_not_billable(string status)
    {
        var account = _repo.Add(_tenant, status);

        var response = await U1(_tenant).Handle(new GetAccountLookupValidationQuery(account.Id), default);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(status, response.Data!.Status);
        Assert.False(response.Data.IsBillable);
    }

    [Fact]
    public async Task AC_AR_3_other_tenant_deleted_and_missing_are_one_404_with_identical_bytes()
    {
        var foreign = _repo.Add(Guid.NewGuid(), "active");
        var deleted = _repo.Add(_tenant, "active", deleted: true);

        var other = await U1(_tenant).Handle(new GetAccountLookupValidationQuery(foreign.Id), default);
        var gone = await U1(_tenant).Handle(new GetAccountLookupValidationQuery(deleted.Id), default);
        var missing = await U1(_tenant).Handle(new GetAccountLookupValidationQuery(Guid.NewGuid()), default);

        Assert.Equal(404, other.StatusCode);
        Assert.Equal(404, gone.StatusCode);
        Assert.Equal(404, missing.StatusCode);
        var body = JsonSerializer.Serialize(missing, Wire);
        Assert.Equal(body, JsonSerializer.Serialize(other, Wire));
        Assert.Equal(body, JsonSerializer.Serialize(gone, Wire));
        Assert.Null(other.Data);
    }

    // ---------- U-3 ----------

    [Fact]
    public async Task AC_AR_11_u3_items_carry_exactly_the_contract_fields_and_the_page_its_own()
    {
        _repo.Add(_tenant, "active", name: "Alfa Eczanesi");

        var response = await U3(_tenant).Handle(new SearchAccountLookupQuery("alfa", null, null, null), default);

        Assert.Equal(200, response.StatusCode);
        var json = JsonSerializer.Serialize(response.Data, Wire);
        Assert.Equal(U3PageFields, FieldNames(json));
        using var doc = JsonDocument.Parse(json);
        var item = Assert.Single(doc.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(U3ItemFields, item.EnumerateObject().Select(p => p.Name).ToArray());
        foreach (var secret in LookupFakeAccountRepository.SecretValues)
        {
            Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
        }

        Assert.Equal(1, response.Data!.Page);
        Assert.Equal(AccountLookupContract.DefaultPageSize, response.Data.PageSize);
        Assert.Equal("crm-account-lookup/1.0", response.Data.ContractVersion);
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", null, null)]
    [InlineData("a", null, null)]
    [InlineData("   a   ", null, null)] // Trim() first: one character
    [InlineData("101", null, null)]     // 101 characters (built below)
    [InlineData("ok", 0, null)]
    [InlineData("ok", -1, null)]
    [InlineData("ok", null, 0)]
    [InlineData("ok", null, 51)]
    public async Task AC_AR_12_out_of_bound_input_is_400_and_nothing_is_read(string? search, int? page, int? pageSize)
    {
        if (search == "101")
        {
            search = new string('x', 101);
        }

        _repo.Add(_tenant, "active", name: "ok account");

        var response = await U3(_tenant).Handle(new SearchAccountLookupQuery(search, page, pageSize, null), default);

        Assert.Equal(400, response.StatusCode);
        Assert.Null(response.Data);
        Assert.Equal(0, _repo.SearchCalls);
    }

    [Theory]
    [InlineData("ab", 1, 1)]
    [InlineData("  ab  ", 1, 50)]
    [InlineData("100", 7, 50)] // exactly 100 characters
    public async Task AC_AR_12_in_bound_input_reaches_the_store(string search, int page, int pageSize)
    {
        if (search == "100")
        {
            search = new string('y', 100);
        }

        var response = await U3(_tenant).Handle(new SearchAccountLookupQuery(search, page, pageSize, null), default);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(1, _repo.SearchCalls);
        Assert.Equal(search.Trim(), _repo.LastSearch!.Value.Search);
        Assert.Equal(page, response.Data!.Page);
        Assert.Equal(pageSize, response.Data.PageSize);
    }

    [Fact]
    public async Task AC_AR_14_billableOnly_asks_the_store_for_active_only_and_passes_the_filtered_total()
    {
        var response = await U3(_tenant).Handle(new SearchAccountLookupQuery("ab", 2, 5, true), default);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("active", _repo.LastSearch!.Value.Status);
        Assert.Equal((2, 5), (_repo.LastSearch.Value.Page, _repo.LastSearch.Value.PageSize));
        Assert.Equal(_repo.LastTotal, response.Data!.Total);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task AC_AR_14_without_billableOnly_no_status_narrows_the_search(bool? billableOnly)
    {
        await U3(_tenant).Handle(new SearchAccountLookupQuery("ab", null, null, billableOnly), default);

        Assert.Null(_repo.LastSearch!.Value.Status);
    }

    [Fact]
    public async Task AC_AR_14_no_match_is_200_with_an_empty_page()
    {
        var response = await U3(_tenant).Handle(new SearchAccountLookupQuery("nothing-like-this", null, null, null), default);

        Assert.Equal(200, response.StatusCode);
        Assert.Empty(response.Data!.Items);
        Assert.Equal(0, response.Data.Total);
    }

    // ---------- both ----------

    public static TheoryData<string> StoreFailures() => new() { "mongo", "timeout" };

    [Theory]
    [MemberData(nameof(StoreFailures))]
    public async Task AC_AR_7a_u1_store_failure_is_503_never_404_200_or_500(string failure)
    {
        var account = _repo.Add(_tenant, "active");
        _repo.Failure = Failure(failure);

        var response = await U1(_tenant).Handle(new GetAccountLookupValidationQuery(account.Id), default);

        Assert.Equal(503, response.StatusCode);
        Assert.Null(response.Data);
        Assert.False(response.IsSuccessful);
    }

    [Theory]
    [MemberData(nameof(StoreFailures))]
    public async Task AC_AR_7a_u3_store_failure_is_503_never_an_empty_list(string failure)
    {
        _repo.Add(_tenant, "active", name: "Alfa");
        _repo.Failure = Failure(failure);

        var response = await U3(_tenant).Handle(new SearchAccountLookupQuery("alfa", null, null, null), default);

        Assert.Equal(503, response.StatusCode);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task AC_AR_7a_a_non_store_failure_is_not_disguised_as_503()
    {
        _repo.Failure = new InvalidOperationException("a bug, not an unreachable store");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            U1(_tenant).Handle(new GetAccountLookupValidationQuery(Guid.NewGuid()), default));
    }

    public static TheoryData<string> AllStatuses() => new() { "active", "inactive", "draft", "suspended", "archived", "closed" };

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public async Task AC_AR_9a_u1_and_u3_give_the_same_isBillable_and_it_is_status_equals_active(string status)
    {
        var account = _repo.Add(_tenant, status, name: "Uyum " + status);

        var u1 = await U1(_tenant).Handle(new GetAccountLookupValidationQuery(account.Id), default);
        var u3 = await U3(_tenant).Handle(new SearchAccountLookupQuery("Uyum " + status, null, null, null), default);

        var item = Assert.Single(u3.Data!.Items);
        Assert.Equal(account.Id, item.AccountId);
        Assert.Equal(u1.Data!.IsBillable, item.IsBillable);
        Assert.Equal(status == "active", u1.Data.IsBillable);
        Assert.Equal(u1.Data.Status, item.Status);
        Assert.Equal((u1.Data.Code, u1.Data.Name), (item.Code, item.Name));
        Assert.Equal("crm-account-lookup/1.0", u1.Data.ContractVersion);
        Assert.Equal("crm-account-lookup/1.0", u3.Data.ContractVersion);
        Assert.Equal(U1Fields, FieldNames(JsonSerializer.Serialize(u1.Data, Wire)));
        Assert.Equal(U3ItemFields, FieldNames(JsonSerializer.Serialize(item, Wire)));
    }

    [Fact]
    public async Task AC_AR_15_u1_without_a_tenant_is_400_and_the_store_is_never_read()
    {
        var account = _repo.Add(_tenant, "active");

        var response = await U1(null).Handle(new GetAccountLookupValidationQuery(account.Id), default);

        Assert.Equal(400, response.StatusCode);
        Assert.Null(response.Data);
        Assert.Equal(0, _repo.GetByIdCalls);
    }

    [Fact]
    public async Task AC_AR_15_u3_without_a_tenant_is_400_and_the_store_is_never_read()
    {
        _repo.Add(_tenant, "active", name: "Alfa");

        var response = await U3(null).Handle(new SearchAccountLookupQuery("alfa", null, null, null), default);

        Assert.Equal(400, response.StatusCode);
        Assert.Null(response.Data);
        Assert.Equal(0, _repo.SearchCalls);
    }

    // ---------- helpers ----------

    internal static string[] FieldNames(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
    }

    private static Exception Failure(string kind) => kind switch
    {
        "mongo" => new MongoClientException("the store is down"),
        _ => new TimeoutException("server selection timed out")
    };

    private GetAccountLookupValidationHandler U1(Guid? tenant) => new(Tenant(tenant), _repo);

    private SearchAccountLookupHandler U3(Guid? tenant) => new(Tenant(tenant), _repo);

    private static TenantContext Tenant(Guid? tenant)
    {
        var context = new TenantContext();
        if (tenant is { } id)
        {
            context.SetTenant(id);
        }

        return context;
    }
}

/// <summary>Keeps the repository's read contract (tenant + !IsDeleted; search is a case-insensitive contains on name or
/// code; name then id ascending) and records what the handler asked.</summary>
internal sealed class LookupFakeAccountRepository : IAccountRepository
{
    internal static readonly string[] SecretValues =
        ["Secret Street 1", "Responsible Secret", "+90 555 000 0000", "secret@example.com", "Secret notes", "data:image/png;base64,SECRET"];

    private readonly List<Account> _rows = [];

    public Exception? Failure { get; set; }

    public int GetByIdCalls { get; private set; }

    public int SearchCalls { get; private set; }

    public (string Search, string? Status, int Page, int PageSize)? LastSearch { get; private set; }

    public long LastTotal { get; private set; }

    public Account Add(Guid tenant, string status, string? name = null, string? code = null, bool deleted = false)
    {
        var account = new Account
        {
            TenantId = tenant,
            Status = status,
            AccountName = name ?? "Account " + Guid.NewGuid().ToString("N")[..8],
            AccountCode = code ?? "ACC-" + Guid.NewGuid().ToString("N")[..10],
            AccountType = "hospital",
            IsDeleted = deleted,
            AddressLine = SecretValues[0],
            ResponsiblePersonName = SecretValues[1],
            ResponsiblePersonPhone = SecretValues[2],
            ResponsiblePersonEmail = SecretValues[3],
            Notes = SecretValues[4],
            LogoDataUri = SecretValues[5]
        };
        _rows.Add(account);
        return account;
    }

    public Task<Account?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        GetByIdCalls++;
        if (Failure is not null)
        {
            throw Failure;
        }

        return Task.FromResult(_rows.FirstOrDefault(a => a.TenantId == tenantId && !a.IsDeleted && a.Id == id));
    }

    public Task<(IReadOnlyList<Account> Items, long Total)> SearchLookupAsync(
        Guid tenantId, string search, string? status, int page, int pageSize, CancellationToken cancellationToken)
    {
        SearchCalls++;
        LastSearch = (search, status, page, pageSize);
        if (Failure is not null)
        {
            throw Failure;
        }

        var matches = _rows
            .Where(a => a.TenantId == tenantId && !a.IsDeleted)
            .Where(a => a.AccountName.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || a.AccountCode.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Where(a => status is null || a.Status == status)
            .OrderBy(a => a.AccountName, StringComparer.Ordinal).ThenBy(a => a.Id)
            .ToList();
        LastTotal = matches.Count;
        IReadOnlyList<Account> pageRows = matches.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult((pageRows, (long)matches.Count));
    }

    public Task<Account?> GetByCodeAsync(Guid tenantId, string accountCode, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<bool> ExistsByCodeAsync(Guid tenantId, string accountCode, Guid? excludeId, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<(IReadOnlyList<Account> Items, long Total, long UnfilteredTotal)> ListAsync(
        Guid tenantId, string? search, int page, int pageSize, string? sortBy, string? sortDir,
        IReadOnlyCollection<string>? statuses, IReadOnlyCollection<string>? accountTypes,
        IReadOnlyCollection<Guid>? accountIdScope, CancellationToken cancellationToken)
        => throw new NotSupportedException("U-1 / U-3 never use the list page's query.");

    public Task<IReadOnlyList<Account>> GetChildrenAsync(Guid tenantId, Guid parentId, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task<bool> WouldCreateCycleAsync(Guid tenantId, Guid accountId, Guid candidateParentId, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public Task InsertAsync(Account account, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task UpdateAsync(Account account, CancellationToken cancellationToken) => throw new NotSupportedException();
}
