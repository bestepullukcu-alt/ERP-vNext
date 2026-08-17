using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[ApiController]
[Route("api/platform/catalog")]
public sealed class PlatformCatalogProxyController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public PlatformCatalogProxyController(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpGet("domain-landscapes")]
    public Task<IActionResult> GetDomainLandscapes() => ProxyGetAsync("domain-landscapes");

    [HttpGet("suite-platforms")]
    public Task<IActionResult> GetSuitePlatforms() => ProxyGetAsync("suite-platforms");

    [HttpGet("capability-groups")]
    public Task<IActionResult> GetCapabilityGroups() => ProxyGetAsync("capability-groups");

    [HttpGet("modules")]
    public Task<IActionResult> GetModules() => ProxyGetAsync("modules");

    [HttpGet("modules/{moduleId}")]
    public Task<IActionResult> GetModule(string moduleId) => ProxyGetAsync($"modules/{Uri.EscapeDataString(moduleId)}");

    [HttpGet("modules/{moduleId}/pages")]
    public Task<IActionResult> GetModulePages(string moduleId) => ProxyGetAsync($"modules/{Uri.EscapeDataString(moduleId)}/pages");

    [HttpGet("modules/{moduleId}/pages/{pageCode}")]
    public Task<IActionResult> GetModulePage(string moduleId, string pageCode) =>
        ProxyGetAsync($"modules/{Uri.EscapeDataString(moduleId)}/pages/{Uri.EscapeDataString(pageCode)}");

    [HttpGet("hierarchy")]
    public Task<IActionResult> GetHierarchy() => ProxyGetAsync("hierarchy");

    [HttpPost("import")]
    public Task<IActionResult> Import()
    {
        return ProxyWriteAsync(HttpMethod.Post, "import");
    }

    [HttpPost("domain-landscapes")]
    public Task<IActionResult> CreateDomainLandscape() => ProxyWriteAsync(HttpMethod.Post, "domain-landscapes");

    [HttpPut("domain-landscapes/{id}")]
    public Task<IActionResult> UpdateDomainLandscape(Guid id) => ProxyWriteAsync(HttpMethod.Put, $"domain-landscapes/{id}");

    [HttpPost("suite-platforms")]
    public Task<IActionResult> CreateSuitePlatform() => ProxyWriteAsync(HttpMethod.Post, "suite-platforms");

    [HttpPut("suite-platforms/{id}")]
    public Task<IActionResult> UpdateSuitePlatform(Guid id) => ProxyWriteAsync(HttpMethod.Put, $"suite-platforms/{id}");

    [HttpPost("capability-groups")]
    public Task<IActionResult> CreateCapabilityGroup() => ProxyWriteAsync(HttpMethod.Post, "capability-groups");

    [HttpPut("capability-groups/{id}")]
    public Task<IActionResult> UpdateCapabilityGroup(Guid id) => ProxyWriteAsync(HttpMethod.Put, $"capability-groups/{id}");

    [HttpPost("modules")]
    public Task<IActionResult> CreateModule() => ProxyWriteAsync(HttpMethod.Post, "modules");

    [HttpPut("modules/{moduleId}")]
    public Task<IActionResult> UpdateModule(string moduleId) => ProxyWriteAsync(HttpMethod.Put, $"modules/{Uri.EscapeDataString(moduleId)}");

    [HttpPost("modules/{moduleId}/pages")]
    public Task<IActionResult> CreateModulePage(string moduleId) => ProxyWriteAsync(HttpMethod.Post, $"modules/{Uri.EscapeDataString(moduleId)}/pages");

    [HttpPut("modules/{moduleId}/pages/{pageCode}")]
    public Task<IActionResult> UpdateModulePage(string moduleId, string pageCode) =>
        ProxyWriteAsync(HttpMethod.Put, $"modules/{Uri.EscapeDataString(moduleId)}/pages/{Uri.EscapeDataString(pageCode)}");

    private Task<IActionResult> ProxyGetAsync(string relativePath)
    {
        return ProxyWriteAsync(HttpMethod.Get, relativePath);
    }

    private async Task<IActionResult> ProxyWriteAsync(HttpMethod method, string relativePath)
    {
        var gatewayBaseUrl = _configuration["GatewayUrl"]?.TrimEnd('/');
        var platformServiceUrl = _configuration["PlatformServiceUrl"]?.TrimEnd('/');

        var response = await SendAsync(method, gatewayBaseUrl, relativePath, allowFallback: true);
        if (response == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                title = "Catalog service unavailable",
                detail = "Neither gateway nor platform service could be reached."
            });
        }

        if ((response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.MethodNotAllowed) &&
            !string.IsNullOrWhiteSpace(platformServiceUrl))
        {
            response.Dispose();
            response = await SendAsync(method, platformServiceUrl, relativePath, allowFallback: false);
        }

        if (response == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                title = "Catalog service unavailable",
                detail = "Platform service fallback could not be reached."
            });
        }

        using var _ = response.Content;
        var payload = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            Content = payload,
            ContentType = contentType
        };
    }

    private async Task<HttpResponseMessage?> SendAsync(HttpMethod method, string? baseUrl, string relativePath, bool allowFallback)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(method, BuildUrl(baseUrl, relativePath));
            var accessToken = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            if (Request.Headers.TryGetValue("Accept-Language", out var language))
            {
                request.Headers.TryAddWithoutValidation("Accept-Language", language.ToString());
            }

            if (method != HttpMethod.Get)
            {
                using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
                var body = await reader.ReadToEndAsync(HttpContext.RequestAborted);
                Request.Body.Position = 0;
                request.Content = new StringContent(body, Encoding.UTF8, Request.ContentType ?? "application/json");
            }

            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
        }
        catch when (allowFallback)
        {
            return null;
        }
    }

    private string BuildUrl(string baseUrl, string relativePath)
    {
        var query = Request.QueryString.HasValue ? Request.QueryString.Value : string.Empty;
        return $"{baseUrl}/api/platform/catalog/{relativePath}{query}";
    }
}
