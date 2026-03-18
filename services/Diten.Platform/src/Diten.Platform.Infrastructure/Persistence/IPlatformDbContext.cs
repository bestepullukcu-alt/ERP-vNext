using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence;

public interface IPlatformDbContext
{
    IMongoDatabase Database { get; }
    IMongoCollection<TDocument> GetCollection<TDocument>(string collectionName);
}
