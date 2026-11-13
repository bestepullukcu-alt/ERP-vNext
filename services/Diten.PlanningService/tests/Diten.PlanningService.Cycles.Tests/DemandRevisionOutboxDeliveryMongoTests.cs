using Diten.BuildingBlocks.Eventing;
using Diten.PlanningService.Application.Features.DemandPlanning;
using Diten.PlanningService.Domain.Features.DemandPlanning;
using Diten.PlanningService.Infrastructure.Features.DemandPlanning;
using Diten.PlanningService.Persistence.Features.DemandPlanning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using Xunit;

namespace Diten.PlanningService.Cycles.Tests;

public sealed partial class PublishedRevisionMongoIntegrationTests
{
    private sealed class DeliveryEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Diten.PlanningService.Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public void V2Outbox_DraftTransport_IsOptInAndNeverEnabledInProduction()
    {
        var disabled = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>()).Build();
        var services = new ServiceCollection();
        services.AddDemandRevisionEventTransport(disabled,
            new DeliveryEnvironment("Production"));
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(IEventTransportPublisher));

        var enabled = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["EventTransport:Enabled"] = "true",
                ["EventTransport:RabbitMqHost"] = "127.0.0.1",
                ["EventTransport:RabbitMqUsername"] = "test",
                ["EventTransport:RabbitMqPassword"] = "test"
            }).Build();
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection()
            .AddDemandRevisionEventTransport(enabled, new DeliveryEnvironment("Production")));
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection()
            .AddDemandRevisionEventTransport(new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["EventTransport:Enabled"] = "true" }).Build(),
                new DeliveryEnvironment("IntegrationTest")));
    }

    private sealed class RecordingDemandTransport : IEventTransportPublisher
    {
        public List<EventTransportMessage> Delivered { get; } = [];
        public bool Fail { get; set; }

        public Task PublishAsync(EventTransportMessage message,
            CancellationToken cancellationToken = default)
        {
            if (Fail) throw new IOException("test broker unavailable");
            Delivered.Add(message);
            return Task.CompletedTask;
        }
    }

    [ManualDraftMongoFact]
    public async Task V2Outbox_PublishedAndSuperseded_DeliverOnceWithDurableReceipt()
    {
        var (drafts, published, context) = Open(databaseName: "mod0188_delivery_tests");
        await context.Outbox.DeleteManyAsync(Builders<DemandOutboxMessage>.Filter.Empty);
        var tenant = Guid.NewGuid(); var company = Guid.NewGuid();
        var first = Draft(tenant, company, Guid.NewGuid(), FirstWeek);
        var second = Draft(tenant, company, Guid.NewGuid(), FirstWeek);
        var reviewer = Guid.NewGuid();
        await Approve(drafts, first, first.CreatedBy, reviewer);
        await Approve(drafts, second, second.CreatedBy, reviewer);
        Assert.Equal(PublishOutcome.Published, (await Publish(published, first, reviewer)).Outcome);
        Assert.Equal(PublishOutcome.Published, (await Publish(published, second, reviewer)).Outcome);

        var transport = new RecordingDemandTransport();
        var processor = new EventOutboxPublisherProcessor(new DemandRevisionOutboxStore(context),
            transport, new EventOutboxPublisherOptions(10, 3, TimeSpan.Zero,
                TimeSpan.Zero, TimeSpan.FromMinutes(5)));
        var result = await processor.PublishPendingAsync();
        Assert.Equal(3, result.Count);
        Assert.All(result, x => Assert.Equal(EventOutboxPublishOutcome.Published, x.Outcome));
        Assert.Equal(3, transport.Delivered.Select(x => x.EventId).Distinct().Count());
        Assert.Contains(transport.Delivered, x => x.EventName == "demand.revision.superseded.v2" &&
            x.PayloadJson.Contains(first.Id.ToString("D"), StringComparison.OrdinalIgnoreCase));
        Assert.All(transport.Delivered, x =>
        {
            Assert.Equal(tenant, x.TenantId);
            Assert.Equal(2, x.EventVersion);
        });
        Assert.Empty(await processor.PublishPendingAsync());
        var stored = await context.Outbox.Find(x => x.TenantId == tenant &&
            x.LegalEntityId == company).ToListAsync();
        Assert.Equal(3, stored.Count);
        Assert.All(stored, x =>
        {
            Assert.Equal("Published", x.DeliveryState);
            Assert.NotNull(x.DispatchedAt);
        });
    }

    [ManualDraftMongoFact]
    public async Task V2Outbox_BrokerFailure_RetriesWithoutLosingIntent()
    {
        var (_, _, context) = Open(databaseName: "mod0188_delivery_tests");
        await context.Outbox.DeleteManyAsync(Builders<DemandOutboxMessage>.Filter.Empty);
        var eventId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        await context.Outbox.InsertOneAsync(new DemandOutboxMessage
        {
            EventId = eventId, CorrelationId = correlationId,
            TenantId = Guid.NewGuid(), LegalEntityId = Guid.NewGuid(),
            RevisionId = Guid.NewGuid(), EventType = "demand.revision.published.v2",
            Payload = "{}", OccurredAt = Now, CreatedAt = Now
        });
        var transport = new RecordingDemandTransport { Fail = true };
        var processor = new EventOutboxPublisherProcessor(new DemandRevisionOutboxStore(context),
            transport, new EventOutboxPublisherOptions(1, 3, TimeSpan.Zero,
                TimeSpan.Zero, TimeSpan.FromMinutes(5)));
        Assert.Equal(EventOutboxPublishOutcome.RetryScheduled,
            Assert.Single(await processor.PublishPendingAsync()).Outcome);
        var pending = await context.Outbox.Find(x => x.EventId == eventId).SingleAsync();
        Assert.Equal("Pending", pending.DeliveryState);
        Assert.Equal(1, pending.AttemptCount);
        Assert.Null(pending.DispatchedAt);
        transport.Fail = false;
        Assert.Equal(EventOutboxPublishOutcome.Published,
            Assert.Single(await processor.PublishPendingAsync()).Outcome);
        var delivered = await context.Outbox.Find(x => x.EventId == eventId).SingleAsync();
        Assert.Equal("Published", delivered.DeliveryState);
        Assert.NotNull(delivered.DispatchedAt);
        Assert.Single(transport.Delivered);
        Assert.Equal(correlationId, transport.Delivered[0].CorrelationId);
    }

    [ManualDraftMongoFact]
    public async Task V2Outbox_LegacyRecordWithoutDeliveryFields_CanBeClaimed()
    {
        var (_, _, context) = Open(databaseName: "mod0188_delivery_tests");
        await context.Outbox.DeleteManyAsync(Builders<DemandOutboxMessage>.Filter.Empty);
        var eventId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await context.Outbox.Database.GetCollection<MongoDB.Bson.BsonDocument>(
            context.Outbox.CollectionNamespace.CollectionName).InsertOneAsync(
            new MongoDB.Bson.BsonDocument
            {
                ["_id"] = new MongoDB.Bson.BsonBinaryData(Guid.NewGuid(), MongoDB.Bson.GuidRepresentation.Standard),
                ["EventId"] = new MongoDB.Bson.BsonBinaryData(eventId, MongoDB.Bson.GuidRepresentation.Standard),
                ["TenantId"] = new MongoDB.Bson.BsonBinaryData(tenantId, MongoDB.Bson.GuidRepresentation.Standard),
                ["LegalEntityId"] = new MongoDB.Bson.BsonBinaryData(Guid.NewGuid(), MongoDB.Bson.GuidRepresentation.Standard),
                ["RevisionId"] = new MongoDB.Bson.BsonBinaryData(Guid.NewGuid(), MongoDB.Bson.GuidRepresentation.Standard),
                ["EventType"] = "demand.revision.published.v2",
                ["Payload"] = "{}",
                ["OccurredAt"] = Now.UtcDateTime,
                ["CreatedAt"] = Now.UtcDateTime
            });
        var transport = new RecordingDemandTransport();
        var processor = new EventOutboxPublisherProcessor(new DemandRevisionOutboxStore(context),
            transport, new EventOutboxPublisherOptions(1, 3, TimeSpan.Zero,
                TimeSpan.Zero, TimeSpan.FromMinutes(5)));
        Assert.Equal(EventOutboxPublishOutcome.Published,
            Assert.Single(await processor.PublishPendingAsync()).Outcome);
        Assert.Equal(eventId, Assert.Single(transport.Delivered).CorrelationId);
    }
}
