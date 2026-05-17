using System.Text;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.Contracts.Eventing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prometheus;
using Xunit;

namespace Diten.Platform.Eventing.Tests;

public sealed class EventingObservabilityMetricsSmokeTests
{
    [Fact]
    public async Task InternalOnlySmoke_ExposesEventingMetricFamilies_WithoutPublicEndpoint()
    {
        const string correlationId = "mod0041-eventbus-proof";
        const string payloadMarker = "payload-secret-proof";

        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<Diten.Platform.Common.Observability.ObservabilityOptions>(options =>
        {
            options.ServiceName = "Diten.Platform.Tests";
            options.Environment = "Test";
        });
        services.AddSingleton<IOutboxObservabilityReader>(new FakeOutboxObservabilityReader(7));
        var transportPublisher = new SwitchableTransportPublisher();
        services.AddSingleton<IEventTransportPublisher>(transportPublisher);
        services.AddEventingObservabilityMetrics();

        await using var provider = services.BuildServiceProvider();

        var publisher = provider.GetRequiredService<IEventTransportPublisher>();
        await publisher.PublishAsync(new EventTransportMessage(
            Guid.NewGuid(),
            "tenant.activated.v1",
            1,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            null,
            null,
            "Diten.Platform.Tests",
            DateTimeOffset.UtcNow,
            $$"""{"marker":"{{payloadMarker}}"}"""));
        transportPublisher.FailNextPublish = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync(new EventTransportMessage(
            Guid.NewGuid(),
            "tenant.activated.v1",
            1,
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            null,
            null,
            "Diten.Platform.Tests",
            DateTimeOffset.UtcNow,
            "{}")));

        var sink = provider.GetRequiredService<IEnumerable<IEventingObservabilitySink>>().Single();
        await sink.OnEventConsumedAsync(
            "tenant.activated.v1",
            "1",
            "TenantActivatedV1Consumer",
            "succeeded",
            TimeSpan.FromMilliseconds(12),
            correlationId);
        await sink.OnEventConsumedAsync(
            "tenant.activated.v1",
            "1",
            "TenantActivatedV1Consumer",
            "failed",
            TimeSpan.FromMilliseconds(3),
            correlationId);
        await sink.OnEventConsumedAsync(
            "tenant.activated.v1",
            "1",
            "TenantActivatedV1Consumer",
            "duplicate",
            TimeSpan.Zero,
            correlationId);

        var hostedService = provider.GetRequiredService<IEnumerable<IHostedService>>()
            .OfType<OutboxPendingCountMetricsService>()
            .Single();
        await hostedService.StartAsync(CancellationToken.None);
        await Task.Delay(100);
        await hostedService.StopAsync(CancellationToken.None);

        var metrics = await ExportMetricsAsync();

        Assert.Contains("event_publish_started", metrics);
        Assert.Contains("event_publish_succeeded", metrics);
        Assert.Contains("event_publish_failed", metrics);
        Assert.Contains("event_publish_duration_seconds", metrics);
        Assert.Contains("event_consume_succeeded", metrics);
        Assert.Contains("event_consume_failed", metrics);
        Assert.Contains("event_consume_skipped", metrics);
        Assert.Contains("event_consume_duration_seconds", metrics);
        Assert.Contains("outbox_pending_count", metrics);
        Assert.Contains("outbox_pending_count{service=\"Diten.Platform.Tests\",environment=\"Test\"} 7", metrics);

        Assert.DoesNotContain(payloadMarker, metrics);
        Assert.DoesNotContain(correlationId, metrics);
        Assert.DoesNotContain("11111111-1111-1111-1111-111111111111", metrics);
        Assert.DoesNotContain("22222222-2222-2222-2222-222222222222", metrics);
        Assert.DoesNotContain("guest", metrics, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", metrics, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionString", metrics, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ExportMetricsAsync()
    {
        await using var stream = new MemoryStream();
        await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(
            stream,
            ExpositionFormat.PrometheusText,
            CancellationToken.None);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private sealed class SwitchableTransportPublisher : IEventTransportPublisher
    {
        public List<EventTransportMessage> Messages { get; } = [];

        public bool FailNextPublish { get; set; }

        public Task PublishAsync(EventTransportMessage message, CancellationToken cancellationToken = default)
        {
            if (FailNextPublish)
            {
                FailNextPublish = false;
                throw new InvalidOperationException("transport unavailable");
            }

            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeOutboxObservabilityReader : IOutboxObservabilityReader
    {
        private readonly long _pendingCount;

        public FakeOutboxObservabilityReader(long pendingCount)
        {
            _pendingCount = pendingCount;
        }

        public Task<long> GetPendingCountAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_pendingCount);
        }
    }
}
