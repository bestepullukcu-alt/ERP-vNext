using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.PpmService.Infrastructure.Audit;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.PpmService.Tests;

public sealed class PpmAuditMassTransitPipelineTests
{
    [Fact]
    public async Task Shared_outbox_processor_publishes_only_permanent_urn_with_exact_envelope_payload_and_headers()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            var eventId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var correlationId = Guid.Parse("55555555-5555-5555-5555-555555555555");
            var tenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");
            var occurredAtUtc = DateTimeOffset.Parse(
                "2026-07-30T10:20:30.0000000+00:00",
                System.Globalization.CultureInfo.InvariantCulture);
            const string payload =
                "{\"actorId\":\"22222222-2222-2222-2222-222222222222\"," +
                "\"auditIntentId\":\"11111111-1111-1111-1111-111111111111\"," +
                "\"entityId\":\"44444444-4444-4444-4444-444444444444\"," +
                "\"entityType\":\"Project\",\"mutation\":\"created\"," +
                "\"occurredAtUtc\":\"2026-07-30T10:20:30.0000000Z\"}";
            var headers = new Dictionary<string, string>
            {
                [TrustedTransportMetadata.SignatureSchemeHeader] = "ppm-event-hmac-sha256.v1",
                [TrustedTransportMetadata.KeyIdHeader] = "ppm-current",
                [TrustedTransportMetadata.SignatureHeader] =
                    "ea31a1822130953463f4705493aa1e4de6a09752801dd52d810dfbc461e9d40e"
            };
            var item = new EventOutboxPublishItem(
                new EventMetadata(
                    eventId,
                    "ppm.audit-intent.submitted.v1",
                    1,
                    correlationId,
                    null,
                    tenantId,
                    "Diten.PpmService",
                    occurredAtUtc),
                Encoding.UTF8.GetBytes(payload),
                new TrustedTransportMetadata(headers),
                EventOutboxDeliveryStatus.Pending,
                0,
                null);
            var store = new SingleItemOutboxStore(item);
            var processor = new EventOutboxPublisherProcessor(
                store,
                new MassTransitPpmEventTransportPublisher(harness.Bus),
                new EventOutboxPublisherOptions(
                    1,
                    5,
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(1)));

            var result = Assert.Single(await processor.PublishPendingAsync());
            var published = await harness.Published
                .SelectAsync<EventTransportMessage>()
                .First();

            Assert.Equal(EventOutboxPublishOutcome.Published, result.Outcome);
            Assert.Equal(eventId, store.CompletedEventId);
            Assert.Equal(eventId, published.Context.Message.EventId);
            Assert.Equal("ppm.audit-intent.submitted.v1", published.Context.Message.EventName);
            Assert.Equal(1, published.Context.Message.EventVersion);
            Assert.Equal(correlationId, published.Context.Message.CorrelationId);
            Assert.Null(published.Context.Message.CausationId);
            Assert.Equal(tenantId, published.Context.Message.TenantId);
            Assert.Equal("Diten.PpmService", published.Context.Message.Producer);
            Assert.Equal(occurredAtUtc, published.Context.Message.OccurredAtUtc);
            Assert.Equal(
                Encoding.UTF8.GetBytes(payload),
                published.Context.Message.CanonicalPayloadUtf8.ToArray());
            Assert.Equal(
                "ppm-event-hmac-sha256.v1",
                published.Context.Headers.Get<string>(
                    TrustedTransportMetadata.SignatureSchemeHeader));
            Assert.Equal(
                "ppm-current",
                published.Context.Headers.Get<string>(TrustedTransportMetadata.KeyIdHeader));
            Assert.Equal(
                headers[TrustedTransportMetadata.SignatureHeader],
                published.Context.Headers.Get<string>(
                    TrustedTransportMetadata.SignatureHeader));
            Assert.Contains(
                "urn:message:Diten.BuildingBlocks.Eventing:EventTransportMessage",
                published.Context.SupportedMessageTypes);
            Assert.DoesNotContain(
                "urn:message:Diten.Platform.Application.Contracts.Eventing:EventTransportMessage",
                published.Context.SupportedMessageTypes);
            Assert.Single(harness.Published.Select<EventTransportMessage>());
        }
        finally
        {
            await harness.Stop();
        }
    }

    private sealed class SingleItemOutboxStore(EventOutboxPublishItem item)
        : IEventOutboxStore
    {
        private bool _claimed;

        public Guid? CompletedEventId { get; private set; }

        public Task<EventOutboxWriteResult> EnqueueAsync(
            EventOutboxWriteRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<EventOutboxPublishItem?> ClaimForPublishAsync(
            DateTimeOffset nowUtc,
            DateTimeOffset stalePublishingCutoffUtc,
            CancellationToken cancellationToken = default)
        {
            if (_claimed)
            {
                return Task.FromResult<EventOutboxPublishItem?>(null);
            }

            _claimed = true;
            return Task.FromResult<EventOutboxPublishItem?>(item);
        }

        public Task CompletePublishAsync(
            Guid eventId,
            CancellationToken cancellationToken = default)
        {
            CompletedEventId = eventId;
            return Task.CompletedTask;
        }

        public Task FailPublishAsync(
            Guid eventId,
            string error,
            DateTimeOffset nextAttemptAtUtc,
            int maxAttempts,
            CancellationToken cancellationToken = default) =>
            throw new Xunit.Sdk.XunitException("Publish unexpectedly entered retry.");

        public Task DeadLetterPublishAsync(
            Guid eventId,
            EventOutboxTerminalFailure failure,
            CancellationToken cancellationToken = default) =>
            throw new Xunit.Sdk.XunitException("Publish unexpectedly dead-lettered.");
    }
}
