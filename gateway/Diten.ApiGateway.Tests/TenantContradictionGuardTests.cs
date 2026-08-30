using System.Security.Claims;
using System.Text.Json;
using Diten.ApiGateway.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.ApiGateway.Tests;

/*
 * THE GUARD — TENANT CONTRADICTION IS REFUSED IN THE GATEWAY (BL-324, rule from BL-323 / DCP-004 §7.4).
 *
 * WHAT WAS WRONG. The gateway read THREE tenant signals (JWT, X-Tenant-Id, subdomain) and on a disagreement it
 * logged "JWT tenant wins" and CARRIED ON — for the header AND for the subdomain. A warning is not a refusal:
 * the session kept running, addressed at tenant A's host while operating on tenant B's data. No data crossed a
 * boundary, but the URL lied and the only trace was a log line nobody reads.
 *
 * THE RULE (owner decision 2026-08-30): if the token names a tenant, EVERY other tenant signal on the request
 * must name the SAME one, or the request is refused 400. One rule for all signals — a per-signal carve-out is
 * exactly what gets forgotten when a fourth signal appears.
 *
 * ⚠ NOT A VACUITY CHECK. A middleware that does nothing answers 200, so "not 403 and not 404" would pass against
 * the very defect this guard exists to catch. Every refusal case below asserts the handler DID NOT RUN, and the
 * controls prove a middleware that refused everything would fail too.
 *
 * ⚠ REAL HOST NAMES. `ReadSubdomainTenant` returns null on "localhost" and on any host with fewer than three
 * labels — which is why the subdomain contradiction was never observed in local development. These tests use a
 * real three-label host so the subdomain signal is actually produced.
 */
public sealed class TenantContradictionGuardTests
{
    private const string TenantHeader = "X-Tenant-Id";
    private const string MainPath = "/api/users";
    private const string PersonalizationPath = "/api/personalization/preferences";

    private static string HostFor(Guid tenant) => $"{tenant}.diten.com";
    private const string NeutralHost = "app.diten.com";

    // (a) — token and subdomain agree.
    [Fact]
    public async Task Jwt_and_matching_subdomain_pass_through()
    {
        var tenant = Guid.NewGuid();
        var run = await RunAsync(MainPath, jwtTenant: tenant, host: HostFor(tenant));

        Assert.True(run.HandlerRan, "a self-consistent request was refused");
        Assert.Equal(StatusCodes.Status200OK, run.StatusCode);
        Assert.Equal(tenant, run.ForwardedTenant);
    }

    // (b) — token A, subdomain B.
    [Theory]
    [InlineData(MainPath)]
    [InlineData(PersonalizationPath)]
    public async Task Jwt_contradicted_by_subdomain_is_refused_400(string path)
    {
        var run = await RunAsync(path, jwtTenant: Guid.NewGuid(), host: HostFor(Guid.NewGuid()));

        Assert.False(run.HandlerRan, "a contradicting request reached the handler");
        Assert.Equal(StatusCodes.Status400BadRequest, run.StatusCode);
        Assert.Equal("Tenant mismatch", run.Title);
        Assert.Equal(["subdomain"], run.ConflictingSignals);
    }

    // (c) — token A, header B.
    [Theory]
    [InlineData(MainPath)]
    [InlineData(PersonalizationPath)]
    public async Task Jwt_contradicted_by_header_is_refused_400(string path)
    {
        var run = await RunAsync(path, jwtTenant: Guid.NewGuid(), headerTenant: Guid.NewGuid(), host: NeutralHost);

        Assert.False(run.HandlerRan, "a contradicting request reached the handler");
        Assert.Equal(StatusCodes.Status400BadRequest, run.StatusCode);
        Assert.Equal(["header"], run.ConflictingSignals);
    }

    // (d) — three-way disagreement. The order is FIXED so the caller can compare it.
    [Theory]
    [InlineData(MainPath)]
    [InlineData(PersonalizationPath)]
    public async Task Jwt_contradicted_by_both_signals_lists_them_in_a_fixed_order(string path)
    {
        var run = await RunAsync(
            path,
            jwtTenant: Guid.NewGuid(),
            headerTenant: Guid.NewGuid(),
            host: HostFor(Guid.NewGuid()));

        Assert.False(run.HandlerRan);
        Assert.Equal(StatusCodes.Status400BadRequest, run.StatusCode);
        Assert.Equal(["header", "subdomain"], run.ConflictingSignals);
    }

    /// <summary>
    /// THE REFUSAL IS NEVER 403 OR 404. 403 would be an access verdict on a request that cannot be evaluated;
    /// 404 would pretend the ADDRESS is missing when it is the REQUEST that is malformed.
    /// </summary>
    [Fact]
    public async Task The_refusal_is_never_403_or_404()
    {
        var run = await RunAsync(MainPath, jwtTenant: Guid.NewGuid(), headerTenant: Guid.NewGuid(), host: NeutralHost);

        Assert.False(run.HandlerRan, "the contradiction was not refused at all");
        Assert.NotEqual(StatusCodes.Status403Forbidden, run.StatusCode);
        Assert.NotEqual(StatusCodes.Status404NotFound, run.StatusCode);
    }

    /// <summary>THE CONTROL — without it, a middleware refusing EVERY request would pass every case above.</summary>
    // (e)
    [Theory]
    [InlineData(MainPath)]
    [InlineData(PersonalizationPath)]
    public async Task Control_all_three_signals_agreeing_passes_through(string path)
    {
        var tenant = Guid.NewGuid();
        var run = await RunAsync(path, jwtTenant: tenant, headerTenant: tenant, host: HostFor(tenant));

        Assert.True(run.HandlerRan, "a self-consistent request was refused");
        Assert.Equal(StatusCodes.Status200OK, run.StatusCode);
        Assert.Equal(tenant, run.ForwardedTenant);
    }

    /// <summary>
    /// (f) — WITHOUT A TOKEN THERE IS NO CONTRADICTION. Header and subdomain disagreeing is not a contradiction:
    /// nothing authenticated has named a tenant, so there is no claim to contradict. Today's precedence
    /// (header first) is preserved exactly.
    /// </summary>
    [Fact]
    public async Task No_token_means_no_contradiction_and_the_header_still_wins()
    {
        var headerTenant = Guid.NewGuid();
        var run = await RunAsync(MainPath, jwtTenant: null, headerTenant: headerTenant, host: HostFor(Guid.NewGuid()));

        Assert.True(run.HandlerRan, "a request with no token was refused as a contradiction");
        Assert.Equal(StatusCodes.Status200OK, run.StatusCode);
        Assert.Equal(headerTenant, run.ForwardedTenant);
    }

    /// <summary>
    /// (g) — LOGIN IS UNTOUCHED. A contradiction needs an authenticated tenant, and at login there is no token
    /// yet: a login request carrying a header and a differently-named host still passes. This pins that BL-324
    /// did not break the sign-in path — a refusal there would be a worse defect than the one fixed.
    /// </summary>
    [Fact]
    public async Task Scope_login_carries_no_token_so_conflicting_signals_still_pass()
    {
        var run = await RunAsync(
            "/api/tenant-auth/login",
            jwtTenant: null,
            headerTenant: Guid.NewGuid(),
            host: HostFor(Guid.NewGuid()));

        Assert.True(run.HandlerRan, "BL-324 broke the login path");
        Assert.Equal(StatusCodes.Status200OK, run.StatusCode);
    }

    /// <summary>
    /// (g, second half) — the same for the refresh path, which unlike login DOES carry a token. A refresh whose
    /// token agrees with the host is untouched.
    /// </summary>
    [Fact]
    public async Task Scope_refresh_with_a_consistent_token_is_untouched()
    {
        var tenant = Guid.NewGuid();
        var run = await RunAsync("/api/auth/refresh-token", jwtTenant: tenant, host: HostFor(tenant));

        Assert.True(run.HandlerRan, "BL-324 broke the refresh path");
        Assert.Equal(StatusCodes.Status200OK, run.StatusCode);
    }

    /// <summary>
    /// (h) — LOCALHOST PRODUCES NO SUBDOMAIN SIGNAL. `ReadSubdomainTenant` bails on "localhost", so local
    /// development cannot manufacture a contradiction out of the host. Pinned because it is the reason this
    /// defect survived: the subdomain branch never ran where anyone was looking.
    /// </summary>
    [Fact]
    public async Task Localhost_produces_no_subdomain_signal_and_therefore_no_contradiction()
    {
        var tenant = Guid.NewGuid();
        var run = await RunAsync(MainPath, jwtTenant: tenant, host: "localhost");

        Assert.True(run.HandlerRan, "localhost manufactured a contradiction out of nothing");
        Assert.Equal(StatusCodes.Status200OK, run.StatusCode);
        Assert.Equal(tenant, run.ForwardedTenant);
    }

    /// <summary>
    /// THE MISSING CASE IS STILL "MISSING", NOT "MISMATCH". A conflict cannot be reported as an absent tenant,
    /// which is what a `null` return would have forced the caller to do.
    /// </summary>
    [Fact]
    public async Task No_tenant_signal_at_all_is_still_answered_Missing_Tenant()
    {
        var run = await RunAsync(MainPath, jwtTenant: null, host: NeutralHost);

        Assert.False(run.HandlerRan);
        Assert.Equal(StatusCodes.Status400BadRequest, run.StatusCode);
        Assert.Equal("Missing Tenant", run.Title);
        Assert.Null(run.ConflictingSignals);
    }

    /// <summary>
    /// THE DEV BYPASS FILLS AN ABSENCE, IT DOES NOT PAPER OVER A CONTRADICTION. It stands in for a MISSING
    /// tenant only; ordering the conflict check after it would have let development quietly resolve a request
    /// the rule refuses.
    /// </summary>
    [Fact]
    public async Task Dev_bypass_fills_a_missing_tenant_but_never_a_contradicting_one()
    {
        var bypassTenant = Guid.NewGuid();

        var missing = await RunAsync(MainPath, jwtTenant: null, host: NeutralHost, devBypassTenant: bypassTenant);
        Assert.True(missing.HandlerRan, "the dev bypass stopped filling a missing tenant");
        Assert.Equal(bypassTenant, missing.ForwardedTenant);

        var contradicting = await RunAsync(
            MainPath,
            jwtTenant: Guid.NewGuid(),
            headerTenant: Guid.NewGuid(),
            host: NeutralHost,
            devBypassTenant: bypassTenant);

        Assert.False(contradicting.HandlerRan, "the dev bypass swallowed a contradiction");
        Assert.Equal(StatusCodes.Status400BadRequest, contradicting.StatusCode);
        Assert.Equal(["header"], contradicting.ConflictingSignals);
    }

    /// <summary>The refusal is a problem+json document, the same shape every other service answers.</summary>
    [Fact]
    public async Task The_refusal_body_is_problem_json_with_a_traceId()
    {
        var run = await RunAsync(MainPath, jwtTenant: Guid.NewGuid(), headerTenant: Guid.NewGuid(), host: NeutralHost);

        Assert.Equal("application/problem+json", run.ContentType);
        Assert.Equal("Tenant mismatch", run.Title);
        Assert.Equal(400, run.Body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(run.Body.GetProperty("detail").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(run.Body.GetProperty("traceId").GetString()));
    }

    private static async Task<Run> RunAsync(
        string path,
        Guid? jwtTenant = null,
        Guid? headerTenant = null,
        string host = NeutralHost,
        Guid? devBypassTenant = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Request.Host = new HostString(host);
        context.Response.Body = new MemoryStream();
        context.RequestServices = BuildRequestServices();

        if (jwtTenant.HasValue)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenant_id", jwtTenant.Value.ToString()), new Claim("actor_type", "tenant_user")],
                "test"));
        }

        if (headerTenant.HasValue)
        {
            context.Request.Headers[TenantHeader] = headerTenant.Value.ToString();
        }

        var settings = new Dictionary<string, string?>();
        if (devBypassTenant.HasValue)
        {
            settings["TenantResolution:DevBypassEnabled"] = "true";
            settings["TenantResolution:DevBypassTenantId"] = devBypassTenant.Value.ToString();
        }

        var ran = false;
        var middleware = new TenantResolutionMiddleware(
            _ =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            NullLogger<TenantResolutionMiddleware>.Instance,
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            // Only the bypass cases run as Development; everywhere else the bypass must not stand in for a
            // tenant and quietly turn a refusal into a pass.
            new StubEnvironment(devBypassTenant.HasValue ? Environments.Development : Environments.Production));

        await middleware.InvokeAsync(context);

        Guid? forwarded = context.Items.TryGetValue(TenantHeader, out var item) && item is Guid g ? g : null;

        context.Response.Body.Position = 0;
        var raw = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var body = string.IsNullOrWhiteSpace(raw)
            ? default(JsonElement?)
            : JsonDocument.Parse(raw).RootElement;

        string[]? signals = null;
        if (body?.TryGetProperty("conflictingSignals", out var signalsElement) == true
            && signalsElement.ValueKind == JsonValueKind.Array)
        {
            signals = signalsElement.EnumerateArray().Select(x => x.GetString()!).ToArray();
        }

        return new Run(
            ran,
            context.Response.StatusCode,
            context.Response.ContentType,
            body ?? default,
            body?.TryGetProperty("title", out var title) == true ? title.GetString() : null,
            signals,
            forwarded);
    }

    private static IServiceProvider BuildRequestServices()
    {
        // The middleware calls context.AuthenticateAsync("Bearer") when context.User is unauthenticated; without
        // an IAuthenticationService in RequestServices that throws before any tenant logic runs.
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService, NoResultAuthenticationService>();
        return services.BuildServiceProvider();
    }

    private sealed record Run(
        bool HandlerRan,
        int StatusCode,
        string? ContentType,
        JsonElement Body,
        string? Title,
        string[]? ConflictingSignals,
        Guid? ForwardedTenant);

    private sealed class NoResultAuthenticationService : IAuthenticationService
    {
        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
            => Task.FromResult(AuthenticateResult.NoResult());

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
            => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
            => Task.CompletedTask;
    }

    private sealed class StubEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Diten.ApiGateway.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
