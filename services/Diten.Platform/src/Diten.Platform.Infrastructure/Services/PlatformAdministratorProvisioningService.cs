using System.Net.Http.Json;
using System.Text.Json;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services;

public sealed class PlatformAdministratorProvisioningService : IPlatformAdministratorProvisioningService
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";
    private const string TenantHeader = "X-Tenant-Id";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthServiceOptions _authServiceOptions;
    private readonly ILogger<PlatformAdministratorProvisioningService> _logger;

    public PlatformAdministratorProvisioningService(
        IHttpClientFactory httpClientFactory,
        IOptions<AuthServiceOptions> authServiceOptions,
        ILogger<PlatformAdministratorProvisioningService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _authServiceOptions = authServiceOptions.Value;
        _logger = logger;
    }

    public async Task<PlatformAdministratorProvisioningResult> ProvisionAsync(PlatformAdministratorProvisioningRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_authServiceOptions.BaseUrl))
        {
            throw new InvalidOperationException("AuthService:BaseUrl configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(_authServiceOptions.InternalApiKey))
        {
            throw new InvalidOperationException("AuthService:InternalApiKey configuration is required.");
        }

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_authServiceOptions.BaseUrl.TrimEnd('/')}/api/platform-auth/platform-admins/provision")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(InternalApiKeyHeader, _authServiceOptions.InternalApiKey);
        AddPlatformTenant(message);

        return await SendInternalRequestAsync(message, request.AdministratorId, ct);
    }

    /// <summary>
    /// BL-521 FIX3 — Auth's tenant resolution answers these two routes 400 "Missing Tenant" without a tenant (they are not
    /// on its public or internal bypass lists); only the Development bypass ever supplied one. Every platform administrator
    /// account lives in the platform system tenant, which is what both Auth handlers address, so it is named on the wire.
    /// Since FIX3 the update syncs Auth BEFORE writing the record, so without this no update could be saved outside
    /// Development.
    /// </summary>
    private static void AddPlatformTenant(HttpRequestMessage message) =>
        message.Headers.Add(TenantHeader, SystemTenantRules.PlatformSystemTenantId.ToString("D"));

    public async Task SyncAsync(PlatformAdministratorProvisioningSyncRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_authServiceOptions.BaseUrl))
        {
            throw new InvalidOperationException("AuthService:BaseUrl configuration is required.");
        }

        if (string.IsNullOrWhiteSpace(_authServiceOptions.InternalApiKey))
        {
            throw new InvalidOperationException("AuthService:InternalApiKey configuration is required.");
        }

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_authServiceOptions.BaseUrl.TrimEnd('/')}/api/platform-auth/platform-admins/sync")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add(InternalApiKeyHeader, _authServiceOptions.InternalApiKey);
        AddPlatformTenant(message);

        await SendInternalRequestAsync(message, request.AdministratorId, ct);
    }

    private async Task<PlatformAdministratorProvisioningResult> SendInternalRequestAsync(HttpRequestMessage message, Guid administratorId, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        using var response = await client.SendAsync(message, ct);
        if (response.IsSuccessStatusCode)
        {
            return await ReadProvisioningResultAsync(response, ct);
        }

        // BL-521 FIX5 item 6 — THE FAILURE SAYS WHO, WHAT AND WHY, NEVER AUTH'S REPLY. It used to log Email={Email} and Auth's
        // whole body at Error and to throw Auth's own message, which Platform's exception handler logs again and puts in the
        // problem+json it answers with: a validation or conflict reply can name the address, a username, a phone number.
        // Now: the administrator's id, the status code and a reason code derived from the status; Auth's body is not even
        // read. The exception carries the same two and says whether Auth gave no answer at all (item 10).
        var statusCode = (int)response.StatusCode;
        var reasonCode = PlatformAdministratorAuthCallException.ReasonCodeOf(response.StatusCode);
        _logger.LogError(
            "Platform administrator AuthService call failed. AdministratorId={AdministratorId} StatusCode={StatusCode} ReasonCode={ReasonCode}",
            administratorId,
            statusCode,
            reasonCode);

        throw new PlatformAdministratorAuthCallException(response.StatusCode);
    }

    private static async Task<PlatformAdministratorProvisioningResult> ReadProvisioningResultAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var raw = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new PlatformAdministratorProvisioningResult(null, true);
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            var setupUrl = root.TryGetProperty("setupUrl", out var setupUrlProp) ? setupUrlProp.GetString() : null;
            var emailSent = !root.TryGetProperty("emailSent", out var emailSentProp) || emailSentProp.GetBoolean();
            return new PlatformAdministratorProvisioningResult(setupUrl, emailSent);
        }
        catch
        {
            return new PlatformAdministratorProvisioningResult(null, true);
        }
    }
}
