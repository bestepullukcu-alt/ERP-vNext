using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Diten.Platform.API.Controllers.Test;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Infrastructure.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class AuthorizationProbeControllerIntegrationTests
{
    private static readonly Guid TenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task Module_probe_without_token_returns_unauthorized()
    {
        using var server = CreateServer();

        var response = await server.CreateClient().GetAsync("/api/test/authorization-probe/module-hr");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Platform_admin_can_access_module_probe_without_entitlement_check()
    {
        var checker = new Mock<IEntitlementChecker>();
        using var server = CreateServer(checker);
        var client = CreateAuthenticatedClient(server, actorType: "platform_admin");

        var response = await client.GetAsync("/api/test/authorization-probe/module-hr");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        checker.Verify(
            x => x.IsModuleEntitledAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Platform_admin_can_access_feature_probe_without_entitlement_check()
    {
        var checker = new Mock<IEntitlementChecker>();
        using var server = CreateServer(checker);
        var client = CreateAuthenticatedClient(server, actorType: "platform_admin");

        var response = await client.GetAsync("/api/test/authorization-probe/feature-advanced-reporting");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        checker.Verify(
            x => x.IsFeatureEnabledAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Tenant_user_with_allowed_module_can_access_module_probe()
    {
        var checker = CreateChecker(moduleAllowed: true);
        using var server = CreateServer(checker);
        var client = CreateAuthenticatedClient(server, actorType: "tenant_user", tenantId: TenantId);

        var response = await client.GetAsync("/api/test/authorization-probe/module-hr");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_user_with_denied_module_gets_forbidden_from_module_probe()
    {
        var checker = CreateChecker(moduleAllowed: false);
        using var server = CreateServer(checker);
        var client = CreateAuthenticatedClient(server, actorType: "tenant_user", tenantId: TenantId);

        var response = await client.GetAsync("/api/test/authorization-probe/module-hr");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_user_with_allowed_feature_can_access_feature_probe()
    {
        var checker = CreateChecker(featureAllowed: true);
        using var server = CreateServer(checker);
        var client = CreateAuthenticatedClient(server, actorType: "tenant_user", tenantId: TenantId);

        var response = await client.GetAsync("/api/test/authorization-probe/feature-advanced-reporting");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_user_with_denied_feature_gets_forbidden_from_feature_probe()
    {
        var checker = CreateChecker(featureAllowed: false);
        using var server = CreateServer(checker);
        var client = CreateAuthenticatedClient(server, actorType: "tenant_user", tenantId: TenantId);

        var response = await client.GetAsync("/api/test/authorization-probe/feature-advanced-reporting");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_user_without_tenant_id_gets_forbidden()
    {
        using var server = CreateServer();
        var client = CreateAuthenticatedClient(server, actorType: "tenant_user");

        var response = await client.GetAsync("/api/test/authorization-probe/module-hr");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Partner_admin_gets_forbidden()
    {
        using var server = CreateServer();
        var client = CreateAuthenticatedClient(server, actorType: "partner_admin", tenantId: TenantId);

        var response = await client.GetAsync("/api/test/authorization-probe/module-hr");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Tenant_user_with_allowed_module_and_feature_can_access_combined_probe()
    {
        var checker = CreateChecker(moduleAllowed: true, featureAllowed: true);
        using var server = CreateServer(checker);
        var client = CreateAuthenticatedClient(server, actorType: "tenant_user", tenantId: TenantId);

        var response = await client.GetAsync("/api/test/authorization-probe/module-and-feature");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static TestServer CreateServer(Mock<IEntitlementChecker>? checker = null)
    {
        checker ??= CreateChecker(moduleAllowed: false, featureAllowed: false);

        var builder = new WebHostBuilder()
            .UseEnvironment("Test")
            .ConfigureServices(services =>
            {
                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                        TestAuthenticationHandler.SchemeName,
                        _ => { });

                services.AddAuthorization();
                services.AddSingleton<IAuthorizationPolicyProvider, EntitlementAuthorizationPolicyProvider>();
                services.AddScoped<IAuthorizationHandler, TenantModuleAuthorizationHandler>();
                services.AddScoped<IAuthorizationHandler, TenantFeatureAuthorizationHandler>();
                services.AddHttpContextAccessor();
                services.AddScoped<IDataScopeResolver, NoOpDataScopeResolver>();
                services.AddScoped<ITenantAuthorizationContext, JwtTenantAuthorizationContext>();
                services.AddSingleton<IEntitlementAuditSink, NullEntitlementAuditSink>();
                services.AddSingleton(checker.Object);
                services
                    .AddControllers()
                    .AddApplicationPart(typeof(AuthorizationProbeController).Assembly);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });

        return new TestServer(builder);
    }

    private static HttpClient CreateAuthenticatedClient(
        TestServer server,
        string actorType,
        Guid? tenantId = null)
    {
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestAuthenticationHandler.SchemeName);
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.ActorTypeHeader, actorType);
        if (tenantId.HasValue)
        {
            client.DefaultRequestHeaders.Add(TestAuthenticationHandler.TenantIdHeader, tenantId.Value.ToString());
        }

        return client;
    }

    private static Mock<IEntitlementChecker> CreateChecker(
        bool moduleAllowed = false,
        bool featureAllowed = false)
    {
        var checker = new Mock<IEntitlementChecker>();
        checker
            .Setup(x => x.IsModuleEntitledAsync(TenantId, "HR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(moduleAllowed
                ? EntitlementCheckResult.Allowed(EntitlementKind.Module, "HR")
                : EntitlementCheckResult.Denied(
                    EntitlementKind.Module,
                    "HR",
                    EntitlementDenyReason.ModuleNotEntitled));
        checker
            .Setup(x => x.IsFeatureEnabledAsync(TenantId, "ADVANCED_REPORTING", It.IsAny<CancellationToken>()))
            .ReturnsAsync(featureAllowed
                ? EntitlementCheckResult.Allowed(EntitlementKind.Feature, "ADVANCED_REPORTING")
                : EntitlementCheckResult.Denied(
                    EntitlementKind.Feature,
                    "ADVANCED_REPORTING",
                    EntitlementDenyReason.FeatureNotEnabled));
        return checker;
    }

    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "Test";
        public const string ActorTypeHeader = "X-Test-Actor-Type";
        public const string TenantIdHeader = "X-Test-Tenant-Id";

        public TestAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey(ActorTypeHeader))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new List<Claim>
            {
                new("actor_type", Request.Headers[ActorTypeHeader].ToString())
            };

            if (Request.Headers.TryGetValue(TenantIdHeader, out var tenantId))
            {
                claims.Add(new Claim("tenant_id", tenantId.ToString()));
            }

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
