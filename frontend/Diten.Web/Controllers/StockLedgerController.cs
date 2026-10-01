using Diten.Web.Models.Inventory;
using Diten.Web.Services;
using Diten.Web.Views.Inventory.StockLedger;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0173-FU01 S-1 Stock list — tenant shell MVC controller (UI-1).
// Route is single-segment /StockLedger; views live under Views/Inventory/StockLedger/ (pack §9 folder decision).
// The list is SERVER mode through the proxy profile (ASSUMPTION-UI-01): the browser asks /StockLedger/api, this
// controller asks Gateway GET /api/inventory/balance — one call per page — plus /availability for the KPI strip
// when a product / SKU filter is set, folded into the SAME browser response (pack §8). The browser never calls
// 5066 or /api/inventory itself. The balance is a projection: no create / edit / delete lives here (UI-2 adds
// the inbound entry page, KD-2).
[Authorize]
[Route("StockLedger")]
public sealed class StockLedgerController : Controller
{
    private const string ViewRoot = "~/Views/Inventory/StockLedger";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<StockLedgerIndex> _localizer;
    private readonly ILogger<StockLedgerController> _logger;

    public StockLedgerController(
        HttpClient httpClient,
        IConfiguration configuration,
        IPermissionSnapshot permissions,
        IStringLocalizer<StockLedgerIndex> localizer,
        ILogger<StockLedgerController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
    }

    // UAS-001: the view itself draws _AccessDenied (and nothing else) without inventory.stock.read — no redirect.
    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    // List adapter: repo server-mode wire → INVENTORY-BUNDLE /balance (pack §8). Errors keep the contract shape.
    [HttpGet("api")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!_permissions.Has(InventoryPermissionKeys.Read))
            return Refuse(StatusCodes.Status403Forbidden, null, null);

        var translation = InventoryListAdapter.BalanceQuery(Request.Query);
        if (!translation.IsValid)
            return Refuse(StatusCodes.Status400BadRequest, translation.ErrorCode, null);

        var gateway = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null)
            return Refuse(StatusCodes.Status401Unauthorized, null, null);

        var reply = await gateway.GetAsync("/api/inventory/balance" + translation.Query, cancellationToken);
        if (!reply.IsSuccess)
        {
            _logger.LogWarning("Inventory balance list failed with {Status} (correlation {CorrelationId}).", reply.StatusCode, reply.CorrelationId);
            return Refuse(reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
        }

        var page = reply.Read<BalancePageDto>() ?? new BalancePageDto();
        var rows = page.Rows ?? [];
        var labels = await MasterLabelResolver.ResolveAsync(
            gateway,
            rows.Select(r => r.ItemId),
            rows.Select(r => (r.SkuId, r.SkuLevel)),
            rows.SelectMany(r => new[] { r.WarehouseId, r.LocationId }),
            cancellationToken);

        StockLedgerSummaryViewModel? summary = null;
        string? summaryMessage = null;
        var availabilityQuery = InventoryListAdapter.AvailabilityQuery(Request.Query);
        if (availabilityQuery is not null)
        {
            var availability = await gateway.GetAsync("/api/inventory/availability" + availabilityQuery, cancellationToken);
            var view = availability.IsSuccess ? availability.Read<AvailabilityViewDto>() : null;
            if (view is not null)
                summary = InventoryListAdapter.Summary(view);
            else
                summaryMessage = _localizer[InventoryErrorMessages.KeyFor(availability.Error?.Code, availability.StatusCode)].Value;
        }

        var total = page.Total ?? rows.Count;
        return Json(new
        {
            data = new
            {
                items = InventoryListAdapter.MapBalanceRows(rows, labels),
                total,
                filteredTotal = total
            },
            summary,
            summaryMessage,
            labelsAvailable = labels.Available,
            contractVersion = page.ContractVersion ?? InventoryProxy.ContractVersion
        });
    }

    private IActionResult Refuse(int status, string? code, string? correlationId) =>
        InventoryProxy.Error(status, code, _localizer[InventoryErrorMessages.KeyFor(code, status)].Value, correlationId);
}
