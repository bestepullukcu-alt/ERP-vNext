namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister.Models;

// DCP-005 (Phase 2a) — controlled-document CITATION read contract. A RICH SIBLING of the effectiveness contract:
// effectiveness answers "is it in force?" (state only — the activation gate); citation answers "what do I cite?"
// (uid / code / title / version / lifecycle + whether it may be cited) for the Task Center's governing-documents
// resolution, document picker and citation freezer. Both read the same live Document Master Register (MOD-0029-FU06)
// and share ONE citable judgment (ControlledDocumentLifecyclePolicy.IsOperationallyEffective), so the two contracts
// can never disagree about Effective/Blocked. This is a read contract — no writes.
//
// The identifier vocabulary (DocumentIdentifierKind) is REUSED from the effectiveness contract (DocumentEffectivenessModels)
// — one spelling of "by code / by uid" for both.

/// <summary>
/// DCP-005 — where a citation was resolved from. The Task side stamps this onto a frozen TaskDocumentReference so a
/// citation records its origin. This port only ever reads the Master Register, so it always emits
/// <see cref="MasterRegister"/>; the retired CSV-lookup origin lives on the Task side's own marker, not here.
/// </summary>
public enum DocumentCitationSource
{
    MasterRegister
}

/// <summary>
/// DCP-005 — one controlled document as it may be CITED. Shaped to mirror the Task side's <c>DocumentReferenceEntryDto</c>
/// so the consumer's mapping stays trivial.
/// <list type="bullet">
/// <item><see cref="Citable"/> is the SAME judgment the effectiveness gate uses
/// (<c>ControlledDocumentLifecyclePolicy.IsOperationallyEffective</c> — Effective ∨ UnderRevision), never a separate
/// status mapping.</item>
/// <item><see cref="BlockedReason"/> is the register's own LifecycleStatus name when <see cref="Citable"/> is false,
/// and null otherwise.</item>
/// </list>
/// A blocked document is still RETURNED by search (show it, refuse to let it be chosen); it is never hidden.
/// </summary>
public sealed record DocumentCitationItem(
    string Uid,
    string Code,
    string Title,
    string? Version,
    string Lifecycle,
    bool Citable,
    string? BlockedReason,
    DocumentCitationSource Source,
    Guid? RegisterEntryId);

/// <summary>
/// DCP-005 — a citation read result. For <c>ResolveDocumentCitationQuery</c> it carries one item per RESOLVED
/// identifier (an identifier with no register row is OMITTED — the caller diffs requested vs returned to find the
/// unresolved ones). For <c>SearchDocumentCitationQuery</c> it carries the matched rows (blocked ones included,
/// with <see cref="DocumentCitationItem.Citable"/> = false).
/// </summary>
public sealed record DocumentCitationResult(IReadOnlyList<DocumentCitationItem> Items);
