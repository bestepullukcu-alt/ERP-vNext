using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class LegalEntityRepository : RepositoryBase<LegalEntity>, ILegalEntityRepository
{
    public LegalEntityRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "mdm_legal_entities")
    {
        EnsureIndexes();
    }

    public async Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var filter = Builders<LegalEntity>.Filter.And(
            TenantFilter,
            Builders<LegalEntity>.Filter.Eq(x => x.Code, code));

        if (excludeId.HasValue)
        {
            filter &= Builders<LegalEntity>.Filter.Ne(x => x.Id, excludeId.Value);
        }

        return await Collection.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LegalEntity>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await Collection.Find(TenantFilter)
            .SortBy(x => x.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<LegalEntity?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var filter = Builders<LegalEntity>.Filter.And(
            TenantFilter,
            Builders<LegalEntity>.Filter.Eq(x => x.Code, code));
        return await Collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    private void EnsureIndexes()
    {
        var keys = Builders<LegalEntity>.IndexKeys
            .Ascending(x => x.TenantId)
            .Ascending(x => x.Code);

        var options = new CreateIndexOptions
        {
            Unique = true,
            Name = "ux_mdm_legal_entities_tenant_code"
        };

        Collection.Indexes.CreateOne(new CreateIndexModel<LegalEntity>(keys, options));

        // Non-unique index supporting parent/children hierarchy lookups (additive).
        Collection.Indexes.CreateOne(new CreateIndexModel<LegalEntity>(
            Builders<LegalEntity>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ParentId),
            new CreateIndexOptions { Name = "ix_mdm_legal_entities_tenant_parent" }));
    }
}
