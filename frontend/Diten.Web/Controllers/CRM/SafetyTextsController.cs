using System.Text.Json;
using Diten.Web.Models.CRM;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-KP-5a-UI — Safety Texts (product × country × language; WP-KP-5a). Golden Compact: list, Create / Edit (draft
/// only, identity fixed after create), Details at <c>/CRM/SafetyTexts/{id}</c> — the MOD-0023 Regulatory task's deep
/// link — with the decision panel. CRM under <c>/api/crm/knowledge/safety-texts</c> decides every rule.
/// </summary>
[Authorize]
[Route("CRM/SafetyTexts")]
public sealed class SafetyTextsController : RegulatoryTextsControllerBase
{
    public const string Read = "crm.safety-text.read";
    public const string Manage = "crm.safety-text.manage";
    public const string Submit = "crm.safety-text.submit";
    private const string GlobalProductReadPermission = "mdm.global-products.read";
    private const string ViewRoot = "~/Views/CRM/SafetyTexts";

    public SafetyTextsController(HttpClient httpClient, IConfiguration configuration, ILogger<SafetyTextsController> logger)
        : base(httpClient, configuration, logger)
    {
    }

    protected override string CrmBase => "/api/crm/knowledge/safety-texts";
    protected override string ReadPermission => Read;
    protected override string ManagePermission => Manage;
    protected override string SubmitPermission => Submit;

    // ---------------- pages (UAS-001: no key → plain 403, no skeleton, no redirect) ----------------

    [HttpGet("")]
    public IActionResult Index()
    {
        if (RequirePage(Read) is { } denied) return denied;
        ViewData["CanManage"] = CanManage;
        return View($"{ViewRoot}/Index.cshtml");
    }

    [HttpGet("Create")]
    public IActionResult Create() =>
        RequirePage(Manage) ?? View($"{ViewRoot}/Create.cshtml", new SafetyTextEditViewModel());

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        if (RequirePage(Manage) is { } denied) return denied;
        var detail = await ReadDetailAsync(id, ct);
        if (detail is not { ValueKind: JsonValueKind.Object } d) return NotFound();
        // Only a draft is edited; anything else is read on its Details page (not an authorization redirect).
        if (!Flag(d, "canEdit")) return RedirectToAction(nameof(Details), new { id });
        return View($"{ViewRoot}/Edit.cshtml", ToModel(id, d));
    }

    /// <summary>The Details page and the WCN deep link (<c>/CRM/SafetyTexts/{id}</c>).</summary>
    [HttpGet("{id:guid}")]
    public IActionResult Details(Guid id)
    {
        if (RequirePage(Read) is { } denied) return denied;
        ViewData["Id"] = id.ToString();
        ViewData["CanManage"] = CanManage;
        ViewData["CanSubmit"] = CanSubmit;
        return View($"{ViewRoot}/Details.cshtml");
    }

    // ---------------- same-origin JSON proxy ----------------

    [HttpGet("api/safety-texts")]
    public Task<IActionResult> List(CancellationToken ct) => ListAsync(ct);

    [HttpGet("api/safety-texts/resolve")]
    public Task<IActionResult> Resolve([FromQuery] string? productId, [FromQuery] string? countryCode,
        [FromQuery] string? languageCode, CancellationToken ct) => ResolveAsync(productId, countryCode, languageCode, ct);

    [HttpGet("api/safety-texts/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => GetAsync(id, ct);

    [HttpPost("api/safety-texts")]
    public Task<IActionResult> CreateItem([FromBody] JsonElement body, CancellationToken ct) => CreateAsync(body, ct);

    [HttpPut("api/safety-texts/{id:guid}")]
    public Task<IActionResult> UpdateItem(Guid id, [FromBody] JsonElement body, CancellationToken ct) => UpdateAsync(id, body, ct);

    [HttpPost("api/safety-texts/{id:guid}/submit")]
    public Task<IActionResult> SubmitItem(Guid id, CancellationToken ct) => SubmitAsync(id, ct);

    [HttpPost("api/safety-texts/{id:guid}/withdraw")]
    public Task<IActionResult> WithdrawItem(Guid id, CancellationToken ct) => WithdrawAsync(id, ct);

    [HttpPost("api/safety-texts/{id:guid}/decision")]
    public Task<IActionResult> Decide(Guid id, [FromBody] JsonElement body, CancellationToken ct) => DecideAsync(id, body, ct);

    [HttpPost("api/safety-texts/{id:guid}/new-version")]
    public Task<IActionResult> NewVersion(Guid id, CancellationToken ct) => NewVersionAsync(id, ct);

    [HttpPost("api/safety-texts/{id:guid}/archive")]
    public Task<IActionResult> Archive(Guid id, CancellationToken ct) => ArchiveAsync(id, ct);

    [HttpGet("api/lookups/countries")]
    public Task<IActionResult> Countries(CancellationToken ct) => CountriesAsync(ct);

    /// <summary>
    /// The product picker: the EXISTING MDM global-product selector (the strategy template's source), read here page by
    /// page (MDM caps a page at 100) so the browser gets the whole catalogue in one call and never carries a page cap.
    /// Fail-closed: MDM unreachable / not permitted → 503 <c>dependency_unavailable</c> (the picker is disabled with that
    /// reason), never a partial or invented list.
    /// </summary>
    [HttpGet("api/lookups/products")]
    public async Task<IActionResult> Products(CancellationToken ct)
    {
        if (RequireJson(GlobalProductReadPermission) is { } denied) return denied;
        var items = new List<object>();
        for (var page = 1; page <= MaxProductPages; page++)
        {
            var data = await ReadDataAsync($"/api/global-products/selector?pageSize={ProductPageSize}&pageNumber={page}", ct);
            if (data is not { ValueKind: JsonValueKind.Object } d || !d.TryGetProperty("items", out var rows)
                || rows.ValueKind != JsonValueKind.Array)
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { errors = new[] { "dependency_unavailable", "The MDM product catalogue is not available." } });
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

    private const int ProductPageSize = 100;
    private const int MaxProductPages = 50;

    // ---------------- helpers ----------------

    private static SafetyTextEditViewModel ToModel(Guid id, JsonElement d) => new()
    {
        Id = id,
        SafetyTextCode = Str(d, "safetyTextCode"),
        Version = Num(d, "version"),
        GlobalProductId = Guid.TryParse(Str(d, "globalProductId"), out var p) ? p : null,
        GlobalProductCodeDisplay = Str(d, "globalProductCodeDisplay"),
        CountryCode = Str(d, "countryCode"),
        LanguageCode = Str(d, "languageCode"),
        Body = Str(d, "body"),
        ShortBody = Str(d, "shortBody"),
        SourceDocumentRef = Str(d, "sourceDocumentRef"),
        SourceDate = DateTime.TryParse(Str(d, "sourceDate"), out var sd) ? sd.Date : null,
        ApprovalReference = Str(d, "approvalReference")
    };

    internal static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    internal static int? Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : null;

    internal static bool Flag(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
}
