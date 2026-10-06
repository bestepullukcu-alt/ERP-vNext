using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.Web.Security;
using Diten.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// Claims console (CAND-CAP-0011 / claims v2). Proxy-only: all business traffic is proxied server-side through Gateway
/// 5000, the browser never sees a service URL or bearer token, and no business rule lives here (CrmService is the
/// authoritative validation + permission layer). No delete surface — closing a claim is Archive.
/// <para>WP-CL-FE-3 — Create / Edit are JS-driven pages over the v2 proxy layer (<see cref="ClaimsController"/> V2 part):
/// the controller renders only the page shell (kind / id), claims are saved through <c>api/v2/claims</c>, evidence is
/// linked through MOD-0031 via CRM, and approval runs only through the MOD-0023 round (submit-review). The former MVC
/// form post is gone.</para>
/// </summary>
[Authorize]
[Route("CRM/Claims")]
public sealed partial class ClaimsController : Controller
{
    private const string ReadPermission = "crm.claim.read";
    private const string ManagePermission = "crm.claim.manage";
    private const string ViewRoot = "~/Views/CRM/Claims";

    // The claim HTTP surface (SCMM-12-API) + the component picker source (MOD-0162 FU02), both through the gateway.
    private const string ClaimsBase = "/api/crm/content-composition/claims";
    private const string ContentsBase = "/api/crm/knowledge/contents";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<ClaimsController> _logger;
    private readonly CrmReferenceSetReader _referenceSets;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public ClaimsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<ClaimsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
        _referenceSets = new CrmReferenceSetReader(httpClient, _gatewayUrl, logger);
    }

    // ---------------- Pages (list + JS-driven Create / Edit) ----------------

    [HttpGet("")]
    public IActionResult Index()
    {
        // UAS-001: without the read permission no page skeleton is drawn and nothing redirects — a plain 403.
        if (RequirePage(ReadPermission) is { } denied) return denied;
        ViewData["CanManageClaims"] = HasAnyPermission(ManagePermission);
        return View($"{ViewRoot}/Index.cshtml");
    }

    /// <summary>WP-CL-FE-2 — claim × country coverage matrix (mockup scenario 2). Read gate like the list (UAS-001: a
    /// plain 403 without a skeleton); cell actions render only for crm.claim.manage and CRM re-checks them anyway.</summary>
    [HttpGet("Coverage")]
    public IActionResult Coverage()
    {
        if (RequirePage(ReadPermission) is { } denied) return denied;
        ViewData["CanManageClaims"] = HasAnyPermission(ManagePermission);
        return View($"{ViewRoot}/Coverage.cshtml");
    }

    /// <summary>New core (default) or local claim. Only the kind travels to the page; everything else is loaded and
    /// saved by claim-form.js through the v2 proxy.</summary>
    [HttpGet("Create")]
    public IActionResult Create([FromQuery] string? kind)
    {
        if (RequirePage(ManagePermission) is { } denied) return denied;
        ViewData["ClaimKind"] = string.Equals(kind, "local", StringComparison.OrdinalIgnoreCase) ? "local" : "core";
        ViewData["ClaimId"] = string.Empty;
        return View($"{ViewRoot}/Create.cshtml");
    }

    [HttpGet("Edit/{id:guid}")]
    public IActionResult Edit(Guid id)
    {
        if (RequirePage(ManagePermission) is { } denied) return denied;
        ViewData["ClaimKind"] = string.Empty; // read from the record
        ViewData["ClaimId"] = id.ToString();
        return View($"{ViewRoot}/Edit.cshtml");
    }

    // ---------------- WP-CL-FE-4 — country version pages + code suggestion ----------------

    /// <summary>Open a country version of a claim (mockup scenario 4). Read opens the page read-only; the save / submit
    /// controls render only for crm.claim.manage (CRM re-checks every write).</summary>
    [HttpGet("{claimId:guid}/Countries/{countryCode}/Create")]
    public IActionResult CountryVersionCreate(Guid claimId, string countryCode)
    {
        if (RequirePage(ReadPermission) is { } denied) return denied;
        ViewData["CanManageClaims"] = HasAnyPermission(ManagePermission);
        ViewData["ClaimId"] = claimId.ToString();
        ViewData["CountryCode"] = (countryCode ?? string.Empty).Trim().ToUpperInvariant();
        ViewData["CountryVersionId"] = string.Empty;
        return View($"{ViewRoot}/CountryVersion.cshtml");
    }

    [HttpGet("CountryVersions/{versionId:guid}/Edit")]
    public IActionResult CountryVersionEdit(Guid versionId)
    {
        if (RequirePage(ReadPermission) is { } denied) return denied;
        ViewData["CanManageClaims"] = HasAnyPermission(ManagePermission);
        ViewData["ClaimId"] = string.Empty; // read from the version
        ViewData["CountryCode"] = string.Empty;
        ViewData["CountryVersionId"] = versionId.ToString();
        return View($"{ViewRoot}/CountryVersion.cshtml");
    }

    /// <summary>CLM-{PRODUCT NAME}-{NN} suggestion for a new claim (see <see cref="ClaimCodeSuggestion"/>); NN follows
    /// the tenant's existing codes (one list read). A failed read still suggests NN = 01.</summary>
    [HttpGet("api/v2/claims/code-suggestion")]
    public async Task<IActionResult> CodeSuggestion([FromQuery] string? productName, CancellationToken ct)
    {
        if (RequireJson(ManagePermission) is { } denied) return denied;
        var existing = new List<string>();
        var response = await SendGatewayAsync(HttpMethod.Get, $"{ClaimsBase}?includeArchived=true", null, ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            try
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                if (doc.RootElement.TryGetProperty("data", out var data)
                    && data.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    existing.AddRange(items.EnumerateArray().Select(i => GetFirstString(i, "claimCode")).OfType<string>());
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Claim code suggestion: the claim list could not be parsed.");
            }
        }

        return Ok(new { data = new { code = ClaimCodeSuggestion.Suggest(productName, existing) } });
    }

    // ---------------- Same-origin browser proxy (claim + component-picker allowlist only) ----------------

    // WP-CL-FE-1 — the old list door now serves the v2 list (same CRM read; kept so older bookmarks / scripts still
    // work). The old direct approve proxy is GONE: claims are approved only through their MOD-0023 workflow round
    // (submit-review). Archive moved to the v2 layer (api/v2/claims/{id}/archive).
    [HttpGet("api/claims")]
    public Task<IActionResult> ClaimList(CancellationToken ct) => V2List(ct);

    // Component picker source — read-only MOD-0162 KnowledgeContent list. Only the list read is allowlisted (no
    // knowledge write path is exposed here).
    [HttpGet("api/contents")]
    public Task<IActionResult> ContentList(CancellationToken ct) =>
        ProxyGetAsync($"{ContentsBase}{Request.QueryString}", ReadPermission, ct);

    // ---------------- helpers ----------------

    private async Task<IActionResult> ProxyGetAsync(string path, string permission, CancellationToken ct)
    {
        if (RequireJson(permission) is { } denied) return denied;
        return await ToProxyResultAsync(await SendGatewayAsync(HttpMethod.Get, path, null, ct), ct);
    }

    private async Task<IActionResult> ProxyJsonAsync(
        HttpMethod method, string path, JsonElement? body, string permission, CancellationToken ct)
    {
        if (RequireJson(permission) is { } denied) return denied;
        if (body.HasValue && ContainsTenantId(body.Value))
            return BadRequest(new { errors = new[] { "TenantId is server-resolved and must not be supplied." } });
        return await ToProxyResultAsync(await SendGatewayAsync(method, path, body, ct), ct);
    }

    private async Task<HttpResponseMessage?> SendGatewayAsync(HttpMethod method, string path, object? body, CancellationToken ct)
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

            if (body is not null)
            {
                var jsonBody = body is JsonElement element ? element.GetRawText() : JsonSerializer.Serialize(body, _json);
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            }

            return await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Claim Gateway request failed: {Method} {Path}", method, path);
            return null;
        }
    }

    private static async Task<IActionResult> ToProxyResultAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null)
            return new ObjectResult(new { errors = new[] { "Gateway unavailable." } }) { StatusCode = 502 };
        // A bodiless status (204 / 304) must stay bodiless: writing even an empty ContentResult with a content type
        // onto a 204 is what turned proxied 204s into 500s elsewhere (memory proxy-forward-204-content-length-crash).
        if (IsBodilessStatus((int)response.StatusCode))
            return new StatusCodeResult((int)response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(ct);
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            Content = content
        };
    }

    internal static bool IsBodilessStatus(int status) => status is 204 or 205 or 304 || status < 200;

    private static bool ContainsTenantId(JsonElement element) => element.ValueKind == JsonValueKind.Object &&
        element.EnumerateObject().Any(x => string.Equals(x.Name, "tenantId", StringComparison.OrdinalIgnoreCase));

    private string? GetTenantId() => User.Claims.FirstOrDefault(x =>
        x.Type == "tenantId" || x.Type == "tenant_id" ||
        x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    private static string? GetFirstString(JsonElement el, params string[] names)
    {
        foreach (var n in names)
        {
            if (el.TryGetProperty(n, out var p) && p.ValueKind == JsonValueKind.String)
            {
                var v = p.GetString();
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }
        }
        return null;
    }

    private bool HasAnyPermission(params string[] permissions) => permissions.Any(x => PermissionClaims.HasPermission(User, x));

    private IActionResult? RequirePage(string permission) =>
        HasAnyPermission(permission) ? null : StatusCode(StatusCodes.Status403Forbidden);

    private IActionResult? RequireJson(string permission) =>
        HasAnyPermission(permission)
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
}
