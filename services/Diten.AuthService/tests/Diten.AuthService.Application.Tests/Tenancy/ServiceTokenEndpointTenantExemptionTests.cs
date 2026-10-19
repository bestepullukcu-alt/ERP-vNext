using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Repositories;
using Diten.AuthService.Infrastructure.Middleware;
using Diten.AuthService.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.AuthService.Application.Tests.Tenancy;

/*
 * MOD-0033-FU02 — THE SERVICE-TOKEN ENDPOINT NEEDS NO TENANT CONTEXT.
 *
 * WHAT WAS WRONG. POST /api/internal/v1/auth/service-tokens/issue is called by a SERVICE: it authenticates with its
 * client id and secret, has no tenant JWT and sends no X-Tenant-Id. The tenant it asks a token for is a field of the
 * JSON body, checked by the handler against an explicit tenant grant. TenantResolutionMiddleware exempted only
 * /internal/*, so outside Development this endpoint answered 400 "Missing Tenant" before the controller ran — and in
 * Development it only worked because the dev-bypass tenant was set on the request.
 *
 * THE RULE NOW. That path prefix — and nothing else under /api/internal — passes without a tenant, and the dev bypass
 * does not stand in for one there. A request that does carry a header or JWT tenant is treated as on any other path.
 *
 * Two layers below: the REAL middleware alone (every branch, both environments), and the REAL Api pipeline over HTTP
 * in a non-Development environment (the middleware is in the pipeline, in front of the real controller and handler).
 */
public sealed class ServiceTokenTenantExemptionMiddlewareTests
{
    private const string TenantHeader = "X-Tenant-Id";
    private const string IssuePath = "/api/internal/v1/auth/service-tokens/issue";
    private static readonly Guid BypassTenant = Guid.Parse("6d0f3b1a-52c4-4e7d-8a90-1b2c3d4e5f60");

    [Theory]
    [InlineData(IssuePath)]
    [InlineData("/api/internal/v1/auth/service-tokens")]
    [InlineData("/API/Internal/V1/Auth/Service-Tokens/Issue")] // routing is case-insensitive, so is the exemption
    public async Task Outside_development_the_service_token_path_passes_without_a_tenant_and_sets_none(string path)
    {
        var (context, tenantContext, handlerRan) = await Run(path, Environments.Production);

        Assert.True(handlerRan(), "the service-token request was refused before the controller");
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.False(tenantContext.IsResolved, "a tenant context was set for a request that carried no tenant");
    }

    [Theory]
    [InlineData("/api/internal")]
    [InlineData("/api/internal/v1")]
    [InlineData("/api/internal/v1/auth")]
    [InlineData("/api/internal/v1/auth/service-tokens-admin/issue")] // a sibling that merely starts with the name
    [InlineData("/api/internal/v1/auth/service-tokensissue")]
    [InlineData("/api/internal/v1/users/service-tokens/issue")]
    [InlineData("/api/v1/auth/service-tokens/issue")]
    [InlineData("/api/users")]
    public async Task The_exemption_is_that_prefix_only_every_other_path_without_a_tenant_is_still_refused(string path)
    {
        var (context, tenantContext, handlerRan) = await Run(path, Environments.Production);

        Assert.False(handlerRan(), "a request without a tenant reached the handler on a path that is not exempt");
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("Missing Tenant", await ReadTitle(context));
        Assert.False(tenantContext.IsResolved);
    }

    [Fact]
    public async Task In_development_the_dev_bypass_does_not_stand_in_for_a_tenant_on_the_service_token_path()
    {
        var (_, tenantContext, handlerRan) = await Run(IssuePath, Environments.Development, devBypass: true);

        Assert.True(handlerRan());
        Assert.False(tenantContext.IsResolved, "the dev-bypass tenant was set on a service-token request");

        // Control: the very same configuration DOES apply the bypass on another path — so the assertion above is
        // about this path, not about a bypass that was never on.
        var (_, otherTenantContext, otherHandlerRan) = await Run("/api/users", Environments.Development, devBypass: true);
        Assert.True(otherHandlerRan());
        Assert.True(otherTenantContext.IsResolved);
        Assert.Equal(BypassTenant, otherTenantContext.TenantId);
    }

    [Fact]
    public async Task A_tenant_the_request_does_carry_is_handled_as_on_any_other_path()
    {
        var tenant = Guid.NewGuid();

        var (_, fromHeader, headerHandlerRan) = await Run(IssuePath, Environments.Production, headerTenant: tenant);
        Assert.True(headerHandlerRan());
        Assert.Equal(tenant, fromHeader.TenantId);

        var (_, fromJwt, jwtHandlerRan) = await Run(IssuePath, Environments.Production, jwtTenant: tenant);
        Assert.True(jwtHandlerRan());
        Assert.Equal(tenant, fromJwt.TenantId);

        // A contradiction is still a malformed request (BL-324), here as everywhere.
        var (context, contradicting, contradictionHandlerRan) =
            await Run(IssuePath, Environments.Production, headerTenant: tenant, jwtTenant: Guid.NewGuid());
        Assert.False(contradictionHandlerRan());
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("Tenant mismatch", await ReadTitle(context));
        Assert.False(contradicting.IsResolved);
    }

    private static async Task<string?> ReadTitle(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement.GetProperty("title").GetString();
    }

    /// <summary>Runs the REAL middleware over one request.</summary>
    private static async Task<(HttpContext Context, TenantContext Tenant, Func<bool> HandlerRan)> Run(
        string path, string environment, bool devBypass = false, Guid? headerTenant = null, Guid? jwtTenant = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        if (jwtTenant.HasValue)
        {
            context.User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim("tenant_id", jwtTenant.Value.ToString())], "test"));
        }

        if (headerTenant.HasValue) context.Request.Headers[TenantHeader] = headerTenant.Value.ToString();

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(devBypass
            ? new Dictionary<string, string?>
            {
                ["TenantResolution:DevBypassEnabled"] = "true",
                ["TenantResolution:DevBypassTenantId"] = BypassTenant.ToString("D")
            }
            : []).Build();
        var ran = false;
        var middleware = new TenantResolutionMiddleware(
            _ =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            NullLogger<TenantResolutionMiddleware>.Instance,
            configuration,
            new StubEnvironment { EnvironmentName = environment });

        var tenantContext = new TenantContext();
        await middleware.InvokeAsync(context, tenantContext);
        return (context, tenantContext, () => ran);
    }

    private sealed class StubEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Diten.AuthService.Application.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

/// <summary>
/// The REAL Api (Program.cs, the whole pipeline: authentication → tenant resolution → authorization → controller →
/// MediatR → handler → repositories) in a NON-Development environment, over HTTP, on a test-owned mongod.
///
/// <para>The host is <see cref="AccountKindAcceptance.AuthTestHost.StartWithMongoDataDirectoryAsync"/>: environment
/// "AcceptanceHostPrep", an empty content root, configuration from test-owned values only (no appsettings file, no
/// user secrets, no dev bypass), data-protection keys in memory. The service client below is a throwaway: a random
/// code and a random secret, written into that disposable database through the production repositories.</para>
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class ServiceTokenEndpointOutsideDevelopmentHttpTests(ServiceTokenEndpointOutsideDevelopmentHttpTests.NonDevelopmentAuthHost fixture)
    : IClassFixture<ServiceTokenEndpointOutsideDevelopmentHttpTests.NonDevelopmentAuthHost>
{
    private const string IssuePath = "api/internal/v1/auth/service-tokens/issue";
    private const string Audience = "TRUSTED_WORKFLOW_CONSUMER";
    private readonly AccountKindAcceptance.AuthTestHost _host = fixture.Host;
    private readonly string _clientCode = fixture.ClientCode;
    private readonly string _secret = fixture.Secret;
    private readonly Guid _grantedTenant = fixture.GrantedTenant;

    /// <summary>One host for the class: started once, with one throwaway service client and one tenant grant.</summary>
    public sealed class NonDevelopmentAuthHost : IAsyncLifetime
    {
        private string _root = null!;

        public AccountKindAcceptance.AuthTestHost Host { get; private set; } = null!;
        public string ClientCode { get; } = "tenant-exemption-test-" + Guid.NewGuid().ToString("N");
        public string Secret { get; } = "test-only-" + Guid.NewGuid().ToString("N");
        public Guid GrantedTenant { get; } = Guid.NewGuid();

        public async Task InitializeAsync()
        {
            _root = Directory.CreateTempSubdirectory("diten-service-token-tenant-").FullName;
            var mongoData = Directory.CreateDirectory(Path.Combine(_root, "mongo")).FullName;
            var contentRoot = Directory.CreateDirectory(Path.Combine(_root, "content")).FullName;
            Host = await AccountKindAcceptance.AuthTestHost.StartWithMongoDataDirectoryAsync(mongoData, contentRoot);

            using var scope = Host.Factory.Services.CreateScope();
            var identityId = ServiceClientIdentityIds.FromClientCode(ClientCode);
            Assert.True(await scope.ServiceProvider.GetRequiredService<IServiceClientIdentityRepository>().CreateIfAbsentAsync(
                new ServiceClientIdentity
                {
                    Id = identityId, ClientCode = ClientCode, ServiceName = "Diten.MDM", AllowedAudience = Audience,
                    ActiveCredentialHash = new ServiceClientCredentialVerifier().Hash(Secret),
                    ActiveCredentialVersion = "test-v1", CreatedBy = nameof(ServiceTokenEndpointOutsideDevelopmentHttpTests)
                }, CancellationToken.None));
            Assert.True(await scope.ServiceProvider.GetRequiredService<IServiceClientTenantGrantRepository>().CreateIfAbsentAsync(
                new ServiceClientTenantGrant
                {
                    TenantId = GrantedTenant, ServiceClientIdentityId = identityId, Audience = Audience, IsEnabled = true,
                    CreatedBy = nameof(ServiceTokenEndpointOutsideDevelopmentHttpTests)
                }, CancellationToken.None));
        }

        public async Task DisposeAsync()
        {
            if (Host is not null) await Host.DisposeAsync();
            try { Directory.Delete(_root, recursive: true); } catch (IOException) { /* best effort, this test's own temp dir */ }
        }
    }

    [Fact]
    public async Task Without_any_tenant_the_request_reaches_the_handler_which_answers_for_the_secret_and_the_body_tenant()
    {
        var environment = _host.Factory.Services.GetRequiredService<IHostEnvironment>();
        Assert.False(environment.IsDevelopment(), "this proof is about a NON-Development environment");
        Assert.False(_host.Factory.Services.GetRequiredService<IConfiguration>().GetValue<bool>("TenantResolution:DevBypassEnabled"));
        using var client = _host.Client(); // no bearer token, no X-Tenant-Id

        // A wrong secret: 401 from the HANDLER (before the fix: 400 "Missing Tenant" from the middleware).
        var wrongSecret = await IssueAsync(client, "test-only-wrong-" + Guid.NewGuid().ToString("N"), _grantedTenant);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongSecret.Status);
        Assert.Contains("Service client authentication failed.", wrongSecret.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Missing Tenant", wrongSecret.Body, StringComparison.Ordinal);

        // The right secret for a tenant the client is NOT granted: 403 from the handler — the tenant it judged is
        // the one in the body.
        var notGranted = await IssueAsync(client, _secret, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Forbidden, notGranted.Status);
        Assert.Contains("not granted for the requested tenant", notGranted.Body, StringComparison.Ordinal);

        // The right secret for the GRANTED tenant gets past authentication and the grant; only this host's missing
        // signing key (no issuer is configured in the prep host) stops it — still an answer of the handler.
        var granted = await IssueAsync(client, _secret, _grantedTenant);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, granted.Status);
        Assert.Contains("Service token issuer is unavailable.", granted.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_tenant_header_does_not_replace_the_tenant_in_the_body()
    {
        // The header names the GRANTED tenant; the body asks for another one. The handler answers for the body.
        using var client = _host.Client(tenantHeader: _grantedTenant);

        var response = await IssueAsync(client, _secret, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Forbidden, response.Status);
        Assert.Contains("not granted for the requested tenant", response.Body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("GET", "api/users")]
    [InlineData("GET", "api/roles")]
    [InlineData("POST", "api/internal/v1/auth/service-tokens-admin/issue")]
    [InlineData("POST", "api/internal/v1/auth")]
    [InlineData("POST", "api/internal/anything")]
    public async Task Another_api_path_without_a_tenant_is_still_refused_with_missing_tenant(string method, string path)
    {
        using var client = _host.Client();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST") request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(body);
        Assert.Equal("Missing Tenant", document.RootElement.GetProperty("title").GetString());
    }

    private async Task<(HttpStatusCode Status, string Body)> IssueAsync(HttpClient client, string secret, Guid tenantId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, IssuePath);
        request.Headers.TryAddWithoutValidation("X-Service-Client-Id", _clientCode);
        request.Headers.TryAddWithoutValidation("X-Service-Client-Secret", secret);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { tenantId, audience = Audience }), Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }
}
