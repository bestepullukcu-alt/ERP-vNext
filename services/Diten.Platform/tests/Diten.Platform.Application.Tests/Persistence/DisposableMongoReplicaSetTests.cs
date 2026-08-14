using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.Persistence;

[Collection(DisposableMongoReplicaSetCollection.Name)]
public sealed class DisposableMongoReplicaSetTests
{
    [Fact]
    public async Task StartsOnSafeDynamicPort_AndCommitsTransaction()
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        Assert.True(replicaSet.Port >= 27022);
        Assert.NotEqual(27017, replicaSet.Port);
        Assert.NotEqual(27018, replicaSet.Port);

        var database = replicaSet.CreateDatabase();
        var collection = database.GetCollection<BsonDocument>("proof");
        using var session = await replicaSet.Client.StartSessionAsync();
        session.StartTransaction();
        await collection.InsertOneAsync(
            session,
            new BsonDocument { { "_id", 1 }, { "value", "committed" } });
        await session.CommitTransactionAsync();

        var stored = await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", 1)).SingleAsync();
        Assert.Equal("committed", stored["value"].AsString);
    }

    [Fact]
    public async Task AbortedTransaction_LeavesZeroResidue()
    {
        await using var replicaSet = await DisposableMongoReplicaSet.StartAsync();
        var database = replicaSet.CreateDatabase();
        var collection = database.GetCollection<BsonDocument>("proof");
        using var session = await replicaSet.Client.StartSessionAsync();
        session.StartTransaction();
        await collection.InsertOneAsync(session, new BsonDocument("_id", 1));
        await session.AbortTransactionAsync();

        Assert.Equal(0, await collection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty));
    }
}
