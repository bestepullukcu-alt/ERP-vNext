using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class SampleRepository : RepositoryBase<SampleEntity>, ISampleRepository
{
    public SampleRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "samples")
    {
    }

    public Task<SampleEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => FindByIdAsync(id, ct);

    public Task<IReadOnlyList<SampleEntity>> GetAllAsync(CancellationToken ct = default)
        => FindAllAsync(ct);

    public Task<SampleEntity> CreateAsync(SampleEntity entity, CancellationToken ct = default)
        => InsertAsync(entity, ct);
}
