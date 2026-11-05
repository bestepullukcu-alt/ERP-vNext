using System.Net;
using System.Security.Claims;
using System.Text;
using Diten.Web.Controllers;
using Diten.Web.Models.SupplyChain.Shipments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

public sealed class SupplyChainShipmentsControllerTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid LegalEntity = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Correlation = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid Shipment = Guid.Parse("44444444-4444-4444-8444-444444444444");

    [Fact]
    public async Task List_ForwardsOnlyFrozenQueryAndScopeThroughGateway()
    {
        var handler = new CaptureHandler(_ => Response(HttpStatusCode.OK, "{\"items\":[],\"total\":0}"));
        var controller = CreateController(handler, ["supplychain.shipments.read"]);
        var result = await controller.List("InTransit", "SO 1", 2, 25, CancellationToken.None);
        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<ContentResult>(result).StatusCode);
        Assert.Equal("http://localhost:5000/api/shipment-bundle/shipments?page=2&pageSize=25&status=InTransit&sourceDocumentId=SO 1", handler.Uri);
        Assert.Equal(Tenant.ToString("D"), handler.Headers["X-Tenant-Id"]);
        Assert.Equal(LegalEntity.ToString("D"), handler.Headers["X-Legal-Entity-Id"]);
        Assert.Equal(Correlation.ToString("D"), handler.Headers["X-Correlation-Id"]);
    }

    // Q371: a Gateway 5xx with no contract body (service down) must reach the page as PERSISTENCE_UNAVAILABLE, not as an
    // empty body the page reads as a validation error; a 5xx that carries the contract envelope passes through untouched.
    [Theory]
    [InlineData(HttpStatusCode.BadGateway, "")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "")]
    [InlineData(HttpStatusCode.GatewayTimeout, "<html>gateway timeout</html>")]
    public async Task UpstreamServerFailureWithoutContractBody_BecomesPersistenceUnavailable(HttpStatusCode upstream, string body)
    {
        var handler = new CaptureHandler(r => IsCrm(r) ? Response(HttpStatusCode.OK, AccountJson) : Response(upstream, body));
        var controller = CreateController(handler, ["supplychain.shipments.create", "crm.account.read"], true);
        var result = await controller.Create(NewShipment("1"), CancellationToken.None);
        var failure = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, failure.StatusCode);
        Assert.Contains("PERSISTENCE_UNAVAILABLE", System.Text.Json.JsonSerializer.Serialize(failure.Value));
        Assert.Contains(Correlation.ToString("D"), System.Text.Json.JsonSerializer.Serialize(failure.Value));
    }

    [Fact]
    public async Task UpstreamServerFailureWithContractBody_PassesThroughUnchanged()
    {
        const string envelope = "{\"error\":{\"code\":\"INTERNAL_ERROR\",\"message\":\"x\",\"correlationId\":\"33333333-3333-4333-8333-333333333333\"},\"contractVersion\":\"v1\"}";
        var handler = new CaptureHandler(_ => Response(HttpStatusCode.InternalServerError, envelope));
        var controller = CreateController(handler, ["supplychain.shipments.read"]);
        var result = Assert.IsType<ContentResult>(await controller.List(null, null, 1, 10, CancellationToken.None));
        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        Assert.Equal(envelope, result.Content);
    }

    [Fact]
    public async Task Create_PreservesExactWireFieldsQuantityAndStableIntentHeader()
    {
        var handler = new CaptureHandler(r => IsCrm(r) ? Response(HttpStatusCode.OK, AccountJson)
            : Response(HttpStatusCode.Created, "{\"shipmentId\":\"44444444-4444-4444-8444-444444444444\"}"));
        var controller = CreateController(handler, ["supplychain.shipments.create", "crm.account.read"], true);
        var result = await controller.Create(NewShipment("1.250"), CancellationToken.None);
        Assert.IsType<ContentResult>(result);
        Assert.Equal("intent-key", handler.Headers["Idempotency-Key"]);
        Assert.Contains("\"quantity\":\"1.250\"", handler.Body);
        Assert.Contains("\"plannedDeliverAt\":null", handler.Body);
        Assert.DoesNotContain("tenantId", handler.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("legalEntityId", handler.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correlationId", handler.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Transition_UsesCancelOnlyForCancelledAndDispatchForEveryOtherTarget()
    {
        var deniedHandler = new CaptureHandler(_ => throw new InvalidOperationException());
        var dispatchOnly = CreateController(deniedHandler, ["supplychain.shipments.dispatch"], true);
        var cancelled = await dispatchOnly.Transition(Shipment, new TransitionShipmentViewModel
        { TargetStatus = "Cancelled", OccurredAt = DateTimeOffset.UtcNow }, CancellationToken.None);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(cancelled).StatusCode);
        Assert.Equal(0, deniedHandler.CallCount);

        var handler = new CaptureHandler(_ => Response(HttpStatusCode.OK, "{}"));
        var controller = CreateController(handler, ["supplychain.shipments.dispatch"], true);
        await controller.Transition(Shipment, new TransitionShipmentViewModel
        { TargetStatus = "Dispatched", OccurredAt = DateTimeOffset.Parse("2026-09-25T08:00:00Z") }, CancellationToken.None);
        Assert.EndsWith($"/{Shipment:D}/transition", handler.Uri);
    }

    [Fact]
    public async Task MissingOrMalformedScopeAndHeadersFailBeforeGateway()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException());
        var controller = CreateController(handler, ["supplychain.shipments.pod.capture"], includeIntent: false);
        var result = await controller.CapturePod(Shipment, new CaptureShipmentPodViewModel
        { RecipientName = "Receiver", ReceivedAt = DateTimeOffset.UtcNow, EvidenceReferenceIds = ["REF"] }, CancellationToken.None);
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal(0, handler.CallCount);
    }

    internal const string Customer = "14900000-0000-4000-8000-000000000001";
    internal const string AccountJson = """{"data":{"id":"14900000-0000-4000-8000-000000000001","accountName":"Acme","accountCode":"C-1","countryRef":"TR","cityRef":"Istanbul","districtRef":null,"addressLine":"Ataturk Cad. No 12","responsiblePersonEmail":"buyer@example.test"},"isSuccessful":true,"statusCode":200}""";
    internal static bool IsCrm(HttpRequestMessage request) => request.RequestUri!.AbsolutePath.StartsWith("/api/crm/", StringComparison.Ordinal);
    internal static CreateShipmentViewModel NewShipment(string quantity, string? customerRef = Customer) => new()
    {
        SourceDocumentId = "SO-1", WarehouseReferenceId = "WH-1", CustomerRef = customerRef, PlannedShipAt = DateTimeOffset.Parse("2026-09-25T08:00:00Z"),
        PlannedDeliverAt = null, Lines = [new() { ItemId = Tenant, SkuId = LegalEntity, SkuLevel = "Lsku", Quantity = quantity, UomId = "EA", FromLocationId = "loc-1" }]
    };

    internal static SupplyChainShipmentsController CreateController(CaptureHandler handler, string[] permissions, bool includeIntent = false,
        Microsoft.Extensions.Logging.ILogger<SupplyChainShipmentsController>? logger = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["GatewayUrl"] = "http://localhost:5000" }).Build();
        var controller = new SupplyChainShipmentsController(new HttpClient(handler), configuration,
            logger ?? NullLogger<SupplyChainShipmentsController>.Instance);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "shipment-tester"),
            new("tenant_id", Tenant.ToString("D")), new("legal_entity_id", LegalEntity.ToString("D")) };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        // R-2 (SHIPMENT-BUNDLE 3.2.0): the adapter reads LegalEntityId from the query, not the token, so the
        // harness sends what the page sends. The adapter builds the downstream query from bound action
        // parameters only, so this key never reaches the gateway.
        context.Request.QueryString = new QueryString("?legalEntityId=" + LegalEntity.ToString("D"));
        context.Request.Headers.Cookie = "access_token=test-token";
        context.Request.Headers["X-Correlation-Id"] = Correlation.ToString("D");
        if (includeIntent) context.Request.Headers["Idempotency-Key"] = "intent-key";
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }

    internal static HttpResponseMessage Response(HttpStatusCode status, string body)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        response.Headers.TryAddWithoutValidation("X-Correlation-Id", Correlation.ToString("D"));
        return response;
    }

    internal sealed class CaptureHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string Uri { get; private set; } = string.Empty;
        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string Body { get; private set; } = string.Empty;
        public List<string> Uris { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++; Uri = request.RequestUri?.ToString() ?? string.Empty; Uris.Add(Uri);
            foreach (var header in request.Headers) Headers[header.Key] = string.Join(',', header.Value);
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }
}
