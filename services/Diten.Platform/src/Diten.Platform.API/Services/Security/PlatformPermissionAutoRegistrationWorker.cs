using System.Globalization;
using Diten.Platform.API.Security;
using Diten.Platform.Application.Contracts;

namespace Diten.Platform.API.Services.Security;

/// <summary>
/// A1 — at startup, reflects every <c>[HasPermission]</c> key declared by this service's controllers and
/// pushes each to AuthService (S2S, idempotent upsert). New modules (workflow, doc-management, …) get their
/// permissions registered automatically — no hand-edited DataSeeder. Best-effort: AuthService being down only
/// logs a warning and never blocks startup (the underlying sync service never throws).
/// </summary>
public sealed class PlatformPermissionAutoRegistrationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PlatformPermissionAutoRegistrationWorker> _logger;

    public PlatformPermissionAutoRegistrationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PlatformPermissionAutoRegistrationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var keys = HasPermissionReflector.CollectPermissionKeys(typeof(HasPermissionAttribute).Assembly);
        if (keys.Count == 0)
        {
            _logger.LogWarning("Permission auto-registration found no [HasPermission] keys to sync.");
            return;
        }

        _logger.LogInformation("Permission auto-registration: syncing {Count} controller permission key(s) to AuthService.", keys.Count);

        using var scope = _scopeFactory.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<ICatalogPermissionSyncService>();

        var synced = 0;
        var failed = 0;
        foreach (var key in keys)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            var status = await syncService.SyncPermissionAsync(key, DeriveDisplayName(key), stoppingToken);
            if (status == CatalogPermissionSyncStatus.Synced)
            {
                synced++;
            }
            else if (status is CatalogPermissionSyncStatus.Failed or CatalogPermissionSyncStatus.InvalidFormat)
            {
                failed++;
            }
        }

        _logger.LogInformation(
            "Permission auto-registration complete. Total={Total} Synced={Synced} Failed/Invalid={Failed}.",
            keys.Count,
            synced,
            failed);
    }

    // "platform.workflow.definitions.view" → "Platform Workflow Definitions View".
    private static string DeriveDisplayName(string key)
    {
        var words = key.Replace('.', ' ').Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpper(w[0], CultureInfo.InvariantCulture) + w[1..]);
        return string.Join(' ', words);
    }
}
