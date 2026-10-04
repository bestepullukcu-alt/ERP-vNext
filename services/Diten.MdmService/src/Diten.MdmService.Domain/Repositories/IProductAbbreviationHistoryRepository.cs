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
    /// <remarks>FIX2 item 6: the key is bound to the entry AND the command, so one key cannot serve two commands.</remarks>
    Task<string?> ClaimCommandKeyAsync(string idempotencyKey, Guid registerEntryId, string commandKind, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Command-key claims are not supported by this repository.");
}
