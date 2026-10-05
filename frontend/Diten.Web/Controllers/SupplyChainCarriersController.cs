using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Diten.Web.Models.SupplyChain.Carriers;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Authorize]
[Route("SupplyChain/Carriers")]
public sealed class SupplyChainCarriersController : Controller
{
    internal const string ReadPermission = "supplychain.carriers.read";
    internal const string CreatePermission = "supplychain.carriers.create";
    internal const string StatusPermission = "supplychain.carriers.status.change";

    private const string CorrelationHeader = "X-Correlation-Id";
    private const string TenantHeader = "X-Tenant-Id";
    private const string LegalEntityHeader = "X-Legal-Entity-Id";
    private const string IdempotencyHeader = "Idempotency-Key";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<SupplyChainCarriersController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public SupplyChainCarriersController(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SupplyChainCarriersController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = (configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.")).TrimEnd('/');
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/SupplyChain/Carriers/Index.cshtml");

    [HttpGet("api")]
    [JsonAdapterEndpoint] // R-4a (MODULE-RECIPE 2.4): without it Q394's challenge cannot tell this endpoint from a page — 302, not JSON 401
    public Task<IActionResult> List([FromQuery] string? status, CancellationToken cancellationToken)
    {
        if (!HasPermission(ReadPermission))
            return Task.FromResult(ContractFailure(StatusCodes.Status403Forbidden));

        var target = $"{_gatewayUrl}/api/shipment-bundle/carriers";
        if (status is not null)
            target += $"?status={Uri.EscapeDataString(status)}";

        return ProxyAsync(HttpMethod.Get, target, content: null, includeIdempotencyKey: false, cancellationToken);
    }

    [HttpPost("api")]
    [JsonAdapterEndpoint] // R-4a (MODULE-RECIPE 2.4): without it Q394's challenge cannot tell this endpoint from a page — 302, not JSON 401
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Create([FromBody] CreateCarrierViewModel model, CancellationToken cancellationToken)
    {
        if (!HasPermission(CreatePermission))
            return Task.FromResult(ContractFailure(StatusCodes.Status403Forbidden));
        if (!ModelState.IsValid)
            return Task.FromResult(ContractFailure(StatusCodes.Status400BadRequest));

        return ProxyAsync(
            HttpMethod.Post,
            $"{_gatewayUrl}/api/shipment-bundle/carriers",
            JsonContent.Create(model, options: _jsonOptions),
            includeIdempotencyKey: true,
            cancellationToken);
    }

    [HttpPost("api/{carrierId:guid}/status")]
    [JsonAdapterEndpoint] // R-4a (MODULE-RECIPE 2.4): without it Q394's challenge cannot tell this endpoint from a page — 302, not JSON 401
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ChangeStatus(
        Guid carrierId,
        [FromBody] ChangeCarrierStatusViewModel model,
        CancellationToken cancellationToken)
    {
        if (!HasPermission(StatusPermission))
            return Task.FromResult(ContractFailure(StatusCodes.Status403Forbidden));
        if (!ModelState.IsValid)
            return Task.FromResult(ContractFailure(StatusCodes.Status400BadRequest));

        return ProxyAsync(
            HttpMethod.Post,
            $"{_gatewayUrl}/api/shipment-bundle/carriers/{carrierId:D}/status",
            JsonContent.Create(model, options: _jsonOptions),
            includeIdempotencyKey: true,
            cancellationToken);
    }

    private async Task<IActionResult> ProxyAsync(
        HttpMethod method,
        string targetUrl,
        HttpContent? content,
        bool includeIdempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryCreateGatewayRequest(method, targetUrl, content, includeIdempotencyKey, out var request, out var localStatus))
            return ContractFailure(localStatus);

        // R-4a (MODULE-RECIPE 2.3): every line below carries the trace the browser sent, so one user action can be followed
        // through the Web, Gateway and service logs by the same value.
        var trace = ResolveTraceCorrelation();
        try
        {
            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                CopyCorrelationHeader(response);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                // R-4a (MODULE-RECIPE 2.1, Q371): a gateway 5xx with no contract body (service down) is the same case as an
                // unreachable gateway, so it gets the same envelope; passed through, the page found no error code.
                if ((int)response.StatusCode >= 500 && !HasContractError(body))
                {
                    _logger.LogWarning("Carrier Gateway answered {StatusCode} without a contract error for {TargetUrl}; correlation {CorrelationId}.",
                        (int)response.StatusCode, targetUrl, trace);
                    return ContractFailure(StatusCodes.Status503ServiceUnavailable);
                }
                _logger.LogInformation("Carrier Gateway answered {StatusCode} for {Method} {TargetUrl}; correlation {CorrelationId}.",
                    (int)response.StatusCode, method.Method, targetUrl, trace);
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
            _logger.LogWarning(exception, "Carrier Gateway request timed out for {TargetUrl}; correlation {CorrelationId}.", targetUrl, trace);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Carrier Gateway request failed for {TargetUrl}; correlation {CorrelationId}.", targetUrl, trace);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected Carrier proxy failure for {TargetUrl}; correlation {CorrelationId}.", targetUrl, trace);
            return ContractFailure(StatusCodes.Status500InternalServerError);
        }
    }

    private bool TryCreateGatewayRequest(
        HttpMethod method,
        string targetUrl,
        HttpContent? content,
        bool includeIdempotencyKey,
        out HttpRequestMessage request,
        out int localStatus)
    {
        request = new HttpRequestMessage(method, targetUrl) { Content = content };
        localStatus = StatusCodes.Status403Forbidden;

        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (string.IsNullOrWhiteSpace(token))
        {
            request.Dispose();
            request = null!;
            localStatus = StatusCodes.Status401Unauthorized;
            return false;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (!TryResolveScopeClaim(["tenant_id", "tenantId"], "/tenantId", out var tenantId)
            || !TryResolveScopeClaim(["legal_entity_id", "legalEntityId"], "/legalEntityId", out var legalEntityId))
        {
            request.Dispose();
            request = null!;
            return false;
        }

        request.Headers.TryAddWithoutValidation(TenantHeader, tenantId.ToString("D"));
        request.Headers.TryAddWithoutValidation(LegalEntityHeader, legalEntityId.ToString("D"));
        ForwardSingleHeader(request, CorrelationHeader);

        if (includeIdempotencyKey)
            ForwardSingleHeader(request, IdempotencyHeader);

        return true;
    }

    private bool TryResolveScopeClaim(string[] exactNames, string suffix, out Guid value)
    {
        value = Guid.Empty;
        var candidates = User.Claims
            .Where(claim => exactNames.Contains(claim.Type, StringComparer.OrdinalIgnoreCase)
                || claim.Type.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .Select(claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return candidates.Length == 1 && Guid.TryParse(candidates[0], out value);
    }

    private void ForwardSingleHeader(HttpRequestMessage request, string name)
    {
        if (!Request.Headers.TryGetValue(name, out var values))
            return;

        foreach (var value in values)
            request.Headers.TryAddWithoutValidation(name, value);
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
            StatusCodes.Status503ServiceUnavailable => ("PERSISTENCE_UNAVAILABLE", "Persistence outcome is unavailable; retry the request using the same idempotency key when applicable."),
            StatusCodes.Status500InternalServerError => ("INTERNAL_ERROR", "An unexpected internal error occurred."),
            _ => ("INVALID_REQUEST", "Request schema validation failed.")
        };

        return StatusCode(statusCode, new
        {
            error = new { code, message, correlationId },
            contractVersion = "v1"
        });
    }

    private Guid ResolveTraceCorrelation()
    {
        if (Request.Headers.TryGetValue(CorrelationHeader, out var values)
            && values.Count == 1
            && Guid.TryParse(values[0], out var correlationId))
            return correlationId;

        return Guid.NewGuid();
    }

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
