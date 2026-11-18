using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Diten.CrmService.Api.Filters;
using Diten.CrmService.Application.Tests.CentralAudit;
using Diten.CrmService.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.CrmService.Application.Tests.AccountLookup;

[CollectionDefinition(nameof(AccountLookupApiCollection), DisableParallelization = true)]
public sealed class AccountLookupApiCollection;

/*
 * MOD-0149 §30 DL-1 — U-1 and U-3 through THE REAL CRM HOST (Program.cs: JWT bearer, TenantResolutionMiddleware,
 * permission policies, MVC filters), signed tokens, real HTTP. Only the account repository is swapped for the recording
 * fake, so "the store was never read" is measured. The collection runs alone: Program.cs reads its settings from
 * process variables while the host is built.
 *
 * K-11 placement (inceleme 2026-11-05-2840220b F-1): the filter is found ONLY on U-1 and U-3 in the endpoint metadata,
 * and never in MvcOptions.Filters. What other CRM endpoints do with a tenant-less token is BL-324's question: no test
 * here expects or pins their data.
 */
[Collection(nameof(AccountLookupApiCollection))]
public sealed class AccountLookupApiTests : IClassFixture<AccountLookupApiFactory>
{
    private const string LookupKey = "crm.account.lookup-validation";
    private const string ReadKey = "crm.account.read";

    private readonly AccountLookupApiFactory _factory;
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly LookupFakeAccountRepository _repo = new();

    public AccountLookupApiTests(AccountLookupApiFactory factory)
    {
        _factory = factory;
        _factory.Repository = _repo;
    }

    public static TheoryData<string> Endpoints() => new() { "u1", "u3" };

    // ---------- AC-AR-8a ----------

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AC_AR_8a_without_the_lookup_key_is_403(string endpoint)
    {
        var url = Url(endpoint, _repo.Add(_tenant, "active", name: "Yetki Hesabı"));

        var response = await SendAsync(url, Token(_tenant), _tenant);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AC_AR_8a_crm_account_read_alone_is_403(string endpoint)
    {
        var url = Url(endpoint, _repo.Add(_tenant, "active", name: "Yetki Hesabı"));

        var response = await SendAsync(url, Token(_tenant, ReadKey), _tenant);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AC_AR_8a_the_lookup_key_without_crm_account_read_is_200(string endpoint)
    {
        var url = Url(endpoint, _repo.Add(_tenant, "active", name: "Yetki Hesabı"));

        var response = await SendAsync(url, Token(_tenant, LookupKey), _tenant);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("crm-account-lookup/1.0", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    // ---------- AC-AR-15 ----------

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AC_AR_15_control_matching_token_and_header_reach_the_data(string endpoint)
    {
        var url = Url(endpoint, _repo.Add(_tenant, "active", name: "Kiracı Hesabı"));

        var response = await SendAsync(url, Token(_tenant, LookupKey), _tenant);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Kiracı Hesabı", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AC_AR_15_header_other_than_the_token_tenant_is_400_and_the_store_is_never_read(string endpoint)
    {
        var other = Guid.NewGuid();
        var url = Url(endpoint, _repo.Add(other, "active", name: "Kiracı Hesabı"));

        var response = await SendAsync(url, Token(_tenant, LookupKey), other);

        await AssertRefusedAsync(response);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AC_AR_15_a_call_without_a_tenant_header_is_400_and_the_store_is_never_read(string endpoint)
    {
        var url = Url(endpoint, _repo.Add(_tenant, "active", name: "Kiracı Hesabı"));

        var response = await SendAsync(url, Token(_tenant, LookupKey), tenantHeader: null);

        await AssertRefusedAsync(response);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AC_AR_15_K11_a_token_without_tenant_id_is_400_even_with_the_header_and_the_store_is_never_read(string endpoint)
    {
        var url = Url(endpoint, _repo.Add(_tenant, "active", name: "Kiracı Hesabı"));

        var response = await SendAsync(url, Token(tenant: null, LookupKey), _tenant);

        await AssertRefusedAsync(response);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AC_AR_15_K11_a_token_whose_tenant_id_is_not_a_guid_is_400(string endpoint)
    {
        var url = Url(endpoint, _repo.Add(_tenant, "active", name: "Kiracı Hesabı"));

        var response = await SendAsync(url, Token(tenant: null, LookupKey, rawTenantClaim: "not-a-guid"), _tenant);

        await AssertRefusedAsync(response);
    }

    /// <summary>K13-F1: a <c>tenant_id</c> of <see cref="Guid.Empty"/> parses as a GUID but names no tenant. The K-11
    /// filter itself must refuse it (its problem body, not the handler's later "no tenant" 400), with no header and with
    /// an <c>X-Tenant-Id</c> that is also <see cref="Guid.Empty"/> (the shared middleware sees no mismatch there and
    /// would hand the empty tenant to the handler).</summary>
    [Theory]
    [MemberData(nameof(EmptyTenantCases))]
    public async Task AC_AR_15_K11_a_token_whose_tenant_id_is_guid_empty_is_400_and_the_store_is_never_read(
        string endpoint, bool emptyHeader)
    {
        var url = Url(endpoint, _repo.Add(_tenant, "active", name: "Kiracı Hesabı"));

        var response = await SendAsync(
            url, Token(tenant: null, LookupKey, rawTenantClaim: Guid.Empty.ToString()), emptyHeader ? Guid.Empty : null);

        await AssertRefusedAsync(response);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(body.RootElement.TryGetProperty("data", out _));
        Assert.Equal("Tenant token required", body.RootElement.GetProperty("title").GetString());
    }

    public static TheoryData<string, bool> EmptyTenantCases() => new()
    {
        { "u1", false },
        { "u1", true },
        { "u3", false },
        { "u3", true }
    };

    /// <summary>K-11 placement, read from the running host: the filter's metadata sits on U-1 and U-3 and on no other
    /// endpoint; it is not a global MVC filter. A rejection moved into the shared middleware (attribute removed) or the
    /// filter lifted to the controller / MvcOptions both turn this red.</summary>
    [Fact]
    public void AC_AR_15_K11_filter_is_on_u1_and_u3_only_and_never_global()
    {
        _factory.CreateClient().Dispose(); // starts the host so the endpoint data source is built

        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;
        var actions = endpoints
            .Select(e => (Endpoint: e, Action: e.Metadata.GetMetadata<ControllerActionDescriptor>()))
            .Where(x => x.Action is not null)
            .ToList();
        Assert.True(actions.Count > 50, $"only {actions.Count} controller endpoints were found; the measurement is vacuous");

        var carrying = actions
            .Where(x => x.Endpoint.Metadata.GetOrderedMetadata<RequireTokenTenantAttribute>().Count > 0
                        || x.Action!.FilterDescriptors.Any(f => IsK11(f.Filter)))
            .Select(x => $"{x.Action!.ControllerTypeInfo.FullName}.{x.Action.ActionName}")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[]
            {
                "Diten.CrmService.Api.Controllers.CRM.AccountController.Lookup",
                "Diten.CrmService.Api.Controllers.CRM.AccountController.LookupValidation"
            },
            carrying);

        var global = _factory.Services.GetRequiredService<IOptions<MvcOptions>>().Value.Filters;
        Assert.DoesNotContain(global, IsK11);
    }

    /// <summary>The filter reads the claim the shared tenant middleware reads; the middleware source is the measure.</summary>
    [Fact]
    public void AC_AR_15_K11_filter_reads_the_same_tenant_claim_as_the_shared_middleware()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(), "services", "Diten.CrmService", "src", "Diten.CrmService.Infrastructure", "Middleware",
            "TenantResolutionMiddleware.cs"));

        var claim = Assert.Single(Regex.Matches(source, "FindFirst\\(\"(?<claim>[^\"]+)\"\\)")).Groups["claim"].Value;

        Assert.Equal(RequireTokenTenantAttribute.TenantClaimType, claim);
    }

    // ---------- AC-AR-3 / AC-AR-7a on the wire ----------

    [Fact]
    public async Task AC_AR_3_u1_404_bodies_are_byte_identical_on_the_wire()
    {
        var foreign = _repo.Add(Guid.NewGuid(), "active");
        var deleted = _repo.Add(_tenant, "active", deleted: true);
        var token = Token(_tenant, LookupKey);

        var other = await SendAsync($"/api/crm/accounts/{foreign.Id}/lookup-validation", token, _tenant);
        var gone = await SendAsync($"/api/crm/accounts/{deleted.Id}/lookup-validation", token, _tenant);
        var missing = await SendAsync($"/api/crm/accounts/{Guid.NewGuid()}/lookup-validation", token, _tenant);

        Assert.Equal(HttpStatusCode.NotFound, other.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var body = await missing.Content.ReadAsByteArrayAsync();
        Assert.Equal(body, await other.Content.ReadAsByteArrayAsync());
        Assert.Equal(body, await gone.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AC_AR_7a_store_failure_is_503_on_the_wire(string endpoint)
    {
        var url = Url(endpoint, _repo.Add(_tenant, "active", name: "Depo Hesabı"));
        _repo.Failure = new TimeoutException("server selection timed out");

        var response = await SendAsync(url, Token(_tenant, LookupKey), _tenant);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("Depo Hesabı", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    // ---------- helpers ----------

    private async Task AssertRefusedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("Kiracı Hesabı", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(0, _repo.GetByIdCalls);
        Assert.Equal(0, _repo.SearchCalls);
    }

    private static bool IsK11(IFilterMetadata filter) => filter switch
    {
        RequireTokenTenantAttribute => true,
        TypeFilterAttribute type => type.ImplementationType == typeof(RequireTokenTenantAttribute),
        ServiceFilterAttribute service => service.ServiceType == typeof(RequireTokenTenantAttribute),
        _ => false
    };

    private static string Url(string endpoint, Domain.Entities.Account account) => endpoint == "u1"
        ? $"/api/crm/accounts/{account.Id}/lookup-validation"
        : $"/api/crm/accounts/lookup?search={Uri.EscapeDataString(account.AccountName)}";

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

    private string Token(Guid? tenant, params string[] permissions) => Token(tenant, permissions, rawTenantClaim: null);

    private string Token(Guid? tenant, string permission, string rawTenantClaim) => Token(tenant, [permission], rawTenantClaim);

    private string Token(Guid? tenant, string[] permissions, string? rawTenantClaim)
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

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "services", "Diten.CrmService")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("The repository root was not found above the test binaries.");
    }
}

/// <summary>Hosts the real CRM Api (the central-audit factory's pattern: settings as process variables while the host is
/// built, a test-owned mongod because the host creates its indexes at registration). The account repository is the
/// recording fake the current test sets.</summary>
public sealed class AccountLookupApiFactory : WebApplicationFactory<Program>
{
    public const string Issuer = "diten-auth-service";
    public const string Audience = "diten-erp";

    private static readonly object EnvironmentGate = new();
    private readonly DisposableCrmMongod _mongod = new();

    public string Secret { get; } = "crm-account-lookup-tests-only-signing-key-" + Guid.NewGuid().ToString("N");

    public IAccountRepository Repository { get; set; } = new LookupFakeAccountRepository();

    private Dictionary<string, string?> Settings => new(StringComparer.Ordinal)
    {
        ["ASPNETCORE_ENVIRONMENT"] = "Testing",
        ["Mongo__ConnectionString"] = _mongod.ConnectionString,
        ["Mongo__DatabaseName"] = "crm_account_lookup_api_tests",
        ["JwtSettings__Secret"] = Secret,
        ["JwtSettings__Issuer"] = Issuer,
        ["JwtSettings__Audience"] = Audience,
        ["CentralAudit__DispatcherEnabled"] = "false",
        ["PlatformRegistration__BaseUrl"] = string.Empty,
        ["PlatformRegistration__InternalApiKey"] = string.Empty
    };

    protected override IHost CreateHost(IHostBuilder builder)
    {
        lock (EnvironmentGate)
        {
            var settings = Settings;
            var previous = settings.Keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
            foreach (var (key, value) in settings)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            try
            {
                return base.CreateHost(builder);
            }
            finally
            {
                foreach (var (key, value) in previous)
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
            services.AddScoped<IAccountRepository>(_ => Repository));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _mongod.Dispose();
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _mongod.Dispose();
    }
}
