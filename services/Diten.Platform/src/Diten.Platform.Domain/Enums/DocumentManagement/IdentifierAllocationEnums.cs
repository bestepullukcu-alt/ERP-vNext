namespace Diten.Platform.Domain.Enums.DocumentManagement;

// MOD-0029-FU07 — Document identifier (Permanent UID / Document Code) allocation enums (GMG-QMS-SOP-0001 §6.3, §9.2,
// §12.3). Kept in a dedicated file so FU07 ownership never edits the FU06 MasterRegisterEnums.cs surface.

/// <summary>MOD-0029-FU07 — which identifier a ledger row / counter governs.</summary>
public enum DocumentIdentifierType
{
    PermanentUid = 0,
    DocumentCode = 1
}

/// <summary>
/// MOD-0029-FU07 — allocation status. The ledger is append-and-status-change only (never hard-deleted); a value in
/// ANY status — including <see cref="Cancelled"/>/<see cref="Abandoned"/> — is NEVER reused (SOP §6.3 UID/code never
/// reused; gaps are permitted).
/// </summary>
public enum DocumentIdentifierAllocationStatus
{
    /// <summary>Reserved (e.g. manual/migration) but not yet bound to an effective document.</summary>
    Reserved = 0,

    /// <summary>Allocated and bound to a register entry.</summary>
    Assigned = 1,

    /// <summary>Allocation cancelled; the value is retained and never reused.</summary>
    Cancelled = 2,

    /// <summary>Document abandoned after allocation; the value is retained and never reused (SOP §6.3 gaps permitted).</summary>
    Abandoned = 3,

    /// <summary>Replaced under a controlled GDocP correction; the old value is retained and never reused.</summary>
    SupersededByCorrection = 4
}

/// <summary>MOD-0029-FU07 — why an identifier was allocated (audit/evidence, SOP §12.3 migration provenance).</summary>
public enum DocumentIdentifierAllocationReason
{
    NewDocument = 0,
    Migration = 1,
    ManualImport = 2,
    Correction = 3
}
