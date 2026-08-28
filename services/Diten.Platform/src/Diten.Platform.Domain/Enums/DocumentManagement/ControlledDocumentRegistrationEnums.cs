namespace Diten.Platform.Domain.Enums.DocumentManagement;

/// <summary>MOD-0029-FU37 — governed ownership scope for controlled-document registration.</summary>
public enum DocumentScope
{
    Company = 0,
    Corporate = 1
}

public enum DocumentLinkScopeCompatibilityStatus
{
    Unvalidated = 0,
    Compatible = 1,
    Invalid = 2
}

/// <summary>
/// What the unified registration flow produces. A ControlledDocument is a governed document (lifecycle / approval /
/// release gates apply). A Record is a completed record (SOP §2): it stores a file and a register entry but is NOT a
/// controlled document — no lifecycle, approval, release gate or identifier allocation runs for it. A Variant is a
/// governed controlled document derived from a parent (translation / site adoption) whose content must differ from
/// the parent; the full localization governance is layered on top separately (Faz 2b).
/// </summary>
public enum RegistrationKind
{
    ControlledDocument = 0,
    Record = 1,
    Variant = 2
}

/// <summary>MOD-0029 — kind of document variant (document-centric; independent of the FU03 template-variant model).</summary>
public enum DocumentVariantType
{
    Translation = 0,
    SiteAdoption = 1
}

/// <summary>MOD-0029-FU36 — durable registration orchestration state. This is not a document lifecycle.</summary>
public enum ControlledDocumentRegistrationStatus
{
    Pending = 0,
    ContentStored = 1,
    DocumentCreated = 2,
    RegisterCreated = 3,
    Linked = 4,
    Completed = 5,
    CompensationPending = 6,
    Failed = 7
}
