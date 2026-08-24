using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Repositories;

public interface IGlobalProductRepository
{
    Task<GlobalProduct?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    async Task<IReadOnlyList<GlobalProduct>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var results = new List<GlobalProduct>();
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
    Task<GlobalProduct?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string normalizedName, CancellationToken cancellationToken = default);
    Task<GlobalProductPage> GetPageAsync(
        int pageNumber,
        int pageSize,
        string? normalizedSearch,
        ProductIdentityLifecycleStatus? lifecycleStatus,
        CancellationToken cancellationToken = default);
    Task<GlobalProductPage> GetReferenceablePageAsync(
        int pageNumber,
        int pageSize,
        string? normalizedSearch,
        CancellationToken cancellationToken = default) =>
        GetPageAsync(pageNumber, pageSize, normalizedSearch, lifecycleStatus: null, cancellationToken);
    Task<GlobalProductCreateResult> CreateDraftAsync(GlobalProduct globalProduct, CancellationToken cancellationToken = default);
}

public sealed record GlobalProductPage(IReadOnlyList<GlobalProduct> Items, long TotalCount);
