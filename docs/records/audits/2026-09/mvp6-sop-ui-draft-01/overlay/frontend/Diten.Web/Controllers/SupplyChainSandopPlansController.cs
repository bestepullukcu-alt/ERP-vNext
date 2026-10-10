using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.SupplyChain.SandopPlans;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// MOD-0190 S&OP Workflow & Sign-offs — same-origin MVC adapter (pack §23.3/§23.4/§23.8). DRAFT overlay — not built,
// not writer-complete.
//
// Browser → this controller → Gateway (GatewayUrl) → SupplyChain 5061. The browser never calls 5000/5061 or holds a token.
// Bound operations only (pack §23.3): createSandopPlan, getSandopPlan, captureSandopSnapshot, listSandopSnapshots,
// recordSandopSignOff, listSandopSignOffs. There is NO plan list/search/paging route (F190-LIST), and no edit, delete,
// status, bulk, import or export route.
//
// Header policy (pack §23.4; backend SandopContextMiddleware.cs:10-20 in the accepted isolated source 8fa00d40…):
//   Authorization (server-side only) + X-Tenant-Id / X-Legal-Entity-Id from the caller's own claims (never from the
//   browser) + X-Correlation-Id = the browser's per-request UUID + Idempotency-Key (POST, browser per-intent key,
//   forwarded byte-exact). The S&OP correlation is a request trace, not a lifecycle root, so echoing it is safe.
// Depends on Diten.Web.Security.JsonAdapterEndpointAttribute and the JSON challenge branch in frontend Program.cs,
// which exist in the A12 360 overlay (7b6a0d1a…314d), not in the common checkout (README FINDING F2).
[Authorize]
[Route("SupplyChain/SandopPlans")]
public sealed class SupplyChainSandopPlansController : Controller
{
    private const string CorrelationHeader = "X-Correlation-Id";
    private const string TenantHeader = "X-Tenant-Id";
    private const string LegalEntityHeader = "X-Legal-Entity-Id";
    private const string IdempotencyHeader = "Idempotency-Key";
    private const string PlansPath = "/api/supply-chain/sandop-plans";

    // Published S&OP error codes (sandop-capacity.openapi.yaml components/responses; pack §23.8). The adapter never
    // originates a code outside this set.
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

    // ── Pages (manifest SANDOP_PLANS / SANDOP_PLAN_DETAILS, pack §24). UAS-001 gating happens in the views. ────────

    /// <summary>Entry page: "Create plan" and "Open plan by ID" only — no plan list and no list request (F190-LIST).</summary>
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/SupplyChain/SandopPlans/Index.cshtml");

    /// <summary>Plan workspace. The ID is route data only; the page loads the plan through the adapters below.</summary>
    [HttpGet("Details/{sandopPlanId:guid}")]
    public IActionResult Details(Guid sandopPlanId) => View("~/Views/SupplyChain/SandopPlans/Details.cshtml", sandopPlanId);

    // ── Read adapters ───────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>GET /SupplyChain/SandopPlans/api/{id} → getSandopPlan.</summary>
    [HttpGet("api/{sandopPlanId:guid}")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> Get(Guid sandopPlanId, CancellationToken cancellationToken) =>
        ForwardAsync(SandopUiPermissions.Read, HttpMethod.Get, $"{PlansPath}/{sandopPlanId:D}", cancellationToken);

    /// <summary>GET /SupplyChain/SandopPlans/api/{id}/snapshots → listSandopSnapshots.</summary>
    [HttpGet("api/{sandopPlanId:guid}/snapshots")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> ListSnapshots(Guid sandopPlanId, CancellationToken cancellationToken) =>
        ForwardAsync(SandopUiPermissions.Read, HttpMethod.Get, $"{PlansPath}/{sandopPlanId:D}/snapshots", cancellationToken);

    /// <summary>GET /SupplyChain/SandopPlans/api/{id}/sign-offs → listSandopSignOffs.</summary>
    [HttpGet("api/{sandopPlanId:guid}/sign-offs")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> ListSignOffs(Guid sandopPlanId, CancellationToken cancellationToken) =>
        ForwardAsync(SandopUiPermissions.Read, HttpMethod.Get, $"{PlansPath}/{sandopPlanId:D}/sign-offs", cancellationToken);

    // ── Mutation adapters (antiforgery on every POST, pack §23.4) ──────────────────────────────────────────────

    /// <summary>POST /SupplyChain/SandopPlans/api → createSandopPlan.</summary>
    [HttpPost("api")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Create(CancellationToken cancellationToken) =>
        ForwardAsync(SandopUiPermissions.Create, HttpMethod.Post, PlansPath, cancellationToken);

    /// <summary>POST /SupplyChain/SandopPlans/api/{id}/snapshots → captureSandopSnapshot.</summary>
    [HttpPost("api/{sandopPlanId:guid}/snapshots")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CaptureSnapshot(Guid sandopPlanId, CancellationToken cancellationToken) =>
        ForwardAsync(SandopUiPermissions.Capture, HttpMethod.Post, $"{PlansPath}/{sandopPlanId:D}/snapshots", cancellationToken);

    /// <summary>POST /SupplyChain/SandopPlans/api/{id}/sign-offs → recordSandopSignOff.</summary>
    [HttpPost("api/{sandopPlanId:guid}/sign-offs")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RecordSignOff(Guid sandopPlanId, CancellationToken cancellationToken) =>
        ForwardAsync(SandopUiPermissions.SignOff, HttpMethod.Post, $"{PlansPath}/{sandopPlanId:D}/sign-offs", cancellationToken);

    // ── Proxy ───────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every gate runs before any Gateway call: permission (403), token (401), browser trace (400
    /// INVALID_CORRELATION_ID), trusted scope from the caller's claims (403), then for POST the JSON content type and
    /// exactly one Idempotency-Key (400 INVALID_REQUEST). The POST body text is forwarded unchanged.
    /// </summary>
    private async Task<IActionResult> ForwardAsync(string permission, HttpMethod method, string path,
        CancellationToken cancellationToken)
    {
        var mutation = method == HttpMethod.Post;
        if (!HasPermission(permission)) return ContractFailure(StatusCodes.Status403Forbidden, "FORBIDDEN");
        if (!TryGetToken(out var token)) return ContractFailure(StatusCodes.Status401Unauthorized, "UNAUTHENTICATED");
        if (!TryGetBrowserTrace(out var trace))
        {
            return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_CORRELATION_ID");
        }

        if (!TryResolveScopeClaim(["tenant_id", "tenantId"], "/tenantId", out var tenantId)
            || !TryResolveScopeClaim(["legal_entity_id", "legalEntityId"], "/legalEntityId", out var legalEntityId))
        {
            return ContractFailure(StatusCodes.Status403Forbidden, "FORBIDDEN");
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
        request.Headers.TryAddWithoutValidation(TenantHeader, tenantId.ToString("D"));
        request.Headers.TryAddWithoutValidation(LegalEntityHeader, legalEntityId.ToString("D"));
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
            if (status >= 400) return await ReenvelopeFailureAsync(response, trace, mutation, cancellationToken);
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
            _logger.LogWarning(exception, "S&OP request timed out. Trace {Trace} Method {Method}.", trace, method);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable, UnavailableCode(mutation));
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "S&OP request failed. Trace {Trace} Method {Method}.", trace, method);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable, UnavailableCode(mutation));
        }
    }

    private static string UnavailableCode(bool mutation) => mutation ? "COMMIT_RESULT_UNRESOLVED" : "DEPENDENCY_UNAVAILABLE";

    /// <summary>
    /// Keeps the backend's status and published code; the correlationId is the browser trace (the backend echoes the
    /// same value). A response without a published code (for example a Gateway error page) is mapped by status to a
    /// published code; 5xx becomes 503 so the browser offers the same-key retry. Upstream text is never relayed raw.
    /// </summary>
    private async Task<IActionResult> ReenvelopeFailureAsync(HttpResponseMessage response, Guid trace, bool mutation,
        CancellationToken cancellationToken)
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
                StatusCodes.Status404NotFound => (status, "UNKNOWN_SANDOP_PLAN"),
                >= 500 => (StatusCodes.Status503ServiceUnavailable, UnavailableCode(mutation)),
                _ => (StatusCodes.Status400BadRequest, "INVALID_REQUEST")
            };
        }

        return StatusCode(status,
            new { error = new { code, message = DefaultMessage(code), correlationId = trace }, contractVersion = "v1" });
    }

    private bool TryGetToken(out string token)
    {
        token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(token);
    }

    /// <summary>Exactly one X-Correlation-Id in the 8-4-4-4-12 "D" form, as SandopContextMiddleware.cs:7 requires.</summary>
    private bool TryGetBrowserTrace(out Guid trace)
    {
        trace = Guid.Empty;
        return Request.Headers.TryGetValue(CorrelationHeader, out var values)
               && values.Count == 1
               && Guid.TryParseExact(values[0], "D", out trace);
    }

    /// <summary>Exactly one non-empty key, forwarded byte-exact (SandopContextMiddleware.cs:19).</summary>
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

    private bool TryResolveScopeClaim(string[] exactNames, string suffix, out Guid value)
    {
        value = Guid.Empty;
        var candidates = User.Claims.Where(c => exactNames.Contains(c.Type, StringComparer.OrdinalIgnoreCase)
                                                || c.Type.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value).Distinct(StringComparer.Ordinal).ToArray();
        return candidates.Length == 1 && Guid.TryParseExact(candidates[0], "D", out value) && value != Guid.Empty;
    }

    private IActionResult ContractFailure(int statusCode, string code)
    {
        var correlationId = TryGetBrowserTrace(out var trace) ? trace : Guid.NewGuid();
        Response.Headers[CorrelationHeader] = correlationId.ToString("D");
        return StatusCode(statusCode,
            new { error = new { code, message = DefaultMessage(code), correlationId }, contractVersion = "v1" });
    }

    // Wire messages are the backend's contract text (SandopContractError.cs:4) for logs and API clients; the page shows
    // localized text chosen by `code`, never this text.
    private static string DefaultMessage(string code) => code switch
    {
        "INVALID_CORRELATION_ID" => "Invalid correlation identifier",
        "UNAUTHENTICATED" => "Authentication required",
        "FORBIDDEN" => "Permission denied",
        "UNKNOWN_SANDOP_PLAN" => "S&OP plan not found",
        "SANDOP_PLAN_ALREADY_EXISTS" => "S&OP plan already exists for this horizon and demand version",
        "IDEMPOTENCY_KEY_REUSED" => "Idempotency key was used with a different valid payload",
        "SANDOP_PLAN_STATE_CONFLICT" => "S&OP plan state does not allow a new snapshot",
        "SANDOP_SIGN_OFF_STATE_CONFLICT" => "S&OP plan state does not permit sign-off",
        "INVALID_SNAPSHOT_REFERENCE" => "Snapshot does not belong to the S&OP plan",
        "SIGN_OFF_ALREADY_RECORDED" => "This role already recorded a decision for the snapshot",
        "INVALID_DEMAND_REFERENCE" => "Referenced DEMAND plan version is unknown or not published",
        "COMMIT_RESULT_UNRESOLVED" => "Commit result unresolved; retry with the same key",
        "DEPENDENCY_UNAVAILABLE" => "Dependency unavailable",
        _ => "Invalid request"
    };

    private bool HasPermission(string permission) => PermissionClaims.HasPermission(User, permission);
}
