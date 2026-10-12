using Diten.InventoryService.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence;

/// <summary>
/// Startup gate, before the host serves requests: (1) creates the MOD-0173 indexes, (2) rebuilds the S2
/// <c>inventory_sku_positions</c> projection from the ledger + reservations (pack ASSUMPTION-0173-13; idempotent, no
/// deletion — S1 ledgers that predate S2 get their positions here), (3) reports groups the rebuild skipped for an
/// incomplete scope and, outside a DB-010 test database, refuses them (FU-S2-01). Refuses to start if any step fails.
///
/// ⚠ DELIBERATE DEVIATION from the ProcurementService template (best-effort BackgroundService that logs and
/// continues): the unique indexes carry idempotency / single reversal / one row per dimension, and the reservation
/// guards rely on correct positions. A stock ledger that accepted postings without them could double-post or let
/// reserved stock be issued. Failing to start is the fail-closed choice (hard-stop 2 of the WPs).
/// </summary>
public sealed class InventoryIndexInitializerHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InventoryIndexInitializerHostedService> _logger;

    public InventoryIndexInitializerHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<InventoryIndexInitializerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        try
        {
            await InventoryIndexConfiguration.ApplyAsync(database, cancellationToken);
            _logger.LogInformation("MOD-0173 Mongo indexes ensured on database {Database}.", database.DatabaseNamespace.DatabaseName);

            var rebuilder = scope.ServiceProvider.GetRequiredService<ISkuPositionRebuilder>();
            var rebuilt = await rebuilder.RebuildAllAsync(cancellationToken);
            _logger.LogInformation("MOD-0173 SKU positions rebuilt from ledger + reservations: {Count}.", rebuilt);
            GuardUnscoped(rebuilder.LastSkippedUnscoped, database.DatabaseNamespace.DatabaseName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogCritical(ex, "MOD-0173 startup gate (indexes / SKU positions) failed; refusing to start.");
            throw;
        }
    }

    /// <summary>
    /// FU-S2-01: groups the rebuild skipped for an incomplete scope are always reported (WARNING, with the count). On a
    /// DB-010 test database that is all; on any other database the gate fails closed — stock without a tenant / LE /
    /// SKU scope means positions that cannot be trusted. The documents are never guessed, fixed or deleted here.
    /// </summary>
    private void GuardUnscoped(int skipped, string databaseName)
    {
        if (skipped == 0)
        {
            return;
        }

        _logger.LogWarning(
            "MOD-0173 SKU-position rebuild skipped {Skipped} group(s) without a complete scope (tenant, LE, item, sku, level) on database {Database} — never guessed, never deleted.",
            skipped,
            databaseName);
        if (!InventoryTestDatabases.IsTestDatabase(databaseName))
        {
            throw new InvalidOperationException(
                $"{skipped} unscoped SKU-position group(s) on non-test database '{databaseName}'; they must be investigated before the service may start.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
