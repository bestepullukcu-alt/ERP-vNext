using Diten.InventoryService.Domain.Entities;

namespace Diten.InventoryService.Domain.Quarantine.Entities;

/// <summary>
/// One <c>updateHold</c> (QUARANTINE-BUNDLE 1.1.0, FU-0175-Q5-01): the analysis note / attachment references BEFORE and AFTER
/// the edit, who, when, which version, which correlation id. Written to <c>inventory_audit</c> — the InventoryService audit row
/// of pack §8 "Audit" — in the same transaction as the hold. INSERT ONLY: nothing updates or deletes it.
/// No <c>QuarantineHistoryEntry</c> is written for this command: the frozen <c>HistoryAction</c> enum has no value for a note
/// edit, and the owner chose "audit only" over a contract change (WP-0174-ITEM, open point O-ITEM-1).
/// The idempotency key + request fingerprint live here too: a repeated key answers the current hold (same body) or 400.
/// </summary>
public sealed class QuarantineHoldUpdateAuditRecord : EntityBase
{
    /// <summary><c>QuarantineContract.HoldUpdatedAuditAction</c> (a declared 0175 constant).</summary>
    public string Action { get; set; } = string.Empty;

    public Guid HoldId { get; set; }

    public string HoldNumber { get; set; } = string.Empty;

    public Guid UserId { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestFingerprint { get; set; } = string.Empty;

    public int PreviousVersion { get; set; }

    public string? PreviousAnalysisNote { get; set; }

    public string? AnalysisNote { get; set; }

    public List<HoldAttachment> PreviousAttachments { get; set; } = [];

    public List<HoldAttachment> Attachments { get; set; } = [];

    public Guid CorrelationId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
