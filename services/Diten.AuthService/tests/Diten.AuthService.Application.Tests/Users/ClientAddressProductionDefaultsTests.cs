using System.Net;
using System.Net.Http.Json;
using Diten.AuthService.Application.Common;
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

        public CountingStatus Status { get; } = new();
        public LinkCapture Links { get; } = new();

        public string InternalKey => InternalApiKey ?? throw new InvalidOperationException("a non-Development host has a test-owned internal key");

        protected override void ConfigureTestServices(IServiceCollection services)
        {
            base.ConfigureTestServices(services);
            services.AddSingleton<ILogger<ClientAddressStartupWarning>>(Logs.CreateLogger<ClientAddressStartupWarning>());
            services.AddSingleton<IStartupFilter, SharedPeerStartupFilter>();
            services.AddSingleton<IPlatformAdministratorStatusClient>(Status);
            services.AddSingleton<IPlatformAuthEmailService>(Links);
            services.AddScoped<ITenantLoginSettingsClient, PlainSettings>();
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

    // ── FIX5: the link door — the valid link is never counted, junk never locks it, junk never reaches Platform ──

    [Fact]
    public async Task Junk_links_for_many_addresses_through_one_peer_are_never_one_bucket()
    {
        for (var i = 0; i <= PasswordDoorRateLimiter.DefaultPerClientLimit; i++)
        {
            var junk = await LinkAsync($"nobody{i}.{Guid.NewGuid():N}@defaults.test", "junk");
            Assert.Equal(HttpStatusCode.BadRequest, junk.StatusCode); // never 429: no shared per-client bucket
        }
    }

    [Fact]
    public async Task The_owners_valid_link_gets_through_after_any_amount_of_junk()
    {
        var email = await ProvisionedAdministratorAsync();
        HttpResponseMessage? junk = null;
        for (var i = 0; i <= PasswordDoorRateLimiter.DefaultPerAddressLimit; i++)
        {
            junk = await LinkAsync(email, "junk");
        }

        Assert.Equal((HttpStatusCode)429, junk!.StatusCode); // the junk is stopped …
        Assert.Equal(HttpStatusCode.NoContent, (await LinkAsync(email, _host.Links.LastTokenFor(email), "Fresh!Passw0rd-529")).StatusCode); // … the owner is not
    }

    [Fact]
    public async Task A_wrong_link_never_reaches_platform()
    {
        var email = await ProvisionedAdministratorAsync();
        var before = _host.Status.CallsFor(email);

        Assert.Equal(HttpStatusCode.BadRequest, (await LinkAsync(email, "junk")).StatusCode);
        Assert.Equal(before, _host.Status.CallsFor(email)); // compared first: no Platform call for a wrong link

        Assert.Equal(HttpStatusCode.NoContent, (await LinkAsync(email, _host.Links.LastTokenFor(email), "Fresh!Passw0rd-529")).StatusCode);
        Assert.Equal(before + 1, _host.Status.CallsFor(email)); // the valid link is checked with Platform
    }

    [Theory]
    [InlineData("10.0.1")]
    [InlineData("10.0.0.0/8")]
    [InlineData("gateway.internal")]
    [InlineData("10.0.0.300")]
    public void An_entry_that_is_not_an_address_stops_the_start_naming_it(string entry)
    {
        var error = Assert.Throws<InvalidOperationException>(() => ClientAddressResolver.ParseTrustedProxies(Config(entry)));
        Assert.Contains(entry, error.Message, StringComparison.Ordinal);
        Assert.Contains(ClientAddressResolver.TrustedProxiesKey, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Who_can_be_told_apart_depends_on_the_list_only()
    {
        Assert.False(new ClientAddressResolver(ClientAddressResolver.ParseTrustedProxies(Config())).IdentifiesClients);
        Assert.True(new ClientAddressResolver(ClientAddressResolver.ParseTrustedProxies(Config("10.0.0.5", "::1", "FE80::1"))).IdentifiesClients);
    }

    [Fact]
    public void Development_trusts_loopback_through_its_configuration_not_through_code()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "services", "Diten.AuthService", "src", "Diten.AuthService.Api")))
        {
            dir = dir.Parent;
        }

        var api = Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("repo root"), "services", "Diten.AuthService", "src", "Diten.AuthService.Api");
        var development = new ConfigurationBuilder().AddJsonFile(Path.Combine(api, "appsettings.Development.json"), optional: false).Build();
        var production = new ConfigurationBuilder().AddJsonFile(Path.Combine(api, "appsettings.json"), optional: false).Build();

        Assert.Equal([IPAddress.Parse("127.0.0.1"), IPAddress.IPv6Loopback], ClientAddressResolver.ParseTrustedProxies(development));
        Assert.Empty(ClientAddressResolver.ParseTrustedProxies(production));
    }

    private async Task<HttpResponseMessage> LinkAsync(string email, string token, string newPassword = "Junk!Passw0rd-529")
    {
        using var client = _host.Client();
        return await client.PostAsJsonAsync("api/platform-auth/reset-password", new { email, token, newPassword });
    }

    private async Task<string> ProvisionedAdministratorAsync()
    {
        var email = $"padmin.{Guid.NewGuid():N}@defaults.test";
        using var platform = _host.Client();
        platform.DefaultRequestHeaders.Add("X-Internal-Api-Key", _host.InternalKey);
        var provisioned = await platform.PostAsJsonAsync("api/platform-auth/platform-admins/provision",
            new { email, userName = email.Split('@')[0], displayName = "Defaults Admin", actorType = "platform_admin", roles = new[] { "ReadOnly" }, requirePasswordChange = true });
        Assert.Equal(HttpStatusCode.OK, provisioned.StatusCode);
        return email;
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

    public sealed class CountingStatus : IPlatformAdministratorStatusClient
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _calls = new(StringComparer.OrdinalIgnoreCase);

        public int CallsFor(string email) => _calls.TryGetValue(email, out var n) ? n : 0;

        public Task<bool> IsActiveAsync(string email, CancellationToken ct)
        {
            _calls.AddOrUpdate(email, 1, (_, n) => n + 1);
            return Task.FromResult(true);
        }

        public Task MarkLoginAcceptedAsync(string email, CancellationToken ct) => Task.CompletedTask;
    }

    public sealed class LinkCapture : IPlatformAuthEmailService
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _last = new(StringComparer.OrdinalIgnoreCase);

        public string LastTokenFor(string email) => _last[email];

        public string BuildPlatformPasswordResetUrl(string email, string resetToken) => "http://localhost/platform/reset-password";

        public Task SendPlatformPasswordResetAsync(string email, string resetToken, CancellationToken ct)
        {
            _last[email] = resetToken;
            return Task.CompletedTask;
        }
    }

    private sealed class PlainSettings : ITenantLoginSettingsClient
    {
        public Task<TenantLoginSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct) => Task.FromResult(new TenantLoginSettingsSnapshot(
            tenantId, TwoFactorEnabled: false, MfaRequired: false, EmailLoginEnabled: true, PhoneLoginEnabled: false,
            PasswordMinLength: 8, PasswordRequireUppercase: true, PasswordRequireLowercase: true, PasswordRequireDigit: true,
            PasswordRequireSpecialChar: true, PasswordExpirationDays: null, SessionTimeoutMinutes: 60, RefreshTokenLifetimeDays: 7,
            MaxFailedLoginAttempts: 5, LockoutDurationMinutes: 15));
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
