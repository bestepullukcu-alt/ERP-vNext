using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence;

public sealed class PlatformDbContext : IPlatformDbContext
{
    public PlatformDbContext(IMongoClient mongoClient, IMongoDatabase database)
    {
        Client = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
        Database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public IMongoClient Client { get; }
    public IMongoDatabase Database { get; }

    public IMongoCollection<TDocument> GetCollection<TDocument>(string collectionName)
    {
        return Database.GetCollection<TDocument>(collectionName);
    }
}
