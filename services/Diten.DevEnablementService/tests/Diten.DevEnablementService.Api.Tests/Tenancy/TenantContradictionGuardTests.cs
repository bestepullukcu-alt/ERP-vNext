using System.Security.Claims;
using System.Text.Json;
using Diten.DevEnablementService.Application.Common;
using Diten.DevEnablementService.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.DevEnablementService.Api.Tests.Tenancy;

/*
 * THE GUARD — TENANT CONTRADICTION IS REFUSED IN DevEnablementService (BL-324, rule from BL-323 / DCP-004 §7.4).
 *
 * THE RULE (owner decision 2026-08-29): if `X-Tenant-Id` and the authenticated JWT name DIFFERENT tenants, the
 * request is refused 400. A malformed request, not an access decision; the caller wrote both values, so nothing is
 * concealed by refusing it out loud.
 *
 * ⚠ WHY THIS FILE EXISTS AT ALL — THE REPO-WIDE GUARD CANNOT SEE THIS. All seven tenant-resolution sites are
 * classified by tests/architecture/.../TenantContradictionSiteGuardTests.cs, and that guard is a TEXT scan: it
 * regex-matches the refusal condition and looks for a nearby Status400BadRequest. MEASURED 2026-08-30: changing
 * the live condition to `if (false && jwtTenant.HasValue && ...)` switches the rule COMPLETELY OFF while leaving
 * every character the scanner looks for in place — the architecture guard stays 14/14 green. Only a behaviour
 * test notices.
 *
 * ⚠ WHAT THIS FILE ADDS OVER WorkItemBridge/CrossTenantContractGuardTests. That file's Case 1 already drives this
 * same middleware and DOES catch the sabotage above — measured, not assumed. What it does not measure is the rest
 * of this middleware's decision table, and the table is where this service differs from every other site: the
 * DEV BYPASS can invent a tenant out of configuration. So this file pins the WHOLE table in one place, named after
 * the rule the way the other six sites' guards are, and the two cases that overlap with Case 1 are the core
 * refusal and its control — deliberately duplicated, because a guard whose central assertion lives in another file
 * is a guard nobody finds.
 *
 * ⚠ NOT A VACUITY CHECK. A middleware that does NOTHING answers 200, which is neither 403 nor 404 — so a test
 * asserting only "not 403 and not 404" passes against the very defect this guard exists to catch. Every case below
 * asserts the request was ACTUALLY REFUSED (the handler did not run), the controls prove a middleware that refused
 * EVERYTHING would fail too, the two 400s are told apart BY THEIR BODIES, and the dev-bypass refusal is paired
 * with a control proving the bypass was genuinely ENABLED in that same configuration.
 */
public sealed class TenantContradictionGuardTests
{
    private const string TenantHeader = "X-Tenant-Id";
    private const string TenantPath = "/api/v1/work-items/projection";

    // ── THE RULE ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Contradiction_is_refused_400_and_never_reaches_the_handler()
    {
        var (context, tenantContext, handlerRan) = await RunTenantMiddleware(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(handlerRan(), "a contradicting request reached the handler");
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        // Neither value may win by default. The reference work-item consumer keys its state by the RAW HEADER, so
        // a middleware that let the token quietly win here would still hand that consumer the other tenant's id.
        Assert.False(tenantContext.IsResolved, "a tenant was resolved from a contradicting request");
    }

    /// <summary>
    /// THE BODY, NOT JUST THE CODE. This service answers 400 to TWO different things — a contradiction and a
    /// missing tenant — so "it was a 400" is not evidence that the contradiction branch ran. The title is what
    /// tells them apart on the wire, and it is asserted here and negated in the missing-tenant test below.
    /// </summary>
    [Fact]
    public async Task Contradiction_is_refused_with_the_tenant_mismatch_body()
    {
        var (context, _, _) = await RunTenantMiddleware(Guid.NewGuid(), Guid.NewGuid());

        var body = BodyOf(context);
        Assert.Equal("Tenant mismatch", body.GetProperty("title").GetString());
        Assert.Equal(StatusCodes.Status400BadRequest, body.GetProperty("status").GetInt32());

        // ⚠ THE MEDIA TYPE, ON THE WIRE — pinned because it was WRONG here until 2026-08-30 and the source did
        // not show it. WriteProblemDetails used to assign `Response.ContentType = "application/problem+json"`
        // and THEN call WriteAsJsonAsync, which overwrites Response.ContentType UNCONDITIONALLY: the wire
        // answered "application/json; charset=utf-8" while the source claimed problem+json, at this site and at
        // the five others that copy this helper (gateway, AuthService, CrmService, HcmService, MdmService). The
        // declaration now travels as WriteAsJsonAsync's `contentType` PARAMETER, the only way it survives. This
        // assertion is what keeps it there: assigning the property again instead would pass every other test in
        // this file and silently put the old value back on the wire.
        Assert.Equal("application/problem+json", context.Response.ContentType);
    }

    [Fact]
    public async Task Contradiction_is_refused_and_the_refusal_is_never_403_or_404()
    {
        var (context, _, handlerRan) = await RunTenantMiddleware(Guid.NewGuid(), Guid.NewGuid());

        // ⚠ This assertion FIRST. Without it the test passes on a middleware that does nothing: a pass-through
        // answers 200, which is neither 403 nor 404, and the "never 403" claim would hold for the wrong reason.
        Assert.False(handlerRan(), "the contradiction was not refused at all");

        // 403 would be an access verdict on a request that cannot be evaluated at all; 404 would pretend the
        // ADDRESS is missing when it is the REQUEST that is malformed. DCP-004 §7.4 case 2's 404 is about a
        // RECORD, and is a different question from this one.
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.NotEqual(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    // ── THE CONTROLS ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// THE CONTROL. Without these, a middleware that refused EVERY request would pass every case above.
    /// The third row matters here: this service resolves from `jwtTenant ?? headerTenant`, so the token alone
    /// RESOLVES the tenant rather than merely corroborating the header — that is Platform's own S2S shape.
    /// </summary>
    [Theory]
    [InlineData(true, true)]    // token and header agree
    [InlineData(false, true)]   // header only — no bearer token on the request; not a contradiction
    [InlineData(true, false)]   // token only — resolved from the claim
    public async Task Control_a_request_that_does_not_contradict_itself_passes_through(bool withJwt, bool withHeader)
    {
        var tenant = Guid.NewGuid();

        var (context, tenantContext, handlerRan) = await RunTenantMiddleware(
            withJwt ? tenant : null,
            withHeader ? tenant : null);

        Assert.True(handlerRan(), "a self-consistent request was refused");
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(tenantContext.IsResolved);
        Assert.Equal(tenant, tenantContext.TenantId);
    }

    /// <summary>
    /// THE OTHER 400, KEPT DISTINCT. A request naming no tenant at all was refused before BL-324 and still is —
    /// but with its OWN title. Asserting the negative ("this is NOT the mismatch body") is the half that matters:
    /// it is what stops <see cref="Contradiction_is_refused_400_and_never_reaches_the_handler"/> from being
    /// satisfiable by a middleware whose contradiction branch is dead and whose missing-tenant branch answers in
    /// its place.
    /// </summary>
    [Fact]
    public async Task Missing_tenant_is_still_its_own_refusal_and_is_not_the_mismatch_body()
    {
        var (context, tenantContext, handlerRan) = await RunTenantMiddleware(jwtTenant: null, headerTenant: null);

        Assert.False(handlerRan(), "a request naming no tenant reached the handler");
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.False(tenantContext.IsResolved);

        var title = BodyOf(context).GetProperty("title").GetString();
        Assert.Equal("Missing Tenant", title);
        Assert.NotEqual("Tenant mismatch", title);
    }

    // ── THE DEV BYPASS — THE BRANCH ONLY THIS SERVICE HAS ────────────────────────────────────────────────

    /// <summary>
    /// THE DEV BYPASS MUST NOT RESCUE A CONTRADICTION. `TryGetDevelopmentBypassTenant` can conjure a tenant id out
    /// of configuration, and it sits in the same method as the rule. Today it runs AFTER the refusal so it cannot
    /// reach a contradicting request — but "cannot" is a claim about statement ORDER, which is exactly the kind of
    /// thing a later edit reorders without noticing. Measured here instead of reasoned about.
    /// </summary>
    [Fact]
    public async Task Dev_bypass_does_not_rescue_a_contradicting_request()
    {
        var (context, tenantContext, handlerRan) = await RunTenantMiddleware(
            Guid.NewGuid(),
            Guid.NewGuid(),
            devBypassTenant: Guid.NewGuid());

        Assert.False(handlerRan(), "the dev bypass carried a contradicting request through");
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.False(tenantContext.IsResolved, "the dev bypass resolved a tenant for a contradicting request");
        Assert.Equal("Tenant mismatch", BodyOf(context).GetProperty("title").GetString());
    }

    /// <summary>
    /// THE CONTROL FOR THE TEST ABOVE, and the reason it is not vacuous. The bypass is only a meaningful obstacle
    /// if it was genuinely ON in that configuration — a bypass that never fires would make the refusal above true
    /// for the wrong reason. Same Development environment, same enabled flag, same tenant id: with no contradiction
    /// to refuse, the bypass DOES fire and supplies the tenant.
    /// </summary>
    [Fact]
    public async Task Control_the_dev_bypass_really_is_enabled_in_that_same_configuration()
    {
        var bypassTenant = Guid.NewGuid();

        var (context, tenantContext, handlerRan) = await RunTenantMiddleware(
            jwtTenant: null,
            headerTenant: null,
            devBypassTenant: bypassTenant);

        Assert.True(handlerRan(), "the dev bypass did not fire, so the refusal above proves nothing");
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(tenantContext.IsResolved);
        Assert.Equal(bypassTenant, tenantContext.TenantId);
    }

    // ── THE SCOPE LINE ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// BYPASS PATHS ARE UNTOUCHED. Each row is a request that would be refused 400 "Missing Tenant" on any other
    /// path — see <see cref="Missing_tenant_is_still_its_own_refusal_and_is_not_the_mismatch_body"/>, which is this
    /// theory's differential control. Passing here is therefore a fact about the PATH and not about a middleware
    /// that lets everything through.
    ///
    /// <para>MEASURED 2026-08-30, reported not fixed: this service's Program.cs maps no /health endpoint at all,
    /// so the first two rows measure IsBypassPath rather than a reachable surface. The bypass list still names
    /// /health, and that mismatch is left exactly as found — this round only adds tests.</para>
    /// </summary>
    [Theory]
    [InlineData("GET", "/health")]
    [InlineData("GET", "/health/ready")]
    [InlineData("GET", "/swagger/index.html")]
    [InlineData("GET", "/favicon.ico")]
    [InlineData("OPTIONS", TenantPath)]   // CORS preflight carries no credentials to contradict
    public async Task Bypass_paths_are_not_touched_by_the_rule(string method, string path)
    {
        var (context, tenantContext, handlerRan) = await RunTenantMiddleware(null, null, method, path);

        Assert.True(handlerRan(), "a bypass path was refused");
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.False(tenantContext.IsResolved, "a bypass path resolved a tenant");
    }

    /// <summary>
    /// THE ORDER, PINNED. The bypass is decided BEFORE the contradiction is looked for, so contradicting signals
    /// on /health are not refused. That is deliberate and safe only because the bypass list names three
    /// tenant-free surfaces (/health, /swagger, /favicon.ico) — this test asserts the ORDER, it does NOT bless the
    /// LIST. If a tenant-bearing path is ever added to IsBypassPath, this test keeps passing and will not warn
    /// anyone; that is the known limit of measuring behaviour rather than membership.
    /// </summary>
    [Fact]
    public async Task Scope_the_bypass_is_decided_before_the_rule_and_that_is_deliberate()
    {
        var (context, tenantContext, handlerRan) =
            await RunTenantMiddleware(Guid.NewGuid(), Guid.NewGuid(), "GET", "/health");

        Assert.True(handlerRan(), "the bypass stopped applying to a request carrying tenant signals");
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.False(tenantContext.IsResolved);
    }

    // ── PLUMBING ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Runs the REAL middleware over a request carrying the given tenant claim and/or header. With
    /// <paramref name="devBypassTenant"/> null the environment is Production and the configuration is empty, so
    /// the dev bypass cannot stand in for a tenant and quietly turn a refusal into a pass.
    /// </summary>
    private static async Task<(HttpContext Context, TenantContext Tenant, Func<bool> HandlerRan)>
        RunTenantMiddleware(
            Guid? jwtTenant,
            Guid? headerTenant,
            string method = "GET",
            string path = TenantPath,
            Guid? devBypassTenant = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        if (jwtTenant.HasValue)
        {
            context.User = new ClaimsPrincipal(
                new ClaimsIdentity([new Claim("tenant_id", jwtTenant.Value.ToString())], "test"));
        }

        if (headerTenant.HasValue)
        {
            context.Request.Headers[TenantHeader] = headerTenant.Value.ToString();
        }

        var configuration = devBypassTenant.HasValue
            ? new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TenantResolution:DevBypassEnabled"] = "true",
                ["TenantResolution:DevBypassTenantId"] = devBypassTenant.Value.ToString()
            }).Build()
            : new ConfigurationBuilder().Build();

        var environment = new StubEnvironment
        {
            EnvironmentName = devBypassTenant.HasValue ? Environments.Development : Environments.Production
        };

        var ran = false;
        var middleware = new TenantResolutionMiddleware(
            _ =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            NullLogger<TenantResolutionMiddleware>.Instance,
            configuration,
            environment);

        var tenantContext = new TenantContext();
        await middleware.InvokeAsync(context, tenantContext);

        return (context, tenantContext, () => ran);
    }

    /// <summary>The refusal AS IT GOES ON THE WIRE — the serialized problem+json, not a C# object.</summary>
    private static JsonElement BodyOf(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        var json = reader.ReadToEnd();

        Assert.False(string.IsNullOrWhiteSpace(json), "the refusal carried no body");
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    private sealed class StubEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Diten.DevEnablementService.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
