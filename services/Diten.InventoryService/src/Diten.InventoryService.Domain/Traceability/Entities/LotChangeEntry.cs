using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;

namespace Diten.InventoryService.Domain.Traceability.Entities;

/// <summary>
/// One lot history row (collection <c>trace_lot_history</c>, pack §4) — written in the SAME transaction as the change,
/// insert-only: no update, replace or delete exists (GxP audit trail). One row per created lot, per changed field, per
/// status change and per genealogy link. <see cref="EntityBase.LegalEntityId"/> is the company the actor worked in.
/// A status change also carries its idempotency key + request fingerprint, so a repeated request replays.
/// </summary>
public sealed class LotChangeEntry : EntityBase
{
    public Guid LotId { get; set; }

    public LotChangeAction Action { get; set; }

    public string? Field { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? Reason { get; set; }

    /// <summary>Guid.Empty for the system (expiry sweeper).</summary>
    public Guid ActorUserId { get; set; }

    public string Source { get; set; } = string.Empty;

    public Guid CorrelationId { get; set; }

    public string? LeSource { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public string? IdempotencyKey { get; set; }

    public string? RequestFingerprint { get; set; }
}
