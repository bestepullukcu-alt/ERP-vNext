using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface ILegalEntityRepository : IRepository<LegalEntity>
{
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LegalEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Tenant-scoped Active (referenceable) legal entities, ordered by legal name — for lookup/dropdown surfaces.</summary>
    Task<IReadOnlyList<LegalEntity>> GetReferenceableAsync(CancellationToken cancellationToken = default);
}
