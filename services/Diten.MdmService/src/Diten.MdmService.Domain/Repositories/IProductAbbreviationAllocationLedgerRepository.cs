using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public sealed record ProductAbbreviationAllocationResult(
    bool Succeeded,
    ProductAbbreviationAllocationLedger? Ledger,
    string? ErrorCode = null,
    bool IsReplay = false);

public interface IProductAbbreviationAllocationLedgerRepository
{
    Task<ProductAbbreviationAllocationLedger?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductAbbreviationAllocationLedger?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<ProductAbbreviationAllocationResult> AllocateAsync(
        ProductAbbreviationAllocationLedger allocation,
        CancellationToken cancellationToken = default);
}
