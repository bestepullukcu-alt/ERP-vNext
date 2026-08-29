namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0029-FU14 — External Document Register (GMG-QMS-SOP-0001 §10) enums. Kept in a dedicated file so FU14
// ownership never edits the FU06 MasterRegisterEnums.cs or FU12 PeriodicReviewEnums.cs surfaces.
//
// BOUNDARY: an external document is NOT an internal controlled document. It is published by an external source,
// is never edited or versioned here, and never enters the internal Effective lifecycle. The system records only
// reference, monitoring, impact and follow-up action. These enums deliberately do NOT reuse
// ControlledDocumentLifecycleStatus or the FU12 periodic-review vocabulary — conflating the two would let an
// external source's status drive an internal release decision.

/// <summary>MOD-0029-FU14 — the kind of external document being tracked (SOP §10.1).</summary>
public enum ExternalDocumentType
{
    Regulation = 0,
    Guideline = 1,
    Standard = 2,
    Pharmacopeia = 3,
    AuthorityCommunication = 4,
    LicenseCommitment = 5,
    QualityAgreementReference = 6,
    TechnicalStandard = 7,
    Other = 8
}

/// <summary>
/// MOD-0029-FU14 — the status of the document AT ITS SOURCE (not an internal lifecycle status).
/// <see cref="DraftConsultation"/> is regulatory intelligence only: it may be monitored but must never be applied
/// as an effective requirement (SOP §10.4).
/// </summary>
public enum ExternalSourceStatus
{
    /// <summary>Published for consultation. Regulatory intelligence only — never an effective requirement.</summary>
    DraftConsultation = 0,

    /// <summary>In force at the source; the monitored baseline for impact assessment.</summary>
    CurrentEffective = 1,

    /// <summary>Replaced by a newer issue at the source; linked internal documents may need assessment.</summary>
    Superseded = 2,

    /// <summary>Withdrawn by the authority; linked internal documents require action.</summary>
    Withdrawn = 3,

    /// <summary>Source status not established yet.</summary>
    Unknown = 4
}

/// <summary>MOD-0029-FU14 — how often the monitoring owner must re-check the source (SOP §10.2).</summary>
public enum ExternalMonitoringFrequency
{
    Weekly = 0,
    Monthly = 1,
    Quarterly = 2,
    SemiAnnual = 3,
    Annual = 4,

    /// <summary>Event-driven only; no scheduled next-check date is computed.</summary>
    OnTrigger = 5
}

/// <summary>MOD-0029-FU14 — the register row's own tracking status. Archival is a status change; never a delete.</summary>
public enum ExternalDocumentStatus
{
    Active = 0,
    Monitoring = 1,
    ActionRequired = 2,
    Superseded = 3,
    Archived = 4
}

/// <summary>MOD-0029-FU14 — impact-assessment progress (SOP §10.3, 10 working days for GMP/GDP/PV/RA impact).</summary>
public enum ExternalImpactAssessmentStatus
{
    NotRequired = 0,
    Pending = 1,
    InProgress = 2,
    Completed = 3,
    Overdue = 4,
    Blocked = 5
}

/// <summary>MOD-0029-FU14 — what caused an impact assessment to be raised.</summary>
public enum ExternalImpactTriggerType
{
    NewExternalDocument = 0,
    VersionChange = 1,
    Supersession = 2,
    RegulatoryAlert = 3,
    PeriodicCheck = 4,
    Manual = 5
}

/// <summary>
/// MOD-0029-FU14 — the assessment's recommendation. A RECOMMENDATION ONLY: FU14 never transitions an internal
/// controlled document. Acting on it stays with the FU08 lifecycle engine / FU13 suspension engine.
/// </summary>
public enum ExternalImpactRecommendedAction
{
    NoAction = 0,
    ReviseInternalDocument = 1,
    CreateInternalDocument = 2,
    SuspendInternalDocument = 3,
    RetireInternalDocument = 4,
    TrainingUpdate = 5,
    RegulatoryNotification = 6,
    QualityEventReview = 7,
    FurtherAssessmentRequired = 8
}

/// <summary>MOD-0029-FU14 — the nature of an external ↔ internal register relation.</summary>
public enum ExternalDocumentLinkType
{
    ImplementsRequirement = 0,
    References = 1,
    ImpactedBy = 2,
    SupersedesExternalBasis = 3,
    RegulatoryCommitment = 4
}

/// <summary>MOD-0029-FU14 — link state. Closing a link is a status change; links are never hard-deleted.</summary>
public enum ExternalDocumentLinkStatus
{
    Active = 0,
    UnderAssessment = 1,
    ActionRequired = 2,
    Closed = 3
}
