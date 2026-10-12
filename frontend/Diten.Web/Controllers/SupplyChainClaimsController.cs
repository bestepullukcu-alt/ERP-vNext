using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.SupplyChain.Claims;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Diten.Web.Services.SupplyChain;

namespace Diten.Web.Controllers;

// MOD-0187 Claims — same-origin MVC adapter (pack §32.3/§32.4). Built by R-4c from the September draft v4, held against
// MODULE-RECIPE.md: every failure is re-enveloped with the trace (2.1: an un-enveloped 5xx becomes 503
// CLAIM_STORAGE_UNAVAILABLE, never a raw 502), and every Gateway answer is logged with its trace (2.3).
//
// Browser → this controller → Gateway (GatewayUrl) → SupplyChain. The browser never calls the Gateway or holds a token.
// Bound operations only: queryClaims, createClaim, transitionClaim, and Shipment-owned getShipment read-only.
// No by-ID Claim GET, no detail page, no edit/delete/bulk/import/export route.
//
// Header policy (pack §32.4):
//   Claims family   → Authorization (server-side) + X-Correlation-Id + Idempotency-Key (POST). NO X-Tenant-Id /
//                     X-Legal-Entity-Id and no scope query keys (ClaimContextMiddleware derives scope from the JWT).
//   Shipment lookup → the existing Shipment adapter policy (SupplyChainShipmentsController in the A12 overlay):
//                     Authorization + tenant/LE headers from the caller's claims + the browser trace.
//   X-Correlation-Id on Claims POSTs = the Shipment lifecycle root read server-side; the root is never sent to the
//   browser (see ProxyClaimsMutationAsync).
// Depends on Diten.Web.Security.JsonAdapterEndpointAttribute and the JSON challenge branch in frontend Program.cs (Q394).
[Authorize]
[Route("SupplyChain/Claims")]
public sealed class SupplyChainClaimsController : Controller
{
    private const string CorrelationHeader = "X-Correlation-Id";
    private const string TenantHeader = "X-Tenant-Id";
    private const string LegalEntityHeader = "X-Legal-Entity-Id";
    private const string IdempotencyHeader = "Idempotency-Key";
    private const string ClaimsPath = "/api/shipment-bundle/claims";
    private const string ShipmentsPath = "/api/shipment-bundle/shipments";

    // The 18 published Claims codes (annex D187-05, pack §32.8). The adapter never relays a code outside this set.
    private static readonly HashSet<string> PublishedCodes = new(StringComparer.Ordinal)
    {
        "INVALID_REQUEST", "UNAUTHENTICATED", "FORBIDDEN", "CLAIM_NOT_FOUND", "UNSUPPORTED_MEDIA_TYPE",
        "CLAIM_SHIPMENT_INELIGIBLE", "CLAIM_CARRIER_MISMATCH", "CLAIM_AMOUNT_INVALID", "CLAIM_APPROVAL_AMOUNT_INVALID",
        "CLAIM_APPROVED_AMOUNT_NOT_ALLOWED", "INVALID_CLAIM_TRANSITION", "CLAIM_CORRELATION_MISMATCH", "IDEMPOTENCY_KEY_REUSED",
        "CLAIM_REFERENCE_INVALID", "CLAIM_REFERENCE_INCOMPLETE", "CLAIM_REFERENCE_UNAVAILABLE", "CLAIM_STORAGE_UNAVAILABLE",
        "INTERNAL_ERROR"
    };

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<SupplyChainClaimsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public SupplyChainClaimsController(HttpClient httpClient, IConfiguration configuration,
        ILogger<SupplyChainClaimsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = (configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.")).TrimEnd('/');
        _logger = logger;
    }

    // ── Page ────────────────────────────────────────────────────────────────────────────────────────────────────
    // The single view route (manifest page CLAIMS, pack §33). UAS-001 gating happens in the view (Perms.Has).
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/SupplyChain/Claims/Index.cshtml");

    // ── List adapter: GET /SupplyChain/Claims/api?shipmentId=&status= → GET /api/shipment-bundle/claims ──────────
    [HttpGet("api")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> List([FromQuery] string? shipmentId, [FromQuery] string? status,
        CancellationToken cancellationToken = default)
    {
        if (!HasPermission(ClaimUiPermissions.Read))
        {
            return Task.FromResult(ContractFailure(StatusCodes.Status403Forbidden, "FORBIDDEN"));
        }

        // Only the two published query parameters, forwarded as given (the backend validates them). Absent → omitted.
        var query = new List<string>(2);
        if (shipmentId is not null) query.Add($"shipmentId={Uri.EscapeDataString(shipmentId)}");
        if (status is not null) query.Add($"status={Uri.EscapeDataString(status)}");
        var target = $"{_gatewayUrl}{ClaimsPath}{(query.Count == 0 ? string.Empty : "?" + string.Join('&', query))}";
        return ProxyClaimsReadAsync(target, cancellationToken);
    }

    // ── Shipment resolve adapter: GET /SupplyChain/Claims/api/shipments/{id} → getShipment (projection only) ──────
    [HttpGet("api/shipments/{shipmentId:guid}")]
    [JsonAdapterEndpoint]
    public async Task<IActionResult> ResolveShipment(Guid shipmentId, CancellationToken cancellationToken)
    {
        if (!HasPermission(ClaimUiPermissions.Create) || !HasPermission(ClaimUiPermissions.ShipmentRead))
        {
            return ContractFailure(StatusCodes.Status403Forbidden, "FORBIDDEN");
        }

        if (!TryGetToken(out var token)) return ContractFailure(StatusCodes.Status401Unauthorized, "UNAUTHENTICATED");
        if (!TryGetBrowserTrace(out var trace)) return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");

        var lookup = await LookupShipmentAsync(shipmentId, token, trace, cancellationToken);
        if (lookup.Failure is not null) return lookup.Failure;

        Response.Headers[CorrelationHeader] = trace.ToString("D");
        return new JsonResult(ClaimShipmentProjection.From(lookup.Detail), _jsonOptions);
    }

    // ── Create adapter: POST /SupplyChain/Claims/api → root via getShipment → POST /api/shipment-bundle/claims ─────
    [HttpPost("api")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        if (!HasPermission(ClaimUiPermissions.Create) || !HasPermission(ClaimUiPermissions.ShipmentRead))
        {
            return ContractFailure(StatusCodes.Status403Forbidden, "FORBIDDEN");
        }

        if (!Request.HasJsonContentType())
        {
            return ContractFailure(StatusCodes.Status415UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE");
        }

        var body = await ReadBodyAsync(cancellationToken);
        if (!ClaimRequestReader.TryReadCreateShipmentId(body, out var shipmentId))
        {
            return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        }

        return await ProxyClaimsMutationAsync(shipmentId, $"{_gatewayUrl}{ClaimsPath}", body, cancellationToken);
    }

    // ── Transition adapter: POST /SupplyChain/Claims/api/{claimId}/transition?shipmentId= ─────────────────────────
    // shipmentId is a lookup hint only (the row's shipmentId); the backend root check (409 CLAIM_CORRELATION_MISMATCH)
    // is authoritative. It travels in the query so the body text reaches the Gateway unchanged.
    [HttpPost("api/{claimId:guid}/transition")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Transition(Guid claimId, [FromQuery] Guid? shipmentId,
        CancellationToken cancellationToken)
    {
        if (!Request.HasJsonContentType())
        {
            return ContractFailure(StatusCodes.Status415UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE");
        }

        var body = await ReadBodyAsync(cancellationToken);
        if (!ClaimRequestReader.TryReadTransitionTarget(body, out var targetStatus))
        {
            return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        }

        // Exact target key (ClaimModels.cs:45 mirror) + G-SHIPREAD, checked before any Gateway call.
        if (!HasPermission(ClaimUiPermissions.ForTarget(targetStatus)) || !HasPermission(ClaimUiPermissions.ShipmentRead))
        {
            return ContractFailure(StatusCodes.Status403Forbidden, "FORBIDDEN");
        }

        if (shipmentId is null || shipmentId.Value == Guid.Empty)
        {
            return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        }

        return await ProxyClaimsMutationAsync(shipmentId.Value,
            $"{_gatewayUrl}{ClaimsPath}/{claimId:D}/transition", body, cancellationToken);
    }

    // ── Proxy helpers ───────────────────────────────────────────────────────────────────────────────────────────

    private async Task<IActionResult> ProxyClaimsReadAsync(string targetUrl, CancellationToken cancellationToken)
    {
        if (!TryGetToken(out var token)) return ContractFailure(StatusCodes.Status401Unauthorized, "UNAUTHENTICATED");
        if (!TryGetBrowserTrace(out var trace)) return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");

        // R-2: ClaimContextMiddleware now REQUIRES X-Legal-Entity-Id — before R-2 it was optional because it
        // defaulted to the legal_entity_id claim, and these two proxies deliberately sent no scope headers.
        // With the claim gone they must send it, or every Claims read and mutation answers 400.
        if (!LegalEntityScopeRequest.TryResolve(Request, out var legalEntityId))
            return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");

        using var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation(CorrelationHeader, trace.ToString("D"));
        request.Headers.TryAddWithoutValidation(LegalEntityHeader, legalEntityId.ToString("D"));
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            // A GET correlation is a request trace only, so the browser's own trace is the support reference.
            Response.Headers[CorrelationHeader] = trace.ToString("D");
            _logger.LogInformation("Claims gateway answered {StatusCode} for GET {TargetUrl}. Trace {Trace}.",
                (int)response.StatusCode, targetUrl, trace);
            return (int)response.StatusCode >= 400
                ? await ReenvelopeFailureAsync(response, trace, cancellationToken)
                : await PassThroughAsync(response, cancellationToken);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Claims list request timed out. Trace {Trace}.", trace);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable, "CLAIM_STORAGE_UNAVAILABLE");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Claims list request failed. Trace {Trace}.", trace);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable, "CLAIM_STORAGE_UNAVAILABLE");
        }
    }

    /// <summary>
    /// Resolve the Shipment root, then forward the browser body text unchanged with the browser's Idempotency-Key and
    /// X-Correlation-Id = root. The Claims response body is passed through (ClaimResponse carries no correlation); the
    /// response correlation header and any error envelope correlationId, which would equal the root, are replaced with
    /// the browser trace so the root never reaches the browser (README FINDING F8). The adapter log line binds
    /// trace ↔ root for support.
    /// </summary>
    private async Task<IActionResult> ProxyClaimsMutationAsync(Guid shipmentId, string targetUrl, string body,
        CancellationToken cancellationToken)
    {
        if (!TryGetToken(out var token)) return ContractFailure(StatusCodes.Status401Unauthorized, "UNAUTHENTICATED");
        if (!TryGetBrowserTrace(out var trace)) return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        if (!TryGetIdempotencyKey(out var idempotencyKey))
        {
            return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        }
        // R-2: resolved before the Shipment lookup, so a request with no legal entity is refused without
        // spending a gateway round trip on it.
        if (!LegalEntityScopeRequest.TryResolve(Request, out var mutationLegalEntityId))
        {
            return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        }

        var lookup = await LookupShipmentAsync(shipmentId, token, trace, cancellationToken);
        if (lookup.Failure is not null) return lookup.Failure;

        var root = ClaimRootResolution.From(lookup.Detail);
        switch (root.State)
        {
            case ClaimRootState.Incomplete:
                return ContractFailure(StatusCodes.Status503ServiceUnavailable, "CLAIM_REFERENCE_INCOMPLETE");
            case ClaimRootState.Invalid:
                return ContractFailure(StatusCodes.Status502BadGateway, "CLAIM_REFERENCE_INVALID");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, targetUrl)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation(CorrelationHeader, root.Root.ToString("D"));
        request.Headers.TryAddWithoutValidation(IdempotencyHeader, idempotencyKey);
        request.Headers.TryAddWithoutValidation(LegalEntityHeader, mutationLegalEntityId.ToString("D"));
        _logger.LogInformation("Claims mutation forwarded. Trace {Trace} Root {Root} Target {TargetUrl}.",
            trace, root.Root, targetUrl);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            Response.Headers[CorrelationHeader] = trace.ToString("D");
            _logger.LogInformation("Claims gateway answered {StatusCode} for POST {TargetUrl}. Trace {Trace} Root {Root}.",
                (int)response.StatusCode, targetUrl, trace, root.Root);
            if ((int)response.StatusCode >= 400)
            {
                return await ReenvelopeFailureAsync(response, trace, cancellationToken);
            }

            return await PassThroughAsync(response, cancellationToken);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Outcome unknown: never reported as rolled back; the browser retries with the same key and body.
            _logger.LogWarning(exception, "Claims mutation timed out. Trace {Trace}.", trace);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable, "CLAIM_STORAGE_UNAVAILABLE");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Claims mutation failed. Trace {Trace}.", trace);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable, "CLAIM_STORAGE_UNAVAILABLE");
        }
    }

    private sealed record ShipmentLookup(JsonElement Detail, IActionResult? Failure);

    /// <summary>getShipment through the Gateway with the Shipment adapter's header policy. Shipment data other than
    /// the caller's projection or root never leaves this method; a Shipment 404 becomes the Claims safe-not-found.</summary>
    private async Task<ShipmentLookup> LookupShipmentAsync(Guid shipmentId, string token, Guid trace,
        CancellationToken cancellationToken)
    {
        if (!TryResolveScopeClaim(["tenant_id", "tenantId"], "/tenantId", out var tenantId))
        {
            return new(default, ContractFailure(StatusCodes.Status403Forbidden, "FORBIDDEN"));
        }
        // R-2 (SHIPMENT-BUNDLE 3.2.0): LegalEntityId is the user's choice on the page, arriving as
        // ?legalEntityId=. Missing or malformed is a REQUEST fault (400), not an authorization answer (403) —
        // the service's own middleware answers a missing X-Legal-Entity-Id the same way.
        if (!LegalEntityScopeRequest.TryResolve(Request, out var legalEntityId))
        {
            return new(default, ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST"));
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_gatewayUrl}{ShipmentsPath}/{shipmentId:D}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation(TenantHeader, tenantId.ToString("D"));
        request.Headers.TryAddWithoutValidation(LegalEntityHeader, legalEntityId.ToString("D"));
        request.Headers.TryAddWithoutValidation(CorrelationHeader, trace.ToString("D"));

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var status = (int)response.StatusCode;
            _logger.LogInformation("Shipment lookup for a claim answered {StatusCode}. Trace {Trace}.", status, trace);
            if (status == StatusCodes.Status200OK)
            {
                var text = await response.Content.ReadAsStringAsync(cancellationToken);
                try
                {
                    using var document = JsonDocument.Parse(text);
                    return new(document.RootElement.Clone(), null);
                }
                catch (JsonException)
                {
                    return new(default, ContractFailure(StatusCodes.Status502BadGateway, "CLAIM_REFERENCE_INVALID"));
                }
            }

            return new(default, status switch
            {
                StatusCodes.Status401Unauthorized => ContractFailure(status, "UNAUTHENTICATED"),
                StatusCodes.Status403Forbidden => ContractFailure(status, "FORBIDDEN"),
                StatusCodes.Status404NotFound => ContractFailure(status, "CLAIM_NOT_FOUND"),
                >= 500 => ContractFailure(StatusCodes.Status503ServiceUnavailable, "CLAIM_REFERENCE_UNAVAILABLE"),
                _ => ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST")
            });
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Shipment lookup for a claim timed out. Trace {Trace}.", trace);
            return new(default, ContractFailure(StatusCodes.Status503ServiceUnavailable, "CLAIM_REFERENCE_UNAVAILABLE"));
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Shipment lookup for a claim failed. Trace {Trace}.", trace);
            return new(default, ContractFailure(StatusCodes.Status503ServiceUnavailable, "CLAIM_REFERENCE_UNAVAILABLE"));
        }
    }

    private static async Task<IActionResult> PassThroughAsync(HttpResponseMessage response,
        CancellationToken cancellationToken) => new ContentResult
    {
        StatusCode = (int)response.StatusCode,
        ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
        Content = await response.Content.ReadAsStringAsync(cancellationToken)
    };

    /// <summary>
    /// Keeps a published code and its status; replaces only the correlationId (the root on a mutation) with the trace.
    /// A failure without a published code is the Gateway's or the transport's, not the service's: MODULE-RECIPE 2.1 — a
    /// 5xx without the contract envelope (service down: the Gateway answers 502 with an empty body) becomes 503
    /// CLAIM_STORAGE_UNAVAILABLE, so the page keeps the outcome open and retries with the same key. The draft relayed the
    /// list's 502 as it came and turned a mutation's into 502 INTERNAL_ERROR.
    /// </summary>
    private async Task<IActionResult> ReenvelopeFailureAsync(HttpResponseMessage response, Guid trace,
        CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        var (code, message) = ErrorOf(await response.Content.ReadAsStringAsync(cancellationToken));
        if (code is null || !PublishedCodes.Contains(code))
        {
            message = null;
            (status, code) = status switch
            {
                StatusCodes.Status401Unauthorized => (status, "UNAUTHENTICATED"),
                StatusCodes.Status403Forbidden => (status, "FORBIDDEN"),
                StatusCodes.Status404NotFound => (status, "CLAIM_NOT_FOUND"),
                StatusCodes.Status415UnsupportedMediaType => (status, "UNSUPPORTED_MEDIA_TYPE"),
                >= 500 => (StatusCodes.Status503ServiceUnavailable, "CLAIM_STORAGE_UNAVAILABLE"),
                _ => (StatusCodes.Status400BadRequest, "INVALID_REQUEST")
            };
        }

        return StatusCode(status,
            new { error = new { code, message = message ?? DefaultMessage(code), correlationId = trace }, contractVersion = "v1" });
    }

    private static (string? Code, string? Message) ErrorOf(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
                return (null, null);
            var code = error.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            var message = error.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
            return (code, message);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private bool TryGetToken(out string token)
    {
        token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(token);
    }

    private bool TryGetBrowserTrace(out Guid trace)
    {
        trace = Guid.Empty;
        return Request.Headers.TryGetValue(CorrelationHeader, out var values)
               && values.Count == 1
               && Guid.TryParse(values[0], out trace);
    }

    /// <summary>Exactly one non-empty key, forwarded byte-exact (the backend enforces 1–128 runes).</summary>
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

    private bool TryResolveScopeClaim(string[] exactNames, string suffix, out Guid value)
    {
        value = Guid.Empty;
        var candidates = User.Claims.Where(c => exactNames.Contains(c.Type, StringComparer.OrdinalIgnoreCase)
                                                || c.Type.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Value).Distinct(StringComparer.Ordinal).ToArray();
        return candidates.Length == 1 && Guid.TryParse(candidates[0], out value);
    }

    private IActionResult ContractFailure(int statusCode, string code)
    {
        var correlationId = TryGetBrowserTrace(out var trace) ? trace : Guid.NewGuid();
        Response.Headers[CorrelationHeader] = correlationId.ToString("D");
        return StatusCode(statusCode,
            new { error = new { code, message = DefaultMessage(code), correlationId }, contractVersion = "v1" });
    }

    // Wire messages are contract text for logs and API clients; the page shows localized text chosen by `code`.
    private static string DefaultMessage(string code) => code switch
    {
        "UNAUTHENTICATED" => "Authentication required.",
        "FORBIDDEN" => "Required permission is missing.",
        "UNSUPPORTED_MEDIA_TYPE" => "Content type must be application/json.",
        "CLAIM_NOT_FOUND" => "The requested record was not found.",
        "CLAIM_REFERENCE_INCOMPLETE" => "Shipment reference is incomplete.",
        "CLAIM_REFERENCE_INVALID" => "Shipment reference is invalid.",
        "CLAIM_REFERENCE_UNAVAILABLE" => "Shipment reference is temporarily unavailable.",
        "CLAIM_STORAGE_UNAVAILABLE" => "Outcome unavailable; retry with the same idempotency key.",
        "INTERNAL_ERROR" => "An unexpected internal error occurred.",
        _ => "Request validation failed."
    };

    private bool HasPermission(string permission) => PermissionClaims.HasPermission(User, permission);
}
