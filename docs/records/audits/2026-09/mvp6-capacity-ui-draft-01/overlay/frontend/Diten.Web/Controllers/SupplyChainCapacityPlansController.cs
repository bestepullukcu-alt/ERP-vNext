using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.SupplyChain.CapacityPlans;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// MOD-0192 Capacity Planning — same-origin MVC adapter (pack §23.3/§23.4/§23.8). DRAFT overlay — not built,
// not writer-complete.
//
// Browser → this controller → Gateway (GatewayUrl) → SupplyChain 5061. The browser never calls 5000/5061 or holds a token.
// Bound operations only (pack §23.3): createCapacityPlan, getCapacityPlan, createCapacityScenario, getCapacityScenario,
// evaluateCapacityScenario, getCapacityEvaluation. There is NO list/search/paging route for plans, scenarios or
// evaluations (F192-LIST), and no edit, delete, approve, archive, cancel, bulk, import or export route.
//
// Header policy (pack §23.4; backend CapacityContextMiddleware.cs:30-64 in the accepted isolated source BC-SOURCE
// ebd5d80c…7064): Authorization (server-side only) + X-Correlation-Id = the browser's per-request UUID + Idempotency-Key
// (POST, browser per-intent key, forwarded byte-exact). NO tenant/legal-entity header and no scope query key: the backend
// takes scope from the signed token claims only (middleware lines 46-49) and rejects tenantId/legalEntityId query keys
// (lines 60-61), so scope never leaves the server side of this adapter (README A2).
// Depends on Diten.Web.Security.JsonAdapterEndpointAttribute and the JSON challenge branch in frontend Program.cs,
// which exist in the A12 360 overlay (7b6a0d1a…314d), not in the common checkout (README FINDING F2).
[Authorize]
[Route("SupplyChain/CapacityPlans")]
public sealed class SupplyChainCapacityPlansController : Controller
{
    private const string CorrelationHeader = "X-Correlation-Id";
    private const string IdempotencyHeader = "Idempotency-Key";
    private const string PlansPath = "/api/supply-chain/capacity-plans";

    // Published Capacity error codes (sandop-capacity.openapi.yaml components/responses; pack §23.8). The adapter never
    // originates or relays a code outside this set.
    private static readonly HashSet<string> PublishedCodes = new(StringComparer.Ordinal)
    {
        "INVALID_REQUEST", "INVALID_CORRELATION_ID", "UNAUTHENTICATED", "FORBIDDEN", "UNKNOWN_CAPACITY_PLAN",
        "UNKNOWN_CAPACITY_SCENARIO", "UNKNOWN_CAPACITY_EVALUATION", "CAPACITY_PLAN_ALREADY_EXISTS",
        "CAPACITY_SCENARIO_NAME_CONFLICT", "CAPACITY_PLAN_STATE_CONFLICT", "EVALUATION_ALREADY_ACTIVE",
        "IDEMPOTENCY_KEY_REUSED", "INVALID_DEMAND_REFERENCE", "INVALID_CONSTRAINT_REFERENCE", "DEPENDENCY_UNAVAILABLE",
        "COMMIT_RESULT_UNRESOLVED"
    };

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<SupplyChainCapacityPlansController> _logger;

    public SupplyChainCapacityPlansController(HttpClient httpClient, IConfiguration configuration,
        ILogger<SupplyChainCapacityPlansController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = (configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.")).TrimEnd('/');
        _logger = logger;
    }

    // ── Pages (manifest CAPACITY_PLANS / CAPACITY_PLAN_DETAILS, pack §24). UAS-001 gating happens in the views. ──────

    /// <summary>Entry page: "Create plan" and "Open plan by ID" only — no plan list and no list request (F192-LIST).</summary>
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/SupplyChain/CapacityPlans/Index.cshtml");

    /// <summary>Plan workspace. The ID is route data only; the page loads the plan through the adapters below.</summary>
    [HttpGet("Details/{capacityPlanId:guid}")]
    public IActionResult Details(Guid capacityPlanId) =>
        View("~/Views/SupplyChain/CapacityPlans/Details.cshtml", capacityPlanId);

    // ── Read adapters ───────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>GET /SupplyChain/CapacityPlans/api/{capacityPlanId} → getCapacityPlan.</summary>
    [HttpGet("api/{capacityPlanId:guid}")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> Get(Guid capacityPlanId, CancellationToken cancellationToken) =>
        ForwardAsync(CapacityUiPermissions.Read, HttpMethod.Get, [capacityPlanId],
            $"{PlansPath}/{capacityPlanId:D}", "UNKNOWN_CAPACITY_PLAN", cancellationToken);

    /// <summary>GET /SupplyChain/CapacityPlans/api/{capacityPlanId}/scenarios/{scenarioId} → getCapacityScenario.</summary>
    [HttpGet("api/{capacityPlanId:guid}/scenarios/{scenarioId:guid}")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> GetScenario(Guid capacityPlanId, Guid scenarioId, CancellationToken cancellationToken) =>
        ForwardAsync(CapacityUiPermissions.Read, HttpMethod.Get, [capacityPlanId, scenarioId],
            $"{PlansPath}/{capacityPlanId:D}/scenarios/{scenarioId:D}", "UNKNOWN_CAPACITY_SCENARIO", cancellationToken);

    /// <summary>GET /SupplyChain/CapacityPlans/api/{capacityPlanId}/evaluations/{evaluationId} → getCapacityEvaluation.
    /// Called once per manual Refresh click; the adapter never polls or retries (F192-POLL).</summary>
    [HttpGet("api/{capacityPlanId:guid}/evaluations/{evaluationId:guid}")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> GetEvaluation(Guid capacityPlanId, Guid evaluationId, CancellationToken cancellationToken) =>
        ForwardAsync(CapacityUiPermissions.Read, HttpMethod.Get, [capacityPlanId, evaluationId],
            $"{PlansPath}/{capacityPlanId:D}/evaluations/{evaluationId:D}", "UNKNOWN_CAPACITY_EVALUATION", cancellationToken);

    // ── Mutation adapters (antiforgery on every POST, pack §23.4) ──────────────────────────────────────────────

    /// <summary>POST /SupplyChain/CapacityPlans/api → createCapacityPlan (201).</summary>
    [HttpPost("api")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Create(CancellationToken cancellationToken) =>
        ForwardAsync(CapacityUiPermissions.Create, HttpMethod.Post, [], PlansPath, "UNKNOWN_CAPACITY_PLAN",
            cancellationToken);

    /// <summary>POST /SupplyChain/CapacityPlans/api/{capacityPlanId}/scenarios → createCapacityScenario (201).</summary>
    [HttpPost("api/{capacityPlanId:guid}/scenarios")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CreateScenario(Guid capacityPlanId, CancellationToken cancellationToken) =>
        ForwardAsync(CapacityUiPermissions.ScenarioCreate, HttpMethod.Post, [capacityPlanId],
            $"{PlansPath}/{capacityPlanId:D}/scenarios", "UNKNOWN_CAPACITY_PLAN", cancellationToken);

    /// <summary>POST /SupplyChain/CapacityPlans/api/{capacityPlanId}/scenarios/{scenarioId}/evaluations →
    /// evaluateCapacityScenario (202).</summary>
    [HttpPost("api/{capacityPlanId:guid}/scenarios/{scenarioId:guid}/evaluations")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Evaluate(Guid capacityPlanId, Guid scenarioId, CancellationToken cancellationToken) =>
        ForwardAsync(CapacityUiPermissions.Evaluate, HttpMethod.Post, [capacityPlanId, scenarioId],
            $"{PlansPath}/{capacityPlanId:D}/scenarios/{scenarioId:D}/evaluations", "UNKNOWN_CAPACITY_SCENARIO",
            cancellationToken);

    // ── Proxy ───────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every gate runs before any Gateway call: permission (403), token (401), browser trace (400
    /// INVALID_CORRELATION_ID), non-nil route IDs (400 INVALID_REQUEST, as CapacityContextMiddleware.cs:58-59), then for
    /// POST the JSON content type and exactly one Idempotency-Key (400 INVALID_REQUEST). The POST body text is forwarded
    /// unchanged. <paramref name="notFoundCode"/> is the published code a code-less 404 maps to for this resource.
    /// </summary>
    private async Task<IActionResult> ForwardAsync(string permission, HttpMethod method, Guid[] routeIds, string path,
        string notFoundCode, CancellationToken cancellationToken)
    {
        var mutation = method == HttpMethod.Post;
        if (!HasPermission(permission)) return ContractFailure(StatusCodes.Status403Forbidden, "FORBIDDEN");
        if (!TryGetToken(out var token)) return ContractFailure(StatusCodes.Status401Unauthorized, "UNAUTHENTICATED");
        if (!TryGetBrowserTrace(out var trace))
        {
            return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_CORRELATION_ID");
        }

        if (routeIds.Any(id => id == Guid.Empty))
        {
            return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        }

        string? body = null;
        string? idempotencyKey = null;
        if (mutation)
        {
            if (!Request.HasJsonContentType() || !TryGetIdempotencyKey(out idempotencyKey))
            {
                return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");
            }

            body = await ReadBodyAsync(cancellationToken);
        }

        using var request = new HttpRequestMessage(method, $"{_gatewayUrl}{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation(CorrelationHeader, trace.ToString("D"));
        if (mutation)
        {
            request.Headers.TryAddWithoutValidation(IdempotencyHeader, idempotencyKey);
            request.Content = new StringContent(body!, Encoding.UTF8, "application/json");
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            Response.Headers[CorrelationHeader] = trace.ToString("D");
            var status = (int)response.StatusCode;
            if (status >= 400)
            {
                return await ReenvelopeFailureAsync(response, trace, mutation, notFoundCode, cancellationToken);
            }

            return new ContentResult
            {
                StatusCode = status,
                ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                Content = await response.Content.ReadAsStringAsync(cancellationToken)
            };
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // A mutation's outcome is unknown after a timeout: never reported as rolled back (pack §23.8);
            // the browser retries with the same key and identical body text.
            _logger.LogWarning(exception, "Capacity request timed out. Trace {Trace} Method {Method}.", trace, method);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable, UnavailableCode(mutation));
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Capacity request failed. Trace {Trace} Method {Method}.", trace, method);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable, UnavailableCode(mutation));
        }
    }

    private static string UnavailableCode(bool mutation) => mutation ? "COMMIT_RESULT_UNRESOLVED" : "DEPENDENCY_UNAVAILABLE";

    /// <summary>
    /// Keeps the backend's status and published code; the correlationId is the browser trace (the backend echoes the
    /// same value, CapacityContextMiddleware.cs:36-38). A response without a published code (for example a Gateway error
    /// page) is mapped by status to a published code; 5xx becomes 503 so the browser offers the same-key retry.
    /// Upstream text is never relayed raw.
    /// </summary>
    private async Task<IActionResult> ReenvelopeFailureAsync(HttpResponseMessage response, Guid trace, bool mutation,
        string notFoundCode, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        string? code = null;
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("code", out var c)
                && c.ValueKind == JsonValueKind.String
                && PublishedCodes.Contains(c.GetString()!))
            {
                code = c.GetString();
            }
        }
        catch (JsonException)
        {
            // Not a contract envelope: mapped by status below.
        }

        if (code is null)
        {
            (status, code) = status switch
            {
                StatusCodes.Status401Unauthorized => (status, "UNAUTHENTICATED"),
                StatusCodes.Status403Forbidden => (status, "FORBIDDEN"),
                StatusCodes.Status404NotFound => (status, notFoundCode),
                >= 500 => (StatusCodes.Status503ServiceUnavailable, UnavailableCode(mutation)),
                _ => (StatusCodes.Status400BadRequest, "INVALID_REQUEST")
            };
        }

        return StatusCode(status,
            new { error = new { code, message = DefaultMessage(code, status), correlationId = trace }, contractVersion = "v1" });
    }

    private bool TryGetToken(out string token)
    {
        token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(token);
    }

    /// <summary>Exactly one non-nil X-Correlation-Id in the 8-4-4-4-12 "D" form (CapacityContextMiddleware.cs:11-12, 34-36).</summary>
    private bool TryGetBrowserTrace(out Guid trace)
    {
        trace = Guid.Empty;
        return Request.Headers.TryGetValue(CorrelationHeader, out var values)
               && values.Count == 1
               && Guid.TryParseExact(values[0], "D", out trace)
               && trace != Guid.Empty;
    }

    /// <summary>Exactly one non-empty key, forwarded byte-exact (CapacityContextMiddleware.cs:53-55).</summary>
    private bool TryGetIdempotencyKey(out string? key)
    {
        key = null;
        if (!Request.Headers.TryGetValue(IdempotencyHeader, out var values) || values.Count != 1
            || string.IsNullOrEmpty(values[0]))
        {
            return false;
        }

        key = values[0];
        return true;
    }

    private async Task<string> ReadBodyAsync(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private IActionResult ContractFailure(int statusCode, string code)
    {
        var correlationId = TryGetBrowserTrace(out var trace) ? trace : Guid.NewGuid();
        Response.Headers[CorrelationHeader] = correlationId.ToString("D");
        return StatusCode(statusCode,
            new { error = new { code, message = DefaultMessage(code, statusCode), correlationId }, contractVersion = "v1" });
    }

    // Wire messages are the backend's contract text (CapacityContractError.cs:41-55 in BC-SOURCE) for logs and API
    // clients; the page shows localized text chosen by `code`, never this text.
    private static string DefaultMessage(string code, int status) => code switch
    {
        "UNKNOWN_CAPACITY_PLAN" => "Capacity plan not found",
        "UNKNOWN_CAPACITY_SCENARIO" => "Capacity scenario not found",
        "UNKNOWN_CAPACITY_EVALUATION" => "Capacity evaluation not found",
        "CAPACITY_PLAN_ALREADY_EXISTS" => "Capacity plan already exists for this horizon and demand version",
        "CAPACITY_PLAN_STATE_CONFLICT" => "Capacity plan state does not allow a new scenario",
        "CAPACITY_SCENARIO_NAME_CONFLICT" => "Capacity scenario name already exists in this plan",
        "EVALUATION_ALREADY_ACTIVE" => "An evaluation is already active for this scenario",
        "IDEMPOTENCY_KEY_REUSED" => "Idempotency key was used with a different valid payload",
        "INVALID_DEMAND_REFERENCE" => "Supplied checksum differs from the exact scoped test fixture",
        "INVALID_CONSTRAINT_REFERENCE" => "Constraint reference is invalid",
        _ => status switch
        {
            StatusCodes.Status401Unauthorized => "Authentication required",
            StatusCodes.Status403Forbidden => "Permission denied",
            StatusCodes.Status503ServiceUnavailable => code == "COMMIT_RESULT_UNRESOLVED"
                ? "Commit result unresolved; retry with the same key"
                : "Dependency unavailable",
            _ => "Invalid request"
        }
    };

    private bool HasPermission(string permission) => PermissionClaims.HasPermission(User, permission);
}
