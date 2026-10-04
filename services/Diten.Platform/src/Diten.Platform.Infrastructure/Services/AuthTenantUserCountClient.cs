using System.Net.Http.Json;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services;

/// <summary>
/// BL-459 — <see cref="ITenantUserCountReader"/> over AuthService's internal counts endpoint. Mirrors
/// <see cref="AuthUserDisplayNameClient"/>: same X-Internal-Api-Key header, same AuthService:BaseUrl, same never-throw
/// contract. The tenant is the caller's explicit argument (a platform administrator looking at one tenant) and AuthService
/// filters by it in the query itself.
/// </summary>
public sealed class AuthTenantUserCountClient : ITenantUserCountReader
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthServiceOptions _authServiceOptions;
    private readonly ILogger<AuthTenantUserCountClient> _logger;

    public AuthTenantUserCountClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AuthServiceOptions> authServiceOptions,
        ILogger<AuthTenantUserCountClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _authServiceOptions = authServiceOptions.Value;
        _logger = logger;
    }

    public async Task<TenantUserCounts?> GetCountsAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(_authServiceOptions.BaseUrl) ||
            string.IsNullOrWhiteSpace(_authServiceOptions.InternalApiKey))
        {
            _logger.LogWarning("Cannot read tenant user counts; AuthService BaseUrl/InternalApiKey not configured.");
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_authServiceOptions.BaseUrl.TrimEnd('/')}/internal/users/counts?tenantId={tenantId:D}");
            request.Headers.Add(InternalApiKeyHeader, _authServiceOptions.InternalApiKey);

            var client = _httpClientFactory.CreateClient(InternalHttpClients.AuthInternal);
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AuthService tenant user counts failed. StatusCode={StatusCode} TenantId={TenantId}",
                    (int)response.StatusCode, tenantId);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<CountsRow>(cancellationToken: ct);
            // A body for another tenant is not an answer about this one.
            return payload is null || payload.TenantId != tenantId
                ? null
                : new TenantUserCounts(payload.Total, payload.Active, payload.Invited, payload.Inactive);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AuthService tenant user counts threw; the summary falls back to its own list. TenantId={TenantId}", tenantId);
            return null;
        }
    }

    private sealed record CountsRow(Guid TenantId, long Total, long Active, long Invited, long Inactive);
}
