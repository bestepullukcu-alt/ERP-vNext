namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0029-FU06 — Document Master Register (GMG-QMS-SOP-0001 §18/§20) enums. Kept in a dedicated file so FU06
// ownership never edits the FU01 ControlledDocumentEnums.cs or FU04 DocumentAccessMatrixEnums.cs surfaces.
//
// SCOPE NOTE (foundation only): these enums MODEL the SOP status/criticality/class vocabulary so the register can
// record decisions. FU06 does NOT implement the transition engine (FU08), the approval route (FU09), or the
// non-waivable release-gate engine (FU10). Those consume this vocabulary later.

/// <summary>
/// MOD-0029-FU06 — the register ENTRY's own projection/governance status (not the document's SOP lifecycle status).
/// This tracks the register row itself; the controlled document's regulated status is <see cref="ControlledDocumentLifecycleStatus"/>.
/// </summary>
public enum DocumentRegisterStatus
{
    /// <summary>Register entry opened (e.g. UID/code allocation stage) — no effective document yet.</summary>
    Draft = 0,

    /// <summary>Register entry is the live authoritative row for its document.</summary>
    Active = 1,

    /// <summary>Register entry archived (kept for history; never hard-deleted).</summary>
    Archived = 2,

    /// <summary>A GDocP correction is in progress on a key field (FU21 hardening consumes this).</summary>
    CorrectionPending = 3,

    /// <summary>Replaced by a newer register row for the same document lineage.</summary>
    Superseded = 4,

    /// <summary>Document retired without replacement; row retained, code/UID never reused.</summary>
    Retired = 5
}

/// <summary>
/// MOD-0029-FU06 — the controlled document's regulated SOP status (GMG-QMS-SOP-0001 §6.2). Recorded on the register
/// row as metadata in this FU. Full transition enforcement is FU08; FU06 stores the value and defaults to
/// <see cref="Draft"/> without driving any workflow side effects.
/// </summary>
public enum ControlledDocumentLifecycleStatus
{
    Draft = 0,
    InReview = 1,
    ApprovedPendingEffective = 2,
    Effective = 3,
    UnderRevision = 4,
    Suspended = 5,
    Superseded = 6,
    Retired = 7,
    ObsoleteCopy = 8
}

/// <summary>MOD-0029-FU06 — document criticality band (SOP §7.1). Drives controls in later FUs; recorded here.</summary>
public enum DocumentCriticality
{
    Critical = 0,
    Major = 1,
    Minor = 2,
    UrgentTemporary = 3
}

/// <summary>MOD-0029-FU06 — document class (SOP §6.1). Distinct from <see cref="DocumentType"/>, which stays FU01-owned.</summary>
public enum ControlledDocumentClass
{
    PolicyGovernance = 0,
    ManualSystemDescription = 1,
    Sop = 2,
    WorkInstruction = 3,
    FormTemplateRegisterMatrixPlanChecklist = 4,
    QualityTechnicalAgreementSdea = 5,
    UrgentTemporaryInstruction = 6,
    Other = 7
}
