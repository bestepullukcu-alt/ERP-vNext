using Diten.MdmService.Application.Common;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
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

    public async Task<IReadOnlyList<LegalEntity>> GetReferenceableAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<LegalEntity>.Filter.And(
            TenantFilter,
            Builders<LegalEntity>.Filter.Eq(x => x.LifecycleStatus, LegalEntityLifecycleStatus.Active));

        return await Collection.Find(filter).SortBy(x => x.LegalName).ToListAsync(cancellationToken);
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
    }
}
