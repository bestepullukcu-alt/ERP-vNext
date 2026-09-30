using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0280-FU01 T2a (pack §5.2) — My Timesheet (<c>/TimeEntry</c>) and its same-origin proxy.
///
/// <para><b>One proxy, one prefix.</b> <c>/TimeEntry/api/{**path}</c> → <c>{GatewayUrl}/api/v1/time-entry/{path}</c>, for
/// GET/POST/PUT/DELETE. The path is re-escaped segment by segment (nothing but path segments can reach the upstream path —
/// no <c>..</c>, no second host) and the query string is forwarded whole and still encoded: the parameters are Platform's
/// contract, not this tier's, and re-listing them here is how one gets dropped silently.</para>
///
/// <para><b>The upstream status and body pass through VERBATIM</b>, reason code included — the page turns
/// <c>TIME_ENTRY_*</c> / <c>TIMESHEET_*</c> codes into sentences in seven languages. The token and the tenant id are
/// attached here from the HTTP-only cookie; the browser never sees either.</para>
///
/// <para>The page itself renders the UAS-001 explanation (no skeleton) for a caller without
/// <c>time-entry.timesheets.read</c>; Platform's <c>[HasPermission]</c> stays the authority.</para>
/// </summary>
[Authorize]
public sealed class TimeEntryController : Controller
{
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string CorrelationHeaderName = "X-Correlation-Id";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _gatewayUrl;
    private readonly ILogger<TimeEntryController> _logger;

    public TimeEntryController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TimeEntryController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/') ?? "http://localhost:5000";
        _logger = logger;
    }

    /// <summary>My Timesheet. <paramref name="week"/> (ISO week, e.g. <c>2026-W40</c>) is handed to the page as is; the
    /// page validates its shape and the server validates the week.</summary>
    [HttpGet("/TimeEntry")]
    public IActionResult Index([FromQuery] string? week = null)
    {
        ViewBag.ActiveMenu = "timeentry";
        ViewData["WeekKey"] = week ?? string.Empty;
        return View("~/Views/TimeEntry/Index.cshtml");
    }

    [AcceptVerbs("GET", "POST", "PUT", "DELETE", Route = "/TimeEntry/api/{**path}")]
    public async Task<IActionResult> Api(string? path)
    {
        var target = UpstreamUrl(path);
        return target is null ? NotFound() : await ForwardAsync(new HttpMethod(Request.Method), target, await ReadBodyAsync());
    }

    // ── T2b pages (pack §5.2, §9). Each view draws the UAS-001 explanation for a caller without its key. ────────────

    [HttpGet("/TimeEntry/Approvals")]
    public IActionResult Approvals()
    {
        ViewBag.ActiveMenu = "timeentry";
        return View("~/Views/TimeEntry/Approvals/Index.cshtml");
    }

    [HttpGet("/TimeEntry/Approvals/{weekId:guid}")]
    public IActionResult ApprovalDetails(Guid weekId)
    {
        ViewBag.ActiveMenu = "timeentry";
        ViewData["WeekId"] = weekId.ToString("D");
        return View("~/Views/TimeEntry/Approvals/Details.cshtml");
    }

    [HttpGet("/TimeEntry/Categories")]
    public IActionResult Categories()
    {
        ViewBag.ActiveMenu = "timeentry";
        return View("~/Views/TimeEntry/Categories/Index.cshtml");
    }

    [HttpGet("/TimeEntry/Settings")]
    public IActionResult Settings()
    {
        ViewBag.ActiveMenu = "timeentry";
        return View("~/Views/TimeEntry/Settings/Index.cshtml");
    }

    // ── T2b list proxies — one per list page, so each page names ITS endpoint (Golden Reference proxy profile) ────

    /// <summary>The approvals list and the read-only week: GET only (an approver never writes a week, D6).</summary>
    [HttpGet("/TimeEntry/Approvals/api/{**path}")]
    public async Task<IActionResult> ApprovalsApi(string? path)
    {
        var target = UpstreamUrl(string.IsNullOrEmpty(path) ? "approvals" : "approvals/" + path);
        return target is null ? NotFound() : await ForwardAsync(HttpMethod.Get, target, null);
    }

    [HttpGet("/TimeEntry/Approvals/api")]
    public Task<IActionResult> ApprovalsListApi() => ApprovalsApi(null);

    /// <summary>The category catalogue: its manager's list and writes (activate / deactivate, never delete — D10).</summary>
    [AcceptVerbs("GET", "POST", "PUT", Route = "/TimeEntry/Categories/api/{**path}")]
    public async Task<IActionResult> CategoriesApi(string? path)
    {
        var target = UpstreamUrl(string.IsNullOrEmpty(path) ? "categories" : "categories/" + path);
        return target is null ? NotFound() : await ForwardAsync(new HttpMethod(Request.Method), target, await ReadBodyAsync());
    }

    [AcceptVerbs("GET", "POST", Route = "/TimeEntry/Categories/api")]
    public Task<IActionResult> CategoriesRootApi() => CategoriesApi(null);

    /// <summary>
    /// U4 — approve or reject ONE week, on the Task Center's OWN action path: the gateway's
    /// <c>POST api/v1/work-items/{approvalTaskId}/actions/{approve|reject}</c> with provider <c>workflow</c>, i.e. the same
    /// MOD-0023 dispatcher and commands the approval card uses. No second approve route exists (D6). The reason travels as
    /// <c>comment</c> — the field that dispatcher reads — and MOD-0023 itself refuses a timesheet reject without one (R5).
    /// </summary>
    [HttpPost("/TimeEntry/Approvals/api/decisions/{decision}")]
    public async Task<IActionResult> Decide(string decision, [FromBody] ApprovalDecisionRequest? body)
    {
        if (decision is not ("approve" or "reject") || body is null || body.ApprovalTaskId == Guid.Empty)
        {
            return BadRequest(new { message = "approve or reject, with the week's approval task." });
        }

        return await ForwardAsync(HttpMethod.Post, WorkItemActionUrl(body.ApprovalTaskId, decision),
            DecisionPayload(body.ExpectedVersion, decision == "reject" ? body.Comment : null));
    }

    /// <summary>
    /// U4 — "approve the selected UNMARKED weeks". One decision per week, each on the same work-item action path as
    /// <see cref="Decide"/>. The web tier re-reads the caller's own approvals list first and approves ONLY a week that is
    /// in it, still open, and carries no mark (11-hour day, timer cut at midnight, outside hours, holiday): what the page
    /// sent is a request, not the authority. Reject is never bulk (it needs its own reason).
    /// </summary>
    [HttpPost("/TimeEntry/Approvals/api/bulk")]
    public async Task<IActionResult> BulkApprove([FromBody] BulkApprovalRequest? body)
    {
        var requested = (body?.WeekIds ?? []).Distinct().ToList();
        if (requested.Count == 0 || requested.Count > 100)
        {
            return BadRequest(new { message = "1 to 100 weeks." });
        }

        var list = await SendAsync(HttpMethod.Get, UpstreamUrl("approvals")! + "?start=0&length=500", null);
        if (list is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Time Entry dependency unavailable." });
        }

        if (list.Value.Status != 200)
        {
            return new ContentResult { Content = list.Value.Content, ContentType = "application/json", StatusCode = list.Value.Status };
        }

        var rows = ApprovalRows(list.Value.Content);
        var approved = new List<Guid>();
        var skipped = new List<object>();
        foreach (var weekId in requested)
        {
            if (!rows.TryGetValue(weekId, out var row) || row.ApprovalTaskId is null)
            {
                skipped.Add(new { weekId, reasonCode = "TIMESHEET_APPROVAL_NOT_FOUND" });
                continue;
            }

            if (row.Marked)
            {
                skipped.Add(new { weekId, reasonCode = "TIMESHEET_BULK_MARKED" });
                continue;
            }

            var result = await SendAsync(HttpMethod.Post, WorkItemActionUrl(row.ApprovalTaskId.Value, "approve"),
                DecisionPayload(row.ApprovalTaskVersion, null));
            if (result is { Status: >= 200 and < 300 })
            {
                approved.Add(weekId);
            }
            else
            {
                skipped.Add(new { weekId, reasonCode = result is null ? "SERVICE_UNAVAILABLE" : ReasonCodeOf(result.Value.Content) });
            }
        }

        return Ok(new { data = new { approved, skipped }, isSuccessful = true, statusCode = 200 });
    }

    // ── T2b settings lookups — the existing platform readers, read-only ───────────────────────────────────────────

    /// <summary>The tenant's positions (MOD-0288), for the time-admin pool picker.</summary>
    [HttpGet("/TimeEntry/Settings/lookup/positions")]
    public Task<IActionResult> PositionsLookup() => ForwardAsync(HttpMethod.Get, $"{_gatewayUrl}/api/platform/positions", null);

    /// <summary>The tenant's legal entities (MDM lookup): the switch list shows every one, a missing switch row being OFF.</summary>
    [HttpGet("/TimeEntry/Settings/lookup/legal-entities")]
    public Task<IActionResult> LegalEntitiesLookup() => ForwardAsync(HttpMethod.Get, $"{_gatewayUrl}/api/legal-entities/lookup", null);

    public sealed record ApprovalDecisionRequest(Guid ApprovalTaskId, int? ExpectedVersion, string? Comment);

    public sealed record BulkApprovalRequest(IReadOnlyList<Guid>? WeekIds);

    private sealed record ApprovalRow(Guid? ApprovalTaskId, int? ApprovalTaskVersion, bool Marked);

    private string WorkItemActionUrl(Guid approvalTaskId, string code)
        => $"{_gatewayUrl}/api/v1/work-items/{approvalTaskId:D}/actions/{code}";

    private static string DecisionPayload(int? expectedVersion, string? comment)
        => System.Text.Json.JsonSerializer.Serialize(new
        {
            providerCode = "workflow",
            payload = new { expectedVersion = expectedVersion ?? 0, reasonCode = (string?)null, comment }
        });

    /// <summary>The caller's approvals as the page's own list read returned them, keyed by week, with the marks folded.</summary>
    private static Dictionary<Guid, ApprovalRow> ApprovalRows(string json)
    {
        var rows = new Dictionary<Guid, ApprovalRow>();
        using var document = System.Text.Json.JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data)
            || !data.TryGetProperty("items", out var items)
            || items.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return rows;
        }

        static int Count(System.Text.Json.JsonElement item, string name)
            => item.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.Array ? value.GetArrayLength() : 0;

        foreach (var item in items.EnumerateArray())
        {
            var weekId = item.GetProperty("weekId").GetGuid();
            var taskId = item.TryGetProperty("approvalTaskId", out var t) && t.ValueKind == System.Text.Json.JsonValueKind.String ? t.GetGuid() : (Guid?)null;
            var version = item.TryGetProperty("approvalTaskVersion", out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number ? v.GetInt32() : (int?)null;
            var outside = item.TryGetProperty("outsideWorkingMinutes", out var o) && o.ValueKind == System.Text.Json.JsonValueKind.Number ? o.GetInt32() : 0;
            var marked = Count(item, "flaggedDates") > 0 || Count(item, "autoClosedDates") > 0 || Count(item, "holidayDates") > 0 || outside > 0;
            rows[weekId] = new ApprovalRow(taskId, version, marked);
        }

        return rows;
    }

    private static string? ReasonCodeOf(string json)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("reason_code", out var code) ? code.GetString() : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    // ── Proxy plumbing ───────────────────────────────────────────────────────────────────────────────────────────

    private async Task<string?> ReadBodyAsync()
    {
        if (HttpMethods.IsGet(Request.Method) || HttpMethods.IsDelete(Request.Method))
        {
            return null;
        }

        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var relayed = await reader.ReadToEndAsync(HttpContext.RequestAborted);
        return string.IsNullOrEmpty(relayed) ? "{}" : relayed;
    }

    /// <summary>One upstream call. Null when there is no session token or the service could not be reached.</summary>
    private async Task<(int Status, string Content, string ContentType)?> SendAsync(HttpMethod method, string target, string? jsonBody)
    {
        if (!TryCreateTenantRequest(method, target, out var request))
        {
            return (401, "{\"message\":\"Unauthorized\"}", "application/json");
        }

        try
        {
            using (request)
            {
                if (jsonBody is not null)
                {
                    request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                }

                var client = _httpClientFactory.CreateClient();
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
                var content = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);
                return ((int)response.StatusCode, content, response.Content.Headers.ContentType?.ToString() ?? "application/json");
            }
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // v3 M5 — the service is down or slow (a restart, a closed port). Every tenant page asks for the timer chip, so
            // this is an expected, recoverable state: a Warning, not an Error flooding the log.
            _logger.LogWarning(ex, "Time Entry proxy could not reach {Method} {TargetUrl}.", method, target);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Time Entry proxy failed for {Method} {TargetUrl}.", method, target);
            return null;
        }
    }

    /// <summary>Forwards and hands the upstream status and body back VERBATIM: a 403, a 409 TIMESHEET_CONCURRENCY_CONFLICT,
    /// a 400 TIME_ENTRY_STEP_INVALID reach the page as themselves, so it can say exactly what happened.</summary>
    private async Task<IActionResult> ForwardAsync(HttpMethod method, string target, string? jsonBody)
    {
        if (!TryCreateTenantRequest(method, target, out var probe))
        {
            return Unauthorized(new { message = "Unauthorized" });
        }

        probe.Dispose();
        var result = await SendAsync(method, target, jsonBody);
        if (result is null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Time Entry dependency unavailable." });
        }

        return new ContentResult { Content = result.Value.Content, ContentType = result.Value.ContentType, StatusCode = result.Value.Status };
    }

    /// <summary>The upstream URL, or null when the path holds anything but plain segments.</summary>
    private string? UpstreamUrl(string? path)
    {
        var segments = (path ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(s => s is "." or ".."))
        {
            return null;
        }

        var escaped = string.Join('/', segments.Select(Uri.EscapeDataString));
        return $"{_gatewayUrl}/api/v1/time-entry/{escaped}{Request.QueryString.Value}";
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
