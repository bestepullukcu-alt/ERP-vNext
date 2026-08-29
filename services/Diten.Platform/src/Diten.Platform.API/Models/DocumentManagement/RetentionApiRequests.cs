namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU15 — retention / legal hold / disposition API request payloads (JSON from the TenantShell proxy).
// TenantId is never accepted from the client; it is server-side resolved. No payload carries document content —
// every evidence field is a REFERENCE string, never bytes.

public sealed class RetentionPolicyApiRequest
{
    public string PolicyKey { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public string? SubjectType { get; set; }
    public string? RetentionClass { get; set; }
    public int MinimumRetentionYears { get; set; }
    public string? RetentionTrigger { get; set; }
    public bool RetainWhileEffective { get; set; }
    public int? RetainAfterRetirementYears { get; set; }
    public int? RetainAfterSupersessionYears { get; set; }
    public bool IsPermanentRetention { get; set; }
    public string? RegulatoryBasis { get; set; }
    public string? Jurisdiction { get; set; }
    public bool IsLongestApplicableCandidate { get; set; } = true;
}

public sealed class EvaluateRetentionApiRequest
{
    public string SubjectType { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public Guid? RegisterEntryId { get; set; }
    public Guid? ControlledDocumentId { get; set; }

    /// <summary>
    /// Caller-supplied retention trigger date. Required for subject types the resolver cannot reach on its own —
    /// see DocumentRetentionTriggerDateResolver for the resolution table.
    /// </summary>
    public DateTimeOffset? TriggerDate { get; set; }

    public string? RetentionClass { get; set; }
}

public sealed class LegalHoldApiRequest
{
    public string HoldTitle { get; set; } = string.Empty;
    public string? HoldKey { get; set; }
    public string? HoldReason { get; set; }
    public string? ScopeType { get; set; }
    public List<Guid>? RegisterEntryIds { get; set; }
    public List<Guid>? ControlledDocumentIds { get; set; }
    public List<string>? SubjectTypes { get; set; }
    public List<Guid>? ExternalDocumentIds { get; set; }
    public string? ScopeDescription { get; set; }
    public Guid? IssuedByLegalUserId { get; set; }
    public string? IssuedByLegalRole { get; set; }
    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveUntil { get; set; }
}

public sealed class ActivateLegalHoldApiRequest
{
    public string LegalApprovalEvidenceReference { get; set; } = string.Empty;
    public Guid? GqdConcurrenceUserId { get; set; }
    public string? GqdConcurrenceEvidenceReference { get; set; }
}

/// <summary>SOP §22 — BOTH references are mandatory; a release with only one is refused.</summary>
public sealed class ReleaseLegalHoldApiRequest
{
    public string ReleaseLegalApprovalReference { get; set; } = string.Empty;
    public string ReleaseGqdConcurrenceReference { get; set; } = string.Empty;
}

public sealed class AddLegalHoldSubjectApiRequest
{
    public string SubjectType { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public Guid? RegisterEntryId { get; set; }
}

public sealed class DispositionRequestApiRequest
{
    public string SubjectType { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public Guid? RegisterEntryId { get; set; }
    public string? Comment { get; set; }
}

public sealed class ApproveDispositionApiRequest
{
    public string ApprovalEvidenceReference { get; set; } = string.Empty;
    public Guid? ApprovedByUserId { get; set; }
}

public sealed class RejectDispositionApiRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class ExecuteDispositionMarkerApiRequest
{
    public string ExecutionEvidenceReference { get; set; } = string.Empty;
}
