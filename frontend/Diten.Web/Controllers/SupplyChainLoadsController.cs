using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Diten.Web.Services.SupplyChain;

namespace Diten.Web.Controllers;

// MOD-0185 Routing & Load Planning — Loads same-origin MVC adapter (pack §30, scope mvp6-loads-ui-scope-01). Built by R-4b
// against MODULE-RECIPE.md. Browser → this controller → Gateway (GatewayUrl) → SupplyChain. Bound operations only: queryLoads
// and createLoadPlan. No transition route (ROOT-UI-01 keeps the transition UI out of this slice), no detail, edit or delete.
//
// The create's X-Correlation-Id IS the new Load's root (CreateLoadHandler passes context.CorrelationId as the root, and
// LoadRepository.cs:16 answers a replay under a different root with 409 CORRELATION_ROOT_MISMATCH), so the adapter forwards
// the browser's id unchanged and the page keeps one per opened form (MODULE-RECIPE 3.2). The create body is forwarded as
// the exact text the browser sent: model binding would re-serialize plannedDepartAt and the stop sequence.
[Authorize]
[Route("SupplyChain/Loads")]
public sealed class SupplyChainLoadsController : Controller
{
    internal const string ReadPermission = "supplychain.loads.read";
    internal const string CreatePermission = "supplychain.loads.create";

    private const string CorrelationHeader = "X-Correlation-Id";
    private const string TenantHeader = "X-Tenant-Id";
    private const string LegalEntityHeader = "X-Legal-Entity-Id";
    private const string IdempotencyHeader = "Idempotency-Key";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<SupplyChainLoadsController> _logger;

    public SupplyChainLoadsController(HttpClient httpClient, IConfiguration configuration, ILogger<SupplyChainLoadsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = (configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.")).TrimEnd('/');
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/SupplyChain/Loads/Index.cshtml");

    // Only the two published query parameters, forwarded as given; the backend validates them. Absent → omitted.
    [HttpGet("api")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? carrierId, CancellationToken cancellationToken)
    {
        if (!HasPermission(ReadPermission))
            return Task.FromResult(ContractFailure(StatusCodes.Status403Forbidden));

        var query = new List<string>(2);
        if (status is not null) query.Add($"status={Uri.EscapeDataString(status)}");
        if (carrierId is not null) query.Add($"carrierId={Uri.EscapeDataString(carrierId)}");
        var target = $"{_gatewayUrl}/api/shipment-bundle/loads{(query.Count == 0 ? string.Empty : "?" + string.Join('&', query))}";
        return ProxyAsync(HttpMethod.Get, target, content: null, includeIdempotencyKey: false, cancellationToken);
    }

    [HttpPost("api")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        if (!HasPermission(CreatePermission))
            return ContractFailure(StatusCodes.Status403Forbidden);
        if (!Request.HasJsonContentType())
            return ContractFailure(StatusCodes.Status415UnsupportedMediaType);

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancellationToken);
        return await ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/shipment-bundle/loads",
            new StringContent(body, Encoding.UTF8, "application/json"), includeIdempotencyKey: true, cancellationToken);
    }

    private async Task<IActionResult> ProxyAsync(HttpMethod method, string targetUrl, HttpContent? content,
        bool includeIdempotencyKey, CancellationToken cancellationToken)
    {
        if (!TryCreateGatewayRequest(method, targetUrl, content, includeIdempotencyKey, out var request, out var localStatus))
            return ContractFailure(localStatus);

        // MODULE-RECIPE 2.3: every line carries the id the Gateway and the service log for this request.
        var correlation = ResolveTraceCorrelation();
        try
        {
            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                CopyCorrelationHeader(response);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                // MODULE-RECIPE 2.1 (Q371): a gateway 5xx with no contract body (service down) gets the adapter's own envelope.
                if ((int)response.StatusCode >= 500 && !HasContractError(body))
                {
                    _logger.LogWarning("Loads Gateway answered {StatusCode} without a contract error for {TargetUrl}; correlation {CorrelationId}.",
                        (int)response.StatusCode, targetUrl, correlation);
                    return ContractFailure(StatusCodes.Status503ServiceUnavailable);
                }
                _logger.LogInformation("Loads Gateway answered {StatusCode} for {Method} {TargetUrl}; correlation {CorrelationId}.",
                    (int)response.StatusCode, method.Method, targetUrl, correlation);
                return new ContentResult
                {
                    StatusCode = (int)response.StatusCode,
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                    Content = body
                };
            }
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Loads Gateway request timed out for {TargetUrl}; correlation {CorrelationId}.", targetUrl, correlation);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Loads Gateway request failed for {TargetUrl}; correlation {CorrelationId}.", targetUrl, correlation);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable);
        }
    }

    // Token (401), scope claims (403), then exactly one UUID X-Correlation-Id (400, MODULE-RECIPE 2.2: the adapter rejects, it
    // never mints one for the request) and, for mutations, exactly one Idempotency-Key (400) — all before any Gateway call.
    private bool TryCreateGatewayRequest(HttpMethod method, string targetUrl, HttpContent? content, bool includeIdempotencyKey,
        out HttpRequestMessage request, out int localStatus)
    {
        request = new HttpRequestMessage(method, targetUrl) { Content = content };
        localStatus = StatusCodes.Status403Forbidden;
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (string.IsNullOrWhiteSpace(token)) return Fail(request, StatusCodes.Status401Unauthorized, out localStatus);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (!TryResolveScopeClaim(["tenant_id", "tenantId"], "/tenantId", out var tenantId))
            return Fail(request, StatusCodes.Status403Forbidden, out localStatus);
        // R-2 (SHIPMENT-BUNDLE 3.2.0): LegalEntityId is the user's choice on the page, arriving as
        // ?legalEntityId=. Missing or malformed is a REQUEST fault (400), not an authorization answer (403) —
        // the service's own middleware answers a missing X-Legal-Entity-Id the same way.
        if (!LegalEntityScopeRequest.TryResolve(Request, out var legalEntityId))
            return Fail(request, StatusCodes.Status400BadRequest, out localStatus);
        request.Headers.TryAddWithoutValidation(TenantHeader, tenantId.ToString("D"));
        request.Headers.TryAddWithoutValidation(LegalEntityHeader, legalEntityId.ToString("D"));
        if (!Request.Headers.TryGetValue(CorrelationHeader, out var correlation) || correlation.Count != 1
            || !Guid.TryParse(correlation[0], out var correlationId))
            return Fail(request, StatusCodes.Status400BadRequest, out localStatus);
        request.Headers.TryAddWithoutValidation(CorrelationHeader, correlationId.ToString("D"));
        if (includeIdempotencyKey)
        {
            if (!Request.Headers.TryGetValue(IdempotencyHeader, out var key) || key.Count != 1 || string.IsNullOrEmpty(key[0]))
                return Fail(request, StatusCodes.Status400BadRequest, out localStatus);
            request.Headers.TryAddWithoutValidation(IdempotencyHeader, key[0]);
        }
        return true;
    }

    private static bool Fail(HttpRequestMessage request, int status, out int localStatus)
    {
        request.Dispose();
        localStatus = status;
        return false;
    }

    private bool TryResolveScopeClaim(string[] exactNames, string suffix, out Guid value)
    {
        value = Guid.Empty;
        var candidates = User.Claims
            .Where(claim => exactNames.Contains(claim.Type, StringComparer.OrdinalIgnoreCase)
                || claim.Type.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .Select(claim => claim.Value).Distinct(StringComparer.Ordinal).ToArray();
        return candidates.Length == 1 && Guid.TryParse(candidates[0], out value);
    }

    private void CopyCorrelationHeader(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues(CorrelationHeader, out var values))
            Response.Headers[CorrelationHeader] = values.ToArray();
    }

    private IActionResult ContractFailure(int statusCode)
    {
        var correlationId = ResolveTraceCorrelation();
        Response.Headers[CorrelationHeader] = correlationId.ToString("D");
        var (code, message) = statusCode switch
        {
            StatusCodes.Status401Unauthorized => ("INVALID_REQUEST", "Authentication required."),
            StatusCodes.Status403Forbidden => ("INVALID_REQUEST", "Required authorization context or permission is missing."),
            StatusCodes.Status415UnsupportedMediaType => ("INVALID_REQUEST", "Content type must be application/json."),
            StatusCodes.Status503ServiceUnavailable => ("PERSISTENCE_UNAVAILABLE", "Persistence outcome is unavailable; retry the request using the same idempotency key when applicable."),
            _ => ("INVALID_REQUEST", "Request schema validation failed.")
        };
        return StatusCode(statusCode, new { error = new { code, message, correlationId }, contractVersion = "v1" });
    }

    private Guid ResolveTraceCorrelation() =>
        Request.Headers.TryGetValue(CorrelationHeader, out var values) && values.Count == 1 && Guid.TryParse(values[0], out var id)
            ? id : Guid.NewGuid();

    private static bool HasContractError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return false;
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == JsonValueKind.Object
                && error.TryGetProperty("code", out var code)
                && code.ValueKind == JsonValueKind.String;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private bool HasPermission(string permission) => PermissionClaims.HasPermission(User, permission);
}
