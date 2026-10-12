using Diten.BuildingBlocks.Eventing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace Diten.SupplyChainService.Infrastructure.Eventing;
public sealed class ShipmentOutboxWorker(IServiceProvider services, ILogger<ShipmentOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var transport = services.GetService<IEventTransportPublisher>();
        if (transport is null) { logger.LogWarning("Shipment outbox transport is not registered. Durable events remain pending; integration is held."); return; }
        var processor = new EventOutboxPublisherProcessor(services.GetRequiredService<IEventOutboxStore>(), transport,
         new EventOutboxPublisherOptions(20, 10, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await processor.PublishPendingAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError("Outbox processing failed: {ExceptionType}", ex.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
