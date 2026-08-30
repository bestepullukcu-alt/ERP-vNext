using System.Security.Claims;
using Diten.HcmService.Application.Common;
using Diten.HcmService.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.HcmService.Application.Tests.Tenancy;

/*
 * THE GUARD — TENANT CONTRADICTION IS REFUSED IN HcmService (BL-324, rule from BL-323 / DCP-004 §7.4).
 *
 * WHAT WAS WRONG. This service's tenant middleware never read the JWT. The `X-Tenant-Id` header alone named the
 * tenant, so an authenticated user of tenant A could send `X-Tenant-Id: B` and the service acted for B. HcmService
 * DOES authenticate — AddAuthentication + UseAuthentication, [Authorize] on every controller — and the middleware
 * runs AFTER UseAuthentication, so the token's tenant was available and simply never consulted.
 * [Authorize] proves WHO the caller is, not WHICH TENANT they may act for.
 *
 * THE RULE (owner decision 2026-08-29): header tenant ≠ JWT tenant → 400. A malformed request, not an access
 * decision; the caller wrote both values, so nothing is concealed by refusing it out loud.
 *
 * ⚠ NOT A VACUITY CHECK. A middleware that does NOTHING answers 200, which is neither 403 nor 404 — so a test
 * asserting only "not 403 and not 404" passes against the very defect this guard exists to catch. That weakness
 * was found in yesterday's BL-323 guard and is not repeated here: every case below asserts the request was
 * ACTUALLY REFUSED (the handler did not run), and the controls prove a middleware that refused EVERYTHING would
 * fail too.
 */
public sealed class TenantContradictionGuardTests
{
    private const string TenantHeader = "X-Tenant-Id";

    [Fact]
    public async Task Contradiction_is_refused_400_and_never_reaches_the_handler()
    {
        var (context, tenantContext, handlerRan) = await RunTenantMiddleware(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(handlerRan(), "a contradicting request reached the handler");
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        // The header must not have won by default. Before this fix it always did.
        Assert.False(tenantContext.HasTenant, "a tenant was resolved from a contradicting request");
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

    /// <summary>
    /// THE CONTROL. Without these, a middleware that refused EVERY request would pass the cases above.
    /// </summary>
    [Theory]
    [InlineData(true, true)]    // header and JWT agree
    [InlineData(false, true)]   // header only — no bearer token on the request
    public async Task Control_a_request_that_does_not_contradict_itself_passes_through(bool withJwt, bool withHeader)
    {
        var tenant = Guid.NewGuid();

        var (context, tenantContext, handlerRan) = await RunTenantMiddleware(
            withJwt ? tenant : null,
            withHeader ? tenant : null);

        Assert.True(handlerRan(), "a self-consistent request was refused");
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(tenantContext.HasTenant);
        Assert.Equal(tenant, tenantContext.TenantId);
    }

    /// <summary>
    /// THE SCOPE LINE, PINNED. A JWT with no header is the tenant-MISSING case, and BL-324 deliberately did not
    /// change it: the request still passes through with the tenant CLEARED, exactly as before. The JWT is read to
    /// DETECT a contradiction, not to become a second tenant source — that is a separate trust decision. If a
    /// later round makes the JWT a resolution source here, this test fails and says so out loud rather than
    /// letting the scope widen unnoticed.
    /// </summary>
    [Fact]
    public async Task Scope_jwt_without_a_header_is_still_the_missing_tenant_case_and_is_unchanged()
    {
        var (context, tenantContext, handlerRan) = await RunTenantMiddleware(Guid.NewGuid(), headerTenant: null);

        Assert.True(handlerRan(), "the missing-tenant path was changed by BL-324");
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.False(tenantContext.HasTenant);
    }

    /// <summary>Runs the REAL middleware over a request carrying the given tenant claim and/or header.</summary>
    private static async Task<(HttpContext Context, TenantContext Tenant, Func<bool> HandlerRan)>
        RunTenantMiddleware(Guid? jwtTenant, Guid? headerTenant)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/employees";
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
}
