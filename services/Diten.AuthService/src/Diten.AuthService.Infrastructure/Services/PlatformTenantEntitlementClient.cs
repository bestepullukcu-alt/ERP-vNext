using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Infrastructure.Services;

/// <summary>
/// Reads a tenant's effective entitled module codes from Platform's internal S2S endpoint
/// (<c>GET /api/internal/tenants/{id}/entitled-modules</c>, X-Internal-Api-Key). Mirrors
/// <see cref="PlatformTenantLoginSettingsClient"/>. On any failure it returns an EMPTY list (best-effort) so the
/// caller can skip the reconcile without breaking provisioning/login.
/// </summary>
public sealed class PlatformTenantEntitlementClient : ITenantEntitlementClient
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
    private readonly ILogger<PlatformTenantEntitlementClient> _logger;

    public PlatformTenantEntitlementClient(
        HttpClient httpClient,
        Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
        IOptions<PlatformServiceOptions> options,
        ILogger<PlatformTenantEntitlementClient> logger)
    {
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> GetEntitledModuleCodesAsync(Guid tenantId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            _logger.LogWarning("Tenant entitlement read skipped: Platform internal API key is not configured.");
            return Array.Empty<string>();
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/internal/tenants/{tenantId:D}/entitled-modules");
            request.Headers.TryAddWithoutValidation(InternalApiKeyHeader, _options.InternalApiKey);
            request.Headers.TryAddWithoutValidation(CorrelationIdHeader, ResolveCorrelationId());

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Tenant entitled-modules read failed. TenantId={TenantId} StatusCode={StatusCode}",
                    tenantId,
                    (int)response.StatusCode);
                return Array.Empty<string>();
            }

            var envelope = await response.Content.ReadFromJsonAsync<PlatformEnvelope<List<string>>>(JsonOptions, ct);
            return envelope?.Data ?? (IReadOnlyList<string>)Array.Empty<string>();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
                                   || ex is TaskCanceledException && !ct.IsCancellationRequested)
        {
            // Best-effort: Platform unreachable / slow / malformed → skip the reconcile, never throw.
            _logger.LogWarning(ex, "Tenant entitled-modules read errored. TenantId={TenantId}", tenantId);
            return Array.Empty<string>();
        }
    }

    public async Task<IReadOnlyList<EntitledModulePermissionKeys>> GetEntitledModulesWithPermissionKeysAsync(Guid tenantId, CancellationToken ct)
    {
        var result = await ReadEntitledModulesWithPermissionKeysAsync(tenantId, ct);
        return result.IsAuthoritative ? result.Modules : Array.Empty<EntitledModulePermissionKeys>();
    }

    public async Task<TenantEntitlementReadResult> ReadEntitledModulesWithPermissionKeysAsync(Guid tenantId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.InternalApiKey))
        {
            _logger.LogWarning("Tenant entitlement (with-permissions) read skipped: Platform internal API key is not configured.");
            return TenantEntitlementReadResult.Unavailable();
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/internal/tenants/{tenantId:D}/entitled-modules-with-permissions");
            request.Headers.TryAddWithoutValidation(InternalApiKeyHeader, _options.InternalApiKey);
            request.Headers.TryAddWithoutValidation(CorrelationIdHeader, ResolveCorrelationId());

            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Tenant entitled-modules-with-permissions read failed. TenantId={TenantId} StatusCode={StatusCode}",
                    tenantId,
                    (int)response.StatusCode);
                return TenantEntitlementReadResult.Unavailable();
            }

            var envelope = await response.Content.ReadFromJsonAsync<PlatformEnvelope<List<EntitledModulePermissionsRow>>>(JsonOptions, ct);
            if (envelope is null || !envelope.IsSuccessful || envelope.Data is null)
            {
                _logger.LogWarning(
                    "Tenant entitled-modules-with-permissions response was not authoritative. TenantId={TenantId}",
                    tenantId);
                return TenantEntitlementReadResult.Unavailable();
            }

            if (envelope.Data.Any(r => string.IsNullOrWhiteSpace(r.ModuleCode)))
            {
                _logger.LogWarning(
                    "Tenant entitled-modules-with-permissions response contained an invalid module row. TenantId={TenantId}",
                    tenantId);
                return TenantEntitlementReadResult.Unavailable();
            }

            var modules = envelope.Data
                .Where(r => !string.IsNullOrWhiteSpace(r.ModuleCode))
                .Select(r => new EntitledModulePermissionKeys(
                    r.ModuleCode!,
                    (IReadOnlyList<string>)(r.PermissionKeys ?? new List<string>())))
                .ToList();

            return TenantEntitlementReadResult.Confirmed(modules);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
                                   || ex is TaskCanceledException && !ct.IsCancellationRequested)
        {
            // Best-effort: Platform unreachable / slow / malformed → skip the reconcile (or fall back), never throw.
            _logger.LogWarning(ex, "Tenant entitled-modules-with-permissions read errored. TenantId={TenantId}", tenantId);
            return TenantEntitlementReadResult.Unavailable();
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

    // Platform returns Response<T> as { data, isSuccessful, statusCode, errors } — only Data is needed here.
    private sealed record PlatformEnvelope<T>(T? Data, bool IsSuccessful);

    // Mirror of Platform's TenantEntitledModulePermissionsDto (case-insensitive binding).
    private sealed record EntitledModulePermissionsRow(string? ModuleCode, List<string>? PermissionKeys);
}
