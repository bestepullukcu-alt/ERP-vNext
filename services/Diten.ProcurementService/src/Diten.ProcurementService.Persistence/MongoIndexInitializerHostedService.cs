using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.ProcurementService.Persistence;

/// <summary>
/// Supplier index'lerini açılışta best-effort kurar (idempotent). Mongo ayakta değilse startup'ı ASLA çökertmez —
/// log'lar ve devam eder (scaffold runnable kalır).
/// </summary>
public sealed class MongoIndexInitializerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MongoIndexInitializerHostedService> _logger;

    public MongoIndexInitializerHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<MongoIndexInitializerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
            await SupplierIndexConfiguration.EnsureIndexesAsync(database, stoppingToken);
            await SourcingIndexConfiguration.EnsureIndexesAsync(database, stoppingToken);
            await RequisitionPoIndexConfiguration.EnsureIndexesAsync(database, stoppingToken);
            await GrnIndexConfiguration.EnsureIndexesAsync(database, stoppingToken);
            await InvoiceMatchIndexConfiguration.EnsureIndexesAsync(database, stoppingToken);
            await ContractingIndexConfiguration.EnsureIndexesAsync(database, stoppingToken);
            _logger.LogInformation("Supplier + Sourcing + Requisition/PO + GRN + Invoice-Match + Contracting Mongo indexes ensured.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down — fine.
        }
        catch (Exception ex)
        {
            // Best-effort: Mongo erişilemezse startup engellenmez.
            _logger.LogWarning(ex, "Supplier Mongo index ensure skipped (Mongo unreachable?).");
        }
    }
}
