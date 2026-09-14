using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface ILegalEntityRepository : IRepository<LegalEntity>
{
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies the owner-approved editable fields when the tenant-owned, non-deleted record still has the
    /// caller-observed version. Lifecycle, governance, identity, deletion and unknown persisted fields are preserved.
    /// </summary>
    Task<bool> UpdateEditableFieldsAsync(
        LegalEntity proposed,
        int expectedVersion,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LegalEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Tenant-scoped Active (referenceable) legal entities, ordered by legal name — for lookup/dropdown surfaces.</summary>
    Task<IReadOnlyList<LegalEntity>> GetReferenceableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns at most 200 supplied, tenant-scoped, active and non-deleted Legal Entities in canonical ID order.
    /// </summary>
    Task<IReadOnlyList<LegalEntity>> GetReferenceableByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
}
