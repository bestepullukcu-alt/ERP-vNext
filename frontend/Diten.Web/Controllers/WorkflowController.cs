using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

// MOD-0023 Batch 08 — Workflow Config / Approval Templates admin UI.
// This controller is a thin view host + gateway proxy. It contains no workflow business logic:
// every API call is forwarded to the gateway (/api/v1/workflow/**) which routes to Diten.Platform.
// TenantId is never accepted from the client; it is resolved server-side from the JWT/claims and
// injected as the X-Tenant-Id header, mirroring the canonical ReferenceData proxy.
[Authorize]
[Route("Platform/Workflow")]
public sealed class WorkflowController : Controller
{
    private const string CorrelationHeaderName = "X-Correlation-Id";
    private const string TenantHeaderName = "X-Tenant-Id";

    // Surfaced to the view so UI affordances can be hinted. The backend remains the source of
    // authorization truth — every endpoint enforces its own [HasPermission] and may return 403.
    private const string DefinitionsViewPermission = "platform.workflow.definitions.view";
    private const string DefinitionsManagePermission = "platform.workflow.definitions.manage";
    private const string DefinitionsPublishPermission = "platform.workflow.definitions.publish";
    private const string InstancesStartPermission = "platform.workflow.instances.start";
    private const string TasksApprovePermission = "platform.workflow.tasks.approve";
    private const string EscalationsManagePermission = "platform.workflow.escalations.manage";
    private const string EscalationsRunPermission = "platform.workflow.escalations.run";
    private const string TransitionsEvaluatePermission = "platform.workflow.transitions.evaluate";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _gatewayUrl;
    private readonly ILogger<WorkflowController> _logger;

    public WorkflowController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<WorkflowController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/') ?? "http://localhost:5000";
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewBag.ActiveMenu = "workflow";
        ApplyPermissionViewData();
        return View("~/Views/Platform/Workflow/Index.cshtml");
    }

    [HttpGet("Definitions/{id:guid}")]
    public IActionResult Details(Guid id)
    {
        ViewBag.ActiveMenu = "workflow";
        ViewData["DefinitionId"] = id;
        ApplyPermissionViewData();
        return View("~/Views/Platform/Workflow/Details.cshtml");
    }

    [HttpGet("Definitions/{id:guid}/VisualDesigner")]
    public IActionResult VisualDesigner(Guid id)
    {
        ViewBag.ActiveMenu = "workflow";
        ViewData["DefinitionId"] = id;
        ApplyPermissionViewData();
        return View("~/Views/Platform/Workflow/VisualDesigner.cshtml");
    }

    [HttpGet("Definitions/{id:guid}/Designer")]
    public IActionResult Designer(Guid id)
    {
        ViewBag.ActiveMenu = "workflow";
        ViewData["DefinitionId"] = id;
        ApplyPermissionViewData();
        return View("~/Views/Platform/Workflow/Designer.cshtml");
    }

    [HttpGet("Definitions/{id:guid}/Versions")]
    public IActionResult Versions(Guid id)
    {
        ViewBag.ActiveMenu = "workflow";
        ViewData["DefinitionId"] = id;
        ApplyPermissionViewData();
        return View("~/Views/Platform/Workflow/Versions.cshtml");
    }

    [HttpGet("lookup/users")]
    public Task<IActionResult> LookupUsers() => ProxyGatewayAsync("/api/users");

    [HttpGet("lookup/positions")]
    public Task<IActionResult> LookupPositions() => ProxyGatewayAsync("/api/v1/workflow/lookups/positions");

    [HttpGet("api/{**everything}")]
    [HttpPost("api/{**everything}")]
    [HttpPut("api/{**everything}")]
    [HttpPatch("api/{**everything}")]
    [HttpDelete("api/{**everything}")]
    public Task<IActionResult> ProxyApi(string? everything) => ProxyWorkflowAsync(everything);

    private async Task<IActionResult> ProxyWorkflowAsync(string? relativePath)
    {
        var targetPath = string.IsNullOrWhiteSpace(relativePath)
            ? "/api/v1/workflow"
            : $"/api/v1/workflow/{relativePath.TrimStart('/')}";

        return await ProxyGatewayAsync(targetPath);
    }

    private async Task<IActionResult> ProxyGatewayAsync(string targetPath)
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
                    "Workflow proxy completed. method={Method} path={Path} status={StatusCode} tenant_id={TenantId} correlation_id={CorrelationId} elapsed_ms={ElapsedMs}",
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
                    "Workflow proxy completed with non-success status. method={Method} path={Path} status={StatusCode} tenant_id={TenantId} correlation_id={CorrelationId} elapsed_ms={ElapsedMs}",
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
                "Workflow proxy failed. method={Method} path={Path} tenant_id={TenantId} correlation_id={CorrelationId} elapsed_ms={ElapsedMs}",
                Request.Method,
                targetPath,
                tenantId,
                correlationId,
                stopwatch.ElapsedMilliseconds);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Workflow gateway proxy unavailable." });
        }
    }

    private void ApplyPermissionViewData()
    {
        ViewData["WfCanViewDefinitions"] = HasPermission(DefinitionsViewPermission);
        ViewData["WfCanManageDefinitions"] = HasPermission(DefinitionsManagePermission);
        ViewData["WfCanPublishDefinitions"] = HasPermission(DefinitionsPublishPermission);
        ViewData["WfCanStartInstances"] = HasPermission(InstancesStartPermission);
        ViewData["WfCanActionTasks"] = HasPermission(TasksApprovePermission);
        ViewData["WfCanManageEscalations"] = HasPermission(EscalationsManagePermission);
        ViewData["WfCanRunEscalations"] = HasPermission(EscalationsRunPermission);
        ViewData["WfCanEvaluateTransitions"] = HasPermission(TransitionsEvaluatePermission);
    }

    private bool HasPermission(string permission)
        => PermissionClaims.HasPermission(User, permission);

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
