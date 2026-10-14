using Diten.Web.Models.Inventory;
using Diten.Web.Models.Traceability;
using Diten.Web.Services;
using Diten.Web.Views.Traceability.LotMovements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0174 P3 — Lot Hareket Geçmişi (pack §11.1/§11.3): the FU01 S-2 history (INVENTORY-BUNDLE getTransactions) filtered
// to ONE lot; page key trace.lots.read, and the rows are ledger rows, so the list also needs inventory.stock.read (pack
// §11.1). The FU01 adapter (InventoryListAdapter) is reused as is — one translation of the ledger wire, not two.
// The contract's TransactionItem carries no serial numbers, no source side and no recorder (BG-05): the serial chip
// (from Seri Gezgini) names the serial and highlights its last movement; it cannot filter rows by serial.
[Authorize]
[Route("LotMovements")]
public sealed class LotMovementsController : Controller
{
    private const string ViewRoot = "~/Views/Traceability/LotMovements";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<LotMovementsIndex> _localizer;
    private readonly ILogger<LotMovementsController> _logger;

    public LotMovementsController(HttpClient httpClient, IConfiguration configuration, IPermissionSnapshot permissions,
        IStringLocalizer<LotMovementsIndex> localizer, ILogger<LotMovementsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index([FromQuery] string? lotId, [FromQuery] string? serial) =>
        View($"{ViewRoot}/Index.cshtml", LotPageViewModel.From(lotId, serial));

    [HttpGet("api")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!_permissions.Has(TracePermissionKeys.LotsRead) || !_permissions.Has(TracePermissionKeys.StockRead)) return Refuse(403, null, null);
        if (!Guid.TryParse(Request.Query["lotId"], out _))
            return Json(new { data = new { items = Array.Empty<object>(), total = 0, filteredTotal = 0 }, contractVersion = InventoryProxy.ContractVersion });
        var translation = InventoryListAdapter.TransactionsQuery(Request.Query);
        if (!translation.IsValid) return Refuse(400, translation.ErrorCode, null);
        var ledger = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (ledger is null) return Refuse(401, null, null);

        var reply = await ledger.GetAsync("/api/inventory/transactions" + translation.Query, cancellationToken);
        if (!reply.IsSuccess)
        {
            _logger.LogWarning("Lot movement list failed with {Status} (correlation {CorrelationId}).", reply.StatusCode, reply.CorrelationId);
            return Refuse(reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
        }

        var page = reply.Read<TransactionPageDto>() ?? new TransactionPageDto();
        var items = page.Items ?? [];
        var labels = await TraceLabels.ResolveAsync(ledger, items.Select(t => t.ItemId), items.Select(t => (t.SkuId, t.SkuLevel)),
            items.SelectMany(t => new[] { t.ToWarehouseId, t.ToLocationId }), cancellationToken);
        var total = page.Total ?? items.Count;
        return Json(new
        {
            data = new { items = InventoryListAdapter.MapTransactions(items, labels), total, filteredTotal = total },
            labelsAvailable = labels.Available,
            contractVersion = page.ContractVersion ?? InventoryProxy.ContractVersion
        });
    }

    // The lot selector (search by lot number) and the serial chip (the serial's last movement).
    [HttpGet("lookups/{kind}")]
    public async Task<IActionResult> Lookups(string kind, [FromQuery] string? q, [FromQuery] string? lotId, [FromQuery] string? serial, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(TracePermissionKeys.LotsRead)) return Refuse(403, null, null);
        var trace = TraceGatewayClient.For(_httpClient, _gatewayUrl, Request, User);
        if (trace is null) return Refuse(401, null, null);

        switch (kind)
        {
            case "lots":
            {
                var search = string.IsNullOrWhiteSpace(q) ? string.Empty : "&q=" + Uri.EscapeDataString(q.Trim()[..Math.Min(q.Trim().Length, TraceListAdapter.MaxSearchLength)]);
                var reply = await trace.GetAsync($"/api/trace/lots?page=1&pageSize=20&sort=lotNumber{search}", cancellationToken);
                var lots = reply.IsSuccess ? reply.Read<LotListDto>()?.Items ?? [] : [];
                return Json(new { items = lots.Select(l => new { value = l.LotId, text = l.LotNumber }), available = reply.IsSuccess });
            }
            case "lot":
            {
                if (!Guid.TryParse(lotId, out var id)) return Json(new { found = false });
                var reply = await trace.GetAsync($"/api/trace/lots/{id:D}", cancellationToken);
                var lot = reply.IsSuccess ? reply.Read<LotRefDto>() : null;
                return Json(new { found = lot is not null, lotId = lot?.LotId, lotNumber = lot?.LotNumber, status = lot?.Status });
            }
            case "serial":
            {
                if (!_permissions.Has(TracePermissionKeys.SerialsRead) || !Guid.TryParse(lotId, out var id) || string.IsNullOrWhiteSpace(serial))
                    return Json(new { found = false });
                var number = LotFieldRules.NormalizeSerial(serial);
                var reply = await trace.GetAsync($"/api/trace/serials?lotId={id:D}&q={Uri.EscapeDataString(number)}&page=1&pageSize=20", cancellationToken);
                var match = reply.IsSuccess ? (reply.Read<SerialPageDto>()?.Items ?? []).FirstOrDefault(s => s.SerialNumber == number) : null;
                return Json(new { found = match is not null, serialNumber = match?.SerialNumber, status = match?.Status, lastTransactionId = match?.LastTransactionId });
            }
            default:
                return NotFound();
        }
    }

    private IActionResult Refuse(int status, string? code, string? correlationId) =>
        TraceErrorMessages.Refuse(status, code, _localizer[TraceErrorMessages.KeyFor(code, status)].Value, correlationId);
}
