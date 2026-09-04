using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.CRM;
using Diten.Web.Security;
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

    [HttpGet("Edit/{planningSessionId:guid}")]
    public IActionResult Edit(Guid planningSessionId)
    {
        if (!HasAnyPermission(GeneratePermission))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return View($"{ViewRoot}/Edit.cshtml", new VisitPlanningSessionPageViewModel
        {
            SessionId = planningSessionId,
            CanGenerate = HasAnyPermission(GeneratePermission),
            CanApply = HasAnyPermission(ApplyPermission) && HasAnyPermission(PlannedVisitManage)
        });
    }

    [HttpGet("Details/{planningSessionId:guid}")]
    public IActionResult Details(Guid planningSessionId)
    {
        if (!HasAnyPermission(ReadPermission))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return View($"{ViewRoot}/Details.cshtml", new VisitPlanningSessionPageViewModel
        {
            SessionId = planningSessionId,
            CanGenerate = HasAnyPermission(GeneratePermission),
            CanApply = HasAnyPermission(ApplyPermission) && HasAnyPermission(PlannedVisitManage)
        });
    }

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

    // Working calendar (weekend + holiday days) for the route tab. Best-effort: the Details view degrades to a Sat/Sun
    // weekend fallback when this refuses (needs platform.working-calendar.override.read) or returns nothing.
    [HttpGet("api/working-calendar")]
    public Task<IActionResult> WorkingCalendar(CancellationToken ct)
        => ProxyAsync(
            HttpMethod.Get, $"/api/platform/working-calendars{Request.QueryString}", null, ReadPermission, ct);

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

    // StrategyTemplate (MOD-0167) — read-only passthrough for the "play" picker. Degrades gracefully in the UI when the
    // key/route is absent (empty picker + a note); the session's StrategyTemplateId stays optional on the backend.
    [HttpGet("api/strategy-templates")]
    public Task<IActionResult> StrategyTemplates(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/strategy-templates{Request.QueryString}", null, ReadPermission, ct);

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
