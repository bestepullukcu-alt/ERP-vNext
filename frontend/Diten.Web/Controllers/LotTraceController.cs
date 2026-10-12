using Diten.Web.Models.Inventory;
using Diten.Web.Models.Traceability;
using Diten.Web.Services;
using Diten.Web.Views.Traceability.LotTrace;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0174 P3 — İzlenebilirlik (pack §11.1/§11.3): a 2-back / 2-forward flow around one lot, not a DataTable. Reads TRACE
// getLotDetail + getGenealogy(depth=2) (+ getLot of the related lots) and the lot's ledger transactions for the outbound
// documents (inventory.stock.read; without it the forward documents are simply not drawn). Read-only.
[Authorize]
[Route("LotTrace")]
public sealed class LotTraceController : Controller
{
    private const string ViewRoot = "~/Views/Traceability/LotTrace";
    private const int MaxRelatedLots = 40;

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<LotTraceIndex> _localizer;

    public LotTraceController(HttpClient httpClient, IConfiguration configuration, IPermissionSnapshot permissions, IStringLocalizer<LotTraceIndex> localizer)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
    }

    [HttpGet("")]
    public IActionResult Index([FromQuery] string? lotId) => View($"{ViewRoot}/Index.cshtml", LotPageViewModel.From(lotId));

    [HttpGet("data/{lotId}")]
    public async Task<IActionResult> Data(string lotId, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(TracePermissionKeys.LotsRead)) return Refuse(403, null, null);
        if (!Guid.TryParse(lotId, out var id)) return Refuse(404, "UNKNOWN_LOT", null);
        var trace = TraceGatewayClient.For(_httpClient, _gatewayUrl, Request, User);
        if (trace is null) return Refuse(401, null, null);

        var detail = await trace.GetAsync($"/api/trace/lots/{id:D}/detail", cancellationToken);
        if (!detail.IsSuccess) return Refuse(detail.StatusCode, detail.Error?.Code, detail.Error?.CorrelationId ?? detail.CorrelationId);
        var root = detail.Read<LotDetailDto>() ?? new LotDetailDto();

        var genealogyReply = await trace.GetAsync($"/api/trace/lots/{id:D}/genealogy?depth=2", cancellationToken);
        var genealogy = genealogyReply.IsSuccess ? genealogyReply.Read<GenealogyDto>() : null;

        var related = new Dictionary<string, LotRefDto>(StringComparer.Ordinal);
        var ids = (genealogy?.Parents ?? []).SelectMany(p => new[] { p.ParentLotId, p.ChildLotId })
            .Concat((genealogy?.Children ?? []).SelectMany(c => new[] { c.ParentLotId, c.ChildLotId }))
            .Where(x => !string.IsNullOrWhiteSpace(x) && x != root.LotId).Distinct(StringComparer.Ordinal).Take(MaxRelatedLots);
        foreach (var relatedId in ids)
        {
            var reply = await trace.GetAsync($"/api/trace/lots/{Uri.EscapeDataString(relatedId!)}", cancellationToken);
            if (reply.Read<LotRefDto>() is { } lot && reply.IsSuccess) related[relatedId!] = lot;
        }

        var transactions = new List<TransactionItemDto>();
        var documentsAvailable = false;
        var ledger = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (ledger is not null && _permissions.Has(TracePermissionKeys.StockRead))
        {
            var reply = await ledger.GetAsync($"/api/inventory/transactions?lotId={id:D}&page=1&pageSize=200", cancellationToken);
            documentsAvailable = reply.IsSuccess;
            if (reply.IsSuccess) transactions = reply.Read<TransactionPageDto>()?.Items ?? [];
        }

        var flow = LotTraceFlow.Build(root, genealogy, related, transactions);
        var labels = ledger is null ? MasterLabels.None : await TraceLabels.ResolveAsync(ledger, [root.ItemId], [(root.SkuId, root.SkuLevel)], [], cancellationToken);
        return Json(new
        {
            root = new
            {
                lotId = root.LotId, lotNumber = root.LotNumber, status = root.Status, expiryDate = root.ExpiryDate,
                product = labels.Product(root.ItemId) ?? root.ItemId, onHand = root.StockSummary?.OnHand, baseUomId = root.StockSummary?.BaseUomId
            },
            flow.Back1, flow.Back2, flow.Forward1, flow.Forward2, flow.Totals, flow.AffectedLots,
            genealogyAvailable = genealogy is not null,
            documentsAvailable,
            contractVersion = InventoryProxy.ContractVersion
        });
    }

    [HttpGet("lookups/lots")]
    public async Task<IActionResult> Lots([FromQuery] string? q, CancellationToken cancellationToken)
    {
        if (!_permissions.Has(TracePermissionKeys.LotsRead)) return Refuse(403, null, null);
        var trace = TraceGatewayClient.For(_httpClient, _gatewayUrl, Request, User);
        if (trace is null) return Refuse(401, null, null);
        var search = string.IsNullOrWhiteSpace(q) ? string.Empty : "&q=" + Uri.EscapeDataString(q.Trim()[..Math.Min(q.Trim().Length, TraceListAdapter.MaxSearchLength)]);
        var reply = await trace.GetAsync($"/api/trace/lots?page=1&pageSize=20&sort=lotNumber{search}", cancellationToken);
        var lots = reply.IsSuccess ? reply.Read<LotListDto>()?.Items ?? [] : [];
        return Json(new { items = lots.Select(l => new { value = l.LotId, text = l.LotNumber }), available = reply.IsSuccess });
    }

    private IActionResult Refuse(int status, string? code, string? correlationId) =>
        TraceErrorMessages.Refuse(status, code, _localizer[TraceErrorMessages.KeyFor(code, status)].Value, correlationId);
}
