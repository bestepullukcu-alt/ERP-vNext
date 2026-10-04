using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface IProductAbbreviationAllocationLedgerRepository
{
    Task<ProductAbbreviationAllocationLedger?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductAbbreviationAllocationLedger?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
}
