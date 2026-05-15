using Diten.Platform.Application.Contracts.Eventing;
using Microsoft.Extensions.Options;
using Prometheus;

namespace Diten.Platform.API.Observability;

public sealed class OutboxPendingCountMetricsService : BackgroundService
{
    private static readonly Gauge PendingCount = Metrics.CreateGauge(
        "outbox_pending_count",
        "Count of pending or retryable outbox events.",
        new GaugeConfiguration { LabelNames = new[] { "service", "environment" } });

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Diten.Platform.Common.Observability.ObservabilityOptions _options;
    private readonly ILogger<OutboxPendingCountMetricsService> _logger;

    public OutboxPendingCountMetricsService(
        IServiceScopeFactory scopeFactory,
        IOptions<Diten.Platform.Common.Observability.ObservabilityOptions> options,
        ILogger<OutboxPendingCountMetricsService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RefreshPendingCountAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshPendingCountAsync(stoppingToken);
        }
    }

    private async Task RefreshPendingCountAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var reader = scope.ServiceProvider.GetRequiredService<IOutboxObservabilityReader>();
            var count = await reader.GetPendingCountAsync(cancellationToken);
            PendingCount.WithLabels(_options.ServiceName, ResolveEnvironment()).Set(count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "outbox.pending_count_metric_failed Operation={Operation} Result={Result} ErrorType={ErrorType}",
                "outbox_pending_count",
                "failed",
                ex.GetType().Name);
        }
    }

    private string ResolveEnvironment()
    {
        return string.IsNullOrWhiteSpace(_options.Environment)
            ? "Unknown"
            : _options.Environment;
    }
}
