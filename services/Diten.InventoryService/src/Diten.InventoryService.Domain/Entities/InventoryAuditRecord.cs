using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Entities;

/// <summary>
/// Who / what / when / correlation of one posting (MOD-0021 pattern, pack §3). Written in the same Mongo
/// transaction as the ledger insert, so an audit row exists if and only if the posting committed.
/// </summary>
public sealed class InventoryAuditRecord : EntityBase
{
    public string Action { get; set; } = string.Empty;

    public Guid TransactionId { get; set; }

    public string TransactionNumber { get; set; } = string.Empty;

    public MovementType MovementType { get; set; }

    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public decimal BaseQuantity { get; set; }

    public string BaseUomId { get; set; } = string.Empty;

    public Guid ActorUserId { get; set; }

    public Guid CorrelationId { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string SourceModule { get; set; } = string.Empty;

    public string? SourceDocumentId { get; set; }

    public string? SourceSystem { get; set; }

    /// <summary>
    /// Where the legal entity of this row came from (W-0173-02): <c>token</c> or <c>header-unverified-membership</c>;
    /// null for a system actor (expiry sweeper) and for rows written before W-0173-02.
    /// </summary>
    public string? LeSource { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
