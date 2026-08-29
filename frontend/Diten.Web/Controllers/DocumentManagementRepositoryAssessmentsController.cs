using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0029-FU28A — Repository Assessment master data (GMG-QMS-SOP-0001 §11, §11.1, §11.2). Repository assessments
/// are TENANT-GLOBAL: a document's Repository tab (FU28) links to one of these rows, it does not own them. This
/// screen classifies a repository against the SOP boundary and records a governance decision; it asserts NO
/// computer-system validation and NO electronic-signature compliance.
///
/// Same-origin MVC proxy profile: the browser never talks to the Platform API (5057) directly and never sends a
/// tenant id — the bearer token and X-Tenant-Id are attached server-side from the HttpOnly auth cookie.
/// </summary>
[Authorize]
[Route("DocumentManagementRepositoryAssessments")]
public sealed class DocumentManagementRepositoryAssessmentsController : Controller
{
    private const string ApiBase = "/api/v1/document-management/repository-assessments";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<DocumentManagementRepositoryAssessmentsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public DocumentManagementRepositoryAssessmentsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<DocumentManagementRepositoryAssessmentsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    // ── Pages ────────────────────────────────────────────────────────────────

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/DocumentManagement/RepositoryAssessments/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create() => View("~/Views/DocumentManagement/RepositoryAssessments/Create.cshtml");

    [HttpGet("Edit/{id:guid}")]
    public IActionResult Edit(Guid id)
    {
        ViewData["RepositoryAssessmentId"] = id;
        return View("~/Views/DocumentManagement/RepositoryAssessments/Edit.cshtml");
    }

    [HttpGet("Details/{id:guid}")]
    public IActionResult Details(Guid id)
    {
        ViewData["RepositoryAssessmentId"] = id;
        return View("~/Views/DocumentManagement/RepositoryAssessments/Details.cshtml");
    }

    // ── Same-origin proxy API ────────────────────────────────────────────────
    //
    // The FU16 list endpoint takes no query parameters, so filtering is client-side over the returned set (the same
    // profile the Template Masters list uses). No query parameter is invented here.

    [HttpGet("/DocumentManagement/RepositoryAssessments/api/list")]
    public Task<IActionResult> List(CancellationToken ct) => ProxyGetAsync(ApiBase, ct);

    [HttpGet("/DocumentManagement/RepositoryAssessments/api/{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken ct) => ProxyGetAsync($"{ApiBase}/{id}", ct);

    [HttpGet("/DocumentManagement/RepositoryAssessments/api/{id:guid}/findings")]
    public Task<IActionResult> Findings(Guid id, CancellationToken ct) => ProxyGetAsync($"{ApiBase}/{id}/findings", ct);

    [HttpPost("/DocumentManagement/RepositoryAssessments/api/create")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CreateAssessment([FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, ApiBase, ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/RepositoryAssessments/api/{id:guid}/update")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateAssessment(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Put, $"{ApiBase}/{id}", ParsePayload(payloadJson), ct);

    /// <summary>
    /// Classifies the repository and returns the boundary readiness. It does NOT approve the assessment and does not
    /// change its status.
    /// </summary>
    [HttpPost("/DocumentManagement/RepositoryAssessments/api/{id:guid}/evaluate")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Evaluate(Guid id, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/evaluate", new { }, ct);

    /// <summary>
    /// Records a governance decision. The backend enforces SOP §11.2 (only GQD / Deputy GQD / IT-CSV owner) and
    /// refuses approval while any Critical finding is open. Nothing here asserts system validation.
    /// </summary>
    [HttpPost("/DocumentManagement/RepositoryAssessments/api/{id:guid}/approve")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/approve", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/RepositoryAssessments/api/{id:guid}/reject")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Reject(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, $"{ApiBase}/{id}/reject", ParsePayload(payloadJson), ct);

    // ── Proxy plumbing (mirrors the MOD-0029 TenantShell proxy profile) ──────

    private object ParsePayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new { };
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            return JsonElementToObject(doc.RootElement) ?? new { };
        }
        catch (JsonException)
        {
            return new { };
        }
    }

    private static object? JsonElementToObject(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };

    private async Task<IActionResult> ProxyGetAsync(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_gatewayUrl}{path}");
        if (!AddAuthHeaders(request))
        {
            return UnauthorizedJson();
        }

        try
        {
            return await PassthroughAsync(await _httpClient.SendAsync(request, ct), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Repository assessment proxy GET {Path} failed.", path);
            return GatewayErrorJson();
        }
    }

    private async Task<IActionResult> ProxyJsonAsync(HttpMethod method, string path, object payload, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, $"{_gatewayUrl}{path}")
        {
            Content = System.Net.Http.Json.JsonContent.Create(payload, options: _jsonOptions)
        };
        if (!AddAuthHeaders(request))
        {
            return UnauthorizedJson();
        }

        try
        {
            return await PassthroughAsync(await _httpClient.SendAsync(request, ct), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Repository assessment proxy {Method} {Path} failed.", method, path);
            return GatewayErrorJson();
        }
    }

    private Task<IActionResult> PassthroughAsync(HttpResponseMessage response, CancellationToken ct) =>
        Diten.Web.Infrastructure.TenantShellProxyResponse.PassthroughAsync(response, Request, ct);

    private IActionResult UnauthorizedJson() => JsonFailure(401, "UNAUTHORIZED", _sharedLocalizer["Unauthorized"].Value);
    private IActionResult GatewayErrorJson() => JsonFailure(502, "GATEWAY_ERROR", _sharedLocalizer["GatewayError"].Value);

    private ContentResult JsonFailure(int status, string reasonCode, string message)
    {
        var json = JsonSerializer.Serialize(new
        {
            data = (object?)null,
            isSuccessful = false,
            statusCode = status,
            errors = new[] { message },
            reason_code = reasonCode,
            correlation_id = HttpContext.TraceIdentifier
        });
        return new ContentResult { Content = json, ContentType = "application/json", StatusCode = status };
    }

    private bool AddAuthHeaders(HttpRequestMessage request)
    {
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Tenant id is resolved SERVER-SIDE from the signed-in principal / access token — never from the client.
        var tenantId = GetTenantId(token);
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return false;
        }

        request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);
        return true;
    }

    private string? GetTenantId(string? accessToken)
    {
        var claimValue = User.Claims.FirstOrDefault(x =>
            x.Type == "tenantId" ||
            x.Type == "tenant_id" ||
            x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase) ||
            x.Type.EndsWith("/tenant_id", StringComparison.OrdinalIgnoreCase))?.Value;
        if (!string.IsNullOrWhiteSpace(claimValue))
        {
            return claimValue;
        }

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(accessToken))
            {
                return null;
            }

            return handler.ReadJwtToken(accessToken).Claims.FirstOrDefault(x =>
                string.Equals(x.Type, "tenantId", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Type, "tenant_id", StringComparison.OrdinalIgnoreCase) ||
                x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase) ||
                x.Type.EndsWith("/tenant_id", StringComparison.OrdinalIgnoreCase))?.Value;
        }
        catch
        {
            return null;
        }
    }
}
