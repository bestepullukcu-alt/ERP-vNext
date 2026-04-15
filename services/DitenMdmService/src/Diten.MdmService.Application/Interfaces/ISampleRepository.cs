using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Application.Interfaces;

/// <summary>
/// Repository for SampleEntity operations.
/// All implementations automatically apply tenant filter.
/// </summary>
public interface ISampleRepository : IRepository<SampleEntity>
{
    // No additional specific methods currently needed — standard CRUD inherited from IRepository<SampleEntity>
}
