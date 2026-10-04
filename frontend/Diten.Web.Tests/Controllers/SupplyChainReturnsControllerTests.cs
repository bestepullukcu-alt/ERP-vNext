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

// MOD-0186 Returns — same-origin adapter contract (pack §32.4/§32.8; RU-01, RU-02, RU-06…RU-08, RU-11, RU-12, RU-15,
// RU-21; §33 M-04/W-01 frontend half). Built and run by R-2 (2026-10-04).
public sealed class SupplyChainReturnsControllerTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid LegalEntity = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Trace = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid Shipment = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid Root = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid ReturnIdValue = Guid.Parse("66666666-6666-4666-8666-666666666666");

    private const string Read = "supplychain.returns.read";
    private const string Create = "supplychain.returns.create";
    private const string Transition = "supplychain.returns.transition";
    private const string Authorize = "supplychain.returns.authorize";
    private const string Transit = "supplychain.returns.transit";
    private const string Cancel = "supplychain.returns.cancel";
    private const string Receive = "supplychain.returns.receive";
    private const string Disposition = "supplychain.returns.disposition";
    private const string Close = "supplychain.returns.close";
    private const string ShipmentRead = "supplychain.shipments.read";
    private const string Gateway = "http://localhost:5000/api/shipment-bundle";

    // ── List ────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_forwards_the_two_published_query_keys_with_session_scope_headers()
    {
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.OK, "{\"items\":[],\"total\":0,\"contractVersion\":\"v1\"}"));
        var controller = CreateController(handler, [Read]);

        var result = await controller.List(Shipment.ToString("D"), "Requested", CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<ContentResult>(result).StatusCode);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"{Gateway}/returns?shipmentId={Shipment:D}&status=Requested", request.Uri);
        Assert.Equal("Bearer test-token", request.Headers["Authorization"]);
        Assert.Equal(Trace.ToString("D"), request.Headers["X-Correlation-Id"]);
        // The Returns family requires both scope headers, filled only from the signed session (middleware lines 67-68).
        Assert.Equal(Tenant.ToString("D"), request.Headers["X-Tenant-Id"]);
        Assert.Equal(LegalEntity.ToString("D"), request.Headers["X-Legal-Entity-Id"]);
        Assert.DoesNotContain("tenant", request.Uri, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("page", request.Uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Browser_supplied_scope_headers_are_ignored_and_the_session_scope_is_sent()
    {
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.OK, "{\"items\":[],\"total\":0}"));
        var controller = CreateController(handler, [Read]);
        controller.HttpContext.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString("D");
        controller.HttpContext.Request.Headers["X-Legal-Entity-Id"] = Guid.NewGuid().ToString("D");
        await controller.List(null, null, CancellationToken.None);
        var request = Assert.Single(handler.Requests);
        Assert.Equal($"{Gateway}/returns", request.Uri);
        Assert.Equal(Tenant.ToString("D"), request.Headers["X-Tenant-Id"]);
        Assert.Equal(LegalEntity.ToString("D"), request.Headers["X-Legal-Entity-Id"]);
    }

    [Fact]
    public async Task List_without_read_is_403_INVALID_REQUEST_with_zero_gateway_calls()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var result = await CreateController(handler, [Create, ShipmentRead]).List(null, null, CancellationToken.None);
        AssertFailure(result, StatusCodes.Status403Forbidden, "INVALID_REQUEST");
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Missing_scope_claim_is_403_and_missing_token_is_401_before_any_gateway_call()
    {
        var noScope = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        AssertFailure(await CreateController(noScope, [Read], includeScope: false).List(null, null, CancellationToken.None),
            StatusCodes.Status403Forbidden, "INVALID_REQUEST");
        var noToken = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var controller = CreateController(noToken, [Read]);
        controller.HttpContext.Request.Headers.Cookie = string.Empty;
        AssertFailure(await controller.List(null, null, CancellationToken.None), StatusCodes.Status401Unauthorized, "INVALID_REQUEST");
        Assert.Empty(noScope.Requests);
        Assert.Empty(noToken.Requests);
    }

    [Fact]
    public async Task List_failure_keeps_the_published_code_and_never_relays_an_unpublished_one()
    {
        var published = new CaptureHandler(_ => Json(HttpStatusCode.ServiceUnavailable,
            "{\"error\":{\"code\":\"PERSISTENCE_UNAVAILABLE\",\"message\":\"m\",\"correlationId\":\"" + Trace + "\"},\"contractVersion\":\"v1\"}"));
        AssertFailure(await CreateController(published, [Read]).List(null, null, CancellationToken.None),
            StatusCodes.Status503ServiceUnavailable, "PERSISTENCE_UNAVAILABLE");
        var gatewayPage = new CaptureHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>bad gateway</html>") });
        var json = AssertFailure(await CreateController(gatewayPage, [Read]).List(null, null, CancellationToken.None),
            StatusCodes.Status503ServiceUnavailable, "PERSISTENCE_UNAVAILABLE");
        Assert.DoesNotContain("<html>", json);
    }

    // ── Shipment resolve ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_returns_number_status_and_lines_only_never_the_root()
    {
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.OK, ShipmentDetail("\"" + Root.ToString("D") + "\"")));
        var result = await CreateController(handler, [Create, ShipmentRead]).ResolveShipment(Shipment, CancellationToken.None);

        var json = JsonSerializer.Serialize(Assert.IsType<JsonResult>(result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"shipmentNumber\":\"SHP-2026-000001\"", json);
        Assert.Contains("\"status\":\"Delivered\"", json);
        Assert.Contains("\"lineNumber\":\"1\"", json);
        Assert.Contains("\"quantity\":\"2.000\"", json);   // exact wire text, no conversion
        Assert.Contains("\"uomId\":\"EA\"", json);
        Assert.DoesNotContain(Root.ToString("D"), json);
        Assert.DoesNotContain("lifecycleCorrelationId", json);
        Assert.DoesNotContain("itemId", json);
        Assert.DoesNotContain("carrierId", json);
        var request = Assert.Single(handler.Requests);
        Assert.Equal($"{Gateway}/shipments/{Shipment:D}", request.Uri);
        Assert.Equal(Tenant.ToString("D"), request.Headers["X-Tenant-Id"]);
        Assert.Equal(LegalEntity.ToString("D"), request.Headers["X-Legal-Entity-Id"]);
    }

    [Fact]
    public async Task Resolve_maps_a_shipment_404_to_the_single_safe_not_found()
    {
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.NotFound,
            "{\"error\":{\"code\":\"SHIPMENT_NOT_FOUND\",\"message\":\"secret detail\"}}"));
        var body = AssertFailure(await CreateController(handler, [Create, ShipmentRead]).ResolveShipment(Shipment, CancellationToken.None),
            StatusCodes.Status404NotFound, "SHIPMENT_NOT_FOUND");
        Assert.DoesNotContain("secret detail", body);
    }

    [Theory]
    [InlineData(Create)]        // missing G-SHIPREAD
    [InlineData(ShipmentRead)]  // missing create
    public async Task Resolve_needs_create_and_shipments_read(string onlyPermission)
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        AssertFailure(await CreateController(handler, [onlyPermission]).ResolveShipment(Shipment, CancellationToken.None),
            StatusCodes.Status403Forbidden, "INVALID_REQUEST");
        Assert.Empty(handler.Requests);
    }

    // ── Create ──────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_forwards_the_exact_body_text_with_root_correlation_session_scope_and_the_intent_key()
    {
        const string body = "{\"shipmentId\":\"44444444-4444-4444-8444-444444444444\",\"reasonCode\":\"\",\"lines\":[{\"shipmentLineNumber\":\"1\",\"quantity\":\"002.50\",\"uomId\":\"EA\"}],\"evidenceReferenceIds\":[\"\",\"A\",\"A\"]}";
        var handler = new CaptureHandler(request => request.Uri.EndsWith("/returns", StringComparison.Ordinal)
            ? Json(HttpStatusCode.Created, $"{{\"returnId\":\"{ReturnIdValue:D}\",\"rmaNumber\":\"RMA-1\",\"shipmentId\":\"{Shipment:D}\",\"status\":\"Requested\",\"idempotentReplay\":false,\"contractVersion\":\"v1\"}}", Root)
            : Json(HttpStatusCode.OK, ShipmentDetail("\"" + Root.ToString("D") + "\"")));
        var controller = CreateController(handler, [Create, ShipmentRead], body, idempotencyKey: "intent-key-1, with comma");

        var result = await controller.Create(CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<ContentResult>(result).StatusCode);
        Assert.Equal(2, handler.Requests.Count);
        var post = handler.Requests[1];
        Assert.Equal(HttpMethod.Post, post.Method);
        Assert.Equal($"{Gateway}/returns", post.Uri);
        Assert.Equal(body, post.Body); // byte-exact: no trim, no number conversion
        Assert.Equal(Root.ToString("D"), post.Headers["X-Correlation-Id"]);
        Assert.Equal("intent-key-1, with comma", post.Headers["Idempotency-Key"]);
        Assert.Equal(Tenant.ToString("D"), post.Headers["X-Tenant-Id"]);
        Assert.Equal(LegalEntity.ToString("D"), post.Headers["X-Legal-Entity-Id"]);
        // The browser gets its own trace back, never the root.
        Assert.Equal(Trace.ToString("D"), controller.Response.Headers["X-Correlation-Id"].ToString());
    }

    [Fact]
    public async Task Create_without_shipments_read_is_403_with_zero_gateway_calls()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        AssertFailure(await CreateController(handler, [Create], "{\"shipmentId\":\"" + Shipment + "\"}", "k").Create(CancellationToken.None),
            StatusCodes.Status403Forbidden, "INVALID_REQUEST");
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("null", StatusCodes.Status503ServiceUnavailable, "RETURN_SHIPMENT_ROOT_UNAVAILABLE")]
    [InlineData("\"\"", StatusCodes.Status503ServiceUnavailable, "RETURN_SHIPMENT_ROOT_UNAVAILABLE")]
    [InlineData(null, StatusCodes.Status503ServiceUnavailable, "RETURN_SHIPMENT_ROOT_UNAVAILABLE")]
    [InlineData("\"not-a-uuid\"", StatusCodes.Status502BadGateway, "RETURN_SHIPMENT_ROOT_INVALID")]
    [InlineData("42", StatusCodes.Status502BadGateway, "RETURN_SHIPMENT_ROOT_INVALID")]
    public async Task Create_with_an_unusable_root_never_calls_returns(string? rootJson, int status, string code)
    {
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.OK, ShipmentDetail(rootJson)));
        AssertFailure(await CreateController(handler, [Create, ShipmentRead], "{\"shipmentId\":\"" + Shipment + "\"}", "k").Create(CancellationToken.None),
            status, code);
        Assert.Single(handler.Requests); // the Shipment lookup only
    }

    [Fact]
    public async Task Create_maps_shipment_dependency_failures_to_published_codes()
    {
        var down = new CaptureHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("x") });
        AssertFailure(await CreateController(down, [Create, ShipmentRead], "{\"shipmentId\":\"" + Shipment + "\"}", "k").Create(CancellationToken.None),
            StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        var rootInvalid = new CaptureHandler(_ => Json(HttpStatusCode.InternalServerError, "{\"error\":{\"code\":\"SHIPMENT_ROOT_INVALID\"}}"));
        AssertFailure(await CreateController(rootInvalid, [Create, ShipmentRead], "{\"shipmentId\":\"" + Shipment + "\"}", "k").Create(CancellationToken.None),
            StatusCodes.Status502BadGateway, "RETURN_SHIPMENT_ROOT_INVALID");
        var garbled = new CaptureHandler(_ => Json(HttpStatusCode.OK, "not json"));
        AssertFailure(await CreateController(garbled, [Create, ShipmentRead], "{\"shipmentId\":\"" + Shipment + "\"}", "k").Create(CancellationToken.None),
            StatusCodes.Status502BadGateway, "DEPENDENCY_RESPONSE_INVALID");
    }

    [Fact]
    public async Task Create_with_a_non_json_content_type_is_415_INVALID_REQUEST()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        AssertFailure(await CreateController(handler, [Create, ShipmentRead], "shipmentId=x", "k", contentType: "text/plain").Create(CancellationToken.None),
            StatusCodes.Status415UnsupportedMediaType, "INVALID_REQUEST");
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Create_without_an_idempotency_key_is_400_before_any_gateway_call()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        AssertFailure(await CreateController(handler, [Create, ShipmentRead], "{\"shipmentId\":\"" + Shipment + "\"}", idempotencyKey: null).Create(CancellationToken.None),
            StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Create_error_envelope_keeps_the_backend_code_and_replaces_the_root_with_the_trace()
    {
        var handler = new CaptureHandler(request => request.Uri.EndsWith("/returns", StringComparison.Ordinal)
            ? Json(HttpStatusCode.UnprocessableEntity, $"{{\"error\":{{\"code\":\"RETURN_QUANTITY_EXCEEDED\",\"message\":\"m\",\"correlationId\":\"{Root:D}\"}},\"contractVersion\":\"v1\"}}", Root)
            : Json(HttpStatusCode.OK, ShipmentDetail("\"" + Root.ToString("D") + "\"")));
        var controller = CreateController(handler, [Create, ShipmentRead], "{\"shipmentId\":\"" + Shipment + "\"}", "k");

        var text = AssertFailure(await controller.Create(CancellationToken.None), StatusCodes.Status422UnprocessableEntity, "RETURN_QUANTITY_EXCEEDED");

        Assert.DoesNotContain(Root.ToString("D"), text);
        Assert.Contains(Trace.ToString("D"), text);
        Assert.Equal(Trace.ToString("D"), controller.Response.Headers["X-Correlation-Id"].ToString());
    }

    [Fact]
    public async Task Create_transport_failure_is_never_reported_as_rolled_back()
    {
        var handler = new CaptureHandler(request => request.Uri.EndsWith("/returns", StringComparison.Ordinal)
            ? throw new TaskCanceledException("timeout")
            : Json(HttpStatusCode.OK, ShipmentDetail("\"" + Root.ToString("D") + "\"")));
        AssertFailure(await CreateController(handler, [Create, ShipmentRead], "{\"shipmentId\":\"" + Shipment + "\"}", "k").Create(CancellationToken.None),
            StatusCodes.Status503ServiceUnavailable, "PERSISTENCE_UNAVAILABLE");
    }

    // ── Transition ──────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Authorized", new[] { Transition, ShipmentRead })]              // target key missing
    [InlineData("Rejected", new[] { Transition, Transit, ShipmentRead })]       // authorize needed
    [InlineData("InTransit", new[] { Transition, Authorize, ShipmentRead })]    // transit needed
    [InlineData("Cancelled", new[] { Transition, Transit, ShipmentRead })]      // cancel needed
    [InlineData("Received", new[] { Transition, Disposition, ShipmentRead })]   // receive needed
    [InlineData("Dispositioned", new[] { Transition, Receive, ShipmentRead })]  // disposition needed
    [InlineData("Closed", new[] { Transition, Disposition, ShipmentRead })]     // close needed
    [InlineData("Closed", new[] { Close, ShipmentRead })]                       // base .transition missing
    [InlineData("Closed", new[] { Transition, Close })]                         // G-SHIPREAD missing
    public async Task Transition_denies_without_every_required_key_and_calls_nothing(string target, string[] permissions)
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var body = $"{{\"targetStatus\":\"{target}\",\"occurredAt\":\"2026-09-26T10:00:00+03:00\"}}";
        AssertFailure(await CreateController(handler, permissions, body, "k").Transition(ReturnIdValue, Shipment, CancellationToken.None),
            StatusCodes.Status403Forbidden, "INVALID_REQUEST");
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("Authorized", Authorize)]
    [InlineData("Rejected", Authorize)]
    [InlineData("InTransit", Transit)]
    [InlineData("Cancelled", Cancel)]
    [InlineData("Received", Receive)]
    [InlineData("Dispositioned", Disposition)]
    [InlineData("Closed", Close)]
    public async Task Transition_forwards_body_text_with_root_and_no_shipment_hint(string target, string key)
    {
        var body = $"{{\"targetStatus\":\"{target}\",\"occurredAt\":\"2026-09-26T10:00:00.5+03:00\",\"dispositionCode\":\" \"}}";
        var handler = new CaptureHandler(request => request.Uri.EndsWith("/transition", StringComparison.Ordinal)
            ? Json(HttpStatusCode.OK, $"{{\"returnId\":\"{ReturnIdValue:D}\",\"rmaNumber\":\"RMA-1\",\"shipmentId\":\"{Shipment:D}\",\"status\":\"{target}\",\"idempotentReplay\":true,\"contractVersion\":\"v1\"}}", Root)
            : Json(HttpStatusCode.OK, ShipmentDetail("\"" + Root.ToString("D") + "\"")));

        var result = await CreateController(handler, [Transition, key, ShipmentRead], body, "k-2").Transition(ReturnIdValue, Shipment, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<ContentResult>(result).StatusCode);
        var post = handler.Requests[1];
        Assert.Equal($"{Gateway}/returns/{ReturnIdValue:D}/transition", post.Uri);
        Assert.Equal(body, post.Body);
        Assert.DoesNotContain("shipmentId", post.Body);
        Assert.Equal(Root.ToString("D"), post.Headers["X-Correlation-Id"]);
        Assert.Equal("k-2", post.Headers["Idempotency-Key"]);
        Assert.Equal(Tenant.ToString("D"), post.Headers["X-Tenant-Id"]);
    }

    [Fact]
    public async Task Transition_without_a_shipment_hint_is_400_and_a_hint_404_is_the_return_safe_not_found()
    {
        var body = "{\"targetStatus\":\"Closed\",\"occurredAt\":\"2026-09-26T10:00:00Z\"}";
        var none = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        AssertFailure(await CreateController(none, [Transition, Close, ShipmentRead], body, "k").Transition(ReturnIdValue, null, CancellationToken.None),
            StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        Assert.Empty(none.Requests);
        var missing = new CaptureHandler(_ => Json(HttpStatusCode.NotFound, "{}"));
        AssertFailure(await CreateController(missing, [Transition, Close, ShipmentRead], body, "k").Transition(ReturnIdValue, Shipment, CancellationToken.None),
            StatusCodes.Status404NotFound, "RETURN_NOT_FOUND");
    }

    // ── Route surface (W-01 frontend half; RU-SCR-02/03; §32.3) ─────────────────────────────────────────────────

    [Fact]
    public void View_routes_are_exactly_the_manifest_page()
    {
        var prefix = "/" + typeof(SupplyChainReturnsController).GetCustomAttribute<RouteAttribute>()!.Template;
        var viewRoutes = Actions()
            .SelectMany(m => m.GetCustomAttributes<HttpGetAttribute>())
            .Select(a => a.Template ?? string.Empty)
            .Where(template => !template.StartsWith("api", StringComparison.Ordinal))
            .Select(template => template.Length == 0 ? prefix : $"{prefix}/{template}")
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(new HashSet<string>(StringComparer.Ordinal) { "/SupplyChain/Returns" }, viewRoutes);
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
            "POST api/{returnId:guid}/transition"
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

    private static IEnumerable<MethodInfo> Actions() => typeof(SupplyChainReturnsController)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

    private static string ShipmentDetail(string? rootJson)
    {
        var root = rootJson is null ? string.Empty : $",\"lifecycleCorrelationId\":{rootJson}";
        return $"{{\"shipmentId\":\"{Shipment:D}\",\"shipmentNumber\":\"SHP-2026-000001\",\"status\":\"Delivered\",\"carrierId\":null,"
               + "\"lines\":[{\"lineNumber\":\"1\",\"itemId\":\"77777777-7777-4777-8777-777777777777\",\"skuId\":\"88888888-8888-4888-8888-888888888888\",\"quantity\":\"2.000\",\"uomId\":\"EA\"}],"
               + $"\"contractVersion\":\"v1\"{root}}}";
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

    private static SupplyChainReturnsController CreateController(CaptureHandler handler, string[] permissions,
        string? body = null, string? idempotencyKey = null, string contentType = "application/json", bool includeScope = true)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["GatewayUrl"] = "http://localhost:5000" }).Build();
        var controller = new SupplyChainReturnsController(new HttpClient(handler), configuration,
            NullLogger<SupplyChainReturnsController>.Instance);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "returns-tester") };
        if (includeScope)
        {
            claims.Add(new("tenant_id", Tenant.ToString("D")));
            claims.Add(new("legal_entity_id", LegalEntity.ToString("D")));
        }

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
