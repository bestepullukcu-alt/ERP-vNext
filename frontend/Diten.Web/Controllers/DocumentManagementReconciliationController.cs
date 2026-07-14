using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0028-FU10 — TenantShell same-origin MVC proxy for the FU09 read-back reconciliation, provisioning evidence,
/// IT/QA sign-off, deviation workflow and qualification-readiness API. Every call is forwarded to the Gateway
/// (<c>GatewayUrl</c>, port 5000) server-side with the HttpOnly bearer token and the resolved <c>X-Tenant-Id</c>; the
/// browser never talks to the Platform API service directly and never supplies a TenantId (proxy profile). Gateway JSON
/// (including <c>reason_code</c>/<c>correlation_id</c>) is passed through verbatim so the UI can render controlled
/// errors such as PROVIDER_UNAVAILABLE. This controller adds no business logic and persists nothing.
/// </summary>
[Authorize]
[Route("DocumentManagementReconciliation")]
public sealed class DocumentManagementReconciliationController : Controller
{
    private const string ApiBase = "/api/v1/document-management";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<DocumentManagementReconciliationController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public DocumentManagementReconciliationController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<DocumentManagementReconciliationController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    // ----- Views (TenantShell pages) -----

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/DocumentManagement/Reconciliation/Index.cshtml");

    [HttpGet("Deviations/{baselineReleaseId:guid}")]
    public IActionResult Deviations(Guid baselineReleaseId)
    {
        ViewData["BaselineReleaseId"] = baselineReleaseId;
        return View("~/Views/DocumentManagement/Reconciliation/Deviations.cshtml");
    }

    [HttpGet("Evidence/{baselineReleaseId:guid}")]
    public IActionResult Evidence(Guid baselineReleaseId)
    {
        ViewData["BaselineReleaseId"] = baselineReleaseId;
        return View("~/Views/DocumentManagement/Reconciliation/Evidence.cshtml");
    }

    // ----- Same-origin JSON proxy endpoints (consumed by page JS) -----

    // Baseline selector source (reuses the FU02 baseline list).
    [HttpGet("/DocumentManagement/Reconciliation/api/baselines")]
    public Task<IActionResult> Baselines(CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/qms-baselines", ct);

    [HttpGet("/DocumentManagement/Reconciliation/api/deviations/{baselineReleaseId:guid}")]
    public Task<IActionResult> GetDeviations(Guid baselineReleaseId, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/qms-baselines/{baselineReleaseId}/deviations", ct);

    [HttpGet("/DocumentManagement/Reconciliation/api/evidence/{baselineReleaseId:guid}")]
    public Task<IActionResult> GetEvidence(Guid baselineReleaseId, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/qms-baselines/{baselineReleaseId}/provisioning-evidence", ct);

    [HttpGet("/DocumentManagement/Reconciliation/api/readiness/{baselineReleaseId:guid}")]
    public Task<IActionResult> GetReadiness(Guid baselineReleaseId, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/qms-baselines/{baselineReleaseId}/qualification-readiness", ct);

    [HttpPost("/DocumentManagement/Reconciliation/api/dry-run/{baselineReleaseId:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DryRun(Guid baselineReleaseId, [FromForm] string? scope, [FromForm] string? provider, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/qms-baselines/{baselineReleaseId}/reconciliation/dry-run", BuildRunPayload(scope, provider), ct);

    [HttpPost("/DocumentManagement/Reconciliation/api/apply-findings/{baselineReleaseId:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ApplyFindings(Guid baselineReleaseId, [FromForm] string? scope, [FromForm] string? provider, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/qms-baselines/{baselineReleaseId}/reconciliation/apply-findings", BuildRunPayload(scope, provider), ct);

    [HttpPost("/DocumentManagement/Reconciliation/api/deviations/{id:guid}/resolve")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ResolveDeviation(Guid id, [FromForm] string? comment, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/deviations/{id}/resolve", new { comment }, ct);

    [HttpPost("/DocumentManagement/Reconciliation/api/deviations/{id:guid}/accept")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AcceptDeviation(Guid id, [FromForm] string? comment, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/deviations/{id}/accept", new { comment }, ct);

    [HttpPost("/DocumentManagement/Reconciliation/api/evidence/upsert")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> UpsertEvidence([FromForm] string payloadJson, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/collection-provisioning-evidence/upsert", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/Reconciliation/api/evidence/{id:guid}/permissions-applied")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> PermissionsApplied(Guid id, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/collection-provisioning-evidence/{id}/permissions-applied", new { }, ct);

    [HttpPost("/DocumentManagement/Reconciliation/api/evidence/{id:guid}/qa-verify")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> QaVerify(Guid id, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/collection-provisioning-evidence/{id}/qa-verify", new { }, ct);

    // ----- Helpers (HTTP/transport only; no business logic) -----

    private static object BuildRunPayload(string? scope, string? provider) => new
    {
        scope = string.IsNullOrWhiteSpace(scope) ? "DefinitionToInstance" : scope,
        provider = string.IsNullOrWhiteSpace(provider) ? "InHouse" : provider
    };

    private object ParsePayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return new { };
        using var doc = JsonDocument.Parse(payloadJson);
        return JsonElementToObject(doc.RootElement) ?? new { };
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

    private Task<IActionResult> ProxyPostAsync(string path, object payload, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, path, payload, ct);

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
            _logger.LogError(ex, "Reconciliation proxy GET {Path} failed.", path);
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
            _logger.LogError(ex, "Reconciliation proxy {Method} {Path} failed.", method, path);
            return GatewayErrorJson();
        }
    }

    // MOD-0029-FU04C — navigation 401/403 → friendly Not Authorized page; AJAX keeps the JSON envelope for a toast.
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

            var token = handler.ReadJwtToken(accessToken);
            return token.Claims.FirstOrDefault(x =>
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
