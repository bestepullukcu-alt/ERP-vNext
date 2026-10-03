using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Infrastructure.Services;

public sealed class PlatformTenantLegalEntityScopeClient : ITenantLegalEntityScopeClient
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PlatformServiceOptions _options;
    private readonly ILogger<PlatformTenantLegalEntityScopeClient> _logger;

    public PlatformTenantLegalEntityScopeClient(
        HttpClient httpClient,
        IHttpContextAccessor httpContextAccessor,
        IOptions<PlatformServiceOptions> options,
        ILogger<PlatformTenantLegalEntityScopeClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Guid?> ResolveSingleAsync(Guid tenantId, Guid userId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty || string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/internal/tenants/{tenantId:D}/users/{userId:D}/legal-entity-scope");
            request.Headers.TryAddWithoutValidation(InternalApiKeyHeader, _options.InternalApiKey);
            request.Headers.TryAddWithoutValidation(
                CorrelationIdHeader,
                _httpContextAccessor.HttpContext?.Request.Headers[CorrelationIdHeader].FirstOrDefault()
                    ?? _httpContextAccessor.HttpContext?.TraceIdentifier
                    ?? Guid.NewGuid().ToString("N"));

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Legal-entity scope read failed closed. TenantId={TenantId} UserId={UserId} StatusCode={StatusCode}",
                    tenantId, userId, (int)response.StatusCode);
                return null;
            }

            var envelope = await response.Content.ReadFromJsonAsync<PlatformEnvelope<ScopeResolution>>(JsonOptions, ct);
            var resolved = envelope?.Data;
            return resolved is { Disposition: "single", LegalEntityId: { } legalEntityId }
                   && legalEntityId != Guid.Empty
                ? legalEntityId
                : null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Legal-entity scope read failed closed. TenantId={TenantId} UserId={UserId}",
                tenantId, userId);
            return null;
        }
    }

    private sealed record ScopeResolution(Guid? LegalEntityId, string Disposition);
    private sealed record PlatformEnvelope<T>(bool Succeeded, string? Message, T? Data);
}
