using Diten.CrmService.Domain.Entities;
using SubjectEntity = Diten.CrmService.Domain.Entities.Subject;
using TopicEntity = Diten.CrmService.Domain.Entities.Topic;
using AudienceProfileEntity = Diten.CrmService.Domain.Entities.AudienceProfile;

namespace Diten.CrmService.Application.Features.Knowledge;

/// <summary>Aggregate ↔ DTO projection for MOD-0162 FU02. Reads never echo TenantId (server-resolved). Reference ids are
/// projected as-is; no master field is resolved or copied.</summary>
public static class KnowledgeMapper
{
    public static KnowledgeContentDto ToDto(KnowledgeContent c) => new(
        c.Id,
        c.ContentCode,
        c.ContentTitle,
        c.ContentType,
        c.ContentStatus,
        c.SubjectId,
        c.TopicId,
        c.AudienceProfileId,
        c.ConceptNodeId,
        c.BrandId,
        c.ProductId,
        c.CampaignId,
        c.SegmentId,
        c.LanguageCode,
        c.Summary,
        c.ContentBodyRef,
        c.ContentAssetRef,
        c.FileRef,
        c.Url,
        c.ContentVersion,
        c.EffectiveFrom,
        c.EffectiveTo,
        c.Source,
        c.Tags.ToList(),
        c.ExternalReferences.Select(ToDto).ToList(),
        c.CreatedAt,
        c.CreatedBy,
        c.UpdatedAt,
        c.UpdatedBy,
        c.ArchivedAt,
        c.ArchivedBy,
        c.IsArchived());

    public static SubjectDto ToDto(SubjectEntity s) => new(
        s.Id,
        s.SubjectCode,
        s.SubjectName,
        s.ParentSubjectId,
        s.Description,
        s.Status,
        s.SortOrder,
        s.EffectiveFrom,
        s.EffectiveTo,
        s.Alias.ToList(),
        s.ExternalReferences.Select(ToDto).ToList(),
        s.CreatedAt,
        s.CreatedBy,
        s.UpdatedAt,
        s.UpdatedBy,
        s.ArchivedAt,
        s.ArchivedBy,
        s.IsArchived());

    public static TopicDto ToDto(TopicEntity t) => new(
        t.Id,
        t.SubjectId,
        t.TopicCode,
        t.ParentTopicId,
        t.TopicName,
        t.Description,
        t.Status,
        t.SortOrder,
        t.EffectiveFrom,
        t.EffectiveTo,
        t.Alias.ToList(),
        t.ExternalReferences.Select(ToDto).ToList(),
        t.CreatedAt,
        t.CreatedBy,
        t.UpdatedAt,
        t.UpdatedBy,
        t.ArchivedAt,
        t.ArchivedBy,
        t.IsArchived());

    public static AudienceProfileDto ToDto(AudienceProfileEntity p) => new(
        p.Id,
        p.ProfileCode,
        p.ProfileName,
        p.Description,
        p.ProfileType,
        p.Status,
        p.SortOrder,
        p.EffectiveFrom,
        p.EffectiveTo,
        p.Alias.ToList(),
        p.ExternalReferences.Select(ToDto).ToList(),
        p.CreatedAt,
        p.CreatedBy,
        p.UpdatedAt,
        p.UpdatedBy,
        p.ArchivedAt,
        p.ArchivedBy,
        p.IsArchived());

    public static KnowledgeExternalReferenceDto ToDto(KnowledgeExternalReference r) => new(
        r.SourceSystem, r.ExternalId, r.ExternalCode, r.ExternalName, r.ImportedAt, r.IsPrimary);

    /// <summary>Inbound external-reference lines → stored value objects. Caller-supplied <c>ImportedAt</c> is preserved
    /// (legacy history is never rewritten) and stamped with "now" only when omitted.</summary>
    public static List<KnowledgeExternalReference> ToEntities(
        IReadOnlyList<KnowledgeExternalReferenceInput>? inputs, DateTimeOffset now)
        => inputs is null
            ? new List<KnowledgeExternalReference>()
            : inputs.Select(i => new KnowledgeExternalReference
            {
                SourceSystem = i.SourceSystem.Trim(),
                ExternalId = i.ExternalId.Trim(),
                ExternalCode = string.IsNullOrWhiteSpace(i.ExternalCode) ? null : i.ExternalCode.Trim(),
                ExternalName = string.IsNullOrWhiteSpace(i.ExternalName) ? null : i.ExternalName.Trim(),
                ImportedAt = i.ImportedAt ?? now,
                IsPrimary = i.IsPrimary
            }).ToList();

    public static List<string> CleanTags(IReadOnlyList<string>? tags)
        => tags is null
            ? new List<string>()
            : tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct().ToList();

    public static List<string> CleanAlias(IReadOnlyList<string>? alias)
        => alias is null
            ? new List<string>()
            : alias.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).Distinct().ToList();
}
