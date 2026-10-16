using System.Globalization;
using System.Text.Json;
using Diten.BuildingBlocks.ListExport;
using Diten.Web.Models.CRM;
using Diten.Web.Views.CRM.PotentialThresholds;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-VW-W4 (W4-WEB-b, §2.2 / §2.7) — <c>/CRM/PotentialThresholds</c>: the per-product Low / Medium / High boundaries of a
/// doctor's monthly patients (tenant default + product rows), the save confirm and the history. Read with
/// <c>crm.doctor-insights.read</c> | <c>.read-all</c> | <c>.thresholds.manage</c> (the CRM rule); written with
/// <c>.thresholds.manage</c>. The product picker reads the MDM Global Product selector (<c>mdm.global-products.read</c>).
/// </summary>
[Authorize]
[Route("CRM/PotentialThresholds")]
public sealed class PotentialThresholdsController : CrmGatewayProxy
{
    public const string Read = "crm.doctor-insights.read";
    public const string ReadAll = "crm.doctor-insights.read-all";
    public const string Manage = "crm.doctor-insights.thresholds.manage";
    public const string ProductRead = "mdm.global-products.read";

    private const string Base = "/api/crm/doctor-insights/thresholds";
    private const int ProductPageSize = 100;
    private const int MaxProductPages = 10;

    public PotentialThresholdsController(HttpClient httpClient, IConfiguration configuration, ILogger<PotentialThresholdsController> logger)
        : base(httpClient, configuration, logger)
    {
    }

    private bool MayRead => HasAny(Read, ReadAll, Manage);

    /// <summary>The page. Without a read key the view draws only the access notice (UAS-001, no redirect).</summary>
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/CRM/PotentialThresholds/Index.cshtml", new PotentialThresholdsViewModel
    {
        CanRead = MayRead,
        CanManage = HasAny(Manage),
        CanReadProducts = HasAny(ProductRead),
        Me = KnowledgePathStudioReview.ActorOf(User)
    });

    [HttpGet("api/thresholds")]
    public Task<IActionResult> Thresholds(CancellationToken ct) => ProxyAsync(HttpMethod.Get, Base, null, MayRead, ct);

    /// <summary>The history list's page cap (the Golden list contract's page sizes 10 / 25 / 50 / 100).</summary>
    public const int MaxHistoryPageSize = 100;

    /// <summary>
    /// W3E-E4R2-FIX-c (U-5) — the change history is a Golden Compact SERVER-mode list: the page asks with the list contract
    /// (start · length · draw; the history has no search, no sort and no filter — the CRM pages it newest first) and reads
    /// { items, total, filteredTotal, displayNames }. The CRM keeps ITS contract (page / pageSize); this proxy translates
    /// and nothing else. Refusals before any CRM call: a page size outside 1–100, a sort key, a search text.
    /// </summary>
    [HttpGet("api/thresholds/history")]
    public async Task<IActionResult> History(
        [FromQuery] int? start, [FromQuery] int? length, [FromQuery] string? search, [FromQuery] string? orderBy, [FromQuery] string? orderDir, CancellationToken ct)
    {
        if (!MayRead) return Denied();
        if (HistoryRefusal(length, search, orderBy, orderDir) is { } refusal) return BadRequest(new { errors = new[] { refusal, "doctor_insights_invalid" } });
        var size = length ?? 10;
        var page = Math.Max(0, start ?? 0) / size + 1;
        using var response = await SendGatewayAsync(HttpMethod.Get, HistoryPath(page, size), null, ct);
        if (response is null || !response.IsSuccessStatusCode) return await ToProxyResultAsync(response, ct);
        if (await HistoryPageOf(response, ct) is not { } chunk) return StatusCode(502, new { errors = new[] { "Gateway unavailable." } });
        return Ok(new { data = new { items = chunk.Items, total = chunk.Total, filteredTotal = chunk.Total, displayNames = chunk.Names } });
    }

    /// <summary>Static, so MVC never treats it as an action: the CRM history path (page ≥ 1, size 1–200).</summary>
    public static string HistoryPath(int page, int pageSize) => $"{Base}/history?page={Math.Max(1, page)}&pageSize={Math.Clamp(pageSize, 1, 200)}";

    /// <summary>Static, so MVC never treats it as an action: the history list contract's refusals (null = acceptable).</summary>
    public static string? HistoryRefusal(int? length, string? search, string? orderBy, string? orderDir)
    {
        if (length is { } n && (n < 1 || n > MaxHistoryPageSize)) return $"length must be 1–{MaxHistoryPageSize}.";
        if (!string.IsNullOrWhiteSpace(search)) return "The history has no search.";
        if (!string.IsNullOrWhiteSpace(orderBy)) return "The history is ordered newest first; orderBy is not accepted.";
        if (!string.IsNullOrWhiteSpace(orderDir) && orderDir is not ("asc" or "desc")) return "orderDir must be asc or desc.";
        return null;
    }

    private sealed record HistoryPage(JsonElement Items, long Total, JsonElement? Names);

    private static async Task<HistoryPage?> HistoryPageOf(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            var data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d) ? d : root;
            if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array) return null;
            var total = data.TryGetProperty("total", out var t) && t.ValueKind == JsonValueKind.Number && t.TryGetInt64(out var n) ? n : items.GetArrayLength();
            JsonElement? names = data.TryGetProperty("displayNames", out var dn) && dn.ValueKind == JsonValueKind.Object ? dn.Clone() : null;
            return new HistoryPage(items.Clone(), total, names);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The history export's columns, by the list's data keys.</summary>
    public static readonly IReadOnlyList<string> HistoryExportKeys = ["at", "by", "productId", "field", "newValue"];

    /// <summary>
    /// W3E-E4R2-FIX-c — <c>GET api/thresholds/history/export?format=csv|xlsx&amp;columns=…</c>: EVERY change (the CRM history
    /// read page by page, 200 at a time — the CRM has no export of its own and this package changes nothing there), the
    /// visible columns in screen order, the person / product / field in the reader's language. More than 50 000 → 413
    /// EXPORT_TOO_LARGE before a row is written; an unknown column → 400.
    /// </summary>
    [HttpGet("api/thresholds/history/export")]
    public async Task<IActionResult> HistoryExport([FromQuery] string? format, [FromQuery] string[]? columns, CancellationToken ct)
    {
        if (!MayRead) return Denied();
        if (!ListExportContract.TryParseFormat(format, out var fileFormat)) return ExportRefusal(400, "format must be 'csv' or 'xlsx'.", ListExportContract.FormatInvalidCode);
        var culture = ListExportContract.ResolveCulture(Request.Headers.AcceptLanguage.ToString());
        if (!HistoryColumns().TryResolve(columns, out var exportColumns, out var columnsError)) return ExportRefusal(400, columnsError!, ListExportContract.ColumnsInvalidCode);

        var rows = new List<JsonElement>();
        var people = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var page = 1; ; page++)
        {
            using var response = await SendGatewayAsync(HttpMethod.Get, HistoryPath(page, 200), null, ct);
            if (response is null || !response.IsSuccessStatusCode) return await ToProxyResultAsync(response, ct);
            if (await HistoryPageOf(response, ct) is not { } chunk) return StatusCode(502, new { errors = new[] { "Gateway unavailable." } });
            if (chunk.Total > ListExportContract.MaxRows)
            {
                return ExportRefusal(StatusCodes.Status413PayloadTooLarge,
                    $"{chunk.Total} changes match; an export carries at most {ListExportContract.MaxRows}.", ListExportContract.TooLargeCode);
            }

            if (chunk.Names is { } names) foreach (var p in names.EnumerateObject()) if (p.Value.ValueKind == JsonValueKind.String) people[p.Name] = p.Value.GetString()!;
            rows.AddRange(chunk.Items.EnumerateArray());
            if (chunk.Items.GetArrayLength() < 200 || rows.Count >= chunk.Total) break;
        }

        var products = HasAny(ProductRead) ? await ProductNamesAsync(ct) : new Dictionary<string, string>();
        var lines = rows.Select(r => new HistoryExportRow(
            r.TryGetProperty("at", out var a) && a.ValueKind == JsonValueKind.String && a.TryGetDateTimeOffset(out var at) ? at : null,
            Str(r, "by") is { } by ? people.GetValueOrDefault(by) : null,
            Str(r, "productId") is { } pid ? products.GetValueOrDefault(pid) ?? pid : null,
            Str(r, "field") ?? string.Empty, Str(r, "oldValue"), Str(r, "newValue"))).ToList();
        var content = ListExportWriter.Write(fileFormat, exportColumns, lines, culture, Text("HistoryTitle", culture));
        return File(content, ListExportContract.ContentType(fileFormat), ListExportContract.FileName("potential-threshold-history", DateTimeOffset.UtcNow, fileFormat));
    }

    /// <summary>One change as the export writes it (the person by name — never the id; the product by name).</summary>
    public sealed record HistoryExportRow(DateTimeOffset? At, string? By, string? Product, string Field, string? OldValue, string? NewValue);

    private ListExportColumnSet<HistoryExportRow> HistoryColumns() => new(
    [
        new("at", c => Text("ColDate", c), (r, c) => r.At?.ToString("g", c)),
        new("by", c => Text("ColPerson", c), (r, _) => r.By ?? "—"),
        new("productId", c => Text("ColTarget", c), (r, c) => r.Product ?? Text("HistoryDefault", c)),
        new("field", c => Text("ColField", c), (r, c) => Text("Field_" + r.Field, c)),
        new("newValue", c => Text("ColChange", c), (r, _) => (r.OldValue ?? "—") + " → " + (r.NewValue ?? "—"))
    ]);

    /// <summary>The MDM Global Product names by id (the product picker's read); empty when MDM does not answer.</summary>
    private async Task<Dictionary<string, string>> ProductNamesAsync(CancellationToken ct)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var page = 1; page <= MaxProductPages; page++)
        {
            var data = await ReadDataAsync($"/api/global-products/selector?pageSize={ProductPageSize}&pageNumber={page}", ct);
            if (data is not { ValueKind: JsonValueKind.Object } d || !d.TryGetProperty("items", out var rows) || rows.ValueKind != JsonValueKind.Array) break;
            var count = 0;
            foreach (var row in rows.EnumerateArray())
            {
                count++;
                if (Str(row, "id") is { } id) names[id] = Str(row, "globalProductName") ?? Str(row, "canonicalCode") ?? id;
            }

            var total = d.TryGetProperty("totalCount", out var t) && t.TryGetInt32(out var n) ? n : 0;
            if (count == 0 || names.Count >= total) break;
        }

        return names;
    }

    /// <summary>A screen word in <paramref name="culture"/> (the page's own resx); the key itself when no localizer is wired.</summary>
    private string Text(string key, CultureInfo culture)
    {
        var localizer = HttpContext?.RequestServices?.GetService(typeof(IStringLocalizer<PotentialThresholdsIndex>)) as IStringLocalizer<PotentialThresholdsIndex>;
        if (localizer is null) return key;
        var before = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = culture;
        try
        {
            var text = localizer[key];
            return text.ResourceNotFound ? key : text.Value;
        }
        finally
        {
            CultureInfo.CurrentUICulture = before;
        }
    }

    private ObjectResult ExportRefusal(int statusCode, string message, string code) => StatusCode(statusCode, new
    {
        isSuccessful = false,
        statusCode,
        errors = new[] { message },
        errorCodes = new[] { new { code } }
    });

    /// <summary><paramref name="target"/> = a Global Product id or <c>default</c>; nothing else reaches the CRM path.</summary>
    [HttpPut("api/thresholds/{target}")]
    public async Task<IActionResult> Save(string target, CancellationToken ct)
    {
        if (!string.Equals(target, "default", StringComparison.Ordinal) && !(Guid.TryParse(target, out var id) && id != Guid.Empty))
        {
            return BadRequest(new { errors = new[] { "The target is a Global Product id or 'default'.", "doctor_insights_invalid" } });
        }

        return await ProxyAsync(HttpMethod.Put, $"{Base}/{target.ToLowerInvariant()}", await ReadBodyAsync(ct), HasAny(Manage), ct);
    }

    /// <summary>The product picker: the MDM Global Product selector read page by page. MDM unreachable ⇒ 503 (never a partial list).</summary>
    [HttpGet("api/lookups/products")]
    public async Task<IActionResult> Products(CancellationToken ct)
    {
        if (!MayRead || !HasAny(ProductRead)) return Denied();
        var items = new List<object>();
        for (var page = 1; page <= MaxProductPages; page++)
        {
            var data = await ReadDataAsync($"/api/global-products/selector?pageSize={ProductPageSize}&pageNumber={page}", ct);
            if (data is not { ValueKind: JsonValueKind.Object } d || !d.TryGetProperty("items", out var rows) || rows.ValueKind != JsonValueKind.Array)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { errors = new[] { "The MDM product catalogue is not available.", "dependency_unavailable" } });
            }

            var count = 0;
            foreach (var row in rows.EnumerateArray())
            {
                count++;
                items.Add(new { id = Str(row, "id"), code = Str(row, "canonicalCode"), name = Str(row, "globalProductName") });
            }

            var total = d.TryGetProperty("totalCount", out var t) && t.TryGetInt32(out var n) ? n : 0;
            if (count == 0 || items.Count >= total) break;
        }

        return Ok(new { data = items });
    }
}
