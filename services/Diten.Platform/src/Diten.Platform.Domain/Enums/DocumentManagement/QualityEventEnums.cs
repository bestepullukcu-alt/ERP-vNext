namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0029-FU22 — document-control scoped Quality Event / Deviation / CAPA bridge (GMG-QMS-SOP-0001) enums.
//
// ⚠️ NAMING COLLISION AVOIDED — READ BEFORE ADDING ENUMS HERE: this namespace ALREADY contains
// MOD-0028-FU09's DeviationSeverity / DeviationStatus / CollectionDeviationType, which describe a
// COLLECTION TREE READ-BACK deviation (expected vs actual provisioned folder path) — an infrastructure
// qualification concern, NOT a GxP quality deviation. Every FU22 deviation enum is therefore prefixed
// "Quality" so the two vocabularies can never be confused or accidentally interchanged. The FU22 aggregate is
// likewise DocumentDeviation, distinct from MOD-0028's DocumentCollectionDeviation.
//
// SCOPE BOUNDARY: this is a BRIDGE, not a QMS. It gives document-control events a traceable quality record with a
// deviation and CAPA skeleton, and leaves a port for a real QMS module later. It implements no CAPA workflow
// engine, no investigation module, no root-cause methodology, no effectiveness scheduler, no e-signature and no
// external QMS API integration.

/// <summary>MOD-0029-FU22 — what kind of document-control failure raised the quality event.</summary>
public enum QualityEventType
{
    ObsoleteCopyUse = 0,
    UncontrolledCopyDetected = 1,
    MissingReconciliation = 2,
    DataIntegrityConcern = 3,
    GDocPCorrectionHighRisk = 4,
    PeriodicReviewOverdue = 5,
    CriticalDocumentGovernanceFailure = 6,
    SuspensionTrigger = 7,
    UrgentWithdrawalTrigger = 8,
    ExternalRegulatoryImpact = 9,
    RepositoryDowntimeIssue = 10,
    TrainingReadinessFailure = 11,
    ReleaseGateFailure = 12,
    Other = 13
}

public enum QualityEventSeverity
{
    Minor = 0,
    Major = 1,
    Critical = 2
}

/// <summary>MOD-0029-FU22 — the quality event's own lifecycle. Closure is gated on its deviation and CAPA.</summary>
public enum QualityEventStatus
{
    Draft = 0,
    Open = 1,
    UnderAssessment = 2,
    DeviationOpened = 3,
    CAPARequired = 4,
    CAPAInProgress = 5,
    Closed = 6,
    Cancelled = 7
}

/// <summary>
/// MOD-0029-FU22 — which FU aggregate the event came from. Drives the trigger mapping and makes the bridge
/// idempotent per (source type, source id, event type).
/// </summary>
public enum QualityEventSourceType
{
    ControlledCopy = 0,
    ObsoleteCopyFinding = 1,
    TemporaryControlledIssue = 2,
    DowntimeEvent = 3,
    GDocPCorrection = 4,
    PeriodicReviewEscalation = 5,
    SuspensionCase = 6,
    RetirementCase = 7,
    ExternalDocumentImpactAssessment = 8,
    ReleaseGateEvaluation = 9,
    TrainingAssignment = 10,
    Manual = 11,
    Other = 12
}

/// <summary>MOD-0029-FU22 — GxP deviation category. NOT MOD-0028's CollectionDeviationType.</summary>
public enum QualityDeviationCategory
{
    DocumentationControl = 0,
    DataIntegrity = 1,
    Training = 2,
    ControlledCopy = 3,
    RepositoryControl = 4,
    ExternalRequirement = 5,
    ReleaseGovernance = 6,
    Other = 7
}

/// <summary>MOD-0029-FU22 — GxP deviation severity. Deliberately NOT MOD-0028-FU09's DeviationSeverity.</summary>
public enum QualityDeviationSeverity
{
    Minor = 0,
    Major = 1,
    Critical = 2
}

/// <summary>MOD-0029-FU22 — GxP deviation lifecycle. Deliberately NOT MOD-0028-FU09's DeviationStatus.</summary>
public enum QualityDeviationStatus
{
    Draft = 0,
    Open = 1,
    UnderInvestigation = 2,
    RootCausePending = 3,
    CAPARequired = 4,
    CAPAInProgress = 5,
    Closed = 6,
    Cancelled = 7
}

/// <summary>
/// MOD-0029-FU22 — root cause classification. <see cref="NotAssessed"/> is the honest default: FU22 implements no
/// root-cause methodology engine, so the category is recorded by a human, never inferred.
/// </summary>
public enum DeviationRootCauseCategory
{
    NotAssessed = 0,
    HumanError = 1,
    ProcessGap = 2,
    SystemFailure = 3,
    TrainingGap = 4,
    DataIntegrityIssue = 5,
    SupplierExternal = 6,
    Other = 7
}

/// <summary>
/// MOD-0029-FU22 — patient / product / regulatory impact verdict. A critical deviation cannot close while this is
/// <see cref="NotAssessed"/>: "we did not look" is never a closure basis.
/// </summary>
public enum DeviationImpactAssessment
{
    NotAssessed = 0,
    NoImpact = 1,
    PotentialImpact = 2,
    ConfirmedImpact = 3
}

/// <summary>MOD-0029-FU22 — the kind of CAPA action.</summary>
public enum CapaActionType
{
    /// <summary>An immediate fix to the specific instance. No due date is demanded.</summary>
    Correction = 0,

    CorrectiveAction = 1,
    PreventiveAction = 2,
    EffectivenessCheck = 3
}

/// <summary>MOD-0029-FU22 — CAPA action state. A foundation state machine, NOT a workflow engine.</summary>
public enum CapaActionStatus
{
    Draft = 0,
    Open = 1,
    InProgress = 2,
    Completed = 3,
    EffectivenessPending = 4,
    Effective = 5,
    Ineffective = 6,
    Cancelled = 7,
    Closed = 8
}

/// <summary>MOD-0029-FU22 — effectiveness verdict. An ineffective action can never be closed as effective.</summary>
public enum CapaEffectivenessResult
{
    NotRequired = 0,
    Pending = 1,
    Effective = 2,
    Ineffective = 3
}

/// <summary>MOD-0029-FU22 — source link state. Links are closed, never deleted.</summary>
public enum QualityEventSourceLinkStatus
{
    Active = 0,
    Resolved = 1,
    Closed = 2
}
