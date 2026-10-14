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
/// WP-VW-W2 (WEB-a) — the Visit Workspace: one calendar to execute the rep's week (plan · visit · report). All business
/// traffic is proxied server-side through the Gateway; the browser never sees a service URL or a bearer token, and the
/// CrmService stays the authoritative permission + validation layer.
/// <para>Every proxy requires EXACTLY the keys its CRM endpoint requires (ALL of them, not any): the workspace reads
/// <c>crm.visit-report.read</c> + <c>crm.visit-plan.read</c>; cancel / unplanned <c>crm.planned-visit.manage</c>; not done /
/// reschedule (outcome + submit) <c>crm.visit-report.record</c> + <c>crm.planned-visit.manage</c>; approve / reopen a
/// week <c>crm.visit-plan.apply</c> + <c>crm.planned-visit.manage</c>; the doctor search <c>crm.contact.read</c>.</para>
/// <para>WP-VW-W3 (WEB-a) — the visit execution: its contract <c>crm.visit-execution.read</c>; the start (multipart: the
/// photo + the position, STREAMED through, never buffered here) <c>crm.visit-execution.record</c>. The CRM checks the
/// ownership, the day, the setting and the distance; the instant is the server's.</para>
/// </summary>
[Authorize]
[Route("CRM/VisitWorkspace")]
public sealed class VisitWorkspaceController : Controller
{
    public const string VisitReportRead = "crm.visit-report.read";
    public const string VisitReportRecord = "crm.visit-report.record";
    public const string VisitPlanRead = "crm.visit-plan.read";
    public const string VisitPlanApply = "crm.visit-plan.apply";
    public const string PlannedVisitManage = "crm.planned-visit.manage";
    public const string ContactRead = "crm.contact.read";
    public const string AccountRead = "crm.account.read";

    /// <summary>WP-VW-W2 (WEB-b) — Plan mode writes through the Visit Planning session update (its generate key).</summary>
    public const string VisitPlanGenerate = "crm.visit-plan.generate";

    /// <summary>WP-VW-W3 (WEB-a) — the visit execution keys (Platform CrmManifestProvider; no seed / grant here).</summary>
    public const string VisitExecutionRead = "crm.visit-execution.read";
    public const string VisitExecutionRecord = "crm.visit-execution.record";

    /// <summary>The CRM's start ceiling: the 10 MB evidence plus the multipart envelope (VisitExecutionController).</summary>
    public const long MaxStartBytes = 11L * 1024 * 1024;

    private static readonly string[] WorkspaceRead = [VisitReportRead, VisitPlanRead];
    private static readonly string[] RecordKeys = [VisitReportRecord, PlannedVisitManage];
    private static readonly string[] ApplyKeys = [VisitPlanApply, PlannedVisitManage];
    private static readonly string[] PlanKeys = [VisitPlanRead, VisitPlanGenerate];
    private static readonly string[] ExecutionKeys = [VisitExecutionRead, VisitExecutionRecord];

    private const string ViewRoot = "~/Views/CRM/VisitWorkspace";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<VisitWorkspaceController> _logger;

    public VisitWorkspaceController(
        HttpClient httpClient, IConfiguration configuration, ILogger<VisitWorkspaceController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _logger = logger;
    }

    // ---------------- page ----------------

    /// <summary>The page. Without the read keys the view draws only the access notice (UAS-001, no redirect).</summary>
    [HttpGet("")]
    public IActionResult Index()
        => View($"{ViewRoot}/Index.cshtml", new VisitWorkspaceIndexViewModel
        {
            CanRead = HasAll(WorkspaceRead),
            CanManageVisits = HasAll(PlannedVisitManage),
            CanRecord = HasAll(RecordKeys),
            CanApplyWeek = HasAll(ApplyKeys),
            CanSearchContacts = HasAll(ContactRead),
            CanSearchPharmacies = HasAll(AccountRead),
            CanPlan = HasAll(PlanKeys),
            CanExecute = HasAll(ExecutionKeys)
        });

    // ---------------- workspace reads ----------------

    [HttpGet("api/contract")]
    public Task<IActionResult> Contract(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, "/api/crm/visit-workspace/contract", null, ct, WorkspaceRead);

    [HttpGet("api/calendar")]
    public Task<IActionResult> Calendar(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/visit-workspace/calendar{Request.QueryString}", null, ct, WorkspaceRead);

    [HttpGet("api/reasons")]
    public Task<IActionResult> Reasons(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/visit-workspace/reasons{Request.QueryString}", null, ct, WorkspaceRead);

    [HttpGet("api/reschedule-options")]
    public Task<IActionResult> RescheduleOptions(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/visit-workspace/reschedule-options{Request.QueryString}", null, ct, WorkspaceRead);

    /// <summary>The last report of a visit (the detail panel's "from the previous visit").</summary>
    [HttpGet("api/reports/{visitReportId:guid}")]
    public Task<IActionResult> Report(Guid visitReportId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/visit-report/{visitReportId}", null, ct, VisitReportRead);

    /// <summary>The week's plan (its version, for approve / reopen).</summary>
    [HttpGet("api/sessions/{planningSessionId:guid}")]
    public Task<IActionResult> Session(Guid planningSessionId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/visit-plan/sessions/{planningSessionId}", null, ct, VisitPlanRead);

    /// <summary>The plan's doctors with their period status (the detail panel's frequency line).</summary>
    [HttpGet("api/sessions/{planningSessionId:guid}/targets")]
    public Task<IActionResult> SessionTargets(Guid planningSessionId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/visit-plan/sessions/{planningSessionId}/targets{Request.QueryString}",
            null, ct, VisitPlanRead);

    /// <summary>The unplanned-visit dialog's doctor search (the existing contact read).</summary>
    [HttpGet("api/contacts/search")]
    public Task<IActionResult> SearchContacts(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/contacts/search{Request.QueryString}", null, ct, ContactRead);

    /// <summary>W4-FIX-c (G-3) — the unplanned-visit dialog's PHARMACY search: the existing CRM account list, pharmacies only
    /// (<c>accountType=pharmacy</c>, the type the CRM requires of a pharmacy target), at most 10.</summary>
    [HttpGet("api/pharmacies/search")]
    public Task<IActionResult> SearchPharmacies([FromQuery] string? search, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/accounts?search={Uri.EscapeDataString((search ?? string.Empty).Trim())}&accountType=pharmacy&page=1&pageSize=10", null, ct, AccountRead);

    // ---------------- writes (existing CRM endpoints) ----------------

    /// <summary>Cancel with a reason code + note.</summary>
    [HttpPost("api/planned-visits/{plannedVisitId:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid plannedVisitId, CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Post, $"/api/crm/planned-visits/{plannedVisitId}/cancel", ct, PlannedVisitManage);

    /// <summary>An UNPLANNED visit (today only). This page creates nothing else: a body without <c>unplanned: true</c>
    /// is refused here, so the workspace can never be a back door to the manual create.</summary>
    [HttpPost("api/planned-visits/unplanned")]
    public async Task<IActionResult> CreateUnplanned(CancellationToken ct)
    {
        var body = await ReadBodyAsync(ct);
        if (body is null || !IsUnplanned(body))
        {
            return BadRequest(new { errors = new[] { "Only an unplanned visit can be created here.", "unplanned_visit_required" } });
        }

        return await ProxyAsync(HttpMethod.Post, "/api/crm/planned-visits", body, ct, PlannedVisitManage);
    }

    /// <summary>Not done / reschedule — step 1: the outcome (reason, note, new day) as a draft.</summary>
    [HttpPost("api/outcome")]
    public Task<IActionResult> RecordOutcome(CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Post, "/api/crm/visit-report/outcome", ct, RecordKeys);

    /// <summary>Not done / reschedule — step 2: submit (finalise; a reschedule creates the new visit here).</summary>
    [HttpPost("api/reports")]
    public Task<IActionResult> Submit(CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Post, "/api/crm/visit-report", ct, RecordKeys);

    /// <summary>Approve the week (the Visit Planning apply, with weekStart).</summary>
    [HttpPost("api/apply")]
    public Task<IActionResult> ApproveWeek(CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Post, "/api/crm/visit-plan/apply", ct, ApplyKeys);

    /// <summary>Reopen an approved week (the 4D reason goes into the week's history).</summary>
    [HttpPost("api/sessions/{planningSessionId:guid}/weeks/{weekStart}/reopen")]
    public Task<IActionResult> ReopenWeek(Guid planningSessionId, string weekStart, CancellationToken ct)
        => ProxyBodyAsync(
            HttpMethod.Post,
            $"/api/crm/visit-plan/sessions/{planningSessionId}/weeks/{Uri.EscapeDataString(weekStart)}/reopen",
            ct, ApplyKeys);

    // ---------------- WP-VW-W3 (WEB-a) — the visit execution ----------------

    /// <summary>The execution contract: the tenant's evidence setting (policy), the codes, the limits.</summary>
    [HttpGet("api/execution/contract")]
    public Task<IActionResult> ExecutionContract(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, "/api/crm/visit-execution/contract", null, ct, VisitExecutionRead);

    /// <summary>Start the visit: multipart/form-data (plannedVisitId, kind, latitude / longitude / accuracyMeters or
    /// locationError, photo, photoCapturedAt) streamed to the CRM as it came (the boundary kept). Anything but
    /// multipart is refused here; the CRM answers the rest (409 / 400 / 503 with its codes).</summary>
    [HttpPost("api/execution/start")]
    [Diten.Web.Filters.DisableFormValueModelBinding] // CT live E4: keep the multipart body unread for the copy
    [RequestSizeLimit(MaxStartBytes)]
    public async Task<IActionResult> StartVisit(CancellationToken ct)
    {
        if (!HasAll(VisitExecutionRecord))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
        }

        var contentType = Request.ContentType;
        if (string.IsNullOrWhiteSpace(contentType)
            || !contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase)
            || !MediaTypeHeaderValue.TryParse(contentType, out var mediaType))
        {
            return BadRequest(new { errors = new[] { "multipart/form-data is required.", "visit_capture_invalid" } });
        }

        // CT (live E4, 2026-10-12): the body is BUFFERED (bounded by [RequestSizeLimit]) and sent as bytes — the same as
        // the presentation page's signature proxy. Streaming Kestrel's request body straight into the gateway call with
        // a pinned Content-Length failed live ("Gateway unavailable", 502) although every unit test with an in-memory
        // body passed.
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, ct);
        var boundary = mediaType.Parameters.FirstOrDefault(x =>
            string.Equals(x.Name, "boundary", StringComparison.OrdinalIgnoreCase))?.Value?.Trim('"');
        if (string.IsNullOrWhiteSpace(boundary))
        {
            return BadRequest(new { errors = new[] { "multipart/form-data needs a boundary.", "visit_capture_invalid" } });
        }

        // WP-VW-W3E (BE-b, K-W3E-10): a browser cannot vouch for its device — the start goes on as clientPlatform=web
        // and the device verification fields never leave this proxy (whatever the page sent).
        var (body, rewrittenType) = await WebStartBodyAsync(buffer.ToArray(), boundary, ct);
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = rewrittenType;

        var response = await SendGatewayAsync(HttpMethod.Post, "/api/crm/visit-execution/start", content, ct);
        return await ToProxyResultAsync(response, ct);
    }

    /// <summary>WP-VW-W3E (BE-b) — the fields a browser's start may NOT carry to the CRM: the device verification claims
    /// (and its own platform, which the proxy writes itself).</summary>
    public static readonly IReadOnlyList<string> WebStartDroppedFields =
        ["clientPlatform", "mockLocationDetected", "integrityProvider", "integrityVerdict", "integrityToken"];

    /// <summary>
    /// WP-VW-W3E (BE-b, K-W3E-10) — Static, so MVC never treats it as an action: the start's multipart body rebuilt with
    /// every part copied as it came (the photo bytes, its file name and type, the location fields) EXCEPT
    /// <see cref="WebStartDroppedFields"/>, plus <c>clientPlatform=web</c>. A new boundary; the result is sent as bytes
    /// (the W3 live-E4 rule: buffered, never a streamed body).
    /// </summary>
    public static async Task<(byte[] Body, MediaTypeHeaderValue ContentType)> WebStartBodyAsync(byte[] original, string boundary, CancellationToken ct)
    {
        using var rebuilt = new MultipartFormDataContent();
        var reader = new Microsoft.AspNetCore.WebUtilities.MultipartReader(boundary, new MemoryStream(original));
        while (await reader.ReadNextSectionAsync(ct) is { } section)
        {
            if (!Microsoft.Net.Http.Headers.ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)) continue;
            var name = Microsoft.Net.Http.Headers.HeaderUtilities.RemoveQuotes(disposition.Name).Value ?? string.Empty;
            if (WebStartDroppedFields.Contains(name, StringComparer.OrdinalIgnoreCase)) continue;

            using var part = new MemoryStream();
            await section.Body.CopyToAsync(part, ct);
            var bytes = new ByteArrayContent(part.ToArray());
            if (!string.IsNullOrWhiteSpace(section.ContentType)) bytes.Headers.ContentType = MediaTypeHeaderValue.Parse(section.ContentType);
            var fileName = Microsoft.Net.Http.Headers.HeaderUtilities.RemoveQuotes(disposition.FileName).Value;
            if (string.IsNullOrEmpty(fileName)) rebuilt.Add(bytes, name);
            else rebuilt.Add(bytes, name, fileName);
        }

        rebuilt.Add(new StringContent("web"), "clientPlatform");
        return (await rebuilt.ReadAsByteArrayAsync(ct), rebuilt.Headers.ContentType!);
    }

    // ---------------- proxy helpers ----------------

    /// <summary>Static, so MVC never treats it as an action: does the JSON body say <c>unplanned: true</c>?</summary>
    public static bool IsUnplanned(string rawBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.EnumerateObject().Any(p =>
                       string.Equals(p.Name, "unplanned", StringComparison.OrdinalIgnoreCase)
                       && p.Value.ValueKind == JsonValueKind.True);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private async Task<IActionResult> ProxyBodyAsync(
        HttpMethod method, string path, CancellationToken ct, params string[] allPermissions)
    {
        var body = await ReadBodyAsync(ct);
        return await ProxyAsync(method, path, body, ct, allPermissions);
    }

    private async Task<IActionResult> ProxyAsync(
        HttpMethod method, string path, string? rawBody, CancellationToken ct, params string[] allPermissions)
    {
        if (!HasAll(allPermissions))
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

    private Task<HttpResponseMessage?> SendGatewayAsync(
        HttpMethod method, string path, string? rawBody, CancellationToken ct)
        => SendGatewayAsync(method, path,
            rawBody is null ? null : new StringContent(rawBody, Encoding.UTF8, "application/json"), ct);

    /// <summary>WP-VW-W3 — the one gateway send: the bearer, the tenant, the page language, any content (JSON or a
    /// streamed multipart). The content is owned (and disposed) by the request.</summary>
    private async Task<HttpResponseMessage?> SendGatewayAsync(
        HttpMethod method, string path, HttpContent? content, CancellationToken ct)
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

            // The reason labels come in the page language (the CRM reads Accept-Language when ?lang is absent).
            var language = Request.Headers.AcceptLanguage.ToString();
            if (!string.IsNullOrWhiteSpace(language))
            {
                request.Headers.TryAddWithoutValidation("Accept-Language", language);
            }

            if (content is not null)
            {
                request.Content = content;
            }

            return await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Visit workspace Gateway request failed: {Method} {Path}", method, path);
            return null;
        }
    }

    private static async Task<IActionResult> ToProxyResultAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null)
        {
            return new ObjectResult(new { errors = new[] { "Gateway unavailable." } }) { StatusCode = 502 };
        }

        // A bodiless upstream status (204/304/…) must not be turned into a body — the same-origin-proxy 204→500 trap.
        if (IsBodilessStatus(response.StatusCode))
        {
            return new StatusCodeResult((int)response.StatusCode);
        }

        var content = await response.Content.ReadAsStringAsync(ct);

        // A refusal that never reached a handler (model binding auto-400) is an RFC 7807 ProblemDetails; hand the page
        // ONE shape (the envelope) — the Visit Execution proxy's rule.
        if (CrmVisitExecutionController.ProblemDetailsToEnvelope(
                response.Content.Headers.ContentType?.MediaType, content, (int)response.StatusCode) is { } envelope)
        {
            return new ContentResult { StatusCode = (int)response.StatusCode, ContentType = "application/json", Content = envelope };
        }

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

    private bool HasAll(params string[] permissions) =>
        permissions.All(x => PermissionClaims.HasPermission(User, x));
}
