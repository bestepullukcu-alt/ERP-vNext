using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Diten.Web.Models.SupplyChain.Returns;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Diten.Web.Services.SupplyChain;

namespace Diten.Web.Controllers;

// MOD-0186 Reverse Logistics — Returns same-origin MVC adapter (pack §32.3/§32.4/§32.8). Built from the Q160 draft
// overlay v2 by R-2 (2026-10-04) against MODULE-RECIPE.md.
//
// Browser → this controller → Gateway (GatewayUrl) → SupplyChain 5061. The browser never calls the Gateway or holds a token.
// Bound operations: queryReturns, createReturn, transitionReturn, getReturn (3.5.0, MOD-0186 r10 DL-4), and read-only
// getShipment / queryShipments, CRM account, LOCATION, PRODUCT-MASTER, TRACE and QUARANTINE reads (r10 §35.2–35.5).
// No edit/delete/bulk/import/export route.
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

    // The published Returns codes (shipment-bundle.openapi.yaml 3.4.0, operations queryReturns/createReturn/
    // transitionReturn). The adapter never originates a code outside this set; the only one it relays is a
    // code-shaped 422 refusal (Q504), so a new refusal reaches the page as a refusal, never as a validation text.
    private static readonly HashSet<string> PublishedCodes = new(StringComparer.Ordinal)
    {
        "INVALID_REQUEST", "RETURN_NOT_FOUND", "SHIPMENT_NOT_FOUND", "SHIPMENT_LINE_NOT_FOUND",
        "INVALID_RETURN_TRANSITION", "DISPOSITION_REQUIRED", "INVALID_RETURN_QUANTITY", "RETURN_QUANTITY_EXCEEDED",
        "RETURN_UOM_MISMATCH", "DUPLICATE_RETURN_LINE", "SHIPMENT_NOT_RETURNABLE", "CORRELATION_ROOT_MISMATCH",
        "IDEMPOTENCY_KEY_REUSED", "RETURN_SOURCE_CHANGED", "RETURN_SHIPMENT_ROOT_INVALID", "DEPENDENCY_RESPONSE_INVALID",
        "RETURN_SHIPMENT_ROOT_UNAVAILABLE", "REFERENCE_STATE_UNAVAILABLE", "DEPENDENCY_UNAVAILABLE",
        "PERSISTENCE_UNAVAILABLE", "INTERNAL_ERROR", "INVENTORY_REFUSED", "RETURN_SOURCE_MOVEMENT_MISSING",
        "RETURN_LINE_NOT_UNPOSTABLE", "RETURN_RECEIPT_IN_PROGRESS"
    };

    // MOD-0186 DL-2 (Q504): the only error details that reach the browser, per code, and only the fields a screen text
    // needs. details.inventory (lot, recall case) stays server-side; every other code carries no details.
    private static readonly Regex RefusalCodeShape = new("^[A-Z][A-Z0-9_]{0,63}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static readonly Dictionary<string, string[]> RelayedDetails = new(StringComparer.Ordinal)
    {
        ["INVENTORY_REFUSED"] = ["shipmentLineNumber", "inventoryCode"],
        ["RETURN_SOURCE_MOVEMENT_MISSING"] = ["shipmentLineNumber"],
        ["RETURN_LINE_NOT_UNPOSTABLE"] = ["shipmentLineNumber"],
        ["RETURN_RECEIPT_IN_PROGRESS"] = ["toWarehouseId", "toLocationId", "occurredAt"]
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

    // MOD-0186 r10 §35.5 (DL-4): the details page (manifest page RETURN_DETAILS, parent RETURNS, not navigation-visible).
    // The page's reads need the company scope, which arrives on the URL from the list; parsed with the adapter's rule.
    [HttpGet("Details/{returnId:guid}")]
    public IActionResult Details(Guid returnId)
    {
        ViewBag.ReturnId = returnId;
        ViewBag.LegalEntityId = LegalEntityScopeRequest.TryResolve(Request, out var legalEntityId) ? legalEntityId.ToString("D") : string.Empty;
        return View("~/Views/SupplyChain/Returns/Details.cshtml");
    }

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
            if ((int)response.StatusCode >= 400) return await ReenvelopeFailureAsync(response, scope.Trace, cancellationToken);
            // r10 §35.5 (RB-25): the customer column comes from the return's own customerRef only; customerRef never reaches
            // the browser. Without crm.account.read nothing is asked of CRM and the column is not drawn.
            var listBody = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken)) as JsonObject;
            if (listBody?["items"] is not JsonArray rows) return ContractFailure(StatusCodes.Status503ServiceUnavailable, "PERSISTENCE_UNAVAILABLE");
            var names = new ReturnNames(this, scope);
            await Task.WhenAll(rows.OfType<JsonObject>().Select(async row =>
            {
                var customerRef = Str(row, "customerRef");
                row.Remove("customerRef");
                if (HasPermission(CrmAccountRead)) row["customer"] = (await names.CustomerAsync(customerRef, cancellationToken)).DeepClone();
            }));
            return JsonOk(listBody.ToJsonString(), "application/json");
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
        // r10 §35.2 (RB-1, RB-2): product / SKU name and code (one bulk read) and lot number per line, read at display time;
        // a failed or denied read keeps the row and says "name unavailable" in the page.
        var names = new ReturnNames(this, scope);
        var skuIds = ReturnShipmentProjection.SkuIds(lookup.Detail);
        var skus = await names.SkusAsync(skuIds, cancellationToken);
        var lots = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var lotId in ReturnShipmentProjection.LotIds(lookup.Detail)) lots[lotId] = await names.LotAsync(lotId, cancellationToken);
        return new JsonResult(ReturnShipmentProjection.From(lookup.Detail, skus, lots), _jsonOptions);
    }

    // ── r10 §35.3 eligible-shipment picker: GET api/eligible-shipments?search=&page=&any= → queryShipments 3.5.0 ──────────
    // Create (any=false): Delivered and Closed only. List filter (any=true): every status. Rows carry the id (option value),
    // number, date and the customer name and code resolved from ShipmentSummary.customerRef (old record without one); no
    // per-row getShipment. Without crm.account.read the row has number and date only.
    [HttpGet("api/eligible-shipments")]
    [JsonAdapterEndpoint]
    public async Task<IActionResult> EligibleShipments([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] bool any = false,
        CancellationToken cancellationToken = default)
    {
        var allowed = any ? HasPermission(ReturnUiPermissions.Read) : HasPermission(ReturnUiPermissions.Create);
        if (!allowed || !HasPermission(ReturnUiPermissions.ShipmentRead)) return ContractFailure(StatusCodes.Status403Forbidden, "INVALID_REQUEST");
        if (page is < 1 or > 50 || (search?.Length ?? 0) > 100) return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        if (!TryGetScope(out var scope, out var failure)) return failure!;
        var filter = string.IsNullOrWhiteSpace(search) ? string.Empty : "&search=" + Uri.EscapeDataString(search.Trim());
        var statuses = any ? new string?[] { null } : ReturnUiLifecycle.EligibleShipmentStatuses.ToArray<string?>();
        var items = new List<JsonObject>(); var more = false;
        foreach (var status in statuses)
        {
            var query = $"page={page}&pageSize={PickerPageSize}&orderBy=plannedShipAt&orderDir=desc" + (status is null ? string.Empty : "&status=" + status) + filter;
            var reply = await GatewayGetAsync($"{ShipmentsPath}?{query}", scope, cancellationToken);
            if (reply.Status != StatusCodes.Status200OK) return PickerFailure(reply.Status);
            if (JsonNode.Parse(reply.Body) is not JsonObject body || body["items"] is not JsonArray rows) return ContractFailure(StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
            items.AddRange(rows.OfType<JsonObject>());
            more |= (long)page * PickerPageSize < (body["filteredTotal"]?.GetValue<long>() ?? body["total"]?.GetValue<long>() ?? 0);
        }
        var names = new ReturnNames(this, scope);
        var canCrm = HasPermission(CrmAccountRead);
        var answer = await Task.WhenAll(items.Select(async row => new JsonObject
        {
            ["id"] = Str(row, "shipmentId"), ["number"] = Str(row, "shipmentNumber"),
            ["date"] = Str(row, "plannedDeliverAt") ?? Str(row, "plannedShipAt"), ["status"] = Str(row, "status"),
            ["customer"] = canCrm ? (await names.CustomerAsync(Str(row, "customerRef"), cancellationToken)).DeepClone() : null
        }));
        return JsonOk(new JsonObject { ["items"] = new JsonArray(answer.Where(r => ReturnWireUuid(Str(r, "id"))).ToArray<JsonNode?>()), ["hasMore"] = more }.ToJsonString(), "application/json");
    }

    // ── r10 §35.3 receiving pickers: warehouse (Active), and the location tree one layer at a time, WITHOUT isStockable ───
    [HttpGet("api/warehouses")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> Warehouses([FromQuery] string? search, CancellationToken cancellationToken)
    {
        if (!HasPermission(ReturnUiPermissions.Receive)) return Task.FromResult(ContractFailure(StatusCodes.Status403Forbidden, "INVALID_REQUEST"));
        if ((search?.Length ?? 0) > 100) return Task.FromResult(ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST"));
        return LocationLayerAsync($"type=Warehouse&status=Active&page=1&pageSize={PickerPageSize}" + Search(search), cancellationToken);
    }

    [HttpGet("api/locations")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> Locations([FromQuery] string? parentId, [FromQuery] string? search, CancellationToken cancellationToken)
    {
        if (!HasPermission(ReturnUiPermissions.Receive)) return Task.FromResult(ContractFailure(StatusCodes.Status403Forbidden, "INVALID_REQUEST"));
        if (!Guid.TryParse(parentId, out var parent) || (search?.Length ?? 0) > 100) return Task.FromResult(ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST"));
        return LocationLayerAsync($"parentId={parent:D}&status=Active&page=1&pageSize={LocationLayerPageSize}" + Search(search), cancellationToken);
    }

    // ── r10 §35.3 (RB-7): RETURN_RECEIPT_IN_PROGRESS fills the pickers by name — the label of a stored place id ─────────────
    [HttpGet("api/places/{locationId}")]
    [JsonAdapterEndpoint]
    public async Task<IActionResult> Place(string locationId, CancellationToken cancellationToken)
    {
        if (!HasPermission(ReturnUiPermissions.Receive)) return ContractFailure(StatusCodes.Status403Forbidden, "INVALID_REQUEST");
        if (string.IsNullOrWhiteSpace(locationId) || locationId.Length > 100) return ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");
        if (!TryGetScope(out var scope, out var failure)) return failure!;
        return JsonOk((await new ReturnNames(this, scope).LocationAsync(locationId, cancellationToken)).ToJsonString(), "application/json");
    }

    // ── r10 §35.5 / §35.6 details adapter: GET api/{returnId} → getReturn 3.5.0, with names (RB-8, RB-9, RB-24, RB-25) ────
    // The summary shows the return's OWN customerRef only (single rule): without one it is "old record", and the shipment's
    // customerRef is never a fallback. Lines get product / SKU and lot names from the shipment the return names.
    [HttpGet("api/{returnId:guid}")]
    [JsonAdapterEndpoint]
    public async Task<IActionResult> Detail(Guid returnId, CancellationToken cancellationToken)
    {
        if (!HasPermission(ReturnUiPermissions.Read)) return ContractFailure(StatusCodes.Status403Forbidden, "INVALID_REQUEST");
        if (!TryGetScope(out var scope, out var failure)) return failure!;
        var read = await ReadReturnAsync(returnId, scope, cancellationToken);
        if (read.Failure is not null) return read.Failure;
        var detail = read.Detail!;
        var names = new ReturnNames(this, scope);
        var customerRef = Str(detail, "customerRef");
        detail.Remove("customerRef");
        detail["customer"] = HasPermission(CrmAccountRead) ? (await names.CustomerAsync(customerRef, cancellationToken)).DeepClone() : null;
        var shipment = Guid.TryParse(Str(detail, "shipmentId"), out var shipmentId) && HasPermission(ReturnUiPermissions.ShipmentRead)
            ? await LookupShipmentAsync(shipmentId, scope, "SHIPMENT_NOT_FOUND", cancellationToken) : null;
        var source = shipment?.Failure is null && shipment is not null ? shipment.Detail : default;
        detail["shipmentNumber"] = source.ValueKind == JsonValueKind.Object && source.TryGetProperty("shipmentNumber", out var number) && number.ValueKind == JsonValueKind.String ? number.GetString() : null;
        var sourceLines = ReturnShipmentProjection.LinesByNumber(source);
        var skus = await names.SkusAsync(sourceLines.Values.Select(l => l.SkuId).Where(x => x is not null).Select(x => x!).Distinct().ToList(), cancellationToken);
        foreach (var line in (detail["lines"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            sourceLines.TryGetValue(Str(line, "shipmentLineNumber") ?? string.Empty, out var origin);
            line["sku"] = origin?.SkuId is { } sku ? (skus.TryGetValue(sku, out var label) ? Resolved(label) : Unavailable(null)).DeepClone() : Unavailable(null).DeepClone();
            line["lot"] = string.IsNullOrWhiteSpace(origin?.LotId) ? null : (await names.LotAsync(origin!.LotId!, cancellationToken)).DeepClone();
            line.Remove("inventoryTransactionReferenceId");
        }
        Response.Headers[CorrelationHeader] = scope.Trace.ToString("D");
        return JsonOk(detail.ToJsonString(), "application/json");
    }

    // ── r10 §35.5 quarantine link: GET api/{returnId}/holds → MOD-0175 listHoldsBySource(MOD-0186, returnId) (RB-10) ───────
    // Gate: read + quarantine.holds.read, checked before any request. Holds are matched to return lines by the shipment line's
    // skuId and lotId (server-side; no id reaches a label); a hold that matches no line is listed apart.
    [HttpGet("api/{returnId:guid}/holds")]
    [JsonAdapterEndpoint]
    public async Task<IActionResult> Holds(Guid returnId, CancellationToken cancellationToken)
    {
        if (!HasPermission(ReturnUiPermissions.Read) || !HasPermission(QuarantineHoldsRead)) return ContractFailure(StatusCodes.Status403Forbidden, "INVALID_REQUEST");
        if (!TryGetScope(out var scope, out var failure)) return failure!;
        var read = await ReadReturnAsync(returnId, scope, cancellationToken);
        if (read.Failure is not null) return read.Failure;
        var holdsReply = await GatewayGetAsync($"/api/quarantine/holds/by-source?sourceModule=MOD-0186&sourceDocumentId={returnId:D}", scope, cancellationToken);
        if (holdsReply.Status != StatusCodes.Status200OK) return PickerFailure(holdsReply.Status);
        if (JsonNode.Parse(holdsReply.Body) is not JsonArray holds) return ContractFailure(StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        var source = Guid.TryParse(Str(read.Detail!, "shipmentId"), out var shipmentId) && HasPermission(ReturnUiPermissions.ShipmentRead)
            ? await LookupShipmentAsync(shipmentId, scope, "SHIPMENT_NOT_FOUND", cancellationToken) : null;
        var sourceLines = ReturnShipmentProjection.LinesByNumber(source?.Failure is null && source is not null ? source.Detail : default);
        var used = new HashSet<JsonObject>();
        var lines = new JsonArray();
        foreach (var line in (read.Detail!["lines"] as JsonArray)?.OfType<JsonObject>() ?? [])
        {
            var number = Str(line, "shipmentLineNumber") ?? string.Empty;
            sourceLines.TryGetValue(number, out var origin);
            var matched = holds.OfType<JsonObject>().Where(h => origin?.SkuId is not null
                && string.Equals(Str(h["item"] as JsonObject ?? [], "skuId"), origin.SkuId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Str(h["lot"] as JsonObject ?? [], "lotId") ?? string.Empty, origin.LotId ?? string.Empty, StringComparison.Ordinal)).ToList();
            used.UnionWith(matched);
            lines.Add(new JsonObject { ["shipmentLineNumber"] = number, ["inventoryPosting"] = Str(line, "inventoryPosting"),
                ["inventoryPostingReason"] = Str(line, "inventoryPostingReason"), ["holds"] = new JsonArray(matched.Select(HoldLink).ToArray<JsonNode?>()) });
        }
        var other = new JsonArray(holds.OfType<JsonObject>().Where(h => !used.Contains(h)).Select(HoldLink).ToArray<JsonNode?>());
        return JsonOk(new JsonObject { ["lines"] = lines, ["other"] = other }.ToJsonString(), "application/json");
    }

    private static JsonObject HoldLink(JsonObject hold) => new()
    { ["holdId"] = Str(hold, "holdId"), ["holdNumber"] = Str(hold, "holdNumber"), ["status"] = Str(hold, "status") };

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

    // ── r10 read helpers ─────────────────────────────────────────────────────────────────────────────────────────
    internal const string CrmAccountRead = "crm.account.read";
    internal const string QuarantineHoldsRead = "quarantine.holds.read";
    internal const int PickerPageSize = 20;
    internal const int LocationLayerPageSize = 200;
    internal static readonly string[] ReasonCodes = ["DAMAGED", "WRONG_ITEM", "WRONG_QUANTITY", "EXPIRED", "QUALITY_COMPLAINT", "CUSTOMER_REFUSED", "OTHER"];

    private sealed record GatewayReply(int Status, string Body);
    private sealed record ReturnRead(JsonObject? Detail, IActionResult? Failure);

    /// <summary>A read through the Gateway with the Returns header policy (caller's token, tenant and chosen company; the
    /// browser trace). Status 0 = unreachable. Only the status is logged, never the body (RB-26).</summary>
    private async Task<GatewayReply> GatewayGetAsync(string pathAndQuery, Scope scope, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _gatewayUrl + pathAndQuery);
        AddReturnsHeaders(request, scope, scope.Trace);
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("Returns read answered {StatusCode}. Trace {Trace}.", (int)response.StatusCode, scope.Trace);
            return new((int)response.StatusCode, body);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { _logger.LogWarning("Returns read timed out. Trace {Trace}.", scope.Trace); }
        catch (HttpRequestException) { _logger.LogWarning("Returns read failed: Gateway unreachable. Trace {Trace}.", scope.Trace); }
        return new(0, string.Empty);
    }

    private async Task<ReturnRead> ReadReturnAsync(Guid returnId, Scope scope, CancellationToken cancellationToken)
    {
        var reply = await GatewayGetAsync($"{ReturnsPath}/{returnId:D}", scope, cancellationToken);
        if (reply.Status == StatusCodes.Status200OK)
            return JsonNode.Parse(reply.Body) is JsonObject detail ? new(detail, null) : new(null, ContractFailure(StatusCodes.Status503ServiceUnavailable, "PERSISTENCE_UNAVAILABLE"));
        return new(null, reply.Status switch
        {
            StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden or StatusCodes.Status400BadRequest => ContractFailure(reply.Status, "INVALID_REQUEST"),
            StatusCodes.Status404NotFound => ContractFailure(StatusCodes.Status404NotFound, "RETURN_NOT_FOUND"),
            _ => ContractFailure(StatusCodes.Status503ServiceUnavailable, "PERSISTENCE_UNAVAILABLE")
        });
    }

    private IActionResult PickerFailure(int status) => status switch
    {
        StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden => ContractFailure(status, "INVALID_REQUEST"),
        _ => ContractFailure(StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE")
    };

    private async Task<IActionResult> LocationLayerAsync(string query, CancellationToken cancellationToken)
    {
        var gateway = Diten.Web.Models.Inventory.InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return ContractFailure(StatusCodes.Status403Forbidden, "INVALID_REQUEST");
        var reply = await gateway.GetAsync("/api/locations/search?" + query, cancellationToken);
        if (!reply.IsSuccess) return PickerFailure(reply.StatusCode);
        if (JsonNode.Parse(reply.Body) is not JsonObject body || body["items"] is not JsonArray rows) return ContractFailure(StatusCodes.Status503ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        var items = rows.OfType<JsonObject>().Where(i => !string.IsNullOrWhiteSpace(Str(i, "locationId"))).Select(i => (JsonNode?)new JsonObject
        {
            ["id"] = Str(i, "locationId"), ["code"] = Str(i, "code"), ["name"] = Str(i, "name"), ["type"] = Str(i, "type"),
            ["isStockable"] = i["isStockable"]?.GetValueKind() == JsonValueKind.True
        }).ToArray();
        return JsonOk(new JsonObject { ["items"] = new JsonArray(items) }.ToJsonString(), "application/json");
    }

    private static ContentResult JsonOk(string json, string contentType = "application/json") =>
        new() { StatusCode = StatusCodes.Status200OK, ContentType = contentType, Content = json };
    private static string Search(string? search) => string.IsNullOrWhiteSpace(search) ? string.Empty : "&search=" + Uri.EscapeDataString(search.Trim());
    private static string? Str(JsonObject node, string name) => node[name]?.GetValueKind() == JsonValueKind.String ? node[name]!.GetValue<string>() : null;
    private static bool ReturnWireUuid(string? value) => Guid.TryParseExact(value, "D", out _);
    private static JsonObject Resolved(string label) => new() { ["state"] = "resolved", ["label"] = label };
    private static JsonObject Unavailable(string? reference) => new() { ["state"] = "unavailable", ["reference"] = reference };

    /// <summary>
    /// r10 §35.2 / §35.5 read-time names for one request: per-request de-duplication, at most four concurrent reads, no cache
    /// across requests or tenants, the caller's token on every read. A failed or denied read never fails the page. The customer
    /// is resolved from a customerRef only: none → "old record", not a lower-case Guid → "not found" with no CRM call, CRM 404 →
    /// "not found", anything else → "unavailable"; never an id. Nothing read from CRM is logged (RB-26).
    /// </summary>
    private sealed class ReturnNames(SupplyChainReturnsController owner, Scope scope)
    {
        private readonly SemaphoreSlim _gate = new(4);
        private readonly Dictionary<string, Task<JsonObject>> _cache = new(StringComparer.Ordinal);
        private readonly object _sync = new();

        public Task<JsonObject> CustomerAsync(string? customerRef, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(customerRef)) return Task.FromResult(new JsonObject { ["state"] = "oldRecord" });
            if (!Diten.Web.Models.SupplyChain.Shipments.ShipmentCustomer.IsCustomerRef(customerRef)) return Task.FromResult(new JsonObject { ["state"] = "notFound" });
            return Once("c:" + customerRef, null, async () =>
            {
                var reply = await owner.GatewayGetAsync($"/api/crm/accounts/{customerRef}", scope, ct);
                if (reply.Status == StatusCodes.Status404NotFound) return new JsonObject { ["state"] = "notFound" };
                var account = reply.Status == StatusCodes.Status200OK ? Diten.Web.Models.SupplyChain.Shipments.ShipmentCustomer.ReadDetail(reply.Body) : null;
                return account is null ? new JsonObject { ["state"] = "unavailable" } : new JsonObject { ["state"] = "resolved", ["name"] = account.Name, ["code"] = account.Code };
            });
        }

        public Task<JsonObject> LocationAsync(string locationId, CancellationToken ct) => Once("l:" + locationId, locationId, async () =>
        {
            var gateway = Diten.Web.Models.Inventory.InventoryProxy.Gateway(owner._httpClient, owner._gatewayUrl, owner.Request, owner.User);
            if (gateway is null) return Unavailable(locationId);
            var reply = await gateway.GetAsync("/api/locations/" + Uri.EscapeDataString(locationId), ct);
            var json = reply.IsSuccess ? JsonNode.Parse(reply.Body) as JsonObject : null;
            var label = json is null ? null : Label(Str(json, "code"), Str(json, "name"));
            return label is null ? Unavailable(locationId) : Resolved(label);
        });

        public Task<JsonObject> LotAsync(string lotId, CancellationToken ct) => Once("t:" + lotId, null, async () =>
        {
            var trace = Diten.Web.Models.Traceability.TraceGatewayClient.For(owner._httpClient, owner._gatewayUrl, owner.Request, owner.User);
            if (trace is null) return Unavailable(null);
            var reply = await trace.GetAsync("/api/trace/lots/" + Uri.EscapeDataString(lotId), ct);
            var json = reply.IsSuccess ? JsonNode.Parse(reply.Body) as JsonObject : null;
            var number = json is null ? null : Str(json, "lotNumber");
            return string.IsNullOrWhiteSpace(number) ? Unavailable(null) : Resolved(number);
        });

        /// <summary>One PRODUCT-MASTER v1.1 bulk read (name + canonicalCode) for every SKU of the shipment.</summary>
        public async Task<Dictionary<string, string>> SkusAsync(IReadOnlyList<string> skuIds, CancellationToken ct)
        {
            var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var gateway = Diten.Web.Models.Inventory.InventoryProxy.Gateway(owner._httpClient, owner._gatewayUrl, owner.Request, owner.User);
            if (skuIds.Count == 0 || gateway is null) return labels;
            try
            {
                var reply = await gateway.PostAsync("/api/product-master/skus/bulk-read", new { skuIds = skuIds.Take(200).ToArray() }, Guid.NewGuid().ToString("D"), ct);
                var json = reply.IsSuccess ? JsonNode.Parse(reply.Body) as JsonObject : null;
                foreach (var item in (json?["items"] as JsonArray)?.OfType<JsonObject>() ?? [])
                    if (Str(item, "skuId") is { } id && Label(Str(item, "canonicalCode"), Str(item, "name")) is { } label) labels.TryAdd(id, label);
            }
            catch (JsonException) { }
            return labels;
        }

        private Task<JsonObject> Once(string key, string? reference, Func<Task<JsonObject>> read)
        {
            lock (_sync)
            {
                if (_cache.TryGetValue(key, out var existing)) return existing;
                var task = Gate(read, reference);
                _cache[key] = task;
                return task;
            }
        }

        private async Task<JsonObject> Gate(Func<Task<JsonObject>> read, string? reference)
        {
            await _gate.WaitAsync();
            try { return await read(); }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException) { return Unavailable(reference); }
            finally { _gate.Release(); }
        }

        private static string? Label(string? code, string? name) =>
            string.IsNullOrWhiteSpace(code) ? (string.IsNullOrWhiteSpace(name) ? null : name)
            : string.IsNullOrWhiteSpace(name) || string.Equals(code, name, StringComparison.OrdinalIgnoreCase) ? code : $"{code} · {name}";
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
    /// never relayed raw. Error details travel only through the <see cref="RelayedDetails"/> allow-list.
    /// </summary>
    private async Task<IActionResult> ReenvelopeFailureAsync(HttpResponseMessage response, Guid trace,
        CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        var code = ErrorCodeOf(text);
        var unpublishedRefusal = status == StatusCodes.Status422UnprocessableEntity && code is not null && RefusalCodeShape.IsMatch(code);
        if (!unpublishedRefusal && (code is null || !PublishedCodes.Contains(code)))
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

        var details = RelayedDetailsOf(code, text);
        return details is null
            ? StatusCode(status, new { error = new { code, message = DefaultMessage(code), correlationId = trace }, contractVersion = "v1" })
            : StatusCode(status, new { error = new { code, message = DefaultMessage(code), details, correlationId = trace }, contractVersion = "v1" });
    }

    private static Dictionary<string, string>? RelayedDetailsOf(string code, string text)
    {
        if (!RelayedDetails.TryGetValue(code, out var fields)) return null;
        try
        {
            using var document = JsonDocument.Parse(text);
            if (!document.RootElement.TryGetProperty("error", out var error)
                || !error.TryGetProperty("details", out var details) || details.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var relayed = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                if (details.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String) relayed[field] = value.GetString()!;
            }

            return relayed;
        }
        catch (JsonException)
        {
            return null;
        }
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

        if (!TryResolveScopeClaim(["tenant_id", "tenantId"], "/tenantId", out var tenantId))
        {
            failure = ContractFailure(StatusCodes.Status403Forbidden, "INVALID_REQUEST");
            return false;
        }
        // R-2 (SHIPMENT-BUNDLE 3.2.0): LegalEntityId is the user's choice on the page, arriving as
        // ?legalEntityId=. Missing or malformed is a REQUEST fault (400), not an authorization answer (403) —
        // the service's own middleware answers a missing X-Legal-Entity-Id the same way.
        if (!LegalEntityScopeRequest.TryResolve(Request, out var legalEntityId))
        {
            failure = ContractFailure(StatusCodes.Status400BadRequest, "INVALID_REQUEST");
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
