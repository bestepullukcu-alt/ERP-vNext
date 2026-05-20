using Diten.Platform.Application.Contracts.Eventing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Infrastructure.Eventing;

public sealed class OutboxPublisherProcessor
{
    private readonly IOutboxEventRepository _outboxRepository;
    private readonly IEventTransportPublisher _publisher;
    private readonly RabbitMqEventingOptions _options;
    private readonly ILogger<OutboxPublisherProcessor> _logger;

    public OutboxPublisherProcessor(
        IOutboxEventRepository outboxRepository,
        IEventTransportPublisher publisher,
        IOptions<RabbitMqEventingOptions> options,
        ILogger<OutboxPublisherProcessor> logger)
    {
        _outboxRepository = outboxRepository;
        _publisher = publisher;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> PublishPendingAsync(CancellationToken cancellationToken = default)
    {
        var published = 0;
        var batchSize = Math.Max(1, _options.BatchSize);

        for (var i = 0; i < batchSize; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var now = DateTimeOffset.UtcNow;
            var staleCutoff = now.AddSeconds(-Math.Max(1, _options.PublishingStaleAfterSeconds));
            var outboxEvent = await _outboxRepository.ClaimNextAsync(now, staleCutoff, cancellationToken);
            if (outboxEvent is null)
            {
                break;
            }

            try
            {
                await _publisher.PublishAsync(outboxEvent.ToTransportMessage(), cancellationToken);
                outboxEvent.MarkPublished();
                await _outboxRepository.UpdateAsync(outboxEvent, cancellationToken);
                published++;

                _logger.LogInformation(
                    "event.outbox.published EventId={EventId} EventName={EventName} EventVersion={EventVersion} CorrelationId={CorrelationId} CausationId={CausationId} TenantId={TenantId} Producer={Producer} ConsumerName={ConsumerName} Status={Status} AttemptCount={AttemptCount} OccurredAtUtc={OccurredAtUtc}",
                    outboxEvent.EventId,
                    outboxEvent.EventName,
                    outboxEvent.EventVersion,
                    outboxEvent.CorrelationId,
                    outboxEvent.CausationId,
                    outboxEvent.TenantId,
                    outboxEvent.Producer,
                    null,
                    outboxEvent.Status,
                    outboxEvent.AttemptCount,
                    outboxEvent.OccurredAtUtc);
            }
            catch (Exception ex)
            {
                var nextAttempt = CalculateNextAttempt(outboxEvent.AttemptCount + 1);
                outboxEvent.MarkPublishFailed(ex.Message, nextAttempt, _options.RetryCount);
                await _outboxRepository.UpdateAsync(outboxEvent, cancellationToken);

                var logName = outboxEvent.Status == OutboxEventStatus.DeadLettered
                    ? "event.deadlettered"
                    : "event.outbox.publish_failed";

                _logger.LogWarning(
                    ex,
                    "{LogName} EventId={EventId} EventName={EventName} EventVersion={EventVersion} CorrelationId={CorrelationId} CausationId={CausationId} TenantId={TenantId} Producer={Producer} ConsumerName={ConsumerName} Status={Status} AttemptCount={AttemptCount} OccurredAtUtc={OccurredAtUtc}",
                    logName,
                    outboxEvent.EventId,
                    outboxEvent.EventName,
                    outboxEvent.EventVersion,
                    outboxEvent.CorrelationId,
                    outboxEvent.CausationId,
                    outboxEvent.TenantId,
                    outboxEvent.Producer,
                    null,
                    outboxEvent.Status,
                    outboxEvent.AttemptCount,
                    outboxEvent.OccurredAtUtc);

                if (outboxEvent.Status != OutboxEventStatus.DeadLettered)
                {
                    _logger.LogInformation(
                        "event.retry_scheduled EventId={EventId} EventName={EventName} EventVersion={EventVersion} CorrelationId={CorrelationId} CausationId={CausationId} TenantId={TenantId} Producer={Producer} ConsumerName={ConsumerName} Status={Status} AttemptCount={AttemptCount} OccurredAtUtc={OccurredAtUtc}",
                        outboxEvent.EventId,
                        outboxEvent.EventName,
                        outboxEvent.EventVersion,
                        outboxEvent.CorrelationId,
                        outboxEvent.CausationId,
                        outboxEvent.TenantId,
                        outboxEvent.Producer,
                        null,
                        outboxEvent.Status,
                        outboxEvent.AttemptCount,
                        outboxEvent.OccurredAtUtc);
                }
            }
        }

        return published;
    }

    private DateTimeOffset CalculateNextAttempt(int attemptCount)
    {
        var initial = TimeSpan.FromSeconds(_options.InitialRetryDelaySeconds);
        var max = TimeSpan.FromSeconds(_options.MaxRetryDelaySeconds);
        var delaySeconds = initial.TotalSeconds * Math.Pow(2, Math.Max(0, attemptCount - 1));
        var delay = TimeSpan.FromSeconds(Math.Min(delaySeconds, max.TotalSeconds));
        return DateTimeOffset.UtcNow.Add(delay);
    }
}
