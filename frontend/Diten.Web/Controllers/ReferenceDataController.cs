using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Diten.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Route("Platform/ReferenceData")]
public sealed class ReferenceDataController : Controller
{
    private const string ReferenceDataReadPermission = "Platform.BusinessReferenceData.Read";
    private const string PermissionSetCreate = "Platform.BusinessReferenceData.Create";
    private const string PermissionSetUpdate = "Platform.BusinessReferenceData.Update";
    private const string PermissionVersionCreate = "Platform.BusinessReferenceData.Version.Create";
    private const string PermissionVersionUpdate = "Platform.BusinessReferenceData.Version.Update";
    private const string PermissionImportPreview = "Platform.BusinessReferenceData.Import.Preview";
    private const string PermissionImportCommit = "Platform.BusinessReferenceData.Import.Commit";
    private const string PermissionVersionValidate = "Platform.BusinessReferenceData.Version.Validate";
    private const string PermissionVersionSubmit = "Platform.BusinessReferenceData.Version.Submit";
    private const string PermissionVersionApprove = "Platform.BusinessReferenceData.Version.Approve";
    private const string PermissionVersionPublish = "Platform.BusinessReferenceData.Version.Publish";
    private const string PermissionVersionPublishOverride = "Platform.BusinessReferenceData.Version.PublishOverride";
    private const string PermissionUsageRegister = "Platform.BusinessReferenceData.Usage.Register";
    private const string CorrelationHeaderName = "X-Correlation-Id";
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string MissingTenantMessage = "ReferenceData requires tenant context. 'X-Tenant-Id' or JWT tenant_id claim is required.";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _gatewayUrl;
    private readonly ILogger<ReferenceDataController> _logger;

    public ReferenceDataController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ReferenceDataController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/') ?? "http://localhost:5000";
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        var denied = EnsureReadAccess();
        if (denied is not null)
        {
            return denied;
        }

        ViewBag.ActiveMenu = "reference-data";
        ApplyPermissionViewData();
        return View("~/Views/Platform/ReferenceData/Index.cshtml");
    }

    [HttpGet("Sets/{setId}")]
    public IActionResult SetDetail(string setId)
    {
        var denied = EnsureReadAccess();
        if (denied is not null)
        {
            return denied;
        }

        if (!Guid.TryParse(setId, out var parsedSetId))
        {
            return NotFound();
        }

        ViewData["SetId"] = parsedSetId;
        ViewBag.ActiveMenu = "reference-data";
        ApplyPermissionViewData();
        return View("~/Views/Platform/ReferenceData/SetDetail.cshtml");
    }

    [HttpGet("Sets/{setId}/DraftWizard")]
    [HttpGet("Sets/{setId}/draft-wizard")]
    public IActionResult DraftWizard(string setId, [FromQuery] string? mode = null, [FromQuery] Guid? versionId = null)
    {
        var denied = EnsureReadAccess();
        if (denied is not null)
        {
            return denied;
        }

        if (!Guid.TryParse(setId, out var parsedSetId))
        {
            return NotFound();
        }

        ViewData["SetId"] = parsedSetId;
        ViewData["WizardMode"] = mode;
        ViewData["VersionId"] = versionId;
        ViewBag.ActiveMenu = "reference-data";
        ApplyPermissionViewData();
        return View("~/Views/Platform/ReferenceData/DraftWizard.cshtml");
    }

    [HttpGet("Versions/{versionId:guid}")]
    public IActionResult VersionDetail(Guid versionId)
    {
        var denied = EnsureReadAccess();
        if (denied is not null)
        {
            return denied;
        }

        ViewData["VersionId"] = versionId;
        ViewBag.ActiveMenu = "reference-data";
        ApplyPermissionViewData();
        return View("~/Views/Platform/ReferenceData/VersionDetail.cshtml");
    }

    [HttpGet("Hierarchy/{setCode}")]
    public IActionResult Hierarchy(string setCode)
    {
        var denied = EnsureReadAccess();
        if (denied is not null)
        {
            return denied;
        }

        ViewData["SetCode"] = setCode;
        ViewBag.ActiveMenu = "reference-data";
        ApplyPermissionViewData();
        return View("~/Views/Platform/ReferenceData/HierarchyView.cshtml");
    }

    [HttpGet("Attributes/{setCode}")]
    public IActionResult AttributeManager(string setCode)
    {
        var denied = EnsureReadAccess();
        if (denied is not null)
        {
            return denied;
        }

        ViewData["SetCode"] = setCode;
        ViewBag.ActiveMenu = "reference-data";
        ApplyPermissionViewData();
        return View("~/Views/Platform/ReferenceData/AttributeManager.cshtml");
    }

    [HttpGet("Mappings/{setCode}")]
    public IActionResult MappingManager(string setCode)
    {
        var denied = EnsureReadAccess();
        if (denied is not null)
        {
            return denied;
        }

        ViewData["SetCode"] = setCode;
        ViewBag.ActiveMenu = "reference-data";
        ApplyPermissionViewData();
        return View("~/Views/Platform/ReferenceData/MappingManager.cshtml");
    }

    [HttpGet("PublishReview/{versionId:guid}")]
    public IActionResult PublishReview(Guid versionId)
    {
        var denied = EnsureReadAccess();
        if (denied is not null)
        {
            return denied;
        }

        ViewData["VersionId"] = versionId;
        ViewBag.ActiveMenu = "reference-data";
        ApplyPermissionViewData();
        return View("~/Views/Platform/ReferenceData/PublishReview.cshtml");
    }

    [HttpGet("Usage/{setCode}")]
    public IActionResult UsageDependencies(string setCode)
    {
        var denied = EnsureReadAccess();
        if (denied is not null)
        {
            return denied;
        }

        ViewData["SetCode"] = setCode;
        ViewBag.ActiveMenu = "reference-data";
        ApplyPermissionViewData();
        return View("~/Views/Platform/ReferenceData/UsageDependencies.cshtml");
    }

    [HttpGet("Usage/{setCode}/Create")]
    public IActionResult UsageCreate(string setCode)
    {
        var denied = EnsureReadAccess();
        if (denied is not null)
        {
            return denied;
        }

        ViewData["SetCode"] = setCode;
        ViewBag.ActiveMenu = "reference-data";
        ApplyPermissionViewData();
        return View("~/Views/Platform/ReferenceData/UsageCreate.cshtml");
    }

    [HttpGet("Usage/{setCode}/Edit/{usageRegistrationId:guid}")]
    public IActionResult UsageEdit(string setCode, Guid usageRegistrationId)
    {
        var denied = EnsureReadAccess();
        if (denied is not null)
        {
            return denied;
        }

        ViewData["SetCode"] = setCode;
        ViewData["UsageRegistrationId"] = usageRegistrationId;
        ViewBag.ActiveMenu = "reference-data";
        ApplyPermissionViewData();
        return View("~/Views/Platform/ReferenceData/UsageEdit.cshtml");
    }

    [HttpGet("Usage/{setCode}/Details/{usageRegistrationId:guid}")]
    public IActionResult UsageDetails(string setCode, Guid usageRegistrationId)
    {
        var denied = EnsureReadAccess();
        if (denied is not null)
        {
            return denied;
        }

        ViewData["SetCode"] = setCode;
        ViewData["UsageRegistrationId"] = usageRegistrationId;
        ViewBag.ActiveMenu = "reference-data";
        ApplyPermissionViewData();
        return View("~/Views/Platform/ReferenceData/UsageDetails.cshtml");
    }

    [HttpGet("ImportPreview")]
    public IActionResult ImportPreview()
    {
        var denied = EnsureReadAccess();
        if (denied is not null)
        {
            return denied;
        }

        ViewBag.ActiveMenu = "reference-data";
        ApplyPermissionViewData();
        return View("~/Views/Platform/ReferenceData/ImportPreview.cshtml");
    }

    [HttpGet("api/{**everything}")]
    [HttpPost("api/{**everything}")]
    [HttpPut("api/{**everything}")]
    [HttpPatch("api/{**everything}")]
    [HttpDelete("api/{**everything}")]
    public async Task<IActionResult> ProxyApi(string? everything)
    {
        if (!HasPermission(ReferenceDataReadPermission))
        {
            return Forbid();
        }

        return await ProxyReferenceDataAsync(everything);
    }

    private async Task<IActionResult> ProxyReferenceDataAsync(string? relativePath)
    {
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { message = "Unauthorized" });
        }

        var tenantId = ResolveTenantId(token);
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { message = $"'{TenantHeaderName}' or JWT tenant_id claim is required." });
        }

        var targetPath = string.IsNullOrWhiteSpace(relativePath)
            ? "/api/v1/reference-data"
            : $"/api/v1/reference-data/{relativePath.TrimStart('/')}";
        var targetUrl = $"{_gatewayUrl}{targetPath}{Request.QueryString}";
        var correlationId = ResolveCorrelationId();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var proxyRequest = new HttpRequestMessage(new HttpMethod(Request.Method), targetUrl);
            proxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            proxyRequest.Headers.TryAddWithoutValidation(TenantHeaderName, tenantId);
            proxyRequest.Headers.TryAddWithoutValidation(CorrelationHeaderName, correlationId);

            if (Request.Headers.TryGetValue("Accept-Language", out var acceptLanguage))
            {
                proxyRequest.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage.ToString());
            }

            if (Request.Headers.TryGetValue("Idempotency-Key", out var idempotencyKey))
            {
                proxyRequest.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey.ToString());
            }

            if (!HttpMethods.IsGet(Request.Method) &&
                !HttpMethods.IsHead(Request.Method) &&
                Request.ContentLength is > 0)
            {
                using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
                var body = await reader.ReadToEndAsync(HttpContext.RequestAborted);
                proxyRequest.Content = new StringContent(body, Encoding.UTF8, Request.ContentType ?? "application/json");
            }

            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(proxyRequest, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
            var payload = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);
            stopwatch.Stop();

            var statusCode = (int)response.StatusCode;
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "BusinessReferenceData proxy completed. method={Method} path={Path} status={StatusCode} tenant_id={TenantId} correlation_id={CorrelationId} elapsed_ms={ElapsedMs}",
                    Request.Method,
                    targetPath,
                    statusCode,
                    tenantId,
                    correlationId,
                    stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogWarning(
                    "BusinessReferenceData proxy completed with non-success status. method={Method} path={Path} status={StatusCode} tenant_id={TenantId} correlation_id={CorrelationId} elapsed_ms={ElapsedMs}",
                    Request.Method,
                    targetPath,
                    statusCode,
                    tenantId,
                    correlationId,
                    stopwatch.ElapsedMilliseconds);
            }

            return new ContentResult
            {
                StatusCode = statusCode,
                Content = payload,
                ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json"
            };
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "BusinessReferenceData proxy failed. method={Method} path={Path} tenant_id={TenantId} correlation_id={CorrelationId} elapsed_ms={ElapsedMs}",
                Request.Method,
                targetPath,
                tenantId,
                correlationId,
                stopwatch.ElapsedMilliseconds);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "ReferenceData gateway proxy unavailable." });
        }
    }

    private void ApplyPermissionViewData()
    {
        ViewData["RdCanReadSet"] = HasPermission(ReferenceDataReadPermission);
        ViewData["RdCanCreateSet"] = HasPermission(PermissionSetCreate);
        ViewData["RdCanUpdateSet"] = HasPermission(PermissionSetUpdate);
        ViewData["RdCanCreateVersion"] = HasPermission(PermissionVersionCreate);
        ViewData["RdCanUpdateVersion"] = HasPermission(PermissionVersionUpdate);
        ViewData["RdCanImportPreview"] = HasPermission(PermissionImportPreview);
        ViewData["RdCanImportCommit"] = HasPermission(PermissionImportCommit);
        ViewData["RdCanValidateVersion"] = HasPermission(PermissionVersionValidate);
        ViewData["RdCanSubmitVersion"] = HasPermission(PermissionVersionSubmit);
        ViewData["RdCanApproveVersion"] = HasPermission(PermissionVersionApprove);
        ViewData["RdCanPublishVersion"] = HasPermission(PermissionVersionPublish);
        ViewData["RdCanPublishOverride"] = HasPermission(PermissionVersionPublishOverride);
        ViewData["RdCanRegisterUsage"] = HasPermission(PermissionUsageRegister);
    }

    private bool HasPermission(string permission)
        => PermissionClaims.HasPermission(User, permission);

    private IActionResult? EnsureReadAccess()
    {
        if (!HasPermission(ReferenceDataReadPermission))
        {
            var returnUrl = $"{Request.Path}{Request.QueryString}";
            return RedirectToAction("AccessDenied", "Account", new { returnUrl });
        }

        return string.IsNullOrWhiteSpace(ResolveTenantId())
            ? BadRequest(new { message = MissingTenantMessage })
            : null;
    }

    private string ResolveCorrelationId()
    {
        if (Request.Headers.TryGetValue(CorrelationHeaderName, out var correlationId) &&
            !string.IsNullOrWhiteSpace(correlationId.ToString()))
        {
            return correlationId.ToString();
        }

        return HttpContext.TraceIdentifier;
    }

    private string? ResolveTenantId(string? token = null)
    {
        var claimValue = FindTenantClaim(User.Claims);
        if (Guid.TryParse(claimValue, out var claimTenantId))
        {
            return claimTenantId.ToString();
        }

        if (Request.Headers.TryGetValue(TenantHeaderName, out var headerTenant) &&
            Guid.TryParse(headerTenant.ToString(), out var parsedHeaderTenant))
        {
            return parsedHeaderTenant.ToString();
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var jwtClaimValue = FindTenantClaim(jwt.Claims);
            return Guid.TryParse(jwtClaimValue, out var jwtTenantId)
                ? jwtTenantId.ToString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindTenantClaim(IEnumerable<Claim> claims)
    {
        return claims.FirstOrDefault(claim =>
            string.Equals(claim.Type, "tenant_id", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(claim.Type, "tenantId", StringComparison.OrdinalIgnoreCase) ||
            claim.Type.EndsWith("/tenant_id", StringComparison.OrdinalIgnoreCase) ||
            claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
    }
}
