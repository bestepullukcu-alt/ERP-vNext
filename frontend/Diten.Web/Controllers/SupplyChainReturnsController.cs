using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.SupplyChain.Returns;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// MOD-0186 Reverse Logistics — Returns same-origin MVC adapter (pack §32.3/§32.4/§32.8). Built from the Q160 draft
// overlay v2 by R-2 (2026-10-04) against MODULE-RECIPE.md.
//
// Browser → this controller → Gateway (GatewayUrl) → SupplyChain 5061. The browser never calls the Gateway or holds a token.
// Bound operations only: queryReturns, createReturn, transitionReturn, and Shipment-owned getShipment read-only.
// No by-ID Return GET, no detail page, no edit/delete/bulk/import/export route.
//
// Header policy (pack §32.4; backend ReturnContextMiddleware.cs:48-100 in the accepted source normal-source.tar.gz
// edb759a0…5a21):
//   Returns family  → Authorization (server-side) + X-Tenant-Id / X-Legal-Entity-Id REQUIRED and filled only from the
//                     caller's signed session claims (middleware lines 64, 67-68, 76: missing → 400, mismatch → 404) +
//                     X-Correlation-Id (GET: the browser trace; POST: the Shipment lifecycle root) + Idempotency-Key
//                     (POST, browser per-intent key, forwarded byte-exact). No scope query key is ever sent (line 78).
//   Shipment lookup → the existing Shipment adapter policy: Authorization + tenant/LE headers from the caller's claims +
//                     the browser trace.
//   The lifecycle root never reaches the browser: the Returns backend echoes X-Correlation-Id (= root on POST) in the
//   response header and in error.correlationId (middleware lines 52-54, 60); both are replaced with the browser trace
//   (README FINDING F8, CT verdict Q64a F8 precedent). The adapter log line binds trace ↔ root.
// Adapter-originated failures use published Returns codes only (shipment-bundle.openapi.yaml /returns operations;
// annex "Auth401/context403/schema400/media415 use INVALID_REQUEST").
// Depends on Diten.Web.Security.JsonAdapterEndpointAttribute and the JSON challenge branch in frontend Program.cs
// (restored by Q394), and on Q394's 15 s read / 30 s write deadline for /api/shipment-bundle/, which covers this path.
[Authorize]
[Route("SupplyChain/Returns")]
public sealed class SupplyChainReturnsController : Controller
{
    private const string CorrelationHeader = "X-Correlation-Id";
    private const string TenantHeader = "X-Tenant-Id";
    private const string LegalEntityHeader = "X-Legal-Entity-Id";
    private const string IdempotencyHeader = "Idempotency-Key";
    private const string ReturnsPath = "/api/shipment-bundle/returns";
    private const string ShipmentsPath = "/api/shipment-bundle/shipments";

    // The 21 published Returns codes (shipment-bundle.openapi.yaml, operations queryReturns/createReturn/
    // transitionReturn). The adapter never originates or relays a code outside this set.
    private static readonly HashSet<string> PublishedCodes = new(StringComparer.Ordinal)
    {
        "INVALID_REQUEST", "RETURN_NOT_FOUND", "SHIPMENT_NOT_FOUND", "SHIPMENT_LINE_NOT_FOUND",
        "INVALID_RETURN_TRANSITION", "DISPOSITION_REQUIRED", "INVALID_RETURN_QUANTITY", "RETURN_QUANTITY_EXCEEDED",
        "RETURN_UOM_MISMATCH", "DUPLICATE_RETURN_LINE", "SHIPMENT_NOT_RETURNABLE", "CORRELATION_ROOT_MISMATCH",
        "IDEMPOTENCY_KEY_REUSED", "RETURN_SOURCE_CHANGED", "RETURN_SHIPMENT_ROOT_INVALID", "DEPENDENCY_RESPONSE_INVALID",
        "RETURN_SHIPMENT_ROOT_UNAVAILABLE", "REFERENCE_STATE_UNAVAILABLE", "DEPENDENCY_UNAVAILABLE",
        "PERSISTENCE_UNAVAILABLE", "INTERNAL_ERROR"
    };

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<SupplyChainReturnsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public SupplyChainReturnsController(HttpClient httpClient, IConfiguration configuration,
        ILogger<SupplyChainReturnsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = (configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.")).TrimEnd('/');
        _logger = logger;
    }

    private sealed record Scope(string Token, Guid Trace, Guid TenantId, Guid LegalEntityId);

    // ── Page ────────────────────────────────────────────────────────────────────────────────────────────────────
    // The single view route (manifest page RETURNS, pack §33). UAS-001 gating happens in the view (Perms.Has).
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/SupplyChain/Returns/Index.cshtml");

    // ── List adapter: GET /SupplyChain/Returns/api?shipmentId=&status= → GET /api/shipment-bundle/returns ─────────
    [HttpGet("api")]
    [JsonAdapterEndpoint]
    public async Task<IActionResult> List([FromQuery] string? shipmentId, [FromQuery] string? status,
        CancellationToken cancellationToken = default)
    {
        if (!HasPermission(ReturnUiPermissions.Read)) return ContractFailure(StatusCodes.Status403Forbidden, "INVALID_REQUEST");
        if (!TryGetScope(out var scope, out var failure)) return failure!;

        // Only the two published query parameters, forwarded as given (the backend validates them). Absent → omitted.
        var query = new List<string>(2);
        if (shipmentId is not null) query.Add($"shipmentId={Uri.EscapeDataString(shipmentId)}");
        if (status is not null) query.Add($"status={Uri.EscapeDataString(status)}");
        var target = $"{_gatewayUrl}{ReturnsPath}{(query.Count == 0 ? string.Empty : "?" + string.Join('&', query))}";

        using var request = new HttpRequestMessage(HttpMethod.Get, target);
        AddReturnsHeaders(request, scope, scope.Trace);
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            Response.Headers[CorrelationHeader] = scope.Trace.ToString("D");
            // R-2 (MODULE-RECIPE 2.3): every gateway answer is logged with the id the Gateway and the service log too.
            _logger.LogInformation("Returns gateway answered {StatusCode} for GET {TargetUrl}. Trace {Trace}.",
                (int)response.StatusCode, target, scope.Trace);
            return (int)response.StatusCode >= 400
                ? await ReenvelopeFailureAsync(response, scope.Trace, cancellationToken)
                : await PassThroughAsync(response, cancellationToken);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Returns list request timed out. Trace {Trace}.", scope.Trace);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable, "PERSISTENCE_UNAVAILABLE");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Returns list request failed. Trace {Trace}.", scope.Trace);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable, "PERSISTENCE_UNAVAILABLE");
        }
    }

    // ── Shipment resolve adapter: GET /SupplyChain/Returns/api/shipments/{id} → getShipment (projection only) ─────
    [HttpGet("api/shipments/{shipmentId:guid}")]
    [JsonAdapterEndpoint]
    public async Task<IActionResult> ResolveShipment(Guid shipmentId, CancellationToken cancellationToken)
    {
        if (!HasPermission(ReturnUiPermissions.Create) || !HasPermission(ReturnUiPermissions.ShipmentRead))
        {
            return ContractFailure(StatusCodes.Status403Forbidden, "INVALID_REQUEST");
        }

        if (!TryGetScope(out var scope, out var failure)) return failure!;
        var lookup = await LookupShipmentAsync(shipmentId, scope, "SHIPMENT_NOT_FOUND", cancellationToken);
        if (lookup.Failure is not null) return lookup.Failure;

        Response.Headers[CorrelationHeader] = scope.Trace.ToString("D");
        return new JsonResult(ReturnShipmentProjection.From(lookup.Detail), _jsonOptions);
    }

    // ── Create adapter: POST /SupplyChain/Returns/api → root via getShipment → POST /api/shipment-bundle/returns ──
    [HttpPost("api")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        if (!HasPermission(ReturnUiPermissions.Create) || !HasPermission(ReturnUiPermissions.ShipmentRead))
        {
            return ContractFailure(StatusCodes.Status403Forbidden, "INVALID_REQUEST");
        }

        if (!Request.HasJsonContentType()) return ContractFailure(StatusCodes.Status415UnsupportedMediaType, "INVALID_REQUEST");

        var body = await ReadBodyAsync(cancellationToken);
        if (!ReturnRequestReader.TryReadCreateShipmentId(body, out var shipmentId))
        {
            return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        }

        return await ProxyReturnsMutationAsync(shipmentId, $"{_gatewayUrl}{ReturnsPath}", body, "SHIPMENT_NOT_FOUND",
            cancellationToken);
    }

    // ── Transition adapter: POST /SupplyChain/Returns/api/{returnId}/transition?shipmentId= ───────────────────────
    // shipmentId is a lookup hint only (the row's shipmentId); the backend root check (409 CORRELATION_ROOT_MISMATCH) is
    // authoritative. It travels in the query so the body text reaches the Gateway unchanged (README A2).
    [HttpPost("api/{returnId:guid}/transition")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transition(Guid returnId, [FromQuery] Guid? shipmentId,
        CancellationToken cancellationToken)
    {
        if (!HasPermission(ReturnUiPermissions.Transition) || !HasPermission(ReturnUiPermissions.ShipmentRead))
        {
            return ContractFailure(StatusCodes.Status403Forbidden, "INVALID_REQUEST");
        }

        if (!Request.HasJsonContentType()) return ContractFailure(StatusCodes.Status415UnsupportedMediaType, "INVALID_REQUEST");

        var body = await ReadBodyAsync(cancellationToken);
        if (!ReturnRequestReader.TryReadTransitionTarget(body, out var targetStatus))
        {
            return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        }

        // Exact target key (ReturnPermissions.ForTarget mirror), checked before any Gateway call. A target without a
        // key (Requested, unknown text) is left to the backend (422 / 400), as ReturnContextMiddleware.cs:92-95 does.
        var targetKey = ReturnUiPermissions.ForTarget(targetStatus);
        if (targetKey is not null && !HasPermission(targetKey))
        {
            return ContractFailure(StatusCodes.Status403Forbidden, "INVALID_REQUEST");
        }

        if (shipmentId is null || shipmentId.Value == Guid.Empty)
        {
            return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        }

        // A Shipment 404 on the hint makes the Return unreachable: one safe-not-found (RETURN_NOT_FOUND).
        return await ProxyReturnsMutationAsync(shipmentId.Value,
            $"{_gatewayUrl}{ReturnsPath}/{returnId:D}/transition", body, "RETURN_NOT_FOUND", cancellationToken);
    }

    // ── Proxy helpers ───────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resolve the Shipment root, then forward the browser body text unchanged with the browser's Idempotency-Key,
    /// X-Correlation-Id = root and the caller's own tenant/LE claims. Success bodies (ReturnResponse, no correlation) pass
    /// through; the response correlation header and any error envelope correlationId, which would equal the root, are
    /// replaced with the browser trace so the root never reaches the browser (README FINDING F8).
    /// </summary>
    private async Task<IActionResult> ProxyReturnsMutationAsync(Guid shipmentId, string targetUrl, string body,
        string shipmentNotFoundCode, CancellationToken cancellationToken)
    {
        if (!TryGetScope(out var scope, out var failure)) return failure!;
        if (!TryGetIdempotencyKey(out var idempotencyKey)) return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");

        var lookup = await LookupShipmentAsync(shipmentId, scope, shipmentNotFoundCode, cancellationToken);
        if (lookup.Failure is not null) return lookup.Failure;

        var root = ReturnRootResolution.From(lookup.Detail);
        switch (root.State)
        {
            case ReturnRootState.Unavailable:
                return ContractFailure(StatusCodes.Status503ServiceUnavailable, "RETURN_SHIPMENT_ROOT_UNAVAILABLE");
            case ReturnRootState.Invalid:
                return ContractFailure(StatusCodes.Status502BadGateway, "RETURN_SHIPMENT_ROOT_INVALID");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, targetUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        AddReturnsHeaders(request, scope, root.Root);
        request.Headers.TryAddWithoutValidation(IdempotencyHeader, idempotencyKey);
        _logger.LogInformation("Returns mutation forwarded. Trace {Trace} Root {Root} Target {TargetUrl}.",
            scope.Trace, root.Root, targetUrl);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            Response.Headers[CorrelationHeader] = scope.Trace.ToString("D");
            _logger.LogInformation("Returns gateway answered {StatusCode} for POST {TargetUrl}. Trace {Trace} Root {Root}.",
                (int)response.StatusCode, targetUrl, scope.Trace, root.Root);
            return (int)response.StatusCode >= 400
                ? await ReenvelopeFailureAsync(response, scope.Trace, cancellationToken)
                : await PassThroughAsync(response, cancellationToken);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Outcome unknown: never reported as rolled back; the browser retries with the same key and body.
            _logger.LogWarning(exception, "Returns mutation timed out. Trace {Trace}.", scope.Trace);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable, "PERSISTENCE_UNAVAILABLE");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Returns mutation failed. Trace {Trace}.", scope.Trace);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable, "PERSISTENCE_UNAVAILABLE");
        }
    }

    private sealed record ShipmentLookup(JsonElement Detail, IActionResult? Failure);

    /// <summary>getShipment through the Gateway with the Shipment adapter's header policy. The mapping mirrors the
    /// backend reader (ReturnReferenceReader.cs:80-90): 404 → safe-not-found, 5xx/timeout → 503
    /// DEPENDENCY_UNAVAILABLE, Shipment 500 SHIPMENT_ROOT_INVALID → 502 RETURN_SHIPMENT_ROOT_INVALID, unreadable →
    /// 502 DEPENDENCY_RESPONSE_INVALID. The caller's own 401/403 on the Shipment keep their status (INVALID_REQUEST).</summary>
    private async Task<ShipmentLookup> LookupShipmentAsync(Guid shipmentId, Scope scope, string notFoundCode,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_gatewayUrl}{ShipmentsPath}/{shipmentId:D}");
        AddReturnsHeaders(request, scope, scope.Trace);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var status = (int)response.StatusCode;
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("Shipment lookup for a return answered {StatusCode}. Trace {Trace}.", status, scope.Trace);
            if (status == StatusCodes.Status200OK)
            {
                try
                {
                    using var document = JsonDocument.Parse(text);
                    if (document.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        return new(document.RootElement.Clone(), null);
                    }
                }
                catch (JsonException)
                {
                    // Unreadable Shipment detail: mapped below.
                }

                return new(default, ContractFailure(StatusCodes.Status502BadGateway, "DEPENDENCY_RESPONSE_INVALID"));
            }

            if (status == StatusCodes.Status500InternalServerError && ErrorCodeOf(text) == "SHIPMENT_ROOT_INVALID")
            {
                return new(default, ContractFailure(StatusCodes.Status502BadGateway, "RETURN_SHIPMENT_ROOT_INVALID"));
            }

            return new(default, status switch
            {
                StatusCodes.Status401Unauthorized => ContractFailure(status, "INVALID_REQUEST"),
                StatusCodes.Status403Forbidden => ContractFailure(status, "INVALID_REQUEST"),
                StatusCodes.Status404NotFound => ContractFailure(status, notFoundCode),
                >= 500 => ContractFailure(StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE"),
                _ => ContractFailure(StatusCodes.Status502BadGateway, "DEPENDENCY_RESPONSE_INVALID")
            });
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Shipment lookup for a return timed out. Trace {Trace}.", scope.Trace);
            return new(default, ContractFailure(StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE"));
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Shipment lookup for a return failed. Trace {Trace}.", scope.Trace);
            return new(default, ContractFailure(StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE"));
        }
    }

    private void AddReturnsHeaders(HttpRequestMessage request, Scope scope, Guid correlation)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", scope.Token);
        request.Headers.TryAddWithoutValidation(TenantHeader, scope.TenantId.ToString("D"));
        request.Headers.TryAddWithoutValidation(LegalEntityHeader, scope.LegalEntityId.ToString("D"));
        request.Headers.TryAddWithoutValidation(CorrelationHeader, correlation.ToString("D"));
    }

    private static async Task<IActionResult> PassThroughAsync(HttpResponseMessage response,
        CancellationToken cancellationToken) => new ContentResult
    {
        StatusCode = (int)response.StatusCode,
        ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
        Content = await response.Content.ReadAsStringAsync(cancellationToken)
    };

    /// <summary>
    /// Keeps the backend's status and published code; the correlationId (the root on POST) becomes the browser trace.
    /// A response without a published code (for example a Gateway error page) is mapped by status to a published code;
    /// 5xx becomes 503 PERSISTENCE_UNAVAILABLE so an unknown commit is never shown as rolled back. Upstream text is
    /// never relayed raw.
    /// </summary>
    private async Task<IActionResult> ReenvelopeFailureAsync(HttpResponseMessage response, Guid trace,
        CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        var code = ErrorCodeOf(await response.Content.ReadAsStringAsync(cancellationToken));
        if (code is null || !PublishedCodes.Contains(code))
        {
            (status, code) = status switch
            {
                StatusCodes.Status400BadRequest or StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden
                    or StatusCodes.Status415UnsupportedMediaType => (status, "INVALID_REQUEST"),
                StatusCodes.Status404NotFound => (status, "RETURN_NOT_FOUND"),
                >= 500 => (StatusCodes.Status503ServiceUnavailable, "PERSISTENCE_UNAVAILABLE"),
                _ => (StatusCodes.Status400BadRequest, "INVALID_REQUEST")
            };
        }

        return StatusCode(status,
            new { error = new { code, message = DefaultMessage(code), correlationId = trace }, contractVersion = "v1" });
    }

    private static string? ErrorCodeOf(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && document.RootElement.TryGetProperty("error", out var error)
                   && error.ValueKind == JsonValueKind.Object
                   && error.TryGetProperty("code", out var code)
                   && code.ValueKind == JsonValueKind.String
                ? code.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Token (401), browser trace (400) and the caller's own scope claims (403), all before any Gateway call.
    /// Every failure uses INVALID_REQUEST with the matching status (annex D186-05).</summary>
    private bool TryGetScope(out Scope scope, out IActionResult? failure)
    {
        scope = new Scope(string.Empty, Guid.Empty, Guid.Empty, Guid.Empty);
        failure = null;
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            failure = ContractFailure(StatusCodes.Status401Unauthorized, "INVALID_REQUEST");
            return false;
        }

        if (!TryGetBrowserTrace(out var trace))
        {
            failure = ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");
            return false;
        }

        if (!TryResolveScopeClaim(["tenant_id", "tenantId"], "/tenantId", out var tenantId)
            || !TryResolveScopeClaim(["legal_entity_id", "legalEntityId"], "/legalEntityId", out var legalEntityId))
        {
            failure = ContractFailure(StatusCodes.Status403Forbidden, "INVALID_REQUEST");
            return false;
        }

        scope = new Scope(token, trace, tenantId, legalEntityId);
        return true;
    }

    /// <summary>Exactly one X-Correlation-Id in the 8-4-4-4-12 form (ReturnModels.cs:10 ReturnWire.Uuid).</summary>
    private bool TryGetBrowserTrace(out Guid trace)
    {
        trace = Guid.Empty;
        return Request.Headers.TryGetValue(CorrelationHeader, out var values)
               && values.Count == 1
               && Guid.TryParseExact(values[0], "D", out trace);
    }

    /// <summary>Exactly one non-empty key, forwarded byte-exact (the backend enforces 1–128 scalars,
    /// ReturnContextMiddleware.cs:11-16, 71-73).</summary>
    private bool TryGetIdempotencyKey(out string key)
    {
        key = string.Empty;
        if (!Request.Headers.TryGetValue(IdempotencyHeader, out var values) || values.Count != 1
            || string.IsNullOrEmpty(values[0]))
        {
            return false;
        }

        key = values[0]!;
        return true;
    }

    private async Task<string> ReadBodyAsync(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false,
            leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    /// <summary>The caller's own signed scope claim, exactly one non-nil UUID (ReturnContextMiddleware.cs:27-32).</summary>
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

    // Wire messages follow the backend default (ReturnContractError.cs:4: the code with spaces) for logs and API
    // clients; the page shows localized text chosen by `code` and status, never this text.
    private static string DefaultMessage(string code) => code.Replace('_', ' ');

    private bool HasPermission(string permission) => PermissionClaims.HasPermission(User, permission);
}
