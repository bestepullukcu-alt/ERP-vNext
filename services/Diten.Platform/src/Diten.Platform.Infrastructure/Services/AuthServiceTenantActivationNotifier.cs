using System.Net.Http.Json;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services;

/// <summary>
/// FIX-ONBOARDING (B1) — posts the "tenant.activated" internal event to AuthService
/// (<c>POST /internal/events/tenant-activated</c>, X-Internal-Api-Key) so AuthService runs EnsureDefaultRoles +
/// SyncEntitledModules automatically. Mirrors <see cref="AdminUserInvitationService"/>'s S2S pattern. Best-effort:
/// any failure (AuthService down, bad config) is logged and swallowed so tenant creation is never blocked. The
/// event carries a fresh EventId — AuthService dedups by it, so retries/duplicates are harmless.
/// </summary>
public sealed class AuthServiceTenantActivationNotifier : ITenantActivationNotifier
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string EventName = "tenant.activated";
    private const string Producer = "Diten.Platform";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthServiceOptions _options;
    private readonly ILogger<AuthServiceTenantActivationNotifier> _logger;

    public AuthServiceTenantActivationNotifier(
        IHttpClientFactory httpClientFactory,
        IOptions<AuthServiceOptions> options,
        ILogger<AuthServiceTenantActivationNotifier> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task NotifyActivatedAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl) || string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            _logger.LogWarning(
                "tenant.activated notify skipped: AuthService BaseUrl/InternalApiKey not configured. TenantId={TenantId}",
                tenantId);
            return;
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            var body = new
            {
                eventId = Guid.NewGuid(),
                tenantId,
                eventName = EventName,
                eventVersion = 1,
                correlationId = Guid.NewGuid(),
                causationId = Guid.Empty,
                occurredAt = now,
                producer = Producer
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_options.BaseUrl.TrimEnd('/')}/internal/events/tenant-activated")
            {
                Content = JsonContent.Create(body)
            };
            request.Headers.Add(InternalApiKeyHeader, _options.InternalApiKey);

            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("tenant.activated notified to AuthService. TenantId={TenantId}", tenantId);
            }
            else
            {
                _logger.LogWarning(
                    "tenant.activated notify returned {StatusCode}. TenantId={TenantId}",
                    (int)response.StatusCode, tenantId);
            }
        }
        catch (Exception ex)
        {
            // Best-effort: never block tenant creation on a notify failure.
            _logger.LogWarning(ex, "tenant.activated notify failed (best-effort). TenantId={TenantId}", tenantId);
        }
    }
}
