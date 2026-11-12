using System.Text.Json.Nodes;
using Diten.Web.Models.Inventory;
using Diten.Web.Models.Quarantine;
using Diten.Web.Models.Warehouse;
using Diten.Web.Services;
using Diten.Web.Views.Warehouse.CountEvidenceExports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

// MOD-0181 DL-5 — Kanıt Paketi (COUNTEVIDENCE, Evidence Pack) — pack §4.5, §8.13, §11: tenant shell MVC controller, route
// /Warehouse/CountEvidenceExports. The browser talks only to this same-origin controller; it calls Gateway 5000 /api/warehouse/… with
// CycleCountGatewayClient (cookie bearer, X-Tenant-Id, the company header and the PAGE's Accept-Language — the file's language is the page's, pack
// §4.5 r6 SB2/F-3). Every write is [ValidateAntiForgeryToken]; every action checks its cycle-count.evidence.* key before the Gateway.
[Authorize]
[Route("Warehouse/CountEvidenceExports")]
public sealed class CountEvidenceExportsController : Controller
{
    private const string ViewRoot = "~/Views/Warehouse/CountEvidenceExports";
    private const string Base = "/api/warehouse/count-evidence-exports";
    private const string CountsBase = "/api/warehouse/cycle-counts";

    public static readonly IReadOnlyList<string> Formats = ["Pdf", "Csv"];

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IPermissionSnapshot _permissions;
    private readonly IStringLocalizer<CountEvidenceExportsIndex> _localizer;
    private readonly ILogger<CountEvidenceExportsController> _logger;

    public CountEvidenceExportsController(HttpClient httpClient, IConfiguration configuration, IPermissionSnapshot permissions,
        IStringLocalizer<CountEvidenceExportsIndex> localizer, ILogger<CountEvidenceExportsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _permissions = permissions;
        _localizer = localizer;
        _logger = logger;
    }

    // UAS-001: the view draws _AccessDenied (and nothing else) without cycle-count.evidence.read — no redirect.
    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml");

    // List adapter: repo server-mode wire → listCountEvidenceExports (countId?, newest first).
    [HttpGet("api")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.EvidenceRead)) return Refuse(403, null, null);
        if (WarehouseProxy.Paging(Request.Query) is not { } paging) return Refuse(400, "VALIDATION_ERROR", null);
        if (ListQuery(Request.Query) is not { } filters) return Refuse(400, "VALIDATION_ERROR", null);
        var gateway = CycleCountProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);

        var reply = await gateway.GetAsync($"{Base}?page={paging.Page}&pageSize={paging.PageSize}{filters}", ct);
        if (!reply.IsSuccess) return Failed("list", reply);
        var page = CycleCountProxy.Node(reply);
        var rows = (page?["items"] as JsonArray ?? []).OfType<JsonObject>().ToList();
        var people = await PeopleAsync(gateway, rows.Select(r => CycleCountProxy.Text(r, "requestedBy")), ct);
        foreach (var row in rows) Decorate(row, people);
        var total = page?["total"]?.GetValue<long>() ?? rows.Count;
        return Json(new { data = new { items = rows, total, filteredTotal = total }, draw = paging.Draw, contractVersion = "v1" });
    }

    /// <summary>The list filter as the query tail, or null when it is outside the contract (a canonical count id).</summary>
    public static string? ListQuery(IQueryCollection source)
    {
        var count = WarehouseProxy.Single(source["countId"]);
        if (count is null) return string.Empty;
        return WarehouseProxy.TryId(count, out var id) ? "&countId=" + id : null;
    }

    // Server export (BL-452): every matching export record as CSV — the same filter as the list, at most WarehouseCsv.MaxRows (413 beyond).
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.EvidenceRead)) return Refuse(403, null, null);
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

        var people = await PeopleAsync(gateway, rows.Select(r => CycleCountProxy.Text(r, "requestedBy")), ct);
        foreach (var row in rows) Decorate(row, people);
        string W(string key) => _localizer[key].Value;
        var header = new[] { "ColCount", "ColFormat", "ColLanguage", "ColRequestedLanguage", "ColRequestedBy", "ColRequestedAt", "ColStatus", "ColSha256", "ColSize" }
            .Select(W);
        var lines = rows.Select(r => new[]
        {
            CycleCountProxy.Text(r, "countNumber"), W("CountEvidence.Format." + CycleCountProxy.Text(r, "format")), W("CountEvidence.Language." + CycleCountProxy.Text(r, "language")),
            W("CountEvidence.Language." + CycleCountProxy.Text(r, "requestedLanguage")), CycleCountProxy.Text(r, "requestedByName"), CycleCountProxy.Text(r, "requestedAt"),
            W("CountEvidence.Status." + CycleCountProxy.Text(r, "status")), CycleCountProxy.Text(r, "sha256"), r["sizeBytes"]?.ToString()
        });
        return File(WarehouseCsv.Write(header, lines), "text/csv; charset=utf-8", $"count-evidence-exports-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    // The export form's count picker: the company's Submitted / Closed counts (the evidence of a count whose variances exist — draft §4.4).
    [HttpGet("counts")]
    public async Task<IActionResult> Counts([FromQuery] string? q, CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.EvidenceExport)) return Refuse(403, null, null);
        if (!_permissions.Has(CycleCountPermissionKeys.CountsRead)) return Json(new { items = Array.Empty<object>(), available = false });
        var gateway = CycleCountProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var search = string.IsNullOrWhiteSpace(q) ? string.Empty : "&q=" + Uri.EscapeDataString(q.Trim().Length > 100 ? q.Trim()[..100] : q.Trim());
        var reply = await gateway.GetAsync($"{CountsBase}?page=1&pageSize=50&status=Submitted&status=Closed{search}", ct);
        if (!reply.IsSuccess) return Json(new { items = Array.Empty<object>(), available = false });
        var items = (CycleCountProxy.Node(reply)?["items"] as JsonArray ?? []).OfType<JsonObject>()
            .Select(c => new { value = CycleCountProxy.Text(c, "countId"), text = CycleCountProxy.Text(c, "number"), status = CycleCountProxy.Text(c, "status") });
        return Json(new { items, available = true });
    }

    // ── the write (draft §4.4; AC-43) ────────────────────────────────────────────────────────────────────

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CountEvidenceWriteRequest? model, CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.EvidenceExport)) return Refuse(403, null, null);
        if (model is null || !WarehouseProxy.IsKey(model.IdempotencyKey) || !WarehouseProxy.TryId(model.CountId, out var countId)
            || !Formats.Contains(model.Format ?? string.Empty, StringComparer.Ordinal))
            return Refuse(400, "VALIDATION_ERROR", null);
        var gateway = CycleCountProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var key = Guid.Parse(model.IdempotencyKey!).ToString("D");
        var reply = await gateway.SendAsync(HttpMethod.Post, $"{CountsBase}/{countId}/evidence-exports", new { idempotencyKey = key, format = model.Format }, key, null, ct);
        if (!reply.IsSuccess) return Failed("create", reply);
        var export = CycleCountProxy.Node(reply) as JsonObject ?? new JsonObject();
        return Json(new { success = true, export, message = _localizer["CountEvidence.Saved.Created"].Value });
    }

    // ── the download (AC-43: the stored bytes; AC-44: evidence.read) ─────────────────────────────────────

    [HttpGet("file/{exportId}")]
    public async Task<IActionResult> Download(string exportId, CancellationToken ct)
    {
        if (!_permissions.Has(CycleCountPermissionKeys.EvidenceRead)) return Refuse(403, null, null);
        if (!WarehouseProxy.TryId(exportId, out var id)) return Refuse(404, "COUNT_EVIDENCE_NOT_FOUND", null);
        var gateway = CycleCountProxy.Gateway(_httpClient, _gatewayUrl, Request, User);
        if (gateway is null) return Refuse(401, null, null);
        var file = await gateway.GetFileAsync($"{Base}/{id}/file", ct);
        if (file.Refusal is { } refusal) return Failed("file", refusal);
        if (file.Sha256 is { } sha) Response.Headers["X-Content-SHA256"] = sha;
        return File(file.Content!, file.ContentType ?? "application/octet-stream", file.FileName ?? $"count-evidence-{id}");
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    private static void Decorate(JsonObject row, IReadOnlyDictionary<string, string> people)
    {
        if (CycleCountProxy.Text(row, "requestedBy") is { } user) row["requestedByName"] = people.GetValueOrDefault(user) ?? user;
    }

    private static async Task<IReadOnlyDictionary<string, string>> PeopleAsync(CycleCountGatewayClient gateway, IEnumerable<string?> ids, CancellationToken ct)
    {
        var distinct = ids.OfType<string>().Distinct().ToList();
        if (distinct.Count == 0) return new Dictionary<string, string>();
        return await QuarantinePeople.ResolveAsync(distinct, async (path, token) =>
        {
            var reply = await gateway.GetAsync(path, token);
            return (reply.StatusCode, reply.Body);
        }, ct);
    }

    private IActionResult Failed(string operation, GatewayReply reply)
    {
        _logger.LogWarning("Count evidence {Operation} refused with {Status} {Code} (correlation {CorrelationId}).",
            operation, reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId);
        return Refuse(reply.StatusCode, reply.Error?.Code, reply.Error?.CorrelationId ?? reply.CorrelationId, reply.Error?.Details);
    }

    private IActionResult Refuse(int status, string? code, string? correlationId, object? details = null) =>
        CycleCountProxy.Error(status, code, _localizer[CycleCountErrorMessages.KeyFor(code, status)].Value, correlationId, details);
}
