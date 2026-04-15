using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class CompositionRepository : RepositoryBase<Composition>, ICompositionRepository
{
    public CompositionRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "compositions")
    {
        var indexKeys = Builders<Composition>.IndexKeys
            .Ascending(x => x.TenantId)
            .Ascending(x => x.FormulationCode)
            .Ascending(x => x.IsDeleted);
        Collection.Indexes.CreateOne(new CreateIndexModel<Composition>(indexKeys, new CreateIndexOptions { Unique = true }));
    }

    // Override GetAllAsync to apply default sort by FormulationCode
    public override async Task<IReadOnlyList<Composition>> GetAllAsync(CancellationToken ct = default)
    {
        return await Collection.Find(TenantFilter).SortBy(x => x.FormulationCode).ToListAsync(ct);
    }

    public async Task<bool> ExistsByCodeAsync(string formulationCode, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filter = Builders<Composition>.Filter.And(
            TenantFilter,
            Builders<Composition>.Filter.Eq(x => x.FormulationCode, formulationCode));

        if (excludeId.HasValue)
        {
            filter &= Builders<Composition>.Filter.Ne(x => x.Id, excludeId.Value);
        }

        return await Collection.Find(filter).AnyAsync(ct);
    }
}
