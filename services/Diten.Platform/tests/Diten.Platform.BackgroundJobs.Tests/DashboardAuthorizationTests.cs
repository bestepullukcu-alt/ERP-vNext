using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Net;
using System.Text;
using Diten.Platform.API.Security;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Diten.Platform.Infrastructure.BackgroundJobs;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit;

namespace Diten.Platform.BackgroundJobs.Tests;

public sealed class DashboardAuthorizationTests
{
    private const string Email = "dashboard.probe@bl521.test";

    [Fact]
    public async Task Anonymous_user_is_rejected()
    {
        Assert.False(await PlatformActorDashboardAuthorization.IsAuthorizedAsync(new DefaultHttpContext()));
    }

    /// <summary>
    /// BL-521 — the dashboard runs every tenant's jobs: a platform administrator only. FIX2 item 4 — "platform
    /// administrator" is the RECORD's answer (the PlatformAdminOnly decision), not the token's: a platform_admin token whose
    /// record says PartnerAdmin (demoted), is suspended, deleted or missing does not open it.
    /// </summary>
    [Theory]
    [InlineData("platform_admin", "PlatformAdmin", "active", true)]
    [InlineData("partner_admin", "PartnerAdmin", "active", false)]
    [InlineData("tenant_user", "PlatformAdmin", "active", false)]
    [InlineData("platform_admin", "PartnerAdmin", "active", false)] // demoted, old token
    [InlineData("platform_admin", "PlatformAdmin", "suspended", false)]
    [InlineData("platform_admin", "PlatformAdmin", "deleted", false)]
    [InlineData("platform_admin", null, "missing", false)]
    public async Task Only_a_platform_administrator_its_record_confirms_opens_the_job_dashboard(
        string actorType, string? recordType, string state, bool expected)
    {
        using var request = RequestScope(recordType, state);
        var http = request.Http(actorType);

        Assert.Equal(expected, await PlatformActorDashboardAuthorization.IsAuthorizedAsync(http));
    }

    /// <summary>The production dashboard filter itself, over the same record decision.</summary>
    [Theory]
    [InlineData("PlatformAdmin", true)]
    [InlineData("PartnerAdmin", false)] // demoted administrator still holding its platform_admin token
    public async Task The_production_dashboard_filter_admits_only_what_the_record_confirms(string recordType, bool expected)
    {
        using var request = RequestScope(recordType, "active");
        var http = request.Http("platform_admin");
        var context = new AspNetCoreDashboardContext(new Mock<JobStorage>().Object, BackgroundJobApplicationBuilderExtensions.CreateDashboardOptions(), http);

        Assert.Equal(expected, await new PlatformActorHangfireAuthorizationFilter().AuthorizeAsync(context));
    }

    /// <summary>
    /// BL-521 FIX3 G4 — the filter's two TOKEN arms: a request that arrives with no principal yet (the dashboard is mapped
    /// outside MVC) is authenticated by the filter itself — first through the JwtBearer scheme, and when that fails by
    /// validating the bearer token against JwtSettings by hand — and EACH arm then asks the record. Before this, only the
    /// first (already-authenticated) arm was measured: a token-only decision in either of the other two stayed green.
    /// </summary>
    [Theory]
    [InlineData("scheme", "PlatformAdmin", true)]
    [InlineData("scheme", "PartnerAdmin", false)] // demoted, still holding a valid platform_admin token
    [InlineData("manual", "PlatformAdmin", true)]
    [InlineData("manual", "PartnerAdmin", false)] // demoted, still holding a valid platform_admin token
    public async Task Each_token_arm_of_the_dashboard_filter_admits_only_what_the_record_confirms(string arm, string recordType, bool expected)
    {
        using var request = TokenArmScope(recordType, schemeValidates: arm == "scheme");
        var http = request.AnonymousWithBearer(PlatformAdminToken());
        var context = new AspNetCoreDashboardContext(new Mock<JobStorage>().Object, BackgroundJobApplicationBuilderExtensions.CreateDashboardOptions(), http);

        Assert.Equal(expected, await new PlatformActorHangfireAuthorizationFilter().AuthorizeAsync(context));
        Assert.True(http.User.Identity?.IsAuthenticated == true, "the arm under test did not authenticate the token");
    }

    /// <summary>
    /// FIX2 item 4 — the dashboard's ONLY door is the async record-reading filter. Hangfire runs both lists (and lets every
    /// request in when both are empty): a token-only synchronous filter added back, or an emptied async list, reopens it.
    /// </summary>
    [Fact]
    public void The_dashboard_is_guarded_only_by_the_async_record_filter()
    {
        var options = BackgroundJobApplicationBuilderExtensions.CreateDashboardOptions();

        Assert.Empty(options.Authorization);
        Assert.IsType<PlatformActorHangfireAuthorizationFilter>(Assert.Single(options.AsyncAuthorization));
    }

    [Fact]
    public void Development_config_true_localhost_allows_anonymous_bypass()
    {
        var httpContext = CreateHttpContext(
            environmentName: Environments.Development,
            allowAnonymousInDevelopment: true,
            remoteIpAddress: IPAddress.Loopback);

        Assert.True(PlatformActorDashboardAuthorization.IsDevelopmentAnonymousBypassAllowed(httpContext));
    }

    [Fact]
    public void Development_config_false_rejects_anonymous_bypass()
    {
        var httpContext = CreateHttpContext(
            environmentName: Environments.Development,
            allowAnonymousInDevelopment: false,
            remoteIpAddress: IPAddress.Loopback);

        Assert.False(PlatformActorDashboardAuthorization.IsDevelopmentAnonymousBypassAllowed(httpContext));
    }

    [Fact]
    public void Production_config_true_rejects_anonymous_bypass()
    {
        var httpContext = CreateHttpContext(
            environmentName: Environments.Production,
            allowAnonymousInDevelopment: true,
            remoteIpAddress: IPAddress.Loopback);

        Assert.False(PlatformActorDashboardAuthorization.IsDevelopmentAnonymousBypassAllowed(httpContext));
    }

    [Fact]
    public void Development_non_localhost_rejects_anonymous_bypass()
    {
        var httpContext = CreateHttpContext(
            environmentName: Environments.Development,
            allowAnonymousInDevelopment: true,
            remoteIpAddress: IPAddress.Parse("203.0.113.10"));

        Assert.False(PlatformActorDashboardAuthorization.IsDevelopmentAnonymousBypassAllowed(httpContext));
    }

    [Fact]
    public async Task Non_platform_actor_token_rejects_even_when_development_bypass_is_enabled()
    {
        var identity = new ClaimsIdentity(
            [new Claim("actor_type", "tenant_user")],
            authenticationType: "Test");

        var httpContext = CreateHttpContext(
            environmentName: Environments.Development,
            allowAnonymousInDevelopment: true,
            remoteIpAddress: IPAddress.Loopback,
            user: new ClaimsPrincipal(identity));

        Assert.False(await PlatformActorDashboardAuthorization.IsAuthorizedAsync(httpContext));
        Assert.False(PlatformActorDashboardAuthorization.IsDevelopmentAnonymousBypassAllowed(httpContext));
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One request over the PRODUCTION scope registration (PlatformActorTenantScopeRegistration) and a repository that
    /// returns the given record — the same wiring the PlatformAdminOnly policy reads through.
    /// </summary>
    private static RequestFixture RequestScope(string? recordType, string state)
    {
        var administrators = new Mock<IPlatformAdministratorRepository>();
        if (recordType is not null)
        {
            administrators.Setup(x => x.GetByNormalizedEmailAsync(Email, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PlatformAdministrator
                {
                    Email = Email, NormalizedEmail = Email, ActorType = Enum.Parse<ActorType>(recordType),
                    AllowedTenantIds = [Guid.NewGuid()],
                    Status = state == "suspended" ? AdministratorStatus.Suspended : AdministratorStatus.Active,
                    IsDeleted = state == "deleted"
                });
        }

        var services = new ServiceCollection().AddLogging().AddPlatformActorTenantScope().AddSingleton(administrators.Object);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        return new RequestFixture(services.BuildServiceProvider());
    }

    private const string TokenIssuer = "bl521-fix3-dashboard-issuer";
    private const string TokenAudience = "bl521-fix3-dashboard-audience";
    private const string TokenSecret = "BL-521 FIX3 dashboard token arms, test only signing key 0123456789";

    private static string PlatformAdminToken() =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
            TokenIssuer, TokenAudience,
            [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()), new Claim(JwtRegisteredClaimNames.Email, Email), new Claim("actor_type", "platform_admin")],
            DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(30),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TokenSecret)), SecurityAlgorithms.HmacSha256)));

    /// <summary>
    /// One ANONYMOUS request over the production scope registration and a repository answering the given record.
    /// <paramref name="schemeValidates"/>: the JwtBearer scheme accepts the token (the AuthenticateAsync arm); otherwise
    /// the scheme has no keys and fails, and the filter validates the token by hand from JwtSettings (the manual arm).
    /// </summary>
    private static TokenArmFixture TokenArmScope(string recordType, bool schemeValidates)
    {
        var administrators = new Mock<IPlatformAdministratorRepository>();
        administrators.Setup(x => x.GetByNormalizedEmailAsync(Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlatformAdministrator
            {
                Email = Email, NormalizedEmail = Email, ActorType = Enum.Parse<ActorType>(recordType),
                AllowedTenantIds = [Guid.NewGuid()], Status = AdministratorStatus.Active
            });

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:Issuer"] = TokenIssuer,
            ["JwtSettings:Audience"] = TokenAudience,
            ["JwtSettings:Secret"] = TokenSecret
        }).Build();

        var services = new ServiceCollection().AddLogging().AddPlatformActorTenantScope().AddSingleton(administrators.Object);
        services.AddSingleton<IConfiguration>(configuration);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            if (schemeValidates)
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = TokenIssuer,
                    ValidAudience = TokenAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TokenSecret))
                };
            }
        });
        return new TokenArmFixture(services.BuildServiceProvider());
    }

    private sealed class TokenArmFixture(ServiceProvider root) : IDisposable
    {
        private readonly IServiceScope _scope = root.CreateScope();

        public HttpContext AnonymousWithBearer(string token)
        {
            var http = new DefaultHttpContext { RequestServices = _scope.ServiceProvider };
            http.Request.Headers.Authorization = "Bearer " + token;
            _scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = http;
            return http;
        }

        public void Dispose()
        {
            _scope.Dispose();
            root.Dispose();
        }
    }

    private sealed class RequestFixture(ServiceProvider root) : IDisposable
    {
        private readonly IServiceScope _scope = root.CreateScope();

        public HttpContext Http(string actorType)
        {
            var http = new DefaultHttpContext
            {
                RequestServices = _scope.ServiceProvider,
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("actor_type", actorType), new Claim(JwtRegisteredClaimNames.Email, Email)], "Bearer"))
            };
            _scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = http;
            return http;
        }

        public void Dispose()
        {
            _scope.Dispose();
            root.Dispose();
        }
    }

    private static DefaultHttpContext CreateHttpContext(
        string environmentName,
        bool allowAnonymousInDevelopment,
        IPAddress remoteIpAddress,
        ClaimsPrincipal? user = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackgroundJobs:DashboardAllowAnonymousInDevelopment"] = allowAnonymousInDevelopment.ToString()
            })
            .Build();

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddSingleton<IHostEnvironment>(new TestHostEnvironment(environmentName))
            .BuildServiceProvider();

        return new DefaultHttpContext
        {
            RequestServices = services,
            User = user ?? new ClaimsPrincipal(),
            Connection =
            {
                RemoteIpAddress = remoteIpAddress
            }
        };
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Diten.Platform.BackgroundJobs.Tests";

        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
