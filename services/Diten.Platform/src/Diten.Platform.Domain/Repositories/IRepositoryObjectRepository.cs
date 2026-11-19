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

    /// <summary>
    /// MOD-0262-FU01 DL-2 — the ONE read that also returns a compensated row, still tenant-scoped (another tenant's row is
    /// null). Only the trusted consumer's compensation uses it, so a repeated compensation answers 204 instead of 404 (§8.1
    /// "Telafi tekrarı"). The default body refuses: an in-memory double written before DL-2 never reaches this path; the
    /// Mongo repository implements it.
    /// </summary>
    Task<RepositoryObject?> GetByContentIdIncludingCompensatedAsync(Guid contentId, CancellationToken ct = default) =>
        throw new NotSupportedException(
            "GetByContentIdIncludingCompensatedAsync is implemented by the Mongo repository only (MOD-0262-FU01 DL-2).");
}
