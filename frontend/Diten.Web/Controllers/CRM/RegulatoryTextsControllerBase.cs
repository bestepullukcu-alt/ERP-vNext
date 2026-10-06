using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-KP-5a-UI — what the two Regulatory master-data screens (Safety Texts, Country Legal Profiles; WP-KP-5a contract)
/// share. All business traffic is proxied server-side through Gateway 5000 to the CrmService under
/// <c>/api/crm/knowledge/*</c> (the existing ocelot route — no new gateway route); the browser never sees a service URL
/// or a token, and CRM stays the authority for every rule (SoD, single active version, the Regulatory decision).
/// <list type="bullet">
/// <item><b>List (server mode).</b> CRM answers the tenant's rows of the resource; this layer pages, searches, sorts
/// and filters them and answers the server-mode contract <c>{ items, total, filteredTotal }</c> the list factory
/// reads, so the browser never holds the whole set.</item>
/// <item><b>Decision (K1).</b> Approve / reject goes to CRM's decision endpoint, which closes the MOD-0023 task. A
/// rejection without a comment is refused HERE too (400 <c>rejection_comment_required</c>, no gateway call); CRM
/// refuses it as well.</item>
/// <item><b>UAS-001.</b> Pages without the read key answer a plain 403 — no skeleton, no redirect.</item>
/// </list>
/// </summary>
public abstract class RegulatoryTextsControllerBase : Controller
{
    public const string RejectionCommentRequired = "rejection_comment_required";
    public const string DecisionInvalid = "decision_invalid";

    /// <summary>The grid columns a client may sort by (the list factory sends the column's <c>data</c> name).</summary>
    public static readonly IReadOnlyList<string> SortableColumns =
        ["code", "productCode", "countryCode", "languageCode", "version", "status", "updatedAt"];

    /// <summary>The lifecycle words of WP-KP-5a (CRM's own vocabulary; the status filter offers exactly these).</summary>
    public static readonly IReadOnlyList<string> Statuses = ["draft", "in-review", "active", "superseded", "archived"];

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger _logger;
    protected readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    protected RegulatoryTextsControllerBase(HttpClient httpClient, IConfiguration configuration, ILogger logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _logger = logger;
    }

    /// <summary>CRM resource base, e.g. <c>/api/crm/knowledge/safety-texts</c>.</summary>
    protected abstract string CrmBase { get; }
    protected abstract string ReadPermission { get; }
    protected abstract string ManagePermission { get; }
    protected abstract string SubmitPermission { get; }

    // ---------------- page helpers ----------------

    protected bool CanManage => HasPermission(ManagePermission);
    protected bool CanSubmit => HasPermission(SubmitPermission);

    protected IActionResult? RequirePage(string permission) =>
        HasPermission(permission) ? null : StatusCode(StatusCodes.Status403Forbidden);

    protected async Task<JsonElement?> ReadDetailAsync(Guid id, CancellationToken ct) =>
        await ReadDataAsync($"{CrmBase}/{id}", ct);

    // ---------------- JSON endpoints (called by the derived controllers' explicit routes) ----------------

    /// <summary>Server-mode list: start / length / search / orderBy / orderDir + the filters (productId, countryCode,
    /// languageCode, status[], includeArchived). <c>total</c> = the tenant's rows of the resource (archived ones only when
    /// asked), <c>filteredTotal</c> = after filters + search.</summary>
    protected async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        if (RequireJson(ReadPermission) is { } denied) return denied;
        var q = Request.Query;
        var orderBy = q["orderBy"].ToString();
        var orderDir = q["orderDir"].ToString();
        if (!string.IsNullOrEmpty(orderBy) && !SortableColumns.Contains(orderBy, StringComparer.Ordinal))
            return BadRequest(new { errors = new[] { "order_by_invalid", $"orderBy '{orderBy}' is not sortable." } });
        if (!string.IsNullOrEmpty(orderDir) && orderDir is not ("asc" or "desc"))
            return BadRequest(new { errors = new[] { "order_dir_invalid", "orderDir must be asc or desc." } });
        var start = int.TryParse(q["start"], out var s) ? s : 0;
        var length = int.TryParse(q["length"], out var l) ? l : 10;
        if (start < 0 || length is < 1 or > 500)
            return BadRequest(new { errors = new[] { "page_invalid", "start must be ≥ 0 and length 1..500." } });

        var includeArchived = string.Equals(q["includeArchived"], "true", StringComparison.OrdinalIgnoreCase);
        var response = await SendAsync(HttpMethod.Get, $"{CrmBase}?includeArchived={(includeArchived ? "true" : "false")}", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return await ToProxyResultAsync(response, ct);
        var rows = ItemsOf(await ReadEnvelopeDataAsync(response, ct)).Select(NormalizeRow).ToList();

        var filtered = ApplyFilters(rows, q["productId"], q["countryCode"], q["languageCode"],
            q["status"].Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).ToList(), q["search"]);
        var ordered = Order(filtered, string.IsNullOrEmpty(orderBy) ? "code" : orderBy, orderDir == "desc");
        return Ok(new
        {
            data = new
            {
                items = ordered.Skip(start).Take(length).ToList(),
                total = rows.Count,
                filteredTotal = filtered.Count
            }
        });
    }

    protected Task<IActionResult> GetAsync(Guid id, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Get, $"{CrmBase}/{id}", null, ReadPermission, ct);

    protected Task<IActionResult> CreateAsync(JsonElement body, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Post, CrmBase, body, ManagePermission, ct);

    protected Task<IActionResult> UpdateAsync(Guid id, JsonElement body, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Put, $"{CrmBase}/{id}", body, ManagePermission, ct);

    protected Task<IActionResult> SubmitAsync(Guid id, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Post, $"{CrmBase}/{id}/submit", null, SubmitPermission, ct);

    protected Task<IActionResult> WithdrawAsync(Guid id, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Post, $"{CrmBase}/{id}/withdraw", null, SubmitPermission, ct);

    protected Task<IActionResult> NewVersionAsync(Guid id, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Post, $"{CrmBase}/{id}/new-version", null, ManagePermission, ct);

    protected Task<IActionResult> ArchiveAsync(Guid id, CancellationToken ct) =>
        ProxyAsync(HttpMethod.Post, $"{CrmBase}/{id}/archive", null, ManagePermission, ct);

    /// <summary>The decision (K1). Read key only: whether the actor may decide is the MOD-0023 task's candidacy, which
    /// CRM checks (403 / <c>self_decision_forbidden</c>). Only <c>{ outcome, comment }</c> is forwarded.</summary>
    protected async Task<IActionResult> DecideAsync(Guid id, JsonElement body, CancellationToken ct)
    {
        if (RequireJson(ReadPermission) is { } denied) return denied;
        var outcome = Text(body, "outcome")?.Trim().ToLowerInvariant();
        var comment = Text(body, "comment")?.Trim();
        if (outcome is not ("approve" or "reject"))
            return BadRequest(new { errors = new[] { DecisionInvalid, "outcome must be approve or reject." } });
        if (outcome == "reject" && string.IsNullOrWhiteSpace(comment))
            return BadRequest(new { errors = new[] { RejectionCommentRequired, "A rejection needs a comment." } });
        var forwarded = JsonSerializer.SerializeToElement(new { outcome, comment = string.IsNullOrWhiteSpace(comment) ? null : comment }, Json);
        return await ToProxyResultAsync(await SendAsync(HttpMethod.Post, $"{CrmBase}/{id}/decision", forwarded, ct), ct);
    }

    /// <summary>The active version of a key (KP-UI-3 will read it); only the identity parameters are forwarded.</summary>
    protected Task<IActionResult> ResolveAsync(string? productId, string? countryCode, string? languageCode, CancellationToken ct)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(productId)) parts.Add($"productId={Uri.EscapeDataString(productId)}");
        parts.Add($"countryCode={Uri.EscapeDataString(countryCode ?? string.Empty)}");
        parts.Add($"languageCode={Uri.EscapeDataString(languageCode ?? string.Empty)}");
        return ProxyAsync(HttpMethod.Get, $"{CrmBase}/resolve?{string.Join('&', parts)}", null, ReadPermission, ct);
    }

    /// <summary>The country axis (COUNTRY_CODES + country-content-languages) — the knowledge path identity picker's
    /// source (KP-UI-1), so a country and its content languages are offered exactly as CRM validates them.</summary>
    protected async Task<IActionResult> CountriesAsync(CancellationToken ct)
    {
        if (RequireJson(ReadPermission) is { } denied) return denied;
        var countries = ReferenceValueSet.Parse(await ReadReferenceSetDataAsync("COUNTRY_CODES", ct));
        if (countries is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { errors = new[] { "reference_set_unavailable", "COUNTRY_CODES is not available." } });
        var languages = ReferenceValueSet.Parse(
            await ReadReferenceSetDataAsync("country-content-languages", ct));
        return Ok(new
        {
            data = ReferenceValueSet.Countries(countries, languages).Select(c => new
            {
                code = c.Code,
                name = c.Name,
                languages = c.LanguageDetails.Select(x => new { code = x.Code, name = x.Name }).ToList()
            }).ToList()
        });
    }

    // ---------------- list shaping (pure; tested through ListAsync) ----------------

    internal sealed record Row(
        string Id, string Code, string? ProductId, string? ProductCode, string? CountryCode, string? LanguageCode,
        int Version, string Status, DateTimeOffset? UpdatedAt, bool IsActive, bool CanEdit, bool CanSubmit);

    internal static Row NormalizeRow(JsonElement e) => new(
        Text(e, "id", "safetyTextId", "countryLegalProfileId", "legalProfileId") ?? string.Empty,
        Text(e, "safetyTextCode", "countryLegalProfileCode", "legalProfileCode", "profileCode", "code") ?? string.Empty,
        Text(e, "globalProductId", "productId"),
        Text(e, "globalProductCodeDisplay", "productCode"),
        Text(e, "countryCode"),
        Text(e, "languageCode"),
        Int(e, "version"),
        Text(e, "status") ?? string.Empty,
        Date(e, "updatedAt") ?? Date(e, "createdAt"),
        Bool(e, "isActive"),
        Bool(e, "canEdit"),
        Bool(e, "canSubmit"));

    internal static List<Row> ApplyFilters(
        IEnumerable<Row> rows, string? productId, string? countryCode, string? languageCode,
        IReadOnlyCollection<string> statuses, string? search)
    {
        bool Same(string? a, string? b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
        var term = search?.Trim();
        return rows.Where(r =>
                (string.IsNullOrWhiteSpace(productId) || Same(r.ProductId, productId))
                && (string.IsNullOrWhiteSpace(countryCode) || Same(r.CountryCode, countryCode))
                && (string.IsNullOrWhiteSpace(languageCode) || Same(r.LanguageCode, languageCode))
                && (statuses.Count == 0 || statuses.Any(s => Same(s, r.Status)))
                && (string.IsNullOrEmpty(term)
                    || new[] { r.Code, r.ProductCode, r.CountryCode, r.LanguageCode, r.Status }
                        .Any(v => v?.Contains(term, StringComparison.OrdinalIgnoreCase) == true)))
            .ToList();
    }

    internal static List<Row> Order(IEnumerable<Row> rows, string orderBy, bool descending)
    {
        Func<Row, IComparable?> key = orderBy switch
        {
            "productCode" => r => r.ProductCode ?? string.Empty,
            "countryCode" => r => r.CountryCode ?? string.Empty,
            "languageCode" => r => r.LanguageCode ?? string.Empty,
            "version" => r => r.Version,
            "status" => r => r.Status,
            "updatedAt" => r => r.UpdatedAt ?? DateTimeOffset.MinValue,
            _ => r => r.Code
        };
        var sorted = descending ? rows.OrderByDescending(key) : rows.OrderBy(key);
        // Ties never shuffle between pages: the order always ends with the id.
        return sorted.ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
    }

    private static IEnumerable<JsonElement> ItemsOf(JsonElement? data)
    {
        if (data is { ValueKind: JsonValueKind.Array } array) return array.EnumerateArray().ToList();
        if (data is { ValueKind: JsonValueKind.Object } obj && obj.TryGetProperty("items", out var items)
            && items.ValueKind == JsonValueKind.Array) return items.EnumerateArray().ToList();
        return [];
    }

    private static string? Text(JsonElement e, params string[] names)
    {
        if (e.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in names)
        {
            foreach (var p in e.EnumerateObject())
            {
                if (!string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
                if (p.Value.ValueKind == JsonValueKind.String) return p.Value.GetString();
                if (p.Value.ValueKind is JsonValueKind.Number) return p.Value.GetRawText();
            }
        }

        return null;
    }

    private static int Int(JsonElement e, string name) =>
        int.TryParse(Text(e, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static DateTimeOffset? Date(JsonElement e, string name) =>
        DateTimeOffset.TryParse(Text(e, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d : null;

    private static bool Bool(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object
        && e.EnumerateObject().Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.True);

    // ---------------- gateway plumbing ----------------

    protected async Task<IActionResult> ProxyAsync(
        HttpMethod method, string path, JsonElement? body, string permission, CancellationToken ct)
    {
        if (RequireJson(permission) is { } denied) return denied;
        if (body is { ValueKind: JsonValueKind.Object } b
            && b.EnumerateObject().Any(p => string.Equals(p.Name, "tenantId", StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { errors = new[] { "TenantId is server-resolved and must not be supplied." } });
        return await ToProxyResultAsync(await SendAsync(method, path, body, ct), ct);
    }

    /// <summary>WP-BRD-TENANT-CRM-SETS — a reference set through the shared <see cref="Diten.Web.Services.CrmReferenceSetReader"/>
    /// (consumable-sets first, so any tenant role reads it). Returns the envelope's <c>data</c>, or null when unavailable.</summary>
    protected async Task<JsonElement?> ReadReferenceSetDataAsync(string setCode, CancellationToken ct)
    {
        using var response = await new Diten.Web.Services.CrmReferenceSetReader(_httpClient, _gatewayUrl, _logger).ReadAsync(
            setCode, Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request), GetTenantId(), ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return await ReadEnvelopeDataAsync(response, ct);
    }
    protected async Task<JsonElement?> ReadDataAsync(string path, CancellationToken ct)
    {
        var response = await SendAsync(HttpMethod.Get, path, null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return await ReadEnvelopeDataAsync(response, ct);
    }

    private static async Task<JsonElement?> ReadEnvelopeDataAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in root.EnumerateObject())
                {
                    if (string.Equals(p.Name, "data", StringComparison.OrdinalIgnoreCase)) return p.Value.Clone();
                }
            }

            return root.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<HttpResponseMessage?> SendAsync(HttpMethod method, string path, JsonElement? body, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, $"{_gatewayUrl}{path}");
            var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var tenantId = GetTenantId();
            if (string.IsNullOrWhiteSpace(tenantId)) return null;
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);
            if (body is { } b) request.Content = new StringContent(b.GetRawText(), Encoding.UTF8, "application/json");
            return await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Regulatory text Gateway request failed: {Method} {Path}", method, path);
            return null;
        }
    }

    private static async Task<IActionResult> ToProxyResultAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null)
            return new ObjectResult(new { errors = new[] { "dependency_unavailable", "Gateway unavailable." } }) { StatusCode = 502 };

        // A bodiless status stays bodiless: a body on a 204/205/304/1xx makes Kestrel throw (Content-Length) → 500.
        var status = (int)response.StatusCode;
        if (status is >= 100 and < 200 || response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.ResetContent or HttpStatusCode.NotModified)
            return new StatusCodeResult(status);

        return new ContentResult
        {
            StatusCode = status,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            Content = await response.Content.ReadAsStringAsync(ct)
        };
    }

    private string? GetTenantId() => User.Claims.FirstOrDefault(x =>
        x.Type == "tenantId" || x.Type == "tenant_id" ||
        x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    protected bool HasPermission(string permission) => PermissionClaims.HasPermission(User, permission);

    protected IActionResult? RequireJson(string permission) =>
        HasPermission(permission) ? null : StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
}
