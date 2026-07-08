using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.Platform;

[Authorize]
public sealed class PersonReferencesController : Controller
{
    private const string PersonSearchPermission = "platform.person.search";
    private const string PersonViewPermission = "platform.person.view";
    private const string PersonLookupValidationPermission = "platform.person.lookup_validation";
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string CorrelationHeaderName = "X-Correlation-Id";
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _gatewayUrl;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<PersonReferencesController> _logger;

    public PersonReferencesController(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<PersonReferencesController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _gatewayUrl = configuration["GatewayUrl"]?.TrimEnd('/') ?? "http://localhost:5000";
        _environment = environment;
        _logger = logger;
    }

    [HttpGet("/Platform/PersonReferences/api")]
    public Task<IActionResult> Search(
        [FromQuery] string? query,
        [FromQuery] string? status,
        [FromQuery] bool? referenceable,
        [FromQuery] int page = DefaultPage,
        [FromQuery] int pageSize = DefaultPageSize)
    {
        if (!HasPermission(PersonSearchPermission))
        {
            return Task.FromResult<IActionResult>(Forbid());
        }

        var normalizedPage = Math.Max(DefaultPage, page);
        var normalizedPageSize = Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, MaxPageSize);
        var normalizedStatus = !string.IsNullOrWhiteSpace(status)
            ? status.Trim()
            : referenceable == true
                ? "Active"
                : null;

        var parameters = new Dictionary<string, string?>
        {
            ["query"] = string.IsNullOrWhiteSpace(query) ? null : query.Trim(),
            ["status"] = normalizedStatus,
            ["page"] = normalizedPage.ToString(),
            ["pageSize"] = normalizedPageSize.ToString()
        };

        var targetUrl = $"{_gatewayUrl}/api/v1/platform/persons{BuildQuery(parameters)}";
        return ProxyJsonGatewayAsync(HttpMethod.Get, targetUrl, readBody: false);
    }

    [HttpGet("/Platform/PersonReferences/api/{personId:guid}")]
    public Task<IActionResult> GetById(Guid personId)
    {
        if (!HasPermission(PersonViewPermission))
        {
            return Task.FromResult<IActionResult>(Forbid());
        }

        var targetUrl = $"{_gatewayUrl}/api/v1/platform/persons/{personId:D}";
        return ProxyJsonGatewayAsync(HttpMethod.Get, targetUrl, readBody: false);
    }

    [HttpPost("/Platform/PersonReferences/api/lookup-validation")]
    public Task<IActionResult> LookupValidation()
    {
        if (!HasPermission(PersonLookupValidationPermission))
        {
            return Task.FromResult<IActionResult>(Forbid());
        }

        var targetUrl = $"{_gatewayUrl}/api/v1/platform/persons/lookup-validation";
        return ProxyJsonGatewayAsync(HttpMethod.Post, targetUrl, readBody: true);
    }

    [HttpGet("/Platform/PersonReferences/api/lookup-validation/diagnostic")]
    public async Task<IActionResult> LookupValidationDiagnostic([FromQuery] Guid personId)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (!HasPermission(PersonLookupValidationPermission))
        {
            return Forbid();
        }

        var targetUrl = $"{_gatewayUrl}/api/v1/platform/persons/lookup-validation";
        if (!TryCreateTenantRequest(HttpMethod.Post, targetUrl, out var request))
        {
            return Unauthorized(new { message = "Unauthorized" });
        }

        try
        {
            using (request)
            {
                request.Content = new StringContent(
                    JsonSerializer.Serialize(new { personIds = new[] { personId.ToString("D") } }),
                    Encoding.UTF8,
                    "application/json");

                var client = _httpClientFactory.CreateClient();
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
                var content = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);
                return StatusCode(StatusCodes.Status200OK, CreateDiagnosticShape(response, content));
            }
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Person reference diagnostic proxy failed for {TargetUrl}.", targetUrl);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                proxyStatus = StatusCodes.Status503ServiceUnavailable,
                topLevelKeys = Array.Empty<string>(),
                resultCount = 0,
                referenceable = (bool?)null,
                errorCode = "dependency_unavailable",
                errorMessage = "Person reference dependency unavailable."
            });
        }
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
            _logger.LogError(ex, "Person reference proxy failed for {Method} {TargetUrl}.", method, targetUrl);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Person reference dependency unavailable." });
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

    private static object CreateDiagnosticShape(HttpResponseMessage response, string content)
    {
        var statusCode = (int)response.StatusCode;
        if (string.IsNullOrWhiteSpace(content))
        {
            return new
            {
                proxyStatus = statusCode,
                topLevelKeys = Array.Empty<string>(),
                resultCount = 0,
                referenceable = (bool?)null,
                errorCode = (string?)null,
                errorMessage = (string?)null
            };
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var data = UnwrapDiagnosticData(root);
            var results = TryGetProperty(data, "results", out var lowerResults)
                ? lowerResults
                : TryGetProperty(data, "Results", out var upperResults)
                    ? upperResults
                    : default;

            var resultCount = results.ValueKind == JsonValueKind.Array ? results.GetArrayLength() : 0;
            bool? referenceable = null;
            if (resultCount > 0)
            {
                var first = results[0];
                referenceable = TryReadBoolean(first, "referenceable")
                    ?? TryReadBoolean(first, "Referenceable")
                    ?? TryReadBoolean(first, "isReferenceable")
                    ?? TryReadBoolean(first, "IsReferenceable");
            }

            return new
            {
                proxyStatus = statusCode,
                topLevelKeys = root.ValueKind == JsonValueKind.Object
                    ? root.EnumerateObject().Select(property => property.Name).ToArray()
                    : Array.Empty<string>(),
                resultCount,
                referenceable,
                errorCode = ReadString(root, "code") ?? ReadString(root, "Code"),
                errorMessage = ReadString(root, "message") ?? ReadString(root, "Message") ?? ReadString(root, "title") ?? ReadString(root, "Title")
            };
        }
        catch (JsonException)
        {
            return new
            {
                proxyStatus = statusCode,
                topLevelKeys = new[] { "non_json_text" },
                resultCount = 0,
                referenceable = (bool?)null,
                errorCode = "non_json_response",
                errorMessage = (string?)null
            };
        }
    }

    private static JsonElement UnwrapDiagnosticData(JsonElement root)
    {
        if (TryGetProperty(root, "data", out var data))
        {
            if (TryGetProperty(data, "data", out var nestedData))
            {
                return nestedData;
            }

            return data;
        }

        if (TryGetProperty(root, "Data", out var upperData))
        {
            if (TryGetProperty(upperData, "Data", out var nestedUpperData))
            {
                return nestedUpperData;
            }

            return upperData;
        }

        return root;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out property))
        {
            return true;
        }

        property = default;
        return false;
    }

    private static bool? TryReadBoolean(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(property.GetString(), out var value) => value,
            _ => null
        };
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
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
