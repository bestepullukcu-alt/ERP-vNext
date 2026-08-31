using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Diten.ApiGateway.Authentication;
using Diten.ApiGateway.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Diten.ApiGateway.Tests;

/*
 * PROBE / GUARD — a token the gateway REFUSED must not be able to speak.
 *
 * Unlike TenantContradictionGuardTests, which constructs context.User directly, every test here goes through the
 * REAL pipeline segment from Program.cs:135-149 (cookie->Authorization promotion, UseAuthentication with the real
 * GatewayJwtAuthenticationHandler, then TenantResolutionMiddleware). That is the only way the fallback path that
 * parses the raw token is reachable at all.
 */
public sealed class RejectedTokenClaimRevivalGuardTests
{
    private const string Secret = "gateway-guard-test-secret-key-that-is-long-enough-0123456789";
    private const string ForgedSecret = "an-entirely-different-key-nobody-trusts-0123456789-abcdefgh";
    private const string Issuer = "diten-test-issuer";
    private const string Audience = "diten-test-audience";

    private static int _authenticateCalls;

    // ---- MEASUREMENT 1: does the raw-token fallback resurrect a REFUSED token's actor_type? ----
    [Fact]
    public async Task Forged_token_claiming_platform_admin_cannot_open_an_admin_path()
    {
        using var host = await BuildHostAsync();
        var client = host.GetTestClient();
        client.BaseAddress = new Uri("http://admin.diten.com");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/tenants");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            Forge("platform_admin", tenantId: null, signingSecret: ForgedSecret));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain("terminal-handler-ran", body);
    }

    // ---- MEASUREMENT 2: does the raw-token fallback resurrect a REFUSED token's tenant_id? ----
    [Fact]
    public async Task Forged_token_tenant_id_is_not_forwarded_downstream()
    {
        var tenant = Guid.NewGuid();
        using var host = await BuildHostAsync();
        var client = host.GetTestClient();
        client.BaseAddress = new Uri("http://app.diten.com");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/users");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            Forge("tenant_user", tenant, ForgedSecret));

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(tenant.ToString(), body);
    }

    // ---- CONTROL: a genuinely signed token still works, so the guard above is not vacuous. ----
    [Fact]
    public async Task Genuine_platform_admin_token_still_opens_an_admin_path()
    {
        using var host = await BuildHostAsync();
        var client = host.GetTestClient();
        client.BaseAddress = new Uri("http://admin.diten.com");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/tenants");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            Forge("platform_admin", tenantId: null, signingSecret: Secret));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("terminal-handler-ran", await response.Content.ReadAsStringAsync());
    }

    // ---- MEASUREMENT 3: is EnsureAuthenticatedUserAsync dead? ----
    // A COOKIE-ONLY genuine token must already be an authenticated principal when TenantResolutionMiddleware
    // starts, and the handler must have been invoked exactly ONCE for the whole request — a second
    // context.AuthenticateAsync("Bearer") returns the cached result and cannot produce a new one.
    [Fact]
    public async Task Cookie_only_token_is_already_authenticated_and_the_handler_runs_once()
    {
        Interlocked.Exchange(ref _authenticateCalls, 0);

        using var host = await BuildHostAsync();
        var client = host.GetTestClient();
        client.BaseAddress = new Uri("http://app.diten.com");

        var tenant = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.Add("Cookie", $"access_token={Forge("tenant_user", tenant, Secret)}");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal("authenticated-before-tenant-middleware", body);
        Assert.Equal(1, Volatile.Read(ref _authenticateCalls));
    }

    // ---- MEASUREMENT 4: a REFUSED token cannot be revived by a second AuthenticateAsync either. ----
    [Fact]
    public async Task Refused_token_stays_refused_across_a_second_authenticate_call()
    {
        Interlocked.Exchange(ref _authenticateCalls, 0);

        using var host = await BuildHostAsync();
        var client = host.GetTestClient();
        client.BaseAddress = new Uri("http://app.diten.com");

        var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            Forge("platform_admin", Guid.NewGuid(), ForgedSecret));

        var response = await client.SendAsync(request);

        Assert.Equal("anonymous-before-tenant-middleware|reauth:NoNewPrincipal", await response.Content.ReadAsStringAsync());
        Assert.Equal(1, Volatile.Read(ref _authenticateCalls));
    }

    private static string Forge(string actorType, Guid? tenantId, string signingSecret)
    {
        var claims = new List<Claim> { new("actor_type", actorType), new("sub", Guid.NewGuid().ToString()) };
        if (tenantId.HasValue)
        {
            claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingSecret));
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<IHost> BuildHostAsync()
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JwtSettings:Secret"] = Secret,
                    ["JwtSettings:Issuer"] = Issuer,
                    ["JwtSettings:Audience"] = Audience
                }));
                webBuilder.ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(LogLevel.Trace);
                    logging.AddProvider(new HandlerInvocationCountingLoggerProvider());
                });
                webBuilder.ConfigureServices(services =>
                {
                    services.AddAuthentication("Bearer")
                        .AddScheme<AuthenticationSchemeOptions, GatewayJwtAuthenticationHandler>("Bearer", _ => { });
                    services.AddAuthorization();
                });
                webBuilder.Configure(app =>
                {
                    // Program.cs:135-145 — cookie promoted to Authorization BEFORE UseAuthentication.
                    app.Use(async (context, next) =>
                    {
                        var cookieToken = AuthTokenCookies.GetAccessToken(context.Request);
                        if (!context.Request.Headers.ContainsKey("Authorization") && !string.IsNullOrWhiteSpace(cookieToken))
                        {
                            context.Request.Headers.Authorization = $"Bearer {cookieToken}";
                        }

                        await next();
                    });

                    app.UseAuthentication();

                    // The probe sits exactly where TenantResolutionMiddleware sits.
                    app.Use(async (context, next) =>
                    {
                        if (!context.Request.Path.StartsWithSegments("/probe"))
                        {
                            await next();
                            return;
                        }

                        if (context.User.Identity?.IsAuthenticated == true)
                        {
                            await context.Response.WriteAsync("authenticated-before-tenant-middleware");
                            return;
                        }

                        var again = await context.AuthenticateAsync("Bearer");
                        var outcome = again.Succeeded && again.Principal is not null ? "NewPrincipal" : "NoNewPrincipal";
                        await context.Response.WriteAsync($"anonymous-before-tenant-middleware|reauth:{outcome}");
                    });

                    app.UseMiddleware<TenantResolutionMiddleware>();
                    app.Run(context => context.Response.WriteAsync("terminal-handler-ran"));
                });
            })
            .StartAsync();

        return host;
    }

    /// <summary>
    /// Counts how many times the REAL GatewayJwtAuthenticationHandler actually executed: it emits exactly one log
    /// entry per HandleAuthenticateAsync call (Debug on success, Warning on failure). Counting those entries
    /// measures handler invocations without substituting a stand-in handler for the one under test.
    /// </summary>
    private sealed class HandlerInvocationCountingLoggerProvider : ILoggerProvider
    {
        public void Dispose()
        {
        }

        public ILogger CreateLogger(string categoryName)
            => categoryName.Contains(nameof(GatewayJwtAuthenticationHandler), StringComparison.Ordinal)
                ? new CountingLogger()
                : NullLogger.Instance;

        private sealed class CountingLogger : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                // The category also carries framework messages ("AuthenticationScheme: Bearer was successfully
                // authenticated."), so only the handler's OWN two messages are counted — one per invocation.
                var message = formatter(state, exception);
                if (message.StartsWith("Gateway JWT authentication", StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref _authenticateCalls);
                }
            }
        }
    }
}
