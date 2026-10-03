using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Infrastructure.Services;

/// <summary>
/// BL-454 — the answers of <c>GET /api/internal/tenants/{id}/email-identity</c>, kept for a few minutes. A tenant's
/// name and language change rarely and an invitation batch asks for the same tenant repeatedly. Only answers are
/// kept: a failure is asked again next time.
/// </summary>
public sealed class TenantEmailIdentityCache
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<Guid, (DateTimeOffset ExpiresAt, TenantEmailIdentity Value)> _entries = new();
    private readonly Func<DateTimeOffset> _now;

    public TenantEmailIdentityCache() : this(() => DateTimeOffset.UtcNow) { }

    public TenantEmailIdentityCache(Func<DateTimeOffset> now) => _now = now;

    public bool TryGet(Guid tenantId, out TenantEmailIdentity? value)
    {
        if (_entries.TryGetValue(tenantId, out var entry) && entry.ExpiresAt > _now())
        {
            value = entry.Value;
            return true;
        }

        value = null;
        return false;
    }

    public void Set(Guid tenantId, TenantEmailIdentity value) => _entries[tenantId] = (_now().Add(Lifetime), value);
}

public sealed class PlatformTenantEmailIdentityClient : ITenantEmailIdentityClient
{
    /// <summary>The longest an e-mail waits for Platform. The typed HttpClient is registered with this timeout.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _httpClient;
    private readonly TenantEmailIdentityCache _cache;
    private readonly PlatformServiceOptions _options;
    private readonly ILogger<PlatformTenantEmailIdentityClient> _logger;

    public PlatformTenantEmailIdentityClient(
        HttpClient httpClient,
        TenantEmailIdentityCache cache,
        IOptions<PlatformServiceOptions> options,
        ILogger<PlatformTenantEmailIdentityClient> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TenantEmailIdentity?> GetAsync(Guid tenantId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
        {
            return null;
        }

        if (_cache.TryGet(tenantId, out var cached))
        {
            return cached;
        }

        if (string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            _logger.LogWarning("tenant.email_identity.unavailable TenantId={TenantId} Reason=InternalApiKeyMissing", tenantId);
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/internal/tenants/{tenantId:D}/email-identity");
            request.Headers.TryAddWithoutValidation(InternalApiKeyHeader, _options.InternalApiKey);

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "tenant.email_identity.unavailable TenantId={TenantId} Reason=Status StatusCode={StatusCode}",
                    tenantId, (int)response.StatusCode);
                return null;
            }

            var envelope = await response.Content.ReadFromJsonAsync<PlatformEnvelope>(JsonOptions, ct);
            if (envelope?.Data is not { } data || string.IsNullOrWhiteSpace(data.DisplayName))
            {
                _logger.LogWarning("tenant.email_identity.unavailable TenantId={TenantId} Reason=EmptyAnswer", tenantId);
                return null;
            }

            var identity = new TenantEmailIdentity(
                data.DisplayName.Trim(),
                string.IsNullOrWhiteSpace(data.Language) ? "en" : data.Language.Trim(),
                string.IsNullOrWhiteSpace(data.SenderName) ? null : data.SenderName.Trim(),
                string.IsNullOrWhiteSpace(data.ReplyToEmail) ? null : data.ReplyToEmail.Trim());
            _cache.Set(tenantId, identity);
            return identity;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Timeout, refused connection, malformed answer: all the same to the e-mail, which is sent anyway.
            _logger.LogWarning(
                "tenant.email_identity.unavailable TenantId={TenantId} Reason={Reason}", tenantId, ex.GetType().Name);
            return null;
        }
    }

    /// <summary>
    /// BL-454 — the client's transport. It does NOT follow redirects: the request carries the internal API key in a
    /// custom header, and a redirect would hand that key to whatever host the answer names (HttpClient drops the
    /// Authorization header on a redirect, never a custom one). A 3xx is simply "no answer" here.
    /// </summary>
    public static HttpMessageHandler CreatePrimaryHandler() => new SocketsHttpHandler { AllowAutoRedirect = false };

    /// <summary>The one registration, used by AddInfrastructure and by the tests that prove it.</summary>
    public static IHttpClientBuilder Register(IServiceCollection services)
    {
        services.AddSingleton<TenantEmailIdentityCache>();
        return services.AddHttpClient<ITenantEmailIdentityClient, PlatformTenantEmailIdentityClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<PlatformServiceOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = Timeout;
            })
            .ConfigurePrimaryHttpMessageHandler(CreatePrimaryHandler);
    }

    private sealed record PlatformEnvelope(IdentityPayload? Data);

    private sealed record IdentityPayload(string? DisplayName, string? Language, string? SenderName, string? ReplyToEmail);
}
