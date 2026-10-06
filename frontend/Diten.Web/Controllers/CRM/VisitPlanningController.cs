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
    private const string ViewRoot = "~/Views/CRM/VisitPlanning";

    // WP-VP-FIX-1 (D6) — the MOD-0048 sets whose labels replace raw codes on the Targets + Route tabs.
    internal const string AccountTypeSetCode = "account-type";
    internal const string MedicalSpecialtySetCode = "medical-specialty";

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
            CanApply = HasAnyPermission(ApplyPermission) && HasAnyPermission(PlannedVisitManage)
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
        return Ok(new { data = new { accountTypes, specialties } });
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

            var payload = await response.Content.ReadFromJsonAsync<GatewayResponse<PublishedValuesModel>>(JsonOptions, ct);
            foreach (var item in payload?.Data?.Items ?? [])
            {
                if (!string.IsNullOrWhiteSpace(item.Value) && !string.IsNullOrWhiteSpace(item.Text))
                {
                    labels[item.Value!] = item.Text!;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Reference set '{SetCode}' labels could not be read; codes are shown.", setCode);
        }

        return labels;
    }

    [HttpGet("api/segments")]
    public Task<IActionResult> Segments(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/segments{Request.QueryString}", null, ReadPermission, ct);

    // Cycle-period scope options — its resolved COUNTRY_CODES `countries` list feeds the Country dropdown, so the codes
    // match the periods' CountryScope exactly. Degrades to an empty picker if it refuses.
    [HttpGet("api/scope-options")]
    public Task<IActionResult> ScopeOptions(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/cycle-periods/scope-options{Request.QueryString}", null, ReadPermission, ct);

    // The rep is a real user (MOD-0151 person resource). Read-only passthrough to the platform user directory so the
    // create/edit form can offer a user picker; the selected id still populates the session's string ResourceId.
    [HttpGet("api/users")]
    public Task<IActionResult> Users(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/users{Request.QueryString}", null, ReadPermission, ct);

    // WP-VP-FIX-1 (A1, K-3) — no strategy-template ("play") proxy: the rep never picks a play; it is derived server-side.

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
