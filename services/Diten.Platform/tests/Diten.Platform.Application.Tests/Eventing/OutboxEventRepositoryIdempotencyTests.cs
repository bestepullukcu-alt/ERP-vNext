using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Application.Tests.Persistence;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Eventing;

public sealed class OutboxEventRepositoryIdempotencyTests
{
    [Fact]
    public async Task SameEventIdAndImmutableContentIsNoOp_ChangedPayloadIsConflict()
    {
        await using var harness = await MongoIntegrationHarness.CreateAsync();
        await EnsureOutboxIndexesAsync(harness);
        var repository = new OutboxEventRepository(harness.DbContext, harness.TenantContext);
        var metadata = new EventMetadata(
            Guid.NewGuid(),
            "test.canonical.v1",
            1,
            Guid.NewGuid(),
            null,
            harness.TenantId,
            "Diten.Platform.Tests",
            DateTimeOffset.UtcNow);
        var trusted = new TrustedTransportMetadata(
        [
            new(TrustedTransportMetadata.SignatureSchemeHeader, "hmac-sha256-v1"),
            new(TrustedTransportMetadata.KeyIdHeader, "key-1"),
            new(TrustedTransportMetadata.SignatureHeader, new string('c', 64))
        ]);
        var request = new EventOutboxWriteRequest(metadata, Encoding.UTF8.GetBytes("{\"a\":1}"), trusted);

        Assert.Equal(EventOutboxWriteResult.Inserted, await repository.EnqueueAsync(request));
        Assert.Equal(EventOutboxWriteResult.Duplicate, await repository.EnqueueAsync(request));

        var changed = request with { CanonicalPayloadUtf8 = Encoding.UTF8.GetBytes("{\"a\":2}") };
        await Assert.ThrowsAsync<EventOutboxConflictException>(() => repository.EnqueueAsync(changed));
        Assert.Equal(
            1,
            await harness.Database
                .GetCollection<MongoDB.Bson.BsonDocument>("outbox_events")
                .CountDocumentsAsync(MongoDB.Driver.FilterDefinition<MongoDB.Bson.BsonDocument>.Empty));
    }

    [Fact]
    public async Task PublishingEvent_TerminalDispositionIsPersistedAtomically()
    {
        await using var harness = await MongoIntegrationHarness.CreateAsync();
        await EnsureOutboxIndexesAsync(harness);
        var repository = new OutboxEventRepository(harness.DbContext, harness.TenantContext);
        var request = CreateRequest(harness.TenantId);
        var failure = CreateFailure();

        await repository.EnqueueAsync(request);
        var claimed = await repository.ClaimForPublishAsync(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(-5));

        Assert.NotNull(claimed);
        Assert.Equal(request.Metadata.EventId, claimed.Metadata.EventId);

        await repository.DeadLetterPublishAsync(request.Metadata.EventId, failure);

        var stored = await repository.GetByEventIdAsync(request.Metadata.EventId);
        Assert.NotNull(stored);
        Assert.Equal(OutboxEventStatus.DeadLettered, stored.Status);
        Assert.Equal(1, stored.AttemptCount);
        Assert.Null(stored.NextAttemptAtUtc);
        Assert.Equal("Contract:contract.invalid:payload rejected", stored.LastError);
        Assert.NotNull(stored.UpdatedAt);
    }

    [Fact]
    public async Task RepeatingSameTerminalDisposition_IsIdempotent()
    {
        await using var harness = await MongoIntegrationHarness.CreateAsync();
        await EnsureOutboxIndexesAsync(harness);
        var repository = new OutboxEventRepository(harness.DbContext, harness.TenantContext);
        var request = CreateRequest(harness.TenantId);
        var failure = CreateFailure();

        await repository.EnqueueAsync(request);
        await repository.ClaimForPublishAsync(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(-5));
        await repository.DeadLetterPublishAsync(request.Metadata.EventId, failure);
        var first = await repository.GetByEventIdAsync(request.Metadata.EventId);

        await repository.DeadLetterPublishAsync(request.Metadata.EventId, failure);

        var repeated = await repository.GetByEventIdAsync(request.Metadata.EventId);
        Assert.NotNull(first);
        Assert.NotNull(repeated);
        Assert.Equal(OutboxEventStatus.DeadLettered, repeated.Status);
        Assert.Equal(1, repeated.AttemptCount);
        Assert.Equal(first.LastError, repeated.LastError);
        Assert.Equal(first.UpdatedAt, repeated.UpdatedAt);
    }

    [Fact]
    public async Task PublishedEvent_CannotBeDeadLettered()
    {
        await using var harness = await MongoIntegrationHarness.CreateAsync();
        await EnsureOutboxIndexesAsync(harness);
        var repository = new OutboxEventRepository(harness.DbContext, harness.TenantContext);
        var request = CreateRequest(harness.TenantId);

        await repository.EnqueueAsync(request);
        await repository.ClaimForPublishAsync(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(-5));
        await repository.CompletePublishAsync(request.Metadata.EventId);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.DeadLetterPublishAsync(request.Metadata.EventId, CreateFailure()));

        Assert.Contains("Published", exception.Message, StringComparison.Ordinal);
        var stored = await repository.GetByEventIdAsync(request.Metadata.EventId);
        Assert.NotNull(stored);
        Assert.Equal(OutboxEventStatus.Published, stored.Status);
        Assert.Equal(0, stored.AttemptCount);
        Assert.Null(stored.LastError);
    }

    [Fact]
    public async Task UnknownEvent_CannotBeDeadLettered()
    {
        await using var harness = await MongoIntegrationHarness.CreateAsync();
        await EnsureOutboxIndexesAsync(harness);
        var repository = new OutboxEventRepository(harness.DbContext, harness.TenantContext);
        var eventId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.DeadLetterPublishAsync(eventId, CreateFailure()));

        Assert.Contains("was not found", exception.Message, StringComparison.Ordinal);
        Assert.Equal(
            0,
            await harness.Database
                .GetCollection<MongoDB.Bson.BsonDocument>("outbox_events")
                .CountDocumentsAsync(MongoDB.Driver.FilterDefinition<MongoDB.Bson.BsonDocument>.Empty));
    }

    [Fact]
    public async Task RepeatingTerminalDispositionWithDifferentReason_IsConflictAndPreservesProvenance()
    {
        await using var harness = await MongoIntegrationHarness.CreateAsync();
        await EnsureOutboxIndexesAsync(harness);
        var repository = new OutboxEventRepository(harness.DbContext, harness.TenantContext);
        var request = CreateRequest(harness.TenantId);
        var initialFailure = CreateFailure();

        await repository.EnqueueAsync(request);
        await repository.ClaimForPublishAsync(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(-5));
        await repository.DeadLetterPublishAsync(request.Metadata.EventId, initialFailure);
        var initial = await repository.GetByEventIdAsync(request.Metadata.EventId);

        var conflictingFailure = new EventOutboxTerminalFailure(
            EventOutboxTerminalFailureKind.Security,
            "signature.invalid",
            "signature rejected");
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.DeadLetterPublishAsync(request.Metadata.EventId, conflictingFailure));

        Assert.Contains("DeadLettered", exception.Message, StringComparison.Ordinal);
        var stored = await repository.GetByEventIdAsync(request.Metadata.EventId);
        Assert.NotNull(initial);
        Assert.NotNull(stored);
        Assert.Equal(OutboxEventStatus.DeadLettered, stored.Status);
        Assert.Equal(1, stored.AttemptCount);
        Assert.Equal(initial.LastError, stored.LastError);
        Assert.Equal(initial.UpdatedAt, stored.UpdatedAt);
        Assert.DoesNotContain("signature.invalid", stored.LastError, StringComparison.Ordinal);
    }

    private static EventOutboxWriteRequest CreateRequest(Guid tenantId)
    {
        var metadata = new EventMetadata(
            Guid.NewGuid(),
            "test.canonical.v1",
            1,
            Guid.NewGuid(),
            null,
            tenantId,
            "Diten.Platform.Tests",
            DateTimeOffset.UtcNow);
        var trusted = new TrustedTransportMetadata(
        [
            new(TrustedTransportMetadata.SignatureSchemeHeader, "hmac-sha256-v1"),
            new(TrustedTransportMetadata.KeyIdHeader, "key-1"),
            new(TrustedTransportMetadata.SignatureHeader, new string('c', 64))
        ]);
        return new EventOutboxWriteRequest(
            metadata,
            Encoding.UTF8.GetBytes("{\"a\":1}"),
            trusted);
    }

    private static EventOutboxTerminalFailure CreateFailure()
    {
        return new EventOutboxTerminalFailure(
            EventOutboxTerminalFailureKind.Contract,
            "contract.invalid",
            "payload rejected");
    }

    private static Task EnsureOutboxIndexesAsync(MongoIntegrationHarness harness)
    {
        var collection = harness.Database.GetCollection<OutboxEvent>("outbox_events");
        return collection.Indexes.CreateManyAsync(
        [
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
        ]);
    }
}
