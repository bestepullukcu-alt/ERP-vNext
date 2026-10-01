using System.Text.RegularExpressions;
using Diten.Web.Models.Inventory;
using Diten.Web.Services;
using Diten.Web.Views.Inventory.StockMovements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0173-FU01 S-2 Movement history — tenant shell MVC controller (UI-1).
// Route is single-segment /StockMovements; views live under Views/Inventory/StockMovements/ (pack §9).
// UI-1 surface: the history list (server mode, proxy profile → Gateway GET /api/inventory/transactions), the
// picker proxies (W-UI-01 Model A products/SKUs, LOCATION warehouses/locations) and ONE state-changing action:
// Reversal (POST reverse/{transactionId} → Gateway POST /api/inventory/movements as a REVERSAL, parent
// ASSUMPTION-0173-05). Create / Details / Edit ("Correct") arrive with UI-2 and are not routed here yet.
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

    public StockMovementsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IPermissionSnapshot permissions,
        IStringLocalizer<StockMovementsIndex> localizer,
        ILogger<StockMovementsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
    }

    // UAS-001: the view draws _AccessDenied (and nothing else) without inventory.stock.read — no redirect.
    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

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
                result = await new ProductPickerSource(gateway).SearchSkusAsync(q, cancellationToken);
                break;
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
            || string.IsNullOrWhiteSpace(model.UomId))
            return Refuse(StatusCodes.Status400BadRequest, InventoryListAdapter.InvalidFilter, null);

        var gateway = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null)
            return Refuse(StatusCodes.Status401Unauthorized, null, null);

        var idempotencyKey = "reversal-" + key.ToString("N");
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture);
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

    private IActionResult Refuse(int status, string? code, string? correlationId) =>
        InventoryProxy.Error(status, code, _localizer[InventoryErrorMessages.KeyFor(code, status)].Value, correlationId);

    [GeneratedRegex(@"^\d+(\.\d{1,6})?$")]
    private static partial Regex QuantityPattern();
}
