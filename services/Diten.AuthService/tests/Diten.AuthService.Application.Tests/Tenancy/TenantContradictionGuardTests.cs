using System.Security.Claims;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.AuthService.Application.Tests.Tenancy;

/*
 * THE GUARD — TENANT CONTRADICTION IS REFUSED IN AuthService (BL-324, rule from BL-323 / DCP-004 §7.4).
 *
 * WHAT WAS WRONG. This middleware DID read the JWT, and on a contradiction it logged
 * "Tenant conflict in AuthService. JWT tenant wins" and CARRIED ON. A warning is not a refusal: the request still
 * ran, and a log line nobody reads is not a control. The rule is 400.
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

        // The JWT must not have "won" any more. Before this fix it silently did.
        Assert.False(tenantContext.IsResolved, "a tenant was resolved from a contradicting request");
    }

    /// <summary>
    /// THE MEDIA TYPE, ON THE WIRE. Until 2026-08-30 this refusal DECLARED problem+json in the source and
    /// answered "application/json; charset=utf-8" to the caller: WriteProblemDetails assigned
    /// <c>Response.ContentType</c> and then called <c>WriteAsJsonAsync</c>, which overwrites it
    /// UNCONDITIONALLY. The declaration only survives as WriteAsJsonAsync's <c>contentType</c> PARAMETER.
    ///
    /// <para>⚠ This is asserted because the source cannot be read for it. Someone "tidying" the helper back
    /// into a <c>Response.ContentType = ...</c> assignment would leave every other test in this file green
    /// and silently put the wrong media type back on the wire — which is exactly how it went unnoticed the
    /// first time.</para>
    /// </summary>
    [Fact]
    public async Task The_refusal_declares_problem_json_on_the_wire()
    {
        var (context, _, handlerRan) = await RunTenantMiddleware(Guid.NewGuid(), Guid.NewGuid());

        // Vacuity guard: a middleware that never refused would leave ContentType null and "not json" would
        // be true for the wrong reason.
        Assert.False(handlerRan(), "the contradiction was not refused at all");
        Assert.Equal("application/problem+json", context.Response.ContentType);
    }

    [Fact]
    public async Task Contradiction_is_refused_and_the_refusal_is_never_403_or_404()
    {
        var (context, _, handlerRan) = await RunTenantMiddleware(Guid.NewGuid(), Guid.NewGuid());

        // ⚠ This assertion FIRST. Without it the test passes on a middleware that does nothing: a pass-through
        // answers 200, which is neither 403 nor 404, and the "never 403" claim would hold for the wrong reason.
        // Measured against the pre-fix middleware, that is exactly how this guard went green.
        Assert.False(handlerRan(), "the contradiction was not refused at all");

        // 403 would be an access verdict on a request that cannot be evaluated at all; 404 would pretend the
        // ADDRESS is missing when it is the REQUEST that is malformed. DCP-004 §7.4 case 2's 404 is about a
        // RECORD, and is a different question from this one.
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.NotEqual(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    /// <summary>
    /// THE CONTROL. Without these, a middleware that refused EVERY request would pass the cases above.
    /// AuthService resolves `jwtTenant ?? headerTenant`, so all three self-consistent shapes must still resolve.
    /// </summary>
    [Theory]
    [InlineData(true, true)]    // header and JWT agree
    [InlineData(true, false)]   // JWT only — no header written yet
    [InlineData(false, true)]   // header only — no bearer token on the request
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
    /// THE SCOPE LINE, PINNED. A contradiction needs BOTH values, so it cannot arise on the public auth paths:
    /// at login there is no bearer token yet, hence no JWT tenant, hence nothing to contradict. This pins that
    /// login is untouched by BL-324 — a refusal that broke login would be a far worse defect than the one fixed.
    /// </summary>
    [Fact]
    public async Task Scope_login_carries_no_token_so_a_header_alone_still_passes()
    {
        var (context, tenantContext, handlerRan) = await RunTenantMiddleware(
            jwtTenant: null,
            headerTenant: Guid.NewGuid(),
            path: "/api/tenant-auth/login");

        Assert.True(handlerRan(), "BL-324 broke the login path");
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.True(tenantContext.IsResolved);
    }

    /// <summary>Runs the REAL middleware over a request carrying the given tenant claim and/or header.</summary>
    private static async Task<(HttpContext Context, TenantContext Tenant, Func<bool> HandlerRan)>
        RunTenantMiddleware(Guid? jwtTenant, Guid? headerTenant, string path = "/api/users")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
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
            NullLogger<TenantResolutionMiddleware>.Instance,
            // Empty configuration and a non-Development environment: the dev bypass must not stand in for a
            // tenant and quietly turn a refusal into a pass.
            new ConfigurationBuilder().Build(),
            new StubEnvironment());

        var tenantContext = new TenantContext();
        await middleware.InvokeAsync(context, tenantContext);

        return (context, tenantContext, () => ran);
    }

    private sealed class StubEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Diten.AuthService.Application.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
