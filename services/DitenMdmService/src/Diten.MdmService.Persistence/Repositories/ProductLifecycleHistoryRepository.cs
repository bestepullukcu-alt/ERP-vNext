using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class ProductLifecycleHistoryRepository : RepositoryBase<ProductLifecycleHistory>, IProductLifecycleHistoryRepository
{
    public ProductLifecycleHistoryRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "product_lifecycle_history")
    {
        var indexKeys = Builders<ProductLifecycleHistory>.IndexKeys
            .Ascending(x => x.TenantId)
            .Ascending(x => x.ProductId)
            .Descending(x => x.ChangedAt);
        Collection.Indexes.CreateOne(new CreateIndexModel<ProductLifecycleHistory>(indexKeys));
    }

    public async Task<ProductLifecycleHistory> CreateAsync(ProductLifecycleHistory entity, CancellationToken cancellationToken = default)
    {
        entity.ChangedAt = DateTimeOffset.UtcNow;
        return await InsertAsync(entity, cancellationToken);
    }
}
