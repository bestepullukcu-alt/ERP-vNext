namespace Diten.BuildingBlocks.Eventing;

public sealed record EventOutboxPublisherOptions(
    int BatchSize,
    int MaxAttempts,
    TimeSpan InitialRetryDelay,
    TimeSpan MaximumRetryDelay,
    TimeSpan PublishingStaleAfter);

public enum EventOutboxPublishOutcome
{
    Published,
    RetryScheduled,
    DeadLettered
}

public sealed record EventOutboxPublishResult(
    Guid EventId,
    EventOutboxPublishOutcome Outcome,
    int AttemptCount,
    string? ReasonCode = null);

public sealed class EventTransportTerminalException : Exception
{
    public EventTransportTerminalException(EventOutboxTerminalFailure failure)
        : base(failure.ReasonCode)
    {
        Failure = failure;
    }

    public EventOutboxTerminalFailure Failure { get; }
}

public sealed class EventOutboxPublisherProcessor
{
    private readonly IEventOutboxStore _store;
    private readonly IEventTransportPublisher _publisher;
    private readonly EventOutboxPublisherOptions _options;

    public EventOutboxPublisherProcessor(
        IEventOutboxStore store,
        IEventTransportPublisher publisher,
        EventOutboxPublisherOptions options)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _options = Validate(options);
    }

    public async Task<IReadOnlyList<EventOutboxPublishResult>> PublishPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new List<EventOutboxPublishResult>(_options.BatchSize);
        for (var index = 0; index < _options.BatchSize; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var now = DateTimeOffset.UtcNow;
            var item = await _store.ClaimForPublishAsync(
                now,
                now.Subtract(_options.PublishingStaleAfter),
                cancellationToken);
            if (item is null)
            {
                break;
            }

            try
            {
                var message = new EventTransportMessage(
                    item.Metadata.EventId,
                    item.Metadata.EventName,
                    item.Metadata.EventVersion,
                    item.Metadata.CorrelationId,
                    item.Metadata.CausationId,
                    item.Metadata.TenantId,
                    item.Metadata.Producer,
                    item.Metadata.OccurredAtUtc,
                    item.CanonicalPayloadUtf8,
                    item.TransportMetadata);

                await _publisher.PublishAsync(message, cancellationToken);
                await _store.CompletePublishAsync(item.Metadata.EventId, cancellationToken);
                results.Add(new EventOutboxPublishResult(
                    item.Metadata.EventId,
                    EventOutboxPublishOutcome.Published,
                    item.AttemptCount));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (EventTransportTerminalException exception)
            {
                await _store.DeadLetterPublishAsync(
                    item.Metadata.EventId,
                    exception.Failure,
                    cancellationToken);
                results.Add(new EventOutboxPublishResult(
                    item.Metadata.EventId,
                    EventOutboxPublishOutcome.DeadLettered,
                    item.AttemptCount + 1,
                    exception.Failure.ReasonCode));
            }
            catch (EventValidationException)
            {
                var failure = new EventOutboxTerminalFailure(
                    EventOutboxTerminalFailureKind.Validation,
                    "event.validation.failed",
                    "Event validation failed.");
                await _store.DeadLetterPublishAsync(item.Metadata.EventId, failure, cancellationToken);
                results.Add(new EventOutboxPublishResult(
                    item.Metadata.EventId,
                    EventOutboxPublishOutcome.DeadLettered,
                    item.AttemptCount + 1,
                    failure.ReasonCode));
            }
            catch (Exception exception)
            {
                var nextAttempt = CalculateNextAttempt(item.AttemptCount + 1);
                await _store.FailPublishAsync(
                    item.Metadata.EventId,
                    EventErrorRedactor.RedactAndTruncate(exception.GetType().Name),
                    nextAttempt,
                    _options.MaxAttempts,
                    cancellationToken);
                results.Add(new EventOutboxPublishResult(
                    item.Metadata.EventId,
                    item.AttemptCount + 1 >= _options.MaxAttempts
                        ? EventOutboxPublishOutcome.DeadLettered
                        : EventOutboxPublishOutcome.RetryScheduled,
                    item.AttemptCount + 1,
                    "event.transport.transient"));
            }
        }

        return results;
    }

    private DateTimeOffset CalculateNextAttempt(int attemptCount)
    {
        var seconds = _options.InitialRetryDelay.TotalSeconds
                      * Math.Pow(2, Math.Max(0, attemptCount - 1));
        return DateTimeOffset.UtcNow.AddSeconds(
            Math.Min(seconds, _options.MaximumRetryDelay.TotalSeconds));
    }

    private static EventOutboxPublisherOptions Validate(EventOutboxPublisherOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.BatchSize <= 0
            || options.MaxAttempts <= 0
            || options.InitialRetryDelay < TimeSpan.Zero
            || options.MaximumRetryDelay < options.InitialRetryDelay
            || options.PublishingStaleAfter <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        return options;
    }
}
