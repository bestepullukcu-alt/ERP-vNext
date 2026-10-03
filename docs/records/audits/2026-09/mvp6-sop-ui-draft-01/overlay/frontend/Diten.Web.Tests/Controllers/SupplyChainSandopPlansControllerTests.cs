using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Controllers;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Web.Tests.Controllers;

// MOD-0190 S&OP — same-origin adapter contract (pack §23.3/§23.4/§23.8; SU-01, SU-02, SU-05, SU-06…SU-08, SU-13…SU-16,
// SU-18 route half, §24 M-04/W-01 frontend half). DRAFT overlay — not built, not run.
public sealed class SupplyChainSandopPlansControllerTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid LegalEntity = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Trace = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid Plan = Guid.Parse("44444444-4444-4444-8444-444444444444");

    private const string Read = "supplychain.sandop-plans.read";
    private const string Create = "supplychain.sandop-plans.create";
    private const string Capture = "supplychain.sandop-plans.snapshot.capture";
    private const string SignOff = "supplychain.sandop-plans.sign-off.record";
    private const string Gateway = "http://localhost:5000/api/supply-chain/sandop-plans";

    // ── Reads ───────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_forwards_to_getSandopPlan_with_server_side_scope_and_the_browser_trace()
    {
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.OK, "{\"sandopPlanId\":\"" + Plan + "\",\"contractVersion\":\"v1\"}"));
        var result = await CreateController(handler, [Read]).Get(Plan, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<ContentResult>(result).StatusCode);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"{Gateway}/{Plan:D}", request.Uri);
        Assert.Equal("Bearer test-token", request.Headers["Authorization"]);
        Assert.Equal(Tenant.ToString("D"), request.Headers["X-Tenant-Id"]);
        Assert.Equal(LegalEntity.ToString("D"), request.Headers["X-Legal-Entity-Id"]);
        Assert.Equal(Trace.ToString("D"), request.Headers["X-Correlation-Id"]);
        Assert.False(request.Headers.ContainsKey("Idempotency-Key"));
    }

    [Fact]
    public async Task List_adapters_hit_only_the_two_published_list_operations()
    {
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.OK, "{\"items\":[],\"contractVersion\":\"v1\"}"));
        var controller = CreateController(handler, [Read]);
        await controller.ListSnapshots(Plan, CancellationToken.None);
        await controller.ListSignOffs(Plan, CancellationToken.None);
        Assert.Equal(new[] { $"{Gateway}/{Plan:D}/snapshots", $"{Gateway}/{Plan:D}/sign-offs" },
            handler.Requests.Select(r => r.Uri).ToArray());
        Assert.All(handler.Requests, r => Assert.Equal(HttpMethod.Get, r.Method));
    }

    // ── Mutations ───────────────────────────────────────────────────────────────────────────────────────────────

    public static TheoryData<string, string, string> Mutations => new()
    {
        { "Create", Create, "" },
        { "CaptureSnapshot", Capture, "/{plan}/snapshots" },
        { "RecordSignOff", SignOff, "/{plan}/sign-offs" }
    };

    [Theory]
    [MemberData(nameof(Mutations))]
    public async Task Mutation_forwards_body_text_and_idempotency_key_byte_exact(string action, string permission, string suffix)
    {
        // Untrimmed, key order as typed, a comma inside the key — the adapter must not rewrite anything.
        const string body = "{\"name\":\" Plan A \",\"demandPlanVersion\":\"v1\"}";
        const string key = "intent-1, with comma";
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.Created, "{\"contractVersion\":\"v1\"}"));
        var controller = CreateController(handler, [permission], body, key);

        var result = await Invoke(controller, action);

        Assert.Equal(StatusCodes.Status201Created, Assert.IsType<ContentResult>(result).StatusCode);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(Gateway + suffix.Replace("{plan}", Plan.ToString("D")), request.Uri);
        Assert.Equal(body, request.Body);
        Assert.Equal(key, request.Headers["Idempotency-Key"]);
        Assert.Equal(Trace.ToString("D"), request.Headers["X-Correlation-Id"]);
        Assert.Equal(Tenant.ToString("D"), request.Headers["X-Tenant-Id"]);
    }

    [Theory]
    [MemberData(nameof(Mutations))]
    public async Task Mutation_without_its_key_is_403_with_zero_gateway_calls(string action, string permission, string suffix)
    {
        Assert.NotNull(suffix); // theory row shape shared with the forwarding test
        var others = new[] { Read, Create, Capture, SignOff }.Where(p => p != permission).ToArray();
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var result = await Invoke(CreateController(handler, others, "{}", "k"), action);
        AssertFailure(result, StatusCodes.Status403Forbidden, "FORBIDDEN");
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [MemberData(nameof(Mutations))]
    public async Task Mutation_without_idempotency_key_or_json_is_400_INVALID_REQUEST(string action, string permission, string suffix)
    {
        Assert.NotNull(suffix); // theory row shape shared with the forwarding test
        var noKey = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        AssertFailure(await Invoke(CreateController(noKey, [permission], "{}", null), action), StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        var notJson = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        AssertFailure(await Invoke(CreateController(notJson, [permission], "{}", "k", "text/plain"), action), StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        Assert.Empty(noKey.Requests);
        Assert.Empty(notJson.Requests);
    }

    // ── Gates and failures ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Read_without_read_key_is_403_with_zero_gateway_calls()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        AssertFailure(await CreateController(handler, [Create, Capture, SignOff]).Get(Plan, CancellationToken.None),
            StatusCodes.Status403Forbidden, "FORBIDDEN");
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Missing_or_malformed_browser_trace_is_400_INVALID_CORRELATION_ID()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var controller = CreateController(handler, [Read]);
        controller.HttpContext.Request.Headers["X-Correlation-Id"] = "{" + Trace + "}";
        AssertFailure(await controller.Get(Plan, CancellationToken.None), StatusCodes.Status400BadRequest, "INVALID_CORRELATION_ID");
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Missing_tenant_claim_is_403_and_scope_never_comes_from_the_browser()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var controller = CreateController(handler, [Read], includeScope: false);
        controller.HttpContext.Request.Headers["X-Tenant-Id"] = Tenant.ToString("D");
        controller.HttpContext.Request.Headers["X-Legal-Entity-Id"] = LegalEntity.ToString("D");
        AssertFailure(await controller.Get(Plan, CancellationToken.None), StatusCodes.Status403Forbidden, "FORBIDDEN");
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Upstream_published_code_passes_through_with_the_trace_as_reference()
    {
        var foreignTrace = Guid.NewGuid();
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.NotFound,
            $"{{\"error\":{{\"code\":\"UNKNOWN_SANDOP_PLAN\",\"message\":\"S&OP plan not found\",\"correlationId\":\"{foreignTrace}\"}},\"contractVersion\":\"v1\"}}"));
        var json = AssertFailure(await CreateController(handler, [Read]).Get(Plan, CancellationToken.None),
            StatusCodes.Status404NotFound, "UNKNOWN_SANDOP_PLAN");
        Assert.Contains(Trace.ToString("D"), json);
        Assert.DoesNotContain(foreignTrace.ToString("D"), json);
    }

    [Fact]
    public async Task Non_contract_5xx_becomes_503_with_a_published_code_per_kind()
    {
        var read = new CaptureHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>bad gateway</html>") });
        var readJson = AssertFailure(await CreateController(read, [Read]).Get(Plan, CancellationToken.None),
            StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        Assert.DoesNotContain("<html>", readJson);

        var write = new CaptureHandler(_ => new HttpResponseMessage(HttpStatusCode.GatewayTimeout) { Content = new StringContent("timeout") });
        AssertFailure(await CreateController(write, [Capture], "{}", "k").CaptureSnapshot(Plan, CancellationToken.None),
            StatusCodes.Status503ServiceUnavailable, "COMMIT_RESULT_UNRESOLVED");
    }

    [Fact]
    public async Task Mutation_timeout_is_never_reported_as_rolled_back()
    {
        var handler = new CaptureHandler(_ => throw new TaskCanceledException("timeout"));
        AssertFailure(await CreateController(handler, [SignOff], "{}", "k").RecordSignOff(Plan, CancellationToken.None),
            StatusCodes.Status503ServiceUnavailable, "COMMIT_RESULT_UNRESOLVED");
        var read = new CaptureHandler(_ => throw new HttpRequestException("refused"));
        AssertFailure(await CreateController(read, [Read]).ListSnapshots(Plan, CancellationToken.None),
            StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
    }

    // ── Route table (SU-18 half; §24 M-04 / shared guard W-01 frontend half) ─────────────────────────────────────

    [Fact]
    public void View_routes_are_exactly_the_two_manifest_pages()
    {
        var views = Actions()
            .Where(m => m.ReturnType == typeof(IActionResult))
            .SelectMany(m => m.GetCustomAttributes<HttpGetAttribute>())
            .Select(a => "/SupplyChain/SandopPlans" + (string.IsNullOrEmpty(a.Template) ? string.Empty : "/" + a.Template))
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(views.SetEquals(new[] { "/SupplyChain/SandopPlans", "/SupplyChain/SandopPlans/Details/{sandopPlanId:guid}" }));
    }

    [Fact]
    public void Api_routes_are_exactly_the_six_bound_operations_and_nothing_else()
    {
        var routes = Actions()
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>().Select(a => (Verb: a.HttpMethods.Single(), a.Template)))
            .Where(r => r.Template?.StartsWith("api", StringComparison.Ordinal) == true)
            .Select(r => $"{r.Verb} {r.Template}")
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(routes.SetEquals(new[]
        {
            "GET api/{sandopPlanId:guid}", "POST api",
            "GET api/{sandopPlanId:guid}/snapshots", "POST api/{sandopPlanId:guid}/snapshots",
            "GET api/{sandopPlanId:guid}/sign-offs", "POST api/{sandopPlanId:guid}/sign-offs"
        }), string.Join(" | ", routes));
        Assert.DoesNotContain(Actions().SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>()),
            a => a.HttpMethods.Any(v => v is "PUT" or "PATCH" or "DELETE"));
    }

    [Fact]
    public void Controller_is_authorized_under_the_tenant_route_and_every_adapter_is_json_with_antiforgery_on_post()
    {
        var type = typeof(SupplyChainSandopPlansController);
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("SupplyChain/SandopPlans", type.GetCustomAttribute<RouteAttribute>()!.Template);
        foreach (var method in Actions().Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any(a => a.Template?.StartsWith("api") == true)))
        {
            Assert.NotNull(method.GetCustomAttribute<JsonAdapterEndpointAttribute>());
            if (method.GetCustomAttributes<HttpPostAttribute>().Any())
            {
                Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
            }
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static IEnumerable<MethodInfo> Actions() => typeof(SupplyChainSandopPlansController)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

    private static Task<IActionResult> Invoke(SupplyChainSandopPlansController controller, string action) => action switch
    {
        "Create" => controller.Create(CancellationToken.None),
        "CaptureSnapshot" => controller.CaptureSnapshot(Plan, CancellationToken.None),
        "RecordSignOff" => controller.RecordSignOff(Plan, CancellationToken.None),
        _ => throw new ArgumentOutOfRangeException(nameof(action))
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
        string? body = null, string? idempotencyKey = null, string contentType = "application/json", bool includeScope = true)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["GatewayUrl"] = "http://localhost:5000" }).Build();
        var controller = new SupplyChainSandopPlansController(new HttpClient(handler), configuration,
            NullLogger<SupplyChainSandopPlansController>.Instance);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "sandop-tester") };
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

    private static HttpResponseMessage Json(HttpStatusCode status, string body)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        response.Headers.TryAddWithoutValidation("X-Correlation-Id", Trace.ToString("D"));
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
