using Diten.Platform.Domain.Entities.DocumentRepository;

namespace Diten.Platform.Domain.Repositories;

/// <summary>
/// MOD-0262-FU01 — repository contract for the document repository's own object rows. Every operation is
/// tenant-scoped by the execution filter: a row belonging to another tenant is not merely forbidden, it is
/// invisible, so a cross-tenant lookup surfaces as "not found" rather than disclosing existence.
/// <para>⛔ There is deliberately no destroy/purge operation here (DCP-008 AD-6). Compensation for a failed
/// metadata commit is the soft <see cref="MarkCompensatedAsync"/>; physical destruction is MOD-0262-FU05.</para>
/// </summary>
public interface IRepositoryObjectRepository
{
    Task<RepositoryObject> CreateAsync(RepositoryObject repositoryObject, CancellationToken ct = default);

    /// <summary>Resolves by the client-facing content id. Returns <c>null</c> for another tenant's object.</summary>
    Task<RepositoryObject?> GetByContentIdAsync(Guid contentId, CancellationToken ct = default);

    Task<IReadOnlyList<RepositoryObject>> GetByOwningItemAsync(Guid owningItemId, CancellationToken ct = default);

    /// <summary>Soft-marks the row after a failed metadata commit; never removes the physical object.</summary>
    Task<bool> MarkCompensatedAsync(Guid contentId, CancellationToken ct = default);
}
