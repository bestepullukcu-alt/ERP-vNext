using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.HCM;

[Authorize]
[Route("HCM/Employees")]
public sealed class EmployeesController : Controller
{
    private const string SearchPermission = "mod0251.employee.search";
    private const string ViewPermission = "mod0251.employee.view";
    private const string DraftPermission = "mod0251.employee.create_draft";
    private const string LegalEntityReadPermission = "mdm.legal-entities.read";
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string CorrelationHeaderName = "X-Correlation-Id";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _gatewayUrl;
    private readonly ILogger<EmployeesController> _logger;

    public EmployeesController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<EmployeesController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/') ?? "http://localhost:5000";
        _logger = logger;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["CanSearchEmployees"] = HasPermission(SearchPermission);
        ViewData["CanViewEmployees"] = HasPermission(ViewPermission);
        return View("~/Views/HCM/Employees/Index.cshtml");
    }

    [HttpGet("{employeeId:guid}")]
    public IActionResult Details(Guid employeeId)
    {
        ViewData["EmployeeId"] = employeeId.ToString("D");
        ViewData["CanViewEmployees"] = HasPermission(ViewPermission);
        return View("~/Views/HCM/Employees/Details.cshtml");
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        ViewData["CanCreateDraft"] = HasPermission(DraftPermission);
        return View("~/Views/HCM/Employees/Create.cshtml");
    }

    [HttpPost("drafts/api")]
    public Task<IActionResult> CreateDraft()
    {
        if (!HasPermission(DraftPermission))
        {
            return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." }));
        }

        return ProxyJsonGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/hcm/employees/drafts", readBody: true);
    }

    [HttpPatch("drafts/api/{draftSessionId:guid}")]
    public Task<IActionResult> PatchDraft(Guid draftSessionId)
    {
        if (!HasPermission(DraftPermission))
        {
            return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." }));
        }

        return ProxyJsonGatewayAsync(HttpMethod.Patch, $"{_gatewayUrl}/api/v1/hcm/employees/drafts/{draftSessionId:D}", readBody: true);
    }

    [HttpGet("drafts/api/{draftSessionId:guid}")]
    public Task<IActionResult> GetDraft(Guid draftSessionId)
    {
        if (!HasPermission(DraftPermission))
        {
            return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." }));
        }

        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/hcm/employees/drafts/{draftSessionId:D}", readBody: false);
    }

    [HttpPost("drafts/api/{draftSessionId:guid}/validate-references")]
    public Task<IActionResult> ValidateReferences(Guid draftSessionId)
    {
        if (!HasPermission(DraftPermission))
        {
            return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." }));
        }

        return ProxyJsonGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/hcm/employees/drafts/{draftSessionId:D}/validate-references", readBody: true);
    }

    [HttpPost("drafts/api/{draftSessionId:guid}/review")]
    public Task<IActionResult> ReviewDraft(Guid draftSessionId)
    {
        if (!HasPermission(DraftPermission))
        {
            return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." }));
        }

        return ProxyJsonGatewayAsync(HttpMethod.Post, $"{_gatewayUrl}/api/v1/hcm/employees/drafts/{draftSessionId:D}/review", readBody: true);
    }

    [HttpGet("reference-api/legal-entities")]
    public Task<IActionResult> SearchLegalEntities(
        [FromQuery] string? query,
        [FromQuery] bool? referenceable,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!HasPermission(LegalEntityReadPermission))
        {
            return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." }));
        }

        var parameters = new Dictionary<string, string?>
        {
            ["query"] = string.IsNullOrWhiteSpace(query) ? null : query.Trim(),
            ["referenceable"] = (referenceable ?? true).ToString().ToLowerInvariant(),
            ["page"] = Math.Max(1, page).ToString(),
            ["pageSize"] = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100).ToString()
        };

        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/legal-entities{BuildQuery(parameters)}", readBody: false);
    }

    [HttpGet("reference-api/legal-entities/{legalEntityId:guid}/lookup-validation")]
    public Task<IActionResult> ValidateLegalEntityReference(Guid legalEntityId)
    {
        if (!HasPermission(LegalEntityReadPermission))
        {
            return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." }));
        }

        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/legal-entities/{legalEntityId:D}/lookup-validation", readBody: false);
    }

    [HttpGet("api")]
    public Task<IActionResult> SearchEmployees(
        [FromQuery] string? search,
        [FromQuery] string? employeeStatus,
        [FromQuery] string? workerType,
        [FromQuery] string? employmentType,
        [FromQuery] Guid? legalEntityId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDirection = null)
    {
        if (!HasPermission(SearchPermission))
        {
            return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." }));
        }

        var parameters = new Dictionary<string, string?>
        {
            ["search"] = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            ["employeeStatus"] = string.IsNullOrWhiteSpace(employeeStatus) ? null : employeeStatus.Trim(),
            ["workerType"] = string.IsNullOrWhiteSpace(workerType) ? null : workerType.Trim(),
            ["employmentType"] = string.IsNullOrWhiteSpace(employmentType) ? null : employmentType.Trim(),
            ["legalEntityId"] = legalEntityId?.ToString("D"),
            ["page"] = Math.Max(1, page).ToString(),
            ["pageSize"] = Math.Clamp(pageSize <= 0 ? 20 : pageSize, 1, 100).ToString(),
            ["sortBy"] = string.IsNullOrWhiteSpace(sortBy) ? null : sortBy.Trim(),
            ["sortDirection"] = string.IsNullOrWhiteSpace(sortDirection) ? null : sortDirection.Trim()
        };

        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/hcm/employees{BuildQuery(parameters)}", readBody: false);
    }

    [HttpGet("api/{employeeId:guid}")]
    public Task<IActionResult> GetEmployee(Guid employeeId)
    {
        if (!HasPermission(ViewPermission))
        {
            return Task.FromResult<IActionResult>(StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." }));
        }

        return ProxyJsonGatewayAsync(HttpMethod.Get, $"{_gatewayUrl}/api/v1/hcm/employees/{employeeId:D}", readBody: false);
    }

    private async Task<IActionResult> ProxyJsonGatewayAsync(HttpMethod method, string targetUrl, bool readBody)
    {
        if (!TryCreateTenantRequest(method, targetUrl, out var request))
        {
            return Unauthorized(new { message = "Unauthorized" });
        }

        try
        {
            using (request)
            {
                if (readBody)
                {
                    using var reader = new StreamReader(Request.Body, Encoding.UTF8);
                    var body = await reader.ReadToEndAsync(HttpContext.RequestAborted);
                    request.Content = new StringContent(body, Encoding.UTF8, Request.ContentType ?? "application/json");
                }

                if (Request.Headers.TryGetValue("If-Match", out var ifMatch))
                {
                    request.Headers.TryAddWithoutValidation("If-Match", ifMatch.ToArray());
                }

                var client = _httpClientFactory.CreateClient();
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
                var content = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);
                return new ContentResult
                {
                    Content = content,
                    ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                    StatusCode = (int)response.StatusCode
                };
            }
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HCM draft proxy failed for {Method} {TargetUrl}.", method, targetUrl);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "HCM draft dependency unavailable." });
        }
    }

    private bool TryCreateTenantRequest(HttpMethod method, string targetUrl, out HttpRequestMessage request)
    {
        request = new HttpRequestMessage(method, targetUrl);
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token) || !TryResolveTenantId(token, out var tenantId))
        {
            request.Dispose();
            request = null!;
            return false;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation(TenantHeaderName, tenantId.ToString("D"));
        request.Headers.TryAddWithoutValidation(CorrelationHeaderName, ResolveCorrelationId());
        if (Request.Headers.TryGetValue("Accept-Language", out var acceptLanguage))
        {
            request.Headers.TryAddWithoutValidation("Accept-Language", acceptLanguage.ToString());
        }

        return true;
    }

    private bool HasPermission(string permission) => PermissionClaims.HasPermission(User, permission);

    private string ResolveCorrelationId()
    {
        if (Request.Headers.TryGetValue(CorrelationHeaderName, out var correlationId) &&
            !string.IsNullOrWhiteSpace(correlationId.ToString()))
        {
            return correlationId.ToString();
        }

        return HttpContext.TraceIdentifier;
    }

    private static bool TryResolveTenantId(string token, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var claimValue = FindClaim(jwt.Claims, "tenant_id", "tenantId");
            return Guid.TryParse(claimValue, out tenantId) && tenantId != Guid.Empty && jwt.ValidTo > DateTime.UtcNow;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindClaim(IEnumerable<Claim> claims, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = claims.FirstOrDefault(claim =>
                string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase) ||
                claim.Type.EndsWith($"/{claimType}", StringComparison.OrdinalIgnoreCase))?.Value;

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string BuildQuery(IReadOnlyDictionary<string, string?> parameters)
    {
        var pairs = parameters
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!)}")
            .ToArray();

        return pairs.Length == 0 ? string.Empty : $"?{string.Join("&", pairs)}";
    }
}
