using Diten.AuthService.Application.Common.Interfaces;
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
    public async Task Real_mongo_claim_protocol_replays_legacy_rejects_drift_and_fences_concurrency()
    {
        var client = CreateClient();
        await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        var database = client.GetDatabase(DatabaseName);
        var collection = database.GetCollection<ProcessedIntegrationEvent>(CollectionName);
        var repository = new IntegrationEventInboxRepository(database);
        var tenantId = Guid.NewGuid();
        var legacyEventId = Guid.NewGuid();
        var concurrentEventId = Guid.NewGuid();
        var recoveryEventId = Guid.NewGuid();
        var ownedEventIds = new[] { legacyEventId, concurrentEventId, recoveryEventId };

        await collection.Indexes.CreateOneAsync(new CreateIndexModel<ProcessedIntegrationEvent>(
            Builders<ProcessedIntegrationEvent>.IndexKeys.Ascending(item => item.EventId),
            new CreateIndexOptions { Unique = true }));

        try
        {
            await CleanupAsync(collection, ownedEventIds);

            Assert.True(await repository.TryInsertAsync(legacyEventId, "tenant.entitlement.enabled.v1", tenantId));
            Assert.Equal(
                IntegrationEventClaimResult.Completed,
                (await repository.TryClaimAsync(
                    legacyEventId,
                    "tenant.entitlement.enabled.v1",
                    tenantId,
                    TimeSpan.FromSeconds(30))).Result);
            Assert.Equal(
                IntegrationEventClaimResult.IdentityMismatch,
                (await repository.TryClaimAsync(
                    legacyEventId,
                    "tenant.entitlement.disabled.v1",
                    tenantId,
                    TimeSpan.FromSeconds(30))).Result);
            Assert.Equal(
                IntegrationEventClaimResult.TenantMismatch,
                (await repository.TryClaimAsync(
                    legacyEventId,
                    "tenant.entitlement.enabled.v1",
                    Guid.NewGuid(),
                    TimeSpan.FromSeconds(30))).Result);

            var concurrentClaims = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ =>
                repository.TryClaimAsync(
                    concurrentEventId,
                    "tenant.subscription.changed.v1",
                    tenantId,
                    TimeSpan.FromSeconds(30))));
            Assert.Single(concurrentClaims, claim => claim.Result == IntegrationEventClaimResult.Claimed);
            Assert.Equal(15, concurrentClaims.Count(claim => claim.Result == IntegrationEventClaimResult.Busy));
            var winningClaim = concurrentClaims.Single(claim => claim.Result == IntegrationEventClaimResult.Claimed);
            await repository.CompleteClaimAsync(concurrentEventId, tenantId, winningClaim.ClaimId!.Value);
            Assert.Equal(
                IntegrationEventClaimResult.Completed,
                (await repository.TryClaimAsync(
                    concurrentEventId,
                    "tenant.subscription.changed.v1",
                    tenantId,
                    TimeSpan.FromSeconds(30))).Result);

            var initial = await repository.TryClaimAsync(
                recoveryEventId,
                "tenant.entitlement.added.v1",
                tenantId,
                TimeSpan.FromMilliseconds(20));
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            var reclaimed = await repository.TryClaimAsync(
                recoveryEventId,
                "tenant.entitlement.added.v1",
                tenantId,
                TimeSpan.FromSeconds(30));
            Assert.Equal(IntegrationEventClaimResult.Claimed, reclaimed.Result);
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CompleteClaimAsync(
                recoveryEventId,
                tenantId,
                initial.ClaimId!.Value));
            await repository.CompleteClaimAsync(recoveryEventId, tenantId, reclaimed.ClaimId!.Value);
        }
        finally
        {
            await CleanupAsync(collection, ownedEventIds);
        }
    }

    [Fact]
    public async Task Real_mongo_repository_propagates_cancellation_without_creating_claim()
    {
        var database = CreateClient().GetDatabase(DatabaseName);
        var collection = database.GetCollection<ProcessedIntegrationEvent>(CollectionName);
        var repository = new IntegrationEventInboxRepository(database);
        var eventId = Guid.NewGuid();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.TryClaimAsync(
                eventId,
                "tenant.entitlement.disabled.v1",
                Guid.NewGuid(),
                TimeSpan.FromSeconds(30),
                cancelled.Token));
            Assert.Equal(0, await collection.CountDocumentsAsync(item => item.EventId == eventId));
        }
        finally
        {
            await CleanupAsync(collection, [eventId]);
        }
    }

    private static MongoClient CreateClient()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://localhost:27017");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        return new MongoClient(settings);
    }

    private static Task CleanupAsync(
        IMongoCollection<ProcessedIntegrationEvent> collection,
        IReadOnlyCollection<Guid> eventIds)
        => collection.DeleteManyAsync(
            Builders<ProcessedIntegrationEvent>.Filter.In(item => item.EventId, eventIds));
}
