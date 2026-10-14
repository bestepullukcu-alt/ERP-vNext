using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface IGskuRepository
{
    Task<Gsku?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Gsku?> GetReferenceableByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Gsku>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);
    Task<GskuPage> GetReferenceablePageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        CancellationToken cancellationToken = default);
    Task<GskuPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? canonicalCodeSearch,
        CancellationToken cancellationToken = default) =>
        GetReferenceablePageAsync(pageNumber, pageSize, canonicalCodeSearch, cancellationToken);
    /// <summary>
    /// WP-VW-W4 (W4-FIX-d, G-8) — the list search: a GSKU whose code CONTAINS the text (<see cref="MdmSearchText"/>) or whose
    /// product definition revision is one of <paramref name="productDefinitionRevisionIds"/> (the revisions of the global
    /// products whose name / code matched). Same tenant, soft-delete and paging rules as <see cref="GetPageAsync"/>.
    /// </summary>
    Task<GskuPage> SearchPageAsync(
        int pageNumber,
        int pageSize,
        string search,
        IReadOnlyCollection<Guid> productDefinitionRevisionIds,
        CancellationToken cancellationToken = default) =>
        GetPageAsync(pageNumber, pageSize, search, cancellationToken);
    Task<IReadOnlyList<Guid>> FindIdsByCanonicalCodeAsync(
        string canonicalCodeSearch,
        CancellationToken cancellationToken = default);
    Task<Gsku?> GetByCreationCommandIdAsync(string creationCommandId, CancellationToken cancellationToken = default);
    Task<GskuCreateResult> CreateDraftAsync(Gsku gsku, CancellationToken cancellationToken = default);
    Task<GskuUpdateResult> UpdateDraftAsync(Gsku gsku, int expectedVersion, CancellationToken cancellationToken = default);
}

public sealed record GskuCreateResult(bool Succeeded, Gsku? Gsku, string? ErrorCode = null);
public sealed record GskuUpdateResult(bool Succeeded, Gsku? Gsku, string? ErrorCode = null);
public sealed record GskuPage(IReadOnlyList<Gsku> Items, long TotalCount);
