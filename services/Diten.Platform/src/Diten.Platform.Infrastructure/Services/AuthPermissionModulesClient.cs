using System.Net.Http.Json;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services;

/// <summary>
/// Best-effort S2S reader of the AuthService distinct permission-modules list. Mirrors the
/// <see cref="CatalogPermissionSyncService"/> internal-call pattern (X-Internal-Api-Key, AuthService:BaseUrl).
/// Never throws — returns an empty list (logged) when AuthService is unreachable or misconfigured.
/// </summary>
public sealed class AuthPermissionModulesClient : IAuthPermissionModulesClient
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthServiceOptions _authServiceOptions;
    private readonly ILogger<AuthPermissionModulesClient> _logger;

    public AuthPermissionModulesClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AuthServiceOptions> authServiceOptions,
        ILogger<AuthPermissionModulesClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _authServiceOptions = authServiceOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AuthPermissionModule>> GetModulesAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_authServiceOptions.BaseUrl) || string.IsNullOrWhiteSpace(_authServiceOptions.InternalApiKey))
        {
            _logger.LogWarning("Cannot read AuthService permission modules; AuthService BaseUrl/InternalApiKey not configured.");
            return [];
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_authServiceOptions.BaseUrl.TrimEnd('/')}/internal/permissions/modules");
            request.Headers.Add(InternalApiKeyHeader, _authServiceOptions.InternalApiKey);

            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AuthService permission modules request failed. StatusCode={StatusCode}",
                    (int)response.StatusCode);
                return [];
            }

            var modules = await response.Content.ReadFromJsonAsync<List<AuthPermissionModule>>(cancellationToken: ct);
            return modules ?? [];
        }
        catch (Exception ex)
        {
            // Best-effort: the Module Catalog form must still render (tags fallback) when auth is down.
            _logger.LogWarning(ex, "AuthService permission modules request threw; returning empty list.");
            return [];
        }
    }
}
