using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0357 S3 — the Meetings tenant surface plus its same-origin API proxy, mirroring <c>TasksController</c>'s
/// own shape exactly (pack §15, ADR-003 §5 Stage 1).
///
/// <para>The browser never addresses a service port: it calls <c>/Meetings/api/*</c> on this app, and the JWT is
/// read server-side from the HTTP-only auth cookie (never exposed to JS). This proxy forwards to the Gateway
/// (5000), never straight to Platform (5057).</para>
/// </summary>
[Authorize]
[Route("Meetings")]
public sealed class MeetingsController : Controller
{
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string CorrelationHeaderName = "X-Correlation-Id";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _gatewayUrl;
    private readonly ILogger<MeetingsController> _logger;

    public MeetingsController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<MeetingsController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/') ?? "http://localhost:5000";
        _logger = logger;
    }

    // ── Views (Golden Reference Compact) ────────────────────────────────────

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/Meetings/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create() => View("~/Views/Meetings/Create.cshtml");

    [HttpGet("{id:guid}")]
    public IActionResult Details(Guid id)
    {
        ViewData["MeetingId"] = id.ToString();
        return View("~/Views/Meetings/Details.cshtml");
    }

    [HttpGet("{id:guid}/Edit")]
    public IActionResult Edit(Guid id)
    {
        ViewData["MeetingId"] = id.ToString();
        return View("~/Views/Meetings/Edit.cshtml");
    }

    // ── S6 — Minutes editor (Compact satellite screen, own page not a tab — pack §5 :323) ────

    [HttpGet("{id:guid}/Minutes")]
    public IActionResult MinutesEditor(Guid id)
    {
        ViewData["MeetingId"] = id.ToString();
        return View("~/Views/Meetings/MinutesEditor/Index.cshtml");
    }

    // ── S8 — Meeting Types (Compact satellite settings screen, pack §5 :323) ──

    [HttpGet("MeetingTypes")]
    public IActionResult MeetingTypesIndex() => View("~/Views/Meetings/MeetingTypes/Index.cshtml");

    [HttpGet("MeetingTypes/Create")]
    public IActionResult MeetingTypesCreate() => View("~/Views/Meetings/MeetingTypes/Create.cshtml");

    [HttpGet("MeetingTypes/{id:guid}/Edit")]
    public IActionResult MeetingTypesEdit(Guid id)
    {
        ViewData["MeetingTypeId"] = id.ToString();
        return View("~/Views/Meetings/MeetingTypes/Edit.cshtml");
    }

    // ── S11 — Meeting Series (Compact satellite settings screen, pack §19) ────

    [HttpGet("Series")]
    public IActionResult MeetingSeriesIndex() => View("~/Views/Meetings/Series/Index.cshtml");

    [HttpGet("Series/Create")]
    public IActionResult MeetingSeriesCreate() => View("~/Views/Meetings/Series/Create.cshtml");

    [HttpGet("Series/{id:guid}/Edit")]
    public IActionResult MeetingSeriesEdit(Guid id)
    {
        ViewData["MeetingSeriesId"] = id.ToString();
        return View("~/Views/Meetings/Series/Edit.cshtml");
    }

    // ── S12 (pack §23) — the meeting report & action register ────────────────

    [HttpGet("Report")]
    public IActionResult ReportIndex() => View("~/Views/Meetings/Report/Index.cshtml");

    // ── Same-origin API proxy ────────────────────────────────────────────────

    [HttpGet("api/list")]
    public Task<IActionResult> ApiList([FromQuery] string? query)
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/meetings{Query(query)}", readBody: false);

    [HttpGet("api/{id:guid}")]
    public Task<IActionResult> ApiGet(Guid id)
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/meetings/{id}", readBody: false);

    [HttpPost("api")]
    public Task<IActionResult> ApiCreate()
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/meetings", readBody: true);

    [HttpPut("api/{id:guid}")]
    public Task<IActionResult> ApiUpdate(Guid id)
        => ProxyAsync(HttpMethod.Put, $"{_gatewayUrl}/api/v1/meetings/{id}", readBody: true);

    [HttpPost("api/{id:guid}/cancel")]
    public Task<IActionResult> ApiCancel(Guid id)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/meetings/{id}/cancel", readBody: true);

    [HttpPost("api/{id:guid}/reassign-organizer")]
    public Task<IActionResult> ApiReassignOrganizer(Guid id)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/meetings/{id}/reassign-organizer", readBody: true);

    [HttpPost("api/{id:guid}/attendees")]
    public Task<IActionResult> ApiAddAttendees(Guid id)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/meetings/{id}/attendees", readBody: true);

    [HttpDelete("api/{id:guid}/attendees/{userId:guid}")]
    public Task<IActionResult> ApiRemoveAttendee(Guid id, Guid userId)
        => ProxyAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/v1/meetings/{id}/attendees/{userId}", readBody: false);

    [HttpPost("api/{id:guid}/agenda")]
    public Task<IActionResult> ApiAddAgendaItem(Guid id)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/meetings/{id}/agenda", readBody: true);

    [HttpPut("api/{id:guid}/agenda/order")]
    public Task<IActionResult> ApiReorderAgenda(Guid id)
        => ProxyAsync(HttpMethod.Put, $"{_gatewayUrl}/api/v1/meetings/{id}/agenda/order", readBody: true);

    [HttpPut("api/{id:guid}/agenda/{itemId:guid}")]
    public Task<IActionResult> ApiUpdateAgendaItem(Guid id, Guid itemId)
        => ProxyAsync(HttpMethod.Put, $"{_gatewayUrl}/api/v1/meetings/{id}/agenda/{itemId}", readBody: true);

    [HttpDelete("api/{id:guid}/agenda/{itemId:guid}")]
    public Task<IActionResult> ApiDeleteAgendaItem(Guid id, Guid itemId)
        => ProxyAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/v1/meetings/{id}/agenda/{itemId}", readBody: false);

    [HttpGet("api/{id:guid}/tasks")]
    public Task<IActionResult> ApiGetLinkedTasks(Guid id)
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/meetings/{id}/tasks", readBody: false);

    // S5, K5 — Accept/Decline.
    [HttpPost("api/{id:guid}/respond")]
    public Task<IActionResult> ApiRespond(Guid id)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/meetings/{id}/respond", readBody: true);

    // ── S6 — minutes (mirrors Platform's MeetingsController.cs 1:1) ────────────────────────────────────────

    [HttpGet("api/{id:guid}/minutes")]
    public Task<IActionResult> ApiGetMinutes(Guid id)
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/meetings/{id}/minutes", readBody: false);

    [HttpPut("api/{id:guid}/minutes/draft")]
    public Task<IActionResult> ApiSaveMinutesDraft(Guid id)
        => ProxyAsync(HttpMethod.Put, $"{_gatewayUrl}/api/v1/meetings/{id}/minutes/draft", readBody: true);

    [HttpPost("api/{id:guid}/minutes/publish")]
    public Task<IActionResult> ApiPublishMinutes(Guid id)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/meetings/{id}/minutes/publish", readBody: true);

    [HttpPost("api/{id:guid}/minutes/correct")]
    public Task<IActionResult> ApiCorrectPublishedMinutes(Guid id)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/meetings/{id}/minutes/correct", readBody: true);

    // ── S7 — continuation scheduling (mirrors Platform's MeetingsController.cs 1:1) ────────────────────────

    [HttpPost("api/{id:guid}/follow-up")]
    public Task<IActionResult> ApiScheduleFollowUp(Guid id)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/meetings/{id}/follow-up", readBody: true);

    // ── S4 — the meeting↔task bridge (mirrors Platform's MeetingsController.cs 1:1) ─────────────────────────

    [HttpPost("api/{id:guid}/tasks")]
    public Task<IActionResult> ApiCreateTaskFromMeeting(Guid id)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/meetings/{id}/tasks", readBody: true);

    [HttpPost("api/{id:guid}/tasks/{taskId:guid}/link")]
    public Task<IActionResult> ApiLinkExistingTask(Guid id, Guid taskId)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/meetings/{id}/tasks/{taskId}/link", readBody: true);

    [HttpPost("api/tasks/{taskId:guid}/schedule-review-meeting")]
    public Task<IActionResult> ApiScheduleReviewMeetingForTask(Guid taskId)
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/meetings/tasks/{taskId}/schedule-review-meeting", readBody: true);

    [HttpGet("api/lookups/attendees")]
    public Task<IActionResult> ApiLookupAttendees()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/meetings/lookups/attendees", readBody: false);

    [HttpGet("api/lookups/types")]
    public Task<IActionResult> ApiLookupTypes()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/meetings/lookups/types", readBody: false);

    // ── S8 — Meeting Types CRUD proxy (types-manage only; "types" never matches {id:guid}, same
    // disambiguation MeetingTypesController's own sub-route already relies on) ─────────────────────

    [HttpGet("api/types")]
    public Task<IActionResult> ApiTypesList()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/meetings/types", readBody: false);

    [HttpGet("api/types/{id:guid}")]
    public Task<IActionResult> ApiTypesGet(Guid id)
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/meetings/types/{id}", readBody: false);

    [HttpPost("api/types")]
    public Task<IActionResult> ApiTypesCreate()
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/meetings/types", readBody: true);

    [HttpPut("api/types/{id:guid}")]
    public Task<IActionResult> ApiTypesUpdate(Guid id)
        => ProxyAsync(HttpMethod.Put, $"{_gatewayUrl}/api/v1/meetings/types/{id}", readBody: true);

    [HttpDelete("api/types/{id:guid}")]
    public Task<IActionResult> ApiTypesDelete(Guid id)
        => ProxyAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/v1/meetings/types/{id}", readBody: false);

    // ── S11 — Meeting Series CRUD proxy (series-manage only; "series" never matches {id:guid}, same
    // disambiguation the S8 "types" sub-route already relies on) ─────────────────────────────────────

    [HttpGet("api/series")]
    public Task<IActionResult> ApiSeriesList()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/meetings/series", readBody: false);

    [HttpGet("api/series/{id:guid}")]
    public Task<IActionResult> ApiSeriesGet(Guid id)
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/meetings/series/{id}", readBody: false);

    [HttpPost("api/series")]
    public Task<IActionResult> ApiSeriesCreate()
        => ProxyAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/meetings/series", readBody: true);

    [HttpPut("api/series/{id:guid}")]
    public Task<IActionResult> ApiSeriesUpdate(Guid id)
        => ProxyAsync(HttpMethod.Put, $"{_gatewayUrl}/api/v1/meetings/series/{id}", readBody: true);

    [HttpDelete("api/series/{id:guid}")]
    public Task<IActionResult> ApiSeriesDelete(Guid id)
        => ProxyAsync(HttpMethod.Delete, $"{_gatewayUrl}/api/v1/meetings/series/{id}", readBody: false);

    // ── S12 (pack §23) — meeting report & its export. The whole query string is forwarded WHOLE
    // (from/to/meetingTypeId/organizerUserId are Platform's contract, not this tier's) — re-listing them here
    // is how a parameter gets dropped silently, the same lesson TasksController's own work-report proxy
    // already states. ────────────────────────────────────────────────────────────────────────────────────

    [HttpGet("api/report")]
    public Task<IActionResult> ApiReport()
        => ProxyAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/meetings/report" + Request.QueryString.Value, readBody: false);

    /// <summary>The report's own rows, as a file — <see cref="ProxyFileAsync"/> (the work report's own file-relay
    /// pattern), with <c>locale</c> appended from <c>window.CurrentLanguage</c> if the caller did not already
    /// send one (pack §23.13/5 — the one disclaimer sentence the export carries).</summary>
    [HttpGet("api/report/export")]
    public Task<IActionResult> ApiReportExport()
        => ProxyFileAsync($"{_gatewayUrl}/api/v1/meetings/report/export" + Request.QueryString.Value, MeetingReportExportRowCountHeader);

    /// <summary>How many rows the file carries — the work report's own <c>X-Work-Report-Export-Row-Count</c>, renamed.</summary>
    public const string MeetingReportExportRowCountHeader = "X-Meeting-Report-Export-Row-Count";

    // ── Proxy plumbing (identical to TasksController's own) ──────────────────

    private static string Query(string? query) => string.IsNullOrWhiteSpace(query) ? string.Empty : $"?{query}";

    private async Task<IActionResult> ProxyAsync(HttpMethod method, string targetUrl, bool readBody, object? body = null)
    {
        if (!TryCreateTenantRequest(method, targetUrl, out var request))
        {
            return Unauthorized(new { message = "Unauthorized" });
        }

        try
        {
            using (request)
            {
                if (body is not null)
                {
                    request.Content = new StringContent(
                        System.Text.Json.JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                }
                else if (readBody)
                {
                    using var reader = new StreamReader(Request.Body, Encoding.UTF8);
                    var relayed = await reader.ReadToEndAsync(HttpContext.RequestAborted);
                    request.Content = new StringContent(
                        relayed, Encoding.UTF8, Request.ContentType ?? "application/json");
                }

                var client = _httpClientFactory.CreateClient();
                using var response = await client.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
                var content = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);

                // Pass the upstream status through verbatim: a 403 (permission not granted) or 409 (concurrency
                // conflict) must reach the browser as itself so the UI can react precisely.
                return new ContentResult
                {
                    Content = content,
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                    StatusCode = (int)response.StatusCode
                };
            }
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Meetings proxy failed for {Method} {TargetUrl}.", method, targetUrl);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Meetings dependency unavailable." });
        }
    }

    /// <summary>S12 (pack §23.7) — the file-download relay, mirroring <c>TasksController.ProxyFileAsync</c>
    /// (the work report's own pattern, itself following the audit export's) exactly: a non-success upstream
    /// response (400/403/503) is relayed as JSON verbatim — including the 503
    /// <c>DATA_EXPORT_AUDIT_NOT_RECORDED</c> the export handler answers when BL-347's audit write fails — never
    /// wrapped into a file.</summary>
    private async Task<IActionResult> ProxyFileAsync(string targetUrl, string rowCountHeaderName)
    {
        if (!TryCreateTenantRequest(HttpMethod.Get, targetUrl, out var request))
        {
            return Unauthorized(new { message = "Unauthorized" });
        }

        try
        {
            using (request)
            {
                var client = _httpClientFactory.CreateClient();
                using var response = await client.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);

                if (!response.IsSuccessStatusCode)
                {
                    return new ContentResult
                    {
                        Content = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted),
                        ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                        StatusCode = (int)response.StatusCode
                    };
                }

                var content = await response.Content.ReadAsByteArrayAsync(HttpContext.RequestAborted);
                var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
                var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                    ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                    ?? "export";

                if (response.Headers.TryGetValues(rowCountHeaderName, out var rowCount))
                {
                    Response.Headers[rowCountHeaderName] = rowCount.FirstOrDefault();
                }

                return File(content, contentType, fileName);
            }
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Meetings file proxy failed for {TargetUrl}.", targetUrl);
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Meetings dependency unavailable." });
        }
    }

    private bool TryCreateTenantRequest(HttpMethod method, string targetUrl, out HttpRequestMessage request)
    {
        request = new HttpRequestMessage(method, targetUrl);
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token) || !TryResolveTenantId(token, out var tenantId))
        {
            request.Dispose();
            request = null!;
            return false;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation(TenantHeaderName, tenantId.ToString("D"));
        request.Headers.TryAddWithoutValidation(CorrelationHeaderName, ResolveCorrelationId());
        if (Request.Headers.TryGetValue("Accept-Language", out var acceptLanguage))
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage.ToString());
        }

        return true;
    }

    private string ResolveCorrelationId()
    {
        if (Request.Headers.TryGetValue(CorrelationHeaderName, out var correlationId) &&
            !string.IsNullOrWhiteSpace(correlationId.ToString()))
        {
            return correlationId.ToString();
        }

        return HttpContext.TraceIdentifier;
    }

    private static bool TryResolveTenantId(string token, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var claimValue = FindClaim(jwt.Claims, "tenant_id", "tenantId");
            return Guid.TryParse(claimValue, out tenantId) && tenantId != Guid.Empty && jwt.ValidTo > DateTime.UtcNow;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindClaim(IEnumerable<Claim> claims, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var match = claims.FirstOrDefault(c => string.Equals(c.Type, claimType, StringComparison.OrdinalIgnoreCase));
            if (match is not null && !string.IsNullOrWhiteSpace(match.Value))
            {
                return match.Value;
            }
        }

        return null;
    }
}
