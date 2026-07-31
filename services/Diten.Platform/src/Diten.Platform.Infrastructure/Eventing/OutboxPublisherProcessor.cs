using Diten.BuildingBlocks.Eventing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Eventing;

public sealed class OutboxPublisherProcessor
{
    private readonly EventOutboxPublisherProcessor _processor;
    private readonly ILogger<OutboxPublisherProcessor> _logger;

    public OutboxPublisherProcessor(
        IEventOutboxStore outboxRepository,
        IEventTransportPublisher publisher,
        IOptions<RabbitMqEventingOptions> options,
        ILogger<OutboxPublisherProcessor> logger)
    {
        var configured = options.Value;
        _processor = new EventOutboxPublisherProcessor(
            outboxRepository,
            publisher,
            new EventOutboxPublisherOptions(
                Math.Max(1, configured.BatchSize),
                Math.Max(1, configured.RetryCount),
                TimeSpan.FromSeconds(Math.Max(0, configured.InitialRetryDelaySeconds)),
                TimeSpan.FromSeconds(Math.Max(
                    Math.Max(0, configured.InitialRetryDelaySeconds),
                    configured.MaxRetryDelaySeconds)),
                TimeSpan.FromSeconds(Math.Max(1, configured.PublishingStaleAfterSeconds))));
        _logger = logger;
    }

    public async Task<int> PublishPendingAsync(CancellationToken cancellationToken = default)
    {
        var results = await _processor.PublishPendingAsync(cancellationToken);
        foreach (var result in results)
        {
            _logger.LogInformation(
                "event.outbox.delivery EventId={EventId} Outcome={Outcome} AttemptCount={AttemptCount} ReasonCode={ReasonCode}",
                result.EventId,
                result.Outcome,
                result.AttemptCount,
                result.ReasonCode);
        }

        return results.Count(result => result.Outcome == EventOutboxPublishOutcome.Published);
    }
}
