using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Diten.Web.Models.MasterData;
using Diten.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace Diten.Web.Controllers.MasterData;

/// <summary>
/// MOD-0290-FU02 — shared Gateway plumbing for the Master Data surfaces so the Brands and Products controllers
/// cannot drift apart on auth, tenancy or error handling.
///
/// Every business call leaves from the SERVER through Gateway 5000. The browser never sees the MdmService port
/// (5059), a bearer token, or a raw service URL: the HttpOnly access-token cookie is read server-side here and
/// forwarded as an Authorization header.
/// </summary>
public abstract class MasterDataGatewayController : Controller
{
    protected const string ContractPath = "/api/mdm/brand-products/contract";

    private readonly HttpClient _httpClient;
    private readonly string _gatewayUrl;
    private readonly ILogger _logger;

    protected readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    protected MasterDataGatewayController(HttpClient httpClient, IConfiguration configuration, ILogger logger)
    {
        _httpClient = httpClient;
        _gatewayUrl = configuration["GatewayUrl"]
            ?? throw new InvalidOperationException("GatewayUrl configuration is required.");
        _logger = logger;
    }

    protected async Task<BrandProductContractViewModel?> LoadContractAsync(CancellationToken cancellationToken)
    {
        var response = await SendGatewayAsync(HttpMethod.Get, ContractPath, null, cancellationToken);
        if (response is null || !response.IsSuccessStatusCode)
        {
            return null;
        }

        return (await response.Content
            .ReadFromJsonAsync<BrandProductGatewayResponse<BrandProductContractViewModel>>(JsonOptions, cancellationToken))?.Data;
    }

    protected async Task<HttpResponseMessage?> SendGatewayAsync(
        HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, $"{_gatewayUrl}{path}");

            var token = Diten.Web.Services.Auth.AuthTokenCookies.GetAccessToken(Request);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // MdmService fails closed without a tenant, so an unresolved tenant is short-circuited here rather
            // than sent as an anonymous-looking request.
            var tenantId = GetTenantId();
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return null;
            }

            request.Headers.TryAddWithoutValidation("X-Tenant-Id", tenantId);

            if (body is not null)
            {
                var json = body is JsonElement element ? element.GetRawText() : JsonSerializer.Serialize(body, JsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            // The Authorization header is deliberately never logged.
            _logger.LogError(ex, "Master Data Gateway request failed: {Method} {Path}", method, path);
            return null;
        }
    }

    protected async Task<IActionResult> ProxyGetAsync(string path, string permission, CancellationToken cancellationToken, params string[] alternatives)
    {
        if (RequireJson(permission, alternatives) is { } denied)
        {
            return denied;
        }

        return await ToProxyResultAsync(await SendGatewayAsync(HttpMethod.Get, path, null, cancellationToken), cancellationToken);
    }

    protected async Task<IActionResult> ProxyJsonAsync(
        HttpMethod method, string path, JsonElement? body, string permission, CancellationToken cancellationToken, params string[] alternatives)
    {
        if (RequireJson(permission, alternatives) is { } denied)
        {
            return denied;
        }

        // Belt-and-braces: even though no view model carries TenantId, a hand-crafted payload is refused here
        // rather than forwarded for the backend to ignore.
        if (body.HasValue && ContainsTenantId(body.Value))
        {
            return BadRequest(new { errors = new[] { "TenantId is server-resolved and must not be supplied." } });
        }

        return await ToProxyResultAsync(await SendGatewayAsync(method, path, body, cancellationToken), cancellationToken);
    }

    protected static async Task<IActionResult> ToProxyResultAsync(HttpResponseMessage? response, CancellationToken cancellationToken)
    {
        if (response is null)
        {
            return new ObjectResult(new { errors = new[] { "Gateway unavailable." } }) { StatusCode = 502 };
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return new ContentResult
        {
            StatusCode = (int)response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json",
            Content = content
        };
    }

    /// <summary>Surfaces backend reason codes verbatim so the operator sees why a write was refused.</summary>
    protected async Task<List<string>> ExtractErrorsAsync(HttpResponseMessage? response, string fallbackMessage, CancellationToken cancellationToken)
    {
        if (response is null)
        {
            return [fallbackMessage];
        }

        try
        {
            var envelope = await response.Content
                .ReadFromJsonAsync<BrandProductGatewayResponse<object>>(JsonOptions, cancellationToken);
            if (envelope?.Errors.Count > 0)
            {
                return envelope.Errors;
            }
        }
        catch
        {
            // Non-envelope payload (proxy error page, plain text) — fall through to the raw body.
        }

        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        return [string.IsNullOrWhiteSpace(raw) ? fallbackMessage : raw];
    }

    protected void AddGatewayErrors(IEnumerable<string> errors)
    {
        foreach (var error in errors)
        {
            ModelState.AddModelError(string.Empty, error);
        }
    }

    protected static void NormalizeExternalReferences(List<BrandProductExternalReferenceViewModel> references)
        => references.RemoveAll(x => string.IsNullOrWhiteSpace(x.SourceSystem) && string.IsNullOrWhiteSpace(x.ExternalId));

    protected static object? ToExternalReferencePayload(List<BrandProductExternalReferenceViewModel> references)
        => references.Count == 0
            ? null
            : references.Select(x => new
            {
                x.SourceSystem,
                x.ExternalId,
                x.ExternalCode,
                x.ExternalName,
                x.ImportedAt,
                x.IsPrimary
            }).ToList();

    protected bool HasAnyPermission(params string[] permissions)
        => permissions.Any(x => PermissionClaims.HasPermission(User, x));

    protected IActionResult? RequirePage(string permission, params string[] alternatives)
        => HasAnyPermission([permission, .. alternatives]) ? null : StatusCode(StatusCodes.Status403Forbidden);

    protected IActionResult? RequireJson(string permission, params string[] alternatives)
        => HasAnyPermission([permission, .. alternatives])
            ? null
            : StatusCode(StatusCodes.Status403Forbidden, new { message = "Permission denied." });

    private static bool ContainsTenantId(JsonElement element)
        => element.ValueKind == JsonValueKind.Object
           && element.EnumerateObject().Any(x => string.Equals(x.Name, "tenantId", StringComparison.OrdinalIgnoreCase));

    private string? GetTenantId() => User.Claims.FirstOrDefault(x =>
        x.Type == "tenantId" || x.Type == "tenant_id" ||
        x.Type.EndsWith("/tenantId", StringComparison.OrdinalIgnoreCase))?.Value;
}
