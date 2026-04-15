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

    // All CRUD methods inherited from RepositoryBase<SampleEntity> via IRepository<SampleEntity>
}
