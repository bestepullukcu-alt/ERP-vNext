using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU13 — a retirement case for a Document Master Register entry (GMG-QMS-SOP-0001 §9.16: "Retirement requires
/// justification, transition assessment, communication and archival"). Retired is terminal and the code/UID are
/// RETAINED and never reused (the FU07 invariant is preserved — retirement never clears or frees an identifier).
/// Executing the case delegates the lifecycle change to the FU08 engine. Never hard-deleted; nothing is destroyed.
/// </summary>
public sealed class DocumentRetirementCase : TenantScopedEntity
{
    public required Guid RegisterEntryId { get; set; }

    public int CaseNumber { get; set; }
    public RetirementCaseStatus CaseStatus { get; set; } = RetirementCaseStatus.Requested;

    public required string RetirementReason { get; set; }

    /// <summary>Mandatory before approval (SOP §9.16).</summary>
    public required string JustificationReference { get; set; }
    public required string TransitionAssessmentReference { get; set; }

    /// <summary>Mandatory before execution (SOP §9.16).</summary>
    public string? CommunicationEvidenceReference { get; set; }
    public string? ArchivalEvidenceReference { get; set; }

    /// <summary>When the retirement has a replacement, its identity is linked (the retired identifier is never reused).</summary>
    public string? ReplacementDocumentUid { get; set; }
    public string? ReplacementDocumentCode { get; set; }

    public string? ApprovedBy { get; set; }
    public ApprovalRequiredRole? ApprovedByRole { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }

    public DateTimeOffset? ExecutedAt { get; set; }
    public string? ExecutedBy { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
