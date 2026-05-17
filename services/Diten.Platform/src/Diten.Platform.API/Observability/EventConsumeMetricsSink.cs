using Diten.Platform.Application.Contracts.Eventing;
using Microsoft.Extensions.Options;
using Prometheus;

namespace Diten.Platform.API.Observability;

public sealed class EventConsumeMetricsSink : IEventingObservabilitySink
{
    private static readonly Counter ConsumeSucceeded = Metrics.CreateCounter(
        "event_consume_succeeded",
        "Event consume attempts succeeded.",
        new CounterConfiguration { LabelNames = new[] { "service", "environment", "event_name" } });

    private static readonly Counter ConsumeFailed = Metrics.CreateCounter(
        "event_consume_failed",
        "Event consume attempts failed.",
        new CounterConfiguration { LabelNames = new[] { "service", "environment", "event_name" } });

    private static readonly Counter ConsumeSkipped = Metrics.CreateCounter(
        "event_consume_skipped",
        "Event consume attempts skipped.",
        new CounterConfiguration { LabelNames = new[] { "service", "environment", "event_name", "result" } });

    private static readonly Histogram ConsumeDuration = Metrics.CreateHistogram(
        "event_consume_duration_seconds",
        "Event consume duration in seconds.",
        new HistogramConfiguration
        {
            LabelNames = new[] { "service", "environment", "event_name", "result" },
            Buckets = Histogram.ExponentialBuckets(0.005, 2, 12)
        });

    private readonly Diten.Platform.Common.Observability.ObservabilityOptions _options;
    private readonly ILogger<EventConsumeMetricsSink> _logger;

    public EventConsumeMetricsSink(
        IOptions<Diten.Platform.Common.Observability.ObservabilityOptions> options,
        ILogger<EventConsumeMetricsSink> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task OnEventConsumedAsync(
        string eventName,
        string eventVersion,
        string? consumerName,
        string result,
        TimeSpan duration,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var environment = ResolveEnvironment();
        var normalizedResult = NormalizeResult(result);

        switch (normalizedResult)
        {
            case "succeeded":
                ConsumeSucceeded.WithLabels(_options.ServiceName, environment, eventName).Inc();
                break;
            case "failed":
                ConsumeFailed.WithLabels(_options.ServiceName, environment, eventName).Inc();
                break;
            case "skipped":
            case "duplicate":
                ConsumeSkipped.WithLabels(_options.ServiceName, environment, eventName, normalizedResult).Inc();
                break;
        }

        ConsumeDuration
            .WithLabels(_options.ServiceName, environment, eventName, normalizedResult)
            .Observe(Math.Max(0, duration.TotalSeconds));

        _logger.LogInformation(
            "event.consume.observed EventName={EventName} EventVersion={EventVersion} ConsumerName={ConsumerName} Operation={Operation} Result={Result} DurationMs={DurationMs} CorrelationId={CorrelationId}",
            eventName,
            eventVersion,
            consumerName,
            "consume",
            normalizedResult,
            duration.TotalMilliseconds,
            correlationId);

        return Task.CompletedTask;
    }

    private static string NormalizeResult(string result)
    {
        return result is "succeeded" or "failed" or "skipped" or "duplicate"
            ? result
            : "skipped";
    }

    private string ResolveEnvironment()
    {
        return string.IsNullOrWhiteSpace(_options.Environment)
            ? "Unknown"
            : _options.Environment;
    }
}
