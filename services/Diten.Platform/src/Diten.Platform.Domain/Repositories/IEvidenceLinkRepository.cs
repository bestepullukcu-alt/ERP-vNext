using Diten.Platform.Domain.Entities.EvidenceLinking;

namespace Diten.Platform.Domain.Repositories;

/// <summary>
/// MOD-0031 slice 1 — evidence links (tenant scoped by the ambient tenant context, soft-delete aware). Writes take the
/// active Platform transaction session so the link and its outbox event commit together. No delete and no update of
/// the link body: <see cref="MarkRemovedAsync"/> is the only mutation.
/// </summary>
public interface IEvidenceLinkRepository
{
    Task<EvidenceLink?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<EvidenceLink>> ListByObjectAsync(
        string module, string objectType, string objectId, string? objectVersion, bool includeRemoved,
        CancellationToken ct = default);

    /// <summary>Reverse lookup: ACTIVE links pointing at a document (optionally one version of it).</summary>
    Task<IReadOnlyList<EvidenceLink>> ListActiveByDocumentAsync(
        Guid documentId, Guid? documentVersionId, CancellationToken ct = default);

    Task<EvidenceLink?> FindActiveByDedupKeyAsync(string activeDedupKey, CancellationToken ct = default);

    /// <summary>Inserts inside the transaction. Throws <see cref="EvidenceLinkDuplicateException"/> when the partial
    /// unique index rejects a second identical active link.</summary>
    Task InsertAsync(IPlatformTransactionSession session, EvidenceLink link, CancellationToken ct = default);

    /// <summary>Persists the removal stamp; false when the link was no longer active (concurrent removal).</summary>
    Task<bool> MarkRemovedAsync(IPlatformTransactionSession session, EvidenceLink link, CancellationToken ct = default);
}
