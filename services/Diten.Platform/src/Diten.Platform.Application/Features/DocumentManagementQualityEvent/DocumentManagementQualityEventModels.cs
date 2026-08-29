using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementQualityEvent;

// MOD-0029-FU22 — quality event / deviation / CAPA bridge contracts, reason codes and wire mapping.

/// <summary>
/// MOD-0029-FU22 — RECOMMENDED Layer 1 RBAC keys. NOT seeded in this FU (no AuthService change): the controller
/// reuses the already-seeded controlled-documents view/create keys. A later hardening FU should seed these —
/// closing a critical deviation is a materially different authority from raising a quality event.
/// </summary>
public static class QualityEventPermissions
{
    public const string QualityEventsView = "platform.document-management.quality-events.view";
    public const string QualityEventsManage = "platform.document-management.quality-events.manage";
    public const string DeviationsView = "platform.document-management.deviations.view";
    public const string DeviationsManage = "platform.document-management.deviations.manage";
    public const string CapaView = "platform.document-management.capa.view";
    public const string CapaManage = "platform.document-management.capa.manage";
    public const string BridgeManage = "platform.document-management.quality-bridge.manage";
}

public static class QualityEventReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string QualityEventNotFound = "QUALITY_EVENT_NOT_FOUND";
    public const string DeviationNotFound = "DEVIATION_NOT_FOUND";
    public const string CapaNotFound = "CAPA_ACTION_NOT_FOUND";
    public const string SourceNotFound = "QUALITY_EVENT_SOURCE_NOT_FOUND";

    public const string TitleRequired = "QUALITY_EVENT_TITLE_REQUIRED";
    public const string DescriptionRequired = "QUALITY_EVENT_DESCRIPTION_REQUIRED";
    public const string DetectionEvidenceRequired = "DETECTION_EVIDENCE_REQUIRED_FOR_NON_MANUAL_SOURCE";
    public const string CriticalRequiresDeviation = "CRITICAL_EVENT_REQUIRES_DEVIATION_OR_JUSTIFICATION";
    public const string ClosureEvidenceRequired = "CLOSURE_EVIDENCE_REQUIRED";
    public const string DeviationNotClosed = "REQUIRED_DEVIATION_NOT_CLOSED";
    public const string CapaNotSettled = "REQUIRED_CAPA_ACTIONS_NOT_SETTLED";
    public const string EventInvalidState = "QUALITY_EVENT_INVALID_STATE";

    public const string DeviationRequiresQualityEvent = "DEVIATION_REQUIRES_QUALITY_EVENT";
    public const string RootCauseRequired = "CRITICAL_DEVIATION_CLOSE_REQUIRES_ROOT_CAUSE";
    public const string ImpactAssessmentRequired = "CRITICAL_DEVIATION_CLOSE_REQUIRES_IMPACT_ASSESSMENT";
    public const string DeviationRequiresCapa = "DEVIATION_REQUIRES_AT_LEAST_ONE_CAPA_ACTION";
    public const string DeviationInvalidState = "DEVIATION_INVALID_STATE";

    public const string CapaRequiresParent = "CAPA_REQUIRES_QUALITY_EVENT_OR_DEVIATION";
    public const string CapaOwnerRequired = "CAPA_OWNER_REQUIRED";
    public const string CapaDueDateRequired = "CAPA_DUE_DATE_REQUIRED_FOR_CORRECTIVE_PREVENTIVE";
    public const string CapaCompletionEvidenceRequired = "CAPA_COMPLETION_EVIDENCE_REQUIRED";
    public const string CapaEffectivenessEvidenceRequired = "CAPA_EFFECTIVENESS_EVIDENCE_REQUIRED";
    public const string CapaEffectivenessPending = "CAPA_CLOSE_BLOCKED_EFFECTIVENESS_PENDING";
    public const string CapaIneffectiveRequiresException = "CAPA_INEFFECTIVE_CLOSE_REQUIRES_EXCEPTION_JUSTIFICATION";
    public const string CapaInvalidState = "CAPA_ACTION_INVALID_STATE";

    public const string ReasonRequired = "REASON_REQUIRED";
    public const string PermissionDenied = "PERMISSION_DENIED";
}

// ── inputs ───────────────────────────────────────────────────────────────────

public sealed record CreateQualityEventInput(
    string EventTitle,
    string EventDescription,
    string? EventType,
    string? EventSeverity,
    string? SourceType,
    Guid? SourceId,
    string? DetectionEvidenceReference,
    Guid? RegisterEntryId,
    Guid? ControlledDocumentId,
    Guid? TemplateVariantId,
    Guid? ExternalDocumentId,
    string? DetectedBy,
    bool ImmediateContainmentRequired,
    string? ImmediateContainmentSummary,
    bool RequiresDeviation,
    bool RequiresCAPA,
    string? DeviationWaiverJustification,
    string? DeviationWaiverEvidenceReference,
    string? ExternalQualitySystemReference);

public sealed record CloseQualityEventInput(
    string ClosureEvidenceReference,
    string? ClosureSummary);

public sealed record CancelQualityEventInput(string Reason);

public sealed record LinkQualityEventSourceInput(
    string SourceType,
    Guid SourceId,
    string? EventType,
    Guid? RegisterEntryId,
    string? SourceReferenceSnapshot,
    string? Notes);

public sealed record CreateDeviationInput(
    Guid QualityEventId,
    string DeviationTitle,
    string DeviationDescription,
    string? DeviationCategory,
    string? DeviationSeverity,
    DateTimeOffset? OccurredAt,
    string? ReportedBy,
    bool RequiresCAPA);

public sealed record RecordDeviationInvestigationInput(
    string? RootCauseSummary,
    string? RootCauseCategory,
    string? ImpactAssessmentSummary,
    string? PatientProductRegulatoryImpact,
    string? InvestigationEvidenceReference);

public sealed record CloseDeviationInput(
    string ClosureEvidenceReference,
    string? ClosureExceptionJustification);

public sealed record CancelDeviationInput(string Reason);

public sealed record CreateCapaActionInput(
    Guid? QualityEventId,
    Guid? DeviationId,
    string? ActionType,
    string ActionTitle,
    string ActionDescription,
    Guid? ActionOwnerUserId,
    string? ActionOwnerRole,
    DateTimeOffset? DueDate,
    bool EffectivenessCheckRequired,
    DateTimeOffset? EffectivenessDueDate,
    IReadOnlyList<Guid>? RelatedRegisterEntryIds,
    IReadOnlyList<Guid>? RelatedControlledDocumentIds,
    IReadOnlyList<Guid>? RelatedExternalDocumentIds);

public sealed record CompleteCapaActionInput(string CompletionEvidenceReference, string? Comment);

public sealed record RecordCapaEffectivenessInput(
    string EffectivenessResult,
    string EffectivenessEvidenceReference,
    string? EffectivenessSummary);

public sealed record CloseCapaActionInput(string? ClosureExceptionJustification);

public sealed record CancelCapaActionInput(string Reason);

/// <summary>Bridge input: raise a quality event from an existing FU aggregate, applying the trigger mapping.</summary>
public sealed record BridgeFromSourceInput(
    string SourceType,
    Guid SourceId,
    string? TriggerReason,
    string? SeverityOverride,
    string? DetectionEvidenceReference);

// ── output models ────────────────────────────────────────────────────────────

public sealed record QualityEventModel(
    Guid Id,
    string QualityEventNumber,
    string EventTitle,
    string EventDescription,
    string EventType,
    string EventSeverity,
    string EventStatus,
    string SourceType,
    Guid? SourceId,
    Guid? RegisterEntryId,
    Guid? ControlledDocumentId,
    Guid? TemplateVariantId,
    Guid? ExternalDocumentId,
    DateTimeOffset DetectedAt,
    string? DetectedBy,
    string? DetectionEvidenceReference,
    bool ImmediateContainmentRequired,
    string? ImmediateContainmentSummary,
    bool RequiresDeviation,
    bool RequiresCAPA,
    Guid? DeviationId,
    IReadOnlyList<Guid> CAPAActionIds,
    string? DeviationWaiverJustification,
    string? ExternalQualitySystemReference,
    string? ClosureEvidenceReference,
    string? ClosureSummary,
    DateTimeOffset? ClosedAt,
    string? ClosedBy,
    string? CancellationReason,
    string BoundaryStatement);

public sealed record DeviationModel(
    Guid Id,
    string DeviationNumber,
    Guid QualityEventId,
    string DeviationTitle,
    string DeviationDescription,
    string DeviationCategory,
    string DeviationSeverity,
    string DeviationStatus,
    DateTimeOffset? OccurredAt,
    DateTimeOffset DetectedAt,
    string? ReportedBy,
    string? RootCauseSummary,
    string RootCauseCategory,
    string? ImpactAssessmentSummary,
    string PatientProductRegulatoryImpact,
    string? InvestigationEvidenceReference,
    bool RequiresCAPA,
    IReadOnlyList<Guid> CAPAActionIds,
    string? ClosureEvidenceReference,
    string? ClosureExceptionJustification,
    DateTimeOffset? ClosedAt,
    string? ClosedBy,
    string? CancellationReason,
    string BoundaryStatement);

public sealed record CapaActionModel(
    Guid Id,
    string CAPANumber,
    Guid? QualityEventId,
    Guid? DeviationId,
    string ActionType,
    string ActionTitle,
    string ActionDescription,
    string ActionStatus,
    Guid? ActionOwnerUserId,
    string? ActionOwnerRole,
    DateTimeOffset? DueDate,
    DateTimeOffset? StartedAt,
    string? CompletionEvidenceReference,
    DateTimeOffset? CompletedAt,
    string? CompletedBy,
    bool EffectivenessCheckRequired,
    DateTimeOffset? EffectivenessDueDate,
    string? EffectivenessEvidenceReference,
    string EffectivenessResult,
    string? EffectivenessSummary,
    IReadOnlyList<Guid> RelatedRegisterEntryIds,
    IReadOnlyList<Guid> RelatedControlledDocumentIds,
    IReadOnlyList<Guid> RelatedExternalDocumentIds,
    string? ClosureExceptionJustification,
    DateTimeOffset? ClosedAt,
    string? ClosedBy,
    string? CancellationReason,
    bool IsOverdue,
    string BoundaryStatement);

public sealed record QualityEventSourceLinkModel(
    Guid Id,
    Guid QualityEventId,
    string SourceType,
    Guid SourceId,
    Guid? RegisterEntryId,
    string EventType,
    string LinkStatus,
    string? SourceReferenceSnapshot,
    string? Notes);

/// <summary>MOD-0029-FU22 — what the trigger mapping concluded for a given source finding.</summary>
public sealed record QualityTriggerMappingModel(
    string EventType,
    string EventSeverity,
    string DeviationCategory,
    string DeviationSeverity,
    bool RequiresDeviation,
    bool RequiresCAPA,
    bool ImmediateContainmentRequired,
    string MappingRationale);

public static class QualityEventWire
{
    public const string BoundaryStatement =
        "Document-control scoped quality bridge: MOD-0029-FU22 gives document-control failures a traceable " +
        "quality event, deviation and CAPA record. It is NOT a QMS module — there is no CAPA workflow engine, no " +
        "investigation module, no root-cause methodology, no effectiveness scheduler, no e-signature and no " +
        "external QMS API integration. ExternalQualitySystemReference is the seam for a future QMS record id.";

    public static QualityEventType ParseEventType(string? v) =>
        Enum.TryParse<QualityEventType>(v, true, out var r) ? r : QualityEventType.Other;

    public static QualityEventSeverity? ParseEventSeverity(string? v) =>
        Enum.TryParse<QualityEventSeverity>(v, true, out var r) ? r : null;

    public static QualityEventSourceType ParseSourceType(string? v) =>
        Enum.TryParse<QualityEventSourceType>(v, true, out var r) ? r : QualityEventSourceType.Manual;

    public static QualityDeviationCategory ParseDeviationCategory(string? v) =>
        Enum.TryParse<QualityDeviationCategory>(v, true, out var r) ? r : QualityDeviationCategory.DocumentationControl;

    public static QualityDeviationSeverity ParseDeviationSeverity(string? v) =>
        Enum.TryParse<QualityDeviationSeverity>(v, true, out var r) ? r : QualityDeviationSeverity.Minor;

    public static DeviationRootCauseCategory ParseRootCause(string? v) =>
        Enum.TryParse<DeviationRootCauseCategory>(v, true, out var r) ? r : DeviationRootCauseCategory.NotAssessed;

    public static DeviationImpactAssessment ParseImpact(string? v) =>
        Enum.TryParse<DeviationImpactAssessment>(v, true, out var r) ? r : DeviationImpactAssessment.NotAssessed;

    public static CapaActionType ParseCapaType(string? v) =>
        Enum.TryParse<CapaActionType>(v, true, out var r) ? r : CapaActionType.CorrectiveAction;

    public static CapaEffectivenessResult? ParseEffectiveness(string? v) =>
        Enum.TryParse<CapaEffectivenessResult>(v, true, out var r) ? r : null;

    public static QualityEventModel ToEvent(DocumentQualityEvent e) => new(
        e.Id, e.QualityEventNumber, e.EventTitle, e.EventDescription, e.EventType.ToString(),
        e.EventSeverity.ToString(), e.EventStatus.ToString(), e.SourceType.ToString(), e.SourceId,
        e.RegisterEntryId, e.ControlledDocumentId, e.TemplateVariantId, e.ExternalDocumentId, e.DetectedAt,
        e.DetectedBy, e.DetectionEvidenceReference, e.ImmediateContainmentRequired, e.ImmediateContainmentSummary,
        e.RequiresDeviation, e.RequiresCAPA, e.DeviationId, e.CAPAActionIds.ToList(),
        e.DeviationWaiverJustification, e.ExternalQualitySystemReference, e.ClosureEvidenceReference,
        e.ClosureSummary, e.ClosedAt, e.ClosedBy, e.CancellationReason, BoundaryStatement);

    public static DeviationModel ToDeviation(DocumentDeviation d) => new(
        d.Id, d.DeviationNumber, d.QualityEventId, d.DeviationTitle, d.DeviationDescription,
        d.DeviationCategory.ToString(), d.DeviationSeverity.ToString(), d.DeviationStatus.ToString(),
        d.OccurredAt, d.DetectedAt, d.ReportedBy, d.RootCauseSummary, d.RootCauseCategory.ToString(),
        d.ImpactAssessmentSummary, d.PatientProductRegulatoryImpact.ToString(), d.InvestigationEvidenceReference,
        d.RequiresCAPA, d.CAPAActionIds.ToList(), d.ClosureEvidenceReference, d.ClosureExceptionJustification,
        d.ClosedAt, d.ClosedBy, d.CancellationReason, BoundaryStatement);

    public static CapaActionModel ToCapa(DocumentCAPAAction a, DateTimeOffset now) => new(
        a.Id, a.CAPANumber, a.QualityEventId, a.DeviationId, a.ActionType.ToString(), a.ActionTitle,
        a.ActionDescription, a.ActionStatus.ToString(), a.ActionOwnerUserId, a.ActionOwnerRole, a.DueDate,
        a.StartedAt, a.CompletionEvidenceReference, a.CompletedAt, a.CompletedBy, a.EffectivenessCheckRequired,
        a.EffectivenessDueDate, a.EffectivenessEvidenceReference, a.EffectivenessResult.ToString(),
        a.EffectivenessSummary, a.RelatedRegisterEntryIds.ToList(), a.RelatedControlledDocumentIds.ToList(),
        a.RelatedExternalDocumentIds.ToList(), a.ClosureExceptionJustification, a.ClosedAt, a.ClosedBy,
        a.CancellationReason,
        !a.IsSettled() && a.DueDate is { } due && now > due,
        BoundaryStatement);

    public static QualityEventSourceLinkModel ToLink(DocumentQualityEventSourceLink l) => new(
        l.Id, l.QualityEventId, l.SourceType.ToString(), l.SourceId, l.RegisterEntryId, l.EventType.ToString(),
        l.LinkStatus.ToString(), l.SourceReferenceSnapshot, l.Notes);
}
