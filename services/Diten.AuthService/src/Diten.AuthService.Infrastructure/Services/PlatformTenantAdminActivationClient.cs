using System.Net.Http.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Infrastructure.Services;

/// <summary>
/// FIX-TENANT-ADMIN-INVITE-ACTIVATION (Part B) — posts the tenant-admin activation callback to Platform. Mirrors
/// <see cref="PlatformAdministratorStatusClient.MarkLoginAcceptedAsync"/>: best-effort (never throws), gated by the
/// shared X-Internal-Api-Key, correlation-id propagated.
/// </summary>
public sealed class PlatformTenantAdminActivationClient : ITenantAdminActivationClient
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string CorrelationIdHeader = "X-Correlation-Id";

    private readonly HttpClient _httpClient;
    private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;
    private readonly PlatformServiceOptions _options;
    private readonly ILogger<PlatformTenantAdminActivationClient> _logger;

    public PlatformTenantAdminActivationClient(
        HttpClient httpClient,
        Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
        IOptions<PlatformServiceOptions> options,
        ILogger<PlatformTenantAdminActivationClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
        _logger = logger;
    }

    public async Task NotifyActivatedAsync(string email, Guid tenantId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email) || tenantId == Guid.Empty || string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            return;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/internal/tenants/admin-activated")
        {
            Content = JsonContent.Create(new { email = normalizedEmail, tenantId })
        };
        request.Headers.TryAddWithoutValidation(InternalApiKeyHeader, _options.InternalApiKey);
        request.Headers.TryAddWithoutValidation(CorrelationIdHeader, ResolveCorrelationId());

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Tenant admin activation callback failed. Email={Email} TenantId={TenantId} StatusCode={StatusCode}",
                    normalizedEmail,
                    tenantId,
                    (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            // Best-effort: Platform being down must not fail the password change.
            _logger.LogWarning(ex, "Tenant admin activation callback threw. Email={Email} TenantId={TenantId}", normalizedEmail, tenantId);
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
}
