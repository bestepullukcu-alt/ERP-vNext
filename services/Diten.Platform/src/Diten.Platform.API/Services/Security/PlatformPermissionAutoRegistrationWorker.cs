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
    private readonly Diten.Platform.API.Services.ModuleRegistration.ModuleSelfRegistrationGate _selfRegistrationGate;
    private readonly ILogger<PlatformPermissionAutoRegistrationWorker> _logger;

    public PlatformPermissionAutoRegistrationWorker(
        IServiceScopeFactory scopeFactory,
        Diten.Platform.API.Services.ModuleRegistration.ModuleSelfRegistrationGate selfRegistrationGate,
        ILogger<PlatformPermissionAutoRegistrationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _selfRegistrationGate = selfRegistrationGate;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // ORDERING GUARANTEE — wait for module self-registration to FINISH before syncing anything. The manifest
        // reconcile owns permission attribution (ModuleCode + route-derived Scope); this worker deliberately sends
        // null/null, so if it reached a key first the key would be stamped Module="platform" + Scope=PlatformAdmin,
        // which AuthService can never downgrade back to Tenant. A real completion signal is used rather than a
        // delay, because a delay is just a slower race. See ModuleSelfRegistrationGate.
        var selfRegistrationCompleted = await _selfRegistrationGate.WaitForCompletionAsync(
            Diten.Platform.API.Services.ModuleRegistration.ModuleSelfRegistrationGate.DefaultWaitTimeout,
            stoppingToken);

        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        if (!selfRegistrationCompleted)
        {
            // Fail-safe: never block permission registration forever. A key that exists with imperfect attribution
            // still beats a missing key (a missing key means the endpoint 403s for everyone).
            _logger.LogWarning(
                "Module self-registration did not signal completion within {Timeout}; proceeding with permission "
                + "auto-registration anyway. Newly created keys may be attributed to Module=\"platform\" with "
                + "PlatformAdmin scope and need manual reconciliation.",
                Diten.Platform.API.Services.ModuleRegistration.ModuleSelfRegistrationGate.DefaultWaitTimeout);
        }

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

            // İŞ3-FAZ1b — this A1 worker only ensures the KEY exists (from [HasPermission] controller attributes); it
            // does not own Module/Scope attribution (that comes from the manifest sync + seed). Pass null/null so an
            // existing permission's Module/Scope is left untouched, and a brand-new platform.* key defaults to
            // Module="platform" + PlatformAdmin via the ctor.
            var status = await syncService.SyncPermissionAsync(key, DeriveDisplayName(key), null, null, stoppingToken);
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

    // FIX-RBAC-PERM-MODULE-ATTRIBUTION — "platform.workflow.definitions.view" → "Definitions View", not
    // "Platform Workflow Definitions View". The old form spelled the whole key out word by word, so every row on the
    // Role Permissions screen repeated its own group header ("Platform Tasks Read" inside the Tasks group). The
    // module now travels in Permission.Module and is shown once, on the group header, so the DisplayName carries
    // only what is left: the resource and the action.
    //
    // The dropped segment is THIS SERVICE'S OWN namespace, which is a fact about Diten.Platform and is stated once
    // below — not a second copy of AuthService's "which namespaces are services" registry
    // (PermissionModuleAttribution.ServiceNamespaces), which answers a different question and stays the single
    // source of the attribution rule. Every key this worker sees comes from a [HasPermission] attribute in this
    // assembly, so its first segment is always this constant; a key that somehow is not is left whole.
    private const string OwnPermissionNamespace = "platform";

    private static string DeriveDisplayName(string key)
    {
        var segments = key.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 2 && string.Equals(segments[0], OwnPermissionNamespace, StringComparison.OrdinalIgnoreCase))
        {
            segments = segments[1..];
        }

        var words = string.Join(' ', segments).Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpper(w[0], CultureInfo.InvariantCulture) + w[1..]);
        return string.Join(' ', words);
    }
}
