using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.CRM;
using Diten.Web.Security;
using Diten.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-VW-W3 (W3-WEB-c) — the objection library (<c>/CRM/ObjectionResponses</c>, Content Studio): the list (A1), the editor
/// for a new / draft answer (A2) and the record page (A3, also the Work Center deep link <c>/CRM/ObjectionResponses/{id}</c>)
/// with the workflow actions. All business traffic is proxied server-side through the Gateway to the CrmService under
/// <c>/api/crm/objection-responses</c> (W3-BE-c contract §2.1); the browser never sees a service URL or a token, and CRM
/// decides every rule (completeness, SoD, the approved-claims check, the lock while in review).
/// <list type="bullet">
/// <item><b>Keys.</b> Every proxy requires EXACTLY its CRM endpoint's keys: reads + the decision
/// <c>crm.objection-response.read</c> (whether the actor may decide is the MOD-0023 task's candidacy, which CRM checks);
/// create / update / retire / copy <c>.manage</c>; submit / withdraw <c>.submit</c>. The claim picker also needs
/// <c>crm.claim.read</c>, the product picker <c>mdm.global-products.read</c>.</item>
/// <item><b>Decision.</b> Only <c>{ decision, comment }</c> is forwarded; a "return" without a note is refused HERE too
/// (400 <c>decision_comment_required</c>, no Gateway call) — CRM refuses it as well.</item>
/// <item><b>Categories.</b> Carried by CODE; the labels are the BRD set <c>visit-objection-category</c>'s
/// (<c>label_&lt;lang&gt;</c>, English = the value's display name — the CRM rule), never written here.</item>
/// <item><b>UAS-001.</b> Without the read key a page draws only the access notice — no skeleton, no redirect.</item>
/// </list>
/// </summary>
[Authorize]
[Route("CRM/ObjectionResponses")]
public sealed class ObjectionResponsesController : Controller
{
    public const string Read = "crm.objection-response.read";
    public const string Manage = "crm.objection-response.manage";
    public const string Submit = "crm.objection-response.submit";
    public const string ClaimRead = "crm.claim.read";
    public const string ProductRead = "mdm.global-products.read";

    public const string DecisionCommentRequired = "decision_comment_required";
    public const string DecisionInvalid = "decision_invalid";
    public const string CategorySet = "visit-objection-category";

    private const string CrmBase = "/api/crm/objection-responses";
    private const string ViewRoot = "~/Views/CRM/ObjectionResponses";
    private const int ProductPageSize = 100;
    private const int MaxProductPages = 50;

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<ObjectionResponsesController> _logger;
    private readonly CrmReferenceSetReader _referenceSets;

    public ObjectionResponsesController(
        HttpClient httpClient, IConfiguration configuration, ILogger<ObjectionResponsesController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _logger = logger;
        _referenceSets = new CrmReferenceSetReader(httpClient, _gatewayUrl, logger);
    }

    // ---------------- pages (UAS-001: the view draws only the access notice without the keys) ----------------

    [HttpGet("")]
    public IActionResult Index() => View($"{ViewRoot}/Index.cshtml", Page(null, null));

    [HttpGet("Create")]
    public IActionResult Create([FromQuery] Guid? productId) => View($"{ViewRoot}/Edit.cshtml", Page(null, productId));

    /// <summary>The editor of a draft. A record that is not editable any more opens its record page (the abilities say
    /// so; that is not an authorization redirect — the editor itself is still keyed by <c>.manage</c>).</summary>
    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var model = Page(id, null);
        if (model.CanRead && model.CanManage
            && await ReadDataAsync($"{CrmBase}/{id}", ct) is { ValueKind: JsonValueKind.Object } d
            && !(d.TryGetProperty("abilities", out var a) && a.ValueKind == JsonValueKind.Object
                 && a.TryGetProperty("canEdit", out var e) && e.ValueKind == JsonValueKind.True))
        {
            return RedirectToAction(nameof(Details), new { id });
        }

        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    /// <summary>The record page and the Work Center deep link (<c>/CRM/ObjectionResponses/{id}</c>).</summary>
    [HttpGet("{id:guid}")]
    public IActionResult Details(Guid id) => View($"{ViewRoot}/Details.cshtml", Page(id, null));

    private ObjectionResponsesPageViewModel Page(Guid? id, Guid? productId) => new()
    {
        CanRead = Has(Read),
        CanManage = Has(Manage),
        CanSubmit = Has(Submit),
        CanReadClaims = Has(ClaimRead),
        CanReadProducts = Has(ProductRead),
        Id = id,
        ProductId = productId,
        MeName = KnowledgePathStudioReview.DisplayNameOf(User)
    };

    // ---------------- list (server mode) ----------------

    /// <summary>The list factory's server-mode request (<c>start, length, search</c> + the filters <c>productId,
    /// categoryCode, status</c>) → CRM's page (<c>page, pageSize, q</c>); answers <c>{ items, total, filteredTotal }</c>.
    /// <c>total</c> = the tenant's whole library (asked once more without filters when a filter or a search is on), so the
    /// page tells an empty library from "no result".</summary>
    [HttpGet("api/objection-responses")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        if (!Has(Read)) return Denied();
        var q = Request.Query;
        var start = int.TryParse(q["start"], out var s) ? s : 0;
        var length = int.TryParse(q["length"], out var l) ? l : 10;
        if (start < 0 || length is < 1 or > 100)
        {
            return BadRequest(new { errors = new[] { "page_invalid", "start must be ≥ 0 and length 1..100." } });
        }

        var filters = new List<string>();
        AddQuery(filters, "productId", Guid.TryParse(q["productId"], out var product) ? product.ToString() : null);
        AddQuery(filters, "categoryCode", Clean(q["categoryCode"]));
        AddQuery(filters, "status", Clean(q["status"]));
        AddQuery(filters, "q", Clean(q["search"]));

        var page = start / length + 1;
        var paging = $"page={page}&pageSize={length}";
        var path = $"{CrmBase}?{string.Join('&', filters.Append(paging))}";
        var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
        if (response is null || !response.IsSuccessStatusCode) return await ToProxyResultAsync(response, ct);
        var data = await ReadEnvelopeDataAsync(response, ct);
        var items = data is { ValueKind: JsonValueKind.Object } d && d.TryGetProperty("items", out var it) && it.ValueKind == JsonValueKind.Array
            ? it.Clone()
            : JsonDocument.Parse("[]").RootElement.Clone();
        var filteredTotal = data is { ValueKind: JsonValueKind.Object } dt && dt.TryGetProperty("total", out var t) && t.TryGetInt32(out var n) ? n : 0;

        var total = filteredTotal;
        if (filters.Count > 0)
        {
            var all = await ReadDataAsync($"{CrmBase}?page=1&pageSize=1", ct);
            total = all is { ValueKind: JsonValueKind.Object } a && a.TryGetProperty("total", out var at) && at.TryGetInt32(out var an) ? an : filteredTotal;
        }

        return Ok(new { data = new { items, total, filteredTotal } });
    }

    // ---------------- record reads / writes ----------------

    [HttpGet("api/objection-responses/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => ProxyAsync(HttpMethod.Get, $"{CrmBase}/{id}", null, ct, Read);

    [HttpGet("api/objection-responses/{id:guid}/usages")]
    public Task<IActionResult> Usages(Guid id, CancellationToken ct) => ProxyAsync(HttpMethod.Get, $"{CrmBase}/{id}/usages", null, ct, Read);

    [HttpPost("api/objection-responses")]
    public async Task<IActionResult> CreateItem(CancellationToken ct)
        => await ProxyAsync(HttpMethod.Post, CrmBase, await ReadBodyAsync(ct), ct, Manage);

    [HttpPut("api/objection-responses/{id:guid}")]
    public async Task<IActionResult> UpdateItem(Guid id, CancellationToken ct)
        => await ProxyAsync(HttpMethod.Put, $"{CrmBase}/{id}", await ReadBodyAsync(ct), ct, Manage);

    [HttpPost("api/objection-responses/{id:guid}/submit")]
    public Task<IActionResult> SubmitItem(Guid id, CancellationToken ct) => ProxyAsync(HttpMethod.Post, $"{CrmBase}/{id}/submit", null, ct, Submit);

    [HttpPost("api/objection-responses/{id:guid}/withdraw")]
    public Task<IActionResult> WithdrawItem(Guid id, CancellationToken ct) => ProxyAsync(HttpMethod.Post, $"{CrmBase}/{id}/withdraw", null, ct, Submit);

    [HttpPost("api/objection-responses/{id:guid}/retire")]
    public async Task<IActionResult> RetireItem(Guid id, CancellationToken ct)
        => await ProxyAsync(HttpMethod.Post, $"{CrmBase}/{id}/retire", await ReadBodyAsync(ct), ct, Manage);

    [HttpPost("api/objection-responses/{id:guid}/copy")]
    public Task<IActionResult> CopyItem(Guid id, CancellationToken ct) => ProxyAsync(HttpMethod.Post, $"{CrmBase}/{id}/copy", null, ct, Manage);

    /// <summary>The reviewer's decision on the Work Center task: <c>approve</c> | <c>return</c> (a return needs a note).</summary>
    [HttpPost("api/objection-responses/{id:guid}/decision")]
    public async Task<IActionResult> Decide(Guid id, CancellationToken ct)
    {
        if (!Has(Read)) return Denied();
        var raw = await ReadBodyAsync(ct);
        string? decision = null;
        string? comment = null;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                decision = Str(doc.RootElement, "decision")?.Trim().ToLowerInvariant();
                comment = Str(doc.RootElement, "comment")?.Trim();
            }
        }
        catch (JsonException)
        {
            // falls through to decision_invalid
        }

        if (decision is not ("approve" or "return"))
        {
            return BadRequest(new { errors = new[] { DecisionInvalid, "decision must be approve or return." } });
        }

        if (decision == "return" && string.IsNullOrWhiteSpace(comment))
        {
            return BadRequest(new { errors = new[] { DecisionCommentRequired, "A return to draft needs a note." } });
        }

        var forwarded = JsonSerializer.Serialize(new { decision, comment = string.IsNullOrWhiteSpace(comment) ? null : comment });
        return await ToProxyResultAsync(await SendGatewayAsync(HttpMethod.Post, $"{CrmBase}/{id}/decision", forwarded, ct), ct);
    }

    // ---------------- lookups ----------------

    /// <summary>The categories: the BRD set's active values in BRD order, labelled in the page language
    /// (<c>label_&lt;lang&gt;</c>; English = display name; a missing label falls back to the display name, then the code —
    /// a missing translation is a data-owner item, never patched here). Unreadable ⇒ 503 <c>reference_data_unavailable</c>.</summary>
    [HttpGet("api/lookups/categories")]
    public async Task<IActionResult> Categories(CancellationToken ct)
    {
        if (!Has(Read)) return Denied();
        var values = await CategoryValuesAsync(ct);
        if (values is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { errors = new[] { "reference_data_unavailable", $"{CategorySet} is not available." } });
        }

        return Ok(new { data = values });
    }

    /// <summary>The product picker: the MDM global-product selector read page by page (the SafetyTexts rule). Fail-closed:
    /// MDM unreachable / not permitted ⇒ 503 <c>dependency_unavailable</c>, never a partial list.</summary>
    [HttpGet("api/lookups/products")]
    public async Task<IActionResult> Products(CancellationToken ct)
    {
        if (!Has(Read, ProductRead)) return Denied();
        var items = new List<object>();
        for (var page = 1; page <= MaxProductPages; page++)
        {
            var data = await ReadDataAsync($"/api/global-products/selector?pageSize={ProductPageSize}&pageNumber={page}", ct);
            if (data is not { ValueKind: JsonValueKind.Object } d || !d.TryGetProperty("items", out var rows)
                || rows.ValueKind != JsonValueKind.Array)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { errors = new[] { "dependency_unavailable", "The MDM product catalogue is not available." } });
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

    /// <summary>The claims of ONE product: <c>scope=approved</c> (the picker — approved, live claims only) or
    /// <c>scope=all</c> (the record page names every linked claim, even one that is not approved any more).</summary>
    [HttpGet("api/lookups/claims")]
    public async Task<IActionResult> Claims([FromQuery] Guid? productId, [FromQuery] string? scope, CancellationToken ct)
    {
        if (!Has(Read, ClaimRead)) return Denied();
        if (productId is null || productId == Guid.Empty) return Ok(new { data = Array.Empty<object>() });
        var approvedOnly = !string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase);
        var path = $"/api/crm/content-composition/claims?productId={productId}"
                   + (approvedOnly ? "&status=approved&includeArchived=false" : "&includeArchived=true");
        var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
        if (response is null || !response.IsSuccessStatusCode) return await ToProxyResultAsync(response, ct);
        var data = await ReadEnvelopeDataAsync(response, ct);
        var rows = data is { ValueKind: JsonValueKind.Object } d && d.TryGetProperty("items", out var it) && it.ValueKind == JsonValueKind.Array
            ? it.EnumerateArray().ToList()
            : data is { ValueKind: JsonValueKind.Array } arr ? arr.EnumerateArray().ToList() : new List<JsonElement>();
        var claims = new List<object>();
        foreach (var c in rows)
        {
            if (c.ValueKind != JsonValueKind.Object) continue;
            claims.Add(new
            {
                id = Str(c, "claimId"),
                code = Str(c, "claimCode"),
                name = Str(c, "claimName"),
                text = Str(c, "claimText"),
                status = Str(c, "status"),
                productId = Str(c, "productId"),
                isArchived = c.TryGetProperty("isArchived", out var ar) && ar.ValueKind == JsonValueKind.True
            });
        }

        return Ok(new { data = claims });
    }

    // ---------------- shared with the Knowledge Path Studio ----------------

    /// <summary>The category values for the page language (also read by the Studio's objection picker).</summary>
    internal async Task<IReadOnlyList<object>?> CategoryValuesAsync(CancellationToken ct)
        => await ReadCategoryValuesAsync(_referenceSets, Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request), GetTenantId(), ct);

    internal static async Task<IReadOnlyList<object>?> ReadCategoryValuesAsync(
        CrmReferenceSetReader reader, string? token, string? tenantId, CancellationToken ct)
    {
        using var response = await reader.ReadAsync(CategorySet, token, tenantId, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        JsonElement? data;
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            data = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var dd) ? dd.Clone() : root.Clone();
        }
        catch (JsonException)
        {
            return null;
        }

        var values = ReferenceValueSet.Parse(data);
        if (values is null) return null;
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return values.Select(v => (object)new { code = v.Code, label = CategoryLabel(v, language) }).ToList();
    }

    /// <summary>The CRM rule (<c>GetVisitReasonsHandler.LabelOf</c>): <c>label_&lt;lang&gt;</c>, English = display name,
    /// then display name, then the code.</summary>
    internal static string CategoryLabel(ReferenceValueSet.Value value, string? language)
    {
        if (!string.IsNullOrWhiteSpace(language) && !string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)
            && value.Attributes.TryGetValue("label_" + language.ToLowerInvariant(), out var label) && !string.IsNullOrWhiteSpace(label))
        {
            return label;
        }

        return string.IsNullOrWhiteSpace(value.Name) ? value.Code : value.Name;
    }

    // ---------------- proxy helpers ----------------

    private async Task<IActionResult> ProxyAsync(HttpMethod method, string path, string? rawBody, CancellationToken ct, params string[] all)
    {
        if (!Has(all)) return Denied();
        if (rawBody is not null && ContainsTenantId(rawBody))
        {
            return BadRequest(new { errors = new[] { "TenantId is server-resolved and must not be supplied." } });
        }

        return await ToProxyResultAsync(await SendGatewayAsync(method, path, rawBody, ct), ct);
    }

    private ObjectResult Denied() => StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });

    private async Task<string?> ReadBodyAsync(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;
        return string.IsNullOrWhiteSpace(body) ? null : body;
    }

    private async Task<JsonElement?> ReadDataAsync(string path, CancellationToken ct)
    {
        using var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
        return response is null || !response.IsSuccessStatusCode ? null : await ReadEnvelopeDataAsync(response, ct);
    }

    private static async Task<JsonElement?> ReadEnvelopeDataAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            return root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var d) ? d.Clone() : root.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<HttpResponseMessage?> SendGatewayAsync(HttpMethod method, string path, string? rawBody, CancellationToken ct)
    {
        try
        {
            var tenantId = GetTenantId();
            if (string.IsNullOrWhiteSpace(tenantId)) return null;
            using var request = new HttpRequestMessage(method, $"{_gatewayUrl}{path}");
            var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);
            var language = Request.Headers.AcceptLanguage.ToString();
            if (!string.IsNullOrWhiteSpace(language))
            {
                request.Headers.TryAddWithoutValidation("Accept-Language", language);
            }

            if (rawBody is not null)
            {
                request.Content = new StringContent(rawBody, Encoding.UTF8, "application/json");
            }
            else if (method == HttpMethod.Post || method == HttpMethod.Put)
            {
                // submit / withdraw / copy carry no body: send an empty JSON object, never a bodiless POST.
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            }

            return await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Objection library Gateway request failed: {Method} {Path}", method, path);
            return null;
        }
    }

    private static async Task<IActionResult> ToProxyResultAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null)
        {
            return new ObjectResult(new { errors = new[] { "Gateway unavailable." } }) { StatusCode = 502 };
        }

        using (response)
        {
            if (IsBodilessStatus(response.StatusCode)) return new StatusCodeResult((int)response.StatusCode);
            var content = await response.Content.ReadAsStringAsync(ct);
            if (CrmVisitExecutionController.ProblemDetailsToEnvelope(
                    response.Content.Headers.ContentType?.MediaType, content, (int)response.StatusCode) is { } envelope)
            {
                return new ContentResult { StatusCode = (int)response.StatusCode, ContentType = "application/json", Content = envelope };
            }

            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                Content = content
            };
        }
    }

    private static bool IsBodilessStatus(HttpStatusCode status)
        => (int)status is >= 100 and < 200 || status is HttpStatusCode.NoContent or HttpStatusCode.ResetContent or HttpStatusCode.NotModified;

    private static bool ContainsTenantId(string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.EnumerateObject().Any(x => string.Equals(x.Name, "tenantId", StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void AddQuery(List<string> query, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) query.Add($"{name}={Uri.EscapeDataString(value)}");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Str(JsonElement e, string name)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private string? GetTenantId() => User.Claims.FirstOrDefault(x =>
        x.Type == "tenantId" || x.Type == "tenant_id" || x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    private bool Has(params string[] permissions) => permissions.All(x => PermissionClaims.HasPermission(User, x));
}
