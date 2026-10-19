namespace Diten.MdmService.Domain.ValueObjects;

/// <summary>
/// FG FIX1 item 1 (MOD-0290 s21.2 / s21.5): the immutable, audited decision that cancelled a Draft Finished Good. The FG
/// is retained (never deleted, its code never handed out again). The decision alone is NOT the end: the cancellation is
/// pending its audit until exactly one validated central receipt of <see cref="IntentId"/> is on the FG — only then does
/// it stop blocking its parent GSKU (FinishedGoodDraftCancellationProof). No flag here says "finalized".
/// </summary>
public sealed class FinishedGoodDraftCancellation
{
    public Guid OperationId { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public Guid IntentId { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string EvidenceHash { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public Guid RequesterSubjectId { get; set; }
    public int BaseVersion { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; }
}

/// <summary>FG FIX1 item 1: what the cancellation means for reads and the parent blocker (derived, never stored).</summary>
public enum FinishedGoodCancellationDisposition
{
    None = 0,
    PendingAudit = 1,
    Cancelled = 2
}
