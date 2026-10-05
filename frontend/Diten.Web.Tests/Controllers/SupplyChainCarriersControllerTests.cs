using System.Net;
using System.Security.Claims;
using System.Text;
using Diten.Web.Controllers;
using Diten.Web.Models.SupplyChain.Carriers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

public sealed class SupplyChainCarriersControllerTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid LegalEntityId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid CorrelationId = Guid.Parse("33333333-3333-4333-8333-333333333333");

    [Fact]
    public async Task List_ForwardsOnlyExactStatusThroughGatewayAndPreservesWireResponse()
    {
        var downstreamBody = "{\"items\":[],\"total\":0,\"contractVersion\":\"v1\"}";
        var handler = new CaptureHandler(_ => Response(HttpStatusCode.OK, downstreamBody));
        var controller = CreateController(handler, "supplychain.carriers.read");

        var result = await controller.List("Active", CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status200OK, content.StatusCode);
        Assert.Equal(downstreamBody, content.Content);
        Assert.Equal("http://localhost:5000/api/shipment-bundle/carriers?status=Active", handler.Uri);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("test-token", handler.Authorization);
        Assert.Equal(TenantId.ToString("D"), handler.Headers["X-Tenant-Id"]);
        Assert.Equal(LegalEntityId.ToString("D"), handler.Headers["X-Legal-Entity-Id"]);
        Assert.Equal(CorrelationId.ToString("D"), handler.Headers["X-Correlation-Id"]);
        Assert.Equal(CorrelationId.ToString("D"), controller.Response.Headers["X-Correlation-Id"]);
    }

    [Fact]
    public async Task Create_PreservesFourFieldPayloadIdempotencyAndExactErrorBody()
    {
        const string errorBody = "{\"error\":{\"code\":\"CARRIER_CODE_CONFLICT\",\"message\":\"Carrier code is already reserved.\",\"correlationId\":\"33333333-3333-4333-8333-333333333333\"},\"contractVersion\":\"v1\"}";
        var handler = new CaptureHandler(_ => Response(HttpStatusCode.Conflict, errorBody));
        var controller = CreateController(handler, "supplychain.carriers.create", includeIdempotencyKey: true);
        var model = new CreateCarrierViewModel
        {
            CarrierCode = "  C-01  ",
            DisplayName = "  Carrier One  ",
            SupportedModes = ["Road", "Road", "Air"],
            ExternalReference = string.Empty
        };

        var result = await controller.Create(model, CancellationToken.None);

        var content = Assert.IsType<ContentResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, content.StatusCode);
        Assert.Equal(errorBody, content.Content);
        Assert.Equal("intent-key", handler.Headers["Idempotency-Key"]);
        Assert.Contains("\"carrierCode\":\"  C-01  \"", handler.Body);
        Assert.Contains("\"displayName\":\"  Carrier One  \"", handler.Body);
        Assert.Contains("\"supportedModes\":[\"Road\",\"Road\",\"Air\"]", handler.Body);
        Assert.Contains("\"externalReference\":\"\"", handler.Body);
        Assert.DoesNotContain("tenantId", handler.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("legalEntityId", handler.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("status", handler.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChangeStatus_AllowsEmptyReasonAndPreservesExactStatusWireBody()
    {
        var carrierId = Guid.Parse("44444444-4444-4444-8444-444444444444");
        var handler = new CaptureHandler(_ => Response(HttpStatusCode.OK,
            "{\"carrierId\":\"44444444-4444-4444-8444-444444444444\",\"carrierCode\":\"C-01\",\"status\":\"Suspended\",\"idempotentReplay\":true,\"contractVersion\":\"v1\"}"));
        var controller = CreateController(handler, "supplychain.carriers.status.change", includeIdempotencyKey: true);

        var result = await controller.ChangeStatus(carrierId,
            new ChangeCarrierStatusViewModel { TargetStatus = "Suspended", ReasonCode = string.Empty },
            CancellationToken.None);

        Assert.IsType<ContentResult>(result);
        Assert.Equal($"http://localhost:5000/api/shipment-bundle/carriers/{carrierId:D}/status", handler.Uri);
        Assert.Equal("{\"targetStatus\":\"Suspended\",\"reasonCode\":\"\"}", handler.Body);
    }

    [Fact]
    public async Task AdapterPermissionsAreIndependentAndDenyBeforeGateway()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("Gateway must not be called."));
        var controller = CreateController(handler, "supplychain.carriers.read", includeIdempotencyKey: true);

        var create = await controller.Create(new CreateCarrierViewModel
        {
            CarrierCode = "C-01", DisplayName = "Carrier", SupportedModes = ["Road"]
        }, CancellationToken.None);
        var status = await controller.ChangeStatus(Guid.NewGuid(),
            new ChangeCarrierStatusViewModel { TargetStatus = "Suspended", ReasonCode = "" },
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(create).StatusCode);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(status).StatusCode);
        Assert.Equal(0, handler.CallCount);
    }

    // R-4a (MODULE-RECIPE 2.1, Q371): a gateway 5xx with no contract body (service down) must reach the page as
    // PERSISTENCE_UNAVAILABLE, not as an empty body; a 5xx that carries the contract envelope passes through untouched.
    [Theory]
    [InlineData(HttpStatusCode.BadGateway, "")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "")]
    [InlineData(HttpStatusCode.GatewayTimeout, "<html>gateway timeout</html>")]
    public async Task UpstreamServerFailureWithoutContractBody_BecomesPersistenceUnavailable(HttpStatusCode upstream, string body)
    {
        var handler = new CaptureHandler(_ => Response(upstream, body));
        var controller = CreateController(handler, "supplychain.carriers.read");
        var failure = Assert.IsType<ObjectResult>(await controller.List(null, CancellationToken.None));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, failure.StatusCode);
        Assert.Contains("PERSISTENCE_UNAVAILABLE", System.Text.Json.JsonSerializer.Serialize(failure.Value));
        Assert.Contains(CorrelationId.ToString("D"), System.Text.Json.JsonSerializer.Serialize(failure.Value));
    }

    [Fact]
    public async Task UpstreamServerFailureWithContractBody_PassesThroughUnchanged()
    {
        const string envelope = "{\"error\":{\"code\":\"INTERNAL_ERROR\",\"message\":\"x\",\"correlationId\":\"33333333-3333-4333-8333-333333333333\"},\"contractVersion\":\"v1\"}";
        var handler = new CaptureHandler(_ => Response(HttpStatusCode.InternalServerError, envelope));
        var controller = CreateController(handler, "supplychain.carriers.read");
        var result = Assert.IsType<ContentResult>(await controller.List(null, CancellationToken.None));
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        Assert.Equal(envelope, result.Content);
    }

    // R-4a (MODULE-RECIPE 2.4): the rescued adapter carried no [JsonAdapterEndpoint], so an unauthenticated call to
    // /SupplyChain/Carriers/api was answered 302 to the login page instead of JSON 401 (measured live). Every api action
    // must carry the marker Q394's cookie challenge reads.
    [Fact]
    public void EveryAdapterActionCarriesTheJsonAdapterMarker()
    {
        var actions = typeof(SupplyChainCarriersController).GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute), true)
                .Cast<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().Any(h => h.Template?.StartsWith("api", StringComparison.Ordinal) == true))
            .ToArray();
        Assert.Equal(3, actions.Length);
        Assert.All(actions, m => Assert.True(m.IsDefined(typeof(Diten.Web.Security.JsonAdapterEndpointAttribute), true), m.Name));
    }

    private static SupplyChainCarriersController CreateController(
        CaptureHandler handler,
        string permission,
        bool includeIdempotencyKey = false)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["GatewayUrl"] = "http://localhost:5000" })
            .Build();
        var controller = new SupplyChainCarriersController(
            new HttpClient(handler), configuration, NullLogger<SupplyChainCarriersController>.Instance);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "carrier-tester"),
            new Claim("tenant_id", TenantId.ToString("D")),
            new Claim("legal_entity_id", LegalEntityId.ToString("D")),
            new Claim("permission", permission)
        };
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        context.Request.Headers.Cookie = "access_token=test-token";
        context.Request.Headers["X-Correlation-Id"] = CorrelationId.ToString("D");
        if (includeIdempotencyKey) context.Request.Headers["Idempotency-Key"] = "intent-key";
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string body)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        response.Headers.TryAddWithoutValidation("X-Correlation-Id", CorrelationId.ToString("D"));
        return response;
    }

    private sealed class CaptureHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string Uri { get; private set; } = string.Empty;
        public HttpMethod? Method { get; private set; }
        public string? Authorization { get; private set; }
        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            Uri = request.RequestUri?.ToString() ?? string.Empty;
            Method = request.Method;
            Authorization = request.Headers.Authorization?.Parameter;
            foreach (var header in request.Headers)
                Headers[header.Key] = string.Join(',', header.Value);
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }
}
