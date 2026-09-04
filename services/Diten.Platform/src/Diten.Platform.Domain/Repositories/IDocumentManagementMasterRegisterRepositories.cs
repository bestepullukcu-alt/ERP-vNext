using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

// MOD-0029-FU06 — Document Master Register repository contract. Every method is tenant-scoped via the
// TenantRepository ExecutionFilter; no hard delete (archival is a status change).

/// <summary>MOD-0029-FU06 — tenant-scoped list filter for the Document Master Register.</summary>
public sealed record MasterRegisterListFilter(
    DocumentRegisterStatus? RegisterStatus = null,
    ControlledDocumentLifecycleStatus? LifecycleStatus = null,
    DocumentCriticality? Criticality = null,
    ControlledDocumentClass? DocumentClass = null,
    Guid? OwnerCompanyId = null);

public interface IDocumentMasterRegisterRepository
{
    Task<DocumentMasterRegisterEntry> CreateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default);
    Task<DocumentMasterRegisterEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Duplicate-UID guard. Only meaningful when a UID has been allocated (nullable pre-FU07).</summary>
    Task<DocumentMasterRegisterEntry?> GetByPermanentUidAsync(string permanentUid, CancellationToken ct = default);

    /// <summary>Duplicate-code guard. Only meaningful when a code has been allocated (nullable pre-FU07).</summary>
    Task<DocumentMasterRegisterEntry?> GetByDocumentCodeAsync(string documentCode, CancellationToken ct = default);

    /// <summary>Register reconciliation seam (SOP §20): find the row that projects a given controlled document.</summary>
    Task<DocumentMasterRegisterEntry?> GetByControlledDocumentIdAsync(Guid controlledDocumentId, CancellationToken ct = default);

    /// <summary>
    /// DCP-005 Phase 2 — batch resolve register rows by Permanent UID (effectiveness resolver seam). The default
    /// implementation is a full tenant read + in-memory filter, so every existing implementer keeps working unchanged;
    /// the Mongo repository OVERRIDES this with an indexed <c>$in</c> pushdown. Behaviour is identical either way: only
    /// rows whose (trimmed) PermanentUid is one of <paramref name="permanentUids"/> are returned. Fail-closed: an
    /// infrastructure failure of the underlying read propagates — it is never swallowed here.
    /// </summary>
    async Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetByPermanentUidsAsync(IReadOnlyCollection<string> permanentUids, CancellationToken ct = default)
    {
        var wanted = NormalizeKeys(permanentUids);
        if (wanted.Count == 0)
        {
            return [];
        }

        var all = await GetAllForTenantAsync(ct);
        return all.Where(e => e.PermanentUid is not null && wanted.Contains(e.PermanentUid.Trim())).ToList();
    }

    /// <summary>
    /// DCP-005 Phase 2 — batch resolve register rows by Document Code. Same default-vs-Mongo-<c>$in</c> contract as
    /// <see cref="GetByPermanentUidsAsync"/>.
    /// </summary>
    async Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetByDocumentCodesAsync(IReadOnlyCollection<string> documentCodes, CancellationToken ct = default)
    {
        var wanted = NormalizeKeys(documentCodes);
        if (wanted.Count == 0)
        {
            return [];
        }

        var all = await GetAllForTenantAsync(ct);
        return all.Where(e => e.DocumentCode is not null && wanted.Contains(e.DocumentCode.Trim())).ToList();
    }

    /// <summary>Trim, drop blanks and de-duplicate the requested identifiers (ordinal) — shared by both batch fallbacks.</summary>
    private static HashSet<string> NormalizeKeys(IReadOnlyCollection<string>? keys) =>
        keys is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(keys.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()), StringComparer.Ordinal);

    Task<IReadOnlyList<DocumentMasterRegisterEntry>> ListAsync(MasterRegisterListFilter filter, CancellationToken ct = default);
    Task<IReadOnlyList<DocumentMasterRegisterEntry>> GetAllForTenantAsync(CancellationToken ct = default);

    Task<bool> UpdateAsync(DocumentMasterRegisterEntry entry, CancellationToken ct = default);
}
