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

// MOD-0192 Capacity — same-origin adapter contract (pack §23.3/§23.4/§23.8; route table, permission gates, header policy,
// published-code envelope, §24 M-04 frontend half). DRAFT overlay — not built, not run.
public sealed class SupplyChainCapacityPlansControllerTests
{
    private static readonly Guid Tenant = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid LegalEntity = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid Trace = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid Plan = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid Scenario = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid Evaluation = Guid.Parse("66666666-6666-4666-8666-666666666666");

    private const string Read = "supplychain.capacity-plans.read";
    private const string Create = "supplychain.capacity-plans.create";
    private const string ScenarioCreate = "supplychain.capacity-plans.scenario.create";
    private const string Evaluate = "supplychain.capacity-plans.evaluate";
    private const string Gateway = "http://localhost:5000/api/supply-chain/capacity-plans";

    // ── Reads ───────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_forwards_to_getCapacityPlan_with_token_and_browser_trace_and_no_scope_header()
    {
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.OK, "{\"capacityPlanId\":\"" + Plan + "\",\"contractVersion\":\"v1\"}"));
        var result = await CreateController(handler, [Read]).Get(Plan, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<ContentResult>(result).StatusCode);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal($"{Gateway}/{Plan:D}", request.Uri);
        Assert.Equal("Bearer test-token", request.Headers["Authorization"]);
        Assert.Equal(Trace.ToString("D"), request.Headers["X-Correlation-Id"]);
        Assert.False(request.Headers.ContainsKey("Idempotency-Key"));
        // Scope is server-side only: the backend reads it from the signed token (CapacityContextMiddleware.cs:46-49).
        Assert.False(request.Headers.ContainsKey("X-Tenant-Id"));
        Assert.False(request.Headers.ContainsKey("X-Legal-Entity-Id"));
        Assert.DoesNotContain("tenantId", request.Uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Scenario_and_evaluation_reads_hit_only_their_published_operations()
    {
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.OK, "{\"contractVersion\":\"v1\"}"));
        var controller = CreateController(handler, [Read]);
        await controller.GetScenario(Plan, Scenario, CancellationToken.None);
        await controller.GetEvaluation(Plan, Evaluation, CancellationToken.None);
        Assert.Equal(new[] { $"{Gateway}/{Plan:D}/scenarios/{Scenario:D}", $"{Gateway}/{Plan:D}/evaluations/{Evaluation:D}" },
            handler.Requests.Select(r => r.Uri).ToArray());
        Assert.All(handler.Requests, r => Assert.Equal(HttpMethod.Get, r.Method));
    }

    [Fact]
    public async Task Browser_supplied_scope_headers_are_never_forwarded()
    {
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.OK, "{\"contractVersion\":\"v1\"}"));
        var controller = CreateController(handler, [Read]);
        controller.HttpContext.Request.Headers["X-Tenant-Id"] = Guid.NewGuid().ToString("D");
        controller.HttpContext.Request.Headers["X-Legal-Entity-Id"] = Guid.NewGuid().ToString("D");
        await controller.Get(Plan, CancellationToken.None);
        var request = Assert.Single(handler.Requests);
        Assert.False(request.Headers.ContainsKey("X-Tenant-Id"));
        Assert.False(request.Headers.ContainsKey("X-Legal-Entity-Id"));
    }

    // ── Mutations ───────────────────────────────────────────────────────────────────────────────────────────────

    public static TheoryData<string, string, string, int> Mutations => new()
    {
        { "Create", Create, "", 201 },
        { "CreateScenario", ScenarioCreate, "/{plan}/scenarios", 201 },
        { "Evaluate", Evaluate, "/{plan}/scenarios/{scenario}/evaluations", 202 }
    };

    [Theory]
    [MemberData(nameof(Mutations))]
    public async Task Mutation_forwards_body_text_and_idempotency_key_byte_exact(string action, string permission, string suffix, int status)
    {
        // Untrimmed, key order as typed, a decimal kept as a string, a comma inside the key — nothing is rewritten.
        const string body = "{\"name\":\" Scenario A \",\"adjustments\":[{\"availableCapacityDelta\":\"-2.50\"}]}";
        const string key = "intent-1, with comma";
        var handler = new CaptureHandler(_ => Json((HttpStatusCode)status, "{\"contractVersion\":\"v1\"}"));
        var controller = CreateController(handler, [permission], body, key);

        var result = await Invoke(controller, action);

        Assert.Equal(status, Assert.IsType<ContentResult>(result).StatusCode);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(Gateway + suffix.Replace("{plan}", Plan.ToString("D")).Replace("{scenario}", Scenario.ToString("D")), request.Uri);
        Assert.Equal(body, request.Body);
        Assert.Equal(key, request.Headers["Idempotency-Key"]);
        Assert.Equal(Trace.ToString("D"), request.Headers["X-Correlation-Id"]);
        Assert.False(request.Headers.ContainsKey("X-Tenant-Id"));
    }

    [Theory]
    [MemberData(nameof(Mutations))]
    public async Task Mutation_without_its_key_is_403_with_zero_gateway_calls(string action, string permission, string suffix, int status)
    {
        Assert.NotNull(suffix); // theory row shape shared with the forwarding test
        Assert.True(status > 0);
        var others = new[] { Read, Create, ScenarioCreate, Evaluate }.Where(p => p != permission).ToArray();
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var result = await Invoke(CreateController(handler, others, "{}", "k"), action);
        AssertFailure(result, StatusCodes.Status403Forbidden, "FORBIDDEN");
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [MemberData(nameof(Mutations))]
    public async Task Mutation_without_idempotency_key_or_json_is_400_INVALID_REQUEST(string action, string permission, string suffix, int status)
    {
        Assert.NotNull(suffix); // theory row shape shared with the forwarding test
        Assert.True(status > 0);
        var noKey = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        AssertFailure(await Invoke(CreateController(noKey, [permission], "{}", null), action), StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        var notJson = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        AssertFailure(await Invoke(CreateController(notJson, [permission], "{}", "k", "text/plain"), action), StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        Assert.Empty(noKey.Requests);
        Assert.Empty(notJson.Requests);
    }

    // ── Gates and failures ──────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reads_without_read_key_are_403_with_zero_gateway_calls()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var controller = CreateController(handler, [Create, ScenarioCreate, Evaluate]);
        AssertFailure(await controller.Get(Plan, CancellationToken.None), StatusCodes.Status403Forbidden, "FORBIDDEN");
        AssertFailure(await controller.GetScenario(Plan, Scenario, CancellationToken.None), StatusCodes.Status403Forbidden, "FORBIDDEN");
        AssertFailure(await controller.GetEvaluation(Plan, Evaluation, CancellationToken.None), StatusCodes.Status403Forbidden, "FORBIDDEN");
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("{33333333-3333-4333-8333-333333333333}")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("not-a-uuid")]
    public async Task Malformed_or_nil_browser_trace_is_400_INVALID_CORRELATION_ID(string trace)
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var controller = CreateController(handler, [Read]);
        controller.HttpContext.Request.Headers["X-Correlation-Id"] = trace;
        AssertFailure(await controller.Get(Plan, CancellationToken.None), StatusCodes.Status400BadRequest, "INVALID_CORRELATION_ID");
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Nil_route_id_is_400_INVALID_REQUEST_with_zero_gateway_calls()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var controller = CreateController(handler, [Read]);
        AssertFailure(await controller.Get(Guid.Empty, CancellationToken.None), StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        AssertFailure(await controller.GetScenario(Plan, Guid.Empty, CancellationToken.None), StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        AssertFailure(await controller.GetEvaluation(Plan, Guid.Empty, CancellationToken.None), StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Missing_token_is_401_UNAUTHENTICATED()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException("must not be called"));
        var controller = CreateController(handler, [Read]);
        controller.HttpContext.Request.Headers.Cookie = string.Empty;
        AssertFailure(await controller.Get(Plan, CancellationToken.None), StatusCodes.Status401Unauthorized, "UNAUTHENTICATED");
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Upstream_published_code_passes_through_with_the_trace_as_reference()
    {
        var foreignTrace = Guid.NewGuid();
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.Conflict,
            $"{{\"error\":{{\"code\":\"EVALUATION_ALREADY_ACTIVE\",\"message\":\"x\",\"correlationId\":\"{foreignTrace}\"}},\"contractVersion\":\"v1\"}}"));
        var json = AssertFailure(await CreateController(handler, [Evaluate], "{}", "k").Evaluate(Plan, Scenario, CancellationToken.None),
            StatusCodes.Status409Conflict, "EVALUATION_ALREADY_ACTIVE");
        Assert.Contains(Trace.ToString("D"), json);
        Assert.DoesNotContain(foreignTrace.ToString("D"), json);
    }

    [Fact]
    public async Task Unpublished_upstream_code_is_never_relayed()
    {
        var handler = new CaptureHandler(_ => Json(HttpStatusCode.Conflict,
            "{\"error\":{\"code\":\"INTERNAL_SECRET_STATE\",\"message\":\"stack\"},\"contractVersion\":\"v1\"}"));
        var json = AssertFailure(await CreateController(handler, [ScenarioCreate], "{}", "k").CreateScenario(Plan, CancellationToken.None),
            StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        Assert.DoesNotContain("INTERNAL_SECRET_STATE", json);
        Assert.DoesNotContain("stack", json);
    }

    [Fact]
    public async Task Codeless_404_maps_to_the_resource_specific_published_code()
    {
        static CaptureHandler NotFound() => new(_ => new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("not found") });
        AssertFailure(await CreateController(NotFound(), [Read]).Get(Plan, CancellationToken.None),
            StatusCodes.Status404NotFound, "UNKNOWN_CAPACITY_PLAN");
        AssertFailure(await CreateController(NotFound(), [Read]).GetScenario(Plan, Scenario, CancellationToken.None),
            StatusCodes.Status404NotFound, "UNKNOWN_CAPACITY_SCENARIO");
        AssertFailure(await CreateController(NotFound(), [Read]).GetEvaluation(Plan, Evaluation, CancellationToken.None),
            StatusCodes.Status404NotFound, "UNKNOWN_CAPACITY_EVALUATION");
    }

    [Fact]
    public async Task Non_contract_5xx_becomes_503_with_a_published_code_per_kind()
    {
        var read = new CaptureHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>bad gateway</html>") });
        var readJson = AssertFailure(await CreateController(read, [Read]).GetEvaluation(Plan, Evaluation, CancellationToken.None),
            StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        Assert.DoesNotContain("<html>", readJson);

        var write = new CaptureHandler(_ => new HttpResponseMessage(HttpStatusCode.GatewayTimeout) { Content = new StringContent("timeout") });
        AssertFailure(await CreateController(write, [Evaluate], "{}", "k").Evaluate(Plan, Scenario, CancellationToken.None),
            StatusCodes.Status503ServiceUnavailable, "COMMIT_RESULT_UNRESOLVED");
    }

    [Fact]
    public async Task Mutation_timeout_is_never_reported_as_rolled_back()
    {
        var handler = new CaptureHandler(_ => throw new TaskCanceledException("timeout"));
        AssertFailure(await CreateController(handler, [Create], "{}", "k").Create(CancellationToken.None),
            StatusCodes.Status503ServiceUnavailable, "COMMIT_RESULT_UNRESOLVED");
        var read = new CaptureHandler(_ => throw new HttpRequestException("refused"));
        AssertFailure(await CreateController(read, [Read]).GetScenario(Plan, Scenario, CancellationToken.None),
            StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
    }

    // ── Route table (pack §23.4; §24 M-04 frontend half) ─────────────────────────────────────────────────────────

    [Fact]
    public void View_routes_are_exactly_the_two_manifest_pages()
    {
        var views = Actions()
            .Where(m => m.ReturnType == typeof(IActionResult))
            .SelectMany(m => m.GetCustomAttributes<HttpGetAttribute>())
            .Select(a => "/SupplyChain/CapacityPlans" + (string.IsNullOrEmpty(a.Template) ? string.Empty : "/" + a.Template))
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(views.SetEquals(new[] { "/SupplyChain/CapacityPlans", "/SupplyChain/CapacityPlans/Details/{capacityPlanId:guid}" }));
    }

    [Fact]
    public void Api_routes_are_exactly_the_six_bound_operations_with_no_list_route()
    {
        var routes = Actions()
            .SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>().Select(a => (Verb: a.HttpMethods.Single(), a.Template)))
            .Where(r => r.Template?.StartsWith("api", StringComparison.Ordinal) == true)
            .Select(r => $"{r.Verb} {r.Template}")
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(routes.SetEquals(new[]
        {
            "GET api/{capacityPlanId:guid}", "POST api",
            "POST api/{capacityPlanId:guid}/scenarios", "GET api/{capacityPlanId:guid}/scenarios/{scenarioId:guid}",
            "POST api/{capacityPlanId:guid}/scenarios/{scenarioId:guid}/evaluations",
            "GET api/{capacityPlanId:guid}/evaluations/{evaluationId:guid}"
        }), string.Join(" | ", routes));
        Assert.DoesNotContain("GET api", routes);
        Assert.DoesNotContain("GET api/{capacityPlanId:guid}/scenarios", routes);
        Assert.DoesNotContain(Actions().SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>()),
            a => a.HttpMethods.Any(v => v is "PUT" or "PATCH" or "DELETE"));
    }

    [Fact]
    public void Controller_is_authorized_under_the_tenant_route_and_every_adapter_is_json_with_antiforgery_on_post()
    {
        var type = typeof(SupplyChainCapacityPlansController);
        Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("SupplyChain/CapacityPlans", type.GetCustomAttribute<RouteAttribute>()!.Template);
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

    private static IEnumerable<MethodInfo> Actions() => typeof(SupplyChainCapacityPlansController)
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

    private static Task<IActionResult> Invoke(SupplyChainCapacityPlansController controller, string action) => action switch
    {
        "Create" => controller.Create(CancellationToken.None),
        "CreateScenario" => controller.CreateScenario(Plan, CancellationToken.None),
        "Evaluate" => controller.Evaluate(Plan, Scenario, CancellationToken.None),
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

    private static SupplyChainCapacityPlansController CreateController(CaptureHandler handler, string[] permissions,
        string? body = null, string? idempotencyKey = null, string contentType = "application/json")
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["GatewayUrl"] = "http://localhost:5000" }).Build();
        var controller = new SupplyChainCapacityPlansController(new HttpClient(handler), configuration,
            NullLogger<SupplyChainCapacityPlansController>.Instance);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "capacity-tester"),
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
