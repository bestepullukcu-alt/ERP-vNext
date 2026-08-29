namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU21 — GDocP correction trail API request payloads (JSON from the TenantShell proxy). TenantId is never
// accepted from the client; it is server-side resolved. Value snapshots are textual field values — never document
// bytes — and every evidence field is a REFERENCE.

public sealed class GDocPCorrectionPolicyApiRequest
{
    public string PolicyKey { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public string? SubjectType { get; set; }

    /// <summary>Field matcher: exact name, <c>prefix*</c>, <c>*suffix</c>, or <c>*</c> for every field.</summary>
    public string FieldPathPattern { get; set; } = "*";

    public bool RequiresCorrectionReason { get; set; } = true;
    public bool RequiresEvidenceReference { get; set; }
    public bool RequiresReview { get; set; }
    public bool RequiresDeviationReferenceForHighRisk { get; set; } = true;
    public bool AllowCorrectionAfterApproval { get; set; } = true;
    public bool AllowCorrectionAfterEffective { get; set; } = true;
    public bool IsBackdatingSensitive { get; set; }
    public bool IsStatusSensitive { get; set; }
    public bool IsEvidenceSensitive { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// MOD-0029-FU21 — NOTE WHAT IS ABSENT: there is no CorrectedAt property. The correction timestamp is stamped by
/// the server, so backdating the correction itself is structurally impossible rather than merely validated.
/// </summary>
public sealed class RecordGDocPCorrectionApiRequest
{
    public string SubjectType { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public string FieldPath { get; set; } = string.Empty;
    public string? FieldDisplayName { get; set; }

    /// <summary>Omit or leave blank when the previous value cannot be established — it becomes an explicit
    /// UNKNOWN_OR_UNAVAILABLE sentinel and raises the correction to high risk.</summary>
    public string? PreviousValueSnapshot { get; set; }

    public string? NewValueSnapshot { get; set; }
    public string? ValueFormat { get; set; }
    public string? CorrectionType { get; set; }
    public string CorrectionReason { get; set; } = string.Empty;
    public string? CorrectionEvidenceReference { get; set; }
    public string? DeviationReference { get; set; }
    public Guid? RegisterEntryId { get; set; }
    public Guid? ControlledDocumentId { get; set; }
    public Guid? CorrectedByUserId { get; set; }
    public string? CorrectedByRole { get; set; }
    public string? RequestedBy { get; set; }

    /// <summary>Lets the caller declare the subject's governance state so policy "allow after" rules can apply.</summary>
    public bool SubjectIsApproved { get; set; }
    public bool SubjectIsEffective { get; set; }
}

public sealed class ReviewGDocPCorrectionApiRequest
{
    public Guid? ReviewerUserId { get; set; }
    public string? ReviewerRole { get; set; }
    public string ReviewEvidenceReference { get; set; } = string.Empty;
    public string? ReviewComment { get; set; }
}

public sealed class RejectGDocPCorrectionApiRequest
{
    public Guid? ReviewerUserId { get; set; }
    public string? ReviewerRole { get; set; }
    public string Reason { get; set; } = string.Empty;
}
