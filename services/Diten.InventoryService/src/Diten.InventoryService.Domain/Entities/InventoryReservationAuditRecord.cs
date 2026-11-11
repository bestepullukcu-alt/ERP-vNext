using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Entities;

/// <summary>Who / what / when / correlation of one reservation change, written to <c>inventory_audit</c> atomically with it.</summary>
public sealed class InventoryReservationAuditRecord : EntityBase
{
    /// <summary>
    /// InventoryReservationCreated · …Released · …Expired · …Consumed · …PartiallyConsumed; MOD-0173-FU03 §4.4: …Refused (a create
    /// refusal) · …ConsumptionRefused (an issue refused by a reservation scope guard). A refusal row is written after the rollback.
    /// </summary>
    public string Action { get; set; } = string.Empty;

    public Guid ReservationId { get; set; }

    public Guid ItemId { get; set; }

    public Guid SkuId { get; set; }

    public SkuLevel SkuLevel { get; set; }

    public decimal ChangedQuantity { get; set; }

    public decimal RemainingQuantity { get; set; }

    /// <summary>Null on a refusal row (no reservation exists — MOD-0173-FU03 §4.4); every other row carries it.</summary>
    public ReservationStatus? Status { get; set; }

    /// <summary>JWT <c>sub</c>; <see cref="Guid.Empty"/> for the expiry sweeper (system actor).</summary>
    public Guid ActorUserId { get; set; }

    public Guid CorrelationId { get; set; }

    public Guid? TransactionId { get; set; }

    /// <summary>
    /// Where the legal entity of this row came from (W-0173-02): <c>token</c> or <c>header-unverified-membership</c>;
    /// null for a system actor (expiry sweeper) and for rows written before W-0173-02.
    /// </summary>
    public string? LeSource { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    // ── MOD-0173-FU03 §4.4 (additive; rows written before it carry none of these and read as Outcome Succeeded) ──

    public string? WarehouseId { get; set; }

    public Guid? LotId { get; set; }

    public string? ClosedReason { get; set; }

    /// <summary>AUD-001 §3 vocabulary: <c>Succeeded</c> | <c>Failed</c> (every refusal of this pack) | <c>Denied</c> (not written here).</summary>
    public string? Outcome { get; set; }

    public string? ErrorCode { get; set; }

    public decimal? RequestedQuantity { get; set; }

    /// <summary>The free quantity of the refusing level (<c>details.available</c>).</summary>
    public decimal? UncoveredQuantity { get; set; }

    /// <summary><c>SKU</c> | <c>WAREHOUSE</c> | <c>LOT</c> — the level that refused.</summary>
    public string? Level { get; set; }

    /// <summary>On a consumption refusal: the movement type (wire name).</summary>
    public string? MovementType { get; set; }

    /// <summary>On a consumption refusal: the <c>reservationId</c> text the client sent, as sent (≤ 100 characters); null when none.</summary>
    public string? ReservationIdText { get; set; }

    public string? SourceModule { get; set; }

    /// <summary>AUD-001 §3 field 2: the actor's name at the time (<c>IPostingActorName.Current</c>), only when the actor is the scope user.</summary>
    public string? ActorDisplayName { get; set; }
}
