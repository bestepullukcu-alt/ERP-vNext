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
/// MOD-0155 FU01 Planned Visit Admin UI (Golden <b>Compact</b>). All business traffic is proxied server-side through the
/// Gateway; the browser never sees a service URL or a bearer token, and the CrmService runtime stays the authoritative
/// validation / permission layer. There is no delete surface (a plan is cancelled and/or archived) and no reopen
/// (archived is terminal); the create/edit/details pages are their own pages (Compact), never an offcanvas.
/// </summary>
[Authorize]
[Route("CRM/PlannedVisits")]
public sealed class PlannedVisitsController : Controller
{
    private const string ReadPermission = "crm.planned-visit.read";
    private const string ManagePermission = "crm.planned-visit.manage";
    private const string ConfirmPermission = "crm.planned-visit.confirm";

    /// <summary>Documented DEV-ONLY fallback until F-RBAC lands. It widens no guard: the CrmService still enforces
    /// tenant isolation, the lifecycle, the consent guard and the overlap ban behind it.</summary>
    private const string ReadFallback = "crm.territory.read";

    private const string ManageFallback = "crm.territory.model.manage";
    private const string ViewRoot = "~/Views/CRM/PlannedVisits";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<PlannedVisitsController> _logger;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public PlannedVisitsController(
        HttpClient httpClient, IConfiguration configuration, ILogger<PlannedVisitsController> logger)
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

        return View($"{ViewRoot}/Index.cshtml", new PlannedVisitIndexViewModel
        {
            CanManage = HasAnyPermission(ManagePermission, ManageFallback),
            CanConfirm = HasAnyPermission(ConfirmPermission, ManagePermission, ManageFallback)
        });
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied)
        {
            return denied;
        }

        return View($"{ViewRoot}/Create.cshtml", new PlannedVisitEditViewModel
        {
            TargetType = "account",
            ResourceType = "person",
            PlanStatus = "draft",
            Source = "manual",
            PlannedDate = DateTimeOffset.UtcNow,
            CanManage = true
        });
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] PlannedVisitEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied)
        {
            return denied;
        }

        model.CanManage = true;
        if (!ModelState.IsValid)
        {
            return View($"{ViewRoot}/Create.cshtml", model);
        }

        var response = await SendGatewayAsync(
            HttpMethod.Post, "/api/crm/planned-visits", ToPayload(model, includeExpectedVersion: false), ct);

        if (response is not null && response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = model.VisitCode;
            return RedirectToAction(nameof(Index));
        }

        await AddGatewayErrorsAsync(response, ct);
        return View($"{ViewRoot}/Create.cshtml", model);
    }

    [HttpGet("Edit/{plannedVisitId:guid}")]
    public async Task<IActionResult> Edit(Guid plannedVisitId, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied)
        {
            return denied;
        }

        var detail = await LoadDetailAsync(plannedVisitId, ct);
        if (detail is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var model = ToEditModel(detail);
        model.CanManage = true;
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpPost("Edit/{plannedVisitId:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        Guid plannedVisitId, [FromForm] PlannedVisitEditViewModel model, CancellationToken ct)
    {
        if (RequirePage(ManagePermission, ManageFallback) is { } denied)
        {
            return denied;
        }

        model.PlannedVisitId = plannedVisitId;
        model.CanManage = true;
        if (!ModelState.IsValid)
        {
            return View($"{ViewRoot}/Edit.cshtml", model);
        }

        var response = await SendGatewayAsync(
            HttpMethod.Put, $"/api/crm/planned-visits/{plannedVisitId}",
            ToPayload(model, includeExpectedVersion: true), ct);

        if (response is not null && response.IsSuccessStatusCode)
        {
            TempData["SuccessMessage"] = model.VisitCode;
            return RedirectToAction(nameof(Index));
        }

        await AddGatewayErrorsAsync(response, ct);
        return View($"{ViewRoot}/Edit.cshtml", model);
    }

    [HttpGet("Details/{plannedVisitId:guid}")]
    public async Task<IActionResult> Details(Guid plannedVisitId, CancellationToken ct)
    {
        if (RequirePage(ReadPermission, ReadFallback) is { } denied)
        {
            return denied;
        }

        var detail = await LoadDetailAsync(plannedVisitId, ct);
        if (detail is null)
        {
            return RedirectToAction(nameof(Index));
        }

        ViewData["CanManage"] = HasAnyPermission(ManagePermission, ManageFallback);
        ViewData["CanConfirm"] = HasAnyPermission(ConfirmPermission, ManagePermission, ManageFallback);
        return View($"{ViewRoot}/Details.cshtml", detail);
    }

    // ---------------- JSON proxies (same-origin; the browser never calls 5061) ----------------

    [HttpGet("api/contract")]
    public Task<IActionResult> Contract(CancellationToken ct) =>
        ProxyAsync(HttpMethod.Get, "/api/crm/planned-visits/contract", null, ReadPermission, ct, ReadFallback);

    [HttpGet("api/plans")]
    public Task<IActionResult> List(CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Get, $"/api/crm/planned-visits{Request.QueryString}", null, ReadPermission, ct, ReadFallback);

    [HttpGet("api/plans/{plannedVisitId:guid}")]
    public Task<IActionResult> Get(Guid plannedVisitId, CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Get, $"/api/crm/planned-visits/{plannedVisitId}", null, ReadPermission, ct, ReadFallback);

    [HttpPost("api/plans/{plannedVisitId:guid}/confirm")]
    public Task<IActionResult> Confirm(Guid plannedVisitId, CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Post, $"/api/crm/planned-visits/{plannedVisitId}/confirm{Request.QueryString}", null,
            ConfirmPermission, ct, ManagePermission, ManageFallback);

    [HttpPost("api/plans/{plannedVisitId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid plannedVisitId, CancellationToken ct)
    {
        var body = await ReadBodyAsync(ct);
        return await ProxyAsync(
            HttpMethod.Post, $"/api/crm/planned-visits/{plannedVisitId}/cancel", body,
            ManagePermission, ct, ManageFallback);
    }

    [HttpPost("api/plans/{plannedVisitId:guid}/archive")]
    public Task<IActionResult> Archive(Guid plannedVisitId, CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Post, $"/api/crm/planned-visits/{plannedVisitId}/archive{Request.QueryString}", null,
            ManagePermission, ct, ManageFallback);

    // Read-only picker passthroughs (MOD-0149/0150/0162 surfaces are READ only; those modules are never touched).
    [HttpGet("api/accounts")]
    public Task<IActionResult> Accounts(CancellationToken ct) =>
        ProxyAsync(HttpMethod.Get, $"/api/crm/accounts{Request.QueryString}", null, ReadPermission, ct, ReadFallback);

    [HttpGet("api/contacts")]
    public Task<IActionResult> Contacts(CancellationToken ct) =>
        ProxyAsync(HttpMethod.Get, $"/api/crm/contacts{Request.QueryString}", null, ReadPermission, ct, ReadFallback);

    [HttpGet("api/journeys")]
    public Task<IActionResult> Journeys(CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Get, $"/api/crm/knowledge/content-engagement-journeys{Request.QueryString}", null,
            ReadPermission, ct, ReadFallback);

    [HttpGet("api/journeys/{journeyId:guid}/stages")]
    public Task<IActionResult> JourneyStages(Guid journeyId, CancellationToken ct) =>
        ProxyAsync(
            HttpMethod.Get, $"/api/crm/knowledge/content-engagement-journeys/{journeyId}/stages", null,
            ReadPermission, ct, ReadFallback);

    // ---------------- form helpers ----------------

    /// <summary>The write payload. <c>TenantId</c> is absent by construction; only the journey/stage of the derive-or-
    /// override content-position ref are sent, and only the reference relevant to the chosen target.</summary>
    private static object ToPayload(PlannedVisitEditViewModel model, bool includeExpectedVersion) => new
    {
        visitCode = Clean(model.VisitCode),
        targetType = Clean(model.TargetType),
        targetId = model.TargetId,
        plannedDate = model.PlannedDate?.UtcDateTime.ToString("yyyy-MM-dd"),
        plannedStartTime = Clean(model.PlannedStartTime),
        plannedEndTime = Clean(model.PlannedEndTime),
        plannedDurationMinutes = model.PlannedDurationMinutes,
        resourceId = Clean(model.ResourceId),
        resourceType = Clean(model.ResourceType),
        resourceDisplayName = Clean(model.ResourceDisplayName),
        visitPurpose = Clean(model.VisitPurpose),
        visitType = Clean(model.VisitType),
        objective = Clean(model.Objective),
        notes = Clean(model.Notes),
        businessUnit = Clean(model.BusinessUnit),
        territoryNodeId = model.TerritoryNodeId,
        territoryModelId = model.TerritoryModelId,
        campaignId = model.CampaignId,
        contentEngagementJourneyId = model.ContentEngagementJourneyId,
        contentEngagementJourneyStageId = model.ContentEngagementJourneyStageId,
        contentSource = Clean(model.ContentSource),
        planStatus = includeExpectedVersion ? null : Clean(model.PlanStatus),
        source = includeExpectedVersion ? null : (Clean(model.Source) ?? "manual"),
        expectedVersion = includeExpectedVersion ? model.ExpectedVersion : null
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static PlannedVisitEditViewModel ToEditModel(PlannedVisitDetailApiModel detail) => new()
    {
        PlannedVisitId = detail.PlannedVisitId,
        VisitCode = detail.VisitCode,
        TargetType = detail.TargetType,
        TargetId = detail.TargetId,
        ResourceId = detail.Resource.ResourceId,
        ResourceType = string.IsNullOrWhiteSpace(detail.Resource.ResourceType) ? "person" : detail.Resource.ResourceType,
        ResourceDisplayName = detail.Resource.DisplayName,
        PlannedDate = ParseDay(detail.PlannedDate),
        PlannedStartTime = detail.PlannedStartTime,
        PlannedEndTime = detail.PlannedEndTime,
        PlannedDurationMinutes = detail.PlannedDurationMinutes,
        VisitPurpose = detail.VisitPurpose,
        VisitType = detail.VisitType,
        Objective = detail.Objective,
        Notes = detail.Notes,
        BusinessUnit = detail.BusinessUnit,
        TerritoryNodeId = detail.TerritoryNodeId,
        TerritoryModelId = detail.TerritoryModelId,
        CampaignId = detail.CampaignId,
        ContentEngagementJourneyId = detail.Content?.JourneyId ?? detail.ContentEngagementJourneyId,
        ContentEngagementJourneyStageId = detail.Content?.StageId ?? detail.ContentEngagementJourneyStageId,
        ContentSource = detail.Content?.ContentSource,
        PlanStatus = detail.PlanStatus,
        Source = detail.Source,
        ExpectedVersion = detail.Version
    };

    private static DateTimeOffset? ParseDay(string? value)
        => DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out var dto)
            ? new DateTimeOffset(dto.UtcDateTime.Date, TimeSpan.Zero)
            : null;

    private async Task<PlannedVisitDetailApiModel?> LoadDetailAsync(Guid plannedVisitId, CancellationToken ct)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, $"/api/crm/planned-visits/{plannedVisitId}", (object?)null, ct);
        if (response is null || !response.IsSuccessStatusCode)
        {
            TempData["ErrorMessage"] = plannedVisitId.ToString();
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer
            .Deserialize<PlannedVisitGatewayResponse<PlannedVisitDetailApiModel>>(body, _json)?.Data;
    }

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
            var envelope = JsonSerializer.Deserialize<PlannedVisitGatewayResponse<object>>(body, _json);
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

    private async Task<string?> ReadBodyAsync(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;
        return string.IsNullOrWhiteSpace(body) ? null : body;
    }

    private async Task<IActionResult> ProxyAsync(
        HttpMethod method, string path, string? rawBody, string permission, CancellationToken ct,
        params string[] fallbacks)
    {
        if (RequireJson(permission, fallbacks) is { } denied)
        {
            return denied;
        }

        if (rawBody is not null && ContainsTenantId(rawBody))
        {
            return BadRequest(new { errors = new[] { "TenantId is server-resolved and must not be supplied." } });
        }

        var response = await SendGatewayAsync(method, path, rawBody, ct);
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
            _logger.LogError(ex, "Planned visit Gateway request failed: {Method} {Path}", method, path);
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
                   && doc.RootElement.EnumerateObject().Any(x => string.Equals(x.Name, "tenantId", StringComparison.OrdinalIgnoreCase));
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

    private IActionResult? RequirePage(string permission, params string[] fallbacks) =>
        HasAnyPermission([permission, .. fallbacks]) ? null : StatusCode(StatusCodes.Status403Forbidden);

    private IActionResult? RequireJson(string permission, params string[] fallbacks) =>
        HasAnyPermission([permission, .. fallbacks])
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
}
