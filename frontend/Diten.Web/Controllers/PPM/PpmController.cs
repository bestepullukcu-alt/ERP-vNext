using System.Net.Http.Headers;
using System.Text;
using Diten.Web.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.PPM;

[Authorize]
[Route("ppm")]
public sealed class PpmController(HttpClient httpClient, IConfiguration configuration, ILogger<PpmController> logger) : Controller
{
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string CorrelationHeaderName = "X-Correlation-Id";
    private static readonly HashSet<string> Resources =
        new(["portfolios", "initiatives", "programs", "projects", "investment-cases", "benefit-commitments"], StringComparer.OrdinalIgnoreCase);

    private readonly string _gatewayUrl = (configuration["GatewayUrl"]
        ?? throw new InvalidOperationException("GatewayUrl configuration is required.")).TrimEnd('/');

    [HttpGet("")]
    public IActionResult Hub() => View("~/Views/PPM/Index.cshtml");

    [HttpGet("{resource}")]
    public IActionResult Index(string resource)
    {
        if (!Resources.Contains(resource))
            return NotFound();

        return View($"~/Views/PPM/{ToFolder(resource)}/Index.cshtml");
    }

    [HttpGet("projects/{id:guid}")]
    public IActionResult ProjectWorkspace(Guid id)
    {
        ViewData["ProjectId"] = id;
        return View("~/Views/PPM/Projects/Workspace.cshtml");
    }

    [HttpGet("{resource}/api")]
    public Task<IActionResult> List(string resource, CancellationToken cancellationToken) =>
        ProxyAsync(resource, HttpMethod.Get, null, cancellationToken, queryString: Request.QueryString.Value);

    [HttpGet("{resource}/api/{id:guid}")]
    public Task<IActionResult> Get(string resource, Guid id, CancellationToken cancellationToken) =>
        ProxyAsync(resource, HttpMethod.Get, id.ToString(), cancellationToken);

    [HttpGet("initiatives/api/contracts/v2")]
    public Task<IActionResult> InitiativeContracts(CancellationToken cancellationToken) =>
        ProxyAsync("initiatives", HttpMethod.Get, "contracts/v2", cancellationToken);

    [HttpGet("initiatives/api/lifecycle-contracts/v2")]
    public Task<IActionResult> InitiativeLifecycleContracts(CancellationToken cancellationToken) =>
        ProxyAsync("initiatives", HttpMethod.Get, "lifecycle-contracts/v2", cancellationToken);

    [HttpGet("initiatives/api/{id:guid}/details/links")]
    public Task<IActionResult> InitiativeDetailLinks(Guid id, CancellationToken cancellationToken) =>
        ProxyAsync("initiatives", HttpMethod.Get, $"{id}/details/links", cancellationToken);

    [ValidateAntiForgeryToken]
    [HttpPost("{resource}/api")]
    public Task<IActionResult> Create(string resource, CancellationToken cancellationToken) =>
        ProxyBodyAsync(resource, HttpMethod.Post, null, cancellationToken);

    [ValidateAntiForgeryToken]
    [HttpPut("{resource}/api/{id:guid}")]
    public Task<IActionResult> Update(string resource, Guid id, CancellationToken cancellationToken) =>
        ProxyBodyAsync(resource, HttpMethod.Put, id.ToString(), cancellationToken);

    [ValidateAntiForgeryToken]
    [HttpPost("{resource}/api/{id:guid}/lifecycle")]
    public Task<IActionResult> Transition(string resource, Guid id, CancellationToken cancellationToken) =>
        ProxyBodyAsync(resource, HttpMethod.Post, $"{id}/lifecycle", cancellationToken);

    [ValidateAntiForgeryToken]
    [HttpPost("initiatives/api/{terminalId:guid}/successors")]
    public Task<IActionResult> CreateInitiativeSuccessor(Guid terminalId, CancellationToken cancellationToken) =>
        ProxyBodyAsync("initiatives", HttpMethod.Post, $"{terminalId}/successors", cancellationToken);

    [ValidateAntiForgeryToken]
    [HttpDelete("{resource}/api/{id:guid}")]
    public Task<IActionResult> SoftDelete(string resource, Guid id, [FromQuery] int expectedVersion, CancellationToken cancellationToken) =>
        ProxyAsync(resource, HttpMethod.Delete, $"{id}?expectedVersion={expectedVersion}", cancellationToken);

    private async Task<IActionResult> ProxyBodyAsync(
        string resource,
        HttpMethod method,
        string? suffix,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(cancellationToken);
        return await ProxyAsync(resource, method, suffix, cancellationToken, body);
    }

    private async Task<IActionResult> ProxyAsync(
        string resource,
        HttpMethod method,
        string? suffix,
        CancellationToken cancellationToken,
        string? body = null,
        string? queryString = null)
    {
        if (!Resources.Contains(resource))
            return NotFound();

        var tenantId = ResolveTenantId();
        if (tenantId is null)
            return StatusCode(StatusCodes.Status403Forbidden);

        var target = $"{_gatewayUrl}/api/v1/ppm/{resource}";
        if (!string.IsNullOrWhiteSpace(suffix))
            target += $"/{suffix}";
        if (!string.IsNullOrWhiteSpace(queryString))
            target += queryString.StartsWith("?", StringComparison.Ordinal) ? queryString : $"?{queryString}";

        using var request = new HttpRequestMessage(method, target);
        var token = AuthTokenCookies.GetAccessToken(Request);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.TryAddWithoutValidation(TenantHeaderName, tenantId.Value.ToString("D"));
        request.Headers.TryAddWithoutValidation(CorrelationHeaderName, ResolveCorrelationId());

        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return new ContentResult
            {
                Content = content,
                ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                StatusCode = (int)response.StatusCode
            };
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "PPM Gateway request failed for {Resource}.", resource);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { statusCode = StatusCodes.Status503ServiceUnavailable });
        }
    }

    private Guid? ResolveTenantId()
    {
        var raw = User.Claims.FirstOrDefault(claim =>
            claim.Type == "tenantId" ||
            claim.Type == "tenant_id" ||
            claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
        return Guid.TryParse(raw, out var tenantId) && tenantId != Guid.Empty ? tenantId : null;
    }

    private string ResolveCorrelationId()
    {
        var incoming = Request.Headers[CorrelationHeaderName].FirstOrDefault();
        return string.IsNullOrWhiteSpace(incoming) ? HttpContext.TraceIdentifier : incoming.Trim();
    }

    private static string ToFolder(string resource) => resource.ToLowerInvariant() switch
    {
        "investment-cases" => "InvestmentCases",
        "benefit-commitments" => "BenefitCommitments",
        _ => char.ToUpperInvariant(resource[0]) + resource[1..]
    };
}
