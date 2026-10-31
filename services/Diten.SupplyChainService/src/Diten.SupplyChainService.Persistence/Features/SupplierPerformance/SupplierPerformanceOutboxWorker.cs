using Diten.BuildingBlocks.Eventing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
namespace Diten.SupplyChainService.Persistence.Features.SupplierPerformance;

/// <summary>MOD-0147 §0.3: delivers Pending publications at-least-once. No transport today: events stay Pending, as Shipment's do.</summary>
public sealed class SupplierPerformanceOutboxWorker(IServiceProvider services, ILogger<SupplierPerformanceOutboxWorker> logger) : BackgroundService
{
    public static readonly EventOutboxPublisherOptions Options = new(20, 10, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var transport = services.GetService<IEventTransportPublisher>();
        if (transport is null) { logger.LogWarning("Supplier performance outbox transport is not registered. Durable events remain pending; integration is held."); return; }
        var processor = new EventOutboxPublisherProcessor(services.GetRequiredKeyedService<IEventOutboxStore>(SupplierPerformanceOutboxStore.ServiceKey), transport, Options);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await processor.PublishPendingAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError("Supplier performance outbox processing failed: {ExceptionType}", ex.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
