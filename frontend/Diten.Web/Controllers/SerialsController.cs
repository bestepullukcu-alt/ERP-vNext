using Diten.Web.Models.Inventory;
using Diten.Web.Models.Traceability;
using Diten.Web.Services;
using Diten.Web.Views.Traceability.Serials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0174 P3 — Seri Gezgini (pack §11.1/§11.3): TRACE v1.1 listSerials, server mode, 15 rows per page. Tenant shell MVC;
// views under Views/Traceability/Serials/. Same-origin proxy only: Gateway /api/trace through TraceGatewayClient; product /
// location names through the FU01 InventoryGatewayClient. Read-only screen — no write exists here.
[Authorize]
[Route("Serials")]
public sealed class SerialsController : Controller
{
    private const string ViewRoot = "~/Views/Traceability/Serials";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<SerialsIndex> _localizer;
    private readonly ILogger<SerialsController> _logger;

    public SerialsController(HttpClient httpClient, IConfiguration configuration, IPermissionSnapshot permissions,
        IStringLocalizer<SerialsIndex> localizer, ILogger<SerialsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index([FromQuery] string? lotId) => View($"{ViewRoot}/Index.cshtml", LotPageViewModel.From(lotId));

    [HttpGet("api")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        if (!_permissions.Has(TracePermissionKeys.SerialsRead)) return Refuse(403, null, null);
        var translation = TraceListAdapter.SerialsQuery(Request.Query);
        if (!translation.IsValid) return Refuse(400, translation.ErrorCode, null);
        var trace = TraceGatewayClient.For(_httpClient, _gatewayUrl, Request, User);
        var ledger = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (trace is null || ledger is null) return Refuse(401, null, null);

        var reply = await trace.GetAsync("/api/trace/serials" + translation.Query, cancellationToken);
        if (!reply.IsSuccess)
        {
            _logger.LogWarning("TRACE listSerials failed with {Status} (correlation {CorrelationId}).", reply.StatusCode, reply.CorrelationId);
            return Refuse(reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
        }

        var page = reply.Read<SerialPageDto>() ?? new SerialPageDto();
        var items = page.Items ?? [];
        var labels = await TraceLabels.ResolveAsync(ledger, items.Select(s => s.ItemId), items.Select(s => (s.SkuId, s.SkuLevel)),
            items.SelectMany(s => new[] { s.WarehouseId, s.LocationId }), cancellationToken);
        var total = page.Total ?? items.Count;
        return Json(new
        {
            data = new
            {
                items = items.Select(s => new
                {
                    id = s.SerialId,
                    serialNumber = s.SerialNumber,
                    itemId = s.ItemId,
                    product = labels.Product(s.ItemId),
                    lotId = s.LotId,
                    lotNumber = s.LotNumber,
                    status = s.Status,
                    warehouse = labels.Location(s.WarehouseId) ?? s.WarehouseId,
                    location = labels.Location(s.LocationId) ?? s.LocationId,
                    lastMovementAt = s.LastMovementAt,
                    lastTransactionId = s.LastTransactionId
                }),
                total,
                filteredTotal = total
            },
            labelsAvailable = labels.Available,
            contractVersion = InventoryProxy.ContractVersion
        });
    }

    // Filters: product (Model A selector, as FU01) and the lots of the chosen product (dependent).
    [HttpGet("lookups/{kind}")]
    public async Task<IActionResult> Lookups(string kind, [FromQuery] string? q, [FromQuery] string? itemId, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(TracePermissionKeys.SerialsRead)) return Refuse(403, null, null);
        var ledger = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        var trace = TraceGatewayClient.For(_httpClient, _gatewayUrl, Request, User);
        if (ledger is null || trace is null) return Refuse(401, null, null);

        switch (kind)
        {
            case "products":
            {
                var result = await new ProductPickerSource(ledger).SearchProductsAsync(q, cancellationToken);
                return Json(new { items = result.Items.Select(o => new { value = o.Value, text = o.Text }), available = result.Available });
            }
            case "lots":
            {
                if (!Guid.TryParse(itemId, out var item)) return Json(new { items = Array.Empty<object>(), available = true });
                var reply = await trace.GetAsync($"/api/trace/lots?itemId={item:D}&page=1&pageSize=200&sort=lotNumber", cancellationToken);
                var lots = reply.IsSuccess ? reply.Read<LotListDto>()?.Items ?? [] : [];
                return Json(new { items = lots.Select(l => new { value = l.LotId, text = l.LotNumber }), available = reply.IsSuccess });
            }
            default:
                return NotFound();
        }
    }

    private IActionResult Refuse(int status, string? code, string? correlationId) =>
        TraceErrorMessages.Refuse(status, code, _localizer[TraceErrorMessages.KeyFor(code, status)].Value, correlationId);
}
