namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// WP-SB-2 — where a Content Studio release came from: the content set, the released set revision (ids only — both
/// aggregates were removed by WP-CLN-1) and the pinned composition template (<see cref="ConceptChainTemplate"/> +
/// ChainVersion). Carried by the <see cref="KnowledgeContent"/> ("assembled presentation") and the
/// <see cref="KnowledgePath"/> a release produces. Provenance only — nothing reads it to decide behaviour. Null on
/// everything authored outside the Studio. Embedded value object (no TenantId / Version / repository).
/// <para>Not to be confused with <see cref="KnowledgeContent.ContentSetId"/>, which is the SCMM-13 language-variant
/// group, not a Content Studio set.</para>
/// <para><b>Obsolete (WP-KP-4).</b> The content set and its SB-2 release producer are retired; nothing writes this any
/// more. It stays as the provenance of what a release produced, so a stored document carrying it still reads (the class
/// map rejects unknown elements). WP-CLN-1 removed the set / revision code; these ids are plain data now. (Documented
/// rather than <c>[Obsolete]</c>-attributed: the attribute would raise CS0618 on the knowledge entities / commands that
/// only carry the field.)</para>
/// </summary>
public sealed class KnowledgeStudioOrigin
{
    public Guid ContentSetId { get; set; }
    public Guid ContentSetRevisionId { get; set; }
    public Guid ConceptChainTemplateId { get; set; }
    public string ChainVersion { get; set; } = string.Empty;
}
