using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementExternalDocuments;

// MOD-0029-FU14 — External Document Register contracts, reason codes and wire mapping (GMG-QMS-SOP-0001 §10).

/// <summary>
/// MOD-0029-FU14 — RECOMMENDED Layer 1 RBAC keys. NOT seeded in this FU (no AuthService change): the controller
/// reuses the already-seeded controlled-documents view/create keys. A later hardening FU should seed these.
/// </summary>
public static class ExternalDocumentPermissions
{
    public const string View = "platform.document-management.external-documents.view";
    public const string Manage = "platform.document-management.external-documents.manage";
    public const string MonitoringRecord = "platform.document-management.external-documents.monitoring.record";
    public const string ImpactManage = "platform.document-management.external-documents.impact.manage";
}

public static class ExternalDocumentReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string ExternalDocumentNotFound = "EXTERNAL_DOCUMENT_NOT_FOUND";
    public const string AssessmentNotFound = "IMPACT_ASSESSMENT_NOT_FOUND";
    public const string TitleRequired = "TITLE_REQUIRED";
    public const string SourceReferenceRequired = "SOURCE_REFERENCE_REQUIRED";
    public const string AuthorityRequired = "AUTHORITY_REQUIRED";
    public const string MonitoringOwnerRequired = "MONITORING_OWNER_REQUIRED";
    public const string MonitoringFrequencyRequired = "MONITORING_FREQUENCY_REQUIRED";
    public const string SourceStatusRequired = "SOURCE_STATUS_REQUIRED";
    public const string MonitoringSourceRequired = "MONITORING_SOURCE_REQUIRED";
    public const string EvidenceReferenceRequired = "EVIDENCE_REFERENCE_REQUIRED";
    public const string ChangeSummaryRequired = "CHANGE_SUMMARY_REQUIRED";
    public const string AssessmentEvidenceRequired = "ASSESSMENT_EVIDENCE_REQUIRED";
    public const string DocumentImpactActionRequired = "DOCUMENT_IMPACT_ACTION_REQUIRED";
    public const string AlreadyCompleted = "ASSESSMENT_ALREADY_COMPLETED";
    public const string ArchivedNotEditable = "EXTERNAL_DOCUMENT_ARCHIVED";
    public const string InternalEntryNotFound = "INTERNAL_REGISTER_ENTRY_NOT_FOUND";
    public const string InternalEntryIsExternal = "INTERNAL_ENTRY_IS_EXTERNAL";
    public const string EffectivePromotionEvidenceRequired = "EFFECTIVE_PROMOTION_EVIDENCE_REQUIRED";
    public const string PermissionDenied = "PERMISSION_DENIED";
}

// ── inputs ───────────────────────────────────────────────────────────────────

/// <summary>All editable external document fields. Used for create and update.</summary>
public sealed record ExternalDocumentFieldsInput(
    string ExternalDocumentTitle,
    string ExternalAuthorityName,
    string SourceReference,
    string? ExternalDocumentCode,
    string? ExternalDocumentType,
    string? Jurisdiction,
    string? CountryCode,
    string? RegionCode,
    string? SourceUrl,
    string? SourceVersion,
    DateTimeOffset? SourceEffectiveDate,
    DateTimeOffset? SourcePublishedDate,
    DateTimeOffset? SourceSupersededDate,
    string? SourceStatus,
    Guid? MonitoringOwnerUserId,
    string? MonitoringOwnerRole,
    string? MonitoringFunction,
    string? MonitoringFrequency,
    bool HasGmpImpact = false,
    bool HasGdpImpact = false,
    bool HasPvImpact = false,
    bool HasRaImpact = false,
    bool HasBatchReleaseImpact = false,
    bool HasTrainingImpact = false,
    bool HasDocumentImpact = false,
    string? PromotionEvidenceReference = null);

public sealed record RecordMonitoringCheckInput(
    string MonitoringSource,
    string EvidenceReference,
    bool ChangeDetected,
    string? ChangeSummary,
    string? SourceVersionObserved,
    DateTimeOffset? SourceEffectiveDateObserved,
    DateTimeOffset? CheckDate);

public sealed record MarkExternalDocumentSupersededInput(
    DateTimeOffset? SourceSupersededDate,
    string? SupersessionSummary);

public sealed record ArchiveExternalDocumentInput(string Reason);

public sealed record CreateExternalImpactAssessmentInput(
    string? TriggerType,
    bool HasGmpImpact,
    bool HasGdpImpact,
    bool HasPvImpact,
    bool HasRaImpact,
    bool HasBatchReleaseImpact,
    bool HasTrainingImpact,
    bool HasDocumentImpact,
    string? ImpactSummary,
    DateTimeOffset? TriggerDate);

public sealed record CompleteExternalImpactAssessmentInput(
    string AssessmentEvidenceReference,
    string? RecommendedAction,
    string? ImpactSummary,
    Guid? ActionOwnerUserId,
    string? ActionOwnerRole,
    DateTimeOffset? ActionDueDate,
    string? ActionReference);

public sealed record LinkExternalDocumentToInternalInput(
    Guid InternalRegisterEntryId,
    string? LinkType,
    string? Notes);

// ── output models ────────────────────────────────────────────────────────────

public sealed record ExternalDocumentModel(
    Guid Id,
    string? ExternalDocumentCode,
    string ExternalDocumentTitle,
    string ExternalDocumentType,
    string ExternalAuthorityName,
    string? Jurisdiction,
    string? CountryCode,
    string? RegionCode,
    string? SourceUrl,
    string SourceReference,
    string? SourceVersion,
    DateTimeOffset? SourceEffectiveDate,
    DateTimeOffset? SourcePublishedDate,
    DateTimeOffset? SourceSupersededDate,
    string SourceStatus,
    Guid? MonitoringOwnerUserId,
    string? MonitoringOwnerRole,
    string? MonitoringFunction,
    string MonitoringFrequency,
    DateTimeOffset? LastCheckedAt,
    string? LastCheckedBy,
    DateTimeOffset? NextCheckDueDate,
    string? LastKnownChangeSummary,
    bool RequiresImpactAssessment,
    DateTimeOffset? ImpactAssessmentDueDate,
    string ImpactAssessmentStatus,
    bool HasGmpImpact,
    bool HasGdpImpact,
    bool HasPvImpact,
    bool HasRaImpact,
    bool HasBatchReleaseImpact,
    bool HasTrainingImpact,
    bool HasDocumentImpact,
    string ExternalDocumentStatus,
    bool IsRegulatoryIntelligenceOnly,
    bool IsMonitoringOverdue,
    string BoundaryStatement);

public sealed record ExternalDocumentMonitoringCheckModel(
    Guid Id,
    Guid ExternalDocumentRegisterEntryId,
    DateTimeOffset CheckDate,
    string? CheckedBy,
    string MonitoringSource,
    string? SourceVersionObserved,
    DateTimeOffset? SourceEffectiveDateObserved,
    bool ChangeDetected,
    string? ChangeSummary,
    string EvidenceReference,
    DateTimeOffset? NextCheckDueDate);

public sealed record ExternalDocumentImpactAssessmentModel(
    Guid Id,
    Guid ExternalDocumentRegisterEntryId,
    string AssessmentStatus,
    string TriggerType,
    DateTimeOffset DueDate,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? CompletedBy,
    string? AssessmentEvidenceReference,
    string? ImpactSummary,
    bool HasGmpImpact,
    bool HasGdpImpact,
    bool HasPvImpact,
    bool HasRaImpact,
    bool HasBatchReleaseImpact,
    bool HasTrainingImpact,
    bool HasDocumentImpact,
    string RecommendedAction,
    Guid? ActionOwnerUserId,
    string? ActionOwnerRole,
    DateTimeOffset? ActionDueDate,
    string? ActionReference,
    bool IsOverdue);

public sealed record ExternalDocumentInternalLinkModel(
    Guid Id,
    Guid ExternalDocumentRegisterEntryId,
    Guid InternalRegisterEntryId,
    string LinkType,
    string LinkStatus,
    string? Notes);

/// <summary>MOD-0029-FU14 — a monitoring-due row: the source has not been checked by its due date.</summary>
public sealed record ExternalDocumentMonitoringDueModel(
    Guid Id,
    string ExternalDocumentTitle,
    string ExternalAuthorityName,
    string MonitoringFrequency,
    Guid? MonitoringOwnerUserId,
    string? MonitoringOwnerRole,
    DateTimeOffset? LastCheckedAt,
    DateTimeOffset? NextCheckDueDate,
    int DaysOverdue,
    bool NeverChecked);

public static class ExternalDocumentWire
{
    public static ExternalDocumentType ParseType(string? v) =>
        Enum.TryParse<ExternalDocumentType>(v, true, out var r) ? r : ExternalDocumentType.Other;

    public static ExternalSourceStatus? ParseSourceStatus(string? v) =>
        Enum.TryParse<ExternalSourceStatus>(v, true, out var r) ? r : null;

    public static ExternalMonitoringFrequency? ParseFrequency(string? v) =>
        Enum.TryParse<ExternalMonitoringFrequency>(v, true, out var r) ? r : null;

    public static ExternalImpactTriggerType ParseTrigger(string? v) =>
        Enum.TryParse<ExternalImpactTriggerType>(v, true, out var r) ? r : ExternalImpactTriggerType.Manual;

    public static ExternalImpactRecommendedAction ParseAction(string? v) =>
        Enum.TryParse<ExternalImpactRecommendedAction>(v, true, out var r) ? r : ExternalImpactRecommendedAction.NoAction;

    public static ExternalDocumentLinkType ParseLinkType(string? v) =>
        Enum.TryParse<ExternalDocumentLinkType>(v, true, out var r) ? r : ExternalDocumentLinkType.References;

    /// <summary>
    /// SOP §10.4 — a draft/consultation document is regulatory intelligence only. It may be monitored and
    /// assessed, but must never be applied as an effective requirement.
    /// </summary>
    public static bool IsRegulatoryIntelligenceOnly(ExternalDocumentRegisterEntry e) =>
        e.SourceStatus == ExternalSourceStatus.DraftConsultation;

    public static bool IsMonitoringOverdue(ExternalDocumentRegisterEntry e, DateTimeOffset now) =>
        e.ExternalDocumentStatus is not (ExternalDocumentStatus.Archived or ExternalDocumentStatus.Superseded)
        && e.NextCheckDueDate is { } due && now > due;

    /// <summary>The standing boundary statement surfaced on every read — FU14 never versions an external document.</summary>
    public static string BoundaryStatement(ExternalDocumentRegisterEntry e) =>
        IsRegulatoryIntelligenceOnly(e)
            ? "External document — reference and monitoring only; this source is draft/consultation and is tracked as regulatory intelligence, not as an effective requirement. It is not an internal controlled document and has no internal version lifecycle."
            : "External document — reference and monitoring only. It is published by an external source, is never edited or versioned here, and has no internal controlled-document lifecycle.";

    public static ExternalDocumentModel ToModel(ExternalDocumentRegisterEntry e, DateTimeOffset now) => new(
        e.Id, e.ExternalDocumentCode, e.ExternalDocumentTitle, e.ExternalDocumentType.ToString(),
        e.ExternalAuthorityName, e.Jurisdiction, e.CountryCode, e.RegionCode, e.SourceUrl, e.SourceReference,
        e.SourceVersion, e.SourceEffectiveDate, e.SourcePublishedDate, e.SourceSupersededDate,
        e.SourceStatus.ToString(), e.MonitoringOwnerUserId, e.MonitoringOwnerRole, e.MonitoringFunction,
        e.MonitoringFrequency.ToString(), e.LastCheckedAt, e.LastCheckedBy, e.NextCheckDueDate,
        e.LastKnownChangeSummary, e.RequiresImpactAssessment, e.ImpactAssessmentDueDate,
        e.ImpactAssessmentStatus.ToString(), e.HasGmpImpact, e.HasGdpImpact, e.HasPvImpact, e.HasRaImpact,
        e.HasBatchReleaseImpact, e.HasTrainingImpact, e.HasDocumentImpact, e.ExternalDocumentStatus.ToString(),
        IsRegulatoryIntelligenceOnly(e), IsMonitoringOverdue(e, now), BoundaryStatement(e));

    public static ExternalDocumentMonitoringCheckModel ToCheck(ExternalDocumentMonitoringCheck c) => new(
        c.Id, c.ExternalDocumentRegisterEntryId, c.CheckDate, c.CheckedBy, c.MonitoringSource,
        c.SourceVersionObserved, c.SourceEffectiveDateObserved, c.ChangeDetected, c.ChangeSummary,
        c.EvidenceReference, c.NextCheckDueDate);

    public static ExternalDocumentImpactAssessmentModel ToAssessment(ExternalDocumentImpactAssessment a, DateTimeOffset now) => new(
        a.Id, a.ExternalDocumentRegisterEntryId, a.AssessmentStatus.ToString(), a.TriggerType.ToString(), a.DueDate,
        a.StartedAt, a.CompletedAt, a.CompletedBy, a.AssessmentEvidenceReference, a.ImpactSummary,
        a.HasGmpImpact, a.HasGdpImpact, a.HasPvImpact, a.HasRaImpact, a.HasBatchReleaseImpact,
        a.HasTrainingImpact, a.HasDocumentImpact, a.RecommendedAction.ToString(), a.ActionOwnerUserId,
        a.ActionOwnerRole, a.ActionDueDate, a.ActionReference,
        a.AssessmentStatus is not ExternalImpactAssessmentStatus.Completed && now > a.DueDate);

    public static ExternalDocumentInternalLinkModel ToLink(ExternalDocumentInternalLink l) => new(
        l.Id, l.ExternalDocumentRegisterEntryId, l.InternalRegisterEntryId, l.LinkType.ToString(),
        l.LinkStatus.ToString(), l.Notes);
}
