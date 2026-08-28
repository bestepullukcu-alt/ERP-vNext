namespace Diten.Platform.API.Models.DocumentManagement;

// MOD-0029-FU22 — quality event / deviation / CAPA API request payloads (JSON from the TenantShell proxy).
// TenantId is never accepted from the client; it is server-side resolved. Every evidence field is a REFERENCE —
// no investigation report, completion record or effectiveness document content is ever transmitted or stored.

public sealed class CreateQualityEventApiRequest
{
    public string EventTitle { get; set; } = string.Empty;
    public string EventDescription { get; set; } = string.Empty;
    public string? EventType { get; set; }
    public string? EventSeverity { get; set; }
    public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }

    /// <summary>Mandatory for any non-manual source.</summary>
    public string? DetectionEvidenceReference { get; set; }

    public Guid? RegisterEntryId { get; set; }
    public Guid? ControlledDocumentId { get; set; }
    public Guid? TemplateVariantId { get; set; }
    public Guid? ExternalDocumentId { get; set; }
    public string? DetectedBy { get; set; }
    public bool ImmediateContainmentRequired { get; set; }
    public string? ImmediateContainmentSummary { get; set; }
    public bool RequiresDeviation { get; set; }
    public bool RequiresCAPA { get; set; }

    /// <summary>Required to raise a CRITICAL event without a deviation. The decision is recorded, not hidden.</summary>
    public string? DeviationWaiverJustification { get; set; }
    public string? DeviationWaiverEvidenceReference { get; set; }

    /// <summary>EXTENSION POINT for a future external QMS record id. FU22 never calls an external QMS.</summary>
    public string? ExternalQualitySystemReference { get; set; }
}

public sealed class CloseQualityEventApiRequest
{
    public string ClosureEvidenceReference { get; set; } = string.Empty;
    public string? ClosureSummary { get; set; }
}

public sealed class CancelQualityRecordApiRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class LinkQualityEventSourceApiRequest
{
    public string SourceType { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public string? EventType { get; set; }
    public Guid? RegisterEntryId { get; set; }
    public string? SourceReferenceSnapshot { get; set; }
    public string? Notes { get; set; }
}

public sealed class CreateDeviationApiRequest
{
    public Guid QualityEventId { get; set; }
    public string DeviationTitle { get; set; } = string.Empty;
    public string DeviationDescription { get; set; } = string.Empty;
    public string? DeviationCategory { get; set; }
    public string? DeviationSeverity { get; set; }
    public DateTimeOffset? OccurredAt { get; set; }
    public string? ReportedBy { get; set; }
    public bool RequiresCAPA { get; set; }
}

public sealed class RecordDeviationInvestigationApiRequest
{
    public string? RootCauseSummary { get; set; }
    public string? RootCauseCategory { get; set; }
    public string? ImpactAssessmentSummary { get; set; }
    public string? PatientProductRegulatoryImpact { get; set; }
    public string? InvestigationEvidenceReference { get; set; }
}

public sealed class CloseDeviationApiRequest
{
    public string ClosureEvidenceReference { get; set; } = string.Empty;

    /// <summary>Documented basis for closing despite an outstanding CAPA requirement.</summary>
    public string? ClosureExceptionJustification { get; set; }
}

public sealed class CreateCapaActionApiRequest
{
    public Guid? QualityEventId { get; set; }
    public Guid? DeviationId { get; set; }
    public string? ActionType { get; set; }
    public string ActionTitle { get; set; } = string.Empty;
    public string ActionDescription { get; set; } = string.Empty;
    public Guid? ActionOwnerUserId { get; set; }
    public string? ActionOwnerRole { get; set; }

    /// <summary>Mandatory for a corrective or preventive action.</summary>
    public DateTimeOffset? DueDate { get; set; }

    public bool EffectivenessCheckRequired { get; set; }
    public DateTimeOffset? EffectivenessDueDate { get; set; }
    public List<Guid>? RelatedRegisterEntryIds { get; set; }
    public List<Guid>? RelatedControlledDocumentIds { get; set; }
    public List<Guid>? RelatedExternalDocumentIds { get; set; }
}

public sealed class CompleteCapaActionApiRequest
{
    public string CompletionEvidenceReference { get; set; } = string.Empty;
    public string? Comment { get; set; }
}

public sealed class RecordCapaEffectivenessApiRequest
{
    /// <summary>Effective or Ineffective. An ineffective action can never be closed as effective.</summary>
    public string EffectivenessResult { get; set; } = string.Empty;

    public string EffectivenessEvidenceReference { get; set; } = string.Empty;
    public string? EffectivenessSummary { get; set; }
}

public sealed class CloseCapaActionApiRequest
{
    /// <summary>Required to close an ineffective or incomplete action.</summary>
    public string? ClosureExceptionJustification { get; set; }
}

public sealed class BridgeFromSourceApiRequest
{
    public string SourceType { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public string? TriggerReason { get; set; }

    /// <summary>May only RAISE the mapped severity; a request to lower it is ignored and noted.</summary>
    public string? SeverityOverride { get; set; }

    public string? DetectionEvidenceReference { get; set; }
}

public sealed class BridgeSeverityOverrideApiRequest
{
    public string? SeverityOverride { get; set; }
}
