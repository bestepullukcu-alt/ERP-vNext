using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Infrastructure.Services;

public sealed class PlatformAdministratorStatusClient : IPlatformAdministratorStatusClient
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
    private readonly ILogger<PlatformAdministratorStatusClient> _logger;

    public PlatformAdministratorStatusClient(
        HttpClient httpClient,
        Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
        IOptions<PlatformServiceOptions> options,
        ILogger<PlatformAdministratorStatusClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> IsActiveAsync(string email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            _logger.LogError("Platform internal API key is not configured.");
            return false;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/internal/platform-administrators/status?email={Uri.EscapeDataString(normalizedEmail)}");
        request.Headers.TryAddWithoutValidation(InternalApiKeyHeader, _options.InternalApiKey);
        request.Headers.TryAddWithoutValidation(CorrelationIdHeader, ResolveCorrelationId());

        using var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Platform administrator status read failed. Email={Email} StatusCode={StatusCode}",
                normalizedEmail,
                (int)response.StatusCode);
            return false;
        }

        var envelope = await response.Content.ReadFromJsonAsync<PlatformEnvelope<PlatformAdministratorStatusSnapshot>>(JsonOptions, ct);
        return envelope?.Data?.IsActive == true;
    }

    public async Task MarkLoginAcceptedAsync(string email, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            return;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/internal/platform-administrators/accept-login")
        {
            Content = JsonContent.Create(new { email = normalizedEmail })
        };
        request.Headers.TryAddWithoutValidation(InternalApiKeyHeader, _options.InternalApiKey);
        request.Headers.TryAddWithoutValidation(CorrelationIdHeader, ResolveCorrelationId());

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Platform administrator login acceptance update failed. Email={Email} StatusCode={StatusCode}",
                    normalizedEmail,
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Platform administrator login acceptance update failed. Email={Email}",
                normalizedEmail);
        }
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

    private sealed record PlatformEnvelope<T>(bool IsSuccessful, int StatusCode, T? Data);
    private sealed record PlatformAdministratorStatusSnapshot(Guid Id, string Email, string Status, bool IsDeleted, bool IsActive);
}
