namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0162 FU03 — ConceptRelationship (legacy <c>UCLNConnection</c>). A directed edge between two nodes of the same
/// subject. A self-loop (<c>From == To</c>) and a cross-subject edge are rejected 400; a cycle among <c>active</c> edges
/// is rejected 400 (read-time, no cache); a second active edge on the same (From, To, RelationshipType) triple is a 409.
/// An edge whose (fromType → toType) pair is not in any chain template is NOT rejected — it is accepted and flagged
/// <see cref="IsTemplateConforming"/> = false, so a non-conforming edge is visible rather than silently kept or dropped.
/// Direction is explicit: a <c>bidirectional</c> edge is a declaration, never an auto-derived reverse edge.
/// </summary>
public sealed class ConceptRelationship : EntityBase
{
    public Guid SubjectId { get; set; }

    public Guid FromConceptNodeId { get; set; }
    public Guid ToConceptNodeId { get; set; }

    /// <summary><see cref="ConceptRelationshipTypes"/> — boundary canonical set (leads-to / requires / addresses /
    /// evidences / belongs-to / custom).</summary>
    public string RelationshipType { get; set; } = ConceptRelationshipTypes.LeadsTo;

    /// <summary>Stable business key + deterministic tie-break anchor.</summary>
    public string RelationshipCode { get; set; } = string.Empty;

    public string RelationshipName { get; set; } = string.Empty;

    /// <summary><see cref="ConceptDirections"/> — outbound (default) / bidirectional. A reverse edge is never derived.</summary>
    public string Direction { get; set; } = ConceptDirections.Outbound;

    /// <summary>Lower value first; ties broken by <see cref="RelationshipCode"/> order (deterministic).</summary>
    public int Priority { get; set; }

    /// <summary>Derived (§6.1): whether the (fromType → toType) pair appears in any non-archived chain template for the
    /// subject. Does not reject — makes conformance visible.</summary>
    public bool IsTemplateConforming { get; set; }

    /// <summary><see cref="ConceptStatuses"/> — draft / active / inactive / archived.</summary>
    public string Status { get; set; } = ConceptStatuses.Draft;

    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;

    /// <summary>Active = status active AND not archived. Cycle detection considers only active edges.</summary>
    public bool IsActive()
        => !IsArchived()
           && string.Equals(Status, ConceptStatuses.Active, StringComparison.OrdinalIgnoreCase);
}
