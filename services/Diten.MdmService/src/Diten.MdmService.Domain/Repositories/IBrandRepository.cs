using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface IBrandRepository : IRepository<Brand>
{
    /// <summary>
    /// Tenant-scoped code uniqueness. Archived brands are INCLUDED on purpose: BrandCode is stable and
    /// permanently reserved (FU01 §3), so an archived code can never be reused. Keeping archived rows in the
    /// check also lets the unique index stay a plain one — no partial filter, hence no `$ne` (which crashes
    /// MongoDB index creation at startup).
    /// </summary>
    Task<bool> ExistsByCodeAsync(string brandCode, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Brand>> GetAllAsync(CancellationToken cancellationToken = default);
}
