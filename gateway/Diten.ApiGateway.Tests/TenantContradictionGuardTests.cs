using System.Security.Claims;
using System.Text.Json;
using Diten.ApiGateway.Middleware;
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
    ///
    /// <para>⚠ This is a statement about the ABSENCE of a token, not about any path being exempt. It is kept
    /// distinct from <see cref="Login_without_a_token_cannot_contradict_anything"/> on purpose: neither of them
    /// says anything about a request that DOES carry a token, and reading either as "login is safe" is the
    /// mistake that left the rule unmeasured for a round.</para>
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
    /// (g) — LOGIN IS NOT EXEMPT; IT IS MERELY TOKENLESS, AND THAT IS ALL THIS TEST MEASURES.
    ///
    /// <para>⚠ This test was previously named "…conflicting_signals_still_pass" and its comment claimed login was
    /// UNTOUCHED by BL-324. It measured no such thing. With <c>jwtTenant: null</c> a contradiction is structurally
    /// impossible, so the test would have stayed green even if every token-carrying login request were refused —
    /// it restated the assumption instead of testing it. The name and the comment now say only what the body
    /// proves: on the login path, with no token, disagreeing header and host are not a contradiction.</para>
    ///
    /// <para>The behaviour it USED to claim to cover is measured for real by
    /// <see cref="Login_that_DOES_carry_a_token_is_refused_like_any_other_path"/>, which asserts 400.</para>
    /// </summary>
    [Fact]
    public async Task Login_without_a_token_cannot_contradict_anything()
    {
        var run = await RunAsync(
            "/api/tenant-auth/login",
            jwtTenant: null,
            headerTenant: Guid.NewGuid(),
            host: HostFor(Guid.NewGuid()));

        Assert.True(run.HandlerRan, "a tokenless login was refused as a contradiction");
        Assert.Equal(StatusCodes.Status200OK, run.StatusCode);
    }

    /*
     * (g, THE REAL MEASUREMENT) — LOGIN HAS NO EXEMPTION. OWNER DECISION 2026-08-30: the single rule stands, so a
     * login request that DOES carry a token naming another tenant is refused 400 exactly like every other path.
     * The 400 below is not a defect being pinned in place; it IS the decision.
     *
     * ⚠ WHY THIS DOES NOT LOCK ANYONE OUT TODAY. A real login never reaches this middleware with a token, and that
     * rests on TWO independent preconditions — neither of which is enforced by this middleware:
     *
     *   1. The gateway receives login SERVER-TO-SERVER, not from the browser. Diten.Web calls
     *      AuthGateway.SendAuthRequestAsync (frontend/Diten.Web/Services/Auth/AuthGateway.cs:203-230) against
     *      BaseAddress localhost:5000: no Cookie header is forwarded (AddClientMetadataHeaders sends only
     *      X-Forwarded-For / User-Agent / X-Correlation-Id), includeBearer is false for login (:56-66), and the
     *      host is "localhost" so ReadSubdomainTenant yields null anyway.
     *   2. The auth cookie is HOST-ONLY. AuthCookieService.BuildCookieOptions
     *      (frontend/Diten.Web/Services/Auth/AuthCookieService.cs:21-31) never sets Domain, so per RFC 6265
     *      tenant A's token is never sent to tenant B's host in the first place.
     *
     * IF EITHER PRECONDITION FALLS, LOGIN BREAKS — the user is refused 400 at sign-in and cannot recover by
     * signing in again. Precondition 2 is the fragile one (one added line does it), so it has its own guard:
     * frontend/Diten.Web.Tests/Auth/AuthCookieDomainScopeGuardTests.cs.
     */
    [Fact]
    public async Task Login_that_DOES_carry_a_token_is_refused_like_any_other_path()
    {
        var run = await RunAsync(
            "/api/tenant-auth/login",
            jwtTenant: Guid.NewGuid(),
            host: HostFor(Guid.NewGuid()));

        Assert.False(run.HandlerRan, "the login path acquired a contradiction exemption");
        Assert.Equal(StatusCodes.Status400BadRequest, run.StatusCode);
        Assert.Equal("Tenant mismatch", run.Title);
        Assert.Equal(["subdomain"], run.ConflictingSignals);
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

        // ⚠ THE OTHER HELPER'S MEDIA TYPE. The contradiction refusal below is written by WriteTenantMismatch,
        // which has passed `contentType:` since e28aa858; THIS refusal comes from WriteProblemDetails, which
        // feeds nine call sites here and answered "application/json; charset=utf-8" until 2026-08-30 because
        // it assigned Response.ContentType before WriteAsJsonAsync overwrote it. Two helpers, so two
        // assertions — a green test on one of them was never evidence about the other.
        Assert.Equal("application/problem+json", run.ContentType);
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
        // The middleware reads claims from context.User ONLY — it no longer re-runs authentication itself, so no
        // IAuthenticationService is needed. RequestServices is still populated because WriteAsJsonAsync looks up
        // JSON options through it.
        return new ServiceCollection().BuildServiceProvider();
    }

    private sealed record Run(
        bool HandlerRan,
        int StatusCode,
        string? ContentType,
        JsonElement Body,
        string? Title,
        string[]? ConflictingSignals,
        Guid? ForwardedTenant);

    private sealed class StubEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Diten.ApiGateway.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
