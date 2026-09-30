using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Application.Tests.Persistence;
using Xunit;

namespace Diten.Platform.Application.Tests.Eventing;

public sealed class OutboxEventRepositoryIdempotencyTests
{
    [Fact]
    public async Task SameEventIdAndImmutableContentIsNoOp_ChangedPayloadIsConflict()
    {
        // BL-482 (L6): underscores, not hyphens — a hyphenated scope fell outside the owned grammar, so its database
        // was never swept and never healable. CreateIsolatedAsync now refuses such a scope.
        await using var harness = await MongoIntegrationHarness.CreateIsolatedAsync(
            "eventing_outbox_idempotency",
            SchemaProfile.Eventing);
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
                .CountDocumentsAsync(
                    MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("EventId", metadata.EventId)));
    }
}
