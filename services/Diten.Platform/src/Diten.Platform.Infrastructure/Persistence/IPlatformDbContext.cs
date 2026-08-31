using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence;

public interface IPlatformDbContext
{
    IMongoClient Client { get; }
    IMongoDatabase Database { get; }
    IMongoCollection<TDocument> GetCollection<TDocument>(string collectionName);
}
