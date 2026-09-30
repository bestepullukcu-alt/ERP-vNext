namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// WP-SB-2 — where a Content Studio release came from: the <see cref="ContentSet"/>, the released
/// <see cref="ContentSetRevision"/> and the pinned composition template (<see cref="ConceptChainTemplate"/> +
/// ChainVersion). Carried by the <see cref="KnowledgeContent"/> ("assembled presentation") and the
/// <see cref="KnowledgePath"/> a release produces. Provenance only — nothing reads it to decide behaviour. Null on
/// everything authored outside the Studio. Embedded value object (no TenantId / Version / repository).
/// <para>Not to be confused with <see cref="KnowledgeContent.ContentSetId"/>, which is the SCMM-13 language-variant
/// group, not a Content Studio set.</para>
/// </summary>
public sealed class KnowledgeStudioOrigin
{
    public Guid ContentSetId { get; set; }
    public Guid ContentSetRevisionId { get; set; }
    public Guid ConceptChainTemplateId { get; set; }
    public string ChainVersion { get; set; } = string.Empty;
}
