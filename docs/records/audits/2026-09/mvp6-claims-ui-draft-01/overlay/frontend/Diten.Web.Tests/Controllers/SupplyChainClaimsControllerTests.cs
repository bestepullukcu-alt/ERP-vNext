using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

// MOD-0187 Claims — same-origin adapter contract (pack §32.4/§32.8; CU-01, CU-02, CU-07…CU-09, CU-14, CU-18, CU-23).
// DRAFT overlay — not built, not run.
public sealed class SupplyChainClaimsControllerTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid LegalEntity = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Trace = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid Shipment = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid Root = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid ClaimIdValue = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly Guid Carrier = Guid.Parse("77777777-7777-4777-8777-777777777777");

    private const string Read = "supplychain.claims.read";
    private const string Create = "supplychain.claims.create";
    private const string Investigate = "supplychain.claims.investigate";
    private const string Decide = "supplychain.claims.decide";
    private const string Settle = "supplychain.claims.settle";
    private const string ShipmentRead = "supplychain.shipments.read";

    // ── List ────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_forwards_only_the_two_published_query_keys_without_scope_headers()
    {
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.OK, "{\"items\":[],\"total\":0,\"contractVersion\":\"v1\"}"));
        var controller = CreateController(handler, [Read]);

        var result = await controller.List(Shipment.ToString("D"), "Open", CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<ContentResult>(result).StatusCode);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"http://localhost:5000/api/shipment-bundle/claims?shipmentId={Shipment:D}&status=Open", request.Uri);
        Assert.Equal("Bearer test-token", request.Headers["Authorization"]);
        Assert.Equal(Trace.ToString("D"), request.Headers["X-Correlation-Id"]);
        Assert.False(request.Headers.ContainsKey("X-Tenant-Id"));
        Assert.False(request.Headers.ContainsKey("X-Legal-Entity-Id"));
        Assert.DoesNotContain("tenant", request.Uri, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("page", request.Uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task List_omits_absent_parameters()
    {
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.OK, "{\"items\":[],\"total\":0}"));
        await CreateController(handler, [Read]).List(null, null, CancellationToken.None);
        Assert.Equal("http://localhost:5000/api/shipment-bundle/claims", Assert.Single(handler.Requests).Uri);
    }

    [Fact]
    public async Task List_without_read_is_403_FORBIDDEN_with_zero_gateway_calls()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var result = await CreateController(handler, [Create, ShipmentRead]).List(null, null, CancellationToken.None);
        AssertFailure(result, StatusCodes.Status403Forbidden, "FORBIDDEN");
        Assert.Empty(handler.Requests);
    }

    // ── Shipment resolve ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_returns_number_status_and_carrier_only_never_the_root()
    {
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.OK, ShipmentDetail("\"" + Root.ToString("D") + "\"", Carrier)));
        var controller = CreateController(handler, [Create, ShipmentRead]);

        var result = await controller.ResolveShipment(Shipment, CancellationToken.None);

        var json = JsonSerializer.Serialize(Assert.IsType<JsonResult>(result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"shipmentNumber\":\"SHP-2026-000001\"", json);
        Assert.Contains("\"status\":\"Dispatched\"", json);
        Assert.Contains($"\"carrierId\":\"{Carrier:D}\"", json);
        Assert.DoesNotContain(Root.ToString("D"), json);
        Assert.DoesNotContain("lifecycleCorrelationId", json);
        Assert.DoesNotContain("lines", json);
        var request = Assert.Single(handler.Requests);
        Assert.Equal($"http://localhost:5000/api/shipment-bundle/shipments/{Shipment:D}", request.Uri);
        // Shipment adapter header policy (A12): scope headers from the caller's claims.
        Assert.Equal(Tenant.ToString("D"), request.Headers["X-Tenant-Id"]);
        Assert.Equal(LegalEntity.ToString("D"), request.Headers["X-Legal-Entity-Id"]);
    }

    [Fact]
    public async Task Resolve_maps_a_shipment_404_to_the_single_safe_not_found()
    {
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.NotFound,
            "{\"error\":{\"code\":\"SHIPMENT_NOT_FOUND\",\"message\":\"secret detail\"}}"));
        var result = await CreateController(handler, [Create, ShipmentRead]).ResolveShipment(Shipment, CancellationToken.None);
        var body = AssertFailure(result, StatusCodes.Status404NotFound, "CLAIM_NOT_FOUND");
        Assert.DoesNotContain("secret detail", body);
    }

    [Theory]
    [InlineData(Create)]        // missing G-SHIPREAD
    [InlineData(ShipmentRead)]  // missing create
    public async Task Resolve_needs_create_and_shipments_read(string onlyPermission)
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var result = await CreateController(handler, [onlyPermission]).ResolveShipment(Shipment, CancellationToken.None);
        AssertFailure(result, StatusCodes.Status403Forbidden, "FORBIDDEN");
        Assert.Empty(handler.Requests);
    }

    // ── Create ──────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_forwards_the_exact_body_text_with_root_correlation_and_the_intent_key()
    {
        const string body = "{\"shipmentId\":\"44444444-4444-4444-8444-444444444444\",\"reasonCode\":\"\",\"claimedAmount\":\"000250.00\",\"currency\":\"usd\",\"evidenceReferenceIds\":[\"\",\"A\",\"A\"]}";
        var handler = new CaptureHandler(request => request.Uri.EndsWith("/claims", StringComparison.Ordinal)
            ? Json(HttpStatusCode.Created, $"{{\"claimId\":\"{ClaimIdValue:D}\",\"claimNumber\":\"CLM-1\",\"shipmentId\":\"{Shipment:D}\",\"status\":\"Open\",\"approvedAmount\":null,\"idempotentReplay\":false,\"contractVersion\":\"v1\"}}", Root)
            : Json(HttpStatusCode.OK, ShipmentDetail("\"" + Root.ToString("D") + "\"", null)));
        var controller = CreateController(handler, [Create, ShipmentRead], body, idempotencyKey: "intent-key-1");

        var result = await controller.Create(CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<ContentResult>(result).StatusCode);
        Assert.Equal(2, handler.Requests.Count);
        var post = handler.Requests[1];
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Equal("http://localhost:5000/api/shipment-bundle/claims", post.Uri);
        Assert.Equal(body, post.Body); // byte-exact: no trim, no uppercase, no number conversion
        Assert.Equal(Root.ToString("D"), post.Headers["X-Correlation-Id"]);
        Assert.Equal("intent-key-1", post.Headers["Idempotency-Key"]);
        Assert.False(post.Headers.ContainsKey("X-Tenant-Id"));
        Assert.False(post.Headers.ContainsKey("X-Legal-Entity-Id"));
        // The browser gets its own trace back, never the root.
        Assert.Equal(Trace.ToString("D"), controller.Response.Headers["X-Correlation-Id"].ToString());
    }

    [Fact]
    public async Task Create_without_shipments_read_is_403_with_zero_gateway_calls()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var result = await CreateController(handler, [Create], "{\"shipmentId\":\"" + Shipment + "\"}", "k").Create(CancellationToken.None);
        AssertFailure(result, StatusCodes.Status403Forbidden, "FORBIDDEN");
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("null", StatusCodes.Status503ServiceUnavailable, "CLAIM_REFERENCE_INCOMPLETE")]
    [InlineData("\"\"", StatusCodes.Status503ServiceUnavailable, "CLAIM_REFERENCE_INCOMPLETE")]
    [InlineData(null, StatusCodes.Status503ServiceUnavailable, "CLAIM_REFERENCE_INCOMPLETE")]
    [InlineData("\"not-a-uuid\"", StatusCodes.Status502BadGateway, "CLAIM_REFERENCE_INVALID")]
    [InlineData("42", StatusCodes.Status502BadGateway, "CLAIM_REFERENCE_INVALID")]
    public async Task Create_with_an_unusable_root_never_calls_claims(string? rootJson, int status, string code)
    {
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.OK, ShipmentDetail(rootJson, null)));
        var result = await CreateController(handler, [Create, ShipmentRead], "{\"shipmentId\":\"" + Shipment + "\"}", "k")
            .Create(CancellationToken.None);
        AssertFailure(result, status, code);
        Assert.Single(handler.Requests); // the Shipment lookup only
    }

    [Fact]
    public async Task Create_with_a_non_json_content_type_is_415()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var result = await CreateController(handler, [Create, ShipmentRead], "shipmentId=x", "k", contentType: "text/plain")
            .Create(CancellationToken.None);
        AssertFailure(result, StatusCodes.Status415UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE");
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Create_without_an_idempotency_key_is_400_before_any_gateway_call()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var result = await CreateController(handler, [Create, ShipmentRead], "{\"shipmentId\":\"" + Shipment + "\"}", idempotencyKey: null)
            .Create(CancellationToken.None);
        AssertFailure(result, StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Create_error_envelope_keeps_the_backend_code_and_replaces_the_root_with_the_trace()
    {
        var handler = new CaptureHandler(request => request.Uri.EndsWith("/claims", StringComparison.Ordinal)
            ? Json(HttpStatusCode.UnprocessableEntity, $"{{\"error\":{{\"code\":\"CLAIM_CARRIER_MISMATCH\",\"message\":\"m\",\"correlationId\":\"{Root:D}\"}},\"contractVersion\":\"v1\"}}", Root)
            : Json(HttpStatusCode.OK, ShipmentDetail("\"" + Root.ToString("D") + "\"", Carrier)));
        var controller = CreateController(handler, [Create, ShipmentRead], "{\"shipmentId\":\"" + Shipment + "\"}", "k");

        var result = await controller.Create(CancellationToken.None);

        var text = AssertFailure(result, StatusCodes.Status422UnprocessableEntity, "CLAIM_CARRIER_MISMATCH");
        Assert.DoesNotContain(Root.ToString("D"), text);
        Assert.Contains(Trace.ToString("D"), text);
        Assert.Equal(Trace.ToString("D"), controller.Response.Headers["X-Correlation-Id"].ToString());
    }

    // ── Transition ──────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Withdrawn", new[] { Create, ShipmentRead })]     // create-only cannot withdraw
    [InlineData("Closed", new[] { Settle, ShipmentRead })]        // settle-only cannot close
    [InlineData("Approved", new[] { Investigate, ShipmentRead })] // decide needed
    [InlineData("Settled", new[] { Decide, ShipmentRead })]       // settle needed
    [InlineData("Investigating", new[] { Investigate })]          // G-SHIPREAD missing
    public async Task Transition_denies_without_the_exact_target_key_and_calls_nothing(string target, string[] permissions)
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var body = $"{{\"targetStatus\":\"{target}\",\"occurredAt\":\"2026-09-26T10:00:00+03:00\"}}";
        var result = await CreateController(handler, permissions, body, "k").Transition(ClaimIdValue, Shipment, CancellationToken.None);
        AssertFailure(result, StatusCodes.Status403Forbidden, "FORBIDDEN");
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("Investigating", Investigate)]
    [InlineData("Withdrawn", Investigate)]
    [InlineData("Approved", Decide)]
    [InlineData("Rejected", Decide)]
    [InlineData("Settled", Settle)]
    [InlineData("Closed", Decide)]
    public async Task Transition_forwards_body_text_with_root_and_no_shipment_hint(string target, string key)
    {
        var body = $"{{\"targetStatus\":\"{target}\",\"occurredAt\":\"2026-09-26T10:00:00.5+03:00\",\"note\":\"\"}}";
        var handler = new CaptureHandler(request => request.Uri.EndsWith("/transition", StringComparison.Ordinal)
            ? Json(HttpStatusCode.OK, $"{{\"claimId\":\"{ClaimIdValue:D}\",\"claimNumber\":\"CLM-1\",\"shipmentId\":\"{Shipment:D}\",\"status\":\"{target}\",\"idempotentReplay\":true,\"contractVersion\":\"v1\"}}", Root)
            : Json(HttpStatusCode.OK, ShipmentDetail("\"" + Root.ToString("D") + "\"", null)));

        var result = await CreateController(handler, [key, ShipmentRead], body, "k-2").Transition(ClaimIdValue, Shipment, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<ContentResult>(result).StatusCode);
        var post = handler.Requests[1];
        Assert.Equal($"http://localhost:5000/api/shipment-bundle/claims/{ClaimIdValue:D}/transition", post.Uri);
        Assert.Equal(body, post.Body);
        Assert.DoesNotContain("shipmentId", post.Body);
        Assert.Equal(Root.ToString("D"), post.Headers["X-Correlation-Id"]);
        Assert.Equal("k-2", post.Headers["Idempotency-Key"]);
    }

    [Fact]
    public async Task Transition_without_a_shipment_hint_is_400()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var body = "{\"targetStatus\":\"Investigating\",\"occurredAt\":\"2026-09-26T10:00:00Z\"}";
        var result = await CreateController(handler, [Investigate, ShipmentRead], body, "k").Transition(ClaimIdValue, null, CancellationToken.None);
        AssertFailure(result, StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        Assert.Empty(handler.Requests);
    }

    // ── Route surface (W-01 frontend half; CU-SCR-02/03; §32.3) ─────────────────────────────────────────────────

    [Fact]
    public void View_routes_are_exactly_the_manifest_page()
    {
        var prefix = "/" + typeof(SupplyChainClaimsController).GetCustomAttribute<RouteAttribute>()!.Template;
        var viewRoutes = Actions()
            .SelectMany(m => m.GetCustomAttributes<HttpGetAttribute>())
            .Select(a => a.Template ?? string.Empty)
            .Where(template => !template.StartsWith("api", StringComparison.Ordinal))
            .Select(template => template.Length == 0 ? prefix : $"{prefix}/{template}")
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(new HashSet<string>(StringComparer.Ordinal) { "/SupplyChain/Claims" }, viewRoutes);
    }

    [Fact]
    public void Adapter_surface_is_exactly_the_four_bound_operations()
    {
        var routes = Actions()
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>().Select(a => $"{string.Join(',', a.HttpMethods)} {a.Template}"))
            .Where(route => route.Contains(" api", StringComparison.Ordinal))
            .OrderBy(route => route, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[]
        {
            "GET api",
            "GET api/shipments/{shipmentId:guid}",
            "POST api",
            "POST api/{claimId:guid}/transition"
        }, routes);
        Assert.DoesNotContain(Actions(), m => m.GetCustomAttributes<HttpDeleteAttribute>().Any()
                                             || m.GetCustomAttributes<HttpPutAttribute>().Any()
                                             || m.GetCustomAttributes<HttpPatchAttribute>().Any());
    }

    [Fact]
    public void Every_adapter_is_a_json_endpoint_and_every_post_validates_antiforgery()
    {
        foreach (var method in Actions().Where(m => m.GetCustomAttributes<HttpMethodAttribute>()
                     .Any(a => (a.Template ?? string.Empty).StartsWith("api", StringComparison.Ordinal))))
        {
            Assert.NotNull(method.GetCustomAttribute<JsonAdapterEndpointAttribute>());
            if (method.GetCustomAttributes<HttpPostAttribute>().Any())
            {
                Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            }
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static IEnumerable<MethodInfo> Actions() => typeof(SupplyChainClaimsController)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

    private static string ShipmentDetail(string? rootJson, Guid? carrier)
    {
        var carrierJson = carrier is null ? "null" : $"\"{carrier:D}\"";
        var root = rootJson is null ? string.Empty : $",\"lifecycleCorrelationId\":{rootJson}";
        return $"{{\"shipmentId\":\"{Shipment:D}\",\"shipmentNumber\":\"SHP-2026-000001\",\"status\":\"Dispatched\",\"carrierId\":{carrierJson},"
               + $"\"lines\":[{{\"lineNumber\":\"1\"}}],\"contractVersion\":\"v1\"{root}}}";
    }

    private static string AssertFailure(IActionResult result, int status, string code)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(status, objectResult.StatusCode);
        var json = JsonSerializer.Serialize(objectResult.Value);
        Assert.Contains($"\"code\":\"{code}\"", json);
        Assert.Contains("\"contractVersion\":\"v1\"", json);
        return json;
    }

    private static SupplyChainClaimsController CreateController(CaptureHandler handler, string[] permissions,
        string? body = null, string? idempotencyKey = null, string contentType = "application/json")
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["GatewayUrl"] = "http://localhost:5000" }).Build();
        var controller = new SupplyChainClaimsController(new HttpClient(handler), configuration,
            NullLogger<SupplyChainClaimsController>.Instance);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "claims-tester"),
            new("tenant_id", Tenant.ToString("D")),
            new("legal_entity_id", LegalEntity.ToString("D"))
        };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        context.Request.Headers.Cookie = "access_token=test-token";
        context.Request.Headers["X-Correlation-Id"] = Trace.ToString("D");
        if (idempotencyKey is not null) context.Request.Headers["Idempotency-Key"] = idempotencyKey;
        if (body is not null)
        {
            context.Request.ContentType = contentType;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        }

        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body, Guid? correlation = null)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        response.Headers.TryAddWithoutValidation("X-Correlation-Id", (correlation ?? Trace).ToString("D"));
        return response;
    }

    private sealed record CapturedRequest(HttpMethod Method, string Uri, Dictionary<string, string> Headers, string Body);

    private sealed class CaptureHandler(Func<CapturedRequest, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in request.Headers) headers[header.Key] = string.Join(',', header.Value);
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var captured = new CapturedRequest(request.Method, request.RequestUri?.ToString() ?? string.Empty, headers, body);
            Requests.Add(captured);
            return responder(captured);
        }
    }
}
