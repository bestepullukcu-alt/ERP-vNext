using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface IProductAbbreviationHistoryRepository
{
    Task<bool> AppendIfAbsentAsync(ProductAbbreviationHistoryEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductAbbreviationHistoryEntry>> GetForRegisterEntryAsync(Guid registerEntryId, CancellationToken cancellationToken = default);
}
