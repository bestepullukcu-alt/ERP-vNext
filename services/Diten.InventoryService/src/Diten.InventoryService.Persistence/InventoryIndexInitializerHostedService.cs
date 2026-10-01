using Diten.InventoryService.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence;

/// <summary>
/// Startup gate, before the host serves requests: (1) creates the MOD-0173 indexes, (2) rebuilds the S2
/// <c>inventory_sku_positions</c> projection from the ledger + reservations (pack ASSUMPTION-0173-13; idempotent, no
/// deletion — S1 ledgers that predate S2 get their positions here). Refuses to start if either fails.
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

            var rebuilt = await scope.ServiceProvider.GetRequiredService<ISkuPositionRebuilder>().RebuildAllAsync(cancellationToken);
            _logger.LogInformation("MOD-0173 SKU positions rebuilt from ledger + reservations: {Count}.", rebuilt);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogCritical(ex, "MOD-0173 startup gate (indexes / SKU positions) failed; refusing to start.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
