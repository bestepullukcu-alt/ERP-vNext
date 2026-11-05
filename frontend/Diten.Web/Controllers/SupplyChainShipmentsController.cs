using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    private readonly Microsoft.Extensions.Localization.IStringLocalizer<Diten.Web.Views.SupplyChain.Shipments.ShipmentsIndex>? _localizer;

    public SupplyChainShipmentsController(HttpClient httpClient, IConfiguration configuration,
        ILogger<SupplyChainShipmentsController> logger,
        Microsoft.Extensions.Localization.IStringLocalizer<Diten.Web.Views.SupplyChain.Shipments.ShipmentsIndex>? localizer = null)
    {
        _localizer = localizer;
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

    // ── MOD-0183 r9 §24.3 Golden Compact list (DL-4): DitenDataTable.createList server mode → queryShipments 3.5.0 ──
    // The factory sends start/length/search/orderBy/orderDir/draw and the status / source-document filters; the answer is the
    // factory's { data: { items, total, filteredTotal } } with names resolved for the page (§24.4). customerRef never reaches
    // the browser: the customer cell carries a name, "old record", "not found" or "name unavailable" (SB-9, SB-10, SB-11a).
    internal static readonly string[] ListOrderColumns = ["shipmentNumber", "sourceDocumentId", "status", "plannedShipAt", "plannedDeliverAt"];

    [HttpGet("api")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? sourceDocumentId,
        [FromQuery] int start = 0, [FromQuery] int length = 50, [FromQuery] string? search = null, [FromQuery] string? orderBy = null,
        [FromQuery] string? orderDir = null, [FromQuery] int draw = 0, CancellationToken cancellationToken = default)
    {
        if (!HasPermission(ReadPermission)) return Task.FromResult(ContractFailure(StatusCodes.Status403Forbidden));
        if (start < 0 || length is < 1 or > 200 || (search?.Length ?? 0) > 100
            || (orderBy is not null && !ListOrderColumns.Contains(orderBy, StringComparer.Ordinal))
            || (orderDir is not null && orderDir is not ("asc" or "desc")))
            return Task.FromResult(ContractFailure(StatusCodes.Status400BadRequest));

        var page = start / length + 1;
        var query = new List<string> { $"page={page}", $"pageSize={length}" };
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(sourceDocumentId)) query.Add($"sourceDocumentId={Uri.EscapeDataString(sourceDocumentId.Trim())}");
        if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (orderBy is not null) query.Add($"orderBy={orderBy}&orderDir={orderDir ?? "asc"}");
        var target = $"{_gatewayUrl}/api/shipment-bundle/shipments?{string.Join('&', query)}";
        return ProxyAsync(HttpMethod.Get, target, null, false, cancellationToken, async (body, ct) =>
        {
            var names = new ShipmentNames(this);
            var items = body["items"] as JsonArray ?? [];
            await Task.WhenAll(items.OfType<JsonObject>().Select(item => names.FillSummaryAsync(item, ct)));
            var total = body["total"]?.DeepClone();
            return new JsonObject
            {
                ["data"] = new JsonObject
                {
                    ["items"] = items.DeepClone(), ["total"] = total, ["filteredTotal"] = body["filteredTotal"]?.DeepClone() ?? total?.DeepClone(),
                    ["page"] = page, ["pageSize"] = length
                },
                ["draw"] = draw, ["contractVersion"] = "v1"
            };
        });
    }

    // ── DL-4 server export (BL-452; owner decision Natig 2026-11-05): every matching row as CSV or Excel, names resolved ──
    // Same filters, search and order as the list; at most ShipmentExport.MaxRows rows, else 413 (the factory says "too large").
    // The export request carries no correlation id, so the adapter mints one for its own downstream calls.
    private static readonly string[] ExportColumns = ["shipmentNumber", "sourceDocumentId", "customer", "warehouse", "status", "plannedShipAt", "plannedDeliverAt"];

    [HttpGet("api/export")]
    [JsonAdapterEndpoint]
    public async Task<IActionResult> Export([FromQuery] string? format, [FromQuery] string? columns, [FromQuery] string? search,
        [FromQuery] string? orderBy, [FromQuery] string? orderDir, [FromQuery] string? status, [FromQuery] string? sourceDocumentId,
        CancellationToken cancellationToken)
    {
        if (!HasPermission(ReadPermission)) return ContractFailure(StatusCodes.Status403Forbidden);
        var keys = string.IsNullOrWhiteSpace(columns) ? ExportColumns : columns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (format is not ("csv" or "xlsx") || keys.Length == 0 || keys.Any(k => !ExportColumns.Contains(k, StringComparer.Ordinal))
            || (search?.Length ?? 0) > 100 || (orderBy is not null && !ListOrderColumns.Contains(orderBy, StringComparer.Ordinal))
            || (orderDir is not null && orderDir is not ("asc" or "desc")))
            return ContractFailure(StatusCodes.Status400BadRequest);
        if (!Request.Headers.ContainsKey(CorrelationHeader)) Request.Headers[CorrelationHeader] = Guid.NewGuid().ToString("D");

        var names = new ShipmentNames(this);
        var rows = new List<JsonObject>();
        for (var page = 1; rows.Count < ShipmentExport.MaxRows; page++)
        {
            var query = new List<string> { $"page={page}", "pageSize=200" };
            if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
            if (!string.IsNullOrWhiteSpace(sourceDocumentId)) query.Add($"sourceDocumentId={Uri.EscapeDataString(sourceDocumentId.Trim())}");
            if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search.Trim())}");
            if (orderBy is not null) query.Add($"orderBy={orderBy}&orderDir={orderDir ?? "asc"}");
            if (!TryCreateGatewayRequest(HttpMethod.Get, $"{_gatewayUrl}/api/shipment-bundle/shipments?{string.Join('&', query)}", null, false, out var request, out var failed))
                return ContractFailure(failed);
            JsonObject? body;
            try
            {
                using (request)
                using (var response = await _httpClient.SendAsync(request, cancellationToken))
                {
                    if (!response.IsSuccessStatusCode)
                        return ContractFailure((int)response.StatusCode is 401 or 403 or 400 ? (int)response.StatusCode : StatusCodes.Status503ServiceUnavailable);
                    body = TryParseObject(await response.Content.ReadAsStringAsync(cancellationToken));
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Shipment export page {Page} failed: Gateway unreachable.", page);
                return ContractFailure(StatusCodes.Status503ServiceUnavailable);
            }
            if (body is null) return ContractFailure(StatusCodes.Status503ServiceUnavailable);
            var filtered = body["filteredTotal"]?.GetValue<long>() ?? body["total"]?.GetValue<long>() ?? 0;
            if (filtered > ShipmentExport.MaxRows) return ContractFailure(StatusCodes.Status413PayloadTooLarge);
            var items = (body["items"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
            await Task.WhenAll(items.Select(item => names.FillSummaryAsync(item, cancellationToken)));
            rows.AddRange(items);
            if (items.Count < 200 || rows.Count >= filtered) break;
        }

        string Text(string key) => _localizer?[key].Value ?? key;
        string Header(string key) => key switch
        {
            "shipmentNumber" => Text("ShipmentNumber"), "sourceDocumentId" => Text("SourceDocumentId"), "customer" => Text("Customer"),
            "warehouse" => Text("Warehouse"), "status" => Text("Status"), "plannedShipAt" => Text("PlannedShipAt"), _ => Text("PlannedDeliverAt")
        };
        static string? Field(JsonObject row, string name) => row[name]?.GetValueKind() == JsonValueKind.String ? row[name]!.GetValue<string>() : null;
        string? Cell(JsonObject row, string key) => key switch
        {
            "customer" => (row["customer"] as JsonObject) is { } c ? Field(c, "state") switch
            {
                "resolved" => string.Join(" · ", new[] { Field(c, "name"), Field(c, "code") }.Where(x => !string.IsNullOrWhiteSpace(x))),
                "oldRecord" => $"{Text("OldRecord")}: {Field(c, "reference")}",
                "notFound" => Text("NameNotFound"),
                _ => Text("NameUnavailable")
            } : Text("NameUnavailable"),
            "warehouse" => (row["warehouse"] as JsonObject) is { } w && Field(w, "state") == "resolved" ? Field(w, "label")
                : $"{Text("NameUnavailable")} {Field(row["warehouse"] as JsonObject ?? [], "reference")}".Trim(),
            "status" => Field(row, "status") is { } s ? Text("Status" + s) : null,
            _ => Field(row, key)
        };
        var headers = keys.Select(Header).ToList();
        var lines = rows.Select(row => (IReadOnlyList<string?>)keys.Select(k => Cell(row, k)).ToList());
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmm", System.Globalization.CultureInfo.InvariantCulture);
        _logger.LogInformation("Shipment export wrote {Rows} rows as {Format}.", rows.Count, format);
        return format == "csv"
            ? File(ShipmentExport.Csv(headers, lines), "text/csv; charset=utf-8", $"shipments-{stamp}.csv")
            : File(ShipmentExport.Xlsx(headers, lines), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"shipments-{stamp}.xlsx");
    }

    // ── §24.4 details: getShipment with names for the summary card and the lines sub-table (SB-12) ──
    [HttpGet("api/{shipmentId:guid}")]
    [JsonAdapterEndpoint]
    public Task<IActionResult> Detail(Guid shipmentId, CancellationToken cancellationToken)
    {
        if (!HasPermission(ReadPermission)) return Task.FromResult(ContractFailure(StatusCodes.Status403Forbidden));
        return ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/shipment-bundle/shipments/{shipmentId:D}", null, false, cancellationToken,
            async (body, ct) => { await new ShipmentNames(this).FillDetailAsync(body, ct); return body; });
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
        bool includeIdempotencyKey, CancellationToken cancellationToken, Func<JsonObject, CancellationToken, Task<JsonObject>>? enrich = null)
    {
        if (!TryCreateGatewayRequest(method, targetUrl, content, includeIdempotencyKey, out var request, out var status))
            return ContractFailure(status);
        using (request) return await SendAsync(request, method, targetUrl, cancellationToken, enrich);
    }

    private async Task<IActionResult> SendAsync(HttpRequestMessage request, HttpMethod method, string targetUrl, CancellationToken cancellationToken,
        Func<JsonObject, CancellationToken, Task<JsonObject>>? enrich = null)
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
                if (enrich is not null && response.IsSuccessStatusCode && TryParseObject(body) is { } parsed)
                    body = (await enrich(parsed, cancellationToken)).ToJsonString();
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

    private static JsonObject? TryParseObject(string body)
    {
        try { return JsonNode.Parse(body) as JsonObject; }
        catch (JsonException) { return null; }
    }

    /// <summary>
    /// MOD-0183 r9 §24.4 read-time names for one page request: per-request de-duplication, at most four concurrent reads, no
    /// cache across requests or tenants (SB-15), the caller's token on every read. A failed or denied read never fails the page:
    /// it yields "unavailable" with the reference for warehouse, location, SKU and lot, and no id for the customer (SB-10).
    /// customerRef is removed from what the browser receives (SB-9, SB-20); nothing read from CRM is logged (SB-21).
    /// </summary>
    private sealed class ShipmentNames(SupplyChainShipmentsController owner)
    {
        private readonly SemaphoreSlim _gate = new(4);
        private readonly Dictionary<string, Task<JsonObject>> _customers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Task<JsonObject>> _locations = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Task<JsonObject>> _lots = new(StringComparer.Ordinal);
        private readonly object _sync = new();
        private Diten.Web.Models.Inventory.InventoryGatewayClient? _gateway;
        private Diten.Web.Models.Inventory.InventoryGatewayClient? Gateway => _gateway ??= Diten.Web.Models.Inventory.InventoryProxy.Gateway(owner._httpClient, owner._gatewayUrl, owner.Request, owner.User);

        public async Task FillSummaryAsync(JsonObject shipment, CancellationToken ct)
        {
            var customerRef = shipment["customerRef"]?.GetValueKind() == JsonValueKind.String ? shipment["customerRef"]!.GetValue<string>() : null;
            var warehouse = shipment["warehouseReferenceId"]?.GetValueKind() == JsonValueKind.String ? shipment["warehouseReferenceId"]!.GetValue<string>() : null;
            var shipTo = shipment["shipToReference"]?.GetValueKind() == JsonValueKind.String ? shipment["shipToReference"]!.GetValue<string>() : null;
            shipment.Remove("customerRef");
            var customerTask = CustomerAsync(customerRef, shipTo, ct);
            var warehouseTask = LocationAsync(warehouse, ct);
            shipment["customer"] = (await customerTask).DeepClone();
            shipment["warehouse"] = (await warehouseTask).DeepClone();
        }

        public async Task FillDetailAsync(JsonObject shipment, CancellationToken ct)
        {
            var lines = (shipment["lines"] as JsonArray)?.OfType<JsonObject>().ToList() ?? [];
            var skuIds = lines.Select(l => Str(l, "skuId")).Where(x => Guid.TryParse(x, out _)).Distinct(StringComparer.OrdinalIgnoreCase).Take(200).ToList();
            var skusTask = SkusAsync(skuIds!, ct);
            var summaryTask = FillSummaryAsync(shipment, ct);
            var lineTasks = lines.Select(async line =>
            {
                var location = await LocationAsync(Str(line, "fromLocationId"), ct);
                var lotId = Str(line, "lotId");
                line["fromLocation"] = location.DeepClone();
                line["lot"] = string.IsNullOrWhiteSpace(lotId) ? null : (await LotAsync(lotId!, ct)).DeepClone();
            }).ToList();
            var skus = await skusTask;
            foreach (var line in lines)
            {
                var skuId = Str(line, "skuId") ?? string.Empty;
                line["sku"] = (skus.TryGetValue(skuId, out var label) ? Resolved(label) : Unavailable(skuId)).DeepClone();
            }
            await summaryTask;
            await Task.WhenAll(lineTasks);
        }

        private Task<JsonObject> CustomerAsync(string? customerRef, string? shipTo, CancellationToken ct)
        {
            // SB-11a / SB-19: an old record keeps its shipToReference as stored, marked "old record", and is never sent to CRM.
            if (string.IsNullOrEmpty(customerRef)) return Task.FromResult(new JsonObject { ["state"] = "oldRecord", ["reference"] = shipTo });
            // SB-25: a stored value that is not a lower-case "D" Guid is "not found", with no CRM call.
            if (!ShipmentCustomer.IsCustomerRef(customerRef)) return Task.FromResult(new JsonObject { ["state"] = "notFound" });
            if (!owner.HasPermission(CrmAccountReadPermission)) return Task.FromResult(new JsonObject { ["state"] = "unavailable" });
            return Once(_customers, customerRef, keepReference: false, async () =>
            {
                var reply = await owner.CrmGetAsync($"{CrmAccountsPath}/{customerRef}", ct);
                if (reply.Status == StatusCodes.Status404NotFound) return new JsonObject { ["state"] = "notFound" };
                var account = reply.Status == StatusCodes.Status200OK ? ShipmentCustomer.ReadDetail(reply.Body) : null;
                return account is null ? new JsonObject { ["state"] = "unavailable" }
                    : new JsonObject { ["state"] = "resolved", ["name"] = account.Name, ["code"] = account.Code };
            });
        }

        private Task<JsonObject> LocationAsync(string? locationId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(locationId)) return Task.FromResult(new JsonObject { ["state"] = "unavailable" });
            return Once(_locations, locationId, keepReference: true, async () =>
            {
                var gateway = Gateway;
                if (gateway is null) return Unavailable(locationId);
                var reply = await gateway.GetAsync("/api/locations/" + Uri.EscapeDataString(locationId), ct);
                var json = reply.IsSuccess ? TryParseObject(reply.Body) : null;
                var label = json is null ? null : Label(Str(json, "code"), Str(json, "name"));
                return label is null ? Unavailable(locationId) : Resolved(label);
            });
        }

        private Task<JsonObject> LotAsync(string lotId, CancellationToken ct) => Once(_lots, lotId, keepReference: true, async () =>
        {
            var trace = Diten.Web.Models.Traceability.TraceGatewayClient.For(owner._httpClient, owner._gatewayUrl, owner.Request, owner.User);
            if (trace is null) return Unavailable(lotId);
            var reply = await trace.GetAsync("/api/trace/lots/" + Uri.EscapeDataString(lotId), ct);
            var json = reply.IsSuccess ? TryParseObject(reply.Body) : null;
            var number = json is null ? null : Str(json, "lotNumber");
            return string.IsNullOrWhiteSpace(number) ? Unavailable(lotId) : Resolved(number);
        });

        // One PRODUCT-MASTER v1.1 bulk read for every SKU of the shipment (name + canonicalCode; the name already names the product).
        private async Task<Dictionary<string, string>> SkusAsync(IReadOnlyList<string> skuIds, CancellationToken ct)
        {
            var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var gateway = Gateway;
            if (skuIds.Count == 0 || gateway is null) return labels;
            await _gate.WaitAsync(ct);
            try
            {
                var reply = await gateway.PostAsync("/api/product-master/skus/bulk-read", new { skuIds }, Guid.NewGuid().ToString("D"), ct);
                var json = reply.IsSuccess ? TryParseObject(reply.Body) : null;
                foreach (var item in (json?["items"] as JsonArray)?.OfType<JsonObject>() ?? [])
                    if (Str(item, "skuId") is { } id && Label(Str(item, "canonicalCode"), Str(item, "name")) is { } label) labels.TryAdd(id, label);
            }
            finally { _gate.Release(); }
            return labels;
        }

        // VER F-4: a read that throws keeps its reference for warehouse, location and lot (SB-10); the customer never has one.
        private Task<JsonObject> Once(Dictionary<string, Task<JsonObject>> cache, string key, bool keepReference, Func<Task<JsonObject>> read)
        {
            lock (_sync)
            {
                if (cache.TryGetValue(key, out var existing)) return existing;
                var task = Gate(read, keepReference ? key : null);
                cache[key] = task;
                return task;
            }
        }

        private async Task<JsonObject> Gate(Func<Task<JsonObject>> read, string? reference)
        {
            await _gate.WaitAsync();
            try { return await read(); }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                return reference is null ? new JsonObject { ["state"] = "unavailable" } : Unavailable(reference);
            }
            finally { _gate.Release(); }
        }

        private static JsonObject Resolved(string label) => new() { ["state"] = "resolved", ["label"] = label };
        private static JsonObject Unavailable(string? reference) => new() { ["state"] = "unavailable", ["reference"] = reference };
        private static string? Label(string? code, string? name) =>
            string.IsNullOrWhiteSpace(code) ? (string.IsNullOrWhiteSpace(name) ? null : name)
            : string.IsNullOrWhiteSpace(name) || string.Equals(code, name, StringComparison.OrdinalIgnoreCase) ? code : $"{code} · {name}";
        private static string? Str(JsonObject node, string name) => node[name]?.GetValueKind() == JsonValueKind.String ? node[name]!.GetValue<string>() : null;
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
