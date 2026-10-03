using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.SupplyChain.Shipments;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Authorize]
[Route("SupplyChain/Shipments")]
public sealed class SupplyChainShipmentsController : Controller
{
    internal const string ReadPermission = "supplychain.shipments.read";
    internal const string CreatePermission = "supplychain.shipments.create";
    internal const string DispatchPermission = "supplychain.shipments.dispatch";
    internal const string CancelPermission = "supplychain.shipments.cancel";
    internal const string PodPermission = "supplychain.shipments.pod.capture";

    private const string CorrelationHeader = "X-Correlation-Id";
    private const string TenantHeader = "X-Tenant-Id";
    private const string LegalEntityHeader = "X-Legal-Entity-Id";
    private const string IdempotencyHeader = "Idempotency-Key";
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<SupplyChainShipmentsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public SupplyChainShipmentsController(HttpClient httpClient, IConfiguration configuration,
        ILogger<SupplyChainShipmentsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = (configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.")).TrimEnd('/');
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/SupplyChain/Shipments/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult CreatePage() => View("~/Views/SupplyChain/Shipments/Create.cshtml");

    [HttpGet("Details/{shipmentId:guid}")]
    public IActionResult Details(Guid shipmentId)
    {
        ViewBag.ShipmentId = shipmentId;
        return View("~/Views/SupplyChain/Shipments/Details.cshtml");
    }

    [HttpGet("api")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? sourceDocumentId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        if (!HasPermission(ReadPermission)) return Task.FromResult(ContractFailure(StatusCodes.Status403Forbidden));
        if (page < 1 || pageSize is < 1 or > 200) return Task.FromResult(ContractFailure(StatusCodes.Status400BadRequest));

        var query = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (status is not null) query.Add($"status={Uri.EscapeDataString(status)}");
        if (sourceDocumentId is not null) query.Add($"sourceDocumentId={Uri.EscapeDataString(sourceDocumentId)}");
        var target = $"{_gatewayUrl}/api/shipment-bundle/shipments?{string.Join('&', query)}";
        return ProxyAsync(HttpMethod.Get, target, null, false, cancellationToken);
    }

    [HttpGet("api/{shipmentId:guid}")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> Detail(Guid shipmentId, CancellationToken cancellationToken)
    {
        if (!HasPermission(ReadPermission)) return Task.FromResult(ContractFailure(StatusCodes.Status403Forbidden));
        return ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/shipment-bundle/shipments/{shipmentId:D}", null, false, cancellationToken);
    }

    [HttpPost("api")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Create([FromBody] CreateShipmentViewModel model, CancellationToken cancellationToken)
    {
        if (!HasPermission(CreatePermission)) return Task.FromResult(ContractFailure(StatusCodes.Status403Forbidden));
        if (!ModelState.IsValid) return Task.FromResult(ContractFailure(StatusCodes.Status400BadRequest));
        return ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/shipment-bundle/shipments",
            JsonContent.Create(model, options: _jsonOptions), true, cancellationToken);
    }

    [HttpPost("api/{shipmentId:guid}/transition")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Transition(Guid shipmentId, [FromBody] TransitionShipmentViewModel model,
        CancellationToken cancellationToken)
    {
        var permission = string.Equals(model.TargetStatus, "Cancelled", StringComparison.Ordinal)
            ? CancelPermission : DispatchPermission;
        if (!HasPermission(permission)) return Task.FromResult(ContractFailure(StatusCodes.Status403Forbidden));
        if (!ModelState.IsValid) return Task.FromResult(ContractFailure(StatusCodes.Status400BadRequest));
        return ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/shipment-bundle/shipments/{shipmentId:D}/transition",
            JsonContent.Create(model, options: _jsonOptions), true, cancellationToken);
    }

    [HttpPost("api/{shipmentId:guid}/pod")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CapturePod(Guid shipmentId, [FromBody] CaptureShipmentPodViewModel model,
        CancellationToken cancellationToken)
    {
        if (!HasPermission(PodPermission)) return Task.FromResult(ContractFailure(StatusCodes.Status403Forbidden));
        if (!ModelState.IsValid) return Task.FromResult(ContractFailure(StatusCodes.Status400BadRequest));
        return ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/shipment-bundle/shipments/{shipmentId:D}/pod",
            JsonContent.Create(model, options: _jsonOptions), true, cancellationToken);
    }

    private async Task<IActionResult> ProxyAsync(HttpMethod method, string targetUrl, HttpContent? content,
        bool includeIdempotencyKey, CancellationToken cancellationToken)
    {
        if (!TryCreateGatewayRequest(method, targetUrl, content, includeIdempotencyKey, out var request, out var status))
            return ContractFailure(status);
        try
        {
            using (request)
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                CopyCorrelationHeader(response);
                return new ContentResult
                {
                    StatusCode = (int)response.StatusCode,
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                    Content = await response.Content.ReadAsStringAsync(cancellationToken)
                };
            }
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Shipment Gateway request timed out for {TargetUrl}.", targetUrl);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Shipment Gateway request failed for {TargetUrl}.", targetUrl);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected Shipment proxy failure for {TargetUrl}.", targetUrl);
            return ContractFailure(StatusCodes.Status500InternalServerError);
        }
    }

    private bool TryCreateGatewayRequest(HttpMethod method, string targetUrl, HttpContent? content,
        bool includeIdempotencyKey, out HttpRequestMessage request, out int localStatus)
    {
        request = new HttpRequestMessage(method, targetUrl) { Content = content };
        localStatus = StatusCodes.Status403Forbidden;
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (string.IsNullOrWhiteSpace(token)) return FailRequest(request, StatusCodes.Status401Unauthorized, out localStatus);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (!TryResolveScopeClaim(["tenant_id", "tenantId"], "/tenantId", out var tenantId)
            || !TryResolveScopeClaim(["legal_entity_id", "legalEntityId"], "/legalEntityId", out var legalEntityId))
            return FailRequest(request, StatusCodes.Status403Forbidden, out localStatus);
        request.Headers.TryAddWithoutValidation(TenantHeader, tenantId.ToString("D"));
        request.Headers.TryAddWithoutValidation(LegalEntityHeader, legalEntityId.ToString("D"));
        if (!TryForwardUuidHeader(request, CorrelationHeader))
            return FailRequest(request, StatusCodes.Status400BadRequest, out localStatus);
        if (includeIdempotencyKey && !TryForwardSingleRequiredHeader(request, IdempotencyHeader))
            return FailRequest(request, StatusCodes.Status400BadRequest, out localStatus);
        return true;
    }

    private static bool FailRequest(HttpRequestMessage request, int status, out int localStatus)
    {
        request.Dispose();
        request = null!;
        localStatus = status;
        return false;
    }

    private bool TryResolveScopeClaim(string[] exactNames, string suffix, out Guid value)
    {
        value = Guid.Empty;
        var candidates = User.Claims.Where(c => exactNames.Contains(c.Type, StringComparer.OrdinalIgnoreCase)
                || c.Type.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value).Distinct(StringComparer.Ordinal).ToArray();
        return candidates.Length == 1 && Guid.TryParse(candidates[0], out value);
    }

    private bool TryForwardUuidHeader(HttpRequestMessage request, string name)
    {
        if (!Request.Headers.TryGetValue(name, out var values) || values.Count != 1 || !Guid.TryParse(values[0], out var value))
            return false;
        request.Headers.TryAddWithoutValidation(name, value.ToString("D"));
        return true;
    }

    private bool TryForwardSingleRequiredHeader(HttpRequestMessage request, string name)
    {
        if (!Request.Headers.TryGetValue(name, out var values) || values.Count != 1 || string.IsNullOrWhiteSpace(values[0]))
            return false;
        request.Headers.TryAddWithoutValidation(name, values[0]);
        return true;
    }

    private void CopyCorrelationHeader(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues(CorrelationHeader, out var values)) Response.Headers[CorrelationHeader] = values.ToArray();
    }

    private IActionResult ContractFailure(int statusCode)
    {
        var correlationId = Request.Headers.TryGetValue(CorrelationHeader, out var values)
            && values.Count == 1 && Guid.TryParse(values[0], out var parsed) ? parsed : Guid.NewGuid();
        Response.Headers[CorrelationHeader] = correlationId.ToString("D");
        var (code, message) = statusCode switch
        {
            StatusCodes.Status401Unauthorized => ("INVALID_REQUEST", "Authentication required."),
            StatusCodes.Status403Forbidden => ("INVALID_REQUEST", "Required authorization context or permission is missing."),
            StatusCodes.Status503ServiceUnavailable => ("PERSISTENCE_UNAVAILABLE", "Persistence outcome is unavailable; retry with the same idempotency key when applicable."),
            StatusCodes.Status500InternalServerError => ("INTERNAL_ERROR", "An unexpected internal error occurred."),
            _ => ("INVALID_REQUEST", "Request schema validation failed.")
        };
        return StatusCode(statusCode, new { error = new { code, message, correlationId }, contractVersion = "v1" });
    }

    private bool HasPermission(string permission) => PermissionClaims.HasPermission(User, permission);
}
