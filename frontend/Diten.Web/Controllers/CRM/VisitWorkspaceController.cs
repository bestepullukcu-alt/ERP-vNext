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

    /// <summary>WP-VW-W2 (WEB-b) — Plan mode writes through the Visit Planning session update (its generate key).</summary>
    public const string VisitPlanGenerate = "crm.visit-plan.generate";

    private static readonly string[] WorkspaceRead = [VisitReportRead, VisitPlanRead];
    private static readonly string[] RecordKeys = [VisitReportRecord, PlannedVisitManage];
    private static readonly string[] ApplyKeys = [VisitPlanApply, PlannedVisitManage];
    private static readonly string[] PlanKeys = [VisitPlanRead, VisitPlanGenerate];

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
            CanPlan = HasAll(PlanKeys)
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

            // The reason labels come in the page language (the CRM reads Accept-Language when ?lang is absent).
            var language = Request.Headers.AcceptLanguage.ToString();
            if (!string.IsNullOrWhiteSpace(language))
            {
                request.Headers.TryAddWithoutValidation("Accept-Language", language);
            }

            if (rawBody is not null)
            {
                request.Content = new StringContent(rawBody, Encoding.UTF8, "application/json");
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
