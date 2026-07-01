using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

[Authorize]
[Route("DocumentManagementTemplateMasters")]
public sealed class DocumentManagementTemplateMastersController : Controller
{
    private const string ApiBase = "/api/v1/document-management";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<DocumentManagementTemplateMastersController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public DocumentManagementTemplateMastersController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<DocumentManagementTemplateMastersController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/DocumentManagement/TemplateMasters/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create() => View("~/Views/DocumentManagement/TemplateMasters/Create.cshtml");

    [HttpGet("Details/{id:guid}")]
    public IActionResult Details(Guid id)
    {
        ViewData["TemplateMasterId"] = id;
        return View("~/Views/DocumentManagement/TemplateMasters/Details.cshtml");
    }

    [HttpGet("/DocumentManagement/TemplateMasters/api/list")]
    public Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? classification,
        [FromQuery] Guid? collectionDefinitionId,
        [FromQuery] string? canonicalId,
        [FromQuery] string? variantPolicy,
        CancellationToken ct)
    {
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(status)) qs.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(classification)) qs.Add($"classification={Uri.EscapeDataString(classification)}");
        if (collectionDefinitionId is { } cd && cd != Guid.Empty) qs.Add($"collectionDefinitionId={cd:D}");
        if (!string.IsNullOrWhiteSpace(canonicalId)) qs.Add($"canonicalId={Uri.EscapeDataString(canonicalId)}");
        if (!string.IsNullOrWhiteSpace(variantPolicy)) qs.Add($"variantPolicy={Uri.EscapeDataString(variantPolicy)}");
        var suffix = qs.Count == 0 ? string.Empty : $"?{string.Join('&', qs)}";
        return ProxyGetAsync($"{ApiBase}/template-masters{suffix}", ct);
    }

    [HttpGet("/DocumentManagement/TemplateMasters/api/detail/{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/template-masters/{id}", ct);

    [HttpGet("/DocumentManagement/TemplateMasters/api/versions/{id:guid}")]
    public Task<IActionResult> Versions(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/template-masters/{id}/versions", ct);

    // Streams a version's file (attachment disposition) by proxying the gateway download endpoint with the
    // server-side bearer token; the browser never sees the token (proxy-profile).
    [HttpGet("/DocumentManagement/TemplateMasters/api/download/{id:guid}/{versionId:guid}")]
    public async Task<IActionResult> DownloadVersion(Guid id, Guid versionId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_gatewayUrl}{ApiBase}/template-masters/{id}/versions/{versionId}/download");
        if (!AddAuthHeaders(request))
        {
            return UnauthorizedJson();
        }

        try
        {
            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return await PassthroughAsync(response, ct);
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? "template";
            return File(bytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Template master version download proxy failed.");
            return GatewayErrorJson();
        }
    }

    [HttpGet("/DocumentManagement/TemplateMasters/api/impact/{id:guid}")]
    public Task<IActionResult> Impact(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/template-masters/{id}/adoption-impact", ct);

    [HttpGet("/DocumentManagement/TemplateMasters/api/options")]
    public Task<IActionResult> Options(CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/template-master-options", ct);

    [HttpGet("/DocumentManagement/TemplateMasters/api/legal-entities")]
    public Task<IActionResult> LegalEntities(CancellationToken ct) =>
        ProxyGetAsync("/api/legal-entities", ct);

    [HttpGet("/DocumentManagement/TemplateMasters/api/users")]
    public Task<IActionResult> Users(CancellationToken ct) =>
        ProxyGetAsync("/api/users?page=1&pageSize=1000", ct);

    [HttpGet("/DocumentManagement/TemplateMasters/api/qms-baselines")]
    public Task<IActionResult> QmsBaselines(CancellationToken ct) =>
        ProxyGetAsync("/api/v1/document-management/qms-baselines", ct);

    [HttpGet("/DocumentManagement/TemplateMasters/api/qms-baselines/{id:guid}/definitions")]
    public Task<IActionResult> QmsBaselineDefinitions(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"/api/v1/document-management/qms-baselines/{id}/definitions", ct);

    [HttpPost("/DocumentManagement/TemplateMasters/api/create")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CreateMaster([FromForm] string payloadJson, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/template-masters", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/TemplateMasters/api/publish-version/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PublishVersion(Guid id, IFormFile? file, [FromForm] string? changeSummary, [FromForm] bool allowUnchanged, CancellationToken ct)
    {
        var fileInput = await BuildFileInputAsync(file, ct);
        if (fileInput is null)
        {
            return JsonFailure(422, "INVALID_UPLOAD", _sharedLocalizer["ValidationFailed"].Value);
        }

        return await ProxyPostAsync($"{ApiBase}/template-masters/{id}/versions/publish", new { file = fileInput, changeSummary, allowUnchanged }, ct);
    }

    [HttpPost("/DocumentManagement/TemplateMasters/api/deprecate/{id:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Deprecate(Guid id, [FromForm] string? deprecationReason, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/template-masters/{id}/deprecate", new { deprecationReason }, ct);

    [HttpPost("/DocumentManagement/TemplateMasters/api/delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Delete, $"{ApiBase}/template-masters/{id}", new { }, ct);

    [HttpPost("/DocumentManagement/TemplateMasters/api/bulk")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> BulkDelete([FromForm] string idsJson, CancellationToken ct)
    {
        var ids = ParseIds(idsJson);
        if (ids.Count == 0)
        {
            return Task.FromResult<IActionResult>(JsonFailure(400, "VALIDATION_FAILED", _sharedLocalizer["ValidationFailed"].Value));
        }

        return ProxyJsonAsync(HttpMethod.Delete, $"{ApiBase}/template-masters/bulk", ids, ct);
    }

    private static List<Guid> ParseIds(string? idsJson)
    {
        if (string.IsNullOrWhiteSpace(idsJson))
        {
            return [];
        }

        try
        {
            var raw = JsonSerializer.Deserialize<List<string>>(idsJson) ?? [];
            return raw
                .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .Distinct()
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static async Task<object?> BuildFileInputAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return null;
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return new
        {
            fileName = file.FileName,
            mediaType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            contentBase64 = Convert.ToBase64String(buffer.ToArray())
        };
    }

    private object ParsePayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new { };
        }

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
            _logger.LogError(ex, "Template master proxy GET {Path} failed.", path);
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
            _logger.LogError(ex, "Template master proxy {Method} {Path} failed.", method, path);
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
