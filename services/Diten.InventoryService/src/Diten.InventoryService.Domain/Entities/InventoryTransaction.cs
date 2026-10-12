using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Entities;

/// <summary>
/// Immutable line-level movement fact (DEC-INV-15, pack §4). Collection <c>inventory_transactions</c> is
/// INSERT-ONLY: no update, replace or delete path exists in any layer (pack §8, AC17). A correction is a new
/// REVERSAL transaction (ASSUMPTION-0173-05); the original document is never touched, which is why the
/// "reversed by" link is derived at read time from <see cref="ReversesTransactionId"/> instead of being written
/// back onto the original.
/// </summary>
public sealed class InventoryTransaction : EntityBase
{
    /// <summary>Server-minted <c>INV-{yyyy}-{0000000}</c>, unique per Tenant + LE (atomic counter).</summary>
    public string TransactionNumber { get; set; } = string.Empty;

    public string? DocumentGroupId { get; set; }

    public MovementType MovementType { get; set; }

    /// <summary>Opaque MOD-0048 reference code; not validated in S1 (pack §2, §20).</summary>
    public string? MovementReason { get; set; }

    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    public string? FromWarehouseId { get; set; }

    public string? FromLocationId { get; set; }

    public string? ToWarehouseId { get; set; }

    public string? ToLocationId { get; set; }

    /// <summary>Quantity in <see cref="UomId"/>, as posted (decimal, never float).</summary>
    public decimal Quantity { get; set; }

    public string UomId { get; set; } = string.Empty;

    /// <summary>Server-computed via PRODUCT-MASTER <c>getSkuUom</c>; never client-supplied.</summary>
    public decimal BaseQuantity { get; set; }

    public string BaseUomId { get; set; } = string.Empty;

    public string? LotId { get; set; }

    public List<string>? SerialIds { get; set; }

    public StockStatus? FromStockStatus { get; set; }

    public StockStatus? ToStockStatus { get; set; }

    public string SourceModule { get; set; } = string.Empty;

    public string SourceType { get; set; } = string.Empty;

    public string? SourceDocumentId { get; set; }

    public string? SourceLineId { get; set; }

    /// <summary>DEC-INV-19 external-integration hook.</summary>
    public string? SourceSystem { get; set; }

    /// <summary>DEC-INV-19 external reference; stored nullable. The frozen v1 request carries no field for it.</summary>
    public string? ExternalRef { get; set; }

    /// <summary>Set only on a REVERSAL: the original transaction it inverts (unique per Tenant + LE).</summary>
    public Guid? ReversesTransactionId { get; set; }

    public DateTimeOffset TransactionDate { get; set; }

    public DateTimeOffset PostingDate { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>SHA-256 of the canonicalised request; distinguishes a replay from a key reuse.</summary>
    public string RequestFingerprint { get; set; } = string.Empty;

    public Guid CorrelationId { get; set; }

    /// <summary>JWT <c>sub</c> of the poster.</summary>
    public Guid CreatedBy { get; set; }

    /// <summary>The durable result returned to the caller (and replayed verbatim on an idempotent retry).</summary>
    public BalanceSnapshot ResultSnapshot { get; set; } = new();
}
