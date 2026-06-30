using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0029-FU03 — TenantShell same-origin MVC proxy for template variant governance + drift. The browser never
/// sees the bearer token or constructs the X-Tenant-Id header; both are added server-side (proxy profile).
/// </summary>
[Authorize]
[Route("DocumentManagementTemplateVariants")]
public sealed class DocumentManagementTemplateVariantsController : Controller
{
    private const string ApiBase = "/api/v1/document-management";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<DocumentManagementTemplateVariantsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public DocumentManagementTemplateVariantsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<DocumentManagementTemplateVariantsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/DocumentManagement/TemplateVariants/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create() => View("~/Views/DocumentManagement/TemplateVariants/Create.cshtml");

    [HttpGet("Edit/{id:guid}")]
    public IActionResult Edit(Guid id)
    {
        ViewData["TemplateVariantId"] = id;
        return View("~/Views/DocumentManagement/TemplateVariants/Edit.cshtml");
    }

    [HttpGet("Details/{id:guid}")]
    public IActionResult Details(Guid id)
    {
        ViewData["TemplateVariantId"] = id;
        return View("~/Views/DocumentManagement/TemplateVariants/Details.cshtml");
    }

    [HttpGet("/DocumentManagement/TemplateVariants/api/list")]
    public Task<IActionResult> List(
        [FromQuery] Guid? templateMasterId,
        [FromQuery] string? scopeType,
        [FromQuery] Guid? scopeId,
        [FromQuery] string? status,
        [FromQuery] string? approvalStatus,
        CancellationToken ct)
    {
        var qs = new List<string>();
        if (templateMasterId is { } m && m != Guid.Empty) qs.Add($"templateMasterId={m:D}");
        if (!string.IsNullOrWhiteSpace(scopeType)) qs.Add($"scopeType={Uri.EscapeDataString(scopeType)}");
        if (scopeId is { } s && s != Guid.Empty) qs.Add($"scopeId={s:D}");
        if (!string.IsNullOrWhiteSpace(status)) qs.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrWhiteSpace(approvalStatus)) qs.Add($"approvalStatus={Uri.EscapeDataString(approvalStatus)}");
        var suffix = qs.Count == 0 ? string.Empty : $"?{string.Join('&', qs)}";
        return ProxyGetAsync($"{ApiBase}/template-variants{suffix}", ct);
    }

    [HttpGet("/DocumentManagement/TemplateVariants/api/detail/{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/template-variants/{id}", ct);

    [HttpGet("/DocumentManagement/TemplateVariants/api/compare/{id:guid}")]
    public Task<IActionResult> Compare(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/template-variants/{id}/compare", ct);

    [HttpGet("/DocumentManagement/TemplateVariants/api/options")]
    public Task<IActionResult> Options(CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/template-variant-options", ct);

    [HttpGet("/DocumentManagement/TemplateVariants/api/by-master/{id:guid}")]
    public Task<IActionResult> ByMaster(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/template-masters/{id}/variants", ct);

    [HttpGet("/DocumentManagement/TemplateVariants/api/legal-entities")]
    public Task<IActionResult> LegalEntities(CancellationToken ct) =>
        ProxyGetAsync("/api/legal-entities", ct);

    [HttpGet("/DocumentManagement/TemplateVariants/api/users")]
    public Task<IActionResult> Users(CancellationToken ct) =>
        ProxyGetAsync("/api/users?page=1&pageSize=1000", ct);

    [HttpGet("/DocumentManagement/TemplateVariants/api/documentation-structures")]
    public Task<IActionResult> DocumentationStructures([FromQuery] Guid companyId, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/documentation-structures{Query("companyId", companyId)}", ct);

    [HttpGet("/DocumentManagement/TemplateVariants/api/collection-instances")]
    public Task<IActionResult> CollectionInstances([FromQuery] Guid companyId, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/collection-instances{Query("companyId", companyId, ("requiredAction", "CreateTemplate"))}", ct);

    [HttpPost("/DocumentManagement/TemplateVariants/api/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateVariant([FromForm] string payloadJson, [FromForm] IFormFile? localContentFile, CancellationToken ct)
    {
        var payload = ParsePayload(payloadJson);
        if (localContentFile is not null && localContentFile.Length > 0)
        {
            payload["localFile"] = await ToFileUploadAsync(localContentFile, ct);
        }

        return await ProxyPostAsync($"{ApiBase}/template-variants", payload, ct);
    }

    [HttpPost("/DocumentManagement/TemplateVariants/api/rebase/{id:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Rebase(Guid id, [FromForm] string? targetMasterVersionId, CancellationToken ct)
    {
        object? targetId = Guid.TryParse(targetMasterVersionId, out var parsed) && parsed != Guid.Empty ? parsed : null;
        return ProxyPostAsync($"{ApiBase}/template-variants/{id}/rebase", new { targetMasterVersionId = targetId }, ct);
    }

    // Bulk delete is intentionally NOT supported in MOD-0029-FU03 (no delete/archive command in scope). The bulk
    // surface scaffolding exists in the DataTable for the Golden v2 contract, but no destructive action is exposed.
    [HttpPost("/DocumentManagement/TemplateVariants/api/bulk")]
    [ValidateAntiForgeryToken]
    public IActionResult BulkDelete() =>
        JsonFailure(501, "NOT_SUPPORTED", _sharedLocalizer["ErrorOccurred"].Value);

    private Dictionary<string, object?> ParsePayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(payloadJson);
        return JsonElementToObject(doc.RootElement) as Dictionary<string, object?> ?? [];
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

    private static async Task<object> ToFileUploadAsync(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ct);
        return new
        {
            fileName = file.FileName,
            mediaType = string.IsNullOrWhiteSpace(file.ContentType) ? null : file.ContentType,
            contentBase64 = Convert.ToBase64String(memory.ToArray())
        };
    }

    private Task<IActionResult> ProxyPostAsync(string path, object payload, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, path, payload, ct);

    private static string Query(string name, Guid? value, params (string Name, string Value)[] extra)
    {
        var parts = new List<string>();
        if (value is { } id && id != Guid.Empty)
        {
            parts.Add($"{name}={id:D}");
        }

        foreach (var (extraName, extraValue) in extra)
        {
            if (!string.IsNullOrWhiteSpace(extraName) && !string.IsNullOrWhiteSpace(extraValue))
            {
                parts.Add($"{Uri.EscapeDataString(extraName)}={Uri.EscapeDataString(extraValue)}");
            }
        }

        return parts.Count == 0 ? string.Empty : $"?{string.Join('&', parts)}";
    }

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
            _logger.LogError(ex, "Template variant proxy GET {Path} failed.", path);
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
            _logger.LogError(ex, "Template variant proxy {Method} {Path} failed.", method, path);
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
