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
