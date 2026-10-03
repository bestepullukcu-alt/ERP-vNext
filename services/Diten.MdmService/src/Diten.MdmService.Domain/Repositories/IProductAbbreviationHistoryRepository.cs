using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface IProductAbbreviationHistoryRepository
{
    Task<bool> AppendIfAbsentAsync(ProductAbbreviationHistoryEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductAbbreviationHistoryEntry>> GetForRegisterEntryAsync(Guid registerEntryId, CancellationToken cancellationToken = default);
    /// <summary>
    /// FIX1 S3 — binds an idempotency key to one register entry, before anything is written under it. Null when the
    /// key is free or already this entry's; <c>ABBREVIATION_IDEMPOTENCY_KEY_REUSED</c> when another entry owns it.
    /// </summary>
    Task<string?> ClaimCommandKeyAsync(string idempotencyKey, Guid registerEntryId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Command-key claims are not supported by this repository.");
}
