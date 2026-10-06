using System.Text.RegularExpressions;
using Diten.Web.Models.Inventory;
using Diten.Web.Models.Traceability;
using Diten.Web.Services;
using Diten.Web.Views.Inventory.StockMovements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0173-FU01 S-2 Movement history + S-3 New movement — tenant shell MVC controller (UI-1, UI-2).
// Route is single-segment /StockMovements; views live under Views/Inventory/StockMovements/ (pack §9).
// UI-1 surface: the history list (server mode, proxy profile → Gateway GET /api/inventory/transactions), the
// picker proxies (W-UI-01 Model A products/SKUs, LOCATION warehouses/locations) and Reversal (POST
// reverse/{transactionId} → Gateway POST /api/inventory/movements as a REVERSAL, parent ASSUMPTION-0173-05).
// UI-2 surface (pack §11/§12/§19): GET Create (S-3), GET Edit ("Correct", KD-1), GET Details (the receipt, PRG) and
// their JSON posts — the ONLY stock-changing path is still Gateway POST /api/inventory/movements: a correction is a
// REVERSAL plus a new movement (two immutable records, two keys), never an edit. The form's lookups are contract
// reads (PRODUCT-MASTER, /balance, LOCATION) plus the W-UI-01 Model A selectors.
[Authorize]
[Route("StockMovements")]
public sealed partial class StockMovementsController : Controller
{
    private const string ViewRoot = "~/Views/Inventory/StockMovements";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<StockMovementsIndex> _localizer;
    private readonly ILogger<StockMovementsController> _logger;
    private readonly IStringLocalizer<Diten.Web.Views.Traceability.Lots.LotFieldIndex>? _lotField;

    public StockMovementsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IPermissionSnapshot permissions,
        IStringLocalizer<StockMovementsIndex> localizer,
        ILogger<StockMovementsController> logger,
        IStringLocalizer<Diten.Web.Views.Traceability.Lots.LotFieldIndex>? lotField = null)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
        _lotField = lotField;
    }

    // UAS-001: the view draws _AccessDenied (and nothing else) without inventory.stock.read — no redirect.
    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    // S-3 (pack §11). The idempotency key is minted HERE, once per opened form (pack §4/§8); the S-1 row "Movement"
    // pre-fills a location transfer from that row (UI-11). UAS-001 on inventory.movements.create is the view's.
    [HttpGet("Create")]
    public IActionResult Create() => View($"{ViewRoot}/Create.cshtml", StockMovementFormViewModel.ForCreate(Request.Query));

    // "Correct" (KD-1, pack §19): the history row in the address (BG-06), two keys minted here — reversal + movement.
    [HttpGet("Edit")]
    public IActionResult Edit() => View($"{ViewRoot}/Edit.cshtml", StockMovementFormViewModel.ForCorrection(Request.Query));

    // The receipt of the movement just posted (PRG via TempData, UI-10) — not addressable by id later (BG-06).
    [HttpGet("Details")]
    public IActionResult Details() => View($"{ViewRoot}/Details.cshtml", MovementReceiptViewModel.From(TempData));

    // S-3 / StockLedger Create post (JSON — the form is a script-driven direction matrix with live pickers). The
    // shape is checked here (pack §12 matrix); MOD-0173 decides everything else and its refusal comes back to the
    // field it belongs to. The key is the FORM's (minted on GET): a double submit replays, never posts twice.
    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostMovement([FromBody] MovementSubmitViewModel? model, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(InventoryPermissionKeys.CreateMovements))
            return Refuse(StatusCodes.Status403Forbidden, null, null);
        if (model is null || !Guid.TryParse(model.IdempotencyKey, out var key))
            return Refuse(StatusCodes.Status400BadRequest, InventoryListAdapter.InvalidFilter, null);

        var check = MovementRules.Check(model, model.Entry ? MovementRules.EntryTypes : MovementRules.ManualTypes, MovementKey(key));
        if (!check.IsValid)
            return FieldErrors(check.Errors);
        if (await RetestRefusalAsync(model, cancellationToken) is { } retest)
            return retest;

        var gateway = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null)
            return Refuse(StatusCodes.Status401Unauthorized, null, null);

        var reply = await gateway.PostAsync("/api/inventory/movements", check.Payload!, MovementKey(key), cancellationToken);
        if (!reply.IsSuccess)
            return MovementRefusal(reply, model, check.Source, null);

        var posted = reply.Read<PostedMovementDto>() ?? new PostedMovementDto();
        var receipt = await ReceiptAsync(gateway, model, check, posted, cancellationToken);
        receipt.FromEntry = model.Entry;
        receipt.Store(TempData);
        return Json(new { success = true, redirect = "/StockMovements/Details", idempotentReplay = posted.IdempotentReplay, transactionNumber = posted.TransactionNumber });
    }

    // "Correct" (KD-1): (1) REVERSAL of the original, (2) the corrected movement — two immutable records, two keys.
    // The form is checked BEFORE step (1), so a malformed correction reverses nothing. If (2) is refused after (1)
    // posted, the answer says so explicitly (partial correction: the reversal number + the reason) and the page offers
    // the retry with the SAME keys: (1) then replays (same key, same fingerprint — its date was fixed on GET) and only
    // (2) is attempted again. Never a silent half-correction.
    [HttpPost("Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostCorrection([FromBody] CorrectionSubmitViewModel? model, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(InventoryPermissionKeys.CreateMovements))
            return Refuse(StatusCodes.Status403Forbidden, null, null);

        var movement = model?.Movement;
        if (model is null || movement is null
            || !Guid.TryParse(model.TransactionId, out var originalId)
            || !Guid.TryParse(model.ReversalKey, out var reversalKey)
            || !Guid.TryParse(movement.IdempotencyKey, out var movementKey)
            || reversalKey == movementKey
            || !MovementRules.TryUtc(model.ReversalAt, out var reversalAt)
            || !Guid.TryParse(model.OriginalItemId, out var originalItem)
            || !Guid.TryParse(model.OriginalSkuId, out var originalSku)
            || !InventoryListAdapter.SkuLevels.Contains(model.OriginalSkuLevel ?? string.Empty, StringComparer.Ordinal)
            || !MovementRules.IsQuantity(model.OriginalQuantity)
            || string.IsNullOrWhiteSpace(model.OriginalUomId))
            return Refuse(StatusCodes.Status400BadRequest, InventoryListAdapter.InvalidFilter, null);

        var check = MovementRules.Check(movement, MovementRules.ManualTypes, MovementKey(movementKey));
        if (!check.IsValid)
            return FieldErrors(check.Errors);
        if (await RetestRefusalAsync(movement, cancellationToken) is { } retest)
            return retest;

        var gateway = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null)
            return Refuse(StatusCodes.Status401Unauthorized, null, null);

        // (1) the reversal — the original's identity as the row showed it; MOD-0173 re-checks it against the original.
        var reversalPayload = new
        {
            idempotencyKey = ReversalKeyOf(reversalKey),
            movementType = InventoryListAdapter.ReversalMovementType,
            itemId = originalItem.ToString("D"),
            skuId = originalSku.ToString("D"),
            skuLevel = model.OriginalSkuLevel,
            quantity = model.OriginalQuantity,
            uomId = model.OriginalUomId!.Trim(),
            sourceModule = InventoryListAdapter.ManualSourceModule,
            sourceType = InventoryListAdapter.ReversalMovementType,
            sourceDocumentId = originalId.ToString("D"),
            transactionDate = reversalAt,
            postingDate = reversalAt
        };
        var first = await gateway.PostAsync("/api/inventory/movements", reversalPayload, ReversalKeyOf(reversalKey), cancellationToken);
        if (!first.IsSuccess)
        {
            _logger.LogWarning("Correction of {TransactionId}: reversal refused with {Status} {Code} (correlation {CorrelationId}).",
                originalId, first.StatusCode, first.Error?.Code, first.Error?.CorrelationId ?? first.CorrelationId);
            // Nothing was posted: the reason is about the ORIGINAL (already reversed, stock already used…), not a field.
            return new JsonResult(new
            {
                step = "reversal",
                error = new
                {
                    code = first.Error?.Code ?? "ERROR",
                    message = _localizer[InventoryErrorMessages.KeyFor(first.Error?.Code, first.StatusCode)].Value,
                    scope = first.StatusCode is 0 or 502 or 503 or 504 ? MovementErrorFields.ScopePage : MovementErrorFields.ScopeForm,
                    correlationId = first.Error?.CorrelationId ?? first.CorrelationId
                },
                contractVersion = InventoryProxy.ContractVersion
            })
            { StatusCode = first.StatusCode == 0 ? StatusCodes.Status503ServiceUnavailable : first.StatusCode };
        }

        var reversal = first.Read<PostedMovementDto>() ?? new PostedMovementDto();

        // (2) the corrected movement, under its own key.
        var second = await gateway.PostAsync("/api/inventory/movements", check.Payload!, MovementKey(movementKey), cancellationToken);
        if (!second.IsSuccess)
        {
            _logger.LogWarning("Correction of {TransactionId}: reversal {Reversal} posted, corrected movement refused with {Status} {Code} (correlation {CorrelationId}).",
                originalId, reversal.TransactionNumber, second.StatusCode, second.Error?.Code, second.Error?.CorrelationId ?? second.CorrelationId);
            return MovementRefusal(second, movement, check.Source, reversal.TransactionNumber);
        }

        var posted = second.Read<PostedMovementDto>() ?? new PostedMovementDto();
        var receipt = await ReceiptAsync(gateway, movement, check, posted, cancellationToken);
        receipt.ReversalNumber = reversal.TransactionNumber;
        receipt.CorrectedNumber = model.TransactionNumber;
        receipt.Store(TempData);
        return Json(new { success = true, redirect = "/StockMovements/Details", reversalNumber = reversal.TransactionNumber, transactionNumber = posted.TransactionNumber });
    }

    // List adapter: repo server-mode wire → INVENTORY-BUNDLE /transactions (pack §8). Errors keep the contract shape.
    [HttpGet("api")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!_permissions.Has(InventoryPermissionKeys.Read))
            return Refuse(StatusCodes.Status403Forbidden, null, null);

        var translation = InventoryListAdapter.TransactionsQuery(Request.Query);
        if (!translation.IsValid)
            return Refuse(StatusCodes.Status400BadRequest, translation.ErrorCode, null);

        var gateway = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null)
            return Refuse(StatusCodes.Status401Unauthorized, null, null);

        var reply = await gateway.GetAsync("/api/inventory/transactions" + translation.Query, cancellationToken);
        if (!reply.IsSuccess)
        {
            _logger.LogWarning("Inventory transaction list failed with {Status} (correlation {CorrelationId}).", reply.StatusCode, reply.CorrelationId);
            return Refuse(reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
        }

        var page = reply.Read<TransactionPageDto>() ?? new TransactionPageDto();
        var items = page.Items ?? [];
        var labels = await MasterLabelResolver.ResolveAsync(
            gateway,
            items.Select(t => t.ItemId),
            items.Select(t => (t.SkuId, t.SkuLevel)),
            items.SelectMany(t => new[] { t.ToWarehouseId, t.ToLocationId }),
            cancellationToken);

        var total = page.Total ?? items.Count;
        return Json(new
        {
            data = new
            {
                items = InventoryListAdapter.MapTransactions(items, labels),
                total,
                filteredTotal = total
            },
            labelsAvailable = labels.Available,
            contractVersion = page.ContractVersion ?? InventoryProxy.ContractVersion
        });
    }

    // Picker proxies (pack §19). products / skus = W-UI-01 Model A selectors behind ProductPickerSource; warehouses /
    // locations = LOCATION v1 (BG-10: no Gateway route yet → "unavailable"). Never a hardcoded list.
    [HttpGet("lookups/{kind}")]
    public async Task<IActionResult> Lookups(string kind, [FromQuery] string? q, [FromQuery] string? warehouseId, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(InventoryPermissionKeys.Read))
            return Refuse(StatusCodes.Status403Forbidden, null, null);

        var gateway = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null)
            return Refuse(StatusCodes.Status401Unauthorized, null, null);

        PickerResult result;
        switch (kind)
        {
            case "products":
                result = await new ProductPickerSource(gateway).SearchProductsAsync(q, cancellationToken);
                break;
            case "skus":
                // S-3 (UI-2): with a product the list is that product's SKUs only (pack §12 field 4); the S-1 filter
                // keeps its free search across products.
                result = Guid.TryParse(Request.Query["itemId"], out var product)
                    ? await new ProductPickerSource(gateway).SearchSkusOfProductAsync(product.ToString("D"), q, cancellationToken)
                    : await new ProductPickerSource(gateway).SearchSkusAsync(q, cancellationToken);
                break;
            case "targetwarehouses":
                result = await new LocationPickerSource(gateway).ActiveWarehousesAsync(cancellationToken);
                break;
            case "targetlocations":
                if (string.IsNullOrWhiteSpace(warehouseId))
                    return Json(new { items = Array.Empty<PickerOption>(), available = true });
                result = await new LocationPickerSource(gateway).StockableLocationsUnderAsync(warehouseId, q, cancellationToken);
                break;
            case "sources":
            case "lots":
            case "product":
            case "effect":
                return await FormLookupAsync(kind, gateway, cancellationToken);
            case "warehouses":
                result = await new LocationPickerSource(gateway).WarehousesAsync(cancellationToken);
                break;
            case "locations":
                if (string.IsNullOrWhiteSpace(warehouseId))
                    return Json(new { items = Array.Empty<PickerOption>(), available = true });
                result = await new LocationPickerSource(gateway).LocationsAsync(warehouseId, cancellationToken);
                break;
            default:
                return NotFound();
        }

        return Json(new
        {
            items = result.Items.Select(o => new { value = o.Value, text = o.Text }),
            available = result.Available,
            message = result.Available ? null : _localizer["PickerUnavailable"].Value
        });
    }

    // Reversal (UI-09, parent ASSUMPTION-0173-05): a NEW immutable REVERSAL movement — never an edit. The original's
    // identity comes from the row (BG-06: no GET /transactions/{id}); MOD-0173 re-checks it against the original
    // and refuses any mismatch, so this proxy cannot reverse something other than what the row showed.
    [HttpPost("reverse/{transactionId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reverse(string transactionId, [FromBody] ReversalRequestViewModel? model, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(InventoryPermissionKeys.CreateMovements))
            return Refuse(StatusCodes.Status403Forbidden, null, null);

        if (model is null
            || !Guid.TryParse(transactionId, out var originalId)
            || !Guid.TryParse(model.IdempotencyKey, out var key)
            || !Guid.TryParse(model.ItemId, out var itemId)
            || !Guid.TryParse(model.SkuId, out var skuId)
            || !InventoryListAdapter.SkuLevels.Contains(model.SkuLevel ?? string.Empty, StringComparer.Ordinal)
            || model.Quantity is null || !QuantityPattern().IsMatch(model.Quantity)
            || string.IsNullOrWhiteSpace(model.UomId)
            // FU-UI2-05: the instant comes WITH the key (fixed when the dialog opened), so a re-send of the same dialog is
            // the same request — a replay — and not IDEMPOTENCY_KEY_REUSED (the service fingerprints the dates).
            || !MovementRules.TryDialogInstant(model.ReversalAt, DateTimeOffset.UtcNow, out var reversalAt))
            return Refuse(StatusCodes.Status400BadRequest, InventoryListAdapter.InvalidFilter, null);

        var gateway = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null)
            return Refuse(StatusCodes.Status401Unauthorized, null, null);

        var idempotencyKey = "reversal-" + key.ToString("N");
        var now = reversalAt;
        var payload = new
        {
            idempotencyKey,
            movementType = InventoryListAdapter.ReversalMovementType,
            itemId = itemId.ToString("D"),
            skuId = skuId.ToString("D"),
            skuLevel = model.SkuLevel,
            quantity = model.Quantity,
            uomId = model.UomId.Trim(),
            sourceModule = InventoryListAdapter.ManualSourceModule,
            sourceType = InventoryListAdapter.ReversalMovementType,
            sourceDocumentId = originalId.ToString("D"),
            transactionDate = now,
            postingDate = now
        };

        var reply = await gateway.PostAsync("/api/inventory/movements", payload, idempotencyKey, cancellationToken);
        if (!reply.IsSuccess)
        {
            _logger.LogWarning("Reversal of {TransactionId} refused with {Status} {Code} (correlation {CorrelationId}).",
                originalId, reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
            return Refuse(reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
        }

        var result = reply.Read<MovementResponseDto>() ?? new MovementResponseDto();
        return Json(new
        {
            success = true,
            transactionId = result.TransactionId,
            transactionNumber = result.TransactionNumber,
            idempotentReplay = result.IdempotentReplay,
            message = string.Format(_localizer["ReversalPosted"].Value, result.TransactionNumber)
        });
    }

    // ── UI-2 form lookups (pack §19): sources / lots from /balance, product facts (lot · serial · units) from
    //    PRODUCT-MASTER, and the stock-effect panel (before → after, decimal, informational only — pack §12). ────────
    private async Task<IActionResult> FormLookupAsync(string kind, InventoryGatewayClient gateway, CancellationToken cancellationToken)
    {
        var query = Request.Query;
        if (!InventoryListAdapter.TryParseSku(query["sku"].ToString(), out var skuLevel, out var skuId))
            return Json(new { items = Array.Empty<object>(), available = true });

        switch (kind)
        {
            case "sources":
            {
                var (options, available) = await MovementFormSources.SourcesAsync(gateway, skuId, skuLevel, cancellationToken);
                return Json(new
                {
                    items = options.Select(o => new
                    {
                        value = o.Value, text = o.Text, warehouseId = o.WarehouseId, locationId = o.LocationId,
                        lotId = o.LotId, stockStatus = o.StockStatus, onHand = o.OnHand, baseUomId = o.BaseUomId
                    }),
                    available,
                    message = available ? null : _localizer["PickerUnavailable"].Value
                });
            }

            case "lots":
            {
                var (lots, available) = await MovementFormSources.KnownLotsAsync(gateway, skuId, skuLevel, cancellationToken);
                return Json(new { items = lots.Select(l => new { value = l, text = l }), available, message = available ? null : _localizer["PickerUnavailable"].Value });
            }

            case "product":
            {
                if (!Guid.TryParse(query["itemId"], out var item))
                    return Json(new { available = false });
                var facts = await MovementFormSources.ProductFactsAsync(gateway, item.ToString("D"), skuId, cancellationToken);
                var quantity = query["quantity"].ToString();
                var uom = query["uomId"].ToString();
                string? baseQuantity = null;
                if (facts.Available && MovementRules.IsQuantity(quantity) && uom.Length > 0)
                    baseQuantity = MovementFormSources.ToBase(quantity, uom, facts);
                return Json(new
                {
                    available = facts.Available,
                    message = facts.Available ? null : _localizer["ProductFactsUnavailable"].Value,
                    lotControlled = facts.LotControlled,
                    serialControlled = facts.SerialControlled,
                    baseUomId = facts.BaseUomId,
                    units = facts.Units,
                    baseQuantity,
                    convertible = baseQuantity is not null
                });
            }

            default: // effect
            {
                if (!Guid.TryParse(query["itemId"], out var item))
                    return Json(new { sides = Array.Empty<object>() });
                var direction = MovementRules.DirectionOf(query["type"].ToString());
                var quantity = query["quantity"].ToString();
                var uom = query["uomId"].ToString();
                if (direction is null || !MovementRules.IsQuantity(quantity) || uom.Length == 0)
                    return Json(new { sides = Array.Empty<object>() });

                var facts = await MovementFormSources.ProductFactsAsync(gateway, item.ToString("D"), skuId, cancellationToken);
                var baseQuantity = facts.Available ? MovementFormSources.ToBase(quantity, uom, facts) : null;
                if (baseQuantity is null)
                    return Json(new { sides = Array.Empty<object>(), baseUomId = facts.BaseUomId });

                MovementRules.SourceRow.TryParse(query["source"].ToString(), out var source);
                (string, string, string, string?)? target = null;
                var toWarehouse = query["toWarehouseId"].ToString();
                var toLocation = query["toLocationId"].ToString();
                if (direction == MovementRules.Direction.Inbound && toWarehouse.Length > 0 && toLocation.Length > 0)
                {
                    var status = query["toStockStatus"].ToString();
                    MovementRules.TryNormalizeLot(query["lotId"].ToString(), out var lot);
                    target = (toWarehouse, toLocation, MovementRules.InboundStatuses.Contains(status) ? status : "AVAILABLE", lot);
                }
                else if (direction is MovementRules.Direction.WarehouseTransfer or MovementRules.Direction.LocationTransfer && source is not null && toLocation.Length > 0)
                {
                    var warehouse = direction == MovementRules.Direction.LocationTransfer ? source.WarehouseId : toWarehouse;
                    if (warehouse.Length > 0) target = (warehouse, toLocation, source.StockStatus, source.LotId);
                }

                var sides = await MovementFormSources.EffectAsync(gateway, skuId, skuLevel, baseQuantity,
                    direction == MovementRules.Direction.Inbound ? null : source, target, cancellationToken);
                return Json(new { sides, baseQuantity, baseUomId = facts.BaseUomId });
            }
        }
    }

    private static string MovementKey(Guid key) => "movement-" + key.ToString("N");

    // The same format as the S-2 Reversal (UI1-03), so a reversal made here and one made there of the same row are
    // two keys and the second one gets ALREADY_REVERSED — never a second reversal.
    private static string ReversalKeyOf(Guid key) => "reversal-" + key.ToString("N");

    /// <summary>The MVC's own shape refusal: every field it could not accept, each with its sentence — nothing was posted.</summary>
    /// <summary>
    /// FU-0174-P3-07 (MOD-0174 K-F, ASSUMPTION-0174-06): an INBOUND movement into a registered lot whose retest date has
    /// passed needs the form's explicit confirmation (header <see cref="LotRetestGate.Header"/>) — refused on the lot field
    /// otherwise, before anything reaches the Gateway (the frozen movement request has no field for it).
    /// </summary>
    private async Task<IActionResult?> RetestRefusalAsync(MovementSubmitViewModel movement, CancellationToken cancellationToken)
    {
        if (MovementRules.DirectionOf(movement.MovementType) != MovementRules.Direction.Inbound) return null;
        if (await LotRetestGate.AllowsAsync(movement.LotId?.Trim(), LotRetestGate.Confirmed(Request), _httpClient, _gatewayUrl, Request, User,
                _permissions, cancellationToken))
            return null;

        var message = _lotField?[LotRetestGate.ErrorKey].Value ?? LotRetestGate.ErrorKey;
        return new JsonResult(new
        {
            error = new { code = InventoryListAdapter.InvalidFilter, message, scope = MovementErrorFields.ScopeField },
            fields = new Dictionary<string, string> { [MovementRules.Fields.Lot] = message },
            contractVersion = InventoryProxy.ContractVersion
        })
        { StatusCode = StatusCodes.Status400BadRequest };
    }

    private IActionResult FieldErrors(IReadOnlyDictionary<string, string> errors) =>
        new JsonResult(new
        {
            error = new
            {
                code = InventoryListAdapter.InvalidFilter,
                message = _localizer["ValFixFields"].Value,
                scope = MovementErrorFields.ScopeField
            },
            fields = errors.ToDictionary(e => e.Key, e => _localizer[e.Value].Value),
            contractVersion = InventoryProxy.ContractVersion
        })
        { StatusCode = StatusCodes.Status400BadRequest };

    /// <summary>
    /// A MOD-0173 refusal of a movement, placed where it belongs (field / form / page / restart — pack §13). With a
    /// <paramref name="reversalNumber"/> it is the partial-correction answer: the reversal stands, this movement did not.
    /// </summary>
    private IActionResult MovementRefusal(GatewayReply reply, MovementSubmitViewModel model, MovementRules.SourceRow? source, string? reversalNumber)
    {
        var code = reply.Error?.Code;
        var details = reply.Error?.Details;
        var (scope, field) = MovementErrorFields.Place(code, reply.StatusCode, details, MovementRules.DirectionOf(model.MovementType), source);
        var message = _localizer[InventoryErrorMessages.KeyFor(code, reply.StatusCode)].Value;
        if (code == "INSUFFICIENT_STOCK")
        {
            var available = MovementErrorFields.Detail(details, "available");
            var requested = MovementErrorFields.Detail(details, "requested");
            // The contract's decimal strings ("24.000") are shown in the reader's number format: in tr "24.000" reads as
            // twenty-four thousand.
            if (available is not null && requested is not null)
                message += " " + string.Format(System.Globalization.CultureInfo.CurrentCulture, _localizer[InventoryErrorMessages.InsufficientStockAmounts].Value,
                    MovementRules.ReadableQuantity(available), MovementRules.ReadableQuantity(requested));
        }

        _logger.LogWarning("Movement refused with {Status} {Code} (correlation {CorrelationId}).", reply.StatusCode, code, reply.Error?.CorrelationId ?? reply.CorrelationId);
        return new JsonResult(new
        {
            partial = reversalNumber is not null,
            reversalNumber,
            error = new { code = code ?? "ERROR", message, scope, field, correlationId = reply.Error?.CorrelationId ?? reply.CorrelationId },
            contractVersion = InventoryProxy.ContractVersion
        })
        { StatusCode = reply.StatusCode == 0 ? StatusCodes.Status503ServiceUnavailable : reply.StatusCode };
    }

    /// <summary>The receipt (UI-10): what was posted, the service's number and the balance row it left (balanceSnapshot).</summary>
    private static async Task<MovementReceiptViewModel> ReceiptAsync(InventoryGatewayClient gateway, MovementSubmitViewModel model,
        MovementRules.Checked check, PostedMovementDto posted, CancellationToken cancellationToken)
    {
        var payload = check.Payload!;
        string? Field(string name) => payload.TryGetValue(name, out var value) ? value as string : null;
        var snapshot = posted.BalanceSnapshot;
        var labels = await MasterLabelResolver.ResolveAsync(gateway, [], [],
            [Field("fromWarehouseId"), Field("fromLocationId"), Field("toWarehouseId"), Field("toLocationId"), snapshot?.WarehouseId, snapshot?.LocationId],
            cancellationToken);
        string? Place(string? warehouse, string? location) =>
            warehouse is null ? null : $"{labels.Location(warehouse) ?? warehouse} / {labels.Location(location) ?? location}";

        return new MovementReceiptViewModel
        {
            TransactionId = posted.TransactionId,
            TransactionNumber = posted.TransactionNumber,
            PostedAt = posted.PostedAt,
            IdempotentReplay = posted.IdempotentReplay,
            MovementType = Field("movementType"),
            ProductText = Short(model.ItemText) ?? Field("itemId"),
            SkuText = Short(model.SkuText) ?? Field("skuId"),
            Quantity = Field("quantity"),
            UomId = Field("uomId"),
            FromText = Place(Field("fromWarehouseId"), Field("fromLocationId")),
            ToText = Place(Field("toWarehouseId"), Field("toLocationId")),
            ToStockStatus = Field("toStockStatus") ?? Field("fromStockStatus"),
            LotId = Field("lotId"),
            SerialCount = payload.TryGetValue("serialIds", out var serials) && serials is IReadOnlyList<string> list ? list.Count : 0,
            DocumentNo = Field("sourceDocumentId"),
            TransactionDate = Field("transactionDate"),
            PostingDate = Field("postingDate"),
            BalanceLocationText = Place(snapshot?.WarehouseId, snapshot?.LocationId),
            BalanceLotId = snapshot?.LotId,
            BalanceStatus = snapshot?.StockStatus,
            BalanceOnHand = snapshot?.OnHand,
            BalanceAvailable = snapshot?.Available,
            BaseQuantity = snapshot?.BaseQuantity,
            BaseUomId = snapshot?.BaseUomId
        };
    }

    private static string? Short(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Length > MovementRules.MaxTextLength ? text[..MovementRules.MaxTextLength] : text.Trim();

    private IActionResult Refuse(int status, string? code, string? correlationId) =>
        InventoryProxy.Error(status, code, _localizer[InventoryErrorMessages.KeyFor(code, status)].Value, correlationId);

    [GeneratedRegex(@"^\d+(\.\d{1,6})?$")]
    private static partial Regex QuantityPattern();
}
