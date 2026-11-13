using System.Text.Json;
using Diten.Web.Models.Inventory;
using Diten.Web.Models.Warehouse;
using Diten.Web.Services;
using Diten.Web.Views.Warehouse.WarehouseExceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0178 DL-7 — İstisna Kutusu (WAREHOUSEEXCEPTIONS, pack §8.23 / §11.1 / §11.5; Slim list, READ-ONLY): tenant shell MVC controller. The PAGE is
// /Warehouse/Exceptions (the manifest route); the same-origin data endpoints follow the view folder, /Warehouse/WarehouseExceptions/… (the DataTable
// contract's proxy path /{Area}/{Module}/api). Same door as the other warehouse screens (WarehouseGatewayClient → Gateway 5000
// /api/warehouse/exceptions; the DL-8 {everything} route — no new Gateway route). Every action checks warehouse.exceptions.read first (DL-3 CT F-1
// lesson); there is no write: no acknowledge / resolve in v1 (FU-0178-S3-ACT). A record links to its screen. No answer carries a ship-to (Q-10a).
[Authorize]
[Route("Warehouse/WarehouseExceptions")]
public sealed class WarehouseExceptionsController : Controller
{
    private const string ViewRoot = "~/Views/Warehouse/WarehouseExceptions";
    private const string Base = "/api/warehouse/exceptions";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<WarehouseExceptionsIndex> _localizer;
    private readonly ILogger<WarehouseExceptionsController> _logger;

    public WarehouseExceptionsController(HttpClient httpClient, IConfiguration configuration, IPermissionSnapshot permissions,
        IStringLocalizer<WarehouseExceptionsIndex> localizer, ILogger<WarehouseExceptionsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
    }

    // UAS-001: the view draws _AccessDenied (and nothing else) without warehouse.exceptions.read — no redirect.
    [HttpGet("~/Warehouse/Exceptions")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    [HttpGet("api")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.ExceptionsRead)) return Refuse(403, null, null);
        if (WarehouseProxy.Paging(Request.Query) is not { } paging) return Refuse(400, "VALIDATION_ERROR", null);
        if (ListQuery(Request.Query) is not { } filters) return Refuse(400, "VALIDATION_ERROR", null);

        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.GetAsync($"{Base}?page={paging.Page}&pageSize={paging.PageSize}{filters}", ct);
        if (!reply.IsSuccess) return Failed("list", reply);

        var page = reply.Read<WmsExceptionPageDto>() ?? new WmsExceptionPageDto();
        var items = page.Items ?? [];
        var labels = await LabelsAsync(items, ct);
        var total = page.Total ?? items.Count;
        return Json(new
        {
            data = new { items = items.Select(e => Row(e, labels)), total, filteredTotal = total },
            draw = paging.Draw,
            namesAvailable = labels.Available,
            contractVersion = InventoryProxy.ContractVersion
        });
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.ExceptionsRead)) return Refuse(403, null, null);
        if (ListQuery(Request.Query) is not { } filters) return Refuse(400, "VALIDATION_ERROR", null);
        var gateway = WarehouseProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);

        var items = new List<WmsExceptionDto>();
        for (var page = 1; ; page++)
        {
            var reply = await gateway.GetAsync($"{Base}?page={page}&pageSize=200{filters}", ct);
            if (!reply.IsSuccess) return Failed("export", reply);
            var dto = reply.Read<WmsExceptionPageDto>() ?? new WmsExceptionPageDto();
            if ((dto.Total ?? 0) > WarehouseCsv.MaxRows) return Refuse(413, null, reply.CorrelationId);
            items.AddRange(dto.Items ?? []);
            if ((dto.Items?.Count ?? 0) < 200 || items.Count >= (dto.Total ?? 0)) break;
        }

        var labels = await LabelsAsync(items, ct);
        string W(string key) => _localizer[key].Value;
        var header = new[] { "ColKind", "ColRecord", "ColWarehouse", "ColCode", "ColOpened" }.Select(W);
        var rows = items.Select(e => new[]
        {
            W("Warehouse.Exception.Kind." + e.Kind), Text(e.Details, "taskNumber") ?? Text(e.Details, "outboundNumber"),
            labels.Location(e.WarehouseId) ?? e.WarehouseId, e.Code, e.OpenedAtUtc
        });
        return File(WarehouseCsv.Write(header, rows), "text/csv; charset=utf-8", $"warehouse-exceptions-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    // Filter: the active warehouses of the company (LOCATION master).
    [HttpGet("warehouses")]
    public async Task<IActionResult> Warehouses(CancellationToken ct)
    {
        if (!_permissions.Has(WarehousePermissionKeys.ExceptionsRead)) return Refuse(403, null, null);
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return Refuse(401, null, null);
        var result = await new LocationPickerSource(inventory).ActiveWarehousesAsync(ct);
        return Json(new { items = result.Items.Select(o => new { value = o.Value, text = o.Text }), available = result.Available });
    }

    /// <summary>The filters as the WMS query tail (<c>&amp;…</c>): <c>kind</c> (one of the nine) and <c>warehouseId</c>; null when one is outside the contract.</summary>
    public static string? ListQuery(IQueryCollection source)
    {
        var query = new List<string>();
        var kind = WarehouseProxy.Single(source["kind"]);
        if (kind is not null)
        {
            if (!WarehouseVocabulary.ExceptionKinds.Contains(kind, StringComparer.Ordinal)) return null;
            query.Add("kind=" + kind);
        }

        var warehouse = WarehouseProxy.Single(source["warehouseId"]);
        if (warehouse is not null)
        {
            if (!WarehouseProxy.TryId(warehouse, out var id)) return null;
            query.Add("warehouseId=" + id);
        }

        return query.Count == 0 ? string.Empty : "&" + string.Join('&', query);
    }

    private async Task<MasterLabels> LabelsAsync(IReadOnlyList<WmsExceptionDto> items, CancellationToken ct)
    {
        if (items.Count == 0) return MasterLabels.None;
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return MasterLabels.None;
        return await MasterLabelResolver.ResolveAsync(inventory, [],
            items.Select(e => (Text(e.Details, "skuId"), Text(e.Details, "skuLevel"))),
            items.SelectMany(e => new[] { e.WarehouseId, Text(e.Details, "stagingLocationId") }), ct);
    }

    /// <summary>A string member of the details object (null when absent or not a string).</summary>
    public static string? Text(JsonElement details, string name)
        => details.ValueKind == JsonValueKind.Object && details.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Number(JsonElement details, string name)
        => details.ValueKind == JsonValueKind.Object && details.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    /// <summary>
    /// One list / QuickView row: the record's kind, its record (task or outbound number — the link target is chosen by the script on the reader's keys),
    /// the names of its ids and the kind's own details. <c>stagingDifference</c> / <c>differenceDeferredBy</c> are passed as the service wrote them
    /// (null = not computed, §8.23). No ship-to.
    /// </summary>
    public static object Row(WmsExceptionDto e, MasterLabels labels)
    {
        var d = e.Details;
        var differences = d.ValueKind == JsonValueKind.Object && d.TryGetProperty("stagingDifference", out var diff) && diff.ValueKind == JsonValueKind.Array
            ? diff.EnumerateArray().Select(x => new
            {
                lotId = Text(x, "lotId"), onHand = Text(x, "onHand"), bound = Text(x, "bound"), difference = Text(x, "difference")
            }).ToList()
            : null;
        var keys = d.ValueKind == JsonValueKind.Object && d.TryGetProperty("reservationKeys", out var k) && k.ValueKind == JsonValueKind.Array
            ? k.EnumerateArray().Select(x => x.GetString()).ToList()
            : null;
        return new
        {
            id = e.ExceptionId,
            e.Kind,
            status = e.Status ?? "Open",
            e.TaskId,
            e.OutboundId,
            taskKind = Text(d, "taskKind"),
            record = Text(d, "taskNumber") ?? Text(d, "outboundNumber"),
            outboundNumber = Text(d, "outboundNumber"),
            warehouse = labels.Location(e.WarehouseId) ?? e.WarehouseId,
            e.Code,
            openedAt = e.OpenedAtUtc,
            seq = Number(d, "seq"),
            quantity = Text(d, "quantity"),
            allocSeq = Number(d, "allocSeq"),
            lot = Text(d, "lotNumber") ?? Text(d, "lotId"),
            outboundStatus = Text(d, "outboundStatus"),
            readyAt = Text(d, "readyAtUtc"),
            createdAt = Text(d, "createdAtUtc"),
            pending = Number(d, "pending"),
            reservationKeys = keys,
            sku = Text(d, "skuId") is { } sku ? labels.Sku(sku) ?? sku : null,
            reason = Text(d, "reason"),
            expected = Text(d, "expected"),
            issued = Text(d, "issued"),
            staging = Text(d, "stagingLocationId") is { } node ? labels.Location(node) ?? node : null,
            differences,
            deferredBy = Text(d, "differenceDeferredBy")
        };
    }

    private IActionResult Failed(string operation, GatewayReply reply)
    {
        _logger.LogWarning("Warehouse exceptions {Operation} refused with {Status} {Code} (correlation {CorrelationId}).",
            operation, reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
        return Refuse(reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
    }

    private IActionResult Refuse(int status, string? code, string? correlationId) =>
        WarehouseProxy.Error(status, code, _localizer[WarehouseErrorMessages.KeyFor(code, status)].Value, correlationId);
}
