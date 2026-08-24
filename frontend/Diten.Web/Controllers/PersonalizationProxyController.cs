using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/personalization/views")]
public sealed class PersonalizationProxyController : ControllerBase
{
    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger<PersonalizationProxyController> _logger;

    public PersonalizationProxyController(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PersonalizationProxyController> logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/')
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _logger = logger;
    }

    [HttpGet]
    public Task<IActionResult> Get(CancellationToken cancellationToken) =>
        ForwardAsync(HttpMethod.Get, id: null, cancellationToken);

    [HttpPost]
    public Task<IActionResult> Create(CancellationToken cancellationToken) =>
        ForwardAsync(HttpMethod.Post, id: null, cancellationToken);

    [HttpPut("{id}")]
    public Task<IActionResult> Update(string id, CancellationToken cancellationToken) =>
        ForwardAsync(HttpMethod.Put, id, cancellationToken);

    [HttpDelete("{id}")]
    public Task<IActionResult> Delete(string id, CancellationToken cancellationToken) =>
        ForwardAsync(HttpMethod.Delete, id, cancellationToken);

    private async Task<IActionResult> ForwardAsync(
        HttpMethod method,
        string? id,
        CancellationToken cancellationToken)
    {
        var target = new StringBuilder($"{_gatewayUrl}/api/personalization/views");
        if (!string.IsNullOrWhiteSpace(id))
        {
            target.Append('/').Append(Uri.EscapeDataString(id));
        }

        target.Append(Request.QueryString);
        using var request = new HttpRequestMessage(method, target.ToString());
        if (!TryApplySecurityHeaders(request))
        {
            return Unauthorized();
        }

        if (method == HttpMethod.Post || method == HttpMethod.Put)
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(cancellationToken);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                return StatusCode(StatusCodes.Status204NoContent);
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return new ContentResult
            {
                StatusCode = (int)response.StatusCode,
                ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
                Content = responseBody
            };
        }
        catch (HttpRequestException exception)
        {
            _logger.LogError(exception, "Personalization Gateway proxy failed.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    private bool TryApplySecurityHeaders(HttpRequestMessage request)
    {
        var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var actorType = User.FindFirstValue("actor_type");
        if (!string.Equals(actorType, "tenant_user", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var tenantValue = User.FindFirstValue("tenant_id")
            ?? User.FindFirstValue("tenantId")
            ?? User.Claims.FirstOrDefault(claim =>
                claim.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
        if (!Guid.TryParse(tenantValue, out var tenantId))
        {
            return false;
        }

        request.Headers.Add("X-Tenant-Id", tenantId.ToString("D"));
        return true;
    }
}
