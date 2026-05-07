using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Infrastructure.Services;

public sealed class PlatformTenantLoginSettingsClient : ITenantLoginSettingsClient
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string CorrelationIdHeader = "X-Correlation-Id";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;
    private readonly PlatformServiceOptions _options;
    private readonly ILogger<PlatformTenantLoginSettingsClient> _logger;

    public PlatformTenantLoginSettingsClient(
        HttpClient httpClient,
        Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
        IOptions<PlatformServiceOptions> options,
        ILogger<PlatformTenantLoginSettingsClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TenantLoginSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            _logger.LogError("Platform internal API key is not configured.");
            throw new InvalidOperationException("Tenant login settings are unavailable.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/internal/tenants/{tenantId:D}/login-settings");
        request.Headers.TryAddWithoutValidation(InternalApiKeyHeader, _options.InternalApiKey);
        request.Headers.TryAddWithoutValidation(CorrelationIdHeader, ResolveCorrelationId());

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Tenant login settings read failed. TenantId={TenantId} StatusCode={StatusCode}",
                tenantId,
                (int)response.StatusCode);
            throw new InvalidOperationException("Tenant login settings are unavailable.");
        }

        var envelope = await response.Content.ReadFromJsonAsync<PlatformEnvelope<TenantLoginSettingsSnapshot>>(JsonOptions, ct);
        if (envelope?.Data is null)
        {
            _logger.LogWarning("Tenant login settings response was empty. TenantId={TenantId}", tenantId);
            throw new InvalidOperationException("Tenant login settings are unavailable.");
        }

        return envelope.Data;
    }

    private string ResolveCorrelationId()
    {
        var context = _httpContextAccessor.HttpContext;
        var existing = context?.Request.Headers[CorrelationIdHeader].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        return context?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
    }

    private sealed record PlatformEnvelope<T>(bool Succeeded, string? Message, T? Data);
}
