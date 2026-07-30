using System.Text;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Infrastructure.Persistence.Configurations;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Diten.Platform.Application.Tests.Persistence;
using Xunit;

namespace Diten.Platform.Application.Tests.Eventing;

public sealed class OutboxEventRepositoryIdempotencyTests
{
    [Fact]
    public async Task SameEventIdAndImmutableContentIsNoOp_ChangedPayloadIsConflict()
    {
        await using var harness = await MongoIntegrationHarness.CreateAsync();
        await MongoDbIndexConfigurations.EnsureIndexesAsync(harness.Database);
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
}
