namespace Diten.InventoryService.Application.Features.Warehouse.Ports;

/// <summary>
/// The ONE door to MOD-0178's user-visible record codes (pack §8.7, §22; owner decision Q-07 B). Until the RCS-001 shared code service
/// (BL-565) exists, a temporary counter behind this port issues <c>{COMPANY}-{TYPE}-{000001}</c>: one atomic counter per (caller's tenant,
/// company prefix, type), never reset, so a number is unique tenant-wide. The move to BL-565 replaces the implementation only
/// (FU-0178-RCS-01). The counter is not part of the caller's transaction (a shared service could not be either): a number taken by a write
/// that later fails is skipped, never reissued.
/// </summary>
public interface IWarehouseRecordCodeAllocator
{
    /// <summary>
    /// The next number of <paramref name="typePrefix"/> (<c>PUT</c>, <c>PCK</c>, <c>OUT</c>, <c>PKG</c>; MOD-0181 <c>CNT</c> — Q-7 = B) under <paramref name="companyCodePrefix"/>
    /// (already checked against <c>^[A-Z]{2,5}$</c> by the caller — an invalid one is an <see cref="ArgumentException"/>). The counter
    /// store unreachable → <see cref="WarehouseRecordCodeUnavailableException"/> (503 <c>WMS_RECORD_CODE_UNAVAILABLE</c>).
    /// </summary>
    Task<string> NextAsync(string companyCodePrefix, string typePrefix, CancellationToken cancellationToken = default);
}

/// <summary>The counter could not be written; nothing was issued (pack §12 → 503 <c>WMS_RECORD_CODE_UNAVAILABLE</c>).</summary>
public sealed class WarehouseRecordCodeUnavailableException(string message, Exception? inner = null) : Exception(message, inner);
