using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.MasterData;
using Diten.Web.Security;
using Diten.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.MasterData;

/// <summary>
/// WP-VW-W4 (W4-WEB-c) — MDM competitor products (<c>/MasterDataManagement/CompetitorProducts</c>, the BE-b2 manifest page):
/// the list with its two tabs (records · the reps' proposals), the separate new / edit / detail pages with section cards,
/// the direct merge of two active records (3 steps, "Birleştir"), and a proposal's page — the decision draft
/// (complete / merge / reject) sent for approval through MOD-0023 ("Onaya gönder"), withdrawn by its submitter and
/// decided by ANOTHER person (the Work Center deep link <c>Proposals/{id}</c>; SoD).
/// <para>Every call is a same-origin proxy through the Gateway with EXACTLY its endpoint's keys: reads
/// <c>mdm.competitor-products.read</c>; writes, the proposals and the decisions <c>.manage</c>; our products
/// <c>mdm.global-products.read</c>; the dosage forms (the MDM vocabulary, G4) the brand/product contract
/// (<c>mdm.brands.read</c>); the indication picker (G3) the CRM's <c>crm.doctor-insights.read</c> | <c>.read-all</c> |
/// <c>.thresholds.manage</c>; the report usage counts (G2) <c>crm.visit-report.read-aggregate</c>; the co-workers' names
/// the Meetings attendee lookup (<c>platform.meetings.create</c>). UAS-001: without the read key only the access notice.</para>
/// </summary>
[Authorize]
[Route("MasterDataManagement/CompetitorProducts")]
public sealed class CompetitorProductsController : Controller
{
    public const string Read = "mdm.competitor-products.read";
    public const string Manage = "mdm.competitor-products.manage";
    public const string ProductRead = "mdm.global-products.read";
    public const string BrandRead = "mdm.brands.read";
    public const string InsightsRead = "crm.doctor-insights.read";
    public const string InsightsReadAll = "crm.doctor-insights.read-all";
    public const string ThresholdsManage = "crm.doctor-insights.thresholds.manage";
    public const string UsageRead = "crm.visit-report.read-aggregate";
    public const string PeopleLookup = "platform.meetings.create";

    /// <summary>The BRD sets this page reads (the reimbursement status labels; the verified market catalog).</summary>
    public static readonly string[] ReadableSets = ["mdm-reimbursement-status", "market"];

    private const string MdmBase = "/api/mdm/competitor-products";
    private const string ViewRoot = "~/Views/MasterData/CompetitorProducts";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly CrmReferenceSetReader _referenceSets;
    private readonly ILogger<CompetitorProductsController> _logger;

    public CompetitorProductsController(HttpClient httpClient, IConfiguration configuration, ILogger<CompetitorProductsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _referenceSets = new CrmReferenceSetReader(httpClient, _gatewayUrl, logger);
        _logger = logger;
    }

    // ---------------- pages ----------------

    [HttpGet("")]
    public IActionResult Index([FromQuery] string? tab) => Page("Index", null, null, string.Equals(tab, "proposals", StringComparison.OrdinalIgnoreCase) ? "proposals" : "records");

    [HttpGet("Create")]
    public IActionResult Create() => Page("Edit", null, null, "create");

    [HttpGet("{id:guid}/Edit")]
    public IActionResult Edit(Guid id) => Page("Edit", id, null, "edit");

    [HttpGet("{id:guid}")]
    public IActionResult Details(Guid id) => Page("Details", id, null, "details");

    /// <summary>R3 "Başka kayıtla birleştir" — two ACTIVE records, directly (no approval).</summary>
    [HttpGet("{id:guid}/Merge")]
    public IActionResult Merge(Guid id) => Page("Merge", id, null, "merge");

    /// <summary>A proposal (also the Work Center deep link): its card, its decision, the approver's buttons.</summary>
    [HttpGet("Proposals/{proposalId:guid}")]
    public IActionResult Proposal(Guid proposalId) => Page("Proposal", null, proposalId, "proposal");

    /// <summary>R4 — "Onayla ve tamamla": the form filled from the proposal; it saves the decision draft.</summary>
    [HttpGet("Proposals/{proposalId:guid}/Complete")]
    public IActionResult Complete(Guid proposalId) => Page("Edit", null, proposalId, "complete");

    /// <summary>R5 — "Var olan kayıtla birleştir": the 3 steps; the last one saves the decision and sends it.</summary>
    [HttpGet("Proposals/{proposalId:guid}/Merge")]
    public IActionResult ProposalMerge(Guid proposalId) => Page("Merge", null, proposalId, "proposal-merge");

    private ViewResult Page(string view, Guid? id, Guid? proposalId, string mode)
        => View($"{ViewRoot}/{view}.cshtml", new CompetitorProductsPageViewModel
        {
            CanRead = HasAll(Read),
            CanManage = HasAll(Manage),
            CanReadProducts = HasAll(ProductRead),
            CanReadForms = HasAll(BrandRead),
            CanReadIndications = HasAny(InsightsRead, InsightsReadAll, ThresholdsManage),
            CanReadUsage = HasAll(UsageRead),
            CanReadPeople = HasAll(PeopleLookup),
            Id = id,
            ProposalId = proposalId,
            Mode = mode,
            Actor = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
        });

    // ---------------- MDM: records ----------------

    [HttpGet("api/records")]
    public Task<IActionResult> List(CancellationToken ct) => ProxyAsync(HttpMethod.Get, $"{MdmBase}{Request.QueryString}", null, ct, Read);

    [HttpGet("api/records/{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => ProxyAsync(HttpMethod.Get, $"{MdmBase}/{id}", null, ct, Read);

    [HttpGet("api/merge-candidates")]
    public Task<IActionResult> MergeCandidates(CancellationToken ct) => ProxyAsync(HttpMethod.Get, $"{MdmBase}/merge-candidates{Request.QueryString}", null, ct, Manage);

    [HttpPost("api/records")]
    public async Task<IActionResult> Create(CancellationToken ct) => await ProxyAsync(HttpMethod.Post, MdmBase, await ReadBodyAsync(ct), ct, Manage);

    [HttpPut("api/records/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CancellationToken ct) => await ProxyAsync(HttpMethod.Put, $"{MdmBase}/{id}", await ReadBodyAsync(ct), ct, Manage);

    [HttpPost("api/records/{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct) => await ProxyAsync(HttpMethod.Post, $"{MdmBase}/{id}/deactivate", await ReadBodyAsync(ct), ct, Manage);

    [HttpPost("api/records/{id:guid}/merge")]
    public async Task<IActionResult> MergeRecord(Guid id, CancellationToken ct) => await ProxyAsync(HttpMethod.Post, $"{MdmBase}/{id}/merge", await ReadBodyAsync(ct), ct, Manage);

    // ---------------- MDM: proposals + decisions ----------------

    [HttpGet("api/proposals")]
    public Task<IActionResult> Proposals(CancellationToken ct) => ProxyAsync(HttpMethod.Get, $"{MdmBase}/proposals", null, ct, Manage);

    [HttpGet("api/proposals/{proposalId:guid}")]
    public Task<IActionResult> GetProposal(Guid proposalId, CancellationToken ct) => ProxyAsync(HttpMethod.Get, $"{MdmBase}/proposals/{proposalId}", null, ct, Manage);

    [HttpPut("api/proposals/{proposalId:guid}/decision")]
    public async Task<IActionResult> SaveDecision(Guid proposalId, CancellationToken ct)
        => await ProxyAsync(HttpMethod.Put, $"{MdmBase}/proposals/{proposalId}/decision", await ReadBodyAsync(ct), ct, Manage);

    [HttpPost("api/proposals/{proposalId:guid}/decision/submit")]
    public Task<IActionResult> SubmitDecision(Guid proposalId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Post, $"{MdmBase}/proposals/{proposalId}/decision/submit", null, ct, Manage);

    [HttpPost("api/proposals/{proposalId:guid}/decision/withdraw")]
    public Task<IActionResult> WithdrawDecision(Guid proposalId, CancellationToken ct)
        => ProxyAsync(HttpMethod.Post, $"{MdmBase}/proposals/{proposalId}/decision/withdraw", null, ct, Manage);

    /// <summary>The approver's decision (approve | return; a return needs a note — the MDM checks it too).</summary>
    [HttpPost("api/proposals/{proposalId:guid}/decision/approve-reject")]
    public async Task<IActionResult> ReviewDecision(Guid proposalId, CancellationToken ct)
        => await ProxyAsync(HttpMethod.Post, $"{MdmBase}/proposals/{proposalId}/decision/approve-reject", await ReadBodyAsync(ct), ct, Manage);

    // ---------------- lookups ----------------

    [HttpGet("api/products")]
    public Task<IActionResult> Products(CancellationToken ct) => ProxyAsync(HttpMethod.Get, $"/api/global-products/selector{Request.QueryString}", null, ct, ProductRead);

    /// <summary>The MDM dosage form vocabulary (G4) through the brand/product contract.</summary>
    [HttpGet("api/forms")]
    public Task<IActionResult> Forms(CancellationToken ct) => ProxyAsync(HttpMethod.Get, "/api/mdm/brand-products/contract", null, ct, BrandRead);

    /// <summary>G3 — a product's indications from the CRM Concept Graph.</summary>
    [HttpGet("api/indications/{productId:guid}")]
    public Task<IActionResult> Indications(Guid productId, CancellationToken ct)
        => HasAny(InsightsRead, InsightsReadAll, ThresholdsManage)
            ? SendAndMapAsync(HttpMethod.Get, $"/api/crm/doctor-insights/products/{productId}/indication-profiles", null, ct)
            : Task.FromResult<IActionResult>(Forbidden());

    /// <summary>G2 — the report usage counts (no doctor identity; a cell under 5 is "&lt;5").</summary>
    [HttpGet("api/usage")]
    public Task<IActionResult> Usage(CancellationToken ct) => ProxyAsync(HttpMethod.Get, $"/api/crm/visit-report/v2/competitor-usage{Request.QueryString}", null, ct, UsageRead);

    [HttpGet("api/people")]
    public Task<IActionResult> People(CancellationToken ct) => ProxyAsync(HttpMethod.Get, "/api/v1/meetings/lookups/attendees", null, ct, PeopleLookup);

    [HttpGet("api/reference-data/{setCode}/values")]
    public async Task<IActionResult> ReferenceValues(string setCode, CancellationToken ct)
    {
        if (!HasAll(Read)) return Forbidden();
        if (!ReadableSets.Contains(setCode, StringComparer.Ordinal)) return NotFound(new { errors = new[] { "unknown_set", "Unknown set." } });
        var response = await _referenceSets.ReadAsync(setCode, Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request), GetTenantId(), ct);
        return await ToProxyResultAsync(response, ct);
    }

    // ---------------- proxy helpers ----------------

    private ObjectResult Forbidden() => StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });

    private async Task<IActionResult> ProxyAsync(HttpMethod method, string path, string? rawBody, CancellationToken ct, params string[] allPermissions)
    {
        if (!HasAll(allPermissions)) return Forbidden();
        if (rawBody is not null && ContainsTenantId(rawBody))
        {
            return BadRequest(new { errors = new[] { "tenant_supplied", "TenantId is server-resolved and must not be supplied." } });
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
            if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var tenantId = GetTenantId();
            if (string.IsNullOrWhiteSpace(tenantId)) return null;
            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);
            var language = Request.Headers.AcceptLanguage.ToString();
            if (!string.IsNullOrWhiteSpace(language)) request.Headers.TryAddWithoutValidation("Accept-Language", language);
            if (rawBody is not null) request.Content = new StringContent(rawBody, Encoding.UTF8, "application/json");
            else if (method == HttpMethod.Post || method == HttpMethod.Put) request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
            return await _httpClient.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Competitor products Gateway request failed: {Method} {Path}", method, path);
            return null;
        }
    }

    private static async Task<IActionResult> ToProxyResultAsync(HttpResponseMessage? response, CancellationToken ct)
    {
        if (response is null) return new ObjectResult(new { errors = new[] { "gateway_unavailable", "Gateway unavailable." } }) { StatusCode = 502 };
        if (IsBodilessStatus(response.StatusCode)) return new StatusCodeResult((int)response.StatusCode);
        var content = await response.Content.ReadAsStringAsync(ct);
        if (Diten.Web.Controllers.CRM.CrmVisitExecutionController.ProblemDetailsToEnvelope(
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
        => (int)status is >= 100 and < 200 || status is HttpStatusCode.NoContent or HttpStatusCode.ResetContent or HttpStatusCode.NotModified;

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
        x.Type == "tenantId" || x.Type == "tenant_id" || x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    private bool HasAll(params string[] permissions) => permissions.All(x => PermissionClaims.HasPermission(User, x));

    private bool HasAny(params string[] permissions) => permissions.Any(x => PermissionClaims.HasPermission(User, x));
}
