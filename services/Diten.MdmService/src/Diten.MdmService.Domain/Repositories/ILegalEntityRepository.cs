using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface ILegalEntityRepository : IRepository<LegalEntity>
{
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Returns all non-deleted legal entities for the current tenant (ordered by code).</summary>
    Task<IReadOnlyList<LegalEntity>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the non-deleted legal entity with the given code in the current tenant, or null.</summary>
    Task<LegalEntity?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}
