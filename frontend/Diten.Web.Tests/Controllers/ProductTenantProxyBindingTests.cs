using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Diten.Web.Controllers;
using Diten.Web.Models.ProductLegalEntityScopes;
using Diten.Web.Views.MasterDataManagement.Gskus;
using Diten.Web.Views.MasterDataManagement.Lskus;
using Diten.Web.Views.MasterDataManagement.ProductLegalEntityScopes;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// PRODUCT-FIVE-TENANT-BINDING-PROOF-01 B-level evidence. Each case calls the real MVC action and captures its
/// outbound HTTP request. This does not run Cookie authentication, endpoint authorization, Gateway middleware,
/// or an MDM repository; those C-level decisions must not be inferred from this controller-only seam.
/// </summary>
public sealed class ProductTenantProxyBindingTests
{
    [Fact]
    public async Task Canonical_tenant_claim_with_product_read_permissions_sends_one_bound_header_from_each_real_action()
    {
        var tenant = Guid.NewGuid();
        var runs = await RunAllAsync(
            [
                new Claim("tenant_id", tenant.ToString("D")),
                new Claim("actor_type", "tenant_user"),
                new Claim("permission", "mdm.gskus.read"),
                new Claim("permission", "mdm.lskus.read"),
                new Claim("permission", "mdm.product-legal-entity-scopes.read")
            ]);

        Assert.All(runs, run =>
        {
            Assert.IsType<ContentResult>(run.Result);
            Assert.True(run.Wire.Called);
            Assert.Equal(tenant.ToString("D"), Assert.Single(run.Wire.TenantHeaders));
            Assert.Equal("Bearer", run.Wire.Authorization?.Scheme);
        });
    }

    [Fact]
    public async Task Missing_or_malformed_tenant_claim_does_not_create_an_outbound_product_request()
    {
        foreach (var claims in new[]
                 {
                     WithProductReadPermissions(new Claim("actor_type", "tenant_user")),
                     WithProductReadPermissions(new Claim("tenant_id", "not-a-guid"), new Claim("actor_type", "tenant_user"))
                 })
        {
            var runs = await RunAllAsync(claims);
            Assert.All(runs, run =>
            {
                Assert.False(run.Wire.Called);
                Assert.IsNotType<ContentResult>(run.Result);
            });
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("service")]
    public async Task Missing_or_invalid_actor_type_does_not_change_the_real_controller_outbound_decision(string? actorType)
    {
        var tenant = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new("tenant_id", tenant.ToString("D")),
            new("permission", "mdm.gskus.read"),
            new("permission", "mdm.lskus.read"),
            new("permission", "mdm.product-legal-entity-scopes.read")
        };
        if (actorType is not null) claims.Add(new Claim("actor_type", actorType));

        var runs = await RunAllAsync(claims);

        // This is deliberately an observation, not a new MVC actor policy: these actions enforce permissions
        // and tenant-header construction only. The Gateway/endpoint C-level authorization decision is unmeasured
        // by this direct-controller seam.
        Assert.All(runs, run =>
        {
            Assert.IsType<ContentResult>(run.Result);
            Assert.True(run.Wire.Called);
            Assert.Equal(tenant.ToString("D"), Assert.Single(run.Wire.TenantHeaders));
        });
    }

    [Fact]
    public async Task Conflicting_canonical_and_legacy_tenant_aliases_do_not_create_an_outbound_product_request()
    {
        var runs = await RunAllAsync(
            [
                new Claim("tenant_id", Guid.NewGuid().ToString("D")),
                new Claim("tenantId", Guid.NewGuid().ToString("D")),
                new Claim("actor_type", "tenant_user"),
                new Claim("permission", "mdm.gskus.read"),
                new Claim("permission", "mdm.lskus.read"),
                new Claim("permission", "mdm.product-legal-entity-scopes.read")
            ]);

        // The outbound seam must reject ambiguous tenant identity. A controller that merely forwards the first
        // claim would make a real gateway call before the downstream trust boundary can decide anything.
        Assert.All(runs, run =>
        {
            Assert.False(run.Wire.Called);
            // GSKU deliberately returns its envelope-bearing UnauthorizedObjectResult while LSKU/Scope use
            // UnauthorizedResult. The tenant-binding contract here is fail-closed/no outbound, not an invented
            // requirement that all three MVC surfaces share one result subtype.
            Assert.IsNotType<ContentResult>(run.Result);
        });
    }

    [Fact]
    public async Task Duplicate_canonical_tenant_claims_do_not_create_an_outbound_product_request()
    {
        var tenant = Guid.NewGuid();
        var runs = await RunAllAsync(
            [
                new Claim("tenant_id", tenant.ToString("D")),
                new Claim("tenant_id", tenant.ToString("D")),
                new Claim("actor_type", "tenant_user"),
                new Claim("permission", "mdm.gskus.read"),
                new Claim("permission", "mdm.lskus.read"),
                new Claim("permission", "mdm.product-legal-entity-scopes.read")
            ]);

        Assert.All(runs, run =>
        {
            Assert.False(run.Wire.Called);
            Assert.IsNotType<ContentResult>(run.Result);
        });
    }

    [Fact]
    public async Task Legacy_only_tenant_alias_sends_a_bound_header_from_each_real_product_action()
    {
        var tenant = Guid.NewGuid();
        var runs = await RunAllAsync(WithProductReadPermissions(
            new Claim("tenantId", tenant.ToString("D")), new Claim("actor_type", "tenant_user")));

        Assert.All(runs, run => AssertOutboundBoundToTenant(run, tenant));
    }

    [Fact]
    public async Task Equal_canonical_and_legacy_tenant_aliases_send_a_bound_header_from_each_real_product_action()
    {
        var tenant = Guid.NewGuid();
        var runs = await RunAllAsync(WithProductReadPermissions(
            new Claim("tenant_id", tenant.ToString("D")),
            new Claim("tenantId", tenant.ToString("D")),
            new Claim("actor_type", "tenant_user")));

        Assert.All(runs, run => AssertOutboundBoundToTenant(run, tenant));
    }

    [Fact]
    public async Task Duplicate_legacy_tenant_aliases_do_not_create_an_outbound_product_request()
    {
        var tenant = Guid.NewGuid();
        var runs = await RunAllAsync(WithProductReadPermissions(
            new Claim("tenantId", tenant.ToString("D")),
            new Claim("tenantId", tenant.ToString("D")),
            new Claim("actor_type", "tenant_user")));

        Assert.All(runs, run =>
        {
            Assert.False(run.Wire.Called, $"{run.Surface} made an outbound call for duplicate legacy claims.");
            Assert.IsNotType<ContentResult>(run.Result);
        });
    }

    [Fact]
    public async Task Conflicting_legacy_tenant_aliases_do_not_create_an_outbound_product_request()
    {
        var runs = await RunAllAsync(WithProductReadPermissions(
            new Claim("tenantId", Guid.NewGuid().ToString("D")),
            new Claim("https://issuer.example/tenantId", Guid.NewGuid().ToString("D")),
            new Claim("actor_type", "tenant_user")));

        Assert.All(runs, run =>
        {
            Assert.False(run.Wire.Called, $"{run.Surface} made an outbound call for conflicting legacy claims.");
            Assert.IsNotType<ContentResult>(run.Result);
        });
    }

    private static async Task<IReadOnlyList<ProxyRun>> RunAllAsync(IReadOnlyCollection<Claim> claims)
    {
        var gateway = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["GatewayUrl"] = "https://gateway-fixture.invalid" }).Build();
        var protection = new EphemeralDataProtectionProvider();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "fixture"));

        var gskuWire = new RecordingHandler();
        var gsku = new GskusController(new HttpClient(gskuWire), gateway, protection,
            new Localizer<Diten.Web.SharedResource>(), new Localizer<GskusIndex>(), NullLogger<GskusController>.Instance)
        { ControllerContext = Context(principal) };

        var lskuWire = new RecordingHandler();
        var lsku = new LskusController(new HttpClient(lskuWire), gateway, protection,
            new Localizer<LskusIndex>(), NullLogger<LskusController>.Instance)
        { ControllerContext = Context(principal) };

        var scopeWire = new RecordingHandler();
        var scope = new ProductLegalEntityScopesController(new HttpClient(scopeWire), gateway, protection,
            new Localizer<ProductLegalEntityScopesIndex>())
        { ControllerContext = Context(principal) };

        return
        [
            new ProxyRun("GskusController.List", await gsku.List(CancellationToken.None), gskuWire),
            new ProxyRun("LskusController.List", await lsku.List(CancellationToken.None), lskuWire),
            new ProxyRun("ProductLegalEntityScopesController.Products", await scope.Products(CancellationToken.None), scopeWire)
        ];
    }

    private static void AssertOutboundBoundToTenant(ProxyRun run, Guid tenant)
    {
        Assert.IsType<ContentResult>(run.Result);
        Assert.True(run.Wire.Called, $"{run.Surface} did not call the outbound HTTP handler.");
        Assert.Equal(tenant.ToString("D"), Assert.Single(run.Wire.TenantHeaders));
    }

    private static Claim[] WithProductReadPermissions(params Claim[] identityClaims) =>
    [
        .. identityClaims,
        new Claim("permission", "mdm.gskus.read"),
        new Claim("permission", "mdm.lskus.read"),
        new Claim("permission", "mdm.product-legal-entity-scopes.read")
    ];

    private static ControllerContext Context(ClaimsPrincipal principal)
    {
        var context = new DefaultHttpContext { User = principal };
        // Synthetic fixture credential: it is only captured by RecordingHandler and never sent to a network.
        context.Request.Headers.Cookie = "access_token=synthetic-product-tenant-binding-proof";
        return new ControllerContext { HttpContext = context };
    }

    private sealed record ProxyRun(string Surface, IActionResult Result, RecordingHandler Wire);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public bool Called { get; private set; }
        public AuthenticationHeaderValue? Authorization { get; private set; }
        public List<string> TenantHeaders { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Called = true;
            Authorization = request.Headers.Authorization;
            if (request.Headers.TryGetValues("X-Tenant-Id", out var tenants))
                TenantHeaders.AddRange(tenants);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"isSuccessful\":true,\"data\":{}}")
            });
        }
    }

    private sealed class Localizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
