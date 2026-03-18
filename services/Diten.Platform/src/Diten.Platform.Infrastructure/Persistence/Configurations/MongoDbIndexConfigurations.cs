using Diten.Platform.Domain.Entities;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

public static class MongoDbIndexConfigurations
{
    public static async Task EnsureIndexesAsync(IMongoDatabase database)
    {
        var collection = database.GetCollection<SavedView>("saved_views");

        await collection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<SavedView>(
                Builders<SavedView>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.ModuleKey)
                    .Ascending(x => x.PageKey)
                    .Ascending(x => x.Status)),
            new CreateIndexModel<SavedView>(
                Builders<SavedView>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.ModuleKey)
                    .Ascending(x => x.PageKey)
                    .Ascending(x => x.IsDefault)
                    .Ascending(x => x.Status)),
            new CreateIndexModel<SavedView>(
                Builders<SavedView>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.UserId)
                    .Ascending(x => x.ModuleKey)
                    .Ascending(x => x.PageKey)
                    .Ascending(x => x.ViewName)
                    .Ascending(x => x.Status))
        });
    }
}
