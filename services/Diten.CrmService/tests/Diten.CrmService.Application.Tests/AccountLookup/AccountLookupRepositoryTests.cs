using System.Reflection;
using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Account;
using Diten.CrmService.Application.Features.Account.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.Account.Queries;
using Diten.CrmService.Application.Tests.CentralAudit;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Xunit;

namespace Diten.CrmService.Application.Tests.AccountLookup;

/*
 * MOD-0149 §30.9 DL-1 — the guards that live IN THE REPOSITORY (tenant + IsDeleted filter, escaped search, filtered
 * total, sort, index) measured on a TEST-OWNED mongod (DisposableCrmMongod: fresh port, temp dbpath, never 27017;
 * CI: DITEN_TEST_MONGOD). A fake repository cannot see any of these sabotages. The real handlers run on top of the real
 * AccountRepository, so the 404 bytes and the wire field lists are the production ones.
 */
public sealed class AccountLookupRepositoryTests : IClassFixture<AccountLookupMongoFixture>
{
    private readonly AccountLookupMongoFixture _mongo;
    private readonly Guid _tenant = Guid.NewGuid();

    public AccountLookupRepositoryTests(AccountLookupMongoFixture mongo) => _mongo = mongo;

    [Fact]
    public async Task AC_AR_3_u1_read_hides_other_tenant_and_deleted_accounts_behind_one_404()
    {
        var own = await _mongo.InsertAsync(_tenant, "Kendi Hesabı", "active");
        var foreign = await _mongo.InsertAsync(Guid.NewGuid(), "Başka Kiracı", "active");
        var deleted = await _mongo.InsertAsync(_tenant, "Silinmiş", "active", deleted: true);
        var handler = new GetAccountLookupValidationHandler(Tenant(_tenant), _mongo.Repository);

        var ok = await handler.Handle(new GetAccountLookupValidationQuery(own.Id), default);
        var other = await handler.Handle(new GetAccountLookupValidationQuery(foreign.Id), default);
        var gone = await handler.Handle(new GetAccountLookupValidationQuery(deleted.Id), default);
        var missing = await handler.Handle(new GetAccountLookupValidationQuery(Guid.NewGuid()), default);

        Assert.Equal(200, ok.StatusCode);
        Assert.Equal(404, other.StatusCode);
        Assert.Equal(404, gone.StatusCode);
        Assert.Equal(404, missing.StatusCode);
        var body = JsonSerializer.Serialize(missing, AccountLookupHandlerTests.Wire);
        Assert.Equal(body, JsonSerializer.Serialize(other, AccountLookupHandlerTests.Wire));
        Assert.Equal(body, JsonSerializer.Serialize(gone, AccountLookupHandlerTests.Wire));
    }

    [Fact]
    public async Task AC_AR_11_u3_returns_only_the_callers_non_deleted_accounts_with_the_contract_fields()
    {
        var own = await _mongo.InsertAsync(_tenant, "Kapsam Eczanesi", "active");
        await _mongo.InsertAsync(Guid.NewGuid(), "Kapsam Eczanesi Başka", "active");
        await _mongo.InsertAsync(_tenant, "Kapsam Eczanesi Silinmiş", "active", deleted: true);
        var handler = new SearchAccountLookupHandler(Tenant(_tenant), _mongo.Repository);

        var response = await handler.Handle(new SearchAccountLookupQuery("Kapsam", null, null, null), default);

        Assert.Equal(200, response.StatusCode);
        var item = Assert.Single(response.Data!.Items);
        Assert.Equal(own.Id, item.AccountId);
        Assert.Equal(1, response.Data.Total);

        var json = JsonSerializer.Serialize(response.Data, AccountLookupHandlerTests.Wire);
        using var doc = JsonDocument.Parse(json);
        var row = Assert.Single(doc.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(AccountLookupHandlerTests.U3ItemFields, row.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.DoesNotContain("Secret Street", json, StringComparison.Ordinal);
        Assert.True(item.IsBillable);
    }

    [Fact]
    public async Task AC_AR_13_search_is_literal_text_never_a_regex()
    {
        var literal = await _mongo.InsertAsync(_tenant, "Xa.b(Y Medikal", "active");
        await _mongo.InsertAsync(_tenant, "Axb Sağlık", "active");
        await _mongo.InsertAsync(_tenant, "Düz Hesap", "active");

        var star = await _mongo.Repository.SearchLookupAsync(_tenant, ".*", null, 1, 50, default);
        var paren = await _mongo.Repository.SearchLookupAsync(_tenant, "a.b(", null, 1, 50, default);

        Assert.Empty(star.Items);
        Assert.Equal(0, star.Total);
        Assert.Equal(literal.Id, Assert.Single(paren.Items).Id);
    }

    [Fact]
    public async Task AC_AR_13_search_is_contains_on_name_or_code_and_turkish_i_insensitive()
    {
        var hamidiye = await _mongo.InsertAsync(_tenant, "HAMİDİYE ETFAL HASTANESİ", "active");
        var byCode = await _mongo.InsertAsync(_tenant, "Kod İle Bulunan", "active", code: "ACC-ZQ-90817");

        var name = await _mongo.Repository.SearchLookupAsync(_tenant, "hamidiye", null, 1, 50, default);
        var code = await _mongo.Repository.SearchLookupAsync(_tenant, "zq-908", null, 1, 50, default);

        Assert.Equal(hamidiye.Id, Assert.Single(name.Items).Id);
        Assert.Equal(byCode.Id, Assert.Single(code.Items).Id);
    }

    [Fact]
    public async Task AC_AR_14_three_equal_names_with_pageSize_1_appear_once_each_over_three_pages()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add((await _mongo.InsertAsync(_tenant, "Eşit Ad Ltd", "active")).Id);
        }

        var seen = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await _mongo.Repository.SearchLookupAsync(_tenant, "Eşit Ad", null, page, 1, default);
            Assert.Equal(3, result.Total);
            seen.Add(Assert.Single(result.Items).Id);
        }

        var fourth = await _mongo.Repository.SearchLookupAsync(_tenant, "Eşit Ad", null, 4, 1, default);

        Assert.Empty(fourth.Items);
        Assert.Equal(ids.OrderBy(id => id).ToList(), seen.OrderBy(id => id).ToList());
    }

    [Fact]
    public async Task AC_AR_14_sorted_by_name_ascending()
    {
        await _mongo.InsertAsync(_tenant, "Sıra B Hesap", "active");
        await _mongo.InsertAsync(_tenant, "Sıra C Hesap", "active");
        await _mongo.InsertAsync(_tenant, "Sıra A Hesap", "active");

        var result = await _mongo.Repository.SearchLookupAsync(_tenant, "Sıra", null, 1, 50, default);

        Assert.Equal(["Sıra A Hesap", "Sıra B Hesap", "Sıra C Hesap"], result.Items.Select(a => a.AccountName).ToArray());
    }

    [Fact]
    public async Task AC_AR_14_billableOnly_returns_active_only_and_total_is_the_filtered_count()
    {
        var active = await _mongo.InsertAsync(_tenant, "Fatura Aktif", "active");
        await _mongo.InsertAsync(_tenant, "Fatura Pasif", "inactive");
        await _mongo.InsertAsync(_tenant, "Fatura Arşiv", "archived");
        await _mongo.InsertAsync(_tenant, "Başka Bir Şey", "active");
        var handler = new SearchAccountLookupHandler(Tenant(_tenant), _mongo.Repository);

        var billable = await handler.Handle(new SearchAccountLookupQuery("Fatura", 1, 1, true), default);
        var all = await handler.Handle(new SearchAccountLookupQuery("Fatura", 1, 1, false), default);

        Assert.Equal(active.Id, Assert.Single(billable.Data!.Items).AccountId);
        Assert.Equal(1, billable.Data.Total);
        Assert.Equal(3, all.Data!.Total);
        Assert.Single(all.Data.Items);
    }

    [Fact]
    public async Task AC_AR_14_no_match_is_200_and_an_empty_page()
    {
        var handler = new SearchAccountLookupHandler(Tenant(_tenant), _mongo.Repository);

        var response = await handler.Handle(new SearchAccountLookupQuery("hiç-eşleşmeyen", null, null, null), default);

        Assert.Equal(200, response.StatusCode);
        Assert.Empty(response.Data!.Items);
        Assert.Equal(0, response.Data.Total);
    }

    /// <summary>The second key is asserted on the definition itself: equal-name paging alone may not catch its removal.</summary>
    [Fact]
    public void AC_AR_14_the_lookup_sort_renders_to_name_ascending_then_id_ascending()
    {
        Assert.Equal(
            new BsonDocument { { "AccountName", 1 }, { "_id", 1 } },
            Render(AccountRepository.LookupSort));
    }

    /// <summary>AC-AR-10: the list page's sort (BuildSort) is untouched by U-3 — its default stays name ascending alone,
    /// and its explicit keys keep their direction.</summary>
    [Fact]
    public void AC_AR_10_the_list_page_sort_is_unchanged()
    {
        var buildSort = typeof(AccountRepository).GetMethod("BuildSort", BindingFlags.NonPublic | BindingFlags.Static)
                        ?? throw new InvalidOperationException("AccountRepository.BuildSort is gone");
        SortDefinition<Account> Sort(string? by, string? dir) => (SortDefinition<Account>)buildSort.Invoke(null, [by, dir])!;

        Assert.Equal(new BsonDocument("AccountName", 1), Render(Sort(null, null)));
        Assert.Equal(new BsonDocument("AccountName", -1), Render(Sort("accountName", "desc")));
        Assert.Equal(new BsonDocument("AccountCode", -1), Render(Sort("accountCode", "desc")));
        Assert.Equal(new BsonDocument("AccountCode", 1), Render(Sort("accountCode", null)));
    }

    [Fact]
    public async Task The_lookup_index_ix_accounts_tenant_name_id_is_created_and_the_old_one_is_unchanged()
    {
        var indexes = await (await _mongo.Database.GetCollection<BsonDocument>("accounts").Indexes.ListAsync())
            .ToListAsync();
        var byName = indexes.ToDictionary(i => i["name"].AsString, i => i["key"].AsBsonDocument);

        Assert.Equal(
            new BsonDocument { { "TenantId", 1 }, { "AccountName", 1 }, { "_id", 1 } },
            byName["ix_accounts_tenant_name_id"]);
        Assert.Equal(new BsonDocument { { "TenantId", 1 }, { "AccountName", 1 } }, byName["ix_accounts_tenant_name"]);
    }

    private static BsonDocument Render(SortDefinition<Account> sort)
        => sort.Render(BsonSerializer.SerializerRegistry.GetSerializer<Account>(), BsonSerializer.SerializerRegistry);

    private static TenantContext Tenant(Guid tenant)
    {
        var context = new TenantContext();
        context.SetTenant(tenant);
        return context;
    }
}

/// <summary>One test-owned mongod for the class; the production index set is created through the production method.</summary>
public sealed class AccountLookupMongoFixture : IDisposable
{
    private readonly DisposableCrmMongod _mongod = new();

    public AccountLookupMongoFixture()
    {
        Persistence.DependencyInjection.EnsureClassMapsForTests();
        // One database name; every test isolates itself by a fresh TenantId (and the mongod itself is test-owned).
        Database = new MongoClient(_mongod.ConnectionString).GetDatabase("crm_account_lookup_tests");
        var ensureIndexes = typeof(Persistence.DependencyInjection)
                                .GetMethod("TryEnsureIndexes", BindingFlags.NonPublic | BindingFlags.Static)
                            ?? throw new InvalidOperationException("DependencyInjection.TryEnsureIndexes is gone");
        ensureIndexes.Invoke(null, [Database]);
        Repository = new AccountRepository(Database);
    }

    public IMongoDatabase Database { get; }

    public AccountRepository Repository { get; }

    public async Task<Account> InsertAsync(Guid tenant, string name, string status, bool deleted = false, string? code = null)
    {
        var account = new Account
        {
            TenantId = tenant,
            AccountName = name,
            AccountCode = code ?? "ACC-" + Guid.NewGuid().ToString("N")[..12],
            AccountType = "hospital",
            Status = status,
            IsDeleted = deleted,
            DeletedAt = deleted ? DateTimeOffset.UtcNow : null,
            AddressLine = "Secret Street 1",
            ResponsiblePersonName = "Responsible Secret"
        };
        await Repository.InsertAsync(account, default);
        return account;
    }

    public void Dispose() => _mongod.Dispose();
}
