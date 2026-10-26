using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.BuildingBlocks.CentralAudit;

/// <summary>
/// FU-BL501-OUTBOX-RACE — startup step, before the host serves requests: the outbox collection and its indexes are created
/// outside any transaction (<see cref="ICentralAuditOutboxStore.EnsureCreatedAsync"/>, idempotent).
///
/// Without it, on a fresh database the first command's transaction created <c>central_audit_outbox</c> implicitly (its
/// enlisted insert) while the dispatcher's background <c>createIndexes</c> created the same namespace outside it; the
/// transaction's commit then failed — "Collection namespace … is already in use" — and the command answered 500 (MVP-6,
/// SupplyChain, 2026-10-22). Registered whether or not the background dispatcher runs.
///
/// Failure: with <see cref="CentralAuditOptions.RequireEnlistment"/> every write enlists in a transaction, so serving
/// requests without the collection brings the race back — the host refuses to start (the Inventory startup gate's
/// fail-closed choice). Otherwise rows are written outside a transaction, which creates the collection without a
/// conflict: the failure is logged and the host starts.
/// </summary>
public sealed class CentralAuditOutboxInitializerHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<CentralAuditOptions> _options;
    private readonly ILogger<CentralAuditOutboxInitializerHostedService> _logger;

    public CentralAuditOutboxInitializerHostedService(
        IServiceScopeFactory scopes,
        IOptions<CentralAuditOptions> options,
        ILogger<CentralAuditOutboxInitializerHostedService> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ICentralAuditOutboxStore>().EnsureCreatedAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (_options.Value.RequireEnlistment)
            {
                _logger.LogCritical(ex, "Central audit: the outbox collection could not be created before serving requests; refusing to start.");
                throw;
            }

            _logger.LogError(ex, "Central audit: the outbox collection could not be created at startup; the first event written creates it.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
