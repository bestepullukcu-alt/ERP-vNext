using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class StandaloneMongoTransactionFailClosedTests
{
    [Fact]
    public async Task StandaloneMongo_ReturnsTyped503_WithAllParticipantResidueZero()
    {
        await using var mongo = await DisposableStandaloneMongo.StartAsync();
        Assert.True(mongo.Port >= 27022);
        Assert.NotEqual(27017, mongo.Port);
        Assert.NotEqual(27018, mongo.Port);
        var database = mongo.CreateDatabase();
        var context = new PlatformDbContext(mongo.Client, database);
        var executor = new PlatformTransactionExecutor(context);
        var names = new[] { "entitlement", "quota_usage", "quota_event", "counter", "integration_outbox", "audit_outbox" };

        var error = await Assert.ThrowsAsync<PlatformTransactionUnavailableException>(() => executor.ExecuteAsync(async (session, ct) =>
        {
            var handle = PlatformMongoTransactionSession.Require(session, context);
            foreach (var name in names)
                await database.GetCollection<BsonDocument>(name).InsertOneAsync(handle, new BsonDocument("value", 1), cancellationToken: ct);
            return true;
        }));

        Assert.Equal(503, error.StatusCode);
        foreach (var name in names)
            Assert.Equal(0, await database.GetCollection<BsonDocument>(name).CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }
}
