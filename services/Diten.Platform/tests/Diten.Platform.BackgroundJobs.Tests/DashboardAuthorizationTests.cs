using System.Security.Claims;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Diten.Platform.Infrastructure.BackgroundJobs;
using Xunit;

namespace Diten.Platform.BackgroundJobs.Tests;

public sealed class DashboardAuthorizationTests
{
    [Fact]
    public void Anonymous_user_is_rejected()
    {
        Assert.False(PlatformActorDashboardAuthorization.IsAuthorized(new ClaimsPrincipal()));
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
    public void Non_platform_actor_is_rejected()
    {
        var identity = new ClaimsIdentity(
            [new Claim("actor_type", "tenant_user")],
            authenticationType: "Test");

        Assert.False(PlatformActorDashboardAuthorization.IsAuthorized(new ClaimsPrincipal(identity)));
    }

    [Fact]
    public void Non_platform_actor_token_rejects_even_when_development_bypass_is_enabled()
    {
        var identity = new ClaimsIdentity(
            [new Claim("actor_type", "tenant_user")],
            authenticationType: "Test");

        var httpContext = CreateHttpContext(
            environmentName: Environments.Development,
            allowAnonymousInDevelopment: true,
            remoteIpAddress: IPAddress.Loopback,
            user: new ClaimsPrincipal(identity));

        Assert.False(PlatformActorDashboardAuthorization.IsAuthorized(httpContext.User));
        Assert.False(PlatformActorDashboardAuthorization.IsDevelopmentAnonymousBypassAllowed(httpContext));
    }

    [Theory]
    [InlineData("platform_admin")]
    [InlineData("partner_admin")]
    public void Platform_actor_is_allowed(string actorType)
    {
        var identity = new ClaimsIdentity(
            [new Claim("actor_type", actorType)],
            authenticationType: "Test");

        Assert.True(PlatformActorDashboardAuthorization.IsAuthorized(new ClaimsPrincipal(identity)));
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
