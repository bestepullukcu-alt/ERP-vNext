namespace Diten.CrmService.Application.Features.Knowledge;

/// <summary>MOD-0162 FU02 read model for a knowledge content row. TenantId is never echoed (server-resolved). Every
/// Subject/Topic/AudienceProfile/Concept/Brand/Product/Campaign/Segment member is an ID reference; no master field is
/// projected, because a copied name goes stale.</summary>
public sealed record KnowledgeContentDto(
    Guid ContentId,
    string ContentCode,
    string ContentTitle,
    string ContentType,
    string ContentStatus,
    Guid SubjectId,
    Guid? TopicId,
    Guid? AudienceProfileId,
    Guid? ConceptNodeId,
    Guid? BrandId,
    Guid? ProductId,
    Guid? CampaignId,
    Guid? SegmentId,
    string LanguageCode,
    string? Summary,
    string? ContentBodyRef,
    string? ContentAssetRef,
    string? FileRef,
    string? Url,
    string ContentVersion,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string Source,
    IReadOnlyList<string> Tags,
    IReadOnlyList<KnowledgeExternalReferenceDto> ExternalReferences,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived);

public sealed record KnowledgeContentListDto(IReadOnlyList<KnowledgeContentDto> Items, int Total);

/// <summary>MOD-0162 FU02 read model for a subject taxonomy row.</summary>
public sealed record SubjectDto(
    Guid SubjectId,
    string SubjectCode,
    string SubjectName,
    Guid? ParentSubjectId,
    string? Description,
    string Status,
    int SortOrder,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    IReadOnlyList<string> Alias,
    IReadOnlyList<KnowledgeExternalReferenceDto> ExternalReferences,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived);

public sealed record SubjectListDto(IReadOnlyList<SubjectDto> Items, int Total);

/// <summary>MOD-0162 FU02 read model for a topic taxonomy row (subject-scoped, hierarchical).</summary>
public sealed record TopicDto(
    Guid TopicId,
    Guid SubjectId,
    string TopicCode,
    Guid? ParentTopicId,
    string TopicName,
    string? Description,
    string Status,
    int SortOrder,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    IReadOnlyList<string> Alias,
    IReadOnlyList<KnowledgeExternalReferenceDto> ExternalReferences,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived);

public sealed record TopicListDto(IReadOnlyList<TopicDto> Items, int Total);

/// <summary>MOD-0162 FU02 read model for an audience-profile row.</summary>
public sealed record AudienceProfileDto(
    Guid AudienceProfileId,
    string ProfileCode,
    string ProfileName,
    string? Description,
    string? ProfileType,
    string Status,
    int SortOrder,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    IReadOnlyList<string> Alias,
    IReadOnlyList<KnowledgeExternalReferenceDto> ExternalReferences,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived);

public sealed record AudienceProfileListDto(IReadOnlyList<AudienceProfileDto> Items, int Total);

/// <summary>External/legacy identity as echoed back (same six-field contract as MOD-0290-FU01 / MOD-0165-FU04).</summary>
public sealed record KnowledgeExternalReferenceDto(
    string SourceSystem,
    string ExternalId,
    string? ExternalCode,
    string? ExternalName,
    DateTimeOffset? ImportedAt,
    bool IsPrimary);

/// <summary>Inbound external-reference line shared by the knowledge write commands.</summary>
public sealed record KnowledgeExternalReferenceInput(
    string SourceSystem,
    string ExternalId,
    string? ExternalCode = null,
    string? ExternalName = null,
    DateTimeOffset? ImportedAt = null,
    bool IsPrimary = false);
