using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.PpmService.IntegrationTests;


[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class PpmMongoCollection : ICollectionFixture<PpmDisposableMongo>
{
    public const string CollectionName = "MOD-0117-disposable-Mongo";
}

internal static class PpmMongoTestDatabase
{
    public static IMongoDatabase Open(string connectionString) =>
        new MongoClient(connectionString).GetDatabase(PpmDisposableMongo.DatabaseName);

    public static async Task ResetAsync(IMongoDatabase database)
    {
        using var cursor = await database.ListCollectionNamesAsync();
        foreach (var collectionName in await cursor.ToListAsync())
            await database.GetCollection<BsonDocument>(collectionName)
                .DeleteManyAsync(FilterDefinition<BsonDocument>.Empty);
    }
}
