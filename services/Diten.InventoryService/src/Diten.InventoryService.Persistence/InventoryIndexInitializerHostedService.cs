using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence;

/// <summary>
/// Creates the MOD-0173 indexes BEFORE the host serves requests, and refuses to start if it cannot.
///
/// ⚠ DELIBERATE DEVIATION from the ProcurementService template (best-effort BackgroundService that logs and
/// continues): here the unique indexes carry correctness — idempotency, single reversal, one row per dimension. A
/// stock ledger that accepted postings without them could double-post under a race. Failing to start is the
/// fail-closed choice (hard-stop 2 of WP-0173-S1: never imitate integrity).
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
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogCritical(ex, "MOD-0173 Mongo indexes could not be ensured; refusing to start.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
