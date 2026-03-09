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

    public Task<LegalEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => FindByIdAsync(id, cancellationToken);

    public new Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => base.ExistsAsync(id, cancellationToken);

    public async Task<IEnumerable<LegalEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var result = await FindAllAsync(cancellationToken);
        return result;
    }

    public Task<LegalEntity> CreateAsync(LegalEntity entity, CancellationToken cancellationToken = default)
        => InsertAsync(entity, cancellationToken);

    public async Task<bool> UpdateAsync(LegalEntity entity, CancellationToken cancellationToken = default)
    {
        var filter = Builders<LegalEntity>.Filter.And(
            TenantFilter,
            Builders<LegalEntity>.Filter.Eq(e => e.Id, entity.Id));

        var result = await Collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<LegalEntity>.Filter.And(
            TenantFilter,
            Builders<LegalEntity>.Filter.Eq(e => e.Id, id));

        var update = Builders<LegalEntity>.Update
            .Set(e => e.IsDeleted, true)
            .Set(e => e.DeletedAt, DateTimeOffset.UtcNow);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }
}
