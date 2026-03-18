using Diten.Platform.Application.Contracts;
using Diten.Platform.Infrastructure.Persistence;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public abstract class RepositoryBase<TEntity>
{
    protected readonly IMongoCollection<TEntity> Collection;
    protected readonly ITenantContext TenantContext;

    protected RepositoryBase(IPlatformDbContext platformDbContext, ITenantContext tenantContext, string collectionName)
    {
        Collection = platformDbContext.GetCollection<TEntity>(collectionName);
        TenantContext = tenantContext;
    }
}
