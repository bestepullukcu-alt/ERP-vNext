namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0029-FU20 — Repository / DMS downtime and temporary controlled issue (GMG-QMS-SOP-0001 §11.3) enums. Kept in
// a dedicated file so FU20 ownership never edits the FU16 RepositoryAssessmentEnums.cs or FU17 ControlledCopyEnums.cs
// surfaces (FU20 REUSES ControlledCopyType.TemporaryControlledIssue rather than inventing its own copy vocabulary).
//
// SCOPE BOUNDARY: this models the GOVERNANCE of issuing a controlled document copy while the normal repository/DMS
// is unavailable, and reconciling it afterwards. It implements no e-signature, no qualified electronic signature
// provider, no CAPA/Quality Event module and no BCP module — those are recorded as evidence REFERENCES only.
//
// NOT TO BE CONFUSED WITH FU13 TemporaryInstructionControl: that governs the 30-day validity of a temporary
// INSTRUCTION DOCUMENT. This governs a temporary ISSUE of an existing controlled document during an outage. The
// two may reference each other but are deliberately separate aggregates.

/// <summary>MOD-0029-FU20 — the downtime event's own lifecycle (SOP §11.3).</summary>
public enum DowntimeStatus
{
    Open = 0,
    Restored = 1,
    ReconciliationInProgress = 2,
    Reconciled = 3,
    Escalated = 4,
    Closed = 5,
    Cancelled = 6
}

/// <summary>MOD-0029-FU20 — why the repository / DMS became unavailable.</summary>
public enum DowntimeType
{
    PlannedMaintenance = 0,
    UnplannedOutage = 1,
    AccessFailure = 2,
    DataIntegrityConcern = 3,
    MigrationCutover = 4,
    Other = 5
}

/// <summary>
/// MOD-0029-FU20 — the temporary controlled issue's lifecycle. <see cref="Overdue"/> is the SOP-critical state:
/// the 3-working-day reconciliation window has passed, which is a deviation.
/// </summary>
public enum TemporaryIssueStatus
{
    Requested = 0,
    Approved = 1,
    Issued = 2,
    ReconciliationDue = 3,
    Reconciled = 4,
    Overdue = 5,
    Cancelled = 6
}

/// <summary>
/// MOD-0029-FU20 — how the outside-normal-environment approval was captured (SOP §11.3).
///
/// IMPORTANT BOUNDARY: recording <see cref="QualifiedElectronicMechanism"/> is a STATEMENT BY THE RECORDER, not a
/// claim by the platform. FU20 implements no e-signature and validates no signature — and a native interim
/// repository can never be presented as a validated DMS on the strength of this field.
/// </summary>
public enum OutsideNormalEnvironmentApprovalMechanism
{
    WetSignature = 0,
    QualifiedElectronicMechanism = 1,
    SeparateApprovalMechanism = 2,
    Other = 3
}

/// <summary>MOD-0029-FU20 — why an escalation was raised (SOP §11.3).</summary>
public enum DowntimeEscalationType
{
    /// <summary>Downtime exceeded 2 working days — GQD + IT/CSV escalation and BCP assessment are required.</summary>
    DowntimeExceedsTwoWorkingDays = 0,

    /// <summary>A temporary issue passed its 3-working-day reconciliation due date.</summary>
    ReconciliationOverdue = 1,

    /// <summary>A temporary issue was closed or abandoned without reconciliation evidence.</summary>
    MissingReconciliation = 2,

    DataIntegrityConcern = 3,
    BcpAssessmentRequired = 4
}

public enum DowntimeEscalationSeverity
{
    Warning = 0,
    Major = 1,
    Critical = 2
}

/// <summary>MOD-0029-FU20 — the role an escalation is directed to (SOP §11.3).</summary>
public enum DowntimeEscalationRole
{
    GQD = 0,
    ITCSVOwner = 1,
    QADocumentation = 2
}

public enum DowntimeEscalationStatus
{
    Open = 0,
    Acknowledged = 1,
    Resolved = 2,
    Closed = 3
}
