using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.Web.Models.CRM;
using Diten.Web.Security;
using Diten.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// MOD-0155 FU05 — the MicroTarget Visit Planning SETUP console (D-UI = B, bespoke tenant-shell). All business traffic is
/// proxied server-side through the Gateway; the browser never sees a service URL or a bearer token, and the CrmService
/// runtime stays the authoritative permission + validation layer. This is NOT a Golden DataTable surface
/// (verify_datatable_page N/A) — it is a selection + generation workflow: pick a period, accounts and doctors, preview a
/// Day/Week grid + the supply-vs-demand warning, apply, re-plan.
/// <para>The FU05 keys carry no dev fallback (fail-closed, mirroring FU03): each proxy answers 403 until the key is
/// granted (F-RBAC). apply / re-plan require BOTH <c>crm.visit-plan.apply</c> AND FU01 <c>crm.planned-visit.manage</c>.</para>
/// </summary>
[Authorize]
[Route("CRM/VisitPlanning")]
public sealed class VisitPlanningController : Controller
{
    private const string ReadPermission = "crm.visit-plan.read";
    private const string GeneratePermission = "crm.visit-plan.generate";
    private const string ApplyPermission = "crm.visit-plan.apply";
    private const string PlannedVisitManage = "crm.planned-visit.manage";
    // WP-VP-4J (4) — reads every rep's plans (CRM VisitPlanningPermissions.ReadAll); the list's "Rep" filter is its only.
    private const string ReadAllPermission = "crm.visit-plan.read-all";
    private const string ViewRoot = "~/Views/CRM/VisitPlanning";

    // WP-VP-FIX-1 (D6) — the MOD-0048 sets whose labels replace raw codes on the Targets + Route tabs.
    internal const string AccountTypeSetCode = "account-type";
    internal const string MedicalSpecialtySetCode = "medical-specialty";
    // WP-VP-4I (8) — the province ("il") labels, keyed by the territory area code ("TR-34-ISTANBUL").
    internal const string CitySetCode = "city";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<VisitPlanningController> _logger;

    public VisitPlanningController(
        HttpClient httpClient, IConfiguration configuration, ILogger<VisitPlanningController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _logger = logger;
    }

    // ---------------- page ----------------

    [HttpGet("")]
    public IActionResult Index()
    {
        if (!HasAnyPermission(ReadPermission))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return View($"{ViewRoot}/Index.cshtml", new VisitPlanningIndexViewModel
        {
            CanGenerate = HasAnyPermission(GeneratePermission),
            CanApply = HasAnyPermission(ApplyPermission) && HasAnyPermission(PlannedVisitManage),
            CanReadAll = HasAnyPermission(ReadAllPermission)
        });
    }

    // Golden Compact authoring/reading pages. The session data itself is loaded client-side through the same-origin
    // /api/sessions proxy (form.js / details.js); these actions only render the shell + carry the permission flags.
    [HttpGet("Create")]
    public IActionResult Create()
    {
        if (!HasAnyPermission(GeneratePermission))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return View($"{ViewRoot}/Create.cshtml", new VisitPlanningSessionPageViewModel
        {
            CanGenerate = HasAnyPermission(GeneratePermission),
            CanApply = HasAnyPermission(ApplyPermission) && HasAnyPermission(PlannedVisitManage)
        });
    }

    // WP-VP-FIX-1 (D2) — a committed / archived plan has nothing left to edit: the Edit page sends the reader back to the
    // (read-only) Details page instead of opening a form whose save CRM would refuse with 409.
    [HttpGet("Edit/{planningSessionId:guid}")]
    public async Task<IActionResult> Edit(Guid planningSessionId, CancellationToken ct)
    {
        if (!HasAnyPermission(GeneratePermission))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (IsLockedStatus(await ReadSessionStatusAsync(planningSessionId, ct)))
        {
            return RedirectToAction(nameof(Details), new { planningSessionId });
        }

        return View($"{ViewRoot}/Edit.cshtml", new VisitPlanningSessionPageViewModel
        {
            SessionId = planningSessionId,
            CanGenerate = HasAnyPermission(GeneratePermission),
            CanApply = HasAnyPermission(ApplyPermission) && HasAnyPermission(PlannedVisitManage)
        });
    }

    // WP-VP-FIX-1 (D2) — a committed / archived plan is READ-ONLY: every write affordance (save as the week's plan, save
    // targets, edit, generate route, re-plan, target checkboxes) is switched off server-side, the page says why, and the
    // route stays viewable. CRM still refuses those writes on its own (409); this only stops offering them.
    [HttpGet("Details/{planningSessionId:guid}")]
    public async Task<IActionResult> Details(Guid planningSessionId, CancellationToken ct)
    {
        if (!HasAnyPermission(ReadPermission))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var readOnly = IsLockedStatus(await ReadSessionStatusAsync(planningSessionId, ct));
        return View($"{ViewRoot}/Details.cshtml", new VisitPlanningSessionPageViewModel
        {
            SessionId = planningSessionId,
            IsReadOnly = readOnly,
            CanGenerate = !readOnly && HasAnyPermission(GeneratePermission),
            CanApply = !readOnly && HasAnyPermission(ApplyPermission) && HasAnyPermission(PlannedVisitManage)
        });
    }

    /// <summary>committed / archived — the statuses a plan can no longer be edited, applied or re-targeted in.</summary>
    internal static bool IsLockedStatus(string? status)
        => string.Equals(status, "committed", StringComparison.OrdinalIgnoreCase)
           || string.Equals(status, "archived", StringComparison.OrdinalIgnoreCase);

    // ---------------- generation proxies ----------------

    [HttpPost("api/preview")]
    public async Task<IActionResult> Preview(CancellationToken ct)
        => await ProxyBodyAsync(HttpMethod.Post, "/api/crm/visit-plan/preview", GeneratePermission, ct);

    [HttpPost("api/apply")]
    public async Task<IActionResult> Apply(CancellationToken ct)
        => await ProxyBodyAsync(HttpMethod.Post, "/api/crm/visit-plan/apply", ApplyPermission, ct, PlannedVisitManage);

    // WP-VP-4A — reopen an approved week (3A): the same keys as apply (apply AND planned-visit.manage); the body (reason,
    // expectedVersion) and CRM's answer (404 / 409 / 400 envelope included) pass through unchanged.
    [HttpPost("api/sessions/{planningSessionId:guid}/weeks/{weekStart}/reopen")]
    public async Task<IActionResult> ReopenWeek(Guid planningSessionId, string weekStart, CancellationToken ct)
        => await ProxyBodyAsync(
            HttpMethod.Post,
            $"/api/crm/visit-plan/sessions/{planningSessionId}/weeks/{Uri.EscapeDataString(weekStart)}/reopen",
            ApplyPermission, ct, PlannedVisitManage);

    [HttpPost("api/re-plan")]
    public async Task<IActionResult> Replan(CancellationToken ct)
        => await ProxyBodyAsync(HttpMethod.Post, "/api/crm/visit-plan/re-plan", ApplyPermission, ct, PlannedVisitManage);

    // ---------------- session CRUD proxies ----------------

    [HttpGet("api/sessions")]
    public Task<IActionResult> ListSessions(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/visit-plan/sessions{Request.QueryString}", null, ReadPermission, ct);

    [HttpGet("api/sessions/{planningSessionId:guid}")]
    public Task<IActionResult> GetSession(Guid planningSessionId, CancellationToken ct)
        => ProxyAsync(
            HttpMethod.Get, $"/api/crm/visit-plan/sessions/{planningSessionId}", null, ReadPermission, ct);

    // WP-VP-3D (D5) — the plan's institutions, pharmacies and doctors (names, places, doctor period statuses) in ONE
    // request. CRM applies the session ownership rule (another rep's plan is 404).
    [HttpGet("api/sessions/{planningSessionId:guid}/targets")]
    public Task<IActionResult> SessionTargets(Guid planningSessionId, CancellationToken ct)
        => ProxyAsync(
            HttpMethod.Get, $"/api/crm/visit-plan/sessions/{planningSessionId}/targets", null, ReadPermission, ct);

    [HttpPost("api/sessions")]
    public async Task<IActionResult> CreateSession(CancellationToken ct)
        => await ProxyBodyAsync(HttpMethod.Post, "/api/crm/visit-plan/sessions", GeneratePermission, ct);

    [HttpPut("api/sessions/{planningSessionId:guid}")]
    public async Task<IActionResult> UpdateSession(Guid planningSessionId, CancellationToken ct)
    {
        var body = await ReadBodyAsync(ct);
        return await ProxyAsync(
            HttpMethod.Put, $"/api/crm/visit-plan/sessions/{planningSessionId}", body, GeneratePermission, ct);
    }

    // ---------------- read-only picker passthroughs (those masters are never touched) ----------------

    [HttpGet("api/cycle-periods")]
    public Task<IActionResult> CyclePeriods(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/cycle-periods{Request.QueryString}", null, ReadPermission, ct);

    [HttpGet("api/accounts")]
    public Task<IActionResult> Accounts(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/accounts{Request.QueryString}", null, ReadPermission, ct);

    // Single account — resolves a saved target's name/type/city on Details reload (distinct from …/{id}/contacts).
    [HttpGet("api/accounts/{accountId:guid}")]
    public Task<IActionResult> Account(Guid accountId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/accounts/{accountId}", null, ReadPermission, ct);

    [HttpGet("api/contacts")]
    public Task<IActionResult> Contacts(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/contacts{Request.QueryString}", null, ReadPermission, ct);

    // Doctors cascade from the selected clinic/hospital: the linked contacts of one account. Downstream
    // crm.account-contact.read is granted to the tenant admin; the /api/crm/accounts/* wildcard covers this route.
    [HttpGet("api/accounts/{accountId:guid}/contacts")]
    public Task<IActionResult> AccountContacts(Guid accountId, CancellationToken ct)
        => ProxyAsync(
            HttpMethod.Get, $"/api/crm/accounts/{accountId}/contacts{Request.QueryString}", null, ReadPermission, ct);

    // Related accounts (Account 360 projection) — used by the Targets tab to surface a clinic/hospital's linked
    // pharmacies as pickable pharmacy targets. Same /api/crm/accounts/* wildcard; crm.account-relationship.read.
    [HttpGet("api/accounts/{accountId:guid}/related-accounts")]
    public Task<IActionResult> RelatedAccounts(Guid accountId, CancellationToken ct)
        => ProxyAsync(
            HttpMethod.Get, $"/api/crm/accounts/{accountId}/related-accounts{Request.QueryString}", null, ReadPermission, ct);

    // WP-VP-3D (D5) — related accounts of up to 100 accounts in one request (CRM crm.account.read; > 100 ⇒ 400
    // too_many_ids). Same /api/crm/accounts/* wildcard.
    [HttpGet("api/accounts/related")]
    public Task<IActionResult> RelatedAccountsBulk(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/accounts/related{Request.QueryString}", null, ReadPermission, ct);

    // WP-VP-FIX-1 (C3) — there is no working-calendar proxy any more: the CRM planner reads the calendar itself (tenant
    // seam) and returns calendarStatus + nonWorkingDates on the preview; the route tab renders from those.

    // WP-VP-FIX-1 (D6) — code → label maps for the institution type (account-type) and the doctor's specialty
    // (medical-specialty), read through the shared CrmReferenceSetReader so every tenant role gets them. A set that cannot
    // be read is an empty map: the page then shows the stored code, exactly as before.
    [HttpGet("api/reference-labels")]
    public async Task<IActionResult> ReferenceLabels(CancellationToken ct)
    {
        if (!HasAnyPermission(ReadPermission))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
        }

        var reader = new CrmReferenceSetReader(_httpClient, _gatewayUrl, _logger);
        var accountTypes = await ReadLabelsAsync(reader, AccountTypeSetCode, ct);
        var specialties = await ReadLabelsAsync(reader, MedicalSpecialtySetCode, ct);
        // WP-VP-4I (8) — additive: the province labels (an unpublished set is an empty map: the page then capitalises the
        // code's last part the Turkish way).
        var cities = await ReadLabelsAsync(reader, CitySetCode, ct);
        return Ok(new { data = new { accountTypes, specialties, cities } });
    }

    private async Task<Dictionary<string, string>> ReadLabelsAsync(
        CrmReferenceSetReader reader, string setCode, CancellationToken ct)
    {
        var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var response = await reader.ReadAsync(
                setCode, Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request), GetTenantId(), ct);
            if (response is null || !response.IsSuccessStatusCode)
            {
                return labels;
            }

            // WP-VP-4I (9) — a value has ONE label (MOD-0048 has no per-language field); the label in the UI language is
            // its label_<lang> attribute, else the label. Root cause of "Clinic" / "Family Medicine" in Turkish: the
            // data had only English labels — the Web read the only label there was.
            var language = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<PublishedValuesModel>>(JsonOptions, ct);
            foreach (var item in payload?.Data?.Items ?? [])
            {
                var text = item.TextFor(language);
                if (!string.IsNullOrWhiteSpace(item.Value) && !string.IsNullOrWhiteSpace(text))
                {
                    labels[item.Value!] = text!;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Reference set '{SetCode}' labels could not be read; codes are shown.", setCode);
        }

        return labels;
    }

    // WP-VP-2 (B-3, K-4) — no segment proxy: the rep never picks a segment; the play is derived from the doctor.

    // WP-VP-2 (B-2) — "my accounts": the accounts the caller's current territory assignments cover (territoryStatus
    // assigned | unassigned — K-5: unassigned lists every account and the page warns). The Targets "add clinic /
    // hospital" search reads THIS; the separate "add out-of-territory" search keeps using api/accounts.
    [HttpGet("api/my-accounts")]
    public Task<IActionResult> MyAccounts(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/visit-plan/my-accounts{Request.QueryString}", null, ReadPermission, ct);

    // WP-VP-3D (B-6) — an institution's active doctors with their period status (required / done / remaining / last
    // visit / due this week …); quick=due|never|all, search, specialty, planningSessionId, paging.
    [HttpGet("api/my-accounts/{accountId:guid}/doctors")]
    public Task<IActionResult> MyAccountDoctors(Guid accountId, CancellationToken ct)
        => ProxyAsync(
            HttpMethod.Get, $"/api/crm/visit-plan/my-accounts/{accountId}/doctors{Request.QueryString}", null, ReadPermission, ct);

    // Cycle-period scope options — its resolved COUNTRY_CODES `countries` list feeds the Country dropdown, so the codes
    // match the periods' CountryScope exactly. Degrades to an empty picker if it refuses.
    [HttpGet("api/scope-options")]
    public Task<IActionResult> ScopeOptions(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/cycle-periods/scope-options{Request.QueryString}", null, ReadPermission, ct);

    // WP-VP-2 (B-1, K-1) — the rep is the signed-in user: no user-directory proxy, no picker. The form shows who it is
    // from resources/me (resourceId + displayName); CRM writes the caller as the plan's resource.
    [HttpGet("api/me")]
    public Task<IActionResult> Me(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, "/api/crm/resources/me", null, ReadPermission, ct);

    // WP-VP-FIX-1 (A1, K-3) — no strategy-template ("play") proxy: the rep never picks a play; it is derived server-side.

    // WP-VP-4C (K-7) — the doctor product picker's catalogue search: the MDM Global Product selector (the Knowledge
    // global-product-options pattern), READ only. pageSize is clamped to the MDM cap of 100; MDM owns its own read key on
    // the gateway. Never an empty silent list: a refusal / outage answers { disabled: true, reason } and the picker says
    // the catalogue cannot be read (the doctor's stored products stay as they are).
    [HttpGet("api/products")]
    public async Task<IActionResult> Products(CancellationToken ct)
    {
        if (!HasAnyPermission(ReadPermission))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
        }

        using var response = await SendGatewayAsync(HttpMethod.Get, $"/api/global-products/selector{BuildProductSelectorQuery()}", null, ct);
        if (response is null || !response.IsSuccessStatusCode)
        {
            var reason = response is null ? "ProductCatalogueUnavailable"
                : (int)response.StatusCode == 403 ? "ProductPermissionMissing"
                : (int)response.StatusCode == 404 ? "ProductEndpointMissing"
                : "ProductCatalogueUnavailable";
            return Ok(new { disabled = true, reason });
        }

        return Ok(new { disabled = false, options = ParseProductOptions(await response.Content.ReadAsStringAsync(ct)) });
    }

    private string BuildProductSelectorQuery()
    {
        var q = Request.Query;
        var search = q["search"].ToString();
        var pageNumber = int.TryParse(q["pageNumber"], out var pn) && pn > 0 ? pn : 1;
        var pageSize = int.TryParse(q["pageSize"], out var ps) ? Math.Clamp(ps, 1, 100) : 100;
        var query = $"?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search)) query += $"&search={Uri.EscapeDataString(search.Trim())}";
        return query;
    }

    private List<object> ParseProductOptions(string body)
    {
        var options = new List<object>();
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("data", out var data)) return options;
            var items = data.ValueKind == JsonValueKind.Array ? data
                : data.ValueKind == JsonValueKind.Object && data.TryGetProperty("items", out var it) ? it
                : default;
            if (items.ValueKind != JsonValueKind.Array) return options;
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var id = FirstString(item, "id", "globalProductId");
                if (!Guid.TryParse(id, out var productId)) continue;
                options.Add(new
                {
                    productId,
                    productCode = FirstString(item, "canonicalCode", "code"),
                    productName = FirstString(item, "globalProductName", "name")
                });
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Visit planning product options could not be parsed.");
        }

        return options;
    }

    private static string? FirstString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(value.GetString()))
            {
                return value.GetString();
            }
        }

        return null;
    }

    // ---------------- proxy helpers ----------------

    private async Task<IActionResult> ProxyBodyAsync(
        HttpMethod method, string path, string permission, CancellationToken ct, params string[] additional)
    {
        var body = await ReadBodyAsync(ct);
        return await ProxyAsync(method, path, body, permission, ct, additional);
    }

    private async Task<IActionResult> ProxyAsync(
        HttpMethod method, string path, string? rawBody, string permission, CancellationToken ct,
        params string[] additional)
    {
        // apply/re-plan require ALL of {permission} ∪ {additional}; reads require just the one.
        if (!HasAnyPermission(permission) || additional.Any(p => !HasAnyPermission(p)))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
        }

        if (rawBody is not null && ContainsTenantId(rawBody))
        {
            return BadRequest(new { errors = new[] { "TenantId is server-resolved and must not be supplied." } });
        }

        var response = await SendGatewayAsync(method, path, rawBody, ct);
        return await ToProxyResultAsync(response, ct);
    }

    /// <summary>The session's status through the same Gateway read the page itself uses; null when it cannot be read (the
    /// page then loads as before and shows the read error client-side).</summary>
    private async Task<string?> ReadSessionStatusAsync(Guid planningSessionId, CancellationToken ct)
    {
        using var response = await SendGatewayAsync(
            HttpMethod.Get, $"/api/crm/visit-plan/sessions/{planningSessionId}", null, ct);
        if (response is null || !response.IsSuccessStatusCode)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("data", out var data)
                   && data.ValueKind == JsonValueKind.Object
                   && data.TryGetProperty("status", out var status)
                   && status.ValueKind == JsonValueKind.String
                ? status.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<string?> ReadBodyAsync(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;
        return string.IsNullOrWhiteSpace(body) ? null : body;
    }

    private async Task<HttpResponseMessage?> SendGatewayAsync(
        HttpMethod method, string path, string? rawBody, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, $"{_gatewayUrl}{path}");
            var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var tenantId = GetTenantId();
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return null;
            }

            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);

            if (rawBody is not null)
            {
                request.Content = new StringContent(rawBody, Encoding.UTF8, "application/json");
            }

            return await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Visit planning Gateway request failed: {Method} {Path}", method, path);
            return null;
        }
    }

    private static async Task<IActionResult> ToProxyResultAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null)
        {
            return new ObjectResult(new { errors = new[] { "Gateway unavailable." } }) { StatusCode = 502 };
        }

        if (IsBodilessStatus(response.StatusCode))
        {
            return new StatusCodeResult((int)response.StatusCode);
        }

        var content = await response.Content.ReadAsStringAsync(ct);
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            Content = content
        };
    }

    private static bool IsBodilessStatus(HttpStatusCode status)
        => (int)status is >= 100 and < 200 || status is HttpStatusCode.NoContent
            or HttpStatusCode.ResetContent or HttpStatusCode.NotModified;

    private static bool ContainsTenantId(string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.EnumerateObject()
                       .Any(x => string.Equals(x.Name, "tenantId", StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private string? GetTenantId() => User.Claims.FirstOrDefault(x =>
        x.Type == "tenantId" || x.Type == "tenant_id" ||
        x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    private bool HasAnyPermission(params string[] permissions) =>
        permissions.Any(x => PermissionClaims.HasPermission(User, x));
}
