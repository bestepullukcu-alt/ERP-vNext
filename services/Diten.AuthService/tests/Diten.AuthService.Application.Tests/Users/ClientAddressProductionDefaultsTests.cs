using System.Net;
using System.Net.Http.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Infrastructure.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// BL-529 FIX4 — the PRODUCTION registration of the anonymous password doors' client identity, on a non-Development host
/// whose <c>ClientAddress:TrustedProxies</c> is empty (appsettings.json's default). Every request reaches Auth from the same
/// peer (the Web server / the gateway), so the per-client limit must be OFF — never one bucket for everybody — the host warns
/// once, and the per-address limits still hold.
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class ClientAddressProductionDefaultsTests : IClassFixture<ClientAddressProductionDefaultsTests.Host>
{
    private const string SharedPeer = "10.20.30.40"; // the Web server / gateway every request comes through

    private readonly Host _host;

    public ClientAddressProductionDefaultsTests(Host host) => _host = host;

    public sealed class Host : AccountKindAcceptance.AuthTestHost
    {
        public CapturingLoggerProvider Logs { get; } = new();

        protected override string HostEnvironmentName => "Staging";

        protected override void ConfigureTestServices(IServiceCollection services)
        {
            base.ConfigureTestServices(services);
            services.AddSingleton<ILogger<ClientAddressStartupWarning>>(Logs.CreateLogger<ClientAddressStartupWarning>());
            services.AddSingleton<IStartupFilter, SharedPeerStartupFilter>();
        }
    }

    [Fact]
    public void With_no_trusted_proxy_the_resolver_cannot_tell_clients_apart_and_the_host_warned_once()
    {
        Assert.False(_host.Factory.Services.GetRequiredService<ClientAddressResolver>().IdentifiesClients);
        Assert.Single(_host.Logs.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains(ClientAddressResolver.TrustedProxiesKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task One_clients_flood_does_not_lock_another_client_behind_the_same_peer()
    {
        for (var i = 0; i <= PasswordDoorRateLimiter.DefaultPerClientLimit; i++)
        {
            var flood = await ForgotAsync($"flood{i}.{Guid.NewGuid():N}@defaults.test", forwardedFor: "198.51.100.66");
            Assert.Equal(HttpStatusCode.OK, flood.StatusCode); // no shared per-client bucket to exhaust
        }

        var someoneElse = await ForgotAsync($"owner.{Guid.NewGuid():N}@defaults.test", forwardedFor: "198.51.100.77");
        Assert.Equal(HttpStatusCode.OK, someoneElse.StatusCode);
    }

    [Fact]
    public async Task The_per_address_limit_still_holds()
    {
        var email = $"target.{Guid.NewGuid():N}@defaults.test";
        for (var i = 0; i < PasswordDoorRateLimiter.DefaultPerAddressLimit; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await ForgotAsync(email, forwardedFor: null)).StatusCode);
        }

        Assert.Equal((HttpStatusCode)429, (await ForgotAsync(email, forwardedFor: null)).StatusCode);
    }

    [Fact]
    public void The_production_rate_limiter_resolves_from_the_container_with_its_defaults()
    {
        Assert.Equal(PasswordDoorRateLimiter.DefaultWindow, _host.Factory.Services.GetRequiredService<PasswordDoorRateLimiter>().Window);
    }

    // ── the configuration itself, read at registration ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("10.0.0.0/8")]
    [InlineData("gateway.internal")]
    [InlineData("10.0.0.300")]
    public void An_entry_that_is_not_an_address_stops_the_start_naming_it(string entry)
    {
        var error = Assert.Throws<InvalidOperationException>(() => ClientAddressResolver.FromConfiguration(Config(entry), new Env("Production")));
        Assert.Contains(entry, error.Message, StringComparison.Ordinal);
        Assert.Contains(ClientAddressResolver.TrustedProxiesKey, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Who_can_be_told_apart_depends_on_the_list_and_the_environment()
    {
        Assert.False(ClientAddressResolver.FromConfiguration(Config(), new Env("Production")).IdentifiesClients);
        Assert.True(ClientAddressResolver.FromConfiguration(Config(), new Env("Development")).IdentifiesClients);
        Assert.True(ClientAddressResolver.FromConfiguration(Config("10.0.0.5", "::1"), new Env("Production")).IdentifiesClients);
    }

    private async Task<HttpResponseMessage> ForgotAsync(string email, string? forwardedFor)
    {
        using var client = _host.Client();
        if (forwardedFor is not null) client.DefaultRequestHeaders.Add("X-Forwarded-For", forwardedFor);
        return await client.PostAsJsonAsync("api/platform-auth/forgot-password", new { email });
    }

    private static IConfiguration Config(params string[] proxies)
        => new ConfigurationBuilder().AddInMemoryCollection(
            proxies.Select((p, i) => new KeyValuePair<string, string?>($"{ClientAddressResolver.TrustedProxiesKey}:{i}", p))).Build();

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Diten.AuthService.Application.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class SharedPeerStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            => app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    context.Connection.RemoteIpAddress = IPAddress.Parse(SharedPeer);
                    await nextMiddleware();
                });
                next(app);
            };
    }
}
