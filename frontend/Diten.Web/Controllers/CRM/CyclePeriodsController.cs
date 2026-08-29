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
/// MOD-0165 FU06/FU07 Cycle Period Admin UI (Golden <b>Compact</b>). All business traffic is proxied server-side
/// through Gateway 5000; the browser never sees a service URL or a bearer token. The CrmService runtime stays the
/// authoritative validation and permission layer — nothing is decided here.
/// <para>FU07 moved the module from Slim to Compact: the create/edit offcanvas and the quick-view are gone, replaced by
/// Create / Edit / Details pages, because the discriminated scope took the form from 8 user fields to 11. A page that
/// carried both shapes would pass neither verifier reference, so the Slim files were deleted rather than kept
/// alongside.</para>
/// <para>There is no delete surface (ending a period is Close), no reopen surface (closed is terminal) and no
/// apply/generate surface at all: applying a plan to a period is MOD-0155. Nothing here computes working days either —
/// that is the working-calendar capability, combined by a consumer.</para>
/// </summary>
[Authorize]
[Route("CRM/CyclePeriods")]
public sealed class CyclePeriodsController : Controller
{
    private const string ReadPermission = "crm.cycle-period.read";
    private const string ManagePermission = "crm.cycle-period.manage";
    private const string ActivatePermission = "crm.cycle-period.activate";

    /// <summary>Documented DEV-ONLY fallback until F-RBAC lands. It widens no guard: the CrmService still enforces
    /// tenant isolation, the lifecycle, the scope invariant and the overlap ban behind it.</summary>
    private const string ReadFallback = "crm.territory.read";

    private const string ManageFallback = "crm.territory.model.manage";
    private const string ViewRoot = "~/Views/CRM/CyclePeriods";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<CyclePeriodsController> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public CyclePeriodsController(
        HttpClient httpClient, IConfiguration configuration, ILogger<CyclePeriodsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _logger = logger;
    }

    // ---------------- pages ----------------

    [HttpGet("")]
    public IActionResult Index()
    {
        if (RequirePage(ReadPermission, ReadFallback) is { } denied)
        {
            return denied;
        }

        return View($"{ViewRoot}/Index.cshtml", new CyclePeriodIndexViewModel
        {
            CanManage = HasAnyPermission(ManagePermission, ManageFallback),
            CanActivate = HasAnyPermission(ActivatePermission, ManagePermission, ManageFallback)
        });
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create(CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied)
        {
            return denied;
        }

        var model = new CyclePeriodEditViewModel
        {
            ScopeType = "tenant",
            ScopeOptions = await LoadScopeOptionsAsync(null, null, null, ct)
        };

        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] CyclePeriodEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied)
        {
            return denied;
        }

        if (!ModelState.IsValid)
        {
            return await RedisplayAsync($"{ViewRoot}/Create.cshtml", model, ct);
        }

        var response = await SendGatewayAsync(
            HttpMethod.Post, "/api/crm/cycle-periods", ToPayload(model, includeExpectedVersion: false), ct);

        if (response is not null && response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = model.CycleName;
            return RedirectToAction(nameof(Index));
        }

        await AddGatewayErrorsAsync(response, ct);
        return await RedisplayAsync($"{ViewRoot}/Create.cshtml", model, ct);
    }

    [HttpGet("Edit/{cyclePeriodId:guid}")]
    public async Task<IActionResult> Edit(Guid cyclePeriodId, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied)
        {
            return denied;
        }

        var detail = await LoadDetailAsync(cyclePeriodId, ct);
        if (detail is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var model = ToEditModel(detail);
        // The model's dates are already UTC-anchored; the raw detail's are not necessarily, and the candidate window
        // must be the same window the author is looking at.
        model.ScopeOptions = await LoadScopeOptionsAsync(
            model.CountryScope, model.StartDate, model.EndDate, ct);

        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpPost("Edit/{cyclePeriodId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        Guid cyclePeriodId, [FromForm] CyclePeriodEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied)
        {
            return denied;
        }

        model.CyclePeriodId = cyclePeriodId;
        if (!ModelState.IsValid)
        {
            return await RedisplayAsync($"{ViewRoot}/Edit.cshtml", model, ct);
        }

        var response = await SendGatewayAsync(
            HttpMethod.Put, $"/api/crm/cycle-periods/{cyclePeriodId}",
            ToPayload(model, includeExpectedVersion: true), ct);

        if (response is not null && response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = model.CycleName;
            return RedirectToAction(nameof(Index));
        }

        await AddGatewayErrorsAsync(response, ct);
        return await RedisplayAsync($"{ViewRoot}/Edit.cshtml", model, ct);
    }

    [HttpGet("Details/{cyclePeriodId:guid}")]
    public async Task<IActionResult> Details(Guid cyclePeriodId, CancellationToken ct)
    {
        if (RequirePage(ReadPermission, ReadFallback) is { } denied)
        {
            return denied;
        }

        var detail = await LoadDetailAsync(cyclePeriodId, ct);
        if (detail is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var model = ToEditModel(detail);
        ViewData["CanManage"] = HasAnyPermission(ManagePermission, ManageFallback);
        return View($"{ViewRoot}/Details.cshtml", model);
    }

    // ---------------- JSON proxies (same-origin; the browser never calls 5061) ----------------

    [HttpGet("api/contract")]
    public Task<IActionResult> Contract(CancellationToken ct) =>
        ProxyAsync(HttpMethod.Get, "/api/crm/cycle-periods/contract", null, ReadPermission, ct, ReadFallback);

    [HttpGet("api/periods")]
    public Task<IActionResult> List(CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Get, $"/api/crm/cycle-periods{Request.QueryString}", null, ReadPermission, ct, ReadFallback);

    [HttpGet("api/periods/{cyclePeriodId:guid}")]
    public Task<IActionResult> Get(Guid cyclePeriodId, CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Get, $"/api/crm/cycle-periods/{cyclePeriodId}", null, ReadPermission, ct, ReadFallback);

    /// <summary>The read-only "which period is in force?" answer. It creates nothing.</summary>
    [HttpGet("api/periods/resolve-active")]
    public Task<IActionResult> ResolveActive(CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Get, $"/api/crm/cycle-periods/resolve-active{Request.QueryString}", null,
            ReadPermission, ct, ReadFallback);

    /// <summary>FU07 — the cascading selector's option source. A READ: it decides nothing about what may be saved.</summary>
    [HttpGet("api/scope-options")]
    public Task<IActionResult> ScopeOptions(CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Get, $"/api/crm/cycle-periods/scope-options{Request.QueryString}", null,
            ReadPermission, ct, ReadFallback);

    [HttpPost("api/periods/{cyclePeriodId:guid}/activate")]
    public Task<IActionResult> Activate(Guid cyclePeriodId, CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Post, $"/api/crm/cycle-periods/{cyclePeriodId}/activate{Request.QueryString}", null,
            ActivatePermission, ct, ManagePermission, ManageFallback);

    [HttpPost("api/periods/{cyclePeriodId:guid}/close")]
    public Task<IActionResult> Close(Guid cyclePeriodId, CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Post, $"/api/crm/cycle-periods/{cyclePeriodId}/close{Request.QueryString}", null,
            ActivatePermission, ct, ManagePermission, ManageFallback);

    // ---------------- form helpers ----------------

    /// <summary>
    /// The write payload. <c>TenantId</c> and <c>CycleStatus</c> are absent by construction, and only the reference
    /// belonging to the chosen scope is sent: a hidden-but-populated field would otherwise reach the runtime and be
    /// refused as an ambiguous scope, which is a confusing way to fail a form the author filled in correctly.
    /// </summary>
    private static object ToPayload(CyclePeriodEditViewModel model, bool includeExpectedVersion)
    {
        var scopeType = (model.ScopeType ?? string.Empty).Trim().ToLowerInvariant();

        return new
        {
            cycleCode = model.CycleCode?.Trim(),
            cycleName = model.CycleName?.Trim(),
            year = model.Year,
            sequenceInYear = model.SequenceInYear,
            // The picked calendar day, anchored to UTC so the runtime stores the day the author chose.
            startDate = PickedDayToUtc(model.StartDate),
            endDate = PickedDayToUtc(model.EndDate),
            scopeType,
            countryScope = scopeType == "country" ? Clean(model.CountryScope) : null,
            legalEntityId = scopeType == "legal-entity" ? model.LegalEntityId : null,
            businessUnitId = scopeType == "business-unit" ? Clean(model.BusinessUnitId) : null,
            // Context, not scope: sent ONLY with a business unit, so it can never look like a country-scoped period.
            businessUnitCountryContext = scopeType == "business-unit"
                ? Clean(model.BusinessUnitCountryContext)
                : null,
            description = Clean(model.Description),
            expectedVersion = includeExpectedVersion ? model.ExpectedVersion : null
        };
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// A date the AUTHOR picked, anchored to UTC midnight.
    /// <para>An <c>&lt;input type="date"&gt;</c> posts a bare calendar day; MVC binds it with the SERVER's offset, so
    /// "1 Jul" arrives as <c>2026-07-01T00:00+03:00</c> — which is 30 Jun in UTC. The runtime stores the UTC day, so
    /// the period silently landed a day early on every server east of Greenwich (and a day late west of it). The date
    /// component here is the day the author actually clicked; pairing it with a zero offset sends exactly that day.
    /// </para>
    /// <para>The runtime's UTC canon is CORRECT and untouched: the fix belongs where a calendar day is turned into an
    /// instant, which is here.</para>
    /// </summary>
    private static DateTimeOffset? PickedDayToUtc(DateTimeOffset? value)
        => value is { } d ? new DateTimeOffset(d.Date, TimeSpan.Zero) : null;

    /// <summary>
    /// A date the RUNTIME returned, anchored to UTC midnight. The opposite reading of
    /// <see cref="PickedDayToUtc"/> and NOT interchangeable with it: a stored instant may deserialize into any offset,
    /// and on a negative one its local date component is the previous day. The stored day is the UTC day.
    /// </summary>
    private static DateTimeOffset? StoredDayToUtc(DateTimeOffset? value)
        => value is { } d ? new DateTimeOffset(d.UtcDateTime.Date, TimeSpan.Zero) : null;

    private static CyclePeriodEditViewModel ToEditModel(CyclePeriodDetailApiModel detail) => new()
    {
        CyclePeriodId = detail.CyclePeriodId,
        CycleCode = detail.CycleCode,
        CycleName = detail.CycleName,
        Year = detail.Year,
        SequenceInYear = detail.SequenceInYear,
        StartDate = StoredDayToUtc(detail.StartDate),
        EndDate = StoredDayToUtc(detail.EndDate),
        ScopeType = string.IsNullOrWhiteSpace(detail.ScopeType) ? "tenant" : detail.ScopeType,
        CountryScope = detail.CountryScope,
        LegalEntityId = detail.LegalEntityId,
        BusinessUnitId = detail.BusinessUnitId,
        BusinessUnitSource = detail.BusinessUnitSource,
        BusinessUnitCountryContext = detail.BusinessUnitCountryContext,
        Description = detail.Description,
        CycleStatus = detail.CycleStatus,
        ExpectedVersion = detail.Version
    };

    /// <summary>Re-renders a rejected form with its option lists intact — an author must not lose their dropdowns
    /// because the runtime refused one field.</summary>
    private async Task<IActionResult> RedisplayAsync(
        string view, CyclePeriodEditViewModel model, CancellationToken ct)
    {
        // Anchor BEFORE re-rendering: the form re-reads these values, and a rejected post that came back shifted by a
        // day would look like the runtime moved the author's dates.
        model.StartDate = PickedDayToUtc(model.StartDate);
        model.EndDate = PickedDayToUtc(model.EndDate);
        model.ScopeOptions = await LoadScopeOptionsAsync(model.CountryScope, model.StartDate, model.EndDate, ct);
        return View(view, model);
    }

    private async Task<CyclePeriodDetailApiModel?> LoadDetailAsync(Guid cyclePeriodId, CancellationToken ct)
    {
        var response = await SendGatewayAsync(
            HttpMethod.Get, $"/api/crm/cycle-periods/{cyclePeriodId}", null, ct);
        if (response is null || !response.IsSuccessStatusCode)
        {
            TempData["ErrorMessage"] = cyclePeriodId.ToString();
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer
            .Deserialize<CyclePeriodGatewayResponse<CyclePeriodDetailApiModel>>(body, _json)?.Data;
    }

    /// <summary>
    /// Loads the selector's options. An unreachable source yields an EMPTY, NOT-READY list — never a substituted one:
    /// a hardcoded fallback would let an author pick a value the platform does not know, and the save would then be
    /// refused for a reason the form never showed them.
    /// </summary>
    private async Task<CyclePeriodScopeOptionsViewModel> LoadScopeOptionsAsync(
        string? country, DateTimeOffset? startDate, DateTimeOffset? endDate, CancellationToken ct)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(country))
        {
            query.Add($"country={Uri.EscapeDataString(country.Trim())}");
        }

        if (startDate is { } start)
        {
            query.Add($"startDate={Uri.EscapeDataString(start.ToString("O"))}");
        }

        if (endDate is { } end)
        {
            query.Add($"endDate={Uri.EscapeDataString(end.ToString("O"))}");
        }

        var path = "/api/crm/cycle-periods/scope-options"
                   + (query.Count == 0 ? string.Empty : "?" + string.Join("&", query));

        var response = await SendGatewayAsync(HttpMethod.Get, path, null, ct);
        if (response is null || !response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Cycle period scope options could not be loaded; rendering the form without them.");
            return new CyclePeriodScopeOptionsViewModel();
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        var api = JsonSerializer
            .Deserialize<CyclePeriodGatewayResponse<CyclePeriodScopeOptionsApiModel>>(body, _json)?.Data;
        if (api is null)
        {
            return new CyclePeriodScopeOptionsViewModel();
        }

        return new CyclePeriodScopeOptionsViewModel
        {
            ScopeTypes = api.ScopeTypes,
            Countries = api.Countries.Select(Option).ToList(),
            CountryReady = api.CountryReady,
            LegalEntities = api.LegalEntities.Select(Option).ToList(),
            LegalEntityReady = api.LegalEntityReady,
            BusinessUnits = api.BusinessUnits.Select(Option).ToList(),
            BusinessUnitReady = api.BusinessUnitReady,
            BusinessUnitFromTerritory = api.BusinessUnitFromTerritory,
            CountrySetCode = api.CountrySetCode,
            BusinessUnitSetCode = api.BusinessUnitSetCode
        };

        static CyclePeriodScopeOptionViewModel Option(CyclePeriodScopeOptionApiModel o)
            => new() { Value = o.Value, Label = o.Label, Hint = o.Hint };
    }

    /// <summary>Surfaces the runtime's own refusal verbatim. The overlap and scope messages name the blocking period
    /// and its address, and flattening them into "save failed" would take away the only thing an author can act on.
    /// </summary>
    private async Task AddGatewayErrorsAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null)
        {
            ModelState.AddModelError(string.Empty, "Gateway unavailable.");
            return;
        }

        try
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var envelope = JsonSerializer.Deserialize<CyclePeriodGatewayResponse<object>>(body, _json);
            if (envelope?.Errors is { Count: > 0 })
            {
                foreach (var error in envelope.Errors)
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                return;
            }
        }
        catch (JsonException)
        {
            // fall through to the status-only message
        }

        ModelState.AddModelError(string.Empty, $"HTTP {(int)response.StatusCode}");
    }

    // ---------------- proxy helpers ----------------

    private async Task<IActionResult> ProxyAsync(
        HttpMethod method, string path, JsonElement? body, string permission, CancellationToken ct,
        params string[] fallbacks)
    {
        if (RequireJson(permission, fallbacks) is { } denied)
        {
            return denied;
        }

        if (body.HasValue && ContainsTenantId(body.Value))
        {
            return BadRequest(new { errors = new[] { "TenantId is server-resolved and must not be supplied." } });
        }

        var response = await SendGatewayAsync(method, path, body?.GetRawText(), ct);
        return await ToProxyResultAsync(response, ct);
    }

    private Task<HttpResponseMessage?> SendGatewayAsync(
        HttpMethod method, string path, object? body, CancellationToken ct)
        => SendGatewayAsync(method, path, body is null ? null : JsonSerializer.Serialize(body, _json), ct);

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
            _logger.LogError(ex, "Cycle period Gateway request failed: {Method} {Path}", method, path);
            return null;
        }
    }

    private static async Task<IActionResult> ToProxyResultAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null)
        {
            return new ObjectResult(new { errors = new[] { "Gateway unavailable." } }) { StatusCode = 502 };
        }

        // A bodiless status must stay bodiless: writing a body onto a 204/205/304/1xx makes Kestrel throw
        // ("Content-Length not allowed"), which turns a perfectly good no-content answer into a 500.
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

    private static bool ContainsTenantId(JsonElement element) => element.ValueKind == JsonValueKind.Object &&
        element.EnumerateObject().Any(x => string.Equals(x.Name, "tenantId", StringComparison.OrdinalIgnoreCase));

    private string? GetTenantId() => User.Claims.FirstOrDefault(x =>
        x.Type == "tenantId" || x.Type == "tenant_id" ||
        x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    private bool HasAnyPermission(params string[] permissions) =>
        permissions.Any(x => PermissionClaims.HasPermission(User, x));

    private IActionResult? RequirePage(string permission, params string[] fallbacks) =>
        HasAnyPermission([permission, .. fallbacks]) ? null : StatusCode(StatusCodes.Status403Forbidden);

    private IActionResult? RequireJson(string permission, params string[] fallbacks) =>
        HasAnyPermission([permission, .. fallbacks])
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
}
