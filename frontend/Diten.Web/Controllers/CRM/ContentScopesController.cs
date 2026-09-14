using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.CRM;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// SCMM-14-UI (CAND-CAP-0011) ContentScope console. Proxy-only, mirroring the SCMM-12 Claims console: all business
/// traffic is proxied server-side through Gateway 5000 to the ready ContentScope surface
/// (<c>/api/crm/content-composition/content-scopes</c>); the browser never sees a service URL or bearer token, and no
/// business rule lives here. No delete surface — closing a scope is Archive. RBAC keys (<c>crm.content-scope.*</c>) are
/// seeded + granted (SCMM-14), so there is no dev-fallback permission.
/// </summary>
[Authorize]
[Route("CRM/ContentScopes")]
public sealed class ContentScopesController : Controller
{
    private const string ReadPermission = "crm.content-scope.read";
    private const string ManagePermission = "crm.content-scope.manage";
    private const string ViewRoot = "~/Views/CRM/ContentScopes";
    private const string ScopesBase = "/api/crm/content-composition/content-scopes";

    // Create / update carry only these editable states; archived is the dedicated archive action.
    private static readonly string[] EditableStatuses = ["draft", "active"];

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<ContentScopesController> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public ContentScopesController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<ContentScopesController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    // ---------------- Pages ----------------

    [HttpGet("")]
    public IActionResult Index() => RequirePage(ReadPermission) ?? View($"{ViewRoot}/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create()
    {
        if (RequirePage(ManagePermission) is { } denied) return denied;
        var model = new ContentScopeEditViewModel
        {
            ScopeCode = SuggestScopeCode(),
            ScopeVersion = "1.0",
            Status = "draft",
            Statuses = EditableStatuses
        };
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ContentScopeEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission) is { } denied) return denied;
        model.Statuses = EditableStatuses;
        if (!ModelState.IsValid) return View($"{ViewRoot}/Create.cshtml", model);

        var response = await SendGatewayAsync(HttpMethod.Post, ScopesBase, ToPayload(model, forUpdate: false), ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            var envelope = await response.Content.ReadFromJsonAsync<ContentGatewayResponse<Guid>>(_json, ct);
            TempData["SuccessMessage"] = _sharedLocalizer["RecordCreated"].Value;
            return envelope?.Data is { } id && id != Guid.Empty
                ? RedirectToAction(nameof(Edit), new { id })
                : RedirectToAction(nameof(Index));
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, ct));
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpGet("Edit/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        if (RequirePage(ManagePermission) is { } denied) return denied;
        var scope = await LoadScopeAsync(id, ct);
        if (scope is null) return NotFound();
        return View($"{ViewRoot}/Edit.cshtml", ToEditModel(scope));
    }

    [HttpPost("Edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, ContentScopeEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission) is { } denied) return denied;
        model.ContentScopeId = id;
        model.Statuses = EditableStatuses;
        if (!ModelState.IsValid) return View($"{ViewRoot}/Edit.cshtml", model);

        var response = await SendGatewayAsync(HttpMethod.Put, $"{ScopesBase}/{id}", ToPayload(model, forUpdate: true), ct);
        if (response is not null && response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = _sharedLocalizer["RecordUpdated"].Value;
            return RedirectToAction(nameof(Edit), new { id });
        }

        AddGatewayErrors(await ExtractErrorsAsync(response, ct));
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    // ---------------- Same-origin browser proxy ----------------

    [HttpGet("api/content-scopes")]
    public Task<IActionResult> ScopeList(CancellationToken ct) =>
        ProxyGetAsync($"{ScopesBase}{Request.QueryString}", ReadPermission, ct);

    [HttpPost("api/content-scopes/{contentScopeId:guid}/archive")]
    public Task<IActionResult> ArchiveScope(Guid contentScopeId, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ScopesBase}/{contentScopeId}/archive", null, ManagePermission, ct);

    // ---------------- helpers ----------------

    private static string SuggestScopeCode() =>
        $"SCOPE-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private async Task<ContentScopeDetailViewModel?> LoadScopeAsync(Guid id, CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, $"{ScopesBase}/{id}", null, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        return (await response.Content.ReadFromJsonAsync<ContentGatewayResponse<ContentScopeDetailViewModel>>(_json, ct))?.Data;
    }

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
            _logger.LogError(ex, "ContentScope Gateway request failed: {Method} {Path}", method, path);
            return null;
        }
    }

    private static async Task<IActionResult> ToProxyResultAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null)
            return new ObjectResult(new { errors = new[] { "Gateway unavailable." } }) { StatusCode = 502 };
        var content = await response.Content.ReadAsStringAsync(ct);
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            Content = content
        };
    }

    private async Task<List<string>> ExtractErrorsAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null) return [_sharedLocalizer["GatewayError"].Value];
        try
        {
            var envelope = await response.Content.ReadFromJsonAsync<ContentGatewayResponse<object>>(_json, ct);
            if (envelope?.Errors.Count > 0) return envelope.Errors;
        }
        catch { }
        var raw = await response.Content.ReadAsStringAsync(ct);
        return [string.IsNullOrWhiteSpace(raw) ? _sharedLocalizer["GatewayError"].Value : raw];
    }

    private void AddGatewayErrors(IEnumerable<string> errors)
    {
        foreach (var error in errors) ModelState.AddModelError(string.Empty, error);
    }

    private static object ToPayload(ContentScopeEditViewModel m, bool forUpdate) => forUpdate
        ? new
        {
            m.ScopeName, m.Description, productRefs = Clean(m.ProductRefs), marketRefs = Clean(m.MarketRefs),
            audienceRefs = Clean(m.AudienceRefs), m.Channel, m.LanguageCode, m.PeriodFrom, m.PeriodTo,
            m.ScopeVersion, m.Status
        }
        : new
        {
            m.ScopeCode, m.ScopeName, m.Description, productRefs = Clean(m.ProductRefs), marketRefs = Clean(m.MarketRefs),
            audienceRefs = Clean(m.AudienceRefs), m.Channel, m.LanguageCode, m.PeriodFrom, m.PeriodTo,
            m.ScopeVersion, m.Status
        };

    private static List<string> Clean(IEnumerable<string>? values) =>
        (values ?? []).Select(v => v?.Trim() ?? string.Empty).Where(v => v.Length > 0).Distinct().ToList();

    private static ContentScopeEditViewModel ToEditModel(ContentScopeDetailViewModel s) => new()
    {
        ContentScopeId = s.ContentScopeId,
        ScopeCode = s.ScopeCode,
        ScopeName = s.ScopeName,
        Description = s.Description,
        ProductRefs = [.. s.ProductRefs],
        MarketRefs = [.. s.MarketRefs],
        AudienceRefs = [.. s.AudienceRefs],
        Channel = s.Channel,
        LanguageCode = s.LanguageCode,
        PeriodFrom = s.PeriodFrom,
        PeriodTo = s.PeriodTo,
        ScopeVersion = s.ScopeVersion,
        Status = s.Status,
        IsArchived = s.IsArchived,
        Statuses = EditableStatuses
    };

    private static bool ContainsTenantId(JsonElement element) => element.ValueKind == JsonValueKind.Object &&
        element.EnumerateObject().Any(x => string.Equals(x.Name, "tenantId", StringComparison.OrdinalIgnoreCase));

    private string? GetTenantId() => User.Claims.FirstOrDefault(x =>
        x.Type == "tenantId" || x.Type == "tenant_id" ||
        x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    private bool HasAnyPermission(params string[] permissions) => permissions.Any(x => PermissionClaims.HasPermission(User, x));

    private IActionResult? RequirePage(string permission) =>
        HasAnyPermission(permission) ? null : StatusCode(StatusCodes.Status403Forbidden);

    private IActionResult? RequireJson(string permission) =>
        HasAnyPermission(permission)
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
}
