using System.Security.Claims;
using System.Text.Json;
using Diten.DevEnablementService.Api.Controllers;
using Diten.DevEnablementService.Application.Common;
using Diten.DevEnablementService.Infrastructure.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.DevEnablementService.Api.Tests.WorkItemBridge;

/*
 * THE GUARD — CROSS-TENANT BEHAVIOUR AT THE WORK-ITEM BRIDGE (BL-323, owner decision 2026-08-29).
 *
 * WHY THIS FILE EXISTS. The bridge imposed nothing: RemoteWorkItemGateway passes the module's own verdict and
 * status code straight through, so the ANSWER to "you asked for another tenant's work" is given by the module,
 * and no line anywhere said what that answer should be. Two answers already existed in the repo, to two
 * different questions, and the next module would have invented a third. The rule is now:
 *
 *   1. header tenant ≠ JWT tenant            → 400. Malformed request; nothing to conceal.
 *   2. they agree, the RECORD is another tenant's → 404, as if it does not exist.
 *   3. NEVER 403 for case 2 — a 403 confirms the record exists, which is the leak 404 prevents.
 *
 * ⚠ WHAT THIS GUARD COVERS, AND WHAT IT CANNOT. It asserts the behaviour of OUR code: this service's tenant
 * middleware (case 1) and the reference work-item consumer (case 2), which DCP-004 §7.6 names as the shape
 * other teams copy. It CANNOT assert what PVG, Global SKU or any other team's endpoint answers — that code is
 * not in this repo and Platform cannot make it answer 404, because the gateway deliberately forwards a module's
 * status rather than rewriting it. For those modules the rule is documentation only, and DCP-004 §7.4 says so
 * in those words rather than implying coverage that does not exist.
 *
 * ⚠ NOT A VACUITY CHECK. Every case-2 assertion is DIFFERENTIAL: the same item id and the same action code must
 * come back 200 for the tenant that owns it and 404 for the tenant that does not. A controller that answered 404
 * to everything — including one switched OFF by `WorkItemReferenceProvider:Enabled=false`, which is exactly how
 * this test could have gone quietly green — fails the owner half of every pair.
 */
public sealed class CrossTenantContractGuardTests
{
    private const string TenantHeader = "X-Tenant-Id";

    // ── CASE 1 — CONTRADICTION IS A MALFORMED REQUEST (400), AND THE HANDLER NEVER RUNS ───────────────────

    [Fact]
    public async Task Case1_header_and_jwt_naming_different_tenants_is_refused_400_and_never_reaches_the_handler()
    {
        var jwtTenant = Guid.NewGuid();
        var headerTenant = Guid.NewGuid();

        var (context, tenantContext, handlerRan) = await RunTenantMiddleware(jwtTenant, headerTenant);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);

        // The handler must not run at all. If it did, it would read one of the two contradicting values and the
        // choice of which would be silent — the defect this refusal removes.
        Assert.False(handlerRan(), "a contradicting request reached the handler");
        Assert.False(tenantContext.IsResolved, "a tenant was resolved from a contradicting request");
    }

    [Fact]
    public async Task Case1_contradiction_is_refused_and_the_refusal_is_never_403_or_404()
    {
        var (context, _, handlerRan) = await RunTenantMiddleware(Guid.NewGuid(), Guid.NewGuid());

        // ⚠ This assertion first. Without it the test passes on a middleware that does NOTHING — a pass-through
        // answers 200, which is neither 403 nor 404, and the "never 403" claim would be true for the wrong
        // reason. Measured: that is exactly what happened when this guard was run against the pre-fix middleware.
        Assert.False(handlerRan(), "the contradiction was not refused at all");

        // 403 would be an access verdict on a request that cannot be evaluated at all; 404 would pretend the
        // ADDRESS is missing when it is the request that is malformed. Case 2's 404 is about a RECORD, not this.
        Assert.NotEqual(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.NotEqual(StatusCodes.Status404NotFound, context.Response.StatusCode);
    }

    /// <summary>
    /// THE CONTROL FOR CASE 1. Without these, a middleware that refused EVERY request would pass the test above.
    /// </summary>
    [Theory]
    [InlineData(true, true)]    // header and JWT agree
    [InlineData(true, false)]   // JWT only — Platform's own S2S shape before a header is written
    [InlineData(false, true)]   // header only — no bearer token yet
    public async Task Case1_control_a_request_that_does_not_contradict_itself_passes_through(bool withJwt, bool withHeader)
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

    // ── CASE 2 — ANOTHER TENANT'S RECORD DOES NOT EXIST FOR YOU (404), NOT "FORBIDDEN" (403) ──────────────

    [Fact]
    public void Case2_reading_never_shows_another_tenants_item()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var itemA = ReadTheOneItemId(tenantA);
        var itemB = ReadTheOneItemId(tenantB);

        // Both reads produced a real item — proves the surface is ON, so a later 404 means "not yours" and not
        // "switched off". This is the assertion that stops the whole case-2 block from passing vacuously.
        Assert.NotEqual(Guid.Empty, itemA);
        Assert.NotEqual(Guid.Empty, itemB);
        Assert.NotEqual(itemA, itemB);
    }

    [Fact]
    public void Case2_acting_on_another_tenants_item_is_404_and_the_owner_of_that_same_item_gets_200()
    {
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();

        var itemId = ReadTheOneItemId(owner);
        Assert.NotEqual(Guid.Empty, itemId);

        // Same item id, same action code, same body. ONLY the tenant differs.
        var strangerResult = Dispatch(stranger, itemId, "accept");
        var ownerResult = Dispatch(owner, itemId, "accept");

        Assert.Equal(StatusCodes.Status404NotFound, StatusOf(strangerResult));
        Assert.Equal(StatusCodes.Status200OK, StatusOf(ownerResult));
    }

    [Fact]
    public void Case2_the_refusal_is_never_403()
    {
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var itemId = ReadTheOneItemId(owner);

        var result = Dispatch(stranger, itemId, "accept");

        // The rule that is easiest to "simplify" back into a 403, because a 403 reads as the more precise answer.
        // It is the less safe one: it confirms the record exists to somebody who may not know that it does.
        Assert.NotEqual(StatusCodes.Status403Forbidden, StatusOf(result));

        // And the refusal carries the ordinary absent-record code — not a cross-tenant code of its own. A distinct
        // code would need a distinct sentence on screen, and that sentence would leak what the 404 conceals.
        Assert.Equal("REFERENCE_ITEM_NOT_FOUND", ReasonCodeOf(result));
    }

    [Fact]
    public void Case2_a_refused_cross_tenant_write_does_not_reach_the_other_tenants_record()
    {
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var itemId = ReadTheOneItemId(owner);

        var before = ReadStatusOfTheOneItem(owner);
        Dispatch(stranger, itemId, "accept");
        var after = ReadStatusOfTheOneItem(owner);

        // 404 must mean the write was not performed, not that it was performed and then denied a receipt.
        Assert.Equal("Pending", before);
        Assert.Equal(after, before);

        // Control: the owner's identical call DOES move it, so "unchanged" above is a tenancy fact and not a
        // dead endpoint.
        Dispatch(owner, itemId, "accept");
        Assert.Equal("InProgress", ReadStatusOfTheOneItem(owner));
    }

    // ── PLUMBING ─────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Runs the REAL middleware over a request carrying the given tenant claim and/or header.</summary>
    private static async Task<(HttpContext Context, TenantContext Tenant, Func<bool> HandlerRan)>
        RunTenantMiddleware(Guid? jwtTenant, Guid? headerTenant)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/v1/work-items/projection";
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

    /// <summary>The REAL controller, wired to the tenant header Platform would have written.</summary>
    private static ReferenceWorkItemProviderController ControllerFor(Guid tenant)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WorkItemReferenceProvider:Enabled"] = "true"
            })
            .Build();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[TenantHeader] = tenant.ToString();

        return new ReferenceWorkItemProviderController(configuration)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static Guid ReadTheOneItemId(Guid tenant)
        => Guid.Parse(TheOneItem(tenant).GetProperty("id").GetString()!);

    private static string ReadStatusOfTheOneItem(Guid tenant)
        => TheOneItem(tenant).GetProperty("normalizedStatus").GetString()!;

    private static JsonElement TheOneItem(Guid tenant)
    {
        var envelope = Wire(ControllerFor(tenant).GetProjection());
        var items = envelope.GetProperty("data").GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        return items[0];
    }

    private static IActionResult Dispatch(Guid tenant, Guid itemId, string actionCode)
        => ControllerFor(tenant).DispatchAction(itemId, actionCode, new ReferenceActionRequest("dev-reference", null));

    private static int StatusOf(IActionResult result)
        => ((IStatusCodeActionResult)result).StatusCode
           ?? throw new InvalidOperationException("the bridge answered without a status code");

    private static string? ReasonCodeOf(IActionResult result)
    {
        var wire = Wire(result);
        return wire.TryGetProperty("reason_code", out var code) && code.ValueKind == JsonValueKind.String
            ? code.GetString()
            : null;
    }

    /// <summary>
    /// The envelope AS IT GOES ON THE WIRE. Asserting the serialized shape rather than the C# object is the
    /// point: `reason_code` and the camelCase item fields are the contract, and a naming change that broke a
    /// consumer would still satisfy a property-level assertion.
    /// </summary>
    private static JsonElement Wire(IActionResult result)
    {
        var value = ((ObjectResult)result).Value
                    ?? throw new InvalidOperationException("the bridge answered with an empty body");

        return JsonSerializer.SerializeToElement(
            value,
            value.GetType(),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    private sealed class StubEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Diten.DevEnablementService.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
