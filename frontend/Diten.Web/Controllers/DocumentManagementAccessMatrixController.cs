using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0029-FU04 — TenantShell same-origin MVC proxy for the document access matrix. The browser never sees the
/// bearer token or builds the X-Tenant-Id header; both are added server-side (proxy profile).
/// </summary>
[Authorize]
[Route("DocumentManagementAccessMatrix")]
public sealed class DocumentManagementAccessMatrixController : Controller
{
    private const string ApiBase = "/api/v1/document-management";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<DocumentManagementAccessMatrixController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public DocumentManagementAccessMatrixController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<DocumentManagementAccessMatrixController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/DocumentManagement/AccessMatrix/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create() => View("~/Views/DocumentManagement/AccessMatrix/Create.cshtml");

    [HttpGet("Edit/{id:guid}")]
    public IActionResult Edit(Guid id)
    {
        ViewData["AccessPolicyId"] = id;
        return View("~/Views/DocumentManagement/AccessMatrix/Edit.cshtml");
    }

    [HttpGet("Details/{id:guid}")]
    public IActionResult Details(Guid id)
    {
        ViewData["AccessPolicyId"] = id;
        return View("~/Views/DocumentManagement/AccessMatrix/Details.cshtml");
    }

    [HttpGet("/DocumentManagement/AccessMatrix/api/list")]
    public Task<IActionResult> List(
        [FromQuery] string? targetType,
        [FromQuery] string? principalType,
        [FromQuery] string? effect,
        [FromQuery] string? action,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var qs = new List<string>();
        if (!string.IsNullOrWhiteSpace(targetType)) qs.Add($"targetType={Uri.EscapeDataString(targetType)}");
        if (!string.IsNullOrWhiteSpace(principalType)) qs.Add($"principalType={Uri.EscapeDataString(principalType)}");
        if (!string.IsNullOrWhiteSpace(effect)) qs.Add($"effect={Uri.EscapeDataString(effect)}");
        if (!string.IsNullOrWhiteSpace(action)) qs.Add($"action={Uri.EscapeDataString(action)}");
        if (!string.IsNullOrWhiteSpace(status)) qs.Add($"status={Uri.EscapeDataString(status)}");
        var suffix = qs.Count == 0 ? string.Empty : $"?{string.Join('&', qs)}";
        return ProxyGetAsync($"{ApiBase}/access-policies{suffix}", ct);
    }

    [HttpGet("/DocumentManagement/AccessMatrix/api/detail/{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/access-policies/{id}", ct);

    [HttpGet("/DocumentManagement/AccessMatrix/api/target-options")]
    public Task<IActionResult> TargetOptions(CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/access-target-options", ct);

    [HttpGet("/DocumentManagement/AccessMatrix/api/principal-options")]
    public Task<IActionResult> PrincipalOptions(CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/access-principal-options", ct);

    [HttpGet("/DocumentManagement/AccessMatrix/api/legal-entities")]
    public Task<IActionResult> LegalEntities(CancellationToken ct) =>
        ProxyGetAsync("/api/legal-entities", ct);

    [HttpGet("/DocumentManagement/AccessMatrix/api/users")]
    public Task<IActionResult> Users(CancellationToken ct) =>
        ProxyGetAsync("/api/users?page=1&pageSize=1000", ct);

    [HttpGet("/DocumentManagement/AccessMatrix/api/roles")]
    public Task<IActionResult> Roles(CancellationToken ct) =>
        ProxyGetAsync("/api/roles", ct);

    [HttpGet("/DocumentManagement/AccessMatrix/api/documentation-structures")]
    public Task<IActionResult> DocumentationStructures([FromQuery] Guid companyId, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/documentation-structures{Query("companyId", companyId)}", ct);

    [HttpGet("/DocumentManagement/AccessMatrix/api/collection-instances")]
    public Task<IActionResult> CollectionInstances([FromQuery] Guid companyId, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/collection-instances{Query("companyId", companyId)}", ct);

    [HttpGet("/DocumentManagement/AccessMatrix/api/effective")]
    public Task<IActionResult> Effective(
        [FromQuery] string targetType,
        [FromQuery] string targetId,
        [FromQuery] string principalType,
        [FromQuery] string principalId,
        CancellationToken ct)
    {
        var qs = $"?targetType={Uri.EscapeDataString(targetType ?? string.Empty)}&targetId={Uri.EscapeDataString(targetId ?? string.Empty)}" +
                 $"&principalType={Uri.EscapeDataString(principalType ?? string.Empty)}&principalId={Uri.EscapeDataString(principalId ?? string.Empty)}";
        return ProxyGetAsync($"{ApiBase}/access-policies/effective{qs}", ct);
    }

    [HttpPost("/DocumentManagement/AccessMatrix/api/create")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CreatePolicy([FromForm] string payloadJson, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/access-policies", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/AccessMatrix/api/update/{id:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> UpdatePolicy(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Put, $"{ApiBase}/access-policies/{id}", ParsePayload(payloadJson), ct);

    [HttpPost("/DocumentManagement/AccessMatrix/api/delete/{id:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Delete, $"{ApiBase}/access-policies/{id}", new { }, ct);

    [HttpPost("/DocumentManagement/AccessMatrix/api/bulk")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> BulkDelete([FromForm] string idsJson, CancellationToken ct)
    {
        var ids = ParseIds(idsJson);
        if (ids.Count == 0)
        {
            return Task.FromResult<IActionResult>(JsonFailure(400, "VALIDATION_FAILED", _sharedLocalizer["ValidationFailed"].Value));
        }

        return ProxyJsonAsync(HttpMethod.Delete, $"{ApiBase}/access-policies/bulk", ids, ct);
    }

    private static List<Guid> ParseIds(string? idsJson)
    {
        if (string.IsNullOrWhiteSpace(idsJson)) return [];
        try
        {
            var raw = JsonSerializer.Deserialize<List<string>>(idsJson) ?? [];
            return raw.Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct().ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

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

    private static string Query(string name, Guid? value) =>
        value is { } id && id != Guid.Empty ? $"?{name}={id:D}" : string.Empty;

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
            _logger.LogError(ex, "Access matrix proxy GET {Path} failed.", path);
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
            _logger.LogError(ex, "Access matrix proxy {Method} {Path} failed.", method, path);
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
