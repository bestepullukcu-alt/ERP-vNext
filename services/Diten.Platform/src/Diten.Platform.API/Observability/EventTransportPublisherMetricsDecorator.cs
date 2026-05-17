using System.Diagnostics;
using Diten.Platform.Application.Contracts.Eventing;
using Microsoft.Extensions.Options;
using Prometheus;

namespace Diten.Platform.API.Observability;

public sealed class EventTransportPublisherMetricsDecorator : IEventTransportPublisher
{
    private static readonly Counter PublishStarted = Metrics.CreateCounter(
        "event_publish_started",
        "Event transport publish attempts started.",
        new CounterConfiguration { LabelNames = new[] { "service", "environment", "event_name" } });

    private static readonly Counter PublishSucceeded = Metrics.CreateCounter(
        "event_publish_succeeded",
        "Event transport publish attempts succeeded.",
        new CounterConfiguration { LabelNames = new[] { "service", "environment", "event_name" } });

    private static readonly Counter PublishFailed = Metrics.CreateCounter(
        "event_publish_failed",
        "Event transport publish attempts failed.",
        new CounterConfiguration { LabelNames = new[] { "service", "environment", "event_name" } });

    private static readonly Histogram PublishDuration = Metrics.CreateHistogram(
        "event_publish_duration_seconds",
        "Event transport publish duration in seconds.",
        new HistogramConfiguration
        {
            LabelNames = new[] { "service", "environment", "event_name", "result" },
            Buckets = Histogram.ExponentialBuckets(0.005, 2, 12)
        });

    private readonly IEventTransportPublisher _inner;
    private readonly Diten.Platform.Common.Observability.ObservabilityOptions _options;
    private readonly ILogger<EventTransportPublisherMetricsDecorator> _logger;

    public EventTransportPublisherMetricsDecorator(
        IEventTransportPublisher inner,
        IOptions<Diten.Platform.Common.Observability.ObservabilityOptions> options,
        ILogger<EventTransportPublisherMetricsDecorator> logger)
    {
        _inner = inner;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(EventTransportMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var labels = GetPublishLabels(message.EventName);
        PublishStarted.WithLabels(labels).Inc();
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "event.publish.started EventName={EventName} EventVersion={EventVersion} Operation={Operation} Result={Result} CorrelationId={CorrelationId}",
            message.EventName,
            message.EventVersion,
            "publish",
            "started",
            message.CorrelationId);

        try
        {
            await _inner.PublishAsync(message, cancellationToken);
            stopwatch.Stop();
            PublishSucceeded.WithLabels(labels).Inc();
            PublishDuration
                .WithLabels(_options.ServiceName, ResolveEnvironment(), message.EventName, "succeeded")
                .Observe(stopwatch.Elapsed.TotalSeconds);

            _logger.LogInformation(
                "event.publish.succeeded EventName={EventName} EventVersion={EventVersion} Operation={Operation} Result={Result} DurationMs={DurationMs} CorrelationId={CorrelationId}",
                message.EventName,
                message.EventVersion,
                "publish",
                "succeeded",
                stopwatch.Elapsed.TotalMilliseconds,
                message.CorrelationId);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            PublishFailed.WithLabels(labels).Inc();
            PublishDuration
                .WithLabels(_options.ServiceName, ResolveEnvironment(), message.EventName, "failed")
                .Observe(stopwatch.Elapsed.TotalSeconds);

            _logger.LogWarning(
                "event.publish.failed EventName={EventName} EventVersion={EventVersion} Operation={Operation} Result={Result} DurationMs={DurationMs} CorrelationId={CorrelationId} ErrorType={ErrorType}",
                message.EventName,
                message.EventVersion,
                "publish",
                "failed",
                stopwatch.Elapsed.TotalMilliseconds,
                message.CorrelationId,
                ex.GetType().Name);
            throw;
        }
    }

    private string[] GetPublishLabels(string eventName)
    {
        return new[] { _options.ServiceName, ResolveEnvironment(), eventName };
    }

    private string ResolveEnvironment()
    {
        return string.IsNullOrWhiteSpace(_options.Environment)
            ? "Unknown"
            : _options.Environment;
    }
}
