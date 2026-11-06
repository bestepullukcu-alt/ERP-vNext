using System.Text.Json;
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
    private sealed class DemandBrokerProbe(TaskCompletionSource<EventTransportMessage> received)
        : IConsumer<EventTransportMessage>
    {
        public Task Consume(ConsumeContext<EventTransportMessage> context)
        {
            received.TrySetResult(context.Message);
            return Task.CompletedTask;
        }
    }

    [ManualDraftMongoFact]
    public async Task RabbitMqOptIn_ConsumesCommittedOutboxEventAndMatchesMongoReceipt()
    {
        if (Environment.GetEnvironmentVariable("MOD0188_TEST_RABBITMQ_ENABLED") != "true")
            return; // The CI planning-tests job enables this after its pinned broker is ready.

        var host = Environment.GetEnvironmentVariable("MOD0188_TEST_RABBITMQ_HOST")
            ?? throw new InvalidOperationException("MOD0188_TEST_RABBITMQ_HOST_REQUIRED");
        if (host != "127.0.0.1")
            throw new InvalidOperationException("The broker test is loopback-only.");
        var portText = Environment.GetEnvironmentVariable("MOD0188_TEST_RABBITMQ_PORT")
            ?? throw new InvalidOperationException("MOD0188_TEST_RABBITMQ_PORT_REQUIRED");
        if (!ushort.TryParse(portText, out var port))
            throw new InvalidOperationException("Invalid broker test port.");

        var signal = new TaskCompletionSource<EventTransportMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var bus = Bus.Factory.CreateUsingRabbitMq(cfg =>
        {
            cfg.Host(host, port, "/", options =>
            {
                options.Username("guest");
                options.Password("guest");
            });
            cfg.ReceiveEndpoint("mod0188-demand-broker-proof-" + Guid.NewGuid().ToString("N"),
                endpoint => endpoint.Consumer(() => new DemandBrokerProbe(signal)));
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
            var (_, _, context) = Open(databaseName: "mod0188_broker_proof_tests");
            await context.Outbox.DeleteManyAsync(Builders<DemandOutboxMessage>.Filter.Empty);
            var eventId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            var companyId = Guid.NewGuid();
            await context.Outbox.InsertOneAsync(new DemandOutboxMessage
            {
                EventId = eventId,
                CorrelationId = correlationId,
                TenantId = tenantId,
                LegalEntityId = companyId,
                RevisionId = Guid.NewGuid(),
                EventType = "demand.revision.published.v2",
                Payload = "{}",
                OccurredAt = Now,
                CreatedAt = Now
            });

            var publisher = new DemandRevisionRabbitMqPublisher(bus);
            var processor = new EventOutboxPublisherProcessor(
                new DemandRevisionOutboxStore(context), publisher,
                new EventOutboxPublisherOptions(1, 3, TimeSpan.Zero,
                    TimeSpan.Zero, TimeSpan.FromMinutes(5)));
            var delivery = Assert.Single(await processor.PublishPendingAsync());
            Assert.Equal(EventOutboxPublishOutcome.Published, delivery.Outcome);
            Assert.Equal(eventId, delivery.EventId);

            var brokerMessage = await signal.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(eventId, brokerMessage.EventId);
            Assert.Equal(correlationId, brokerMessage.CorrelationId);
            Assert.Equal(tenantId, brokerMessage.TenantId);
            Assert.Equal("demand.revision.published.v2", brokerMessage.EventName);

            var receipt = await context.Outbox.Find(x => x.EventId == brokerMessage.EventId)
                .SingleAsync();
            Assert.Equal(correlationId, receipt.CorrelationId);
            Assert.Equal("Published", receipt.DeliveryState);
            Assert.NotNull(receipt.DispatchedAt);

            // CI uploads this proof alongside its TRX only after the real consumer and Mongo receipt agree.
            var runnerTemp = Environment.GetEnvironmentVariable("RUNNER_TEMP");
            if (!string.IsNullOrWhiteSpace(runnerTemp))
            {
                var resultsDirectory = Path.Combine(runnerTemp, "test-results");
                Directory.CreateDirectory(resultsDirectory);
                var proof = new
                {
                    Broker = "RabbitMQ",
                    BrokerEventId = brokerMessage.EventId,
                    BrokerCorrelationId = brokerMessage.CorrelationId,
                    OutboxEventId = receipt.EventId,
                    OutboxCorrelationId = receipt.CorrelationId,
                    OutboxDeliveryState = receipt.DeliveryState,
                    OutboxDispatchedAt = receipt.DispatchedAt
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