using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Account.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.Account.Queries;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.AccountLookup;

/*
 * MOD-0149 §32.7 DL-3 — AC-ST-3 where the guard lives: the tenant + IsDeleted filter of AccountRepository.GetByIdAsync,
 * measured with the REAL U-4 handler on the REAL repository over a TEST-OWNED mongod (the DL-1 fixture:
 * DisposableCrmMongod, fresh port, never 27017; CI: DITEN_TEST_MONGOD). A fake repository cannot see a filter removed
 * from the repository.
 */
public sealed class AccountShipToRepositoryTests : IClassFixture<AccountLookupMongoFixture>
{
    private readonly AccountLookupMongoFixture _mongo;
    private readonly Guid _tenant = Guid.NewGuid();

    public AccountShipToRepositoryTests(AccountLookupMongoFixture mongo) => _mongo = mongo;

    [Fact]
    public async Task AC_ST_3_u4_read_hides_other_tenant_and_deleted_accounts_behind_one_404_with_identical_bytes()
    {
        var own = await _mongo.InsertAsync(_tenant, "Kendi Hesabı", "active");
        var foreign = await _mongo.InsertAsync(Guid.NewGuid(), "Başka Kiracı", "active");
        var deleted = await _mongo.InsertAsync(_tenant, "Silinmiş", "active", deleted: true);
        var handler = new GetAccountShipToHandler(Tenant(_tenant), _mongo.Repository);

        var ok = await handler.Handle(new GetAccountShipToQuery(own.Id), default);
        var other = await handler.Handle(new GetAccountShipToQuery(foreign.Id), default);
        var gone = await handler.Handle(new GetAccountShipToQuery(deleted.Id), default);
        var missing = await handler.Handle(new GetAccountShipToQuery(Guid.NewGuid()), default);

        Assert.Equal(200, ok.StatusCode);
        Assert.Equal(own.Id, ok.Data!.AccountId);
        Assert.Equal(404, other.StatusCode);
        Assert.Equal(404, gone.StatusCode);
        Assert.Equal(404, missing.StatusCode);
        Assert.Null(other.Data);
        Assert.Null(gone.Data);

        var body = JsonSerializer.Serialize(missing, AccountLookupHandlerTests.Wire);
        Assert.Equal(body, JsonSerializer.Serialize(other, AccountLookupHandlerTests.Wire));
        Assert.Equal(body, JsonSerializer.Serialize(gone, AccountLookupHandlerTests.Wire));
        Assert.DoesNotContain("Secret Street", body, StringComparison.Ordinal);
    }

    /// <summary>The same account is readable by its own tenant and only by it: the 404 above is the filter's doing, not a
    /// broken read.</summary>
    [Fact]
    public async Task AC_ST_3_the_other_tenants_account_is_a_200_for_its_own_tenant()
    {
        var otherTenant = Guid.NewGuid();
        var foreign = await _mongo.InsertAsync(otherTenant, "Başka Kiracı", "active");

        var mine = await new GetAccountShipToHandler(Tenant(_tenant), _mongo.Repository)
            .Handle(new GetAccountShipToQuery(foreign.Id), default);
        var theirs = await new GetAccountShipToHandler(Tenant(otherTenant), _mongo.Repository)
            .Handle(new GetAccountShipToQuery(foreign.Id), default);

        Assert.Equal(404, mine.StatusCode);
        Assert.Equal(200, theirs.StatusCode);
    }

    /// <summary>AC-ST-1 / AC-ST-5 through the real store: the stored (untrimmed) address comes back trimmed and the body
    /// keeps the contract's field lists. The account has no extra delivery-address data (DL-4 not built).</summary>
    [Fact]
    public async Task AC_ST_1_a_stored_account_without_extra_address_data_round_trips_as_one_trimmed_primary_entry()
    {
        var account = new Account
        {
            TenantId = _tenant,
            AccountName = "Gerçek Depo Hesabı",
            AccountCode = "ACC-" + Guid.NewGuid().ToString("N")[..12],
            AccountType = "wholesaler",
            Status = "suspended",
            CountryRef = " tr ",
            CityRef = "ist ",
            DistrictRef = null,
            AddressLine = "  Depo Cad. No: 9  ",
            ResponsiblePersonName = "Responsible Secret",
            ResponsiblePersonPhone = "+90 555 000 0000",
            ResponsiblePersonEmail = "secret@example.com",
            Notes = "Secret notes"
        };
        await _mongo.Repository.InsertAsync(account, default);

        var response = await new GetAccountShipToHandler(Tenant(_tenant), _mongo.Repository)
            .Handle(new GetAccountShipToQuery(account.Id), default);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("suspended", response.Data!.Status);
        Assert.Equal("primary", response.Data.DefaultAddressId);
        var address = Assert.Single(response.Data.Addresses);
        Assert.Equal(("tr", "ist", (string?)null, "Depo Cad. No: 9"),
            (address.CountryRef, address.CityRef, address.DistrictRef, address.AddressLine));

        var json = JsonSerializer.Serialize(response.Data, AccountLookupHandlerTests.Wire);
        Assert.Equal(AccountShipToHandlerTests.U4Fields, AccountLookupHandlerTests.FieldNames(json));
        foreach (var secret in new[] { "Responsible Secret", "+90 555 000 0000", "secret@example.com", "Secret notes" })
        {
            Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
        }
    }

    private static TenantContext Tenant(Guid tenant)
    {
        var context = new TenantContext();
        context.SetTenant(tenant);
        return context;
    }
}
