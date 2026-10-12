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
/// WP-VW-W3 (W3-WEB-b) — the Visit Presentation page (<c>/CRM/VisitPresentation?plannedVisitId=…[&amp;phase=summary]</c>):
/// the doctor's screen (E4a), the rep's remote (E4b) and the finish summary with the doctor's signature (E4c). It is
/// reached from the Visit Workspace only (no sidebar entry). All business traffic is proxied server-side through the
/// Gateway; the browser never sees a service URL or a bearer token, and the CrmService stays the authority.
/// <para>Every proxy requires EXACTLY the keys its endpoint requires: the presentation read <c>crm.visit-report.read</c>;
/// the planned visit (name, institution) <c>crm.planned-visit.read</c>; the execution reads (contract, by-plan, brief)
/// <c>crm.visit-execution.read</c>; capture / finish / signature <c>crm.visit-execution.record</c>; the co-visitor
/// people lookup the Meetings attendee lookup's <c>platform.meetings.create</c>. UAS-001: without the read keys the
/// page draws only the access notice — no skeleton, no redirect.</para>
/// </summary>
[Authorize]
[Route("CRM/VisitPresentation")]
public sealed class VisitPresentationController : Controller
{
    public const string VisitReportRead = "crm.visit-report.read";
    public const string VisitExecutionRead = "crm.visit-execution.read";
    public const string VisitExecutionRecord = "crm.visit-execution.record";
    public const string PlannedVisitRead = "crm.planned-visit.read";
    public const string PeopleLookup = "platform.meetings.create";

    private static readonly string[] PageRead = [VisitReportRead, VisitExecutionRead];

    /// <summary>The signature png (≤ 10 MB) plus the multipart envelope.</summary>
    private const long MaxSignatureRequestBytes = 11L * 1024 * 1024;

    private const string ViewRoot = "~/Views/CRM/VisitPresentation";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<VisitPresentationController> _logger;

    public VisitPresentationController(
        HttpClient httpClient, IConfiguration configuration, ILogger<VisitPresentationController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _logger = logger;
    }

    // ---------------- page ----------------

    [HttpGet("")]
    public IActionResult Index([FromQuery] Guid? plannedVisitId, [FromQuery] string? phase)
        => View($"{ViewRoot}/Index.cshtml", new VisitPresentationIndexViewModel
        {
            CanRead = HasAll(PageRead),
            CanRecord = HasAll(VisitExecutionRecord),
            CanReadVisit = HasAll(PlannedVisitRead),
            CanPickPeople = HasAll(PeopleLookup),
            PlannedVisitId = plannedVisitId,
            Summary = string.Equals(phase, "summary", StringComparison.OrdinalIgnoreCase)
        });

    // ---------------- reads ----------------

    [HttpGet("api/presentation/{plannedVisitId:guid}")]
    public Task<IActionResult> Presentation(Guid plannedVisitId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/visit-presentation/{plannedVisitId}{Request.QueryString}", null, null, ct, VisitReportRead);

    [HttpGet("api/visits/{plannedVisitId:guid}")]
    public Task<IActionResult> Visit(Guid plannedVisitId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/planned-visits/{plannedVisitId}", null, null, ct, PlannedVisitRead);

    [HttpGet("api/execution/contract")]
    public Task<IActionResult> Contract(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, "/api/crm/visit-execution/contract", null, null, ct, VisitExecutionRead);

    [HttpGet("api/execution/by-plan/{plannedVisitId:guid}")]
    public Task<IActionResult> ByPlan(Guid plannedVisitId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/visit-execution/by-plan/{plannedVisitId}", null, null, ct, VisitExecutionRead);

    [HttpGet("api/execution/by-plan/{plannedVisitId:guid}/brief")]
    public Task<IActionResult> Brief(Guid plannedVisitId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/visit-execution/by-plan/{plannedVisitId}/brief", null, null, ct, VisitExecutionRead);

    /// <summary>The co-visitor picker's people: the existing Meetings attendee lookup (active tenant users, the same
    /// AssignablePersonDto rows the shared person picker renders).</summary>
    [HttpGet("api/people")]
    public Task<IActionResult> People(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, "/api/v1/meetings/lookups/attendees", null, null, ct, PeopleLookup);

    // ---------------- writes ----------------

    [HttpPut("api/execution/{executionId:guid}/capture")]
    public async Task<IActionResult> Capture(Guid executionId, CancellationToken ct)
        => await ProxyAsync(HttpMethod.Put, $"/api/crm/visit-execution/{executionId}/capture", await ReadBodyAsync(ct), null, ct, VisitExecutionRecord);

    [HttpPost("api/execution/{executionId:guid}/finish")]
    public async Task<IActionResult> Finish(Guid executionId, CancellationToken ct)
        => await ProxyAsync(HttpMethod.Post, $"/api/crm/visit-execution/{executionId}/finish", await ReadBodyAsync(ct), null, ct, VisitExecutionRecord);

    /// <summary>The doctor's signature: the browser's multipart (file png, confirmed, context) is streamed as-is.</summary>
    [HttpPost("api/execution/{executionId:guid}/signature")]
    [RequestSizeLimit(MaxSignatureRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxSignatureRequestBytes)]
    public async Task<IActionResult> Signature(Guid executionId, CancellationToken ct)
    {
        if (!HasAll(VisitExecutionRecord))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
        }

        var contentType = Request.ContentType;
        if (string.IsNullOrWhiteSpace(contentType)
            || !contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { errors = new[] { "A multipart/form-data body is required.", "visit_capture_invalid" } });
        }

        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, ct);
        var content = new ByteArrayContent(buffer.ToArray());
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        return await ProxyAsync(HttpMethod.Post, $"/api/crm/visit-execution/{executionId}/signature", null, content, ct, VisitExecutionRecord);
    }

    // ---------------- proxy helpers ----------------

    private async Task<IActionResult> ProxyAsync(
        HttpMethod method, string path, string? rawBody, HttpContent? content, CancellationToken ct, params string[] allPermissions)
    {
        if (!HasAll(allPermissions))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
        }

        if (rawBody is not null && ContainsTenantId(rawBody))
        {
            return BadRequest(new { errors = new[] { "TenantId is server-resolved and must not be supplied." } });
        }

        var response = await SendGatewayAsync(method, path, rawBody, content, ct);
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
        HttpMethod method, string path, string? rawBody, HttpContent? content, CancellationToken ct)
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

            // The objection category labels come in the page language (the CRM reads Accept-Language without ?lang).
            var language = Request.Headers.AcceptLanguage.ToString();
            if (!string.IsNullOrWhiteSpace(language))
            {
                request.Headers.TryAddWithoutValidation("Accept-Language", language);
            }

            if (content is not null)
            {
                request.Content = content;
            }
            else if (rawBody is not null)
            {
                request.Content = new StringContent(rawBody, Encoding.UTF8, "application/json");
            }
            else if (method == HttpMethod.Post || method == HttpMethod.Put)
            {
                // finish takes an optional body: send an empty JSON object, never a bodiless POST the CRM binder refuses.
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            }

            return await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Visit presentation Gateway request failed: {Method} {Path}", method, path);
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
