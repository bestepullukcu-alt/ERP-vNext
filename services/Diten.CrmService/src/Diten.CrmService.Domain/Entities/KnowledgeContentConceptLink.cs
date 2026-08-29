namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0162 FU03 — KnowledgeContentConceptLink (new; AC-LINK). A many-to-many link between FU02
/// <see cref="KnowledgeContent"/> and a <see cref="ConceptNode"/>, because the same content can serve several concepts.
/// A link is <b>always anchored to a node</b> (<see cref="ConceptNodeId"/> required — the node is the addressable unit
/// of the graph). When the content belongs to a relationship <i>context</i>, <see cref="ConceptRelationshipId"/> is
/// supplied in addition and that relationship must contain the anchored node (its From or To) — there is no node-less
/// pure relationship link. Archived content / archived node accept no new link. Closing a link is the soft archive
/// lifecycle; the FU02 <c>KnowledgeContent.ConceptNodeId</c> shortcut stays and is neither removed nor moved.
/// </summary>
public sealed class KnowledgeContentConceptLink : EntityBase
{
    public Guid KnowledgeContentId { get; set; }

    /// <summary>Required anchor node. Archived node accepts no new link.</summary>
    public Guid ConceptNodeId { get; set; }

    /// <summary>Optional relationship context. When present it must contain <see cref="ConceptNodeId"/> (From or To).</summary>
    public Guid? ConceptRelationshipId { get; set; }

    /// <summary><see cref="ConceptLinkRoles"/> — primary / supporting / evidence / objection-handling.</summary>
    public string LinkRole { get; set; } = ConceptLinkRoles.Primary;

    /// <summary>Deterministic display order.</summary>
    public int SortOrder { get; set; }

    /// <summary><see cref="ConceptStatuses"/> — draft / active / inactive / archived.</summary>
    public string Status { get; set; } = ConceptStatuses.Active;

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;
}
