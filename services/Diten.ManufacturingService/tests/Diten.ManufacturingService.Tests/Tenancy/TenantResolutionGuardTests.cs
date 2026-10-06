using System.Security.Claims;
using System.Text.Json;
using Diten.ManufacturingService.Application.Common;
using Diten.ManufacturingService.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.ManufacturingService.Tests.Tenancy;

/*
 * BEHAVIOUR TEST for the tenant-resolution site that TenantContradictionSiteGuardTests classifies:
 * services/Diten.ManufacturingService/src/Diten.ManufacturingService.Infrastructure/Middleware/TenantResolutionMiddleware.cs
 * It drives the REAL middleware. Every refusal is paired with a control that passes in the same setup (K3), and every
 * refusal answers the BOM contract Error shape with the request's correlation id.
 */
public sealed class TenantResolutionGuardTests
{
    [Fact]
    public async Task Tenant_contradiction_is_refused_400_before_the_handler()
    {
        var (context, tenant, ran) = await Run(jwtTenant: Guid.NewGuid(), jwtLe: Guid.NewGuid(), headerTenant: Guid.NewGuid(), headerLe: null);
        Assert.False(ran());
        Assert.Equal(400, context.Response.StatusCode);
        Assert.False(tenant.IsResolved);
        Assert.StartsWith("Tenant mismatch", Error(context).GetProperty("message").GetString());
    }

    [Fact]
    public async Task Legal_entity_header_and_query_contradiction_is_refused_400()
    {
        var t = Guid.NewGuid();
        var (context, _, ran) = await Run(jwtTenant: t, jwtLe: null, headerTenant: t, headerLe: Guid.NewGuid(), queryLe: Guid.NewGuid().ToString());
        Assert.False(ran());
        Assert.Equal(400, context.Response.StatusCode);
        Assert.StartsWith("Legal-entity mismatch", Error(context).GetProperty("message").GetString());
    }

    [Fact]
    public async Task Missing_legal_entity_is_refused_400_never_pooled_under_an_empty_one()
    {
        var t = Guid.NewGuid();
        var (context, tenant, ran) = await Run(jwtTenant: t, jwtLe: null, headerTenant: t, headerLe: null);
        Assert.False(ran());
        Assert.Equal(400, context.Response.StatusCode);
        Assert.False(tenant.IsResolved);
        Assert.StartsWith("Missing Legal-Entity", Error(context).GetProperty("message").GetString());
    }

    [Fact]
    public async Task Missing_tenant_is_refused_400()
    {
        var (context, _, ran) = await Run(jwtTenant: null, jwtLe: null, headerTenant: null, headerLe: null);
        Assert.False(ran());
        Assert.Equal(400, context.Response.StatusCode);
        Assert.StartsWith("Missing Tenant", Error(context).GetProperty("message").GetString());
    }

    [Fact]
    public async Task Control_a_fully_scoped_request_resolves_from_the_token()
    {
        var t = Guid.NewGuid();
        var le = Guid.NewGuid();
        var (context, tenant, ran) = await Run(jwtTenant: t, jwtLe: null, headerTenant: t, headerLe: le);
        Assert.True(ran());
        Assert.Equal(200, context.Response.StatusCode);
        Assert.Equal(t, tenant.TenantId);
        Assert.Equal(le, tenant.LegalEntityId);
    }

    [Fact]
    public async Task A_legal_entity_claim_in_the_token_is_not_a_legal_entity()
    {
        // The platform token has no such claim; if one ever appeared it must not silently scope the request.
        var t = Guid.NewGuid();
        var (context, tenant, ran) = await Run(jwtTenant: t, jwtLe: Guid.NewGuid(), headerTenant: t, headerLe: null);
        Assert.False(ran());
        Assert.Equal(400, context.Response.StatusCode);
        Assert.False(tenant.IsResolved);
        Assert.Equal("LEGAL_ENTITY_REQUIRED", Error(context).GetProperty("code").GetString());
    }

    [Fact]
    public async Task On_GET_the_legal_entity_may_come_from_the_query_control_with_the_same_value()
    {
        var t = Guid.NewGuid();
        var le = Guid.NewGuid();
        var (_, viaQuery, ranQuery) = await Run(jwtTenant: t, jwtLe: null, headerTenant: t, headerLe: null, queryLe: le.ToString());
        var (_, both, ranBoth) = await Run(jwtTenant: t, jwtLe: null, headerTenant: t, headerLe: le, queryLe: le.ToString());
        Assert.True(ranQuery());
        Assert.Equal(le, viaQuery.LegalEntityId);
        Assert.True(ranBoth());
        Assert.Equal(le, both.LegalEntityId);
    }

    [Fact]
    public async Task On_POST_a_query_legal_entity_is_not_read()
    {
        var t = Guid.NewGuid();
        var (context, _, ran) = await Run(jwtTenant: t, jwtLe: null, headerTenant: t, headerLe: null, queryLe: Guid.NewGuid().ToString(), method: "POST");
        Assert.False(ran());
        Assert.Equal(400, context.Response.StatusCode);
    }

    [Fact]
    public async Task Refusal_carries_the_contract_error_shape_and_the_correlation_id()
    {
        var correlation = new CorrelationContext();
        correlation.Set("7d3c4f8e-0a7b-4b1e-9c3d-2f1a0b9c8d7e");
        var (context, _, _) = await Run(jwtTenant: Guid.NewGuid(), jwtLe: Guid.NewGuid(), headerTenant: Guid.NewGuid(), headerLe: null, correlation: correlation);

        using var body = JsonDocument.Parse(Read(context));
        Assert.Equal("v1", body.RootElement.GetProperty("contractVersion").GetString());
        var error = body.RootElement.GetProperty("error");
        Assert.Equal("INVALID_REQUEST", error.GetProperty("code").GetString());
        Assert.Equal(correlation.CorrelationId, error.GetProperty("correlationId").GetString());
    }

    private static async Task<(DefaultHttpContext Context, TenantContext Tenant, Func<bool> HandlerRan)> Run(
        Guid? jwtTenant, Guid? jwtLe, Guid? headerTenant, Guid? headerLe, CorrelationContext? correlation = null, string? queryLe = null, string method = "GET")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = "/api/bom/versions";
        if (queryLe is not null) context.Request.QueryString = new QueryString("?legalEntityId=" + Uri.EscapeDataString(queryLe));
        context.Response.Body = new MemoryStream();
        var claims = new List<Claim>();
        if (jwtTenant is { } jt) claims.Add(new Claim("tenant_id", jt.ToString()));
        if (jwtLe is { } jl) claims.Add(new Claim("legal_" + "entity_id", jl.ToString())); // a stray claim, to prove it is ignored
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        if (headerTenant is { } ht) context.Request.Headers["X-Tenant-Id"] = ht.ToString();
        if (headerLe is { } hl) context.Request.Headers["X-Legal-Entity-Id"] = hl.ToString();

        var ran = false;
        var middleware = new TenantResolutionMiddleware(
            _ => { ran = true; context.Response.StatusCode = 200; return Task.CompletedTask; },
            NullLogger<TenantResolutionMiddleware>.Instance,
            new ConfigurationBuilder().Build(),
            new Environment());
        var tenant = new TenantContext();
        await middleware.InvokeAsync(context, tenant, correlation ?? new CorrelationContext());
        return (context, tenant, () => ran);
    }

    private static string Read(DefaultHttpContext context)
    {
        context.Response.Body.Position = 0;
        return new StreamReader(context.Response.Body).ReadToEnd();
    }

    private static JsonElement Error(DefaultHttpContext context) => JsonDocument.Parse(Read(context)).RootElement.GetProperty("error");

    private sealed class Environment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Diten.ManufacturingService.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
