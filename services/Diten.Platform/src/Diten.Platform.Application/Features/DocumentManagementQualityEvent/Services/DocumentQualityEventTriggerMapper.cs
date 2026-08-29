using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementQualityEvent.Services;

/// <summary>
/// MOD-0029-FU22 — maps a document-control finding onto its quality consequence (GMG-QMS-SOP-0001).
///
/// This is where the SOP's judgement calls live: an obsolete copy of a SUSPENDED document in use is a critical
/// deviation, whereas a superseded copy at a point of use is major; a reconstruction in the GDocP trail is a
/// critical data-integrity deviation demanding CAPA, whereas an evidence-reference correction is major.
///
/// A pure function — no repositories, no persistence, no side effects. That keeps every mapping decision in one
/// readable table and makes each branch directly testable. The caller may raise the severity via an override but
/// never lower it: a mapping can only be made stricter by hand.
/// </summary>
public static class DocumentQualityEventTriggerMapper
{
    /// <summary>FU17 obsolete copy finding → quality consequence.</summary>
    public static QualityTriggerMappingModel FromObsoleteCopyFinding(ObsoleteCopyFindingType findingType) => findingType switch
    {
        // A suspended document still in use is the most direct patient/product risk in document control.
        ObsoleteCopyFindingType.SuspendedDocumentInUse => Map(
            QualityEventType.ObsoleteCopyUse, QualityEventSeverity.Critical,
            QualityDeviationCategory.ControlledCopy, QualityDeviationSeverity.Critical,
            requiresDeviation: true, requiresCapa: true, containment: true,
            "A suspended document in active use requires immediate containment, a critical deviation and CAPA."),

        ObsoleteCopyFindingType.RetiredCopyAvailable => Map(
            QualityEventType.ObsoleteCopyUse, QualityEventSeverity.Critical,
            QualityDeviationCategory.ControlledCopy, QualityDeviationSeverity.Critical,
            requiresDeviation: true, requiresCapa: false, containment: true,
            "A retired document still available at a point of use requires containment and a critical deviation."),

        ObsoleteCopyFindingType.SupersededCopyAtPointOfUse => Map(
            QualityEventType.ObsoleteCopyUse, QualityEventSeverity.Major,
            QualityDeviationCategory.ControlledCopy, QualityDeviationSeverity.Major,
            requiresDeviation: true, requiresCapa: false, containment: true,
            "A superseded copy at a point of use requires withdrawal and a major deviation."),

        // An uncontrolled copy means the control system itself was bypassed — CAPA, not just a fix.
        ObsoleteCopyFindingType.UncontrolledCopyDetected => Map(
            QualityEventType.UncontrolledCopyDetected, QualityEventSeverity.Critical,
            QualityDeviationCategory.ControlledCopy, QualityDeviationSeverity.Critical,
            requiresDeviation: true, requiresCapa: true, containment: true,
            "An uncontrolled copy indicates the control system was bypassed; CAPA is required, not only removal."),

        ObsoleteCopyFindingType.MissingCopyDuringReconciliation => Map(
            QualityEventType.MissingReconciliation, QualityEventSeverity.Major,
            QualityDeviationCategory.ControlledCopy, QualityDeviationSeverity.Major,
            requiresDeviation: true, requiresCapa: false, containment: false,
            "A copy unaccounted for during reconciliation requires a major deviation."),

        ObsoleteCopyFindingType.MissingWithdrawalEvidence => Map(
            QualityEventType.MissingReconciliation, QualityEventSeverity.Major,
            QualityDeviationCategory.DocumentationControl, QualityDeviationSeverity.Major,
            requiresDeviation: true, requiresCapa: false, containment: false,
            "Withdrawal without evidence cannot be demonstrated; a major deviation is required."),

        _ => Map(
            QualityEventType.Other, QualityEventSeverity.Minor,
            QualityDeviationCategory.ControlledCopy, QualityDeviationSeverity.Minor,
            requiresDeviation: false, requiresCapa: false, containment: false,
            "Unclassified copy finding; assessed as minor pending human review.")
    };

    /// <summary>
    /// FU21 GDocP correction → quality consequence. Reconstruction and data-integrity corrections are the two
    /// cases the SOP treats as inherently critical.
    /// </summary>
    public static QualityTriggerMappingModel FromGDocPCorrection(GDocPCorrectionType correctionType, bool isBackdating) =>
        correctionType switch
        {
            GDocPCorrectionType.Reconstruction => Map(
                QualityEventType.DataIntegrityConcern, QualityEventSeverity.Critical,
                QualityDeviationCategory.DataIntegrity, QualityDeviationSeverity.Critical,
                requiresDeviation: true, requiresCapa: true, containment: false,
                "Reconstruction of a lost regulated value is a critical data-integrity deviation requiring CAPA."),

            GDocPCorrectionType.DataIntegrityCorrection => Map(
                QualityEventType.DataIntegrityConcern, QualityEventSeverity.Critical,
                QualityDeviationCategory.DataIntegrity, QualityDeviationSeverity.Critical,
                requiresDeviation: true, requiresCapa: true, containment: false,
                "A data-integrity correction is a critical deviation requiring CAPA."),

            GDocPCorrectionType.EvidenceReferenceCorrection => Map(
                QualityEventType.GDocPCorrectionHighRisk, QualityEventSeverity.Major,
                QualityDeviationCategory.DataIntegrity, QualityDeviationSeverity.Major,
                requiresDeviation: true, requiresCapa: false, containment: false,
                "Changing the evidence a regulated decision rests on requires a major deviation."),

            GDocPCorrectionType.StatusCorrection => Map(
                QualityEventType.GDocPCorrectionHighRisk, QualityEventSeverity.Major,
                QualityDeviationCategory.DocumentationControl, QualityDeviationSeverity.Major,
                requiresDeviation: true, requiresCapa: false, containment: false,
                "Correcting a regulated lifecycle status requires a major deviation."),

            // Backdating is judged on the ACT, not the declared type: any correction that moved a regulated
            // timestamp earlier is critical regardless of how it was labelled.
            _ when isBackdating => Map(
                QualityEventType.DataIntegrityConcern, QualityEventSeverity.Critical,
                QualityDeviationCategory.DataIntegrity, QualityDeviationSeverity.Critical,
                requiresDeviation: true, requiresCapa: false, containment: false,
                "A regulated timestamp was moved earlier (backdating); this is a critical data-integrity deviation."),

            _ => Map(
                QualityEventType.GDocPCorrectionHighRisk, QualityEventSeverity.Minor,
                QualityDeviationCategory.DocumentationControl, QualityDeviationSeverity.Minor,
                requiresDeviation: false, requiresCapa: false, containment: false,
                "Routine correction; no deviation indicated by the mapping.")
        };

    /// <summary>FU20 temporary controlled issue → quality consequence.</summary>
    public static QualityTriggerMappingModel FromTemporaryIssue(TemporaryIssueStatus issueStatus) => issueStatus switch
    {
        TemporaryIssueStatus.Overdue => Map(
            QualityEventType.MissingReconciliation, QualityEventSeverity.Major,
            QualityDeviationCategory.RepositoryControl, QualityDeviationSeverity.Major,
            requiresDeviation: true, requiresCapa: false, containment: false,
            "A temporary controlled issue past its 3-working-day reconciliation window is a major deviation."),

        // Cancelled after copies went out would mean copies in the field with no reconciliation path.
        TemporaryIssueStatus.Cancelled => Map(
            QualityEventType.MissingReconciliation, QualityEventSeverity.Major,
            QualityDeviationCategory.ControlledCopy, QualityDeviationSeverity.Major,
            requiresDeviation: true, requiresCapa: false, containment: true,
            "A cancelled temporary issue requires confirmation that no copies remain unreconciled in the field."),

        _ => Map(
            QualityEventType.RepositoryDowntimeIssue, QualityEventSeverity.Minor,
            QualityDeviationCategory.RepositoryControl, QualityDeviationSeverity.Minor,
            requiresDeviation: false, requiresCapa: false, containment: false,
            "Temporary issue within its reconciliation window; no deviation indicated.")
    };

    /// <summary>FU20 downtime escalation → quality consequence.</summary>
    public static QualityTriggerMappingModel FromDowntimeEscalation(DowntimeEscalationType escalationType) => escalationType switch
    {
        DowntimeEscalationType.DataIntegrityConcern => Map(
            QualityEventType.DataIntegrityConcern, QualityEventSeverity.Critical,
            QualityDeviationCategory.DataIntegrity, QualityDeviationSeverity.Critical,
            requiresDeviation: true, requiresCapa: true, containment: true,
            "A data-integrity concern during downtime is a critical deviation requiring CAPA."),

        DowntimeEscalationType.MissingReconciliation => Map(
            QualityEventType.MissingReconciliation, QualityEventSeverity.Major,
            QualityDeviationCategory.RepositoryControl, QualityDeviationSeverity.Major,
            requiresDeviation: true, requiresCapa: false, containment: false,
            "An unreconciled temporary issue past its due date is a major deviation."),

        DowntimeEscalationType.DowntimeExceedsTwoWorkingDays or DowntimeEscalationType.BcpAssessmentRequired => Map(
            QualityEventType.RepositoryDowntimeIssue, QualityEventSeverity.Major,
            QualityDeviationCategory.RepositoryControl, QualityDeviationSeverity.Major,
            requiresDeviation: false, requiresCapa: false, containment: false,
            "Downtime beyond the escalation threshold is a major quality event; the BCP assessment carries the follow-up."),

        _ => Map(
            QualityEventType.RepositoryDowntimeIssue, QualityEventSeverity.Minor,
            QualityDeviationCategory.RepositoryControl, QualityDeviationSeverity.Minor,
            requiresDeviation: false, requiresCapa: false, containment: false,
            "Downtime escalation assessed as minor.")
    };

    /// <summary>FU12 periodic review escalation → quality consequence.</summary>
    public static QualityTriggerMappingModel FromPeriodicReviewEscalation(ReviewEscalationSeverity severity) => severity switch
    {
        ReviewEscalationSeverity.Critical => Map(
            QualityEventType.PeriodicReviewOverdue, QualityEventSeverity.Critical,
            QualityDeviationCategory.DocumentationControl, QualityDeviationSeverity.Critical,
            requiresDeviation: true, requiresCapa: false, containment: false,
            "A critical document overdue for periodic review requires a critical deviation."),

        ReviewEscalationSeverity.Major => Map(
            QualityEventType.PeriodicReviewOverdue, QualityEventSeverity.Major,
            QualityDeviationCategory.DocumentationControl, QualityDeviationSeverity.Major,
            requiresDeviation: true, requiresCapa: false, containment: false,
            "An overdue periodic review at major severity requires a deviation."),

        _ => Map(
            QualityEventType.PeriodicReviewOverdue, QualityEventSeverity.Minor,
            QualityDeviationCategory.DocumentationControl, QualityDeviationSeverity.Minor,
            requiresDeviation: false, requiresCapa: false, containment: false,
            "Periodic review escalation at warning level; tracked without a deviation.")
    };

    /// <summary>FU14 external document impact assessment recommendation → quality consequence.</summary>
    public static QualityTriggerMappingModel FromExternalImpact(ExternalImpactRecommendedAction recommendedAction) =>
        recommendedAction switch
        {
            ExternalImpactRecommendedAction.QualityEventReview => Map(
                QualityEventType.ExternalRegulatoryImpact, QualityEventSeverity.Major,
                QualityDeviationCategory.ExternalRequirement, QualityDeviationSeverity.Major,
                requiresDeviation: true, requiresCapa: false, containment: false,
                "The impact assessment referred this external change for quality event review."),

            ExternalImpactRecommendedAction.RegulatoryNotification => Map(
                QualityEventType.ExternalRegulatoryImpact, QualityEventSeverity.Major,
                QualityDeviationCategory.ExternalRequirement, QualityDeviationSeverity.Major,
                requiresDeviation: false, requiresCapa: true, containment: false,
                "A regulatory notification commitment is tracked as a CAPA action."),

            ExternalImpactRecommendedAction.SuspendInternalDocument or ExternalImpactRecommendedAction.RetireInternalDocument => Map(
                QualityEventType.ExternalRegulatoryImpact, QualityEventSeverity.Critical,
                QualityDeviationCategory.ExternalRequirement, QualityDeviationSeverity.Critical,
                requiresDeviation: true, requiresCapa: true, containment: true,
                "An external change requiring suspension or retirement of an internal document is critical."),

            _ => Map(
                QualityEventType.ExternalRegulatoryImpact, QualityEventSeverity.Minor,
                QualityDeviationCategory.ExternalRequirement, QualityDeviationSeverity.Minor,
                requiresDeviation: false, requiresCapa: false, containment: false,
                "External impact recorded; no deviation indicated by the mapping.")
        };

    /// <summary>FU13 suspension case → quality consequence. A suspension is always evidence of a real problem.</summary>
    public static QualityTriggerMappingModel FromSuspensionCase() => Map(
        QualityEventType.SuspensionTrigger, QualityEventSeverity.Major,
        QualityDeviationCategory.DocumentationControl, QualityDeviationSeverity.Major,
        requiresDeviation: true, requiresCapa: false, containment: true,
        "A document suspension indicates a governance or quality failure and requires a deviation.");

    /// <summary>
    /// Applies a caller-supplied severity override. It may only RAISE the severity: a mapping decision can be
    /// tightened by human judgement but never quietly weakened.
    /// </summary>
    public static QualityTriggerMappingModel WithSeverityOverride(QualityTriggerMappingModel mapping, QualityEventSeverity? requested)
    {
        if (requested is not { } severity)
        {
            return mapping;
        }

        var current = Enum.Parse<QualityEventSeverity>(mapping.EventSeverity);
        if (severity <= current)
        {
            return mapping with
            {
                MappingRationale = mapping.MappingRationale +
                    $" A requested severity of {severity} was ignored: an override may only raise severity."
            };
        }

        var deviationSeverity = severity switch
        {
            QualityEventSeverity.Critical => QualityDeviationSeverity.Critical,
            QualityEventSeverity.Major => QualityDeviationSeverity.Major,
            _ => QualityDeviationSeverity.Minor
        };

        return mapping with
        {
            EventSeverity = severity.ToString(),
            DeviationSeverity = deviationSeverity.ToString(),
            RequiresDeviation = mapping.RequiresDeviation || severity == QualityEventSeverity.Critical,
            MappingRationale = mapping.MappingRationale + $" Severity raised to {severity} by explicit override."
        };
    }

    private static QualityTriggerMappingModel Map(
        QualityEventType eventType,
        QualityEventSeverity eventSeverity,
        QualityDeviationCategory deviationCategory,
        QualityDeviationSeverity deviationSeverity,
        bool requiresDeviation,
        bool requiresCapa,
        bool containment,
        string rationale) =>
        new(eventType.ToString(), eventSeverity.ToString(), deviationCategory.ToString(),
            deviationSeverity.ToString(), requiresDeviation, requiresCapa, containment, rationale);
}
