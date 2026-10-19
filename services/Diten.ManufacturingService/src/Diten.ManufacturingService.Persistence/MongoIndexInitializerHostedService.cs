using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.ManufacturingService.Persistence;

/// <summary>
/// BOM index'lerini açılışta kurar (idempotent). Mongo erişilemezse açılışı çökertmez — uyarı yazar; ama
/// "tek Effective" kuralı bu index'e dayandığı için uyarı açıkça söyler.
/// </summary>
public sealed class MongoIndexInitializerHostedService(IServiceScopeFactory scopeFactory, ILogger<MongoIndexInitializerHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            await BomIndexConfiguration.EnsureIndexesAsync(scope.ServiceProvider.GetRequiredService<IMongoDatabase>(), stoppingToken);
            logger.LogInformation("BOM Mongo indexes ensured.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "BOM Mongo indexes NOT ensured — the single-Effective-version rule is unenforced until they are.");
        }
    }
}
