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
/// MOD-0155 FU02 — the Visit Report EXECUTION calendar (D-CALENDAR-UI = A, bespoke tenant-shell). The EXECUTION
/// counterpart of FU05's setup console: the rep opens a Day/Week calendar of the FU01 plan atoms, marks each
/// done/missed/rescheduled inline, and records the immutable Visit Report + amendments. All business traffic is proxied
/// server-side through the Gateway; the browser never sees a service URL or a bearer token, and the CrmService runtime
/// stays the authoritative permission + validation layer. This is NOT a Golden DataTable surface (verify_datatable_page
/// N/A).
/// <para>read = crm.visit-report.read; record (outcome + submit) = crm.visit-report.record + FU01 crm.planned-visit.manage;
/// amend = crm.visit-report.amend. None is seeded (F-RBAC), so each check accepts the documented DEV-ONLY territory
/// fallback, mirroring the CrmService controller — the fallback widens no guard (tenant isolation, the lifecycle,
/// immutability and the fail-closed vocabulary all still run behind it).</para>
/// </summary>
[Authorize]
[Route("CRM/VisitExecution")]
public sealed class CrmVisitExecutionController : Controller
{
    private const string ReadPermission = "crm.visit-report.read";
    private const string RecordPermission = "crm.visit-report.record";
    private const string AmendPermission = "crm.visit-report.amend";
    private const string PlannedVisitManage = "crm.planned-visit.manage";

    // Documented DEV-ONLY fallback until F-RBAC lands (already granted to CRM roles). Not for production.
    private const string ReadFallback = "crm.territory.read";
    private const string ManageFallback = "crm.territory.model.manage";
    private const string ViewRoot = "~/Views/CRM/VisitExecution";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<CrmVisitExecutionController> _logger;

    public CrmVisitExecutionController(
        HttpClient httpClient, IConfiguration configuration, ILogger<CrmVisitExecutionController> logger)
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
        if (!HasAnyPermission(ReadPermission, ReadFallback))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return View($"{ViewRoot}/Index.cshtml", new VisitExecutionIndexViewModel
        {
            CanRecord = HasAnyPermission(RecordPermission, ManageFallback)
                        && HasAnyPermission(PlannedVisitManage, ManageFallback),
            CanAmend = HasAnyPermission(AmendPermission, ManageFallback)
        });
    }

    // ---------------- read proxies ----------------

    [HttpGet("api/contract")]
    public Task<IActionResult> Contract(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, "/api/crm/visit-report/contract", null, ct, ReadPermission, ReadFallback);

    [HttpGet("api/calendar")]
    public Task<IActionResult> Calendar(CancellationToken ct)
        => ProxyAsync(
            HttpMethod.Get, $"/api/crm/visit-report/calendar{Request.QueryString}", null, ct, ReadPermission, ReadFallback);

    [HttpGet("api/reports")]
    public Task<IActionResult> ListReports(CancellationToken ct)
        => ProxyAsync(
            HttpMethod.Get, $"/api/crm/visit-report{Request.QueryString}", null, ct, ReadPermission, ReadFallback);

    [HttpGet("api/reports/{visitReportId:guid}")]
    public Task<IActionResult> GetReport(Guid visitReportId, CancellationToken ct)
        => ProxyAsync(
            HttpMethod.Get, $"/api/crm/visit-report/{visitReportId}", null, ct, ReadPermission, ReadFallback);

    // ---------------- write proxies ----------------

    [HttpPost("api/outcome")]
    public Task<IActionResult> RecordOutcome(CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Post, "/api/crm/visit-report/outcome", ct, RecordPermission, ManageFallback);

    [HttpPost("api/reports")]
    public Task<IActionResult> Submit(CancellationToken ct)
        => ProxyBodyAsync(HttpMethod.Post, "/api/crm/visit-report", ct, RecordPermission, ManageFallback);

    [HttpPost("api/reports/{visitReportId:guid}/amend")]
    public Task<IActionResult> Amend(Guid visitReportId, CancellationToken ct)
        => ProxyBodyAsync(
            HttpMethod.Post, $"/api/crm/visit-report/{visitReportId}/amend", ct, AmendPermission, ManageFallback);

    // ---------------- proxy helpers ----------------

    private async Task<IActionResult> ProxyBodyAsync(
        HttpMethod method, string path, CancellationToken ct, params string[] anyPermission)
    {
        var body = await ReadBodyAsync(ct);
        return await ProxyAsync(method, path, body, ct, anyPermission);
    }

    private async Task<IActionResult> ProxyAsync(
        HttpMethod method, string path, string? rawBody, CancellationToken ct, params string[] anyPermission)
    {
        if (!HasAnyPermission(anyPermission))
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
            _logger.LogError(ex, "Visit execution Gateway request failed: {Method} {Path}", method, path);
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
