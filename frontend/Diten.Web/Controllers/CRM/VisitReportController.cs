using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.CRM;
using Diten.Web.Security;
using Diten.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.CRM;

/// <summary>
/// WP-VW-W4 (W4-WEB-a) — the visit report wizard v2 (<c>/CRM/VisitReport?plannedVisitId=…</c>): the doctor report
/// (general → one step per product: A presentation + interest, B market + competitor, C requests + next step →
/// follow-up + safety → the doctor profile card → summary + submit), the 60-minute free correction and the reasoned
/// amendment with old → new values. It is reached from the Visit Workspace / the Visit Presentation (no sidebar entry);
/// the v1 report screen (VisitExecution) stays for reading v1 reports.
/// <para>Every call is a same-origin proxy through the Gateway with EXACTLY the keys of its endpoint: the report reads
/// <c>crm.visit-report.read</c>; the draft / submit <c>crm.visit-report.record</c> + <c>crm.planned-visit.manage</c>; the
/// amendment <c>crm.visit-report.amend</c>; the planned visit <c>crm.planned-visit.read</c>; the execution (co-visitor
/// names, the presentation's note) <c>crm.visit-execution.read</c>; the doctor profile (history, availability, channel)
/// <c>crm.doctor-insights.read</c> or <c>.read-all</c>; the MDM competitor lookup <c>mdm.competitor-products.read</c> and
/// the rep's proposal <c>mdm.competitor-products.propose</c>; the sample's GSKUs <c>mdm.gskus.read</c>; the unplanned
/// product picker <c>mdm.global-products.read</c>. The BRD set the pre-fill does not carry (the competitor card's
/// reimbursement status) is read through the consumable-sets route. UAS-001:
/// without <c>crm.visit-report.read</c> the page draws only the access notice — no skeleton, no redirect.</para>
/// </summary>
[Authorize]
[Route("CRM/VisitReport")]
public sealed class VisitReportController : Controller
{
    public const string ReportRead = "crm.visit-report.read";
    public const string ReportRecord = "crm.visit-report.record";
    public const string ReportAmend = "crm.visit-report.amend";
    public const string PlannedVisitRead = "crm.planned-visit.read";
    public const string PlannedVisitManage = "crm.planned-visit.manage";
    public const string ExecutionRead = "crm.visit-execution.read";
    public const string InsightsRead = "crm.doctor-insights.read";
    public const string InsightsReadAll = "crm.doctor-insights.read-all";
    public const string CompetitorRead = "mdm.competitor-products.read";
    public const string CompetitorPropose = "mdm.competitor-products.propose";
    public const string GskuRead = "mdm.gskus.read";
    public const string ProductRead = "mdm.global-products.read";

    /// <summary>The BRD set this page reads besides the pre-fill's option sets (no other set is readable here).</summary>
    public static readonly string[] ReadableSets = ["mdm-reimbursement-status"];

    private static readonly string[] RecordKeys = [ReportRecord, PlannedVisitManage];
    private const string ViewRoot = "~/Views/CRM/VisitReport";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly CrmReferenceSetReader _referenceSets;
    private readonly ILogger<VisitReportController> _logger;

    public VisitReportController(HttpClient httpClient, IConfiguration configuration, ILogger<VisitReportController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _referenceSets = new CrmReferenceSetReader(httpClient, _gatewayUrl, logger);
        _logger = logger;
    }

    // ---------------- page ----------------

    [HttpGet("")]
    public IActionResult Index([FromQuery] Guid? plannedVisitId)
        => View($"{ViewRoot}/Index.cshtml", new VisitReportIndexViewModel
        {
            CanRead = HasAll(ReportRead),
            CanRecord = HasAll(RecordKeys),
            CanAmend = HasAll(ReportAmend),
            CanReadVisit = HasAll(PlannedVisitRead),
            CanReadExecution = HasAll(ExecutionRead),
            CanReadInsights = HasAny(InsightsRead, InsightsReadAll),
            CanLookupCompetitors = HasAll(CompetitorRead),
            CanProposeCompetitors = HasAll(CompetitorPropose),
            CanReadGskus = HasAll(GskuRead),
            CanPickProducts = HasAll(ProductRead),
            PlannedVisitId = plannedVisitId
        });

    // ---------------- the report ----------------

    [HttpGet("api/prefill/{plannedVisitId:guid}")]
    public Task<IActionResult> Prefill(Guid plannedVisitId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/visit-report/v2/prefill?plannedVisitId={plannedVisitId}", null, ct, ReportRead);

    [HttpGet("api/reports/{visitReportId:guid}")]
    public Task<IActionResult> Report(Guid visitReportId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/visit-report/{visitReportId}", null, ct, ReportRead);

    [HttpPut("api/draft")]
    public async Task<IActionResult> Draft(CancellationToken ct)
        => await ProxyAsync(HttpMethod.Put, "/api/crm/visit-report/v2/draft", await ReadBodyAsync(ct), ct, RecordKeys);

    [HttpPost("api/submit")]
    public async Task<IActionResult> Submit(CancellationToken ct)
        => await ProxyAsync(HttpMethod.Post, "/api/crm/visit-report/v2/submit", await ReadBodyAsync(ct), ct, RecordKeys);

    [HttpPost("api/reports/{visitReportId:guid}/amend")]
    public async Task<IActionResult> Amend(Guid visitReportId, CancellationToken ct)
        => await ProxyAsync(HttpMethod.Post, $"/api/crm/visit-report/{visitReportId}/amend", await ReadBodyAsync(ct), ct, ReportAmend);

    // ---------------- the visit, the execution, the presentation ----------------

    [HttpGet("api/visits/{plannedVisitId:guid}")]
    public Task<IActionResult> Visit(Guid plannedVisitId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/planned-visits/{plannedVisitId}", null, ct, PlannedVisitRead);

    [HttpGet("api/execution/by-plan/{plannedVisitId:guid}")]
    public Task<IActionResult> Execution(Guid plannedVisitId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/visit-execution/by-plan/{plannedVisitId}", null, ct, ExecutionRead);

    /// <summary>The W3 presentation (the objection library answers a product's slides carried).</summary>
    [HttpGet("api/presentation/{plannedVisitId:guid}")]
    public Task<IActionResult> Presentation(Guid plannedVisitId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/crm/visit-presentation/{plannedVisitId}{Request.QueryString}", null, ct, ReportRead);

    // ---------------- the doctor insights ----------------

    [HttpGet("api/doctors/{contactId:guid}/profile")]
    public Task<IActionResult> Profile(Guid contactId, CancellationToken ct)
        => ProxyAnyAsync(HttpMethod.Get, $"/api/crm/doctor-insights/doctors/{contactId}/profile", ct, InsightsRead, InsightsReadAll);

    [HttpGet("api/doctors/{contactId:guid}/potentials")]
    public Task<IActionResult> Potentials(Guid contactId, CancellationToken ct)
        => ProxyAnyAsync(HttpMethod.Get, $"/api/crm/doctor-insights/doctors/{contactId}/potentials{Request.QueryString}", ct, InsightsRead, InsightsReadAll);

    [HttpGet("api/products/{productId:guid}/indication-profiles")]
    public Task<IActionResult> IndicationProfiles(Guid productId, CancellationToken ct)
        => ProxyAnyAsync(HttpMethod.Get, $"/api/crm/doctor-insights/products/{productId}/indication-profiles", ct, InsightsRead, InsightsReadAll);

    // ---------------- MDM: competitors, GSKUs, products ----------------

    [HttpGet("api/competitors/lookup")]
    public Task<IActionResult> CompetitorLookup(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/mdm/competitor-products/lookup{Request.QueryString}", null, ct, CompetitorRead);

    /// <summary>The rep's "not in the list, add a competitor": name + company + our product (+ the report) → proposed.</summary>
    [HttpPost("api/competitors/proposals")]
    public async Task<IActionResult> CompetitorProposal(CancellationToken ct)
        => await ProxyAsync(HttpMethod.Post, "/api/mdm/competitor-products/proposals", await ReadBodyAsync(ct), ct, CompetitorPropose);

    [HttpGet("api/gskus")]
    public Task<IActionResult> Gskus(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/gskus{Request.QueryString}", null, ct, GskuRead);

    [HttpGet("api/products")]
    public Task<IActionResult> Products(CancellationToken ct)
        => ProxyAsync(HttpMethod.Get, $"/api/global-products/selector{Request.QueryString}", null, ct, ProductRead);

    /// <summary>A BRD set the pre-fill does not carry — only <see cref="ReadableSets"/>; the page key is enough.</summary>
    [HttpGet("api/reference-data/{setCode}/values")]
    public async Task<IActionResult> ReferenceValues(string setCode, CancellationToken ct)
    {
        if (!HasAll(ReportRead))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
        }

        if (!ReadableSets.Contains(setCode, StringComparer.Ordinal))
        {
            return NotFound(new { errors = new[] { "Unknown set." } });
        }

        var response = await _referenceSets.ReadAsync(setCode, Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request), GetTenantId(), ct);
        return await ToProxyResultAsync(response, ct);
    }

    // ---------------- proxy helpers ----------------

    private Task<IActionResult> ProxyAnyAsync(HttpMethod method, string path, CancellationToken ct, params string[] anyPermission)
        => HasAny(anyPermission)
            ? SendAndMapAsync(method, path, null, ct)
            : Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." }));

    private async Task<IActionResult> ProxyAsync(HttpMethod method, string path, string? rawBody, CancellationToken ct, params string[] allPermissions)
    {
        if (!HasAll(allPermissions))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });
        }

        if (rawBody is not null && ContainsTenantId(rawBody))
        {
            return BadRequest(new { errors = new[] { "TenantId is server-resolved and must not be supplied." } });
        }

        return await SendAndMapAsync(method, path, rawBody, ct);
    }

    private async Task<IActionResult> SendAndMapAsync(HttpMethod method, string path, string? rawBody, CancellationToken ct)
        => await ToProxyResultAsync(await SendGatewayAsync(method, path, rawBody, ct), ct);

    private async Task<string?> ReadBodyAsync(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;
        return string.IsNullOrWhiteSpace(body) ? null : body;
    }

    private async Task<HttpResponseMessage?> SendGatewayAsync(HttpMethod method, string path, string? rawBody, CancellationToken ct)
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

            // The option labels (objection categories) come in the page language.
            var language = Request.Headers.AcceptLanguage.ToString();
            if (!string.IsNullOrWhiteSpace(language))
            {
                request.Headers.TryAddWithoutValidation("Accept-Language", language);
            }

            // The draft's compare-and-swap travels as If-Match too (the CRM reads the header first).
            var ifMatch = Request.Headers.IfMatch.ToString();
            if (!string.IsNullOrWhiteSpace(ifMatch))
            {
                request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
            }

            if (rawBody is not null)
            {
                request.Content = new StringContent(rawBody, Encoding.UTF8, "application/json");
            }
            else if (method == HttpMethod.Post || method == HttpMethod.Put)
            {
                request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            }

            return await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Visit report Gateway request failed: {Method} {Path}", method, path);
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

    private bool HasAll(params string[] permissions) => permissions.All(x => PermissionClaims.HasPermission(User, x));

    private bool HasAny(params string[] permissions) => permissions.Any(x => PermissionClaims.HasPermission(User, x));
}
