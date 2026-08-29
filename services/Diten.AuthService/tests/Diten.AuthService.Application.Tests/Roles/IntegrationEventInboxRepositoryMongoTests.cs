using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.AuthService.Application.Tests.Roles;

[Collection(AuthPermissionOnboardingMongoCollectionDefinition.Name)]
public sealed class IntegrationEventInboxRepositoryMongoTests
{
    private const string DatabaseName = "diten_auth_permission_onboarding_itest";
    private const string CollectionName = "integrationEventInbox";

    [Fact]
    public async Task Real_mongo_completion_protocol_replays_legacy_rejects_drift_and_suppresses_completed_duplicates()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://localhost:27017");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);
        await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

        var database = client.GetDatabase(DatabaseName);
        var collection = database.GetCollection<ProcessedIntegrationEvent>(CollectionName);
        var repository = new IntegrationEventInboxRepository(database);
        var tenantId = Guid.NewGuid();
        var legacyEventId = Guid.NewGuid();
        var concurrentEventId = Guid.NewGuid();
        var ownedEventIds = new[] { legacyEventId, concurrentEventId };

        // This is the existing production index contract, not a test-only index or full Auth schema bootstrap.
        await collection.Indexes.CreateOneAsync(new CreateIndexModel<ProcessedIntegrationEvent>(
            Builders<ProcessedIntegrationEvent>.IndexKeys.Ascending(item => item.EventId),
            new CreateIndexOptions { Unique = true }));

        try
        {
            await CleanupAsync(collection, ownedEventIds);

            Assert.True(await repository.TryInsertAsync(
                legacyEventId,
                "tenant.entitlement.enabled.v1",
                tenantId));
            await collection.UpdateOneAsync(
                item => item.EventId == legacyEventId,
                Builders<ProcessedIntegrationEvent>.Update.Unset(
                    nameof(ProcessedIntegrationEvent.CompletionProtocolVersion)));

            var legacy = await repository.GetAsync(
                legacyEventId,
                "tenant.entitlement.enabled.v1",
                tenantId);
            Assert.NotNull(legacy);
            Assert.Null(legacy.CompletionProtocolVersion);

            await repository.MarkCompletedAsync(
                legacyEventId,
                "tenant.entitlement.enabled.v1",
                tenantId);
            await repository.MarkCompletedAsync(
                legacyEventId,
                "tenant.entitlement.enabled.v1",
                tenantId);

            var completedLegacy = await repository.GetAsync(
                legacyEventId,
                "tenant.entitlement.enabled.v1",
                tenantId);
            Assert.Equal(ProcessedIntegrationEvent.CurrentCompletionProtocolVersion, completedLegacy?.CompletionProtocolVersion);
            Assert.Equal(1, await collection.CountDocumentsAsync(item => item.EventId == legacyEventId));

            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.MarkCompletedAsync(
                legacyEventId,
                "tenant.entitlement.disabled.v1",
                tenantId));
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.MarkCompletedAsync(
                legacyEventId,
                "tenant.entitlement.enabled.v1",
                Guid.NewGuid()));
            Assert.Equal(1, await collection.CountDocumentsAsync(item => item.EventId == legacyEventId));

            var concurrentCompletions = Enumerable.Range(0, 16)
                .Select(_ => repository.MarkCompletedAsync(
                    concurrentEventId,
                    "tenant.subscription.changed.v1",
                    tenantId));
            await Task.WhenAll(concurrentCompletions);

            var completedConcurrent = await repository.GetAsync(
                concurrentEventId,
                "tenant.subscription.changed.v1",
                tenantId);
            Assert.Equal(ProcessedIntegrationEvent.CurrentCompletionProtocolVersion, completedConcurrent?.CompletionProtocolVersion);
            Assert.Equal(1, await collection.CountDocumentsAsync(item => item.EventId == concurrentEventId));
        }
        finally
        {
            await CleanupAsync(collection, ownedEventIds);
        }
    }

    [Fact]
    public async Task Real_mongo_repository_propagates_cancellation_without_writing_completion()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://localhost:27017");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);
        var database = client.GetDatabase(DatabaseName);
        var collection = database.GetCollection<ProcessedIntegrationEvent>(CollectionName);
        var repository = new IntegrationEventInboxRepository(database);
        var eventId = Guid.NewGuid();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.MarkCompletedAsync(
                eventId,
                "tenant.entitlement.disabled.v1",
                Guid.NewGuid(),
                cancelled.Token));
            Assert.Null(await repository.GetAsync(
                eventId,
                "tenant.entitlement.disabled.v1",
                Guid.NewGuid()));
        }
        finally
        {
            await CleanupAsync(collection, [eventId]);
        }
    }

    private static Task CleanupAsync(
        IMongoCollection<ProcessedIntegrationEvent> collection,
        IReadOnlyCollection<Guid> eventIds)
        => collection.DeleteManyAsync(
            Builders<ProcessedIntegrationEvent>.Filter.In(item => item.EventId, eventIds));
}
