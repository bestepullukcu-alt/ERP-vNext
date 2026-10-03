using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

public sealed class ProductAbbreviationRegisterEntry : EntityBase, IAuditIntentAggregate
{
    public string NormalizedAbbreviation { get; set; } = string.Empty;
    public Guid GlobalProductId { get; set; }
    public Guid AllocationLedgerId { get; set; }
    public string AllocationIdempotencyKey { get; set; } = string.Empty;
    public ProductAbbreviationLifecycleStatus LifecycleStatus { get; set; }
        = ProductAbbreviationLifecycleStatus.REQUESTED;
    public string RequestedByCanonicalSubjectId { get; set; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; set; }
    public Guid? ReplacesEntryId { get; set; }
    /// <summary>
    /// On an ACTIVE entry: the correction entry that claimed it (FIX1 B3). A second correction or a retirement
    /// request is refused while the claimed entry is still REQUESTED; a claim whose entry is no longer REQUESTED
    /// (approved, rejected, cancelled, never inserted) is stale and may be taken over. Not a version change.
    /// </summary>
    public Guid? PendingCorrectionEntryId { get; set; }
    public string? RetirementRequestId { get; set; }
    public string? RetirementRequestedByCanonicalSubjectId { get; set; }
    public DateTimeOffset? RetirementRequestedAtUtc { get; set; }
    public string? LastDecisionByCanonicalSubjectId { get; set; }
    public string? LastDecisionIdempotencyKey { get; set; }
    public string? LastDecisionReason { get; set; }
    public DateTimeOffset? LastDecisionAtUtc { get; set; }
    public List<LocalAuditIntent> AuditIntents { get; set; } = [];
    public List<LocalAuditIntentReceipt> AuditIntentReceipts { get; set; } = [];
}
