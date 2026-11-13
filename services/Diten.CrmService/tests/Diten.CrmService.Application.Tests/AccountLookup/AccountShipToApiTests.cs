using System.Net;
using System.Text.Json;
using Diten.CrmService.Api.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using Xunit.Abstractions;

namespace Diten.CrmService.Application.Tests.AccountLookup;

/*
 * MOD-0149 §32 DL-3 — U-4 (ship-to) through THE REAL CRM HOST (the DL-1 factory: JWT bearer, TenantResolutionMiddleware,
 * permission policies, MVC filters, real HTTP, signed tokens). Only the account repository is the recording fake, so
 * "the store was never read" is measured. Same collection as the DL-1 API tests: the host reads its settings from
 * process variables while it is built, so the two classes never run side by side.
 *
 * The K-11 placement over the whole host ({U-1, U-3, U-4}, never global) is AccountLookupApiTests'
 * AC_AR_15_K11_filter_is_on_u1_u3_and_u4_only_and_never_global; this class holds U-4's own side of it.
 */
[Collection(nameof(AccountLookupApiCollection))]
public sealed class AccountShipToApiTests : IClassFixture<AccountLookupApiFactory>
{
    private const string ShipToKey = "crm.account-ship-to.read";
    private const string LookupKey = "crm.account.lookup-validation";
    private const string ReadKey = "crm.account.read";
    private const string Marker = "Sevk Hesabı";

    private readonly AccountLookupApiFactory _factory;
    private readonly ITestOutputHelper _output;
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly LookupFakeAccountRepository _repo = new();

    public AccountShipToApiTests(AccountLookupApiFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _factory.Repository = _repo;
        _output = output;
    }

    // ---------- AC-ST-6 ----------

    public static TheoryData<string, string[]> InsufficientKeys() => new()
    {
        { "no key", [] },
        { "crm.account.read alone", [ReadKey] },
        { "crm.account.lookup-validation alone", [LookupKey] },
        { "crm.account.read + crm.account.lookup-validation", [ReadKey, LookupKey] },
        { "a key that only starts like it", ["crm.account-ship-to"] },
        { "a key that only starts like it (2)", ["crm.account-ship-to.read.all"] }
    };

    [Theory]
    [MemberData(nameof(InsufficientKeys))]
    public async Task AC_ST_6_u4_without_the_ship_to_key_is_403(string label, string[] permissions)
    {
        var account = Add();

        var response = await SendAsync(ShipToUrl(account), Token(_tenant, permissions), _tenant);

        Assert.True(HttpStatusCode.Forbidden == response.StatusCode, $"{label}: {(int)response.StatusCode}");
        Assert.DoesNotContain(Marker, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(0, _repo.GetByIdCalls);
        _output.WriteLine($"403 ({label}): [{await response.Content.ReadAsStringAsync()}]");
    }

    [Fact]
    public async Task AC_ST_6_the_ship_to_key_alone_is_200()
    {
        var account = Add();

        var response = await SendAsync(ShipToUrl(account), Token(_tenant, ShipToKey), _tenant);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("crm-account-lookup/1.1", body, StringComparison.Ordinal);
        Assert.Contains(Marker, body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("u1")]
    [InlineData("u3")]
    public async Task AC_ST_6_the_ship_to_key_alone_is_403_on_u1_and_u3(string endpoint)
    {
        var account = Add();
        var url = endpoint == "u1"
            ? $"/api/crm/accounts/{account.Id}/lookup-validation"
            : $"/api/crm/accounts/lookup?search={Uri.EscapeDataString(Marker)}";

        var response = await SendAsync(url, Token(_tenant, ShipToKey), _tenant);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain(Marker, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(0, _repo.GetByIdCalls);
        Assert.Equal(0, _repo.SearchCalls);
    }

    /// <summary>The full-record endpoint stays behind <c>crm.account.read</c>: the ship-to key does not open it.</summary>
    [Fact]
    public async Task AC_ST_6_the_ship_to_key_alone_is_403_on_the_full_account_record()
    {
        var account = Add();

        var response = await SendAsync($"/api/crm/accounts/{account.Id}", Token(_tenant, ShipToKey), _tenant);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- AC-ST-1 / AC-ST-2 / AC-ST-5 on the wire ----------

    [Fact]
    public async Task AC_ST_5_the_wire_body_is_exactly_the_contract_fields_nulls_included()
    {
        var account = Add();
        account.ParentAccountId = Guid.NewGuid();
        account.Latitude = 41.123456;
        account.Longitude = 29.654321;

        var response = await SendAsync(ShipToUrl(account), Token(_tenant, ShipToKey), _tenant);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"200: {body}");
        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(AccountShipToHandlerTests.U4Fields, data.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(account.Id, data.GetProperty("accountId").GetGuid());
        Assert.Equal("primary", data.GetProperty("defaultAddressId").GetString());
        Assert.Equal("crm-account-lookup/1.1", data.GetProperty("contractVersion").GetString());

        var address = Assert.Single(data.GetProperty("addresses").EnumerateArray()); // no extra delivery-address data
        Assert.Equal(AccountShipToHandlerTests.U4AddressFields, address.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal("primary", address.GetProperty("addressId").GetString());
        Assert.Equal("primary", address.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, address.GetProperty("label").ValueKind);
        Assert.True(address.GetProperty("isDefault").GetBoolean());
        Assert.Equal("tr", address.GetProperty("countryRef").GetString());
        Assert.Equal("Sevk Cad. No: 3", address.GetProperty("addressLine").GetString());

        foreach (var secret in LookupFakeAccountRepository.SecretValues.Skip(1))
        {
            Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(account.ParentAccountId.Value.ToString(), body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("41.123456", body, StringComparison.Ordinal);
        Assert.DoesNotContain("isBillable", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AC_ST_2_an_account_without_an_address_is_200_with_an_empty_list_and_a_null_default_on_the_wire()
    {
        var account = Add();
        account.CountryRef = account.CityRef = account.DistrictRef = null;
        account.AddressLine = "   ";

        var response = await SendAsync(ShipToUrl(account), Token(_tenant, ShipToKey), _tenant);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"200 (no address): {body}");
        using var doc = JsonDocument.Parse(body);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(AccountShipToHandlerTests.U4Fields, data.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.Equal(0, data.GetProperty("addresses").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("defaultAddressId").ValueKind);
    }

    // ---------- AC-ST-7 ----------

    [Fact]
    public async Task AC_ST_7_header_other_than_the_token_tenant_is_400_and_the_store_is_never_read()
    {
        var other = Guid.NewGuid();
        var account = Add(other);

        var response = await SendAsync(ShipToUrl(account), Token(_tenant, ShipToKey), other);

        await AssertRefusedAsync(response);
    }

    [Fact]
    public async Task AC_ST_7_a_call_without_a_tenant_header_is_400_and_the_store_is_never_read()
    {
        var account = Add();

        var response = await SendAsync(ShipToUrl(account), Token(_tenant, ShipToKey), tenantHeader: null);

        await AssertRefusedAsync(response);
    }

    [Fact]
    public async Task AC_ST_7_K11_a_token_without_tenant_id_is_400_even_with_the_header_and_the_store_is_never_read()
    {
        var account = Add();

        var response = await SendAsync(ShipToUrl(account), Token(tenant: null, [ShipToKey]), _tenant);

        await AssertRefusedAsync(response);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(body.RootElement.TryGetProperty("data", out _));
        Assert.Equal("Tenant token required", body.RootElement.GetProperty("title").GetString());
    }

    [Theory]
    [InlineData("not-a-guid", false)]
    [InlineData("00000000-0000-0000-0000-000000000000", false)]
    [InlineData("00000000-0000-0000-0000-000000000000", true)]
    public async Task AC_ST_7_K11_a_token_whose_tenant_id_names_no_tenant_is_400_and_the_store_is_never_read(
        string rawTenantClaim, bool emptyHeader)
    {
        var account = Add();

        var response = await SendAsync(
            ShipToUrl(account), Token(tenant: null, [ShipToKey], rawTenantClaim), emptyHeader ? Guid.Empty : _tenant);

        await AssertRefusedAsync(response);
    }

    /// <summary>U-4's own side of the placement rule: the action carries the K-11 filter and the ship-to permission, in
    /// the running host's endpoint metadata.</summary>
    [Fact]
    public void AC_ST_7_the_u4_action_carries_the_K11_filter_and_only_the_ship_to_permission()
    {
        _factory.CreateClient().Dispose(); // starts the host so the endpoint data source is built

        var u4 = Assert.Single(
            _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>(),
            e => string.Equals(e.RoutePattern.RawText, "api/crm/accounts/{accountId:guid}/ship-to", StringComparison.Ordinal));

        var action = u4.Metadata.GetMetadata<ControllerActionDescriptor>()!;
        Assert.Equal("Diten.CrmService.Api.Controllers.CRM.AccountController", action.ControllerTypeInfo.FullName);
        Assert.Equal("ShipTo", action.ActionName);
        Assert.NotEmpty(u4.Metadata.GetOrderedMetadata<RequireTokenTenantAttribute>());
        Assert.Contains(action.FilterDescriptors, f => f.Filter is RequireTokenTenantAttribute);
        Assert.Equal(["GET"], u4.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods);

        var permissions = u4.Metadata
            .GetOrderedMetadata<Diten.CrmService.Infrastructure.Authorization.HasPermissionAttribute>()
            .Select(p => p.Permission)
            .ToArray();
        Assert.Equal([ShipToKey], permissions);
    }

    // ---------- AC-ST-3 / AC-ST-8 on the wire ----------

    [Fact]
    public async Task AC_ST_3_u4_404_bodies_are_byte_identical_on_the_wire_and_the_same_as_u1()
    {
        var foreign = Add(Guid.NewGuid());
        var deleted = _repo.Add(_tenant, "active", deleted: true);
        var missingId = Guid.NewGuid();
        var token = Token(_tenant, ShipToKey);

        var other = await SendAsync(ShipToUrl(foreign), token, _tenant);
        var gone = await SendAsync(ShipToUrl(deleted), token, _tenant);
        var missing = await SendAsync($"/api/crm/accounts/{missingId}/ship-to", token, _tenant);
        var u1 = await SendAsync($"/api/crm/accounts/{missingId}/lookup-validation", Token(_tenant, LookupKey), _tenant);

        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, u1.StatusCode);
        var body = await missing.Content.ReadAsByteArrayAsync();
        Assert.Equal(body, await other.Content.ReadAsByteArrayAsync());
        Assert.Equal(body, await gone.Content.ReadAsByteArrayAsync());
        Assert.Equal(await u1.Content.ReadAsByteArrayAsync(), body);
        Assert.DoesNotContain(Marker, System.Text.Encoding.UTF8.GetString(body), StringComparison.Ordinal);
        _output.WriteLine($"404: {System.Text.Encoding.UTF8.GetString(body)}");
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("archived")]
    [InlineData("closed")]
    public async Task AC_ST_4_a_non_active_account_is_200_on_the_wire(string status)
    {
        var account = Add(status: status);

        var response = await SendAsync(ShipToUrl(account), Token(_tenant, ShipToKey), _tenant);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(status, doc.RootElement.GetProperty("data").GetProperty("status").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("data").GetProperty("addresses").GetArrayLength());
    }

    [Fact]
    public async Task AC_ST_8_store_failure_is_503_on_the_wire()
    {
        var account = Add();
        _repo.Failure = new TimeoutException("server selection timed out");

        var response = await SendAsync(ShipToUrl(account), Token(_tenant, ShipToKey), _tenant);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Marker, body, StringComparison.Ordinal);
        Assert.DoesNotContain("addresses", body, StringComparison.Ordinal);
        _output.WriteLine($"503: {body}");
    }

    // ---------- helpers ----------

    private Domain.Entities.Account Add(Guid? tenant = null, string status = "active")
    {
        var account = _repo.Add(tenant ?? _tenant, status, name: Marker);
        account.CountryRef = " tr ";
        account.CityRef = "ist";
        account.DistrictRef = "kadikoy";
        account.AddressLine = " Sevk Cad. No: 3 ";
        return account;
    }

    private static string ShipToUrl(Domain.Entities.Account account) => $"/api/crm/accounts/{account.Id}/ship-to";

    private async Task AssertRefusedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Marker, body, StringComparison.Ordinal);
        Assert.DoesNotContain("Sevk Cad", body, StringComparison.Ordinal);
        Assert.Equal(0, _repo.GetByIdCalls);
        Assert.Equal(0, _repo.SearchCalls);
    }

    private async Task<HttpResponseMessage> SendAsync(string url, string token, Guid? tenantHeader)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        if (tenantHeader is { } tenant)
        {
            request.Headers.Add("X-Tenant-Id", tenant.ToString());
        }

        return await client.SendAsync(request);
    }

    private string Token(Guid? tenant, string permission) => Token(tenant, [permission]);

    private string Token(Guid? tenant, string[] permissions, string? rawTenantClaim = null)
    {
        var claims = new Dictionary<string, object> { ["sub"] = Guid.NewGuid().ToString() };
        if (tenant is { } id)
        {
            claims["tenant_id"] = id.ToString();
        }
        else if (rawTenantClaim is not null)
        {
            claims["tenant_id"] = rawTenantClaim;
        }

        if (permissions.Length > 0)
        {
            claims["permission"] = permissions;
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = AccountLookupApiFactory.Issuer,
            Audience = AccountLookupApiFactory.Audience,
            Claims = claims,
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_factory.Secret)), SecurityAlgorithms.HmacSha256)
        });
    }
}
