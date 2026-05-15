using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Services.Eventing;

public sealed class ConsumedEventStore
{
    private readonly IConsumedEventRepository _repository;
    private readonly IEnumerable<IEventingObservabilitySink> _observabilitySinks;
    private readonly ILogger<ConsumedEventStore> _logger;

    public ConsumedEventStore(
        IConsumedEventRepository repository,
        ILogger<ConsumedEventStore> logger,
        IEnumerable<IEventingObservabilitySink>? observabilitySinks = null)
    {
        _repository = repository;
        _logger = logger;
        _observabilitySinks = observabilitySinks ?? [];
    }

    public async Task<ConsumedEventExecutionResult> ExecuteOnceAsync<TEvent>(
        EventEnvelope<TEvent> envelope,
        string consumerName,
        Func<CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerName);
        ArgumentNullException.ThrowIfNull(handler);

        var started = ConsumedEvent.Started(envelope.Metadata, consumerName);
        var startResult = await _repository.TryStartAsync(started, cancellationToken);
        if (startResult.IsDuplicate)
        {
            var duplicateDuration = TimeSpan.Zero;
            await _repository.MarkSkippedDuplicateAsync(envelope.EventId, consumerName, cancellationToken);
            _logger.LogInformation(
                "event.consumer.duplicate_skipped EventId={EventId} EventName={EventName} EventVersion={EventVersion} CorrelationId={CorrelationId} CausationId={CausationId} TenantId={TenantId} Producer={Producer} ConsumerName={ConsumerName} Status={Status} AttemptCount={AttemptCount} OccurredAtUtc={OccurredAtUtc}",
                envelope.EventId,
                envelope.EventName,
                envelope.EventVersion,
                envelope.CorrelationId,
                envelope.CausationId,
                envelope.TenantId,
                envelope.Producer,
                consumerName,
                ConsumedEventStatus.SkippedDuplicate,
                startResult.Event.AttemptCount,
                envelope.OccurredAtUtc);
            await NotifyConsumedAsync(envelope, consumerName, "duplicate", duplicateDuration, cancellationToken);
            return ConsumedEventExecutionResult.Duplicate;
        }

        _logger.LogInformation(
            "event.consumer.started EventId={EventId} EventName={EventName} EventVersion={EventVersion} CorrelationId={CorrelationId} CausationId={CausationId} TenantId={TenantId} Producer={Producer} ConsumerName={ConsumerName} Status={Status} AttemptCount={AttemptCount} OccurredAtUtc={OccurredAtUtc}",
            envelope.EventId,
            envelope.EventName,
            envelope.EventVersion,
            envelope.CorrelationId,
            envelope.CausationId,
            envelope.TenantId,
            envelope.Producer,
            consumerName,
            ConsumedEventStatus.Started,
            startResult.Event.AttemptCount,
            envelope.OccurredAtUtc);

        var handlerStartedAt = DateTimeOffset.UtcNow;
        try
        {
            await handler(cancellationToken);
            var duration = DateTimeOffset.UtcNow - handlerStartedAt;
            await _repository.MarkConsumedAsync(envelope.EventId, consumerName, cancellationToken);
            _logger.LogInformation(
                "event.consumer.completed EventId={EventId} EventName={EventName} EventVersion={EventVersion} CorrelationId={CorrelationId} CausationId={CausationId} TenantId={TenantId} Producer={Producer} ConsumerName={ConsumerName} Status={Status} AttemptCount={AttemptCount} OccurredAtUtc={OccurredAtUtc}",
                envelope.EventId,
                envelope.EventName,
                envelope.EventVersion,
                envelope.CorrelationId,
                envelope.CausationId,
                envelope.TenantId,
                envelope.Producer,
                consumerName,
                ConsumedEventStatus.Consumed,
                startResult.Event.AttemptCount,
                envelope.OccurredAtUtc);
            await NotifyConsumedAsync(envelope, consumerName, "succeeded", duration, cancellationToken);
            return ConsumedEventExecutionResult.Consumed;
        }
        catch (Exception ex)
        {
            var duration = DateTimeOffset.UtcNow - handlerStartedAt;
            await _repository.MarkFailedAsync(envelope.EventId, consumerName, ex.Message, cancellationToken);
            _logger.LogError(
                ex,
                "event.consumer.failed EventId={EventId} EventName={EventName} EventVersion={EventVersion} CorrelationId={CorrelationId} CausationId={CausationId} TenantId={TenantId} Producer={Producer} ConsumerName={ConsumerName} Status={Status} AttemptCount={AttemptCount} OccurredAtUtc={OccurredAtUtc}",
                envelope.EventId,
                envelope.EventName,
                envelope.EventVersion,
                envelope.CorrelationId,
                envelope.CausationId,
                envelope.TenantId,
                envelope.Producer,
                consumerName,
                ConsumedEventStatus.Failed,
                startResult.Event.AttemptCount + 1,
                envelope.OccurredAtUtc);
            await NotifyConsumedAsync(envelope, consumerName, "failed", duration, cancellationToken);
            throw;
        }
    }

    private async Task NotifyConsumedAsync<TEvent>(
        EventEnvelope<TEvent> envelope,
        string consumerName,
        string result,
        TimeSpan duration,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        foreach (var sink in _observabilitySinks)
        {
            try
            {
                await sink.OnEventConsumedAsync(
                    envelope.EventName,
                    envelope.EventVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    consumerName,
                    result,
                    duration < TimeSpan.Zero ? TimeSpan.Zero : duration,
                    envelope.CorrelationId.ToString(),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "event.observability_sink_failed EventName={EventName} EventVersion={EventVersion} ConsumerName={ConsumerName} Result={Result} CorrelationId={CorrelationId} ErrorType={ErrorType}",
                    envelope.EventName,
                    envelope.EventVersion,
                    consumerName,
                    result,
                    envelope.CorrelationId,
                    ex.GetType().Name);
            }
        }
    }
}

public enum ConsumedEventExecutionResult
{
    Consumed = 0,
    Duplicate = 1
}
