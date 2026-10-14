using System.Text;
using System.Text.Json;
using Diten.Web.Models.CRM;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-VW-W3E (W3E-WEB-a) — the institution QR codes: the bulk page <c>/CRM/VisitQrCodes</c> (Q5), the print page
/// <c>/CRM/VisitQrCodes/Print</c> (Q2: single A4 / A5, label sheet 4 / 8) and the same-origin proxies the account card (Q1,
/// Q3, Q4) uses. Reads need <c>crm.visit-qr.read</c>; every write and every payload <c>crm.visit-qr.manage</c> (the CRM's
/// own rule — it decides again). The payload only ever leaves through the two print endpoints, as an SVG drawn HERE
/// (D-9: QRCoder, MIT), never cached (<c>no-store</c>) and never logged; the card's preview is decoration (D-8).
/// </summary>
[Authorize]
[Route("CRM/VisitQrCodes")]
public sealed class VisitQrCodesController : CrmGatewayProxy
{
    public const string Read = "crm.visit-qr.read";
    public const string Manage = "crm.visit-qr.manage";
    public const string AccountRead = "crm.account.read";

    private const string Base = "/api/crm/visit-qr";
    private const string AccountTypeSet = "account-type";
    private const string CitySet = "city";

    /// <summary>The CRM list's own page cap (VisitQrLimits.MaxPageSize).</summary>
    public const int MaxPageSize = 100;

    /// <summary>The CRM bulk cap (VisitQrLimits.MaxBulk).</summary>
    public const int MaxBulk = 200;

    public static readonly IReadOnlyList<string> SinglePapers = ["a4", "a5"];
    public static readonly IReadOnlyList<string> LabelPapers = ["labels4", "labels8"];

    public VisitQrCodesController(HttpClient httpClient, IConfiguration configuration, ILogger<VisitQrCodesController> logger)
        : base(httpClient, configuration, logger)
    {
    }

    private bool MayRead => HasAny(Read, Manage);

    // ---------------- pages ----------------

    /// <summary>Q5 — the bulk page. Without the read key the view draws only the access notice (UAS-001, no redirect).</summary>
    [HttpGet("")]
    public IActionResult Index() => View("~/Views/CRM/VisitQrCodes/Index.cshtml", new VisitQrCodesViewModel
    {
        CanRead = MayRead,
        CanManage = HasAny(Manage),
        CanReadAccounts = HasAny(AccountRead)
    });

    /// <summary>
    /// Q2 — the print page: <paramref name="accountId"/> (single, A4 / A5) or <paramref name="ids"/> (the label sheet, ≤ 200,
    /// 4 / 8 a page). It shows NO payload until "Print" is pressed — the payload (and the printed event) is fetched then.
    /// Without the manage key: the access notice only (UAS-001).
    /// </summary>
    [HttpGet("Print")]
    public async Task<IActionResult> Print([FromQuery] Guid? accountId, [FromQuery] string? ids, [FromQuery] string? mode, CancellationToken ct)
    {
        var list = ParseIds(ids);
        var single = accountId is { } a && a != Guid.Empty ? a : (Guid?)null;
        var model = new VisitQrPrintViewModel
        {
            CanPrint = HasAny(Manage),
            AccountId = single,
            AccountIds = list,
            Labels = list.Count > 0 && (single is null || string.Equals(mode, "labels", StringComparison.OrdinalIgnoreCase)),
            CanReadAccounts = HasAny(AccountRead)
        };

        // the single sticker's texts: the institution, its parent, its address (crm.account.read; else the name the print
        // answer carries — never a personal datum: no rep, no phone)
        if (model.CanPrint && single is { } id && model.CanReadAccounts && await ReadDataAsync($"/api/crm/accounts/{id}", ct) is { } account)
        {
            model.AccountName = Str(account, "accountName");
            if (Guid.TryParse(Str(account, "parentAccountId"), out var parentId) && parentId != Guid.Empty
                && await ReadDataAsync($"/api/crm/accounts/{parentId}", ct) is { } parent)
            {
                model.ParentName = Str(parent, "accountName");
            }

            var cityCode = Str(account, "cityRef");
            var districtCode = Str(account, "districtRef");
            var city = string.IsNullOrWhiteSpace(cityCode) ? null : (await ReadReferenceLabelsAsync(CitySet, ct)).GetValueOrDefault(cityCode, cityCode);
            var district = string.IsNullOrWhiteSpace(districtCode) ? null : (await ReadReferenceLabelsAsync("district", ct)).GetValueOrDefault(districtCode, districtCode);
            model.Address = AddressOf(Str(account, "addressLine"), district, city);
        }

        return View("~/Views/CRM/VisitQrCodes/Print.cshtml", model);
    }

    /// <summary>Static, so MVC never treats it as an action: "line, district / city" with the empty parts left out.</summary>
    public static string? AddressOf(string? line, string? district, string? city)
    {
        var place = string.Join(" / ", new[] { district, city }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
        var text = string.Join(", ", new[] { line?.Trim(), place }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return text.Length == 0 ? null : text;
    }

    // ---------------- reads ----------------

    [HttpGet("api/accounts")]
    public Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] string? accountType, [FromQuery] string? city, [FromQuery] string? territoryId,
        [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, ListPath(status, accountType, city, territoryId, search, page, pageSize), null, MayRead, ct);

    /// <summary>Static, so MVC never treats it as an action: the CRM list path — every filter escaped, the page clamped
    /// (1 … ; 1 … 100), empty filters left out.</summary>
    public static string ListPath(string? status, string? accountType, string? city, string? territoryId, string? search, int? page, int? pageSize)
    {
        var q = new List<string>();
        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) q.Add(name + "=" + Uri.EscapeDataString(value.Trim()));
        }

        Add("status", status);
        Add("accountType", accountType);
        Add("city", city);
        Add("territoryId", territoryId);
        Add("search", search);
        q.Add("page=" + Math.Max(1, page ?? 1));
        q.Add("pageSize=" + Math.Clamp(pageSize ?? 25, 1, MaxPageSize));
        return Base + "/accounts?" + string.Join("&", q);
    }

    [HttpGet("api/accounts/{accountId:guid}")]
    public Task<IActionResult> Get(Guid accountId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Base}/accounts/{accountId}", null, MayRead, ct);

    [HttpGet("api/accounts/{accountId:guid}/history")]
    public Task<IActionResult> History(Guid accountId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"{Base}/accounts/{accountId}/history", null, MayRead, ct);

    /// <summary>The list's filter options in the user's language: institution types and cities (MOD-0048 sets). A set
    /// that cannot be read is an empty list (the filter then offers only "all").</summary>
    [HttpGet("api/lookups")]
    public async Task<IActionResult> Lookups(CancellationToken ct)
    {
        if (!MayRead) return Denied();
        var types = await ReadReferenceLabelsAsync(AccountTypeSet, ct);
        var cities = await ReadReferenceLabelsAsync(CitySet, ct);
        static object Options(IReadOnlyDictionary<string, string> map)
            => map.OrderBy(x => x.Value, StringComparer.CurrentCultureIgnoreCase).Select(x => new { value = x.Key, text = x.Value }).ToList();
        return Ok(new { data = new { accountTypes = Options(types), cities = Options(cities) } });
    }

    // ---------------- writes ----------------

    /// <summary>Q1a / Q1c "Generate" and Q3 "Regenerate" (the CRM tells them apart by the active code; expectedVersion).</summary>
    [HttpPost("api/accounts/{accountId:guid}/issue")]
    public async Task<IActionResult> Issue(Guid accountId, CancellationToken ct)
        => await ProxyAsync(HttpMethod.Post, $"{Base}/accounts/{accountId}/issue", await ReadBodyAsync(ct) ?? "{}", HasAny(Manage), ct);

    /// <summary>Q4 — revoke (reason required, ≤ 500 — the CRM checks again).</summary>
    [HttpPost("api/accounts/{accountId:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid accountId, CancellationToken ct)
        => await ProxyAsync(HttpMethod.Post, $"{Base}/accounts/{accountId}/revoke", await ReadBodyAsync(ct) ?? "{}", HasAny(Manage), ct);

    /// <summary>Q5 "Generate for the selected (those without a code)".</summary>
    [HttpPost("api/bulk-issue")]
    public async Task<IActionResult> BulkIssue(CancellationToken ct)
        => await ProxyAsync(HttpMethod.Post, $"{Base}/bulk-issue", await ReadBodyAsync(ct) ?? "{}", HasAny(Manage), ct);

    // ---------------- the print payload (manage; no-store; never logged) ----------------

    /// <summary>
    /// Q2 single — the active code drawn as an SVG for <paramref name="paper"/> (a4 | a5) × <paramref name="copies"/> (1–50):
    /// the CRM writes the <c>printed</c> event with both. The answer carries the SVG and the sticker's texts, NOT the payload.
    /// </summary>
    [HttpGet("api/accounts/{accountId:guid}/print")]
    public async Task<IActionResult> PrintOne(Guid accountId, [FromQuery] string? paper, [FromQuery] int? copies, CancellationToken ct)
    {
        NoStore();
        if (!HasAny(Manage)) return Denied();
        var p = (paper ?? string.Empty).Trim().ToLowerInvariant();
        if (!SinglePapers.Contains(p) || copies is not (>= 1 and <= 50))
        {
            return BadRequest(new { errors = new[] { "paper must be a4 or a5 and copies 1–50.", "visit_qr_request_invalid" } });
        }

        var response = await SendGatewayAsync(HttpMethod.Get, $"{Base}/accounts/{accountId}/payload?paper={p}&copies={copies}", null, ct);
        return await ToPrintResultAsync(response, single: true, ct);
    }

    /// <summary>Q2 labels — the ACTIVE codes of the selected institutions (≤ 200) as SVGs, labels4 | labels8; one printed
    /// event each (CRM). An institution without an active code is simply not in the answer.</summary>
    [HttpPost("api/bulk-print")]
    public async Task<IActionResult> PrintMany(CancellationToken ct)
    {
        NoStore();
        if (!HasAny(Manage)) return Denied();
        var body = await ReadBodyAsync(ct);
        if (body is null || ContainsTenantId(body)) return BadRequest(new { errors = new[] { "The request body is invalid.", "visit_qr_request_invalid" } });
        BulkPrintInput? input;
        try
        {
            input = JsonSerializer.Deserialize<BulkPrintInput>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            input = null;
        }

        var paper = (input?.Paper ?? string.Empty).Trim().ToLowerInvariant();
        if (input?.AccountIds is not { Count: > 0 and <= MaxBulk } || !LabelPapers.Contains(paper) || input.Copies is not (>= 1 and <= 50))
        {
            return BadRequest(new { errors = new[] { "1–200 institutions, paper labels4 or labels8 and copies 1–50.", "visit_qr_request_invalid" } });
        }

        var forward = JsonSerializer.Serialize(new { accountIds = input.AccountIds, paper, copies = input.Copies });
        var response = await SendGatewayAsync(HttpMethod.Post, $"{Base}/bulk-payload", new StringContent(forward, Encoding.UTF8, "application/json"), ct);
        return await ToPrintResultAsync(response, single: false, ct);
    }

    public sealed record BulkPrintInput(IReadOnlyList<Guid>? AccountIds, string? Paper, int? Copies);

    /// <summary>The CRM payload answer → the same shape with <c>svg</c> instead of <c>payload</c>; any failure as it came.</summary>
    private async Task<IActionResult> ToPrintResultAsync(HttpResponseMessage? response, bool single, CancellationToken ct)
    {
        if (response is null || !response.IsSuccessStatusCode) return await ToProxyResultAsync(response, ct);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        var data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d) ? d : root;
        if (single)
        {
            return data.ValueKind == JsonValueKind.Object ? Ok(new { data = Sticker(data) }) : StatusCode(502, new { errors = new[] { "Gateway unavailable." } });
        }

        return data.ValueKind == JsonValueKind.Array
            ? Ok(new { data = data.EnumerateArray().Select(Sticker).ToList() })
            : StatusCode(502, new { errors = new[] { "Gateway unavailable." } });
    }

    /// <summary>One payload row → the sticker (the payload itself never leaves this method except as the drawing).</summary>
    private static object Sticker(JsonElement row)
    {
        var payload = Str(row, "payload") ?? string.Empty;
        return new
        {
            accountId = Str(row, "accountId"),
            accountName = Str(row, "accountName"),
            accountCode = Str(row, "accountCode"),
            version = row.TryGetProperty("version", out var v) && v.TryGetInt32(out var n) ? n : 0,
            svg = VisitQrSvg.Render(payload)
        };
    }

    private void NoStore()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
    }

    /// <summary>Static, so MVC never treats it as an action: "a,b,c" → distinct non-empty ids, at most 200.</summary>
    public static IReadOnlyList<Guid> ParseIds(string? ids)
        => (ids ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => Guid.TryParse(x, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct().Take(MaxBulk).ToList();
}
