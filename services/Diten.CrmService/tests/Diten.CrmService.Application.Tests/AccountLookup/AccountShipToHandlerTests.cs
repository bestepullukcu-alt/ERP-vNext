using System.Text.Json;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Account;
using Diten.CrmService.Application.Features.Account.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.Account.Queries;
using Diten.CrmService.Domain.Entities;
using MongoDB.Driver;
using Xunit;

namespace Diten.CrmService.Application.Tests.AccountLookup;

/*
 * MOD-0149 §32 DL-3 — the U-4 (ship-to) HANDLER over the recording fake repository of the DL-1 tests (it keeps the
 * repository's read contract: tenant + !IsDeleted). The real filter is measured on a real mongod in
 * AccountShipToRepositoryTests, the permission / tenant-token rules on the real host in AccountShipToApiTests.
 * Field lists are compared on the WIRE shape against CRM-ACCOUNT-LOOKUP 1.1 §2.2.
 *
 * DL-4 note (§32.7): every account here has NO extra delivery-address data and no Account.PostalCode (neither exists
 * yet). "One entry" and "postalCode null" are asserted for THAT data only, never as a general rule of U-4.
 */
public sealed class AccountShipToHandlerTests
{
    internal static readonly string[] U4Fields =
        ["accountId", "code", "name", "status", "defaultAddressId", "addresses", "contractVersion"];

    internal static readonly string[] U4AddressFields =
        ["addressId", "kind", "label", "isDefault", "countryRef", "cityRef", "districtRef", "postalCode", "addressLine"];

    private readonly Guid _tenant = Guid.NewGuid();
    private readonly LookupFakeAccountRepository _repo = new();

    // ---------- AC-ST-1 ----------

    [Fact]
    public async Task AC_ST_1_four_filled_fields_without_extra_address_data_are_one_trimmed_primary_default_entry()
    {
        var account = Account("active", "  tr ", "\tist", "kadikoy  ", "  Örnek Mah. Örnek Cad. No: 1  ");
        account.AccountName = "Example Ecza Deposu";
        account.AccountCode = "ACC-2026-000042";

        var response = await U4(_tenant).Handle(new GetAccountShipToQuery(account.Id), default);

        Assert.Equal(200, response.StatusCode);
        var dto = response.Data!;
        Assert.Equal(account.Id, dto.AccountId);
        Assert.Equal("ACC-2026-000042", dto.Code);
        Assert.Equal("Example Ecza Deposu", dto.Name);
        Assert.Equal("active", dto.Status);
        Assert.Equal("crm-account-lookup/1.1", dto.ContractVersion);
        Assert.Equal("primary", dto.DefaultAddressId);

        var address = Assert.Single(dto.Addresses); // no extra delivery-address data on this account (DL-4 adds entries)
        Assert.Equal("primary", address.AddressId);
        Assert.Equal("primary", address.Kind);
        Assert.Null(address.Label);
        Assert.True(address.IsDefault);
        Assert.Equal("tr", address.CountryRef);
        Assert.Equal("ist", address.CityRef);
        Assert.Equal("kadikoy", address.DistrictRef);
        Assert.Equal("Örnek Mah. Örnek Cad. No: 1", address.AddressLine);
        Assert.Null(address.PostalCode); // the account has no postal-code data yet (K-16 is DL-4)
    }

    [Fact]
    public async Task AC_ST_1_the_address_line_is_never_cut()
    {
        var line = string.Join(' ', Enumerable.Repeat("Uzun Adres Satırı No: 12345", 200));
        var account = Account("active", "tr", "ist", "kadikoy", line);

        var response = await U4(_tenant).Handle(new GetAccountShipToQuery(account.Id), default);

        Assert.Equal(line, Assert.Single(response.Data!.Addresses).AddressLine);
    }

    // ---------- AC-ST-2 ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" \t\r\n ")]
    public async Task AC_ST_2_four_blank_fields_are_200_with_no_address_and_no_default(string? blank)
    {
        var account = Account("active", blank, blank, blank, blank);

        var response = await U4(_tenant).Handle(new GetAccountShipToQuery(account.Id), default);

        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(response.Data!.Addresses);
        Assert.Empty(response.Data.Addresses);
        Assert.Null(response.Data.DefaultAddressId);
        Assert.Equal(account.Id, response.Data.AccountId);
        Assert.Equal("crm-account-lookup/1.1", response.Data.ContractVersion);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(response.Data, AccountLookupHandlerTests.Wire));
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("addresses").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("defaultAddressId").ValueKind);
    }

    [Theory]
    [InlineData("countryRef")]
    [InlineData("cityRef")]
    [InlineData("districtRef")]
    [InlineData("addressLine")]
    public async Task AC_ST_2_one_filled_field_is_one_primary_entry_and_the_other_fields_are_null(string filled)
    {
        var account = Account(
            "active",
            filled == "countryRef" ? " dolu " : "  ",
            filled == "cityRef" ? " dolu " : null,
            filled == "districtRef" ? " dolu " : "",
            filled == "addressLine" ? " dolu " : " \t ");

        var response = await U4(_tenant).Handle(new GetAccountShipToQuery(account.Id), default);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("primary", response.Data!.DefaultAddressId);
        var address = Assert.Single(response.Data.Addresses);
        Assert.Equal("primary", address.AddressId);
        Assert.True(address.IsDefault);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(address, AccountLookupHandlerTests.Wire));
        foreach (var field in new[] { "countryRef", "cityRef", "districtRef", "addressLine" })
        {
            var value = doc.RootElement.GetProperty(field);
            if (field == filled)
            {
                Assert.Equal("dolu", value.GetString());
            }
            else
            {
                Assert.Equal(JsonValueKind.Null, value.ValueKind);
            }
        }
    }

    // ---------- AC-ST-3 (the handler's part; the real filter is in AccountShipToRepositoryTests) ----------

    [Fact]
    public async Task AC_ST_3_other_tenant_deleted_and_missing_are_one_404_with_identical_bytes_the_same_as_u1()
    {
        var foreign = _repo.Add(Guid.NewGuid(), "active");
        var deleted = _repo.Add(_tenant, "active", deleted: true);
        var missingId = Guid.NewGuid();

        var other = await U4(_tenant).Handle(new GetAccountShipToQuery(foreign.Id), default);
        var gone = await U4(_tenant).Handle(new GetAccountShipToQuery(deleted.Id), default);
        var missing = await U4(_tenant).Handle(new GetAccountShipToQuery(missingId), default);
        var u1 = await new GetAccountLookupValidationHandler(Tenant(_tenant), _repo)
            .Handle(new GetAccountLookupValidationQuery(missingId), default);

        Assert.Equal(404, other.StatusCode);
        Assert.Equal(404, gone.StatusCode);
        Assert.Equal(404, missing.StatusCode);
        Assert.Null(other.Data);
        Assert.Null(gone.Data);
        var body = JsonSerializer.Serialize(missing, AccountLookupHandlerTests.Wire);
        Assert.Equal(body, JsonSerializer.Serialize(other, AccountLookupHandlerTests.Wire));
        Assert.Equal(body, JsonSerializer.Serialize(gone, AccountLookupHandlerTests.Wire));
        Assert.Equal(JsonSerializer.Serialize(u1, AccountLookupHandlerTests.Wire), body);
    }

    // ---------- AC-ST-4 ----------

    public static TheoryData<string> AllStatuses() => new() { "active", "inactive", "draft", "suspended", "archived", "closed" };

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public async Task AC_ST_4_every_status_is_200_with_the_status_as_stored_and_the_address(string status)
    {
        var account = Account(status, "tr", "ank", "cankaya", "Durum Sok. No: 5");

        var response = await U4(_tenant).Handle(new GetAccountShipToQuery(account.Id), default);

        Assert.Equal(200, response.StatusCode);
        Assert.True(response.IsSuccessful);
        Assert.Equal(status, response.Data!.Status);
        Assert.Equal("primary", response.Data.DefaultAddressId);
        Assert.Contains(response.Data.Addresses, a => a.AddressId == "primary" && a.AddressLine == "Durum Sok. No: 5");
    }

    // ---------- AC-ST-5 ----------

    [Fact]
    public async Task AC_ST_5_the_body_is_exactly_the_contract_fields_for_an_account_whose_other_fields_are_all_filled()
    {
        // _repo.Add fills the responsible person (name / phone / e-mail), the notes and the logo with SecretValues.
        var account = Account("active", "tr", "ist", "kadikoy", "Açık Adres Cad. No: 7");
        account.ParentAccountId = Guid.NewGuid();
        account.AccountCategory = "secret-category";
        account.Latitude = 41.123456;
        account.Longitude = 29.654321;
        Assert.All(
            new[] { account.ResponsiblePersonName, account.ResponsiblePersonPhone, account.ResponsiblePersonEmail, account.Notes, account.LogoDataUri },
            value => Assert.False(string.IsNullOrWhiteSpace(value)));

        var response = await U4(_tenant).Handle(new GetAccountShipToQuery(account.Id), default);

        var json = JsonSerializer.Serialize(response.Data, AccountLookupHandlerTests.Wire);
        Assert.Equal(U4Fields, AccountLookupHandlerTests.FieldNames(json));
        using var doc = JsonDocument.Parse(json);
        var address = Assert.Single(doc.RootElement.GetProperty("addresses").EnumerateArray());
        Assert.Equal(U4AddressFields, address.EnumerateObject().Select(p => p.Name).ToArray());

        foreach (var secret in LookupFakeAccountRepository.SecretValues.Skip(1)) // [0] is the fake's address line
        {
            Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(account.ParentAccountId.Value.ToString(), json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-category", json, StringComparison.Ordinal);
        Assert.DoesNotContain("41.123456", json, StringComparison.Ordinal);
        Assert.DoesNotContain("29.654321", json, StringComparison.Ordinal);
        Assert.DoesNotContain("isBillable", json, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The DTO types themselves carry nothing beyond the contract (a property that is null for this account
    /// would not show in a value search, and <c>[JsonIgnore]</c> would hide one from the wire list).</summary>
    [Fact]
    public void AC_ST_5_the_dto_types_declare_exactly_the_contract_properties()
    {
        static string[] Names(Type type) => type.GetProperties()
            .Where(p => p.Name != "EqualityContract")
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name))
            .ToArray();

        Assert.Equal(U4Fields.Order().ToArray(), Names(typeof(AccountShipToDto)).Order().ToArray());
        Assert.Equal(U4AddressFields.Order().ToArray(), Names(typeof(AccountShipToAddressDto)).Order().ToArray());
    }

    // ---------- AC-ST-7 (the handler's part) ----------

    [Fact]
    public async Task AC_ST_7_without_a_tenant_is_400_and_the_store_is_never_read()
    {
        var account = Account("active", "tr", "ist", "kadikoy", "Kiracı Sok. No: 1");

        var response = await U4(null).Handle(new GetAccountShipToQuery(account.Id), default);

        Assert.Equal(400, response.StatusCode);
        Assert.Null(response.Data);
        Assert.Equal(0, _repo.GetByIdCalls);
    }

    // ---------- AC-ST-8 ----------

    [Theory]
    [InlineData("mongo")]
    [InlineData("timeout")]
    public async Task AC_ST_8_store_failure_is_503_never_404_200_or_500(string failure)
    {
        var account = Account("active", "tr", "ist", "kadikoy", "Depo Sok. No: 1");
        _repo.Failure = failure == "mongo"
            ? new MongoClientException("the store is down")
            : new TimeoutException("server selection timed out");

        var response = await U4(_tenant).Handle(new GetAccountShipToQuery(account.Id), default);

        Assert.Equal(503, response.StatusCode);
        Assert.Null(response.Data);
        Assert.False(response.IsSuccessful);
        Assert.Equal(1, _repo.GetByIdCalls);
    }

    [Fact]
    public async Task AC_ST_8_a_non_store_failure_is_not_disguised_as_503()
    {
        _repo.Failure = new InvalidOperationException("a bug, not an unreachable store");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            U4(_tenant).Handle(new GetAccountShipToQuery(Guid.NewGuid()), default));
    }

    // ---------- AC-ST-9 ----------

    [Fact]
    public async Task AC_ST_9_u1_and_u3_stay_on_contract_1_0_with_their_own_fields_while_u4_reports_1_1()
    {
        var account = Account("active", "tr", "ist", "kadikoy", "Sürüm Sok. No: 1");
        account.AccountName = "Sürüm Hesabı";

        var u1 = await new GetAccountLookupValidationHandler(Tenant(_tenant), _repo)
            .Handle(new GetAccountLookupValidationQuery(account.Id), default);
        var u3 = await new SearchAccountLookupHandler(Tenant(_tenant), _repo)
            .Handle(new SearchAccountLookupQuery("Sürüm Hesabı", null, null, null), default);
        var u4 = await U4(_tenant).Handle(new GetAccountShipToQuery(account.Id), default);

        Assert.Equal("crm-account-lookup/1.0", u1.Data!.ContractVersion);
        Assert.Equal("crm-account-lookup/1.0", u3.Data!.ContractVersion);
        Assert.Equal("crm-account-lookup/1.1", u4.Data!.ContractVersion);
        Assert.Equal("crm-account-lookup/1.0", AccountLookupContract.ContractVersion);
        Assert.Equal(AccountLookupHandlerTests.U1Fields, AccountLookupHandlerTests.FieldNames(JsonSerializer.Serialize(u1.Data, AccountLookupHandlerTests.Wire)));
        Assert.Equal(AccountLookupHandlerTests.U3PageFields, AccountLookupHandlerTests.FieldNames(JsonSerializer.Serialize(u3.Data, AccountLookupHandlerTests.Wire)));
        Assert.Equal(
            AccountLookupHandlerTests.U3ItemFields,
            AccountLookupHandlerTests.FieldNames(JsonSerializer.Serialize(Assert.Single(u3.Data.Items), AccountLookupHandlerTests.Wire)));
    }

    // ---------- helpers ----------

    private Account Account(string status, string? country, string? city, string? district, string? line)
    {
        var account = _repo.Add(_tenant, status);
        account.CountryRef = country;
        account.CityRef = city;
        account.DistrictRef = district;
        account.AddressLine = line;
        return account;
    }

    private GetAccountShipToHandler U4(Guid? tenant) => new(Tenant(tenant), _repo);

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
