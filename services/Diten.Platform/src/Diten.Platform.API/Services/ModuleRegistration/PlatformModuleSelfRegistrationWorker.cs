using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleRegistration;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Common.Tenancy;
using MediatR;

namespace Diten.Platform.API.Services.ModuleRegistration;

/// <summary>
/// MC-3b — at startup, reconciles every Platform-internal <see cref="IModuleManifestProvider"/> into the module
/// catalog IN-PROCESS (no HTTP/self-call). Mirrors InternalModuleRegistrationController: sets the platform context
/// (Guid.Empty) so the reconcile reads/writes the same scope the catalog UI uses, then sends
/// <see cref="RegisterModuleManifestCommand"/> via MediatR. Idempotent (the reconcile is); a failure for one
/// provider is logged and never blocks the others or startup. Cross-service modules (goldenslim) still push over
/// HTTP from their own service — this only covers modules that live inside Platform.
/// </summary>
public sealed class PlatformModuleSelfRegistrationWorker : BackgroundService
{
    private readonly IEnumerable<IModuleManifestProvider> _providers;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ModuleSelfRegistrationGate _gate;
    private readonly ILogger<PlatformModuleSelfRegistrationWorker> _logger;

    public PlatformModuleSelfRegistrationWorker(
        IEnumerable<IModuleManifestProvider> providers,
        IServiceScopeFactory scopeFactory,
        ModuleSelfRegistrationGate gate,
        ILogger<PlatformModuleSelfRegistrationWorker> logger)
    {
        _providers = providers;
        _scopeFactory = scopeFactory;
        _gate = gate;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ReconcileAllAsync(stoppingToken).ConfigureAwait(false);

            // AFTER the modules exist in the catalog — the event sync reads their manifests.
            await SyncNotificationEventsAsync(stoppingToken).ConfigureAwait(false);
        }
        finally
        {
            // Release the A1 permission worker in EVERY outcome (success, per-module failure, cancellation,
            // unexpected throw) so a problem here can never hang startup permission registration.
            _gate.MarkCompleted();
            _logger.LogInformation("Module self-registration finished; permission auto-registration released.");
        }
    }

    private async Task ReconcileAllAsync(CancellationToken stoppingToken)
    {
        foreach (var provider in _providers)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            var manifest = provider.GetManifest();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();

                // Same scope the catalog UI / internal registration endpoint uses (platform-scope, Guid.Empty).
                tenantContext.SetPlatformContext(Guid.Empty);

                var response = await mediator.Send(new RegisterModuleManifestCommand(manifest), stoppingToken);
                if (response.IsSuccessful)
                {
                    var r = response.Data;
                    _logger.LogInformation(
                        "Self-registered module {ModuleCode} ({Action}). Pages={Pages} Actions={Actions} PermissionsSynced={Perms}.",
                        manifest.ModuleCode,
                        r?.CatalogAction,
                        r?.PagesUpserted,
                        r?.ActionsUpserted,
                        r?.PermissionsSynced);
                }
                else
                {
                    _logger.LogWarning(
                        "Self-registration reconcile returned failure for module {ModuleCode}: {Errors}",
                        manifest.ModuleCode,
                        string.Join("; ", response.Errors));
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Best-effort: one module's failure must not block the others or startup.
                _logger.LogError(ex, "Self-registration failed for module {ModuleCode}.", manifest.ModuleCode);
            }
        }
    }

    /// <summary>
    /// WC-4 — reconcile every manifest-declared notification event into the catalog at STARTUP.
    ///
    /// <para><b>Why it had to become automatic.</b> The sync only ran when somebody POSTed
    /// <c>/api/platform/notifications/events/sync-from-manifest</c> by hand. An event declared in a manifest but
    /// absent from the catalog does not error — the dispatch simply returns a reason code and nothing is sent —
    /// so a module could ship, register its pages and permissions, and have every one of its notifications
    /// silently do nothing until an operator happened to run a command nobody documented. That is precisely the
    /// "believed to work, does not" class this ticket exists to end.</para>
    ///
    /// <para>Idempotent, like the module reconcile it follows, and best-effort for the same reason: a failure
    /// here must not block startup. It is logged loudly because the symptom otherwise is silence.</para>
    /// </summary>
    private async Task SyncNotificationEventsAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
            tenantContext.SetPlatformContext(Guid.Empty);

            var response = await mediator.Send(new SyncNotificationEventsFromManifestCommand(), stoppingToken);
            if (response.IsSuccessful)
            {
                _logger.LogInformation("Notification events synced from module manifests at startup.");
            }
            else
            {
                _logger.LogWarning(
                    "Notification event sync returned failure at startup: {Errors}. "
                    + "Events declared in a manifest but missing from the catalog will NOT dispatch.",
                    string.Join("; ", response.Errors));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down; nothing to report.
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Notification event sync threw at startup. Events declared in a manifest but missing from the "
                + "catalog will NOT dispatch until this is resolved.");
        }
    }
}
