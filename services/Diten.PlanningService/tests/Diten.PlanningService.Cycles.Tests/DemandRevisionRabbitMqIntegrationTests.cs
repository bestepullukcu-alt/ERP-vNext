using System.Text.Json;
using System.Threading.Channels;
using Diten.BuildingBlocks.Eventing;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Infrastructure.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using MassTransit;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

[Collection(LiveRabbitMqCollection.Name)]
public sealed partial class PublishedRevisionMongoIntegrationTests
{
    private sealed class DemandBrokerProbe(ChannelWriter<EventTransportMessage> received)
        : IConsumer<EventTransportMessage>
    {
        public Task Consume(ConsumeContext<EventTransportMessage> context)
        {
            received.TryWrite(context.Message);
            return Task.CompletedTask;
        }
    }

    [ManualDraftMongoFact]
    public async Task RabbitMqOptIn_ConsumesCommittedOutboxEventAndMatchesMongoReceipt()
    {
        if (Environment.GetEnvironmentVariable("MOD0188_TEST_RABBITMQ_ENABLED") != "true")
            throw new InvalidOperationException("MOD0188_TEST_RABBITMQ_ENABLED_REQUIRED");

        var host = Environment.GetEnvironmentVariable("MOD0188_TEST_RABBITMQ_HOST")
            ?? throw new InvalidOperationException("MOD0188_TEST_RABBITMQ_HOST_REQUIRED");
        if (host != "127.0.0.1")
            throw new InvalidOperationException("The broker test is loopback-only.");
        var portText = Environment.GetEnvironmentVariable("MOD0188_TEST_RABBITMQ_PORT")
            ?? throw new InvalidOperationException("MOD0188_TEST_RABBITMQ_PORT_REQUIRED");
        if (!ushort.TryParse(portText, out var port))
            throw new InvalidOperationException("Invalid broker test port.");

        var received = Channel.CreateUnbounded<EventTransportMessage>();
        var bus = Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host(host, port, "/", options =>
            {
                options.Username("guest");
                options.Password("guest");
            });
            cfg.ReceiveEndpoint("mod0188-demand-broker-proof-" + Guid.NewGuid().ToString("N"),
                endpoint => endpoint.Consumer(() => new DemandBrokerProbe(received.Writer)));
        });
        using var startupLimit = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        try
        {
            await bus.StartAsync(startupLimit.Token);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("MOD0188_RABBITMQ_CONNECTION_FAILED", exception);
        }

        try
        {
            var (drafts, published, context) = Open(
                databaseName: "mod0188_broker_proof_tests", correlationId: Guid.NewGuid());
            await context.Outbox.DeleteManyAsync(Builders<DemandOutboxMessage>.Filter.Empty);
            var tenantId = Guid.NewGuid();
            var companyId = Guid.NewGuid();
            var creator = Guid.NewGuid();
            var reviewer = Guid.NewGuid();
            var first = Draft(tenantId, companyId, creator, FirstWeek);
            var replacement = Draft(tenantId, companyId, creator, FirstWeek, quantity: 7m);
            await Approve(drafts, first, creator, reviewer);
            await Approve(drafts, replacement, creator, reviewer);
            Assert.Equal(PublishOutcome.Published,
                (await Publish(published, first, reviewer)).Outcome);
            Assert.Equal(PublishOutcome.Published,
                (await Publish(published, replacement, reviewer)).Outcome);

            var publisher = new DemandRevisionRabbitMqPublisher(bus);
            var processor = new EventOutboxPublisherProcessor(
                new DemandRevisionOutboxStore(context), publisher,
                new EventOutboxPublisherOptions(3, 3, TimeSpan.Zero,
                    TimeSpan.Zero, TimeSpan.FromMinutes(5)));
            var deliveries = await processor.PublishPendingAsync();
            Assert.Equal(3, deliveries.Count);
            Assert.All(deliveries, x => Assert.Equal(EventOutboxPublishOutcome.Published, x.Outcome));
            var brokerMessages = new List<EventTransportMessage>();
            for (var index = 0; index < 3; index++)
                brokerMessages.Add(await received.Reader.ReadAsync().AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(15)));
            Assert.Equal(3, brokerMessages.Select(x => x.EventId).Distinct().Count());
            Assert.Equal(2, brokerMessages.Count(x => x.EventName == "demand.revision.published.v2"));
            Assert.Single(brokerMessages, x => x.EventName == "demand.revision.superseded.v2");
            Assert.All(brokerMessages, x => Assert.Equal(tenantId, x.TenantId));
            var receipts = await context.Outbox.Find(x => x.TenantId == tenantId &&
                x.LegalEntityId == companyId).ToListAsync();
            Assert.Equal(3, receipts.Count);
            foreach (var message in brokerMessages)
            {
                var receipt = Assert.Single(receipts, x => x.EventId == message.EventId);
                Assert.Equal(receipt.CorrelationId, message.CorrelationId);
                Assert.Equal("Published", receipt.DeliveryState);
                Assert.NotNull(receipt.DispatchedAt);
                Assert.NotEqual("{}", receipt.Payload);
            }

            // CI uploads this proof alongside its TRX only after the real consumer and Mongo receipt agree.
            var runnerTemp = Environment.GetEnvironmentVariable("RUNNER_TEMP");
            if (!string.IsNullOrWhiteSpace(runnerTemp))
            {
                var resultsDirectory = Path.Combine(runnerTemp, "test-results");
                Directory.CreateDirectory(resultsDirectory);
                var proof = new
                {
                    Broker = "RabbitMQ",
                    Events = brokerMessages.Select(message =>
                    {
                        var receipt = receipts.Single(x => x.EventId == message.EventId);
                        return new
                        {
                            message.EventName,
                            BrokerEventId = message.EventId,
                            BrokerCorrelationId = message.CorrelationId,
                            OutboxEventId = receipt.EventId,
                            OutboxCorrelationId = receipt.CorrelationId,
                            OutboxDeliveryState = receipt.DeliveryState,
                            OutboxDispatchedAt = receipt.DispatchedAt
                        };
                    }).ToArray()
                };
                await File.WriteAllTextAsync(
                    Path.Combine(resultsDirectory, "mod0188-rabbitmq-delivery-proof.json"),
                    JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        finally
        {
            await bus.StopAsync();
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LiveRabbitMqCollection
{
    public const string Name = "Live RabbitMQ broker";
}
