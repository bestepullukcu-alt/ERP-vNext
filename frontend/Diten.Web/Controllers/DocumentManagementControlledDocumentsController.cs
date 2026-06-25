using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0029-FU01 — TenantShell same-origin proxy for the controlled-document / template / folder-share API.
/// Every call is forwarded to the Gateway (<c>GatewayUrl</c>, port 5000) server-side with the HttpOnly bearer
/// token and the resolved <c>X-Tenant-Id</c>; the browser never talks to the Platform API service directly and
/// never supplies a TenantId. Gateway JSON (incl. <c>reason_code</c>/<c>correlation_id</c>) is passed through
/// verbatim. This controller adds no business logic and persists nothing. Uploads are read from IFormFile and
/// forwarded as base64 JSON (no raw bytes on disk).
/// </summary>
[Authorize]
[Route("DocumentManagementControlledDocuments")]
public sealed class DocumentManagementControlledDocumentsController : Controller
{
    private const string ApiBase = "/api/v1/document-management";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<DocumentManagementControlledDocumentsController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public DocumentManagementControlledDocumentsController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<DocumentManagementControlledDocumentsController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"] ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    // ----- Views -----

    [HttpGet("")]
    public IActionResult Index() => View("~/Views/DocumentManagement/ControlledDocuments/Index.cshtml");

    [HttpGet("Create")]
    public IActionResult Create() => View("~/Views/DocumentManagement/ControlledDocuments/Create.cshtml");

    [HttpGet("Edit/{id:guid}")]
    public IActionResult Edit(Guid id)
    {
        ViewData["DocumentId"] = id;
        return View("~/Views/DocumentManagement/ControlledDocuments/Edit.cshtml");
    }

    [HttpGet("Details/{id:guid}")]
    public IActionResult Details(Guid id)
    {
        ViewData["DocumentId"] = id;
        return View("~/Views/DocumentManagement/ControlledDocuments/Details.cshtml");
    }

    [HttpGet("FolderShare")]
    public IActionResult FolderShare() => View("~/Views/DocumentManagement/ControlledDocuments/FolderShare.cshtml");

    // ----- Controlled-document JSON proxy -----

    [HttpGet("list")]
    public Task<IActionResult> List([FromQuery] Guid? collectionInstanceId, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/controlled-documents{Query("collectionInstanceId", collectionInstanceId)}", ct);

    [HttpGet("detail/{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken ct) => ProxyGetAsync($"{ApiBase}/controlled-documents/{id}", ct);

    [HttpGet("versions/{id:guid}")]
    public Task<IActionResult> Versions(Guid id, CancellationToken ct) => ProxyGetAsync($"{ApiBase}/controlled-documents/{id}/versions", ct);

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateDocument(IFormFile? file, [FromForm] string payloadJson, CancellationToken ct)
    {
        var payload = await BuildUploadPayloadAsync(file, payloadJson, ct);
        if (payload is null)
        {
            return UnprocessableJson("invalid_upload");
        }

        return await ProxyPostAsync($"{ApiBase}/controlled-documents", payload, ct);
    }

    [HttpPost("edit/{id:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> EditDocument(Guid id, [FromForm] string payloadJson, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Put, $"{ApiBase}/controlled-documents/{id}", ParsePayload(payloadJson), ct);

    [HttpPost("upload-version/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadVersion(Guid id, IFormFile? file, [FromForm] string? changeSummary, CancellationToken ct)
    {
        var fileInput = await BuildFileInputAsync(file, ct);
        if (fileInput is null)
        {
            return UnprocessableJson("invalid_upload");
        }

        return await ProxyPostAsync($"{ApiBase}/controlled-documents/{id}/versions", new { file = fileInput, changeSummary }, ct);
    }

    [HttpGet("download/{id:guid}/{versionId:guid}")]
    public async Task<IActionResult> Download(Guid id, Guid versionId, CancellationToken ct)
    {
        if (!AddAuthHeaders())
        {
            return UnauthorizedJson();
        }

        try
        {
            var response = await _httpClient.GetAsync($"{_gatewayUrl}{ApiBase}/controlled-documents/{id}/versions/{versionId}/download", ct);
            if (!response.IsSuccessStatusCode)
            {
                return await PassthroughAsync(response, ct);
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? "document";
            return File(bytes, contentType, fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Controlled-document download proxy failed.");
            return GatewayErrorJson();
        }
    }

    [HttpPost("share/{id:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Share(Guid id, [FromForm] Guid targetCompanyId, [FromForm] string? shareMode, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/controlled-documents/{id}/share", new { targetCompanyId, shareMode }, ct);

    // ----- Template JSON proxy -----

    [HttpGet("templates")]
    public Task<IActionResult> Templates([FromQuery] Guid? collectionInstanceId, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/templates{Query("collectionInstanceId", collectionInstanceId)}", ct);

    // Create form lookup sources. Browser calls stay same-origin; the MVC proxy forwards through Gateway.
    [HttpGet("legal-entities")]
    public Task<IActionResult> LegalEntities(CancellationToken ct) =>
        ProxyGetAsync("/api/legal-entities", ct);

    [HttpGet("collection-instances")]
    public Task<IActionResult> CollectionInstances([FromQuery] Guid companyId, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/collection-instances{Query("companyId", companyId)}", ct);

    [HttpGet("templates/detail/{id:guid}")]
    public Task<IActionResult> TemplateDetail(Guid id, CancellationToken ct) => ProxyGetAsync($"{ApiBase}/templates/{id}", ct);

    [HttpPost("templates/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTemplate(IFormFile? file, [FromForm] string payloadJson, CancellationToken ct)
    {
        var payload = await BuildUploadPayloadAsync(file, payloadJson, ct);
        if (payload is null)
        {
            return UnprocessableJson("invalid_upload");
        }

        return await ProxyPostAsync($"{ApiBase}/templates", payload, ct);
    }

    [HttpPost("templates/share/{id:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ShareTemplate(Guid id, [FromForm] Guid targetCompanyId, [FromForm] string? shareMode, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/templates/{id}/share", new { targetCompanyId, shareMode }, ct);

    // ----- Folder documents / access / shares -----

    [HttpGet("folder-documents")]
    public Task<IActionResult> FolderDocuments([FromQuery] Guid collectionInstanceId, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/folder-documents{Query("collectionInstanceId", collectionInstanceId)}", ct);

    [HttpGet("folder-access")]
    public Task<IActionResult> FolderAccess([FromQuery] Guid collectionInstanceId, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/folder-documents/access{Query("collectionInstanceId", collectionInstanceId)}", ct);

    [HttpPost("folder-access")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> UpsertFolderAccess([FromForm] string payloadJson, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/folder-documents/access", ParsePayload(payloadJson), ct);

    [HttpPost("folder-shares/dry-run")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> FolderShareDryRun([FromForm] string payloadJson, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/folder-shares/dry-run", ParsePayload(payloadJson), ct);

    [HttpPost("folder-shares/execute")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> FolderShareExecute([FromForm] string payloadJson, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/folder-shares/execute", ParsePayload(payloadJson), ct);

    [HttpGet("folder-shares/{operationId:guid}")]
    public Task<IActionResult> FolderShareOperation(Guid operationId, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/folder-shares/{operationId}", ct);

    // ----- helpers (HTTP/transport only) -----

    private async Task<object?> BuildUploadPayloadAsync(IFormFile? file, string payloadJson, CancellationToken ct)
    {
        var fileInput = await BuildFileInputAsync(file, ct);
        if (fileInput is null || string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(payloadJson);
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            dict[prop.Name] = JsonElementToObject(prop.Value);
        }

        dict["file"] = fileInput;
        return dict;
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

    private static string Query(string key, Guid? value) => value is null || value == Guid.Empty ? string.Empty : $"?{key}={value:D}";

    private async Task<IActionResult> ProxyGetAsync(string path, CancellationToken ct)
    {
        if (!AddAuthHeaders())
        {
            return UnauthorizedJson();
        }

        try
        {
            return await PassthroughAsync(await _httpClient.GetAsync($"{_gatewayUrl}{path}", ct), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Controlled-document proxy GET {Path} failed.", path);
            return GatewayErrorJson();
        }
    }

    private Task<IActionResult> ProxyPostAsync(string path, object payload, CancellationToken ct) =>
        ProxyJsonAsync(HttpMethod.Post, path, payload, ct);

    private async Task<IActionResult> ProxyJsonAsync(HttpMethod method, string path, object payload, CancellationToken ct)
    {
        if (!AddAuthHeaders())
        {
            return UnauthorizedJson();
        }

        try
        {
            using var request = new HttpRequestMessage(method, $"{_gatewayUrl}{path}")
            {
                Content = System.Net.Http.Json.JsonContent.Create(payload, options: _jsonOptions)
            };
            return await PassthroughAsync(await _httpClient.SendAsync(request, ct), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Controlled-document proxy {Method} {Path} failed.", method, path);
            return GatewayErrorJson();
        }
    }

    private async Task<IActionResult> PassthroughAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if ((int)response.StatusCode is 204 or 205 or 304)
        {
            return new StatusCodeResult((int)response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        return new ContentResult
        {
            Content = string.IsNullOrWhiteSpace(body) ? "{}" : body,
            ContentType = "application/json",
            StatusCode = (int)response.StatusCode
        };
    }

    private IActionResult UnauthorizedJson() => JsonFailure(401, "UNAUTHORIZED", _sharedLocalizer["Unauthorized"].Value);
    private IActionResult GatewayErrorJson() => JsonFailure(502, "GATEWAY_ERROR", _sharedLocalizer["GatewayError"].Value);
    private IActionResult UnprocessableJson(string reasonCode) => JsonFailure(422, reasonCode, _sharedLocalizer["ValidationFailed"].Value);

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
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
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
            x.Type == "tenantId" || x.Type == "tenant_id" ||
            x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
}
