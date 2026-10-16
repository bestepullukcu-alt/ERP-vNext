using System.Security.Claims;
using System.Text.Json;
using Diten.MdmService.Application.Common;
using Diten.MdmService.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.MdmService.Application.Tests.Tenancy;

/*
 * THE GUARD — TENANT CONTRADICTION IS REFUSED IN MdmService (BL-324, rule from BL-323 / DCP-004 §7.4).
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
 * test notices. Five of the seven sites had one; MdmService did not. This is it.
 *
 * ⚠ NOT A VACUITY CHECK. A middleware that does NOTHING answers 200, which is neither 403 nor 404 — so a test
 * asserting only "not 403 and not 404" passes against the very defect this guard exists to catch. Every case below
 * asserts the request was ACTUALLY REFUSED (the handler did not run), the controls prove a middleware that refused
 * EVERYTHING would fail too, and the two refusals are told apart BY THEIR BODIES so that a contradiction 400
 * cannot be satisfied by the missing-tenant 400 firing in its place.
 *
 * ⚠ MdmService IS NOT CrmService/HcmService, AND THE DIFFERENCE IS ASSERTED. There the JWT is read ONLY to detect
 * a contradiction and a token-without-header stays the tenant-MISSING case. Here `jwtTenant ?? headerTenant` makes
 * the token a RESOLUTION SOURCE, and a request with no signal at all is REFUSED 400 rather than passed through
 * with the tenant cleared. Both differences are pinned below rather than inherited from the precedent file.
 */
public sealed class TenantContradictionGuardTests
{
    private const string TenantHeader = "X-Tenant-Id";
    private const string TenantPath = "/api/legal-entities";

    // ── THE RULE ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Contradiction_is_refused_400_and_never_reaches_the_handler()
    {
        var (context, tenantContext, handlerRan) = await RunTenantMiddleware(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(handlerRan(), "a contradicting request reached the handler");
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        // Neither value may win by default. Reading TenantId while unresolved throws, so a handler downstream
        // cannot silently act for whichever of the two the middleware happened to prefer.
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
        // the five others that copy this helper (gateway, AuthService, CrmService, DevEnablementService,
        // HcmService). The declaration now travels as WriteAsJsonAsync's `contentType` PARAMETER, the only way
        // it survives. This assertion is what keeps it there: assigning the property again instead would pass
        // every other test in this file and silently put the old value back on the wire.
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
    /// The third row is MdmService-specific: here the token alone RESOLVES the tenant, it does not merely
    /// corroborate the header.
    /// </summary>
    [Theory]
    [InlineData(true, true)]    // token and header agree
    [InlineData(false, true)]   // header only — no bearer token on the request; not a contradiction
    [InlineData(true, false)]   // token only — MdmService resolves from `jwtTenant ?? headerTenant`
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
    /// satisfiable by a middleware whose contradiction branch is dead and whose missing-tenant branch answers for
    /// it. Measured 2026-08-30: it cannot, because a contradiction leaves both values present.
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

    // ── THE SCOPE LINE ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// BYPASS PATHS ARE UNTOUCHED. Each row is a request that would be refused 400 "Missing Tenant" on any other
    /// path — see the test above, which is this theory's differential control. Passing here is therefore a fact
    /// about the PATH and not about a middleware that lets everything through.
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

    // PRODUCT-FIVE-TENANT-BINDING-PROOF-01. These are A-level middleware observations. MdmService's
    // TenantResolutionMiddleware has no actor-type branch, so authorization and repository admission remain
    // outside this direct middleware harness.
    [Fact]
    public async Task Canonical_tenant_user_claim_and_matching_header_bind_the_tenant_context()
    {
        var tenant = Guid.NewGuid();
        var (context, tenantContext, handlerRan) = await RunWithClaimsAsync(
            [new Claim("tenant_id", tenant.ToString("D")), new Claim("actor_type", "tenant_user")], tenant);

        Assert.True(handlerRan());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(tenantContext.IsResolved);
        Assert.Equal(tenant, tenantContext.TenantId);
    }

    [Fact]
    public async Task Header_only_tenant_selection_binds_the_tenant_context_without_an_authenticated_claim()
    {
        var tenant = Guid.NewGuid();
        var (context, tenantContext, handlerRan) = await RunWithClaimsAsync([], tenant);

        Assert.True(handlerRan());
        Assert.True(tenantContext.IsResolved);
        Assert.Equal(tenant, tenantContext.TenantId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("service")]
    public async Task Missing_or_invalid_actor_type_does_not_change_mdm_tenant_resolution(string? actorType)
    {
        var tenant = Guid.NewGuid();
        var claims = new List<Claim> { new("tenant_id", tenant.ToString("D")) };
        if (actorType is not null) claims.Add(new Claim("actor_type", actorType));

        var (context, tenantContext, handlerRan) = await RunWithClaimsAsync(claims, tenant);

        // MDM's current tenant-resolution middleware has no actor branch. This records that limited A-level
        // fact; neither [Authorize] nor permission handlers are in this direct middleware harness.
        Assert.True(handlerRan());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(tenantContext.IsResolved);
        Assert.Equal(tenant, tenantContext.TenantId);
    }

    [Fact]
    public async Task Conflicting_duplicate_canonical_tenant_claims_are_refused_without_binding_the_context()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var (context, tenantContext, handlerRan) = await RunWithClaimsAsync(
            [new Claim("tenant_id", first.ToString("D")), new Claim("tenant_id", second.ToString("D"))], first);

        Assert.False(handlerRan());
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.False(tenantContext.IsResolved);
    }

    [Fact]
    public async Task Equal_duplicate_canonical_tenant_claims_are_refused_without_binding_the_context()
    {
        var tenant = Guid.NewGuid();
        var (context, tenantContext, handlerRan) = await RunWithClaimsAsync(
            [new Claim("tenant_id", tenant.ToString("D")), new Claim("tenant_id", tenant.ToString("D"))], tenant);

        // Equal values do not make a repeated canonical identity singular; the middleware must reject before
        // header fallback, next-pipeline execution, or TenantContext binding.
        Assert.False(handlerRan());
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.False(tenantContext.IsResolved);
    }

    [Fact]
    public async Task Conflicting_canonical_and_legacy_tenant_aliases_are_refused_without_binding_the_context()
    {
        var canonical = Guid.NewGuid();
        var legacy = Guid.NewGuid();
        var (context, tenantContext, handlerRan) = await RunWithClaimsAsync(
            [new Claim("tenant_id", canonical.ToString("D")), new Claim("tenantId", legacy.ToString("D"))], canonical);

        Assert.False(handlerRan());
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.False(tenantContext.IsResolved);
    }

    [Fact]
    public async Task Legacy_only_tenant_alias_and_matching_header_bind_the_tenant_context()
    {
        var tenant = Guid.NewGuid();
        var (context, tenantContext, handlerRan) = await RunWithClaimsAsync([new Claim("tenantId", tenant.ToString("D"))], tenant);

        Assert.True(handlerRan());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(tenantContext.IsResolved);
        Assert.Equal(tenant, tenantContext.TenantId);
    }

    [Fact]
    public async Task Legacy_only_tenant_alias_without_a_header_remains_missing_and_does_not_bind_the_context()
    {
        var tenant = Guid.NewGuid();
        var (context, tenantContext, handlerRan) = await RunWithClaimsAsync([new Claim("tenantId", tenant.ToString("D"))], null);

        // MDM reads tenantId only to reject conflicting identity evidence. The canonical tenant_id claim and
        // existing header selection remain the only sources that can resolve the tenant context.
        Assert.False(handlerRan());
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("Missing Tenant", BodyOf(context).GetProperty("title").GetString());
        Assert.False(tenantContext.IsResolved);
    }

    [Fact]
    public async Task Equal_canonical_and_legacy_tenant_aliases_and_matching_header_bind_the_tenant_context()
    {
        var tenant = Guid.NewGuid();
        var (context, tenantContext, handlerRan) = await RunWithClaimsAsync(
            [new Claim("tenant_id", tenant.ToString("D")), new Claim("tenantId", tenant.ToString("D"))], tenant);

        Assert.True(handlerRan());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(tenantContext.IsResolved);
        Assert.Equal(tenant, tenantContext.TenantId);
    }

    [Fact]
    public async Task Duplicate_legacy_tenant_aliases_are_refused_without_binding_the_context()
    {
        var tenant = Guid.NewGuid();
        var (context, tenantContext, handlerRan) = await RunWithClaimsAsync(
            [new Claim("tenantId", tenant.ToString("D")), new Claim("tenantId", tenant.ToString("D"))], tenant);

        Assert.False(handlerRan());
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.False(tenantContext.IsResolved);
    }

    [Fact]
    public async Task Conflicting_legacy_tenant_aliases_are_refused_without_binding_the_context()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var (context, tenantContext, handlerRan) = await RunWithClaimsAsync(
            [new Claim("tenantId", first.ToString("D")), new Claim("tenantId", second.ToString("D"))], first);

        Assert.False(handlerRan());
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.False(tenantContext.IsResolved);
    }

    [Fact]
    public async Task Malformed_tenant_claim_with_valid_header_records_header_only_resolution()
    {
        var tenant = Guid.NewGuid();
        var (context, tenantContext, handlerRan) = await RunWithClaimsAsync([new Claim("tenant_id", "malformed")], tenant);

        Assert.True(handlerRan());
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(tenantContext.IsResolved);
        Assert.Equal(tenant, tenantContext.TenantId);
    }

    // ── PLUMBING ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Runs the REAL middleware over a request carrying the given tenant claim and/or header.</summary>
    private static async Task<(HttpContext Context, TenantContext Tenant, Func<bool> HandlerRan)>
        RunTenantMiddleware(Guid? jwtTenant, Guid? headerTenant, string method = "GET", string path = TenantPath)
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

        var ran = false;
        var middleware = new TenantResolutionMiddleware(
            _ =>
            {
                ran = true;
                return Task.CompletedTask;
            },
            NullLogger<TenantResolutionMiddleware>.Instance);

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

    private static async Task<(HttpContext Context, TenantContext Tenant, Func<bool> HandlerRan)> RunWithClaimsAsync(
        IReadOnlyCollection<Claim> claims,
        Guid? headerTenant)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = TenantPath;
        context.Response.Body = new MemoryStream();
        if (claims.Count > 0)
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        if (headerTenant.HasValue)
            context.Request.Headers[TenantHeader] = headerTenant.Value.ToString("D");

        var ran = false;
        var tenantContext = new TenantContext();
        var middleware = new TenantResolutionMiddleware(
            _ => { ran = true; return Task.CompletedTask; },
            NullLogger<TenantResolutionMiddleware>.Instance);
        await middleware.InvokeAsync(context, tenantContext);
        return (context, tenantContext, () => ran);
    }
}
