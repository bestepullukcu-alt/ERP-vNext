using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.SupplyChain.Shipments;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Diten.Web.Services.SupplyChain;

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
    // MOD-0183 r9 §24.1: the customer picker's display gate is create + CRM's own read key (not a SupplyChain key).
    internal const string CrmAccountReadPermission = "crm.account.read";
    internal const string CrmAccountsPath = "/api/crm/accounts";
    internal const int CustomerPageSize = 20;
    internal const int PickerPageSize = 50;
    internal const int LocationLayerPageSize = 200;

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
        // G6-prep fix (2026-10-18): the workspace's API calls need the legal-entity scope the adapter requires
        // (?legalEntityId=, else 400). It arrives on the page URL from the list or the create flow; parsed here with the
        // same rule as the adapter so the page never forwards a value the adapter would reject. Empty = fail closed.
        ViewBag.LegalEntityId = LegalEntityScopeRequest.TryResolve(Request, out var legalEntityId) ? legalEntityId.ToString("D") : string.Empty;
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

    // ── MOD-0183 r9 §24.1 customer picker: GET api/customers?search=&page= → GET /api/crm/accounts (name and code only) ──
    // Contractless measured use (open item 1): only id, name and code reach the browser; nothing from CRM is logged (SB-21).
    [HttpGet("api/customers")]
    [JsonAdapterEndpoint]
    public async Task<IActionResult> Customers([FromQuery] string? search, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        if (!HasPermission(CreatePermission) || !HasPermission(CrmAccountReadPermission)) return ContractFailure(StatusCodes.Status403Forbidden);
        if (page < 1 || page > 50 || (search?.Length ?? 0) > 100) return ContractFailure(StatusCodes.Status400BadRequest);
        var query = $"page={page}&pageSize={CustomerPageSize}" + (string.IsNullOrWhiteSpace(search) ? string.Empty : "&search=" + Uri.EscapeDataString(search.Trim()));
        var reply = await CrmGetAsync($"{CrmAccountsPath}?{query}", cancellationToken);
        if (reply.Status != StatusCodes.Status200OK) return CrmFailure(reply.Status, notFoundIsInvalid: false);
        var rows = ShipmentCustomer.ReadList(reply.Body);
        if (rows is null) return ContractFailure(StatusCodes.Status503ServiceUnavailable);
        return Ok(new { items = rows.Items.Select(r => new { id = r.Id, name = r.Name, code = r.Code }), hasMore = (long)page * CustomerPageSize < rows.Total });
    }

    // ── §24.1 address after a choice: GET api/customers/{id} → GET /api/crm/accounts/{id} (name, code, four address fields) ──
    [HttpGet("api/customers/{accountId:guid}")]
    [JsonAdapterEndpoint]
    public async Task<IActionResult> Customer(Guid accountId, CancellationToken cancellationToken)
    {
        if (!HasPermission(CreatePermission) || !HasPermission(CrmAccountReadPermission)) return ContractFailure(StatusCodes.Status403Forbidden);
        var reply = await CrmGetAsync($"{CrmAccountsPath}/{accountId:D}", cancellationToken);
        if (reply.Status != StatusCodes.Status200OK) return CrmFailure(reply.Status, notFoundIsInvalid: true);
        var address = ShipmentCustomer.ReadDetail(reply.Body);
        return address is null ? ContractFailure(StatusCodes.Status503ServiceUnavailable) : Ok(address);
    }

    // ── §24.2 warehouse picker: GET api/warehouses?search= → LOCATION /api/locations/search (Warehouse, Active) ──
    [HttpGet("api/warehouses")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> Warehouses([FromQuery] string? search, CancellationToken cancellationToken)
    {
        if (!HasPermission(CreatePermission)) return Task.FromResult(ContractFailure(StatusCodes.Status403Forbidden));
        if ((search?.Length ?? 0) > 100) return Task.FromResult(ContractFailure(StatusCodes.Status400BadRequest));
        return LocationLayerAsync($"type=Warehouse&status=Active&page=1&pageSize={PickerPageSize}" + Search(search), cancellationToken);
    }

    // ── §24.2 tree picker: one layer at a time, parentId = the opened node, Active, WITHOUT isStockable (SB-7) ──
    [HttpGet("api/locations")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> Locations([FromQuery] string? parentId, [FromQuery] string? search, CancellationToken cancellationToken)
    {
        if (!HasPermission(CreatePermission)) return Task.FromResult(ContractFailure(StatusCodes.Status403Forbidden));
        if (!Guid.TryParse(parentId, out var parent) || (search?.Length ?? 0) > 100) return Task.FromResult(ContractFailure(StatusCodes.Status400BadRequest));
        return LocationLayerAsync($"parentId={parent:D}&status=Active&page=1&pageSize={LocationLayerPageSize}" + Search(search), cancellationToken);
    }

    // ── §24.2 product / SKU picker: GET api/products?q= → PRODUCT-MASTER /stockable-items (Active), name + canonicalCode ──
    // Accepted before G6 for display and selection only (OD-183-2); the service still does not validate products.
    [HttpGet("api/products")]
    [JsonAdapterEndpoint]
    public async Task<IActionResult> Products([FromQuery] string? q, CancellationToken cancellationToken)
    {
        if (!HasPermission(CreatePermission)) return ContractFailure(StatusCodes.Status403Forbidden);
        if ((q?.Length ?? 0) > 200) return ContractFailure(StatusCodes.Status400BadRequest);
        var gateway = Diten.Web.Models.Inventory.InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return ContractFailure(StatusCodes.Status403Forbidden);
        var reply = await gateway.GetAsync("/api/product-master/stockable-items?status=Active&page=1&pageSize=" + PickerPageSize
            + (string.IsNullOrWhiteSpace(q) ? string.Empty : "&q=" + Uri.EscapeDataString(q.Trim())), cancellationToken);
        if (!reply.IsSuccess) return PickerFailure(reply.StatusCode);
        var items = ReadItems(reply.Body)?.Select(i => new
        {
            skuId = Text(i, "skuId"), itemId = Text(i, "itemId"), skuLevel = Text(i, "skuLevel"), name = Text(i, "name"),
            code = Text(i, "canonicalCode"), uomId = Text(i, "baseUomId"),
            lotControlled = i.TryGetProperty("lotControlled", out var lot) && lot.ValueKind == JsonValueKind.True
        }).Where(i => Guid.TryParse(i.skuId, out _) && Guid.TryParse(i.itemId, out _) && i.skuLevel is "Gsku" or "Lsku" or "FinishedGood" or "Item"
            && !string.IsNullOrWhiteSpace(i.uomId)).ToList();
        return items is null ? ContractFailure(StatusCodes.Status503ServiceUnavailable) : Ok(new { items });
    }

    // ── §24.2 lot picker: GET api/lots?skuId=&skuLevel=&q= → TRACE /api/trace/lots ──
    [HttpGet("api/lots")]
    [JsonAdapterEndpoint]
    public async Task<IActionResult> Lots([FromQuery] Guid skuId, [FromQuery] string? skuLevel, [FromQuery] string? q, CancellationToken cancellationToken)
    {
        if (!HasPermission(CreatePermission)) return ContractFailure(StatusCodes.Status403Forbidden);
        if (skuId == Guid.Empty || skuLevel is not ("Gsku" or "Lsku" or "FinishedGood" or "Item") || (q?.Length ?? 0) > 100)
            return ContractFailure(StatusCodes.Status400BadRequest);
        var trace = Diten.Web.Models.Traceability.TraceGatewayClient.For(_httpClient, _gatewayUrl, Request, User);
        if (trace is null) return ContractFailure(StatusCodes.Status403Forbidden);
        var reply = await trace.GetAsync($"/api/trace/lots?skuId={skuId:D}&skuLevel={skuLevel}&page=1&pageSize={PickerPageSize}"
            + (string.IsNullOrWhiteSpace(q) ? string.Empty : "&q=" + Uri.EscapeDataString(q.Trim())), cancellationToken);
        if (!reply.IsSuccess) return PickerFailure(reply.StatusCode);
        var items = ReadItems(reply.Body)?.Select(i => new { id = Text(i, "lotId"), lotNumber = Text(i, "lotNumber") })
            .Where(i => !string.IsNullOrWhiteSpace(i.id) && !string.IsNullOrWhiteSpace(i.lotNumber)).ToList();
        return items is null ? ContractFailure(StatusCodes.Status503ServiceUnavailable) : Ok(new { items });
    }

    // ── §24.1 / §24.2 create adapter: format check → CRM account read (caller's token) → destination text → createShipment ──
    // SB-2a: a missing, malformed or unreadable account (CRM 404, another tenant's id included) is 400 INVALID_CUSTOMER_ACCOUNT
    // with no createShipment call; CRM 403 → 403; CRM unreachable → 503. An account with no address → 400 CUSTOMER_ADDRESS_MISSING.
    [HttpPost("api")]
    [JsonAdapterEndpoint]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateShipmentViewModel model, CancellationToken cancellationToken)
    {
        if (!HasPermission(CreatePermission) || !HasPermission(CrmAccountReadPermission)) return ContractFailure(StatusCodes.Status403Forbidden);
        if (!ModelState.IsValid || model is null) return ContractFailure(StatusCodes.Status400BadRequest);
        if (!ShipmentCustomer.IsCustomerRef(model.CustomerRef)) return ContractFailure(StatusCodes.Status400BadRequest, ShipmentCustomer.InvalidAccountCode);
        if (!TryCreateGatewayRequest(HttpMethod.Post, $"{_gatewayUrl}/api/shipment-bundle/shipments", null, true, out var request, out var status))
            return ContractFailure(status);
        using (request)
        {
            var account = await CrmGetAsync($"{CrmAccountsPath}/{model.CustomerRef}", cancellationToken);
            if (account.Status != StatusCodes.Status200OK) return CrmFailure(account.Status, notFoundIsInvalid: true);
            var address = ShipmentCustomer.ReadDetail(account.Body);
            if (address is null) return ContractFailure(StatusCodes.Status503ServiceUnavailable);
            var destination = ShipmentCustomer.Destination(address);
            if (destination is null) return ContractFailure(StatusCodes.Status400BadRequest, ShipmentCustomer.AddressMissingCode);
            request.Content = JsonContent.Create(ShipmentCreateWire.From(model, model.CustomerRef!, destination), options: _jsonOptions);
            return await SendAsync(request, HttpMethod.Post, request.RequestUri!.ToString(), cancellationToken);
        }
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
        using (request) return await SendAsync(request, method, targetUrl, cancellationToken);
    }

    private async Task<IActionResult> SendAsync(HttpRequestMessage request, HttpMethod method, string targetUrl, CancellationToken cancellationToken)
    {
        // Q372: the id TryCreateGatewayRequest validated and forwarded; every line below carries it, so one user action
        // can be followed through the Web, Gateway and service logs by the same value (MOD-0183 §8.1 O-1).
        var correlationId = request.Headers.GetValues(CorrelationHeader).Single();
        try
        {
            using (var response = await _httpClient.SendAsync(request, cancellationToken))
            {
                CopyCorrelationHeader(response);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                // Q371: when the Gateway cannot reach the service it answers 502/503/504 with no contract body. Passed
                // through as-is, the page found no error code and fell back to its validation message, so a server outage
                // read as "Check the entered values." (Q362, live). An upstream 5xx without the Error envelope is the same
                // case as an unreachable Gateway, so it gets the same envelope the HttpRequestException branch sends.
                if ((int)response.StatusCode >= 500 && !HasContractError(body))
                {
                    _logger.LogWarning("Shipment Gateway answered {StatusCode} without a contract error for {TargetUrl}; correlation {CorrelationId}.", (int)response.StatusCode, targetUrl, correlationId);
                    return ContractFailure(StatusCodes.Status503ServiceUnavailable);
                }
                _logger.LogInformation("Shipment Gateway answered {StatusCode} for {Method} {TargetUrl}; correlation {CorrelationId}.",
                    (int)response.StatusCode, method.Method, targetUrl, correlationId);
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
            _logger.LogWarning(exception, "Shipment Gateway request timed out for {TargetUrl}; correlation {CorrelationId}.", targetUrl, correlationId);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Shipment Gateway request failed for {TargetUrl}; correlation {CorrelationId}.", targetUrl, correlationId);
            return ContractFailure(StatusCodes.Status503ServiceUnavailable);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unexpected Shipment proxy failure for {TargetUrl}; correlation {CorrelationId}.", targetUrl, correlationId);
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
        if (!TryResolveScopeClaim(["tenant_id", "tenantId"], "/tenantId", out var tenantId))
            return FailRequest(request, StatusCodes.Status403Forbidden, out localStatus);
        // R-2 (SHIPMENT-BUNDLE 3.2.0): LegalEntityId is the user's choice on the page, arriving as
        // ?legalEntityId=. Missing or malformed is a REQUEST fault (400), not an authorization answer (403) —
        // the service's own middleware answers a missing X-Legal-Entity-Id the same way.
        if (!LegalEntityScopeRequest.TryResolve(Request, out var legalEntityId))
            return FailRequest(request, StatusCodes.Status400BadRequest, out localStatus);
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

    private IActionResult ContractFailure(int statusCode, string adapterCode)
    {
        var correlationId = Request.Headers.TryGetValue(CorrelationHeader, out var values)
            && values.Count == 1 && Guid.TryParse(values[0], out var parsed) ? parsed : Guid.NewGuid();
        Response.Headers[CorrelationHeader] = correlationId.ToString("D");
        // MOD-0183 r9 §24.7: INVALID_CUSTOMER_ACCOUNT / CUSTOMER_ADDRESS_MISSING are MVC adapter codes, not wire codes.
        return StatusCode(statusCode, new { error = new { code = adapterCode, message = adapterCode, correlationId }, contractVersion = "v1" });
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

    private static bool HasContractError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return false;
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(body);
            return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var error)
                && error.ValueKind == System.Text.Json.JsonValueKind.Object
                && error.TryGetProperty("code", out var code)
                && code.ValueKind == System.Text.Json.JsonValueKind.String;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }

    private sealed record CrmReply(int Status, string Body);

    /// <summary>A CRM read with the caller's token and tenant (SB-15: no cache across requests). Status 0 = unreachable.
    /// Only the status and correlation are logged, never the body (SB-21).</summary>
    private async Task<CrmReply> CrmGetAsync(string pathAndQuery, CancellationToken cancellationToken)
    {
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (string.IsNullOrWhiteSpace(token)) return new CrmReply(StatusCodes.Status401Unauthorized, string.Empty);
        if (!TryResolveScopeClaim(["tenant_id", "tenantId"], "/tenantId", out var tenantId)) return new CrmReply(StatusCodes.Status403Forbidden, string.Empty);
        using var request = new HttpRequestMessage(HttpMethod.Get, _gatewayUrl + pathAndQuery);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation(TenantHeader, tenantId.ToString("D"));
        if (Request.Headers.TryGetValue(CorrelationHeader, out var values) && values.Count == 1 && Guid.TryParse(values[0], out var correlation))
            request.Headers.TryAddWithoutValidation(CorrelationHeader, correlation.ToString("D"));
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("Shipment customer read answered {StatusCode}.", (int)response.StatusCode);
            return new CrmReply((int)response.StatusCode, body);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { _logger.LogWarning("Shipment customer read timed out."); }
        catch (HttpRequestException) { _logger.LogWarning("Shipment customer read failed: CRM unreachable."); }
        return new CrmReply(0, string.Empty);
    }

    private IActionResult CrmFailure(int status, bool notFoundIsInvalid) => status switch
    {
        StatusCodes.Status401Unauthorized => ContractFailure(StatusCodes.Status401Unauthorized),
        StatusCodes.Status403Forbidden => ContractFailure(StatusCodes.Status403Forbidden),
        StatusCodes.Status404NotFound or StatusCodes.Status400BadRequest when notFoundIsInvalid => ContractFailure(StatusCodes.Status400BadRequest, ShipmentCustomer.InvalidAccountCode),
        _ => ContractFailure(StatusCodes.Status503ServiceUnavailable)
    };

    private IActionResult PickerFailure(int status) => status switch
    {
        StatusCodes.Status401Unauthorized => ContractFailure(StatusCodes.Status401Unauthorized),
        StatusCodes.Status403Forbidden => ContractFailure(StatusCodes.Status403Forbidden),
        _ => ContractFailure(StatusCodes.Status503ServiceUnavailable)
    };

    private async Task<IActionResult> LocationLayerAsync(string query, CancellationToken cancellationToken)
    {
        var gateway = Diten.Web.Models.Inventory.InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return ContractFailure(StatusCodes.Status403Forbidden);
        var reply = await gateway.GetAsync("/api/locations/search?" + query, cancellationToken);
        if (!reply.IsSuccess) return PickerFailure(reply.StatusCode);
        var items = ReadItems(reply.Body)?.Select(i => new
        {
            id = Text(i, "locationId"), code = Text(i, "code"), name = Text(i, "name"), type = Text(i, "type"),
            isStockable = i.TryGetProperty("isStockable", out var s) && s.ValueKind == JsonValueKind.True
        }).Where(i => !string.IsNullOrWhiteSpace(i.id)).ToList();
        return items is null ? ContractFailure(StatusCodes.Status503ServiceUnavailable) : Ok(new { items });
    }

    private static string Search(string? search) => string.IsNullOrWhiteSpace(search) ? string.Empty : "&search=" + Uri.EscapeDataString(search.Trim());

    private static List<JsonElement>? ReadItems(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
                ? items.EnumerateArray().Select(x => x.Clone()).ToList() : null;
        }
        catch (JsonException) { return null; }
    }

    private static string? Text(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private bool HasPermission(string permission) => PermissionClaims.HasPermission(User, permission);
}
