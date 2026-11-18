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
        Assert.DoesNotContain("skuId", json); // r10 §35.2: ids stay server-side
        var request = Assert.Single(handler.Requests, r => r.Uri.Contains("/shipments/", StringComparison.Ordinal));
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

    // MOD-0186 DL-2 (Q504): the four receipt codes keep their status and carry only the fields a screen text needs.
    [Theory]
    [InlineData(422, "INVENTORY_REFUSED", "{\"shipmentLineNumber\":\"2\",\"inventoryCode\":\"LOT_UNDER_RECALL\",\"inventory\":{\"recallCaseNumber\":\"RC-SECRET\"}}", "{\"shipmentLineNumber\":\"2\",\"inventoryCode\":\"LOT_UNDER_RECALL\"}")]
    [InlineData(422, "RETURN_SOURCE_MOVEMENT_MISSING", "{\"shipmentLineNumber\":\"1\",\"other\":\"RC-SECRET\"}", "{\"shipmentLineNumber\":\"1\"}")]
    [InlineData(422, "RETURN_LINE_NOT_UNPOSTABLE", "{\"shipmentLineNumber\":\"3\",\"lastInventoryCode\":\"RC-SECRET\"}", "{\"shipmentLineNumber\":\"3\"}")]
    [InlineData(409, "RETURN_RECEIPT_IN_PROGRESS", "{\"toWarehouseId\":\"wh-01\",\"toLocationId\":null,\"occurredAt\":\"2026-10-26T08:00:00Z\",\"other\":\"RC-SECRET\"}", "{\"toWarehouseId\":\"wh-01\",\"occurredAt\":\"2026-10-26T08:00:00Z\"}")]
    public async Task Transition_relays_receipt_codes_with_status_and_filtered_details(int status, string code, string details, string expected)
    {
        var json = AssertFailure(await ReceiptFailure(status, code, details), status, code);
        Assert.Contains($"\"details\":{expected}", json);
        Assert.DoesNotContain("RC-SECRET", json);
    }

    [Fact]
    public async Task Transition_carries_no_details_for_any_other_code()
    {
        var json = AssertFailure(await ReceiptFailure(503, "DEPENDENCY_UNAVAILABLE", "{\"shipmentLineNumber\":\"1\"}"),
            StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        Assert.DoesNotContain("details", json);
    }

    [Fact]
    public async Task Transition_keeps_an_unrecognized_422_as_a_refusal_with_its_code_and_no_details()
    {
        var json = AssertFailure(await ReceiptFailure(422, "NEW_RECEIPT_RULE", "{\"shipmentLineNumber\":\"RC-SECRET\"}"),
            StatusCodes.Status422UnprocessableEntity, "NEW_RECEIPT_RULE");
        Assert.DoesNotContain("details", json);
        Assert.DoesNotContain("RC-SECRET", json);
    }

    [Theory]
    [InlineData("<b>raw text</b>")]
    [InlineData("lower_case")]
    public async Task Transition_never_relays_a_422_code_that_is_not_code_shaped(string code)
    {
        var json = AssertFailure(await ReceiptFailure(422, code, "{}"), StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        Assert.DoesNotContain(code, json);
    }

    private static Task<IActionResult> ReceiptFailure(int status, string code, string details)
    {
        var body = "{\"targetStatus\":\"Received\",\"occurredAt\":\"2026-09-26T10:00:00+03:00\",\"toWarehouseId\":\"wh-01\"}";
        var handler = new CaptureHandler(request => request.Uri.EndsWith("/transition", StringComparison.Ordinal)
            ? Json((HttpStatusCode)status, $"{{\"error\":{{\"code\":\"{code}\",\"message\":\"m\",\"details\":{details},\"correlationId\":\"{Root:D}\"}},\"contractVersion\":\"v1\"}}", Root)
            : Json(HttpStatusCode.OK, ShipmentDetail("\"" + Root.ToString("D") + "\"")));
        return CreateController(handler, [Transition, Receive, ShipmentRead], body, "k-3").Transition(ReturnIdValue, Shipment, CancellationToken.None);
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
        // MOD-0186 r10 §35.5 (DL-4): the details page joins the list page (manifest page RETURN_DETAILS).
        Assert.Equal(new HashSet<string>(StringComparer.Ordinal) { "/SupplyChain/Returns", "/SupplyChain/Returns/Details/{returnId:guid}" }, viewRoutes);
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
            // r10 §35.3 / §35.5 (DL-3, DL-4): read-only picker, place and details adapters join the four bound operations.
            "GET api",
            "GET api/eligible-shipments",
            "GET api/locations",
            "GET api/places/{locationId}",
            "GET api/shipments/{shipmentId:guid}",
            "GET api/warehouses",
            "GET api/{returnId:guid}",
            "GET api/{returnId:guid}/holds",
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


    // ── MOD-0186 r10 DL-3 / DL-4 adapters (§35.2–35.5) ─────────────────────────────────────────────────────────────
    private const string CrmRead = "crm.account.read";
    private const string HoldsRead = "quarantine.holds.read";
    private const string Account = "14900000-0000-4000-8000-000000000001";
    private const string Sku = "88888888-8888-4888-8888-888888888888";
    private const string AccountJson = "{\"data\":{\"accountName\":\"Acme\",\"accountCode\":\"C-1\",\"responsiblePersonEmail\":\"buyer@example.test\",\"addressLine\":\"Ataturk Cad\"}}";

    private static string SummaryRow(string number, string? customerRef) =>
        $"{{\"shipmentId\":\"{Guid.NewGuid():D}\",\"shipmentNumber\":\"{number}\",\"status\":\"Delivered\",\"plannedShipAt\":\"2026-09-20T08:00:00Z\",\"customerRef\":{(customerRef is null ? "null" : $"\"{customerRef}\"")}}}";

    private static string ShipmentWithLot(string? customerRef = Account) =>
        $"{{\"shipmentId\":\"{Shipment:D}\",\"shipmentNumber\":\"SHP-2026-000001\",\"status\":\"Delivered\",\"customerRef\":{(customerRef is null ? "null" : $"\"{customerRef}\"")},\"lifecycleCorrelationId\":\"{Root:D}\","
        + $"\"lines\":[{{\"lineNumber\":\"1\",\"itemId\":\"77777777-7777-4777-8777-777777777777\",\"skuId\":\"{Sku}\",\"skuLevel\":\"Lsku\",\"lotId\":\"lot-1\",\"quantity\":\"2.000\",\"uomId\":\"EA\"}}],\"contractVersion\":\"v1\"}}";

    private static HttpResponseMessage Names(CapturedRequest r, string fallback, bool namesUp = true)
    {
        var path = new Uri(r.Uri).AbsolutePath;
        if (path == "/api/product-master/skus/bulk-read")
            return namesUp ? Json(HttpStatusCode.OK, $"{{\"items\":[{{\"skuId\":\"{Sku}\",\"skuLevel\":\"Lsku\",\"canonicalCode\":\"LS-1\",\"name\":\"Tablet 16\",\"lifecycleStatus\":\"Active\"}}],\"notFound\":[],\"contractVersion\":\"v1\"}}")
                : Json(HttpStatusCode.InternalServerError, "{}");
        if (path == "/api/trace/lots/lot-1") return namesUp ? Json(HttpStatusCode.OK, "{\"lotId\":\"lot-1\",\"lotNumber\":\"L-0042\"}") : Json(HttpStatusCode.Forbidden, "{}");
        if (path.StartsWith("/api/crm/accounts/", StringComparison.Ordinal)) return Json(HttpStatusCode.OK, AccountJson);
        return Json(HttpStatusCode.OK, fallback);
    }

    // RB-1: the resolve projection carries the product / SKU name and code and the lot number; no id is a label.
    [Fact]
    public async Task Rb1_Resolve_carries_sku_name_and_code_and_lot_number_and_no_id()
    {
        var handler = new CaptureHandler(r => Names(r, ShipmentWithLot()));
        var result = await CreateController(handler, [Create, ShipmentRead]).ResolveShipment(Shipment, CancellationToken.None);
        var json = JsonSerializer.Serialize(Assert.IsType<JsonResult>(result).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var line = JsonDocument.Parse(json).RootElement.GetProperty("lines")[0];
        Assert.Equal("LS-1 \u00B7 Tablet 16", line.GetProperty("skuLabel").GetString());
        Assert.Equal("L-0042", line.GetProperty("lotNumber").GetString());
        foreach (var id in new[] { Sku, "77777777-7777-4777-8777-777777777777", "lot-1", Account }) Assert.DoesNotContain(id, json);
        Assert.Single(handler.Requests, x => x.Uri.EndsWith("/skus/bulk-read", StringComparison.Ordinal));
    }

    // RB-2: a failed or denied name read keeps the row with "unavailable", and the resolve still answers 200.
    [Fact]
    public async Task Rb2_Failed_name_reads_keep_the_row_and_say_unavailable()
    {
        var handler = new CaptureHandler(r => Names(r, ShipmentWithLot(), namesUp: false));
        var json = JsonSerializer.Serialize(Assert.IsType<JsonResult>(await CreateController(handler, [Create, ShipmentRead]).ResolveShipment(Shipment, CancellationToken.None)).Value,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"skuState\":\"unavailable\"", json);
        Assert.Contains("\"lotState\":\"unavailable\"", json);
        Assert.Contains("\"lineNumber\":\"1\"", json);
    }

    // RB-3: the create picker asks Delivered and Closed with the search, draws the customer from the summary's customerRef
    // (no per-row getShipment), shows "old record" without one, and never sends the id as text.
    [Fact]
    public async Task Rb3_Eligible_picker_asks_delivered_and_closed_with_search_and_resolves_the_customer()
    {
        var handler = new CaptureHandler(r => Names(r, $"{{\"items\":[{SummaryRow("SHP-1", Account)},{SummaryRow("SHP-2", null)}],\"total\":2,\"filteredTotal\":2}}"));
        var result = Assert.IsType<ContentResult>(await CreateController(handler, [Create, ShipmentRead, CrmRead]).EligibleShipments(" 0042 ", 1, false, CancellationToken.None));
        var shipments = handler.Requests.Where(x => new Uri(x.Uri).AbsolutePath == "/api/shipment-bundle/shipments").Select(x => x.Uri).ToList();
        Assert.Equal(2, shipments.Count);
        Assert.Contains(shipments, u => u.Contains("status=Delivered") && u.Contains("search=0042"));
        Assert.Contains(shipments, u => u.Contains("status=Closed") && u.Contains("search=0042"));
        Assert.DoesNotContain(handler.Requests, x => System.Text.RegularExpressions.Regex.IsMatch(new Uri(x.Uri).AbsolutePath, "/shipments/[0-9a-f-]{36}$"));
        var body = JsonDocument.Parse(result.Content!).RootElement;
        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, i => i.GetProperty("customer").GetProperty("state").GetString() == "resolved" && i.GetProperty("customer").GetProperty("name").GetString() == "Acme");
        Assert.Contains(items, i => i.GetProperty("customer").GetProperty("state").GetString() == "oldRecord");
        Assert.DoesNotContain(Account, result.Content!);
    }

    [Fact]
    public async Task Rb3_Without_crm_read_the_picker_shows_number_and_date_and_asks_no_crm()
    {
        var handler = new CaptureHandler(r => Names(r, $"{{\"items\":[{SummaryRow("SHP-1", Account)}],\"total\":1}}"));
        var result = Assert.IsType<ContentResult>(await CreateController(handler, [Create, ShipmentRead]).EligibleShipments(null, 1, false, CancellationToken.None));
        Assert.DoesNotContain(handler.Requests, x => x.Uri.Contains("/api/crm/", StringComparison.Ordinal));
        var item = JsonDocument.Parse(result.Content!).RootElement.GetProperty("items")[0];
        Assert.Equal(JsonValueKind.Null, item.GetProperty("customer").ValueKind);
        Assert.Equal("SHP-1", item.GetProperty("number").GetString());
    }

    // RB-6: the layer asks the opened node's direct children, Active, and never isStockable; the warehouse picker asks Active.
    [Fact]
    public async Task Rb6_Location_layers_never_ask_isStockable()
    {
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.OK, "{\"items\":[{\"locationId\":\"z1\",\"code\":\"Z-1\",\"name\":\"Zone\",\"type\":\"Zone\",\"isStockable\":false}]}"));
        var parent = Guid.NewGuid();
        Assert.IsType<ContentResult>(await CreateController(handler, [Receive]).Locations(parent.ToString(), null, CancellationToken.None));
        Assert.IsType<ContentResult>(await CreateController(handler, [Receive]).Warehouses(null, CancellationToken.None));
        Assert.Equal($"http://localhost:5000/api/locations/search?parentId={parent:D}&status=Active&page=1&pageSize=200", handler.Requests[0].Uri);
        Assert.Equal("http://localhost:5000/api/locations/search?type=Warehouse&status=Active&page=1&pageSize=20", handler.Requests[1].Uri);
        Assert.All(handler.Requests, x => Assert.DoesNotContain("isStockable", x.Uri, StringComparison.OrdinalIgnoreCase));
        AssertFailure(await CreateController(handler, [Read]).Locations(parent.ToString(), null, CancellationToken.None), StatusCodes.Status403Forbidden, "INVALID_REQUEST");
    }

    private static string ReturnDetailJson(string? customerRef) =>
        $"{{\"returnId\":\"{ReturnIdValue:D}\",\"rmaNumber\":\"RMA-1\",\"shipmentId\":\"{Shipment:D}\",\"status\":\"Received\"{(customerRef is null ? string.Empty : $",\"customerRef\":\"{customerRef}\"")},\"reasonCode\":\"DAMAGED\",\"createdAt\":\"2026-09-22T09:00:00Z\","
        + "\"lines\":[{\"shipmentLineNumber\":\"1\",\"quantity\":\"2.000\",\"uomId\":\"EA\",\"inventoryTransactionReferenceId\":\"17300000-0000-4000-8000-000000000001\",\"inventoryPosting\":\"Posted\",\"inventoryPostingReason\":null}],\"contractVersion\":\"v1\"}";

    private static HttpResponseMessage DetailRoute(CapturedRequest r, string? returnCustomer, string? shipmentCustomer)
    {
        var path = new Uri(r.Uri).AbsolutePath;
        if (path == $"/api/shipment-bundle/returns/{ReturnIdValue:D}") return Json(HttpStatusCode.OK, ReturnDetailJson(returnCustomer));
        if (path == $"/api/shipment-bundle/shipments/{Shipment:D}") return Json(HttpStatusCode.OK, ShipmentWithLot(shipmentCustomer));
        if (path == "/api/quarantine/holds/by-source")
            return Json(HttpStatusCode.OK, $"[{{\"holdId\":\"99999999-9999-4999-8999-999999999999\",\"holdNumber\":\"KRN-2026-0007\",\"status\":\"Open\",\"item\":{{\"skuId\":\"{Sku}\"}},\"lot\":{{\"lotId\":\"lot-1\"}}}},{{\"holdId\":\"aaaaaaaa-9999-4999-8999-999999999999\",\"holdNumber\":\"KRN-2026-0008\",\"status\":\"Decided\",\"item\":{{\"skuId\":\"{Guid.NewGuid():D}\"}}}}]");
        return Names(r, "{}");
    }

    // RB-8 / RB-9 adapter: getReturn with names; the return's own customerRef only, never the shipment's (single rule).
    [Fact]
    public async Task Rb25_Details_shows_the_returns_own_customer_and_never_falls_back_to_the_shipment()
    {
        var own = new CaptureHandler(r => DetailRoute(r, Account, null));
        var body = JsonDocument.Parse(Assert.IsType<ContentResult>(await CreateController(own, [Read, ShipmentRead, CrmRead]).Detail(ReturnIdValue, CancellationToken.None)).Content!).RootElement;
        Assert.Equal("resolved", body.GetProperty("customer").GetProperty("state").GetString());
        Assert.False(body.TryGetProperty("customerRef", out _));
        Assert.Equal("SHP-2026-000001", body.GetProperty("shipmentNumber").GetString());
        var line = body.GetProperty("lines")[0];
        Assert.Equal("LS-1 \u00B7 Tablet 16", line.GetProperty("sku").GetProperty("label").GetString());
        Assert.Equal("L-0042", line.GetProperty("lot").GetProperty("label").GetString());
        Assert.False(line.TryGetProperty("inventoryTransactionReferenceId", out _));

        var old = new CaptureHandler(r => DetailRoute(r, null, Account));
        var oldBody = JsonDocument.Parse(Assert.IsType<ContentResult>(await CreateController(old, [Read, ShipmentRead, CrmRead]).Detail(ReturnIdValue, CancellationToken.None)).Content!).RootElement;
        Assert.Equal("oldRecord", oldBody.GetProperty("customer").GetProperty("state").GetString());
        Assert.DoesNotContain(old.Requests, x => x.Uri.Contains("/api/crm/", StringComparison.Ordinal));
    }

    // RB-25 / SB-25: a stored value that is not a lower-case Guid is "not found", with no CRM call.
    [Fact]
    public async Task Rb25_A_malformed_stored_customer_is_not_found_with_no_crm_call()
    {
        var handler = new CaptureHandler(r => DetailRoute(r, "ACME-01", null));
        var body = JsonDocument.Parse(Assert.IsType<ContentResult>(await CreateController(handler, [Read, ShipmentRead, CrmRead]).Detail(ReturnIdValue, CancellationToken.None)).Content!).RootElement;
        Assert.Equal("notFound", body.GetProperty("customer").GetProperty("state").GetString());
        Assert.DoesNotContain(handler.Requests, x => x.Uri.Contains("/api/crm/", StringComparison.Ordinal));
    }

    // RB-10: without quarantine.holds.read the holds adapter refuses before any request; with it, each line gets its KRN.
    [Fact]
    public async Task Rb10_Holds_need_the_quarantine_key_and_are_matched_to_lines()
    {
        var denied = new CaptureHandler(_ => throw new InvalidOperationException("no request"));
        AssertFailure(await CreateController(denied, [Read, ShipmentRead]).Holds(ReturnIdValue, CancellationToken.None), StatusCodes.Status403Forbidden, "INVALID_REQUEST");
        Assert.Empty(denied.Requests);

        var handler = new CaptureHandler(r => DetailRoute(r, Account, Account));
        var body = JsonDocument.Parse(Assert.IsType<ContentResult>(await CreateController(handler, [Read, ShipmentRead, HoldsRead]).Holds(ReturnIdValue, CancellationToken.None)).Content!).RootElement;
        var line = body.GetProperty("lines")[0];
        Assert.Equal("KRN-2026-0007", line.GetProperty("holds")[0].GetProperty("holdNumber").GetString());
        Assert.Equal("KRN-2026-0008", body.GetProperty("other")[0].GetProperty("holdNumber").GetString());
        Assert.Contains(handler.Requests, x => x.Uri.EndsWith($"/api/quarantine/holds/by-source?sourceModule=MOD-0186&sourceDocumentId={ReturnIdValue:D}", StringComparison.Ordinal));
    }

    // RB-25 (list): the customer column comes from the return's own customerRef; customerRef never reaches the page; without
    // crm.account.read nothing is asked of CRM.
    [Fact]
    public async Task Rb25_List_resolves_the_returns_own_customer_and_drops_customerRef()
    {
        var list = $"{{\"items\":[{{\"returnId\":\"{ReturnIdValue:D}\",\"rmaNumber\":\"RMA-1\",\"shipmentId\":\"{Shipment:D}\",\"status\":\"Requested\",\"customerRef\":\"{Account}\"}},{{\"returnId\":\"{Guid.NewGuid():D}\",\"rmaNumber\":\"RMA-2\",\"shipmentId\":\"{Shipment:D}\",\"status\":\"Requested\"}}],\"total\":2,\"contractVersion\":\"v1\"}}";
        var handler = new CaptureHandler(r => Names(r, list));
        var content = Assert.IsType<ContentResult>(await CreateController(handler, [Read, CrmRead]).List(null, null, CancellationToken.None)).Content!;
        Assert.DoesNotContain(Account, content);
        var items = JsonDocument.Parse(content).RootElement.GetProperty("items");
        Assert.Equal("resolved", items[0].GetProperty("customer").GetProperty("state").GetString());
        Assert.Equal("oldRecord", items[1].GetProperty("customer").GetProperty("state").GetString());

        var noCrm = new CaptureHandler(r => Names(r, list));
        var plain = Assert.IsType<ContentResult>(await CreateController(noCrm, [Read]).List(null, null, CancellationToken.None)).Content!;
        Assert.DoesNotContain(Account, plain);
        Assert.DoesNotContain("\"customer\"", plain);
        Assert.DoesNotContain(noCrm.Requests, x => x.Uri.Contains("/api/crm/", StringComparison.Ordinal));
    }


    // VER F-2: the create answer (ReturnResponse 3.5.0) carries customerRef from the service; the browser never receives it.
    [Fact]
    public async Task VerF2_Create_answer_reaches_the_browser_without_customerRef()
    {
        const string body = "{\"shipmentId\":\"44444444-4444-4444-8444-444444444444\",\"reasonCode\":\"DAMAGED\",\"lines\":[{\"shipmentLineNumber\":\"1\",\"quantity\":\"1\",\"uomId\":\"EA\"}]}";
        var handler = new CaptureHandler(request => request.Uri.EndsWith("/returns", StringComparison.Ordinal)
            ? Json(HttpStatusCode.Created, $"{{\"returnId\":\"{ReturnIdValue:D}\",\"rmaNumber\":\"RMA-1\",\"shipmentId\":\"{Shipment:D}\",\"status\":\"Requested\",\"customerRef\":\"{Account}\",\"idempotentReplay\":false,\"contractVersion\":\"v1\"}}", Root)
            : Json(HttpStatusCode.OK, ShipmentDetail("\"" + Root.ToString("D") + "\"")));
        var result = Assert.IsType<ContentResult>(await CreateController(handler, [Create, ShipmentRead], body, idempotencyKey: "k").Create(CancellationToken.None));
        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.DoesNotContain(Account, result.Content!);
        Assert.Contains("RMA-1", result.Content!);
    }

    // VER F-4: two Posted lines with the same SKU and lot cannot be told apart, so their hold is listed apart, not twice.
    [Fact]
    public async Task VerF4_An_ambiguous_hold_is_listed_apart()
    {
        var twoLines = ReturnDetailJson(Account).Replace("\"lines\":[", "\"lines\":[{\"shipmentLineNumber\":\"2\",\"quantity\":\"1.000\",\"uomId\":\"EA\",\"inventoryPosting\":\"Posted\"},");
        var shipment = ShipmentWithLot(Account).Replace("\"lines\":[", $"\"lines\":[{{\"lineNumber\":\"2\",\"skuId\":\"{Sku}\",\"lotId\":\"lot-1\",\"quantity\":\"1.000\",\"uomId\":\"EA\"}},");
        var handler = new CaptureHandler(r =>
        {
            var path = new Uri(r.Uri).AbsolutePath;
            if (path == $"/api/shipment-bundle/returns/{ReturnIdValue:D}") return Json(HttpStatusCode.OK, twoLines);
            if (path == $"/api/shipment-bundle/shipments/{Shipment:D}") return Json(HttpStatusCode.OK, shipment);
            return DetailRoute(r, Account, Account);
        });
        var body = JsonDocument.Parse(Assert.IsType<ContentResult>(await CreateController(handler, [Read, ShipmentRead, HoldsRead]).Holds(ReturnIdValue, CancellationToken.None)).Content!).RootElement;
        Assert.All(body.GetProperty("lines").EnumerateArray(), l => Assert.Equal(0, l.GetProperty("holds").GetArrayLength()));
        Assert.Equal(2, body.GetProperty("other").GetArrayLength());
        Assert.True(body.GetProperty("linesMatched").GetBoolean());
    }

    // VER F-6 / RB-1: Item, Gsku, Lsku and FinishedGood lines all get their name and code from the one bulk read.
    [Fact]
    public async Task Rb1_Every_sku_level_gets_its_name()
    {
        var levels = new[] { ("Item", "99990000-0000-4000-8000-000000000001"), ("Gsku", "99990000-0000-4000-8000-000000000002"), ("Lsku", Sku), ("FinishedGood", "99990000-0000-4000-8000-000000000004") };
        var lines = string.Join(',', levels.Select((l, i) => $"{{\"lineNumber\":\"{i + 1}\",\"itemId\":\"{l.Item2}\",\"skuId\":\"{l.Item2}\",\"skuLevel\":\"{l.Item1}\",\"quantity\":\"1\",\"uomId\":\"EA\"}}"));
        var detail = $"{{\"shipmentId\":\"{Shipment:D}\",\"shipmentNumber\":\"SHP-1\",\"status\":\"Delivered\",\"lifecycleCorrelationId\":\"{Root:D}\",\"lines\":[{lines}],\"contractVersion\":\"v1\"}}";
        var bulk = "{\"items\":[" + string.Join(',', levels.Select(l => $"{{\"skuId\":\"{l.Item2}\",\"skuLevel\":\"{l.Item1}\",\"canonicalCode\":\"C-{l.Item1}\",\"name\":\"N {l.Item1}\",\"lifecycleStatus\":\"Active\"}}")) + "],\"notFound\":[]}";
        var handler = new CaptureHandler(r => new Uri(r.Uri).AbsolutePath == "/api/product-master/skus/bulk-read" ? Json(HttpStatusCode.OK, bulk) : Json(HttpStatusCode.OK, detail));
        var json = JsonSerializer.Serialize(Assert.IsType<JsonResult>(await CreateController(handler, [Create, ShipmentRead]).ResolveShipment(Shipment, CancellationToken.None)).Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var labels = JsonDocument.Parse(json).RootElement.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("skuLabel").GetString()).ToArray();
        Assert.Equal(levels.Select(l => $"C-{l.Item1} \u00B7 N {l.Item1}").ToArray(), labels);
    }
    // RB-26: nothing read from CRM reaches the log on the list, picker and details adapters.
    [Fact]
    public async Task Rb26_Nothing_read_from_crm_reaches_the_log()
    {
        var log = new ReturnsCapturingLogger();
        var list = $"{{\"items\":[{{\"returnId\":\"{ReturnIdValue:D}\",\"rmaNumber\":\"RMA-1\",\"shipmentId\":\"{Shipment:D}\",\"status\":\"Requested\",\"customerRef\":\"{Account}\"}}],\"total\":1}}";
        await CreateController(new CaptureHandler(r => Names(r, list)), [Read, CrmRead], logger: log).List(null, null, CancellationToken.None);
        await CreateController(new CaptureHandler(r => Names(r, $"{{\"items\":[{SummaryRow("SHP-1", Account)}],\"total\":1}}")), [Create, ShipmentRead, CrmRead], logger: log).EligibleShipments(null, 1, false, CancellationToken.None);
        await CreateController(new CaptureHandler(r => DetailRoute(r, Account, null)), [Read, ShipmentRead, CrmRead], logger: log).Detail(ReturnIdValue, CancellationToken.None);
        Assert.NotEmpty(log.Lines);
        foreach (var secret in new[] { "Acme", "C-1", "buyer@example.test", "Ataturk" }) Assert.DoesNotContain(log.Lines, l => l.Contains(secret, StringComparison.Ordinal));
    }

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
        string? body = null, string? idempotencyKey = null, string contentType = "application/json", bool includeScope = true,
        Microsoft.Extensions.Logging.ILogger<SupplyChainReturnsController>? logger = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["GatewayUrl"] = "http://localhost:5000" }).Build();
        var controller = new SupplyChainReturnsController(new HttpClient(handler), configuration,
            logger ?? NullLogger<SupplyChainReturnsController>.Instance);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "returns-tester") };
        if (includeScope)
        {
            claims.Add(new("tenant_id", Tenant.ToString("D")));
            claims.Add(new("legal_entity_id", LegalEntity.ToString("D")));
        }

        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        // R-2 (SHIPMENT-BUNDLE 3.2.0): the adapter reads LegalEntityId from the query, not the token, so the
        // harness sends what the page sends. The adapter builds the downstream query from bound action
        // parameters only, so this key never reaches the gateway.
        context.Request.QueryString = new QueryString("?legalEntityId=" + LegalEntity.ToString("D"));
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

    private sealed class ReturnsCapturingLogger : Microsoft.Extensions.Logging.ILogger<SupplyChainReturnsController>
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Lines.Add(formatter(state, exception) + (exception is null ? string.Empty : " " + exception));
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
