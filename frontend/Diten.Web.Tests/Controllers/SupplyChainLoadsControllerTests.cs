using System.Net;
using System.Security.Claims;
using System.Text;
using Diten.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

// MOD-0185 Loads adapter (R-4b). The create body travels as the exact text the browser sent; the browser's X-Correlation-Id
// travels unchanged because it is the new Load's root (MODULE-RECIPE 3.2); a request without one is refused before the
// Gateway (2.2); an un-enveloped 5xx becomes 503 PERSISTENCE_UNAVAILABLE (2.1); every api action carries the JSON marker (2.4).
public sealed class SupplyChainLoadsControllerTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid LegalEntity = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Correlation = Guid.Parse("33333333-3333-4333-8333-333333333333");

    [Fact]
    public async Task List_ForwardsOnlyThePublishedQueryAndScope()
    {
        var handler = new CaptureHandler(_ => Response(HttpStatusCode.OK, "{\"items\":[],\"total\":0,\"contractVersion\":\"v1\"}"));
        var controller = CreateController(handler, ["supplychain.loads.read"]);
        var result = Assert.IsType<ContentResult>(await controller.List("Draft", "44444444-4444-4444-8444-444444444444", CancellationToken.None));
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal("http://localhost:5000/api/shipment-bundle/loads?status=Draft&carrierId=44444444-4444-4444-8444-444444444444", handler.Uri);
        Assert.Equal(Tenant.ToString("D"), handler.Headers["X-Tenant-Id"]);
        Assert.Equal(LegalEntity.ToString("D"), handler.Headers["X-Legal-Entity-Id"]);
        Assert.Equal(Correlation.ToString("D"), handler.Headers["X-Correlation-Id"]);
    }

    [Fact]
    public async Task Create_ForwardsExactBodyKeyAndRootCorrelation()
    {
        const string body = "{\"carrierId\":\"44444444-4444-4444-8444-444444444444\",\"shipmentIds\":[\"55555555-5555-4555-8555-555555555555\"],\"mode\":\"Road\",\"plannedDepartAt\":\"2026-10-05T06:00:00.000Z\",\"stops\":[{\"sequence\":1,\"locationReferenceId\":\"\",\"action\":\"Pickup\"},{\"sequence\":2,\"locationReferenceId\":\"LOC-2\",\"action\":\"Delivery\"}]}";
        var handler = new CaptureHandler(_ => Response(HttpStatusCode.Created, "{\"loadId\":\"66666666-6666-4666-8666-666666666666\"}"));
        var controller = CreateController(handler, ["supplychain.loads.create"], body, includeKey: true);
        var result = Assert.IsType<ContentResult>(await controller.Create(CancellationToken.None));
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.Equal(body, handler.Body);
        Assert.Equal("intent-key", handler.Headers["Idempotency-Key"]);
        Assert.Equal(Correlation.ToString("D"), handler.Headers["X-Correlation-Id"]);
    }

    [Fact]
    public async Task Create_WithoutCorrelationOrKeyOrPermission_IsRefusedBeforeTheGateway()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("Gateway must not be called."));
        var noCorrelation = CreateController(handler, ["supplychain.loads.create"], "{}", includeKey: true, includeCorrelation: false);
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(await noCorrelation.Create(CancellationToken.None)).StatusCode);
        var noKey = CreateController(handler, ["supplychain.loads.create"], "{}", includeKey: false);
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(await noKey.Create(CancellationToken.None)).StatusCode);
        var readOnly = CreateController(handler, ["supplychain.loads.read"], "{}", includeKey: true);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(await readOnly.Create(CancellationToken.None)).StatusCode);
        Assert.Equal(0, handler.CallCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway, "")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "")]
    [InlineData(HttpStatusCode.GatewayTimeout, "<html>gateway timeout</html>")]
    public async Task UpstreamServerFailureWithoutContractBody_BecomesPersistenceUnavailable(HttpStatusCode upstream, string body)
    {
        var handler = new CaptureHandler(_ => Response(upstream, body));
        var controller = CreateController(handler, ["supplychain.loads.read"]);
        var failure = Assert.IsType<ObjectResult>(await controller.List(null, null, CancellationToken.None));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, failure.StatusCode);
        Assert.Contains("PERSISTENCE_UNAVAILABLE", System.Text.Json.JsonSerializer.Serialize(failure.Value));
    }

    [Fact]
    public async Task UpstreamServerFailureWithContractBody_PassesThroughUnchanged()
    {
        const string envelope = "{\"error\":{\"code\":\"DEPENDENCY_UNAVAILABLE\",\"message\":\"x\",\"correlationId\":\"33333333-3333-4333-8333-333333333333\"},\"contractVersion\":\"v1\"}";
        var handler = new CaptureHandler(_ => Response(HttpStatusCode.ServiceUnavailable, envelope));
        var result = Assert.IsType<ContentResult>(await CreateController(handler, ["supplychain.loads.read"]).List(null, null, CancellationToken.None));
        Assert.Equal(envelope, result.Content);
    }

    [Fact]
    public void EveryAdapterActionCarriesTheJsonAdapterMarker_AndThereIsNoTransitionRoute()
    {
        var actions = typeof(SupplyChainLoadsController).GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(HttpMethodAttribute), true).Cast<HttpMethodAttribute>()
                .Any(h => h.Template?.StartsWith("api", StringComparison.Ordinal) == true)).ToArray();
        Assert.Equal(2, actions.Length);
        Assert.All(actions, m => Assert.True(m.IsDefined(typeof(Diten.Web.Security.JsonAdapterEndpointAttribute), true), m.Name));
        Assert.DoesNotContain(typeof(SupplyChainLoadsController).GetMethods().SelectMany(m => m.GetCustomAttributes(typeof(HttpMethodAttribute), true).Cast<HttpMethodAttribute>()),
            h => h.Template?.Contains("transition", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static SupplyChainLoadsController CreateController(CaptureHandler handler, string[] permissions, string? body = null,
        bool includeKey = false, bool includeCorrelation = true)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://localhost:5000" }).Build();
        var controller = new SupplyChainLoadsController(new HttpClient(handler), configuration, NullLogger<SupplyChainLoadsController>.Instance);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "load-tester"), new("tenant_id", Tenant.ToString("D")), new("legal_entity_id", LegalEntity.ToString("D")) };
        claims.AddRange(permissions.Select(p => new Claim("permission", p)));
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        context.Request.Headers.Cookie = "access_token=test-token";
        if (includeCorrelation) context.Request.Headers["X-Correlation-Id"] = Correlation.ToString("D");
        if (includeKey) context.Request.Headers["Idempotency-Key"] = "intent-key";
        if (body is not null)
        {
            context.Request.ContentType = "application/json";
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string body)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        response.Headers.TryAddWithoutValidation("X-Correlation-Id", Correlation.ToString("D"));
        return response;
    }

    private sealed class CaptureHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string Uri { get; private set; } = string.Empty;
        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string Body { get; private set; } = string.Empty;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++; Uri = request.RequestUri?.ToString() ?? string.Empty;
            foreach (var header in request.Headers) Headers[header.Key] = string.Join(',', header.Value);
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }
}
