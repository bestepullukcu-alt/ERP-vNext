namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0162 FU03 — ConceptType (legacy <c>UCLEType</c>). Answers "which kinds of concept exist in this subject?"
/// (indication · audience-profile · objection · key-message · sop · control-point · …). It is <b>subject-scoped</b>:
/// there is no single global concept graph, so a pharma subject and a QMS subject never share a type pool.
/// <see cref="ConceptTypeCode"/> is the stable business key (rename goes through <see cref="ConceptTypeName"/> only).
/// Closing a type is the soft <see cref="ArchivedAt"/> lifecycle; there is no hard delete and an archived type accepts
/// no update. This is a configuration surface only — no traversal, resolution or recommendation engine lives here.
/// </summary>
public sealed class ConceptType : EntityBase
{
    /// <summary>Owning subject (MOD-0162 FU02). A new type cannot be created under an archived subject.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>Stable business key, unique within (TenantId, SubjectId) among non-archived rows. Never renamed.</summary>
    public string ConceptTypeCode { get; set; } = string.Empty;

    public string ConceptTypeName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Management ordering — NOT the chain order (that is <see cref="ConceptChainTemplate.OrderedConceptTypes"/>).</summary>
    public int SortOrder { get; set; }

    /// <summary>SCMM-09 (①) — optional display colour as a CSS hex string (<c>#RGB</c> or <c>#RRGGBB</c>). Presentation
    /// only; no business rule is ever read from it.</summary>
    public string? Color { get; set; }

    /// <summary>SCMM-09 (①) — legacy <c>UCLEType.isGroup</c>: this type groups other types (a heading) rather than
    /// holding leaf value nodes. Metadata only; no traversal or engine behaviour changes.</summary>
    public bool IsGroup { get; set; }

    /// <summary>SCMM-09 (①) — legacy <c>UCLEType.isList</c>: this type is authored as a list of nodes. Metadata only.</summary>
    public bool IsList { get; set; }

    /// <summary>SCMM-09 (①) — optional hierarchical parent type within the SAME subject (RM1, DEC-SCMM-03). A self-parent,
    /// an archived/cross-subject parent or a cycle is rejected 400. This is the type hierarchy — distinct from
    /// <see cref="SortOrder"/> (management ordering) and from the chain order. Archiving does not cascade.</summary>
    public Guid? ParentConceptTypeId { get; set; }

    /// <summary><see cref="ConceptStatuses"/> — draft / active / inactive / archived.</summary>
    public string Status { get; set; } = ConceptStatuses.Draft;

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;
}
