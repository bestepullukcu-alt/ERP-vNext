using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface IProductDefinitionRevisionRepository
{
    Task<ProductDefinitionRevision?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    async Task<IReadOnlyList<ProductDefinitionRevision>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ProductDefinitionRevision>();
        foreach (var id in ids)
        {
            var item = await GetByIdAsync(id, cancellationToken);
            if (item is not null)
            {
                results.Add(item);
            }
        }

        return results;
    }
    /// <summary>WP-VW-W4 (W4-FIX-d, G-8) — the ids of the tenant's active revisions of these global products.</summary>
    Task<IReadOnlyList<Guid>> FindIdsByGlobalProductIdsAsync(
        IReadOnlyCollection<Guid> globalProductIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());
    Task<ProductDefinitionRevision?> GetByCreationCommandIdAsync(
        string creationCommandId,
        CancellationToken cancellationToken = default);
    Task<FirstGskuPairAllocationResult> AllocateForFirstGskuAsync(
        Guid globalProductId,
        string creationCommandId,
        CancellationToken cancellationToken = default);
    Task<ProductDefinitionRevisionCreateResult> CreateForFirstGskuAsync(
        ProductDefinitionRevision revision,
        CancellationToken cancellationToken = default);
}

public sealed record ProductDefinitionRevisionCreateResult(
    bool Succeeded,
    ProductDefinitionRevision? Revision,
    string? ErrorCode = null);

public sealed record FirstGskuPairAllocationResult(
    Guid RevisionId,
    Guid GskuId,
    int RevisionOrdinal,
    string RevisionIdentifier);
