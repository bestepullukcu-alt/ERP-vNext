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

    /// <summary><see cref="ConceptStatuses"/> — draft / active / inactive / archived.</summary>
    public string Status { get; set; } = ConceptStatuses.Draft;

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;
}
