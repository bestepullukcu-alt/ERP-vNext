using Diten.BuildingBlocks.Eventing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.ManufacturingService.Infrastructure.Eventing;

/// <summary>
/// Moves BOM-version events from the outbox to the broker with the shared <see cref="EventOutboxPublisherProcessor"/>
/// (retry with backoff, dead-letter after <see cref="MaxAttempts"/>). Without a registered
/// <see cref="IEventTransportPublisher"/> (<c>EventTransport:Enabled=false</c>, the default) it says so once, loudly,
/// and stops: the events stay <c>Pending</c> in the outbox — written, not delivered.
/// </summary>
public sealed class BomOutboxPublisherWorker(IServiceScopeFactory scopes, ILogger<BomOutboxPublisherWorker> logger) : BackgroundService
{
    public const int MaxAttempts = 10;
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (var probe = scopes.CreateScope())
        {
            if (probe.ServiceProvider.GetService<IEventTransportPublisher>() is null)
            {
                logger.LogWarning(
                    "BOM-version event transport is not configured (EventTransport:Enabled=false). Events are written to the outbox and stay Pending; nothing is delivered to the broker.");
                return;
            }
        }

        logger.LogInformation("BOM-version outbox publisher started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var processor = new EventOutboxPublisherProcessor(
                    scope.ServiceProvider.GetRequiredService<IEventOutboxStore>(),
                    scope.ServiceProvider.GetRequiredService<IEventTransportPublisher>(),
                    new EventOutboxPublisherOptions(20, MaxAttempts, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)));
                var results = await processor.PublishPendingAsync(stoppingToken);
                foreach (var failed in results.Where(r => r.Outcome != EventOutboxPublishOutcome.Published))
                {
                    logger.LogWarning("BOM-version event {EventId} not delivered: {Outcome} after {Attempts} attempt(s), {Reason}.",
                        failed.EventId, failed.Outcome, failed.AttemptCount, failed.ReasonCode);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError("BOM-version outbox processing failed: {ExceptionType}", ex.GetType().Name);
            }

            await Task.Delay(Idle, stoppingToken);
        }
    }
}
