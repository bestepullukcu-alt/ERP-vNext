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
    bool IsArchived,
    // SCMM-13 language-variant linkage (additive tail — see KnowledgeContent §13).
    Guid ContentSetId,
    bool IsSourceLanguage,
    string TranslationStatus,
    // WP-CL-BE-6 claim references (additive tail). The detail read enriches each ref with the claim's current status.
    IReadOnlyList<KnowledgeContentClaimRefDto>? ClaimRefs = null);

/// <summary>WP-CL-BE-6 — one claim a content uses. <see cref="ClaimStatus"/> / <see cref="CountryVersionStatus"/> are
/// the CURRENT statuses of the bound claim record / country version (filled on the detail read; null on list reads or
/// when the record no longer resolves). <see cref="ClaimNeedsReview"/> is true when the bound record is
/// <c>review-required</c> (usable, but a newer core version exists).</summary>
public sealed record KnowledgeContentClaimRefDto(
    string ClaimCode,
    Guid ClaimId,
    Guid? CountryVersionId,
    string? CountryCode,
    string? ClaimStatus = null,
    string? CountryVersionStatus = null,
    bool ClaimNeedsReview = false);

/// <summary>WP-CL-BE-6 — write shape of one claim reference. <see cref="CountryVersionId"/> set ⇒ country-specific
/// content (<see cref="CountryCode"/> may be omitted — it is taken from the version, and must match it when given);
/// empty ⇒ global content bound to the core claim.</summary>
public sealed record KnowledgeContentClaimRefInput(
    string ClaimCode,
    Guid ClaimId,
    Guid? CountryVersionId = null,
    string? CountryCode = null);

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
/// <summary>SCMM-11 (AUD, RM3) read model for one multi-axis assignment.</summary>
public sealed record AudienceDimensionAssignmentDto(string AxisCode, IReadOnlyList<string> Values);

public sealed record AudienceProfileDto(
    Guid AudienceProfileId,
    string ProfileCode,
    string ProfileName,
    string? Description,
    Guid? SubjectId,
    string? ProfileType,
    IReadOnlyList<AudienceDimensionAssignmentDto> Dimensions,
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
