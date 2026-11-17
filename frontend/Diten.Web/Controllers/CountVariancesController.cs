using System.Text.Json.Nodes;
using Diten.Web.Models.Inventory;
using Diten.Web.Models.Warehouse;
using Diten.Web.Services;
using Diten.Web.Views.Warehouse.CountVariances;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0181 DL-4 — Fark İzi (COUNTVARIANCES, Variance Trace) — pack §2.4, §8.5, §8.13, §11: tenant shell MVC controller, route
// /Warehouse/CountVariances. The browser talks only to this same-origin controller; it calls Gateway 5000 /api/warehouse/count-variances (and the
// recount under /cycle-counts) with CycleCountGatewayClient (cookie bearer, X-Tenant-Id, the company header, the page's Accept-Language). Decisions
// only on "Sayım" rows; "Elle" rows are read-only (§2.4). Four eyes: the screen withholds "Onayla" from the line's counter / the count's submitter
// (selfApproval) — the service still answers 403 COUNT_SELF_APPROVAL_FORBIDDEN; the screen rule never replaces the server's (§8.5).
[Authorize]
[Route("Warehouse/CountVariances")]
public sealed class CountVariancesController : Controller
{
    private const string ViewRoot = "~/Views/Warehouse/CountVariances";
    private const string Base = "/api/warehouse/count-variances";
    private const string Counts = "/api/warehouse/cycle-counts";

    public static readonly IReadOnlyList<string> Sources = ["count", "manual", "all"];
    public static readonly IReadOnlyList<string> LineStatuses = ["NoVariance", "AwaitingApproval", "Approved", "Rejected", "Recounted"];

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<CountVariancesIndex> _localizer;
    private readonly ILogger<CountVariancesController> _logger;

    public CountVariancesController(HttpClient httpClient, IConfiguration configuration, IPermissionSnapshot permissions,
        IStringLocalizer<CountVariancesIndex> localizer, ILogger<CountVariancesController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
    }

    // UAS-001: the view draws _AccessDenied (and nothing else) without cycle-count.variances.read — no redirect.
    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    // List adapter: repo server-mode wire → listCountVariances (source, warehouseId, from, to, status*, skuId, lotId).
    [HttpGet("api")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.VariancesRead)) return Refuse(403, null, null);
        if (WarehouseProxy.Paging(Request.Query) is not { } paging) return Refuse(400, "VALIDATION_ERROR", null);
        if (ListQuery(Request.Query) is not { } filters) return Refuse(400, "VALIDATION_ERROR", null);
        var gateway = CycleCountProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);

        var reply = await gateway.GetAsync($"{Base}?page={paging.Page}&pageSize={paging.PageSize}{filters}", ct);
        if (!reply.IsSuccess) return Failed("list", reply);
        var page = CycleCountProxy.Node(reply);
        var rows = (page?["items"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        await DecorateAsync(gateway, rows, ct);
        var total = page?["total"]?.GetValue<long>() ?? rows.Count;
        return Json(new { data = new { items = rows, total, filteredTotal = total }, draw = paging.Draw, contractVersion = "v1" });
    }

    // Server export (BL-452): every matching row as CSV — the same filters (and the same manual / all window rule) as the list, at most
    // WarehouseCsv.MaxRows (413 beyond).
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.VariancesRead)) return Refuse(403, null, null);
        if (ListQuery(Request.Query) is not { } filters) return Refuse(400, "VALIDATION_ERROR", null);
        var gateway = CycleCountProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);

        var rows = new List<JsonObject>();
        for (var page = 1; ; page++)
        {
            var reply = await gateway.GetAsync($"{Base}?page={page}&pageSize=200{filters}", ct);
            if (!reply.IsSuccess) return Failed("export", reply);
            var body = CycleCountProxy.Node(reply);
            var total = body?["total"]?.GetValue<long>() ?? 0;
            if (total > WarehouseCsv.MaxRows) return Refuse(413, null, reply.CorrelationId);
            var items = (body?["items"] as JsonArray ?? []).OfType<JsonObject>().ToList();
            rows.AddRange(items);
            if (items.Count < 200 || rows.Count >= total) break;
        }

        await DecorateAsync(gateway, rows, ct);
        string W(string key) => _localizer[key].Value;
        var header = new[] { "ColCount", "ColSource", "ColBin", "ColProduct", "ColLot", "ColExpectedAtCount", "ColCounted", "ColVariance", "ColStatus", "ColAdjustment" }.Select(W);
        // DL-4 FIX-02: names, never ids — an unread name / lot number is said in words
        string? Lot(JsonObject? o) => CycleCountProxy.Text(o, "lotId") is null ? null : CycleCountProxy.Text(o, "lotNumber") ?? W("LotUnknown");
        string Name(JsonObject? o, string member) => CycleCountProxy.Text(o, member) ?? W("NameUnknown");
        var lines = rows.Select(r =>
        {
            if (r["line"] is JsonObject line)
            {
                var adjustment = line["adjustment"] as JsonObject;
                return new[]
                {
                    CycleCountProxy.Text(r, "countNumber"), W("CycleCount.Source.Count"), Name(line, "location"), Name(line, "sku"),
                    Lot(line), CycleCountProxy.Text(line, "expectedAtCount"), CycleCountProxy.Text(line, "countedQuantity"),
                    CycleCountProxy.Text(line, "varianceQuantity"), W("CycleCount.LineStatus." + CycleCountProxy.Text(line, "status")),
                    adjustment is null ? null : $"{W("CycleCount.Adjustment." + CycleCountProxy.Text(adjustment, "status"))} {CycleCountProxy.Text(adjustment, "transactionNumber")}".Trim()
                };
            }

            var movement = r["movement"] as JsonObject;
            return new[]
            {
                CycleCountProxy.Text(movement, "transactionNumber"), W("CycleCount.Source.Manual"), Name(movement, "location"),
                Name(movement, "sku"), Lot(movement), null, null,
                $"{(CycleCountProxy.Text(movement, "movementType") == "COUNT_LOSS" ? "-" : "+")}{CycleCountProxy.Text(movement, "quantity")}",
                W("ReadOnlyManual"), CycleCountProxy.Text(movement, "transactionNumber")
            };
        });
        return File(WarehouseCsv.Write(header, lines), "text/csv; charset=utf-8", $"count-variances-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    /// <summary>The list filters as the query tail, or null when one is outside the contract (source / status enums, canonical ids, dates).</summary>
    public static string? ListQuery(IQueryCollection source)
    {
        var query = new List<string>();
        var origin = WarehouseProxy.Single(source["source"]);
        if (origin is not null)
        {
            if (!Sources.Contains(origin, StringComparer.Ordinal)) return null;
            query.Add("source=" + origin);
        }

        foreach (var status in source["status"].Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            if (!LineStatuses.Contains(status!, StringComparer.Ordinal)) return null;
            query.Add("status=" + status);
        }

        foreach (var name in new[] { "warehouseId", "skuId" })
        {
            var value = WarehouseProxy.Single(source[name]);
            if (value is null) continue;
            if (!WarehouseProxy.TryId(value, out var id)) return null;
            query.Add($"{name}={id}");
        }

        foreach (var name in new[] { "from", "to" })
        {
            var value = WarehouseProxy.Single(source[name]);
            if (value is null) continue;
            if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", out _)) return null;
            query.Add($"{name}={value}");
        }

        var lot = WarehouseProxy.Single(source["lotId"]);
        if (lot is not null) query.Add("lotId=" + Uri.EscapeDataString(lot.Length > 100 ? lot[..100] : lot));
        return query.Count == 0 ? string.Empty : "&" + string.Join('&', query);
    }

    [HttpGet("variance/{lineId}")]
    public async Task<IActionResult> Detail(string lineId, CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.VariancesRead)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(lineId, out var id)) return Refuse(404, "COUNT_LINE_NOT_FOUND", null);
        var gateway = CycleCountProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.GetAsync($"{Base}/{id}", ct);
        if (!reply.IsSuccess) return Failed("get", reply);
        if (CycleCountProxy.Node(reply) is not JsonObject row) return Refuse(503, null, reply.CorrelationId);
        await DecorateAsync(gateway, [row], ct);
        return Json(row);
    }

    [HttpGet("warehouses")]
    public async Task<IActionResult> Warehouses(CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.VariancesRead)) return Refuse(403, null, null);
        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (inventory is null) return Refuse(401, null, null);
        var result = await new LocationPickerSource(inventory).ActiveWarehousesAsync(ct);
        return Json(new { items = result.Items.Select(o => new { value = o.Value, text = o.Text }), available = result.Available });
    }

    // ── writes (pack §8.5 – §8.7; AC-41: one action per real button) ─────────────────────────────────────

    [HttpPost("approve/{lineId}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(string lineId, [FromBody] CycleCountWriteRequest? model, CancellationToken ct)
        => LineAsync("approve", CycleCountPermissionKeys.VariancesApprove, lineId, model, null, "approve", "CycleCount.Saved.Approved", ct);

    [HttpPost("reject/{lineId}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Reject(string lineId, [FromBody] CycleCountWriteRequest? model, CancellationToken ct)
        => LineAsync("reject", CycleCountPermissionKeys.VariancesReject, lineId, model, b => b["reason"] = model?.Reason?.Trim(), "reject", "CycleCount.Saved.Rejected", ct);

    [HttpPost("retry/{lineId}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Retry(string lineId, [FromBody] CycleCountWriteRequest? model, CancellationToken ct)
        => LineAsync("retry", CycleCountPermissionKeys.VariancesPost, lineId, model, null, "retry", "CycleCount.Saved.Retried", ct);

    [HttpPost("resolve/{lineId}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Resolve(string lineId, [FromBody] CycleCountWriteRequest? model, CancellationToken ct)
    {
        if (model?.Decision is not ("AcceptHolder" or "Reject")) return Task.FromResult(Refuse(400, "VALIDATION_ERROR", null));
        return LineAsync("resolve", CycleCountPermissionKeys.VariancesResolve, lineId, model,
            b => { b["decision"] = model.Decision; b["reason"] = model.Reason?.Trim(); }, "resolve-integrity", "CycleCount.Saved.Resolved", ct);
    }

    // "Yeniden say" (§8.5): the selected lines of ONE count (If-Match = the count's ETag) — a new released count; blind / scope are never sent.
    [HttpPost("recount/{countId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Recount(string countId, [FromBody] CycleCountWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.VariancesRecount)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(countId, out var count) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.IsETag(model.IfMatch)
            || model.LineIds is not { Count: > 0 and <= 200 } || model.LineIds.Any(l => !WarehouseProxy.TryId(l, out _)))
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        var body = new Dictionary<string, object?>
        {
            ["idempotencyKey"] = key, ["lineIds"] = model.LineIds.Select(l => Guid.Parse(l).ToString("D")).ToList(), ["reason"] = model.Reason?.Trim()
        };
        if (!string.IsNullOrWhiteSpace(model.AssignedTo))
        {
            if (!WarehouseProxy.TryId(model.AssignedTo, out var assignee)) return Refuse(400, "VALIDATION_ERROR", null);
            body["assignedTo"] = assignee;
        }

        if (!string.IsNullOrWhiteSpace(model.Note)) body["note"] = model.Note.Trim();
        var gateway = CycleCountProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.SendAsync(HttpMethod.Post, $"{Counts}/{count}/recount", body, key, model.IfMatch, ct);
        if (!reply.IsSuccess) return Failed("recount", reply);
        var created = CycleCountProxy.Node(reply);
        return Json(new { success = true, id = CycleCountProxy.Text(created, "countId"), number = CycleCountProxy.Text(created, "number"), message = _localizer["CycleCount.Saved.Recounted"].Value });
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    private async Task<IActionResult> LineAsync(string operation, string permission, string lineId, CycleCountWriteRequest? model,
        Action<Dictionary<string, object?>>? fill, string verb, string savedKey, CancellationToken ct)
    {
        if (!_permissions.Has(permission)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(lineId, out var line) || model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.IsETag(model.IfMatch))
            return Refuse(400, "VALIDATION_ERROR", null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        var body = new Dictionary<string, object?> { ["idempotencyKey"] = key };
        fill?.Invoke(body); // the service owns COUNT_REASON_REQUIRED (1..500)
        var gateway = CycleCountProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var reply = await gateway.SendAsync(HttpMethod.Post, $"{Base}/{line}/{verb}", body, key, model.IfMatch, ct);
        if (!reply.IsSuccess) return Failed(operation, reply);
        return Json(new { success = true, line = CycleCountProxy.Node(reply), message = _localizer[savedKey].Value });
    }

    /// <summary>
    /// Names and the four-eyes flag of each row: location / SKU / warehouse names (raw ids when a master cannot say) and, on a "Sayım" row,
    /// <c>selfApproval</c> = the caller counted the line or submitted its count (the count is read once per count).
    /// </summary>
    private async Task DecorateAsync(CycleCountGatewayClient gateway, IReadOnlyList<JsonObject> rows, CancellationToken ct)
    {
        var me = Me();
        var lines = rows.Select(r => r["line"] as JsonObject).OfType<JsonObject>().ToList();
        var movements = rows.Select(r => r["movement"] as JsonObject).OfType<JsonObject>().ToList();
        var counts = new Dictionary<string, (string? SubmittedBy, string? ETag, string? Status)>(StringComparer.Ordinal);
        foreach (var countId in lines.Select(l => CycleCountProxy.Text(l, "countId")).OfType<string>().Distinct().Take(50))
        {
            var count = await gateway.GetAsync($"{Counts}/{countId}", ct);
            var node = count.IsSuccess ? CycleCountProxy.Node(count) : null;
            counts[countId] = (CycleCountProxy.Text(node, "submittedBy"), node is null ? null : count.ETag ?? $"W/\"{node["version"]}\"", CycleCountProxy.Text(node, "status"));
        }

        var inventory = InventoryProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        var labels = inventory is null ? MasterLabels.None : await MasterLabelResolver.ResolveAsync(inventory,
            lines.Select(l => l["item"]?["itemId"]?.GetValue<string>()).Concat(movements.Select(m => m["item"]?["itemId"]?.GetValue<string>())),
            lines.Select(l => (l["item"]?["skuId"]?.GetValue<string>(), l["item"]?["skuLevel"]?.GetValue<string>()))
                .Concat(movements.Select(m => (m["item"]?["skuId"]?.GetValue<string>(), m["item"]?["skuLevel"]?.GetValue<string>()))),
            lines.SelectMany(l => new[] { CycleCountProxy.Text(l, "locationId"), CycleCountProxy.Text(l, "warehouseId") })
                .Concat(movements.SelectMany(m => new[] { CycleCountProxy.Text(m, "locationId"), CycleCountProxy.Text(m, "warehouseId") })), ct);

        // DL-4 FIX-02: the lot NUMBERS of every line and manual movement, one lot-register read per distinct SKU (never per row)
        var lots = await CycleCountNames.LotNumbersAsync(_httpClient, _gatewayUrl, Request, User, _permissions,
            lines.Select(l => (CycleCountProxy.Text(l, "lotId"), l["item"]?["skuId"]?.GetValue<string>(), l["item"]?["skuLevel"]?.GetValue<string>()))
                .Concat(movements.Select(m => (CycleCountProxy.Text(m, "lotId"), m["item"]?["skuId"]?.GetValue<string>(), m["item"]?["skuLevel"]?.GetValue<string>()))), ct);
        foreach (var row in rows)
        {
            if (row["line"] is JsonObject line)
            {
                // DL-4 FIX-02 (owner Beste 2026-11-12): a name the master cannot say is LEFT OUT — never the id in its place
                if (labels.Location(CycleCountProxy.Text(line, "locationId")) is { } location) line["location"] = location;
                if (labels.Location(CycleCountProxy.Text(line, "warehouseId")) is { } warehouse) line["warehouse"] = warehouse;
                if ((labels.Sku(line["item"]?["skuId"]?.GetValue<string>()) ?? labels.Product(line["item"]?["itemId"]?.GetValue<string>())) is { } sku) line["sku"] = sku;
                if (CycleCountProxy.Text(line, "lotId") is { } lotId && lots.TryGetValue(lotId, out var lotNumber)) line["lotNumber"] = lotNumber;
                line["etag"] = $"W/\"{line["version"]}\"";
                var countedBy = CycleCountProxy.Text(line, "countedBy");
                var count = counts.GetValueOrDefault(CycleCountProxy.Text(line, "countId") ?? string.Empty);
                row["selfApproval"] = me is not null && (string.Equals(countedBy, me, StringComparison.OrdinalIgnoreCase)
                                                         || string.Equals(count.SubmittedBy, me, StringComparison.OrdinalIgnoreCase));
                row["countEtag"] = count.ETag; // "Yeniden say" sends the count's ETag (§8.5)
                row["countStatus"] = count.Status;
            }

            if (row["movement"] is JsonObject movement)
            {
                if (labels.Location(CycleCountProxy.Text(movement, "locationId")) is { } location) movement["location"] = location;
                if ((labels.Sku(movement["item"]?["skuId"]?.GetValue<string>()) ?? labels.Product(movement["item"]?["itemId"]?.GetValue<string>())) is { } sku) movement["sku"] = sku;
                if (CycleCountProxy.Text(movement, "lotId") is { } lotId && lots.TryGetValue(lotId, out var lotNumber)) movement["lotNumber"] = lotNumber;
            }
        }
    }

    private string? Me()
    {
        var sub = User.Claims.FirstOrDefault(c => c.Type is "sub" or "userId" || c.Type.EndsWith("/nameidentifier", StringComparison.OrdinalIgnoreCase))?.Value;
        return Guid.TryParse(sub, out var id) ? id.ToString("D") : null;
    }

    private IActionResult Failed(string operation, GatewayReply reply)
    {
        _logger.LogWarning("Count variance {Operation} refused with {Status} {Code} (correlation {CorrelationId}).",
            operation, reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
        return Refuse(reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId, reply.Error?.Details);
    }

    private IActionResult Refuse(int status, string? code, string? correlationId, object? details = null) =>
        CycleCountProxy.Error(status, code, _localizer[CycleCountErrorMessages.KeyFor(code, status)].Value, correlationId, details);
}
