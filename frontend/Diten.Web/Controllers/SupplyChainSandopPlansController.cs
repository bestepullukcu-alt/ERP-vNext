using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.SupplyChain.SandopPlans;
using Diten.Web.Security;
using Diten.Web.Services.SupplyChain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// MOD-0190 S&OP Workflow & Sign-offs — same-origin MVC adapter (proxy-profile; UI-SCOPE-0190 §3, G6).
// Browser → this controller → Gateway (GatewayUrl) → SupplyChain /api/supply-chain/sandop-plans. The browser never calls
// the Gateway or holds a token. Bound operations only: createSandopPlan, getSandopPlan, captureSandopSnapshot,
// listSandopSnapshots, recordSandopSignOff, listSandopSignOffs. No plan list (F190-LIST), no edit/delete/status change.
//
// Header policy (SandopContextMiddleware): Authorization (server-side) + X-Tenant-Id (from the tenant_id claim) +
// X-Legal-Entity-Id (from ?legalEntityId=, the page's choice; the service checks it against the token) + the browser's
// X-Correlation-Id (exactly one UUID) + Idempotency-Key on POSTs (exactly one, forwarded as sent).
// Request bodies are forwarded as the browser sent them: the contract is the validator, the adapter adds no rule.
[Authorize]
[Route("SupplyChain/SandopPlans")]
public sealed class SupplyChainSandopPlansController : Controller
{
    private const string CorrelationHeader = "X-Correlation-Id";
    private const string TenantHeader = "X-Tenant-Id";
    private const string LegalEntityHeader = "X-Legal-Entity-Id";
    private const string IdempotencyHeader = "Idempotency-Key";
    private const string PlansPath = "/api/supply-chain/sandop-plans";

    // The published S&OP codes (SandopContractError.cs, UI-SCOPE-0190 §5). The adapter never relays a code outside this set.
    private static readonly HashSet<string> PublishedCodes = new(StringComparer.Ordinal)
    {
        "INVALID_REQUEST", "INVALID_CORRELATION_ID", "UNAUTHENTICATED", "FORBIDDEN", "UNKNOWN_SANDOP_PLAN",
        "SANDOP_PLAN_ALREADY_EXISTS", "SANDOP_PLAN_STATE_CONFLICT", "SANDOP_SIGN_OFF_STATE_CONFLICT",
        "SIGN_OFF_ALREADY_RECORDED", "IDEMPOTENCY_KEY_REUSED", "INVALID_DEMAND_REFERENCE", "INVALID_SNAPSHOT_REFERENCE",
        "DEPENDENCY_UNAVAILABLE", "COMMIT_RESULT_UNRESOLVED"
    };

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<SupplyChainSandopPlansController> _logger;

    public SupplyChainSandopPlansController(HttpClient httpClient, IConfiguration configuration,
        ILogger<SupplyChainSandopPlansController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = (configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.")).TrimEnd('/');
        _logger = logger;
    }

    // ── Pages ───────────────────────────────────────────────────────────────────────────────────────────────────
    // UAS-001 gating happens in the views (Perms.Has); the adapter actions below check again before any Gateway call.
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/SupplyChain/SandopPlans/Index.cshtml");

    [HttpGet("Details/{sandopPlanId:guid}")]
    public IActionResult Details(Guid sandopPlanId)
    {
        ViewBag.SandopPlanId = sandopPlanId;
        // Same rule as the adapter, so the page never forwards a scope the adapter would reject. Empty = fail closed.
        ViewBag.LegalEntityId = LegalEntityScopeRequest.TryResolve(Request, out var legalEntityId) ? legalEntityId.ToString("D") : string.Empty;
        return View("~/Views/SupplyChain/SandopPlans/Details.cshtml");
    }

    // ── Adapter ─────────────────────────────────────────────────────────────────────────────────────────────────
    [HttpPost("api")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Create(CancellationToken cancellationToken) =>
        MutationAsync(SandopUiPermissions.Create, PlansPath, cancellationToken);

    [HttpGet("api/{sandopPlanId:guid}")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> Get(Guid sandopPlanId, CancellationToken cancellationToken) =>
        ReadAsync($"{PlansPath}/{sandopPlanId:D}", cancellationToken);

    [HttpPost("api/{sandopPlanId:guid}/snapshots")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CaptureSnapshot(Guid sandopPlanId, CancellationToken cancellationToken) =>
        MutationAsync(SandopUiPermissions.Capture, $"{PlansPath}/{sandopPlanId:D}/snapshots", cancellationToken);

    [HttpGet("api/{sandopPlanId:guid}/snapshots")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> ListSnapshots(Guid sandopPlanId, CancellationToken cancellationToken) =>
        ReadAsync($"{PlansPath}/{sandopPlanId:D}/snapshots", cancellationToken);

    [HttpPost("api/{sandopPlanId:guid}/sign-offs")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RecordSignOff(Guid sandopPlanId, CancellationToken cancellationToken) =>
        MutationAsync(SandopUiPermissions.SignOff, $"{PlansPath}/{sandopPlanId:D}/sign-offs", cancellationToken);

    [HttpGet("api/{sandopPlanId:guid}/sign-offs")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> ListSignOffs(Guid sandopPlanId, CancellationToken cancellationToken) =>
        ReadAsync($"{PlansPath}/{sandopPlanId:D}/sign-offs", cancellationToken);

    // ── Proxy ───────────────────────────────────────────────────────────────────────────────────────────────────
    private Task<IActionResult> ReadAsync(string path, CancellationToken cancellationToken)
    {
        if (!HasPermission(SandopUiPermissions.Read)) return Task.FromResult(ContractFailure(StatusCodes.Status403Forbidden, "FORBIDDEN"));
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
            _logger.LogInformation("S&OP Gateway answered {StatusCode} for {Method} {TargetUrl}; correlation {CorrelationId}.",
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
            _logger.LogWarning(exception, "S&OP Gateway request timed out for {TargetUrl}; correlation {CorrelationId}.", targetUrl, trace);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "S&OP Gateway request failed for {TargetUrl}; correlation {CorrelationId}.", targetUrl, trace);
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
                StatusCodes.Status404NotFound => (status, "UNKNOWN_SANDOP_PLAN"),
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
        "UNKNOWN_SANDOP_PLAN" => "S&OP plan not found.",
        "DEPENDENCY_UNAVAILABLE" => "Dependency unavailable; retry with the same idempotency key when applicable.",
        _ => "Request validation failed."
    };

    private bool HasPermission(string permission) => PermissionClaims.HasPermission(User, permission);
}
