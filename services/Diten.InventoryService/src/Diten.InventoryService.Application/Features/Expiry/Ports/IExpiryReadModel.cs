using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Traceability.Entities;

namespace Diten.InventoryService.Application.Features.Expiry.Ports;

/// <summary>
/// One lot in one warehouse with on-hand &gt; 0 (pack §8.8 "lots + balances"): AVAILABLE on-hand vs. on-hand in any other status
/// ("Kullanılabilir" / "Bekleyen", ASSUMPTION-0176-09). <see cref="HeldStatuses"/> names the statuses of the held part.
/// </summary>
public sealed record LotStockRow(Lot Lot, string WarehouseId, string BaseUomId, decimal Available, decimal Held, IReadOnlyCollection<StockStatus> HeldStatuses)
{
    public decimal OnHand => Available + Held;
}

/// <summary>Exception figures of the panel (pack §8.8): active (Open / InReview), of which past due; mean resolution time in the period.</summary>
public sealed record ExpiryExceptionFigures(int Open, int Late, double? AvgResolutionHours);

/// <summary>
/// WP-0176-F4 — the reads behind the panel, the lists and the FEFO order. Every read is scoped to the caller's tenant AND company
/// (pack §8.8, §13 #8); the FEFO candidates are the SAME read the posting hook uses (one candidate reader, one <see cref="FefoOrderRule"/>).
/// </summary>
public interface IExpiryReadModel
{
    /// <summary>Lot × warehouse rows with on-hand &gt; 0 (registered lots only), optionally one warehouse / one SKU.</summary>
    Task<IReadOnlyList<LotStockRow>> LotStockAsync(string? warehouseId, Guid? skuId, CancellationToken cancellationToken = default);

    /// <summary>The FEFO candidates of one company + warehouse + SKU — the posting hook's read (KF-B / KF-F share it).</summary>
    Task<IReadOnlyList<FefoCandidate>> FefoCandidatesAsync(string warehouseId, Guid itemId, Guid skuId, SkuLevel skuLevel, CancellationToken cancellationToken = default);

    /// <summary>FEFO issue snapshots posted in [from, to): (all, compliant), optionally one warehouse.</summary>
    Task<(int Issues, int Compliant)> FefoIssuesAsync(DateTimeOffset from, DateTimeOffset to, string? warehouseId, CancellationToken cancellationToken = default);

    Task<ExpiryExceptionFigures> ExceptionFiguresAsync(DateTimeOffset now, DateTimeOffset periodFrom, string? warehouseId, CancellationToken cancellationToken = default);

    /// <summary>The active (Open / InReview) case per lot + warehouse — the row link "İstisna" (EXPIRED_ON_HAND first).</summary>
    Task<IReadOnlyDictionary<(string LotId, string? WarehouseId), Guid>> ActiveCasesAsync(CancellationToken cancellationToken = default);
}
