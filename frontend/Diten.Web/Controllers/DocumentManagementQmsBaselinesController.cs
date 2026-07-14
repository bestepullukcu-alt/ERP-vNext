using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Diten.Web.Controllers;

/// <summary>
/// MOD-0028-FU03 — TenantShell same-origin proxy for the FU02 QMS baseline API. Every call is forwarded to the
/// Gateway (<c>GatewayUrl</c>, port 5000) server-side with the HttpOnly bearer token and the resolved
/// <c>X-Tenant-Id</c>; the browser never talks to the Platform API service directly and never supplies a TenantId. Gateway JSON
/// (including <c>reason_code</c>/<c>correlation_id</c>) is passed through verbatim so the UI renders controlled
/// errors. This controller adds no business logic and persists nothing.
/// </summary>
[Authorize]
[Route("DocumentManagementQmsBaselines")]
public sealed class DocumentManagementQmsBaselinesController : Controller
{
    private const string ApiBase = "/api/v1/document-management/qms-baselines";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;
    private readonly ILogger<DocumentManagementQmsBaselinesController> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public DocumentManagementQmsBaselinesController(
        HttpClient httpClient,
        IConfiguration configuration,
        IStringLocalizer<SharedResource> sharedLocalizer,
        ILogger<DocumentManagementQmsBaselinesController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _sharedLocalizer = sharedLocalizer;
        _logger = logger;
    }

    // ----- Views (TenantShell pages) -----

    [HttpGet("")]
    public IActionResult Index() =>
        View("~/Views/DocumentManagement/QmsBaselines/Index.cshtml");

    [HttpGet("Import")]
    public IActionResult Import() =>
        View("~/Views/DocumentManagement/QmsBaselines/Import.cshtml");

    [HttpGet("CreateManual")]
    public IActionResult CreateManual() =>
        View("~/Views/DocumentManagement/QmsBaselines/CreateManual.cshtml");

    [HttpGet("Details/{id:guid}")]
    public IActionResult Details(Guid id)
    {
        ViewData["BaselineId"] = id;
        return View("~/Views/DocumentManagement/QmsBaselines/Details.cshtml");
    }

    [HttpGet("Designer/{id:guid}")]
    public IActionResult Designer(Guid id)
    {
        ViewData["BaselineId"] = id;
        return View("~/Views/DocumentManagement/QmsBaselines/Designer.cshtml");
    }

    // ----- Same-origin JSON proxy endpoints (consumed by page JS) -----

    [HttpGet("list")]
    public Task<IActionResult> List(CancellationToken ct) =>
        ProxyGetAsync(ApiBase, ct);

    [HttpGet("detail/{id:guid}")]
    public Task<IActionResult> Detail(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/{id}", ct);

    [HttpGet("definitions/{id:guid}")]
    public Task<IActionResult> Definitions(Guid id, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/{id}/definitions", ct);

    [HttpGet("definitions/{id:guid}/{canonicalId}")]
    public Task<IActionResult> Definition(Guid id, string canonicalId, CancellationToken ct) =>
        ProxyGetAsync($"{ApiBase}/{id}/definitions/{Uri.EscapeDataString(canonicalId)}", ct);

    // MOD-0029-FU02A — read-only "Related Corporate Templates" for a selected definition node. Reuses the FU02
    // template-masters list filter (collectionDefinitionId / canonicalId); the backend is tenant-scoped, AND-matches
    // the binding fields and is gated by platform.document-management.template-masters.view. No MOD-0028 mutation.
    [HttpGet("related-template-masters")]
    public Task<IActionResult> RelatedTemplateMasters(
        [FromQuery] Guid? collectionDefinitionId,
        [FromQuery] string? canonicalId,
        CancellationToken ct)
    {
        var hasDefinition = collectionDefinitionId is { } cd && cd != Guid.Empty;
        var hasCanonical = !string.IsNullOrWhiteSpace(canonicalId);
        if (!hasDefinition && !hasCanonical)
        {
            // No binding key → never fall through to an unfiltered (all masters) query for a folder.
            return Task.FromResult<IActionResult>(EmptyListJson());
        }

        var qs = new List<string>();
        if (hasDefinition) qs.Add($"collectionDefinitionId={collectionDefinitionId:D}");
        if (hasCanonical) qs.Add($"canonicalId={Uri.EscapeDataString(canonicalId!)}");
        return ProxyGetAsync($"/api/v1/document-management/template-masters?{string.Join('&', qs)}", ct);
    }

    // MOD-0028-FU04 — Business Reference Data (PSS-012) published values feeding the node-editor governance dropdowns
    // (document class / classification / retention). Same-origin proxy → Gateway → Platform.
    [HttpGet("reference-data/{setCode}")]
    public Task<IActionResult> ReferenceData(string setCode, CancellationToken ct) =>
        ProxyGetAsync($"/api/v1/reference-data/sets/{Uri.EscapeDataString(setCode)}/published-values", ct);

    [HttpPost("import/dry-run")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DryRun(IFormFile? file, [FromForm] string? sourceBaselineKey, CancellationToken ct)
    {
        var payload = await BuildImportPayloadAsync(file, sourceBaselineKey, baselineVersion: null, changeSummary: null, ct);
        if (payload is null)
        {
            return UnprocessableJson("invalid_workbook_upload");
        }

        return await ProxyPostAsync($"{ApiBase}/import/dry-run", payload, ct);
    }

    [HttpPost("import/commit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Commit(
        IFormFile? file,
        [FromForm] string? sourceBaselineKey,
        [FromForm] string? baselineVersion,
        [FromForm] string? changeSummary,
        CancellationToken ct)
    {
        var payload = await BuildImportPayloadAsync(file, sourceBaselineKey, baselineVersion, changeSummary, ct);
        if (payload is null)
        {
            return UnprocessableJson("invalid_workbook_upload");
        }

        return await ProxyPostAsync($"{ApiBase}/import/commit", payload, ct);
    }

    [HttpPost("publish/{id:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Publish(Guid id, [FromForm] int expectedVersion, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/{id}/publish", new { expectedVersion }, ct);

    // MOD-0028-FU08 — DRAFT → APPROVED (freezes the immutable snapshot manifest; still non-instantiable).
    [HttpPost("approve/{id:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Approve(
        Guid id,
        [FromForm] int expectedVersion,
        [FromForm] string? approvalReference,
        [FromForm] string? approvalComment,
        CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/{id}/approve", new { expectedVersion, approvalReference, approvalComment }, ct);

    // MOD-0028-FU08 — APPROVED → EFFECTIVE (instantiable; supersedes the prior Effective of the same source key).
    [HttpPost("mark-effective/{id:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> MarkEffective(Guid id, [FromForm] int expectedVersion, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/{id}/mark-effective", new { expectedVersion }, ct);

    [HttpPost("manual")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CreateManualBaseline(
        [FromForm] string? baselineVersion,
        [FromForm] string? name,
        [FromForm] string? changeSummary,
        [FromForm] DateTimeOffset? effectiveDate,
        [FromForm] string? sourceBaselineKey,
        CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/manual", new { baselineVersion, name, changeSummary, effectiveDate, sourceBaselineKey }, ct);

    [HttpPost("definitions/{id:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> CreateDefinition(Guid id, [FromForm] QmsDefinitionFormInput input, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/{id}/definitions", input.ToPayload(), ct);

    [HttpPost("definitions/{id:guid}/{canonicalId}/edit")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateDefinition(Guid id, string canonicalId, [FromForm] QmsDefinitionFormInput input, CancellationToken ct) =>
        ProxyPutAsync($"{ApiBase}/{id}/definitions/{Uri.EscapeDataString(canonicalId)}", input.ToPayload(), ct);

    [HttpPost("definitions/{id:guid}/{canonicalId}/move")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> MoveDefinition(Guid id, string canonicalId, [FromForm] QmsDefinitionMoveFormInput input, CancellationToken ct) =>
        ProxyPatchAsync($"{ApiBase}/{id}/definitions/{Uri.EscapeDataString(canonicalId)}/move", input.ToPayload(), ct);

    [HttpPost("definitions/{id:guid}/{canonicalId}/delete")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteDefinition(Guid id, string canonicalId, [FromForm] int versionToken, CancellationToken ct) =>
        ProxyDeleteAsync($"{ApiBase}/{id}/definitions/{Uri.EscapeDataString(canonicalId)}?versionToken={versionToken}", ct);

    [HttpPost("validate/{id:guid}")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ValidateDraft(Guid id, CancellationToken ct) =>
        ProxyPostAsync($"{ApiBase}/{id}/validate", new { }, ct);

    // ----- Helpers (HTTP/transport only; no business logic) -----

    private async Task<object?> BuildImportPayloadAsync(
        IFormFile? file,
        string? sourceBaselineKey,
        string? baselineVersion,
        string? changeSummary,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return null;
        }

        // The workbook is governance metadata only: read bytes -> base64 -> import API. Never written to disk.
        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var contentBase64 = Convert.ToBase64String(buffer.ToArray());

        // MOD-0028-FU07 — the import API resolves its parser by format/filename (xlsx, csv, flat-json). Derive the
        // format from the uploaded file extension instead of hardcoding xlsx, so register CSV / flat-JSON packages
        // import through the same wizard.
        var format = ResolveImportFormat(file.FileName);

        if (baselineVersion is null)
        {
            return new
            {
                fileName = file.FileName,
                format,
                contentBase64,
                sourceBaselineKey = sourceBaselineKey ?? string.Empty
            };
        }

        return new
        {
            fileName = file.FileName,
            format,
            contentBase64,
            sourceBaselineKey = sourceBaselineKey ?? string.Empty,
            baselineVersion,
            changeSummary
        };
    }

    private static string ResolveImportFormat(string? fileName)
    {
        var name = (fileName ?? string.Empty).Trim().ToLowerInvariant();
        if (name.EndsWith(".csv", StringComparison.Ordinal)) return "csv";
        if (name.EndsWith(".json", StringComparison.Ordinal)) return "json";
        return "xlsx";
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
            _logger.LogError(ex, "QMS baseline proxy GET {Path} failed.", path);
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
            _logger.LogError(ex, "QMS baseline proxy POST {Path} failed.", path);
            return GatewayErrorJson();
        }
    }

    private async Task<IActionResult> ProxyPutAsync(string path, object payload, CancellationToken ct) =>
        await ProxyJsonAsync(HttpMethod.Put, path, payload, ct);

    private async Task<IActionResult> ProxyPatchAsync(string path, object payload, CancellationToken ct) =>
        await ProxyJsonAsync(HttpMethod.Patch, path, payload, ct);

    private async Task<IActionResult> ProxyDeleteAsync(string path, CancellationToken ct)
    {
        if (!AddAuthHeaders())
        {
            return UnauthorizedJson();
        }

        try
        {
            var response = await _httpClient.DeleteAsync($"{_gatewayUrl}{path}", ct);
            return await PassthroughAsync(response, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QMS baseline proxy DELETE {Path} failed.", path);
            return GatewayErrorJson();
        }
    }

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
                Content = JsonContent.Create(payload, options: _jsonOptions)
            };
            var response = await _httpClient.SendAsync(request, ct);
            return await PassthroughAsync(response, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "QMS baseline proxy {Method} {Path} failed.", method, path);
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

    private IActionResult UnprocessableJson(string reasonCode) =>
        JsonFailure(422, reasonCode, _sharedLocalizer["ValidationFailed"].Value);

    // Successful empty-list envelope (matches the gateway shape) used when no binding key is supplied.
    private ContentResult EmptyListJson() => new()
    {
        Content = JsonSerializer.Serialize(new
        {
            data = Array.Empty<object>(),
            isSuccessful = true,
            statusCode = 200,
            errors = Array.Empty<string>(),
            correlation_id = HttpContext.TraceIdentifier
        }),
        ContentType = "application/json",
        StatusCode = 200
    };

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
            x.Type == "tenantId" ||
            x.Type == "tenant_id" ||
            x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;

    public sealed class QmsDefinitionFormInput
    {
        public string Name { get; set; } = string.Empty;
        public string? ParentCanonicalId { get; set; }
        public string? PurposeScope { get; set; }
        public string? RequiredByScope { get; set; }
        public string? AllowedDocClass { get; set; }
        public string? DefaultClassificationLevel { get; set; }
        public string? DefaultRetentionHint { get; set; }
        public int DisplayOrder { get; set; }
        public bool AllowsManualChildren { get; set; }
        public bool TemplatesAllowed { get; set; }
        public bool IsMandatory { get; set; }
        public bool IsProtected { get; set; }
        public int VersionToken { get; set; }

        public object ToPayload() => new
        {
            name = Name,
            parentCanonicalId = ParentCanonicalId,
            purposeScope = PurposeScope,
            requiredByScope = RequiredByScope,
            allowedDocClass = AllowedDocClass,
            defaultClassificationLevel = DefaultClassificationLevel,
            defaultRetentionHint = DefaultRetentionHint,
            displayOrder = DisplayOrder,
            allowsManualChildren = AllowsManualChildren,
            templatesAllowed = TemplatesAllowed,
            isMandatory = IsMandatory,
            isProtected = IsProtected,
            versionToken = VersionToken
        };
    }

    public sealed class QmsDefinitionMoveFormInput
    {
        public string? ParentCanonicalId { get; set; }
        public int DisplayOrder { get; set; }
        public int VersionToken { get; set; }

        public object ToPayload() => new
        {
            parentCanonicalId = ParentCanonicalId,
            displayOrder = DisplayOrder,
            versionToken = VersionToken
        };
    }
}
