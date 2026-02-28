using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class LegalEntityRepository : RepositoryBase<LegalEntity>, ILegalEntityRepository
{
    public LegalEntityRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "legal_entities")
    {
    }

    public Task<LegalEntity?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
        => FindByIdAsync(id, cancellationToken);

    public async Task<IEnumerable<LegalEntity>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var result = await FindAllAsync(cancellationToken);
        return result;
    }

    public Task<LegalEntity> CreateAsync(LegalEntity entity, CancellationToken cancellationToken = default)
        => InsertAsync(entity, cancellationToken);

    public async Task<LegalEntity> UpdateAsync(LegalEntity entity, CancellationToken cancellationToken = default)
    {
        var filter = Builders<LegalEntity>.Filter.And(
            TenantFilter,
            Builders<LegalEntity>.Filter.Eq(e => e.Id, entity.Id));

        await Collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return entity;
    }

    public async Task DeleteAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<LegalEntity>.Filter.And(
            TenantFilter,
            Builders<LegalEntity>.Filter.Eq(e => e.Id, id));

        await Collection.DeleteOneAsync(filter, cancellationToken);
    }
}
