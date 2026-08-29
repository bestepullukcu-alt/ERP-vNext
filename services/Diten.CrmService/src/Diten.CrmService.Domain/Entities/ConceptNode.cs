namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0162 FU03 — ConceptNode (legacy <c>UCLNList</c>). The concrete value of a <see cref="ConceptType"/>
/// (e.g. an actual indication, a specific objection, one SOP). <see cref="EntityBase.Id"/> is the ConceptNodeId that
/// FU02 <c>KnowledgeContent.ConceptNodeId</c> points at. The node is <b>never the system of record</b> of any master:
/// it carries at most one explicit external reference (<see cref="ExternalRefType"/> + <see cref="ExternalRefId"/>) and
/// copies no master field — for a product the target is the MDM Global Product (<c>global-product</c>). Closing a node
/// is the soft <see cref="ArchivedAt"/> lifecycle; there is no hard delete and an archived node accepts no update.
/// </summary>
public sealed class ConceptNode : EntityBase
{
    public Guid SubjectId { get; set; }

    /// <summary>The node's type. The type's subject must equal <see cref="SubjectId"/> (else 400).</summary>
    public Guid ConceptTypeId { get; set; }

    /// <summary>Stable business key, unique within (SubjectId, ConceptTypeId) among non-archived rows.</summary>
    public string ConceptNodeCode { get; set; } = string.Empty;

    public string ConceptNodeName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary><see cref="ConceptStatuses"/> — draft / active / inactive / archived.</summary>
    public string Status { get; set; } = ConceptStatuses.Draft;

    public DateTimeOffset EffectiveFrom { get; set; }

    /// <summary>Open-ended when null. EffectiveFrom / EffectiveTo are DateTimeOffset (BSON array): never both used as
    /// index keys nor sorted server-side (parallel-array trap).</summary>
    public DateTimeOffset? EffectiveTo { get; set; }

    /// <summary><see cref="ConceptExternalRefTypes"/> — global-product / document / audience-profile /
    /// reference-data-value / other. Optional; the master stays the SoR, nothing is copied.</summary>
    public string? ExternalRefType { get; set; }

    /// <summary>Identifier in the referenced master (e.g. the MDM Global Product Id). Provenance only.</summary>
    public string? ExternalRefId { get; set; }

    /// <summary>Escape hatch for annotations. No business rule is ever read from here.</summary>
    public string? MetadataJson { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;

    /// <summary>Effective at the instant (read-only helper; draws no traversal/recommendation conclusion).</summary>
    public bool IsEffectiveAt(DateTimeOffset at)
        => EffectiveFrom <= at && (EffectiveTo is null || at <= EffectiveTo);
}
