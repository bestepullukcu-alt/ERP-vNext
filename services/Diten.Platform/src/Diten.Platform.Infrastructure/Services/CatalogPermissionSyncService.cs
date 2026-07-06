using System.Net.Http.Json;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModulePages;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Services;

/// <summary>
/// Best-effort S2S sync of catalog-declared permissions into the AuthService permission catalogue.
/// Mirrors the <see cref="AdminUserInvitationService"/> internal-call pattern (X-Internal-Api-Key,
/// AuthService:BaseUrl). Never throws on a sync failure — the catalog save is the system of record and
/// must succeed even when AuthService is down; failures are logged for follow-up reconciliation (Phase 1.5).
/// </summary>
public sealed class CatalogPermissionSyncService : ICatalogPermissionSyncService
{
    private const string InternalApiKeyHeader = "X-Internal-Api-Key";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AuthServiceOptions _authServiceOptions;
    private readonly ILogger<CatalogPermissionSyncService> _logger;

    public CatalogPermissionSyncService(
        IHttpClientFactory httpClientFactory,
        IOptions<AuthServiceOptions> authServiceOptions,
        ILogger<CatalogPermissionSyncService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _authServiceOptions = authServiceOptions.Value;
        _logger = logger;
    }

    public async Task<CatalogPermissionSyncStatus> SyncPermissionAsync(
        string? permissionKey, string? displayName, string? moduleCode, string? scope, CancellationToken ct)
    {
        var normalized = ModulePageDescriptorNormalizer.NormalizePermission(permissionKey);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return CatalogPermissionSyncStatus.SkippedEmpty;
        }

        if (!ModulePageDescriptorNormalizer.IsCanonicalPermissionKey(normalized))
        {
            _logger.LogWarning(
                "Skipping catalog permission sync; key is not canonical module.resource.action. PermissionKey={PermissionKey}",
                permissionKey);
            return CatalogPermissionSyncStatus.InvalidFormat;
        }

        if (string.IsNullOrWhiteSpace(_authServiceOptions.BaseUrl) || string.IsNullOrWhiteSpace(_authServiceOptions.InternalApiKey))
        {
            _logger.LogError(
                "Cannot sync catalog permission; AuthService BaseUrl/InternalApiKey not configured. PermissionKey={PermissionKey}",
                normalized);
            return CatalogPermissionSyncStatus.Failed;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_authServiceOptions.BaseUrl.TrimEnd('/')}/internal/permissions/sync")
            {
                Content = JsonContent.Create(new SyncPermissionRequest(
                    normalized,
                    string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
                    null,
                    string.IsNullOrWhiteSpace(moduleCode) ? null : moduleCode.Trim(),
                    string.IsNullOrWhiteSpace(scope) ? null : scope.Trim()))
            };
            request.Headers.Add(InternalApiKeyHeader, _authServiceOptions.InternalApiKey);

            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "Catalog permission sync failed. PermissionKey={PermissionKey} StatusCode={StatusCode} Response={Response}",
                    normalized,
                    (int)response.StatusCode,
                    responseText);
                return CatalogPermissionSyncStatus.Failed;
            }

            _logger.LogInformation("Catalog permission synced to AuthService. PermissionKey={PermissionKey}", normalized);
            return CatalogPermissionSyncStatus.Synced;
        }
        catch (Exception ex)
        {
            // Best-effort: swallow so the catalog save is never rolled back by a transient auth outage.
            _logger.LogError(
                ex,
                "Catalog permission sync threw; catalog save is unaffected. PermissionKey={PermissionKey}",
                normalized);
            return CatalogPermissionSyncStatus.Failed;
        }
    }

    public async Task<CatalogPermissionSyncStatus> RemovePermissionAsync(string? permissionKey, CancellationToken ct)
    {
        var normalized = ModulePageDescriptorNormalizer.NormalizePermission(permissionKey);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return CatalogPermissionSyncStatus.SkippedEmpty;
        }

        if (!ModulePageDescriptorNormalizer.IsCanonicalPermissionKey(normalized))
        {
            _logger.LogWarning(
                "Skipping catalog permission removal; key is not canonical module.resource.action. PermissionKey={PermissionKey}",
                permissionKey);
            return CatalogPermissionSyncStatus.InvalidFormat;
        }

        if (string.IsNullOrWhiteSpace(_authServiceOptions.BaseUrl) || string.IsNullOrWhiteSpace(_authServiceOptions.InternalApiKey))
        {
            _logger.LogError(
                "Cannot remove catalog permission; AuthService BaseUrl/InternalApiKey not configured. PermissionKey={PermissionKey}",
                normalized);
            return CatalogPermissionSyncStatus.Failed;
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Delete,
                $"{_authServiceOptions.BaseUrl.TrimEnd('/')}/internal/permissions/{Uri.EscapeDataString(normalized)}");
            request.Headers.Add(InternalApiKeyHeader, _authServiceOptions.InternalApiKey);

            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(request, ct);

            // 204 (removed) and 404 (already gone) are both success — removal is idempotent. Anything else (incl. 409
            // for a protected system permission) is a non-success: logged, catalog delete unaffected.
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Catalog permission removed from AuthService. PermissionKey={PermissionKey}", normalized);
                return CatalogPermissionSyncStatus.Removed;
            }

            var responseText = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Catalog permission removal failed. PermissionKey={PermissionKey} StatusCode={StatusCode} Response={Response}",
                normalized,
                (int)response.StatusCode,
                responseText);
            return CatalogPermissionSyncStatus.Failed;
        }
        catch (Exception ex)
        {
            // Best-effort: swallow so the catalog delete is never rolled back by a transient auth outage.
            _logger.LogError(
                ex,
                "Catalog permission removal threw; catalog delete is unaffected. PermissionKey={PermissionKey}",
                normalized);
            return CatalogPermissionSyncStatus.Failed;
        }
    }

    private sealed record SyncPermissionRequest(
        string PermissionKey, string? DisplayName, string? Description, string? ModuleCode, string? Scope);
}
