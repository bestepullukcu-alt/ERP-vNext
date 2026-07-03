using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0028-FU05 — TenantShell same-origin proxy for company adoption / CollectionInstance provisioning.
/// Browser calls stay on this MVC surface; the server forwards to the Gateway with the HttpOnly bearer token
/// and resolved tenant context. Gateway JSON, including reason_code and correlation_id, is passed through.
/// </summary>
[Authorize]
[Route("DocumentManagementInstantiations")]
public sealed class DocumentManagementInstantiationsController : Controller
{
    private const string ApiBase = "/api/v1/document-management";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<DocumentManagementInstantiationsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public DocumentManagementInstantiationsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<DocumentManagementInstantiationsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() =>
        View("~/Views/DocumentManagement/Instantiations/Index.cshtml");

    [HttpGet("Details/{id:guid}")]
    public IActionResult Details(Guid id)
    {
        ViewData["InstanceId"] = id;
        return View("~/Views/DocumentManagement/Instantiations/Details.cshtml");
    }

    [HttpGet("prerequisites")]
    public Task<IActionResult> Prerequisites(CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/instantiations/prerequisites", ct);

    // MOD-0028-FU05 — Company/LegalEntity Select2 source: tenant-scoped referenceable legal entities (MDM, MOD-0220).
    // Same-origin proxy → Gateway → MDM; browser never calls MDM (5059) directly.
    [HttpGet("legal-entities")]
    public Task<IActionResult> LegalEntities(CancellationToken ct) =>
        ProxyGetAsync("/api/legal-entities", ct);

    [HttpGet("instances")]
    public Task<IActionResult> Instances(
        [FromQuery] Guid? companyId,
        [FromQuery] Guid? baselineReleaseId,
        [FromQuery] string? instanceToken,
        CancellationToken ct)
    {
        var query = BuildQuery(("companyId", companyId?.ToString()), ("baselineReleaseId", baselineReleaseId?.ToString()), ("instanceToken", instanceToken));
        return ProxyGetAsync($"{ApiBase}/collection-instances{query}", ct);
    }

    [HttpGet("instances/{id:guid}")]
    public Task<IActionResult> InstanceDetail(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/collection-instances/{id}", ct);

    [HttpPost("instances/{id:guid}/archive")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ArchiveInstance(Guid id, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/collection-instances/{id}/archive", new { }, ct);

    [HttpPost("instances/{id:guid}/restore")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> RestoreInstance(Guid id, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/collection-instances/{id}/restore", new { }, ct);

    [HttpGet("operations/{operationId:guid}")]
    public Task<IActionResult> Operation(Guid operationId, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/instantiations/{operationId}", ct);

    [HttpPost("dry-run")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DryRun([FromForm] InstantiationFormInput input, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/instantiations/dry-run", input.ToPayload(), ct);

    [HttpPost("execute")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Execute([FromForm] InstantiationFormInput input, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/instantiations/execute", input.ToPayload(), ct);

    [HttpPost("operations/{operationId:guid}/retry")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Retry(Guid operationId, [FromForm] string[]? nodeKeys, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/instantiations/{operationId}/retry", new { nodeKeys = nodeKeys ?? [] }, ct);

    private static string BuildQuery(params (string Key, string? Value)[] values)
    {
        var parts = values
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value!)}")
            .ToArray();
        return parts.Length == 0 ? string.Empty : "?" + string.Join("&", parts);
    }

    private async Task<IActionResult> ProxyGetAsync(string path, CancellationToken ct)
    {
        if (!AddAuthHeaders())
        {
            return UnauthorizedJson();
        }

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}{path}", ct);
            return await PassthroughAsync(response, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Instantiation proxy GET {Path} failed.", path);
            return GatewayErrorJson();
        }
    }

    private async Task<IActionResult> ProxyPostAsync(string path, object payload, CancellationToken ct)
    {
        if (!AddAuthHeaders())
        {
            return UnauthorizedJson();
        }

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_gatewayUrl}{path}", payload, _jsonOptions, ct);
            return await PassthroughAsync(response, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Instantiation proxy POST {Path} failed.", path);
            return GatewayErrorJson();
        }
    }

    // MOD-0029-FU04C — navigation 401/403 → friendly Not Authorized page; AJAX keeps the JSON envelope for a toast.
    private Task<IActionResult> PassthroughAsync(HttpResponseMessage response, CancellationToken ct) =>
        Diten.Web.Infrastructure.TenantShellProxyResponse.PassthroughAsync(response, Request, ct);

    private IActionResult UnauthorizedJson() =>
        JsonFailure(401, "UNAUTHORIZED", _sharedLocalizer["Unauthorized"].Value);

    private IActionResult GatewayErrorJson() =>
        JsonFailure(502, "GATEWAY_ERROR", _sharedLocalizer["GatewayError"].Value);

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

    private bool AddAuthHeaders()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        var token = Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (_httpClient.DefaultRequestHeaders.Contains("X-Tenant-Id"))
        {
            _httpClient.DefaultRequestHeaders.Remove("X-Tenant-Id");
        }

        var tenantId = GetTenantId();
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return false;
        }

        _httpClient.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
        return true;
    }

    private string? GetTenantId() =>
        User.Claims.FirstOrDefault(x =>
            x.Type == "tenantId" ||
            x.Type == "tenant_id" ||
            x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    public sealed class InstantiationFormInput
    {
        public Guid BaselineReleaseId { get; set; }
        public Guid CompanyId { get; set; }
        public Guid? PlantId { get; set; }
        public Guid? BusinessUnitId { get; set; }
        public string? InstanceToken { get; set; }
        public string? CorrelationId { get; set; }
        public string? SelectionMode { get; set; }
        public string[]? SelectedCanonicalIds { get; set; }
        public bool IncludeDescendants { get; set; } = true;
        public bool IncludeRequiredAncestors { get; set; } = true;

        public object ToPayload() => new
        {
            BaselineReleaseId,
            Scope = new
            {
                CompanyId,
                PlantId,
                BusinessUnitId,
                InstanceToken
            },
            SelectionMode = string.IsNullOrWhiteSpace(SelectionMode) ? "FULL_TREE" : SelectionMode,
            SelectedCanonicalIds = SelectedCanonicalIds ?? [],
            IncludeDescendants,
            IncludeRequiredAncestors,
            CorrelationId
        };
    }
}
