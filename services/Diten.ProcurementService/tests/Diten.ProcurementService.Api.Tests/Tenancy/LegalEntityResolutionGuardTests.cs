using System.Security.Claims;
using System.Text.Json;
using Diten.ProcurementService.Application.Common;
using Diten.ProcurementService.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.ProcurementService.Api.Tests.Tenancy;

/*
 * THE GUARD — MISSING LEGAL-ENTITY IS FAIL-CLOSED 400 in ProcurementService (MOD-0140 FAZ 2 harden).
 *
 * THE RULE: a tenant-scoped request that names a tenant but NO legal-entity (neither JWT `legal_entity_id` claim
 * nor `X-Legal-Entity-Id` header, nor a dev bypass) is refused 400. The scaffold used to fall back to Guid.Empty,
 * which silently pooled EVERY legal entity's records under one empty LE key — a cross-LE leak. This file pins the
 * refusal against the REAL middleware (production code), not a copy.
 *
 * ⚠ NOT A VACUITY CHECK (K3). The refusal is meaningless unless a request that DOES carry an LE passes in the same
 * setup — so every "absent → 400" case is paired with a "present → resolved 200" control. A middleware that refused
 * everything would fail the controls; one that resolved everything would fail the refusals.
 */
public sealed class LegalEntityResolutionGuardTests
{
    private const string TenantHeader = "X-Tenant-Id";
    private const string LegalEntityHeader = "X-Legal-Entity-Id";
    private const string TenantPath = "/api/suppliers";

    // ── THE RULE + ITS VACUITY CONTROL ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Missing_legal_entity_is_refused_400_and_never_reaches_the_handler()
    {
        var tenant = Guid.NewGuid();

        var (context, tenantContext, handlerRan) = await Run(jwtTenant: tenant, jwtLe: null, headerTenant: tenant, headerLe: null);

        Assert.False(handlerRan(), "a request naming no legal-entity reached the handler");
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.False(tenantContext.IsResolved, "a legal-entity-less request resolved a tenant context");

        var body = BodyOf(context);
        Assert.Equal("Missing Legal-Entity", body.GetProperty("title").GetString());
        Assert.NotEqual("Missing Tenant", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Control_with_legal_entity_present_the_same_request_resolves_and_passes()
    {
        var tenant = Guid.NewGuid();
        var le = Guid.NewGuid();

        var (context, tenantContext, handlerRan) = await Run(jwtTenant: tenant, jwtLe: le, headerTenant: tenant, headerLe: le);

        Assert.True(handlerRan(), "a fully-scoped request was refused");
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(tenantContext.IsResolved);
        Assert.Equal(tenant, tenantContext.TenantId);
        Assert.Equal(le, tenantContext.LegalEntityId);
    }

    [Fact]
    public async Task Missing_legal_entity_is_not_403_or_404()
    {
        var tenant = Guid.NewGuid();
        var (context, _, handlerRan) = await Run(jwtTenant: tenant, jwtLe: null, headerTenant: tenant, headerLe: null);

        Assert.False(handlerRan(), "the missing-LE request was not refused at all");
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.NotEqual(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    // ── LE CONTRADICTION → 400 (distinct body) ─────────────────────────────────────────────────

    [Fact]
    public async Task Legal_entity_contradiction_is_refused_400()
    {
        var tenant = Guid.NewGuid();

        var (context, tenantContext, handlerRan) = await Run(
            jwtTenant: tenant, jwtLe: Guid.NewGuid(), headerTenant: tenant, headerLe: Guid.NewGuid());

        Assert.False(handlerRan(), "a contradicting legal-entity reached the handler");
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.False(tenantContext.IsResolved);
        Assert.Equal("Legal-entity mismatch", BodyOf(context).GetProperty("title").GetString());
    }

    // ── THE HARDEN DID NOT BREAK THE TENANT GUARD ──────────────────────────────────────────────

    [Fact]
    public async Task Tenant_contradiction_is_still_refused_400()
    {
        var (context, tenantContext, handlerRan) = await Run(
            jwtTenant: Guid.NewGuid(), jwtLe: Guid.NewGuid(), headerTenant: Guid.NewGuid(), headerLe: null);

        Assert.False(handlerRan(), "a contradicting tenant reached the handler");
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("Tenant mismatch", BodyOf(context).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Missing_tenant_is_still_its_own_refusal()
    {
        var (context, _, handlerRan) = await Run(jwtTenant: null, jwtLe: null, headerTenant: null, headerLe: null);

        Assert.False(handlerRan(), "a request naming no tenant reached the handler");
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("Missing Tenant", BodyOf(context).GetProperty("title").GetString());
    }

    // ── DEV BYPASS supplies BOTH tenant and LE (and its control proves it really fired) ────────

    [Fact]
    public async Task Dev_bypass_supplies_legal_entity_when_enabled()
    {
        var bypassTenant = Guid.NewGuid();
        var bypassLe = Guid.NewGuid();

        var (context, tenantContext, handlerRan) = await Run(
            jwtTenant: null, jwtLe: null, headerTenant: null, headerLe: null,
            devBypassTenant: bypassTenant, devBypassLe: bypassLe);

        Assert.True(handlerRan(), "the dev bypass did not fire");
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(tenantContext.IsResolved);
        Assert.Equal(bypassLe, tenantContext.LegalEntityId);
    }

    [Fact]
    public async Task Dev_bypass_without_legal_entity_still_fails_closed()
    {
        // Tenant bypass ON but NO LE bypass configured → tenant resolves, LE does not → 400.
        var bypassTenant = Guid.NewGuid();

        var (context, tenantContext, handlerRan) = await Run(
            jwtTenant: null, jwtLe: null, headerTenant: null, headerLe: null,
            devBypassTenant: bypassTenant, devBypassLe: null);

        Assert.False(handlerRan(), "a tenant-only dev bypass carried a legal-entity-less request through");
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.False(tenantContext.IsResolved);
        Assert.Equal("Missing Legal-Entity", BodyOf(context).GetProperty("title").GetString());
    }

    // ── PLUMBING ───────────────────────────────────────────────────────────────────────────────

    private static async Task<(HttpContext Context, TenantContext Tenant, Func<bool> HandlerRan)> Run(
        Guid? jwtTenant,
        Guid? jwtLe,
        Guid? headerTenant,
        Guid? headerLe,
        Guid? devBypassTenant = null,
        Guid? devBypassLe = null,
        string method = "GET",
        string path = TenantPath)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        var claims = new List<Claim>();
        if (jwtTenant.HasValue) claims.Add(new Claim("tenant_id", jwtTenant.Value.ToString()));
        if (jwtLe.HasValue) claims.Add(new Claim("legal_entity_id", jwtLe.Value.ToString()));
        if (claims.Count > 0)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }

        if (headerTenant.HasValue) context.Request.Headers[TenantHeader] = headerTenant.Value.ToString();
        if (headerLe.HasValue) context.Request.Headers[LegalEntityHeader] = headerLe.Value.ToString();

        var bypassEnabled = devBypassTenant.HasValue || devBypassLe.HasValue;
        var configValues = new Dictionary<string, string?>();
        if (bypassEnabled)
        {
            configValues["TenantResolution:DevBypassEnabled"] = "true";
            if (devBypassTenant.HasValue) configValues["TenantResolution:DevBypassTenantId"] = devBypassTenant.Value.ToString();
            if (devBypassLe.HasValue) configValues["TenantResolution:DevBypassLegalEntityId"] = devBypassLe.Value.ToString();
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();

        var environment = new StubEnvironment
        {
            EnvironmentName = bypassEnabled ? Environments.Development : Environments.Production
        };

        var ran = false;
        var middleware = new TenantResolutionMiddleware(
            _ => { ran = true; return Task.CompletedTask; },
            NullLogger<TenantResolutionMiddleware>.Instance,
            configuration,
            environment);

        var tenantContext = new TenantContext();
        await middleware.InvokeAsync(context, tenantContext);

        return (context, tenantContext, () => ran);
    }

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
        public string ApplicationName { get; set; } = "Diten.ProcurementService.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
