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

// MOD-0190 S&OP — same-origin adapter contract (UI-SCOPE-0190 §3–§5; G6, 2026-10-18). The header policy is the one
// SandopContextMiddleware enforces: tenant from the claim, legal entity from the page's ?legalEntityId=, the browser
// trace, and the browser's Idempotency-Key on POSTs; bodies forwarded as sent.
public sealed class SupplyChainSandopPlansControllerTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid LegalEntity = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Trace = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid Plan = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ServiceCorrelation = Guid.Parse("55555555-5555-4555-8555-555555555555");

    private const string Read = "supplychain.sandop-plans.read";
    private const string Create = "supplychain.sandop-plans.create";
    private const string Capture = "supplychain.sandop-plans.snapshot.capture";
    private const string SignOff = "supplychain.sandop-plans.sign-off.record";
    private const string Gateway = "http://localhost:5000/api/supply-chain/sandop-plans";

    public static TheoryData<string, string, string, string> Operations => new()
    {
        // name, method, gateway path suffix, permission
        { "Create", "POST", "", Create },
        { "Get", "GET", $"/{Plan:D}", Read },
        { "CaptureSnapshot", "POST", $"/{Plan:D}/snapshots", Capture },
        { "ListSnapshots", "GET", $"/{Plan:D}/snapshots", Read },
        { "RecordSignOff", "POST", $"/{Plan:D}/sign-offs", SignOff },
        { "ListSignOffs", "GET", $"/{Plan:D}/sign-offs", Read }
    };

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task Each_operation_forwards_to_its_gateway_path_with_the_scope_headers(string name, string method, string suffix, string permission)
    {
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.OK, "{\"contractVersion\":\"v1\"}"));
        var post = method == "POST";
        var controller = CreateController(handler, [permission], body: post ? "{\"name\":\" keep spaces \"}" : null, idempotencyKey: post ? "intent-1" : null);

        var result = await Invoke(controller, name);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<ContentResult>(result).StatusCode);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(method, request.Method.Method);
        Assert.Equal(Gateway + suffix, request.Uri);
        Assert.Equal("Bearer test-token", request.Headers["Authorization"]);
        Assert.Equal(Tenant.ToString("D"), request.Headers["X-Tenant-Id"]);
        Assert.Equal(LegalEntity.ToString("D"), request.Headers["X-Legal-Entity-Id"]);
        Assert.Equal(Trace.ToString("D"), request.Headers["X-Correlation-Id"]);
        Assert.DoesNotContain("legalEntityId", request.Uri, StringComparison.OrdinalIgnoreCase);
        if (post)
        {
            Assert.Equal("intent-1", request.Headers["Idempotency-Key"]);
            Assert.Equal("{\"name\":\" keep spaces \"}", request.Body); // byte-exact: no trim, no re-serialization
        }
        else
        {
            Assert.False(request.Headers.ContainsKey("Idempotency-Key"));
        }
    }

    [Theory]
    [MemberData(nameof(Operations))]
    public async Task Each_operation_without_its_permission_is_403_with_zero_gateway_calls(string name, string method, string _, string permission)
    {
        var others = new[] { Read, Create, Capture, SignOff }.Where(p => p != permission).ToArray();
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var controller = CreateController(handler, others, body: method == "POST" ? "{}" : null, idempotencyKey: method == "POST" ? "k" : null);
        AssertFailure(await Invoke(controller, name), StatusCodes.Status403Forbidden, "FORBIDDEN");
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-uuid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task A_missing_or_malformed_legal_entity_is_400_with_zero_gateway_calls(string? legalEntity)
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var controller = CreateController(handler, [Read], legalEntity: legalEntity);
        AssertFailure(await controller.Get(Plan, CancellationToken.None), StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_post_without_an_idempotency_key_or_json_is_400_with_zero_gateway_calls()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        AssertFailure(await CreateController(handler, [Create], body: "{}").Create(CancellationToken.None), StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        AssertFailure(await CreateController(handler, [Create], body: "{}", idempotencyKey: "k", contentType: "text/plain").Create(CancellationToken.None),
            StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_malformed_browser_trace_is_400_INVALID_CORRELATION_ID_with_zero_gateway_calls()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var controller = CreateController(handler, [Read], trace: "not-a-uuid");
        AssertFailure(await controller.Get(Plan, CancellationToken.None), StatusCodes.Status400BadRequest, "INVALID_CORRELATION_ID");
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task A_published_code_keeps_its_status_and_the_correlation_becomes_the_browser_trace()
    {
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.Conflict,
            $"{{\"error\":{{\"code\":\"SIGN_OFF_ALREADY_RECORDED\",\"message\":\"x\",\"correlationId\":\"{ServiceCorrelation:D}\"}},\"contractVersion\":\"v1\"}}"));
        var controller = CreateController(handler, [SignOff], body: "{}", idempotencyKey: "k");
        var json = AssertFailure(await controller.RecordSignOff(Plan, CancellationToken.None), StatusCodes.Status409Conflict, "SIGN_OFF_ALREADY_RECORDED");
        Assert.Contains(Trace.ToString("D"), json);
        Assert.Equal(Trace.ToString("D"), controller.Response.Headers["X-Correlation-Id"].ToString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway, "", StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE")]
    [InlineData(HttpStatusCode.InternalServerError, "{\"error\":{\"code\":\"SECRET_INTERNAL\"}}", StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE")]
    [InlineData(HttpStatusCode.NotFound, "", StatusCodes.Status404NotFound, "UNKNOWN_SANDOP_PLAN")]
    [InlineData(HttpStatusCode.Forbidden, "", StatusCodes.Status403Forbidden, "FORBIDDEN")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "{\"error\":{\"code\":\"SHIPMENT_THING\"}}", StatusCodes.Status400BadRequest, "INVALID_REQUEST")]
    public async Task An_unpublished_or_missing_code_is_reenveloped_by_status(HttpStatusCode upstream, string body, int status, string code)
    {
        var handler = new CaptureHandler(_ => Json(upstream, body));
        var json = AssertFailure(await CreateController(handler, [Read]).Get(Plan, CancellationToken.None), status, code);
        Assert.DoesNotContain("SECRET_INTERNAL", json);
        Assert.DoesNotContain("SHIPMENT_THING", json);
    }

    [Fact]
    public async Task An_unreachable_gateway_is_503_DEPENDENCY_UNAVAILABLE()
    {
        var handler = new CaptureHandler(_ => throw new HttpRequestException("down"));
        AssertFailure(await CreateController(handler, [Read]).Get(Plan, CancellationToken.None), StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
    }

    [Fact]
    public void Details_hands_the_page_only_a_scope_the_adapter_would_accept()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var valid = CreateController(handler, [Read]);
        valid.Details(Plan);
        Assert.Equal(LegalEntity.ToString("D"), (string)valid.ViewBag.LegalEntityId);
        var invalid = CreateController(handler, [Read], legalEntity: "not-a-uuid");
        invalid.Details(Plan);
        Assert.Equal(string.Empty, (string)invalid.ViewBag.LegalEntityId);
    }

    [Fact]
    public void View_routes_are_exactly_the_manifest_pages()
    {
        var prefix = "/" + typeof(SupplyChainSandopPlansController).GetCustomAttribute<RouteAttribute>()!.Template;
        var viewRoutes = Actions()
            .SelectMany(m => m.GetCustomAttributes<HttpGetAttribute>())
            .Select(a => a.Template ?? string.Empty)
            .Where(template => !template.StartsWith("api", StringComparison.Ordinal))
            .Select(template => template.Length == 0 ? prefix : $"{prefix}/{template}")
            .ToHashSet(StringComparer.Ordinal);
        // = SopWorkflowSignoffsManifestProvider RoutePaths (M-04 on the service side).
        Assert.Equal(new HashSet<string>(StringComparer.Ordinal)
            { "/SupplyChain/SandopPlans", "/SupplyChain/SandopPlans/Details/{sandopPlanId:guid}" }, viewRoutes);
    }

    [Fact]
    public void Adapter_surface_is_exactly_the_six_bound_operations()
    {
        var routes = Actions()
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>().Select(a => $"{string.Join(',', a.HttpMethods)} {a.Template}"))
            .Where(route => route.Contains(" api", StringComparison.Ordinal))
            .OrderBy(route => route, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(new[]
        {
            "GET api/{sandopPlanId:guid}",
            "GET api/{sandopPlanId:guid}/sign-offs",
            "GET api/{sandopPlanId:guid}/snapshots",
            "POST api",
            "POST api/{sandopPlanId:guid}/sign-offs",
            "POST api/{sandopPlanId:guid}/snapshots"
        }, routes);
    }

    [Fact]
    public void Every_adapter_is_a_json_endpoint_and_every_post_validates_antiforgery()
    {
        foreach (var method in Actions().Where(m => m.GetCustomAttributes<HttpMethodAttribute>()
                     .Any(a => (a.Template ?? string.Empty).StartsWith("api", StringComparison.Ordinal))))
        {
            Assert.NotNull(method.GetCustomAttribute<JsonAdapterEndpointAttribute>());
            if (method.GetCustomAttributes<HttpPostAttribute>().Any())
                Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static IEnumerable<MethodInfo> Actions() => typeof(SupplyChainSandopPlansController)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

    private static Task<IActionResult> Invoke(SupplyChainSandopPlansController controller, string name) => name switch
    {
        "Create" => controller.Create(CancellationToken.None),
        "Get" => controller.Get(Plan, CancellationToken.None),
        "CaptureSnapshot" => controller.CaptureSnapshot(Plan, CancellationToken.None),
        "ListSnapshots" => controller.ListSnapshots(Plan, CancellationToken.None),
        "RecordSignOff" => controller.RecordSignOff(Plan, CancellationToken.None),
        "ListSignOffs" => controller.ListSignOffs(Plan, CancellationToken.None),
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    private static string AssertFailure(IActionResult result, int status, string code)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(status, objectResult.StatusCode);
        var json = JsonSerializer.Serialize(objectResult.Value);
        Assert.Contains($"\"code\":\"{code}\"", json);
        Assert.Contains("\"contractVersion\":\"v1\"", json);
        return json;
    }

    private static SupplyChainSandopPlansController CreateController(CaptureHandler handler, string[] permissions,
        string? body = null, string? idempotencyKey = null, string contentType = "application/json",
        string? legalEntity = "22222222-2222-4222-8222-222222222222", string trace = "33333333-3333-4333-8333-333333333333")
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["GatewayUrl"] = "http://localhost:5000" }).Build();
        var controller = new SupplyChainSandopPlansController(new HttpClient(handler), configuration,
            NullLogger<SupplyChainSandopPlansController>.Instance);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "sandop-tester"), new("tenant_id", Tenant.ToString("D")) };
        claims.AddRange(permissions.Select(permission => new Claim("permission", permission)));
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) };
        if (legalEntity is not null) context.Request.QueryString = new QueryString("?legalEntityId=" + legalEntity);
        context.Request.Headers.Cookie = "access_token=test-token";
        context.Request.Headers["X-Correlation-Id"] = trace;
        if (idempotencyKey is not null) context.Request.Headers["Idempotency-Key"] = idempotencyKey;
        if (body is not null)
        {
            context.Request.ContentType = contentType;
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        }

        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        response.Headers.TryAddWithoutValidation("X-Correlation-Id", ServiceCorrelation.ToString("D"));
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
