namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0162 FU02 — Topic. A hierarchical sub-area of a <see cref="Subject"/> (parent-child via
/// <see cref="ParentTopicId"/>). A topic lives only inside its own subject: a cross-subject parent, a self-parent or a
/// parent cycle is rejected (400). <see cref="TopicCode"/> is the stable business key inside the subject (rename is
/// done through <see cref="TopicName"/> / <see cref="Alias"/> only). Closing a topic is the soft
/// <see cref="ArchivedAt"/> lifecycle; there is no hard delete, and an archived topic accepts no new content.
/// Concept-graph notions (indication / need / benefit) are NOT embedded in this tree — those live in MOD-0162-FU01C.
/// </summary>
public sealed class Topic : EntityBase
{
    /// <summary>The subject this topic belongs to. A topic never changes subject.</summary>
    public Guid SubjectId { get; set; }

    /// <summary>Stable business key inside the subject. Never renamed.</summary>
    public string TopicCode { get; set; } = string.Empty;

    /// <summary>Optional parent within the SAME subject. Null for a root topic.</summary>
    public Guid? ParentTopicId { get; set; }

    public string TopicName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary><see cref="TaxonomyStatuses"/> — draft / active / inactive / archived.</summary>
    public string Status { get; set; } = TaxonomyStatuses.Draft;

    public int SortOrder { get; set; }

    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    public List<string> Alias { get; set; } = new();

    public List<KnowledgeExternalReference> ExternalReferences { get; set; } = new();

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;
}
