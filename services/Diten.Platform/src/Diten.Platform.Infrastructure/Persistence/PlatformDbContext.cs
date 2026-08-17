using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence;

public sealed class PlatformDbContext : IPlatformDbContext
{
    public PlatformDbContext(IMongoClient mongoClient, IMongoDatabase database)
    {
        Database = database;
    }

    public IMongoDatabase Database { get; }

    public IMongoCollection<TDocument> GetCollection<TDocument>(string collectionName)
    {
        return Database.GetCollection<TDocument>(collectionName);
    }
}
