using System.Net.Http.Json;
using System.Text.Json;
using Diten.HcmService.Application.Features.CoreHrEmployeeMaster;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Diten.HcmService.Infrastructure.ReferenceValidation;

public sealed class GatewayReferenceValidationClient : IReferenceValidationClient
{
    private const string TenantHeaderName = "X-Tenant-Id";
    private const string AuthorizationHeaderName = "Authorization";
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<GatewayReferenceValidationClient> _logger;

    public GatewayReferenceValidationClient(
        HttpClient httpClient,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor,
        ILogger<GatewayReferenceValidationClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;

        var gatewayBaseUrl = configuration["Gateway:BaseUrl"] ?? "http://localhost:5000";
        _httpClient.BaseAddress = new Uri(gatewayBaseUrl);
    }

    public Task<ReferenceValidationItem> ValidatePersonAsync(string? personId, CancellationToken cancellationToken)
        => ValidatePostAsync("person", personId, "MOD-0288", "/api/v1/platform/persons/lookup-validation", cancellationToken);

    public Task<ReferenceValidationItem> ValidateOrganizationUnitAsync(string? organizationUnitId, CancellationToken cancellationToken)
        => ValidateGetAsync("organization_unit", organizationUnitId, "MOD-0288", $"/api/platform/organization-units/{organizationUnitId}", cancellationToken);

    public Task<ReferenceValidationItem> ValidatePositionAsync(string? positionId, CancellationToken cancellationToken)
        => ValidateGetAsync("position", positionId, "MOD-0288", $"/api/platform/positions/{positionId}", cancellationToken);

    public Task<ReferenceValidationItem> ValidateLegalEntityAsync(string? legalEntityId, CancellationToken cancellationToken)
        => ValidateGetAsync("legal_entity", legalEntityId, "MDM", $"/api/legal-entities/{legalEntityId}/lookup-validation", cancellationToken);

    private async Task<ReferenceValidationItem> ValidateGetAsync(
        string referenceType,
        string? referenceId,
        string provider,
        string path,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(referenceId))
        {
            return Missing(referenceType, provider);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            ForwardContextHeaders(request);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return await ToValidationItemAsync(referenceType, referenceId, provider, response, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Reference validation dependency unavailable for {ReferenceType}.", referenceType);
            return Unavailable(referenceType, referenceId, provider);
        }
    }

    private async Task<ReferenceValidationItem> ValidatePostAsync(
        string referenceType,
        string? referenceId,
        string provider,
        string path,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(referenceId))
        {
            return Missing(referenceType, provider);
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(new { personIds = new[] { referenceId } })
            };
            ForwardContextHeaders(request);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            return await ToValidationItemAsync(referenceType, referenceId, provider, response, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Reference validation dependency unavailable for {ReferenceType}.", referenceType);
            return Unavailable(referenceType, referenceId, provider);
        }
    }

    private void ForwardContextHeaders(HttpRequestMessage request)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        if (httpContext.Request.Headers.TryGetValue(TenantHeaderName, out var tenantHeader))
        {
            request.Headers.TryAddWithoutValidation(TenantHeaderName, tenantHeader.ToArray());
        }

        if (httpContext.Request.Headers.TryGetValue(AuthorizationHeaderName, out var authorizationHeader))
        {
            request.Headers.TryAddWithoutValidation(AuthorizationHeaderName, authorizationHeader.ToArray());
        }
    }

    private static async Task<ReferenceValidationItem> ToValidationItemAsync(
        string referenceType,
        string referenceId,
        string provider,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var metadata = await TryReadSafeMetadataAsync(response, cancellationToken);
            return new ReferenceValidationItem(referenceType, referenceId, "valid", true, provider, null, metadata);
        }

        var reasonCode = response.StatusCode switch
        {
            System.Net.HttpStatusCode.NotFound => "not_found_or_tenant_mismatch",
            System.Net.HttpStatusCode.Forbidden => "permission_denied",
            System.Net.HttpStatusCode.Unauthorized => "permission_denied",
            System.Net.HttpStatusCode.Gone => "stale_or_deprecated",
            _ => "provider_rejected"
        };

        return new ReferenceValidationItem(referenceType, referenceId, "blocked", false, provider, reasonCode, new Dictionary<string, string>());
    }

    private static async Task<IReadOnlyDictionary<string, string>> TryReadSafeMetadataAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AddIfPresent(document.RootElement, metadata, "displayName");
            AddIfPresent(document.RootElement, metadata, "name");
            AddIfPresent(document.RootElement, metadata, "code");
            AddIfPresent(document.RootElement, metadata, "status");
            return metadata;
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private static void AddIfPresent(JsonElement root, IDictionary<string, string> metadata, string key)
    {
        if (TryFindString(root, key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            metadata[key] = value;
        }
    }

    private static bool TryFindString(JsonElement element, string key, out string? value)
    {
        value = null;

        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(key, out var direct) && direct.ValueKind == JsonValueKind.String)
            {
                value = direct.GetString();
                return true;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (TryFindString(property.Value, key, out value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static ReferenceValidationItem Missing(string referenceType, string provider)
        => new(referenceType, string.Empty, "missing", false, provider, "missing_reference", new Dictionary<string, string>());

    private static ReferenceValidationItem Unavailable(string referenceType, string referenceId, string provider)
        => new(referenceType, referenceId, "blocked", false, provider, "dependency_unavailable", new Dictionary<string, string>());
}
