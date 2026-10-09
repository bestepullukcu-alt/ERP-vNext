using System.Globalization;
using System.Text.Json;
using Diten.Web.Models.Inventory;
using Diten.Web.Models.Traceability;
using Diten.Web.Services;
using Diten.Web.Views.Traceability.Lots;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0174 P3 — Lot Gezgini, Lot Detayı, Yeni Lot / Düzenle, Durum Değiştir (pack §11.1) + the lot-field proxies of the
// Stock Ledger form (§11.4). Tenant shell MVC; views under Views/Traceability/Lots/ (ASSUMPTION-0174-01). The browser talks
// only to this same-origin controller; it calls Gateway 5000 /api/trace (INT-TRACE) through TraceGatewayClient and
// /api/inventory + PRODUCT-MASTER through the FU01 InventoryGatewayClient — cookie bearer, X-Tenant-Id, the W-0173-02
// company header. Every write is [ValidateAntiForgeryToken] and offered only to a holder of its key; InventoryService's
// [HasPermission] stays the authority (UAS-001 §4). No delete exists (pack §4).
[Authorize]
[Route("Lots")]
public sealed class LotsController : Controller
{
    private const string ViewRoot = "~/Views/Traceability/Lots";
    private const int HistoryPageSize = 10;
    private const int SerialPreview = 8;

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<LotsIndex> _localizer;
    private readonly IStringLocalizer<LotFieldIndex> _field;
    private readonly ILogger<LotsController> _logger;

    public LotsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IPermissionSnapshot permissions,
        IStringLocalizer<LotsIndex> localizer,
        IStringLocalizer<LotFieldIndex> field,
        ILogger<LotsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
        _field = field;
        _logger = logger;
    }

    // ── pages (UAS-001 is the view's: _AccessDenied and nothing else without the page key, no redirect) ──────────

    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Details")]
    public IActionResult Details([FromQuery] string? lotId) => View($"{ViewRoot}/Details.cshtml", LotPageViewModel.From(lotId));

    [HttpGet("Create")]
    public IActionResult Create() => View($"{ViewRoot}/Create.cshtml", LotFormPageViewModel.ForCreate());

    [HttpGet("Edit")]
    public IActionResult Edit([FromQuery] string? lotId) => View($"{ViewRoot}/Edit.cshtml", LotFormPageViewModel.ForEdit(lotId));

    // ── Lot Gezgini list (server mode) ──────────────────────────────────────────────────────────────────────

    [HttpGet("api")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!_permissions.Has(TracePermissionKeys.LotsRead)) return Refuse(403, null, null);
        var translation = TraceListAdapter.LotsQuery(Request.Query);
        if (!translation.IsValid) return Refuse(400, translation.ErrorCode, null);
        var trace = Trace();
        if (trace is null) return Refuse(401, null, null);

        var reply = await trace.GetAsync("/api/trace/lots" + translation.Query, cancellationToken);
        if (!reply.IsSuccess) return Failed("listLots", reply);

        var page = reply.Read<LotListDto>() ?? new LotListDto();
        var items = page.Items ?? [];
        var labels = await Labels(items, cancellationToken);
        var total = page.Total ?? items.Count;
        return Json(new
        {
            data = new { items = items.Select(l => Row(l, labels)), total, filteredTotal = total },
            labelsAvailable = labels.Available,
            contractVersion = InventoryProxy.ContractVersion
        });
    }

    // ── Lot Detayı (one call for the page: lot + stock rows + serial preview + history page 1) ──────────────

    [HttpGet("data/{lotId}")]
    public async Task<IActionResult> Data(string lotId, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(TracePermissionKeys.LotsRead)) return Refuse(403, null, null);
        if (!Guid.TryParse(lotId, out var id)) return Refuse(404, "UNKNOWN_LOT", null);
        var trace = Trace();
        if (trace is null) return Refuse(401, null, null);

        var reply = await trace.GetAsync($"/api/trace/lots/{id:D}/detail", cancellationToken);
        if (!reply.IsSuccess) return Failed("getLotDetail", reply);
        var lot = reply.Read<LotDetailDto>() ?? new LotDetailDto();

        var history = await trace.GetAsync($"/api/trace/lots/{id:D}/history?page=1&pageSize={HistoryPageSize}", cancellationToken);

        // Stok dağılımı = the ledger balance rows of the lot in the selected company (read path: its failure does not fail the page).
        object? stock = null;
        var ledger = Ledger();
        if (ledger is not null && _permissions.Has(TracePermissionKeys.StockRead))
        {
            var balance = await ledger.GetAsync($"/api/inventory/balance?lotId={id:D}&page=1&pageSize=200", cancellationToken);
            if (balance.IsSuccess)
            {
                var rows = ReadRows(balance.Body);
                var locationLabels = await MasterLabelResolver.ResolveAsync(ledger, [], [], rows.SelectMany(r => new[] { r.WarehouseId, r.LocationId }), cancellationToken);
                stock = new
                {
                    rows = rows.Select(r => new
                    {
                        warehouse = locationLabels.Location(r.WarehouseId) ?? r.WarehouseId,
                        location = locationLabels.Location(r.LocationId) ?? r.LocationId,
                        warehouseId = r.WarehouseId,
                        stockStatus = r.StockStatus,
                        onHand = r.OnHand,
                        baseUomId = r.BaseUomId
                    }),
                    locations = rows.Where(r => Positive(r.OnHand)).Select(r => r.LocationId).Distinct(StringComparer.Ordinal).Count(),
                    warehouses = rows.Where(r => Positive(r.OnHand)).Select(r => locationLabels.Location(r.WarehouseId) ?? r.WarehouseId).Distinct(StringComparer.Ordinal).ToList()
                };
            }
        }

        object? serials = null;
        if (lot.TrackingMode == "LotAndSerial" && _permissions.Has(TracePermissionKeys.SerialsRead))
        {
            var page = await trace.GetAsync($"/api/trace/serials?lotId={id:D}&page=1&pageSize={SerialPreview}", cancellationToken);
            var dto = page.IsSuccess ? page.Read<SerialPageDto>() : null;
            serials = dto is null ? null : new { items = (dto.Items ?? []).Select(s => new { s.SerialNumber, s.Status }), total = dto.Total ?? 0 };
        }

        var labels = await Labels([lot], cancellationToken);
        return Json(new
        {
            lot = Row(lot, labels),
            etag = reply.ETag,
            source = lot.Source,
            serialCount = lot.SerialCount ?? 0,
            stock,
            serials,
            history = history.IsSuccess ? HistoryRows(history.Read<LotHistoryPageDto>()) : null,
            statusOptions = LotFormRules.StatusOptions(lot.Status).Select(o => new
            {
                status = o.Status,
                allowed = o.Allowed,
                reason = o.ReasonKey is null ? null : _localizer[o.ReasonKey].Value,
                danger = o.Danger
            }),
            editAction = TraceActions.EditActionFor(lot.Status),
            contractVersion = InventoryProxy.ContractVersion
        });
    }

    [HttpGet("history/{lotId}")]
    public async Task<IActionResult> History(string lotId, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        if (!_permissions.Has(TracePermissionKeys.LotsRead)) return Refuse(403, null, null);
        if (!Guid.TryParse(lotId, out var id) || page < 1) return Refuse(400, TraceListAdapter.InvalidQuery, null);
        var trace = Trace();
        if (trace is null) return Refuse(401, null, null);
        var reply = await trace.GetAsync($"/api/trace/lots/{id:D}/history?page={page}&pageSize={HistoryPageSize}", cancellationToken);
        return reply.IsSuccess ? Json(HistoryRows(reply.Read<LotHistoryPageDto>())) : Failed("getLotHistory", reply);
    }

    // ── writes ───────────────────────────────────────────────────────────────────────────────────────────────

    // Yeni Lot (and the Stock Ledger quick lot, origin "ledger-shortcut"): always Draft (K-E), SKU + lot number fixed.
    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateLot([FromBody] LotFormInput? form, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(TracePermissionKeys.LotsCreate)) return Refuse(403, null, null);
        if (form is null || !Guid.TryParse(form.IdempotencyKey, out var key)) return Refuse(400, "INVALID_REQUEST", null);
        var errors = LotFormRules.Check(form, create: true, currentStatus: null, Today());
        if (errors.Count > 0) return FieldErrors(errors);
        var trace = Trace();
        if (trace is null) return Refuse(401, null, null);

        LotFormRules.TryParseSku(form.Sku, out var level, out var skuId);
        var idempotencyKey = (form.Origin == "ledger-shortcut" ? "quick-lot-" : "lot-") + key.ToString("N");
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = idempotencyKey,
            ["itemId"] = Guid.Parse(form.ItemId!).ToString("D"),
            ["lotNumber"] = LotFormRules.NormalizeLotNumber(form.LotNumber!),
            ["skuId"] = skuId,
            ["skuLevel"] = level
        };
        AddAttributes(body, form, includeNulls: false);

        var reply = await trace.PostAsync("/api/trace/lots", body, idempotencyKey, null, cancellationToken);
        if (!reply.IsSuccess) return WriteRefusal(reply);
        var lot = reply.Read<LotRefDto>() ?? new LotRefDto();
        return Json(new
        {
            success = true,
            lotId = lot.LotId,
            lotNumber = lot.LotNumber,
            status = lot.Status,
            option = LotFieldRules.PickerOptions([lot], Today()).FirstOrDefault(),
            redirect = $"/Lots/Details?lotId={lot.LotId}"
        });
    }

    // Düzenle: Draft → trace.lots.update; any other status → trace.lots.update-released + reason ≥ 10 (K-C, CT-D8).
    [HttpPost("edit/{lotId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditLot(string lotId, [FromBody] LotFormInput? form, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(TracePermissionKeys.LotsUpdate) && !_permissions.Has(TracePermissionKeys.LotsUpdateReleased)) return Refuse(403, null, null);
        if (form is null || !Guid.TryParse(lotId, out var id) || string.IsNullOrWhiteSpace(form.IfMatch)) return Refuse(400, "INVALID_REQUEST", null);
        var trace = Trace();
        if (trace is null) return Refuse(401, null, null);

        var current = await trace.GetAsync($"/api/trace/lots/{id:D}", cancellationToken);
        if (!current.IsSuccess) return Failed("getLot", current);
        var status = current.Read<LotRefDto>()?.Status;
        if (!TraceActions.Allowed(_permissions, TraceActions.EditActionFor(status))) return Refuse(403, null, null);

        var errors = LotFormRules.Check(form, create: false, status, Today());
        if (errors.Count > 0) return FieldErrors(errors);

        var body = new Dictionary<string, object?>();
        AddAttributes(body, form, includeNulls: true);
        if (!string.Equals(status, "Draft", StringComparison.Ordinal)) body["reason"] = form.Reason!.Trim();

        var reply = await trace.PatchAsync($"/api/trace/lots/{id:D}", body, form.IfMatch!, cancellationToken);
        if (!reply.IsSuccess) return WriteRefusal(reply);
        return Json(new { success = true, etag = reply.ETag, redirect = $"/Lots/Details?lotId={id:D}" });
    }

    // Durum Değiştir (K-E): the dialog's key + If-Match; Blocked needs the acknowledgement; reason ≥ 10.
    [HttpPost("status/{lotId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(string lotId, [FromBody] LotStatusInput? input, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(TracePermissionKeys.LotsChangeStatus)) return Refuse(403, null, null);
        if (input is null || !Guid.TryParse(lotId, out var id) || !Guid.TryParse(input.IdempotencyKey, out var key)
            || string.IsNullOrWhiteSpace(input.IfMatch) || !TraceListAdapter.LotStatuses.Contains(input.ToStatus ?? string.Empty, StringComparer.Ordinal))
            return Refuse(400, "INVALID_REQUEST", null);
        if ((input.Reason?.Trim().Length ?? 0) < LotFormRules.MinReasonLength) return FieldErrors(new Dictionary<string, string> { ["reason"] = "Validation.ReasonMin" });
        var trace = Trace();
        if (trace is null) return Refuse(401, null, null);

        var idempotencyKey = "lot-status-" + key.ToString("N");
        var reply = await trace.PostAsync($"/api/trace/lots/{id:D}/status", new
        {
            idempotencyKey,
            toStatus = input.ToStatus,
            reason = input.Reason!.Trim(),
            acknowledgeImpact = input.AcknowledgeImpact == true
        }, idempotencyKey, input.IfMatch, cancellationToken);
        if (!reply.IsSuccess) return WriteRefusal(reply);
        if (reply.StatusCode == StatusCodes.Status202Accepted)
        {
            // MOD-0175 KQ-I (TRACE 1.2.0 x-lot-release-gate, pack §11.4): Draft/Blocked → Released waits for a quality approval.
            // The lot is unchanged; pendingRequestId is the QUARANTINE decision (LotRelease, Draft) the requester signs and sends.
            var pendingRequestId = ReadPendingRequestId(reply.Body);
            return Json(new
            {
                success = true,
                pending = true,
                pendingRequestId,
                submitUrl = pendingRequestId is null ? null : $"/Quarantine/Decisions/{Uri.EscapeDataString(pendingRequestId)}/Submit"
            });
        }

        var lot = reply.Read<LotRefDto>();
        return Json(new { success = true, status = lot?.Status, etag = reply.ETag });
    }

    // ── pickers (the lot form: product → SKUs of the product; W-UI-01 Model A selectors, as FU01) ────────────

    [HttpGet("lookups/{kind}")]
    public async Task<IActionResult> Lookups(string kind, [FromQuery] string? q, [FromQuery] string? itemId, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(TracePermissionKeys.LotsRead) && !_permissions.Has(TracePermissionKeys.LotsCreate)) return Refuse(403, null, null);
        var ledger = Ledger();
        if (ledger is null) return Refuse(401, null, null);
        var source = new ProductPickerSource(ledger);
        PickerResult result = kind switch
        {
            "products" => await source.SearchProductsAsync(q, cancellationToken),
            "skus" when Guid.TryParse(itemId, out var item) => await source.SearchSkusOfProductAsync(item.ToString("D"), q, cancellationToken),
            "skus" => await source.SearchSkusAsync(q, cancellationToken),
            _ => PickerResult.Unavailable
        };
        if (kind is not ("products" or "skus")) return NotFound();
        return Json(new { items = result.Items.Select(o => new { value = o.Value, text = o.Text }), available = result.Available });
    }

    // ── the Stock Ledger lot field (pack §11.4) ───────────────────────────────────────────────────────────

    // The registered lots of the SKU, Blocked / Expired disabled with the reason (CT-D4), retest flagged (K-F).
    [HttpGet("form/lots")]
    public async Task<IActionResult> FormLots([FromQuery] string? sku, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(TracePermissionKeys.LotsRead)) return Json(new { items = Array.Empty<object>(), available = false, message = _field["Lot.Unavailable"].Value });
        if (!LotFormRules.TryParseSku(sku, out var level, out var skuId)) return Json(new { items = Array.Empty<object>(), available = true });
        var trace = Trace();
        if (trace is null) return Refuse(401, null, null);
        var reply = await trace.GetAsync($"/api/trace/lots?skuId={skuId}&skuLevel={level}&page=1&pageSize=200&sort=expiryDate", cancellationToken);
        if (!reply.IsSuccess) return Json(new { items = Array.Empty<object>(), available = false, message = _field["Lot.Unavailable"].Value });
        var options = LotFieldRules.PickerOptions(reply.Read<LotListDto>()?.Items ?? [], Today());
        return Json(new { items = options.Select(o => OptionJson(o)), available = true });
    }

    // Live duplicate check of the lot form (pack §11.3: "Bu ürün için bu lot numarası zaten var").
    [HttpGet("form/duplicate")]
    public async Task<IActionResult> Duplicate([FromQuery] string? sku, [FromQuery] string? lotNumber, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(TracePermissionKeys.LotsRead)) return Json(new { duplicate = false });
        if (!LotFormRules.TryParseSku(sku, out var level, out var skuId) || string.IsNullOrWhiteSpace(lotNumber)) return Json(new { duplicate = false });
        var number = LotFormRules.NormalizeLotNumber(lotNumber);
        if (!LotFormRules.IsLotNumber(number)) return Json(new { duplicate = false });
        var trace = Trace();
        if (trace is null) return Refuse(401, null, null);
        var reply = await trace.GetAsync($"/api/trace/lots?skuId={skuId}&skuLevel={level}&q={Uri.EscapeDataString(number)}&page=1&pageSize=50", cancellationToken);
        var duplicate = reply.IsSuccess && (reply.Read<LotListDto>()?.Items ?? []).Any(l => string.Equals(l.LotNumber, number, StringComparison.Ordinal));
        return Json(new { duplicate, message = duplicate ? _localizer["Form.Duplicate"].Value : null });
    }

    // K-B "Barkod (karekod)": the four AIs checked by the service; GTIN-null → skipped + the warning, lot / SKT always.
    [HttpPost("form/gs1")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Gs1([FromBody] Gs1Input? input, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(TracePermissionKeys.LotsRead)) return Refuse(403, null, null);
        if (input is null || string.IsNullOrWhiteSpace(input.Code) || input.Code.Length > 200
            || !LotFormRules.TryParseSku(input.Sku, out var level, out var skuId) || !Guid.TryParse(input.LotId, out var lotId))
            return Json(new { accepted = false, messages = new[] { _field["Serial.Unreadable"].Value } });
        var trace = Trace();
        if (trace is null) return Refuse(401, null, null);
        var reply = await trace.PostAsync("/api/trace/serials/gs1-check", new { code = input.Code.Trim(), skuId, skuLevel = level, lotId = lotId.ToString("D") }, null, null, cancellationToken);
        if (!reply.IsSuccess)
            return Json(new { accepted = false, messages = new[] { _field[reply.Error?.Code == "GS1_UNREADABLE" ? "Serial.Unreadable" : "Serial.CheckUnavailable"].Value } });
        var answer = LotFieldRules.Gs1(reply.Read<Gs1CheckResultDto>() ?? new Gs1CheckResultDto());
        return Json(new
        {
            accepted = answer.Accepted,
            serialNumber = answer.SerialNumber,
            gtinSkipped = answer.GtinSkipped,
            warning = answer.WarningKey is null ? null : _field[answer.WarningKey].Value,
            messages = answer.MismatchKeys.Select(k => _field[k].Value)
        });
    }

    // K-B "Aralık": expanded HERE (no arithmetic in the browser — the FU01 UI-13 rule).
    [HttpGet("form/range")]
    public IActionResult Range([FromQuery] string? start, [FromQuery] string? end)
    {
        if (!_permissions.Has(TracePermissionKeys.MovementsCreate)) return Refuse(403, null, null);
        var (serials, error) = LotFieldRules.ExpandRange(start, end);
        return Json(new { serials, error = error is null ? null : _field[error].Value });
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────────

    private object OptionJson(LotPickerOption o) => new
    {
        value = o.Value,
        lotNumber = o.LotNumber,
        expiryDate = o.ExpiryDate,
        status = o.Status,
        statusText = _localizer["Status." + o.Status].Value,
        disabled = o.Disabled,
        disabledReason = o.DisabledReasonKey is null ? null : _field[o.DisabledReasonKey].Value,
        retestDue = o.RetestDue,
        retestDate = o.RetestDate
    };

    private static void AddAttributes(Dictionary<string, object?> body, LotFormInput form, bool includeNulls)
    {
        void Put(string name, string? value)
        {
            var trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            if (trimmed is not null || includeNulls) body[name] = trimmed;
        }

        Put("manufacturerLot", form.ManufacturerLot);
        Put("supplierLot", form.SupplierLot);
        Put("manufacturingDate", LotFormRules.Wire(form.ManufacturingDate));
        Put("expiryDate", LotFormRules.Wire(form.ExpiryDate));
        Put("retestDate", LotFormRules.Wire(form.RetestDate));
        Put("countryOfOrigin", form.CountryOfOrigin?.Trim().ToUpperInvariant());
        Put("coaReference", form.CoaReference);
    }

    private object Row(LotRefDto l, MasterLabels labels) => new
    {
        id = l.LotId,
        lotNumber = l.LotNumber,
        itemId = l.ItemId,
        product = labels.Product(l.ItemId),
        sku = labels.Sku(l.SkuId),
        skuId = l.SkuId,
        skuLevel = l.SkuLevel,
        manufacturerLot = l.ManufacturerLot,
        supplierLot = l.SupplierLot,
        manufacturingDate = l.ManufacturingDate,
        expiryDate = l.ExpiryDate,
        expiryState = l.ExpiryState,
        retestDate = l.RetestDate,
        retestDue = l.RetestDue == true,
        countryOfOrigin = l.CountryOfOrigin,
        country = string.IsNullOrWhiteSpace(l.CountryOfOrigin) ? null : _localizer["Country." + l.CountryOfOrigin].Value,
        status = l.Status,
        coaReference = l.CoaReference,
        onHand = l.StockSummary?.OnHand,
        baseUomId = l.StockSummary?.BaseUomId,
        trackingMode = l.TrackingMode,
        version = l.Version
    };

    private static object HistoryRows(LotHistoryPageDto? page) => new
    {
        items = (page?.Items ?? []).Select(h => new
        {
            h.Action, h.Field, h.OldValue, h.NewValue, h.Reason, h.ActorDisplay, h.ActorUserId, h.OccurredAt, h.Source, h.CorrelationId
        }),
        total = page?.Total ?? 0
    };

    private sealed record BalanceRow(string? WarehouseId, string? LocationId, string? StockStatus, string? OnHand, string? BaseUomId);

    private static List<BalanceRow> ReadRows(string body)
    {
        var rows = new List<BalanceRow>();
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("rows", out var list) || list.ValueKind != JsonValueKind.Array) return rows;
            foreach (var r in list.EnumerateArray())
                rows.Add(new BalanceRow(Text(r, "warehouseId"), Text(r, "locationId"), Text(r, "stockStatus"), Text(r, "onHand"), Text(r, "baseUomId")));
        }
        catch (JsonException)
        {
            // read path: no rows
        }

        return rows;
    }

    private static string? Text(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>TRACE 202 PendingApproval body <c>{ lotId, pendingRequestId, contractVersion }</c> → the decision id.</summary>
    private static string? ReadPendingRequestId(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var id = Text(doc.RootElement, "pendingRequestId");
            return Guid.TryParse(id, out var guid) ? guid.ToString("D") : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool Positive(string? quantity) =>
        decimal.TryParse(quantity, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var q) && q > 0m;

    private async Task<MasterLabels> Labels(IReadOnlyList<LotRefDto> lots, CancellationToken ct)
    {
        var ledger = Ledger();
        if (ledger is null) return MasterLabels.None;
        return await TraceLabels.ResolveAsync(ledger, lots.Select(l => l.ItemId), lots.Select(l => (l.SkuId, l.SkuLevel)), [], ct);
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    private TraceGatewayClient? Trace() => TraceGatewayClient.For(_httpClient, _gatewayUrl, Request, User);

    private InventoryGatewayClient? Ledger() => InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);

    private IActionResult FieldErrors(IReadOnlyDictionary<string, string> errors) =>
        new JsonResult(new
        {
            error = new { code = "INVALID_REQUEST", message = _localizer["Validation.FixFields"].Value, scope = "field" },
            fields = errors.ToDictionary(e => e.Key, e => _localizer[e.Value].Value),
            contractVersion = InventoryProxy.ContractVersion
        })
        { StatusCode = 400 };

    /// <summary>A TRACE refusal of a write, placed on its field where it has one (pack §13), else on the form.</summary>
    private IActionResult WriteRefusal(GatewayReply reply)
    {
        var code = reply.Error?.Code;
        var field = code switch
        {
            "LOT_ALREADY_EXISTS" or "LOT_NUMBER_INVALID" => "lotNumber",
            "LOT_DATES_INVALID" => "expiryDate",
            "REASON_REQUIRED" => "reason",
            "PRODUCT_NOT_LOT_CONTROLLED" or "UNKNOWN_ITEM" or "LOT_SKU_REQUIRED" or "LOT_SKU_MISMATCH" => "sku",
            "ACKNOWLEDGEMENT_REQUIRED" => "acknowledge",
            _ => null
        };
        _logger.LogWarning("Lot write refused with {Status} {Code} (correlation {CorrelationId}).", reply.StatusCode, code, reply.Error?.CorrelationId ?? reply.CorrelationId);
        return new JsonResult(new
        {
            error = new
            {
                code = code ?? "ERROR",
                message = _localizer[TraceErrorMessages.KeyFor(code, reply.StatusCode)].Value,
                scope = field is null ? (reply.StatusCode is 0 or 502 or 503 or 504 ? "page" : "form") : "field",
                field,
                correlationId = reply.Error?.CorrelationId ?? reply.CorrelationId
            },
            contractVersion = InventoryProxy.ContractVersion
        })
        { StatusCode = reply.StatusCode == 0 ? 503 : reply.StatusCode };
    }

    private IActionResult Failed(string operation, GatewayReply reply)
    {
        _logger.LogWarning("TRACE {Operation} failed with {Status} (correlation {CorrelationId}).", operation, reply.StatusCode, reply.Error?.CorrelationId ?? reply.CorrelationId);
        return Refuse(reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
    }

    private IActionResult Refuse(int status, string? code, string? correlationId) =>
        TraceErrorMessages.Refuse(status, code, _localizer[TraceErrorMessages.KeyFor(code, status)].Value, correlationId);
}

/// <summary>The status dialog's post (lots/status.js).</summary>
public sealed record LotStatusInput(string? IdempotencyKey, string? ToStatus, string? Reason, bool? AcknowledgeImpact, string? IfMatch);

/// <summary>The serial panel's scanned code (form.js "Barkod").</summary>
public sealed record Gs1Input(string? Code, string? Sku, string? LotId);
