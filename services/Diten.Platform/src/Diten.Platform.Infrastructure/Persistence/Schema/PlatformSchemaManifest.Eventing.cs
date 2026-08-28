using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Entities.Notifications;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Entities.InterfaceRegistry;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Features.SubscriptionFeatures;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Models;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Schema;

public static partial class PlatformSchemaManifest
{
    /// <summary>
    /// Transport bookkeeping. Two collections; the RabbitMQ integration tests used to build all 82 to reach
/// them.
    /// </summary>
    private static readonly SchemaCollection[] EventingCollections =
    {
        Collection<OutboxEvent>(
            SchemaProfile.Eventing,
            PlatformCollections.OutboxEvents,
            () => new CreateIndexModel<OutboxEvent>[]
            {
                    new CreateIndexModel<OutboxEvent>(
                        Builders<OutboxEvent>.IndexKeys.Ascending(x => x.EventId),
                        new CreateIndexOptions { Unique = true, Name = "ux_outbox_events_event_id" }),
                    new CreateIndexModel<OutboxEvent>(
                        Builders<OutboxEvent>.IndexKeys.Ascending(x => x.EventName),
                        new CreateIndexOptions { Name = "ix_outbox_events_event_name" }),
                    new CreateIndexModel<OutboxEvent>(
                        Builders<OutboxEvent>.IndexKeys.Ascending(x => x.CorrelationId),
                        new CreateIndexOptions { Name = "ix_outbox_events_correlation_id" }),
                    new CreateIndexModel<OutboxEvent>(
                        Builders<OutboxEvent>.IndexKeys
                            .Ascending(x => x.Status)
                            .Ascending(x => x.NextAttemptAtUtc),
                        new CreateIndexOptions { Name = "ix_outbox_events_status_next_attempt" })

            }),
        Collection<ConsumedEvent>(
            SchemaProfile.Eventing,
            PlatformCollections.ConsumedEvents,
            () => new CreateIndexModel<ConsumedEvent>[]
            {
                    new CreateIndexModel<ConsumedEvent>(
                        Builders<ConsumedEvent>.IndexKeys
                            .Ascending(x => x.EventId)
                            .Ascending(x => x.ConsumerName),
                        new CreateIndexOptions { Unique = true, Name = "ux_consumed_events_event_consumer" })

            }),
    };
}
