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
