using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.SupplyChain.CapacityPlans;
using Diten.Web.Security;
using Diten.Web.Services.SupplyChain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// MOD-0192 Capacity Planning — same-origin MVC adapter (proxy-profile; UI-SCOPE-0192 §3, G6).
// Browser → this controller → Gateway (GatewayUrl) → SupplyChain /api/supply-chain/capacity-plans. The browser never calls
// the Gateway or holds a token. Bound operations only: createCapacityPlan, getCapacityPlan, createCapacityScenario,
// getCapacityScenario, evaluateCapacityScenario, getCapacityEvaluation. No list of any kind (F192-LIST), no edit/delete.
//
// Header policy (CapacityContextMiddleware, R-2 aligned 2026-10-18): Authorization (server-side) + X-Tenant-Id (from the
// tenant_id claim) + X-Legal-Entity-Id (from ?legalEntityId=, the page's choice; the service has MDM validate it) + the
// browser's X-Correlation-Id (exactly one UUID) + Idempotency-Key on POSTs. The service refuses scope in the query, so
// the downstream URL carries the path only. Bodies are forwarded as sent: the contract is the validator.
[Authorize]
[Route("SupplyChain/CapacityPlans")]
public sealed class SupplyChainCapacityPlansController : Controller
{
    private const string CorrelationHeader = "X-Correlation-Id";
    private const string TenantHeader = "X-Tenant-Id";
    private const string LegalEntityHeader = "X-Legal-Entity-Id";
    private const string IdempotencyHeader = "Idempotency-Key";
    private const string PlansPath = "/api/supply-chain/capacity-plans";

    // The published Capacity codes (CapacityContractError.cs, UI-SCOPE-0192 §5). The adapter never relays a code outside this set.
    private static readonly HashSet<string> PublishedCodes = new(StringComparer.Ordinal)
    {
        "INVALID_REQUEST", "INVALID_CORRELATION_ID", "UNAUTHENTICATED", "FORBIDDEN", "UNKNOWN_CAPACITY_PLAN",
        "UNKNOWN_CAPACITY_SCENARIO", "UNKNOWN_CAPACITY_EVALUATION", "CAPACITY_PLAN_ALREADY_EXISTS",
        "CAPACITY_PLAN_STATE_CONFLICT", "CAPACITY_SCENARIO_NAME_CONFLICT", "EVALUATION_ALREADY_ACTIVE",
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

    // ── Pages ───────────────────────────────────────────────────────────────────────────────────────────────────
    // UAS-001 gating happens in the views (Perms.Has); the adapter actions below check again before any Gateway call.
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/SupplyChain/CapacityPlans/Index.cshtml");

    [HttpGet("Details/{capacityPlanId:guid}")]
    public IActionResult Details(Guid capacityPlanId)
    {
        ViewBag.CapacityPlanId = capacityPlanId;
        // Same rule as the adapter, so the page never forwards a scope the adapter would reject. Empty = fail closed.
        ViewBag.LegalEntityId = LegalEntityScopeRequest.TryResolve(Request, out var legalEntityId) ? legalEntityId.ToString("D") : string.Empty;
        return View("~/Views/SupplyChain/CapacityPlans/Details.cshtml");
    }

    // ── Adapter ─────────────────────────────────────────────────────────────────────────────────────────────────
    [HttpPost("api")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Create(CancellationToken cancellationToken) =>
        MutationAsync(CapacityUiPermissions.Create, PlansPath, cancellationToken);

    [HttpGet("api/{capacityPlanId:guid}")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> GetPlan(Guid capacityPlanId, CancellationToken cancellationToken) =>
        ReadAsync($"{PlansPath}/{capacityPlanId:D}", cancellationToken);

    [HttpPost("api/{capacityPlanId:guid}/scenarios")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CreateScenario(Guid capacityPlanId, CancellationToken cancellationToken) =>
        MutationAsync(CapacityUiPermissions.ScenarioCreate, $"{PlansPath}/{capacityPlanId:D}/scenarios", cancellationToken);

    [HttpGet("api/{capacityPlanId:guid}/scenarios/{scenarioId:guid}")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> GetScenario(Guid capacityPlanId, Guid scenarioId, CancellationToken cancellationToken) =>
        ReadAsync($"{PlansPath}/{capacityPlanId:D}/scenarios/{scenarioId:D}", cancellationToken);

    // 202 Accepted: the evaluation runs in the service's executor; the page reads it back with GetEvaluation (manual
    // Refresh only — F192-POLL stays out).
    [HttpPost("api/{capacityPlanId:guid}/scenarios/{scenarioId:guid}/evaluations")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Evaluate(Guid capacityPlanId, Guid scenarioId, CancellationToken cancellationToken) =>
        MutationAsync(CapacityUiPermissions.Evaluate, $"{PlansPath}/{capacityPlanId:D}/scenarios/{scenarioId:D}/evaluations", cancellationToken);

    [HttpGet("api/{capacityPlanId:guid}/evaluations/{evaluationId:guid}")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> GetEvaluation(Guid capacityPlanId, Guid evaluationId, CancellationToken cancellationToken) =>
        ReadAsync($"{PlansPath}/{capacityPlanId:D}/evaluations/{evaluationId:D}", cancellationToken);

    // ── Proxy ───────────────────────────────────────────────────────────────────────────────────────────────────
    private Task<IActionResult> ReadAsync(string path, CancellationToken cancellationToken)
    {
        if (!HasPermission(CapacityUiPermissions.Read)) return Task.FromResult(ContractFailure(StatusCodes.Status403Forbidden, "FORBIDDEN"));
        return ProxyAsync(HttpMethod.Get, path, null, null, cancellationToken);
    }

    private async Task<IActionResult> MutationAsync(string permission, string path, CancellationToken cancellationToken)
    {
        if (!HasPermission(permission)) return ContractFailure(StatusCodes.Status403Forbidden, "FORBIDDEN");
        if (!Request.HasJsonContentType()) return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        if (!TryGetIdempotencyKey(out var key)) return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        var body = await ReadBodyAsync(cancellationToken);
        return await ProxyAsync(HttpMethod.Post, path, body, key, cancellationToken);
    }

    private async Task<IActionResult> ProxyAsync(HttpMethod method, string path, string? body, string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (string.IsNullOrWhiteSpace(token)) return ContractFailure(StatusCodes.Status401Unauthorized, "UNAUTHENTICATED");
        if (!TryGetBrowserTrace(out var trace)) return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_CORRELATION_ID");
        if (!TryResolveScopeClaim(["tenant_id", "tenantId"], "/tenantId", out var tenantId))
            return ContractFailure(StatusCodes.Status403Forbidden, "FORBIDDEN");
        // The page's company choice. Missing or malformed is a request fault (400), as the service answers it.
        if (!LegalEntityScopeRequest.TryResolve(Request, out var legalEntityId))
            return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");

        var targetUrl = $"{_gatewayUrl}{path}";
        using var request = new HttpRequestMessage(method, targetUrl);
        if (body is not null) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation(TenantHeader, tenantId.ToString("D"));
        request.Headers.TryAddWithoutValidation(LegalEntityHeader, legalEntityId.ToString("D"));
        request.Headers.TryAddWithoutValidation(CorrelationHeader, trace.ToString("D"));
        if (idempotencyKey is not null) request.Headers.TryAddWithoutValidation(IdempotencyHeader, idempotencyKey);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            Response.Headers[CorrelationHeader] = trace.ToString("D");
            _logger.LogInformation("Capacity Gateway answered {StatusCode} for {Method} {TargetUrl}; correlation {CorrelationId}.",
                (int)response.StatusCode, method.Method, targetUrl, trace);
            return (int)response.StatusCode >= 400
                ? await ReenvelopeFailureAsync(response, trace, cancellationToken)
                : new ContentResult
                {
                    StatusCode = (int)response.StatusCode,
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                    Content = await response.Content.ReadAsStringAsync(cancellationToken)
                };
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Outcome unknown on a POST: never reported as rolled back; the page retries with the same key and body.
            _logger.LogWarning(exception, "Capacity Gateway request timed out for {TargetUrl}; correlation {CorrelationId}.", targetUrl, trace);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Capacity Gateway request failed for {TargetUrl}; correlation {CorrelationId}.", targetUrl, trace);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        }
    }

    /// <summary>
    /// Keeps a published code and its status. A failure without one is the Gateway's or the transport's, not the
    /// service's (MODULE-RECIPE 2.1): an un-enveloped 5xx — the Gateway's empty 502 when the service is down — becomes 503
    /// DEPENDENCY_UNAVAILABLE, so the page keeps the outcome open and retries with the same key.
    /// </summary>
    private async Task<IActionResult> ReenvelopeFailureAsync(HttpResponseMessage response, Guid trace,
        CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        var code = CodeOf(await response.Content.ReadAsStringAsync(cancellationToken));
        if (code is null || !PublishedCodes.Contains(code))
        {
            (status, code) = status switch
            {
                StatusCodes.Status401Unauthorized => (status, "UNAUTHENTICATED"),
                StatusCodes.Status403Forbidden => (status, "FORBIDDEN"),
                StatusCodes.Status404NotFound => (status, "UNKNOWN_CAPACITY_PLAN"),
                >= 500 => (StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE"),
                _ => (StatusCodes.Status400BadRequest, "INVALID_REQUEST")
            };
        }

        return Envelope(status, code, trace);
    }

    private static string? CodeOf(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object
                   && error.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
                ? code.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private bool TryGetBrowserTrace(out Guid trace)
    {
        trace = Guid.Empty;
        return Request.Headers.TryGetValue(CorrelationHeader, out var values) && values.Count == 1
               && Guid.TryParseExact(values[0], "D", out trace);
    }

    /// <summary>Exactly one non-empty key, forwarded byte-exact.</summary>
    private bool TryGetIdempotencyKey(out string key)
    {
        key = string.Empty;
        if (!Request.Headers.TryGetValue(IdempotencyHeader, out var values) || values.Count != 1 || string.IsNullOrEmpty(values[0]))
            return false;
        key = values[0]!;
        return true;
    }

    private async Task<string> ReadBodyAsync(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private bool TryResolveScopeClaim(string[] exactNames, string suffix, out Guid value)
    {
        value = Guid.Empty;
        var candidates = User.Claims.Where(c => exactNames.Contains(c.Type, StringComparer.OrdinalIgnoreCase)
                                                || c.Type.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value).Distinct(StringComparer.Ordinal).ToArray();
        return candidates.Length == 1 && Guid.TryParse(candidates[0], out value);
    }

    private IActionResult ContractFailure(int statusCode, string code) =>
        Envelope(statusCode, code, TryGetBrowserTrace(out var trace) ? trace : Guid.NewGuid());

    private IActionResult Envelope(int statusCode, string code, Guid correlationId)
    {
        Response.Headers[CorrelationHeader] = correlationId.ToString("D");
        return StatusCode(statusCode,
            new { error = new { code, message = DefaultMessage(code), correlationId }, contractVersion = "v1" });
    }

    // Wire messages are contract text for logs and API clients; the page shows localized text chosen by `code`.
    private static string DefaultMessage(string code) => code switch
    {
        "UNAUTHENTICATED" => "Authentication required.",
        "FORBIDDEN" => "Required permission is missing.",
        "INVALID_CORRELATION_ID" => "Invalid correlation identifier.",
        "UNKNOWN_CAPACITY_PLAN" => "Capacity plan not found.",
        "DEPENDENCY_UNAVAILABLE" => "Dependency unavailable; retry with the same idempotency key when applicable.",
        _ => "Request validation failed."
    };

    private bool HasPermission(string permission) => PermissionClaims.HasPermission(User, permission);
}
