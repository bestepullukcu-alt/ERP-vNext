using Diten.CrmService.Application.Features.Knowledge;

namespace Diten.CrmService.Api.Models.CRM;

// MOD-0162 FU02 request models. TenantId is NEVER part of any request body — it is server-resolved from the JWT claim.
// Route ids (contentId / subjectId / topicId / audienceProfileId) come from the path, never the body.

public sealed record CreateKnowledgeContentRequest(
    string ContentCode,
    string ContentTitle,
    string ContentType,
    Guid SubjectId,
    string LanguageCode,
    string ContentVersion,
    DateTimeOffset EffectiveFrom,
    string? ContentStatus = null,
    Guid? TopicId = null,
    Guid? AudienceProfileId = null,
    Guid? ConceptNodeId = null,
    Guid? BrandId = null,
    Guid? ProductId = null,
    Guid? CampaignId = null,
    Guid? SegmentId = null,
    string? Summary = null,
    string? ContentBodyRef = null,
    string? ContentAssetRef = null,
    string? FileRef = null,
    string? Url = null,
    DateTimeOffset? EffectiveTo = null,
    string? Source = null,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<KnowledgeExternalReferenceInput>? ExternalReferences = null);

public sealed record UpdateKnowledgeContentRequest(
    string ContentTitle,
    string ContentType,
    Guid SubjectId,
    string LanguageCode,
    string ContentVersion,
    DateTimeOffset EffectiveFrom,
    string? ContentStatus = null,
    Guid? TopicId = null,
    Guid? AudienceProfileId = null,
    Guid? ConceptNodeId = null,
    Guid? BrandId = null,
    Guid? ProductId = null,
    Guid? CampaignId = null,
    Guid? SegmentId = null,
    string? Summary = null,
    string? ContentBodyRef = null,
    string? ContentAssetRef = null,
    string? FileRef = null,
    string? Url = null,
    DateTimeOffset? EffectiveTo = null,
    string? Source = null,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<KnowledgeExternalReferenceInput>? ExternalReferences = null);

public sealed record CreateSubjectRequest(
    string SubjectCode,
    string SubjectName,
    DateTimeOffset EffectiveFrom,
    Guid? ParentSubjectId = null,
    string? Description = null,
    string? Status = null,
    int SortOrder = 0,
    DateTimeOffset? EffectiveTo = null,
    IReadOnlyList<string>? Alias = null,
    IReadOnlyList<KnowledgeExternalReferenceInput>? ExternalReferences = null);

public sealed record UpdateSubjectRequest(
    string SubjectName,
    DateTimeOffset EffectiveFrom,
    Guid? ParentSubjectId = null,
    string? Description = null,
    string? Status = null,
    int SortOrder = 0,
    DateTimeOffset? EffectiveTo = null,
    IReadOnlyList<string>? Alias = null,
    IReadOnlyList<KnowledgeExternalReferenceInput>? ExternalReferences = null);

public sealed record CreateTopicRequest(
    Guid SubjectId,
    string TopicCode,
    string TopicName,
    DateTimeOffset EffectiveFrom,
    Guid? ParentTopicId = null,
    string? Description = null,
    string? Status = null,
    int SortOrder = 0,
    DateTimeOffset? EffectiveTo = null,
    IReadOnlyList<string>? Alias = null,
    IReadOnlyList<KnowledgeExternalReferenceInput>? ExternalReferences = null);

public sealed record UpdateTopicRequest(
    string TopicName,
    DateTimeOffset EffectiveFrom,
    Guid? ParentTopicId = null,
    string? Description = null,
    string? Status = null,
    int SortOrder = 0,
    DateTimeOffset? EffectiveTo = null,
    IReadOnlyList<string>? Alias = null,
    IReadOnlyList<KnowledgeExternalReferenceInput>? ExternalReferences = null);

public sealed record CreateAudienceProfileRequest(
    string ProfileCode,
    string ProfileName,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    string? ProfileType = null,
    string? Status = null,
    int SortOrder = 0,
    DateTimeOffset? EffectiveTo = null,
    IReadOnlyList<string>? Alias = null,
    IReadOnlyList<KnowledgeExternalReferenceInput>? ExternalReferences = null);

public sealed record UpdateAudienceProfileRequest(
    string ProfileName,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    string? ProfileType = null,
    string? Status = null,
    int SortOrder = 0,
    DateTimeOffset? EffectiveTo = null,
    IReadOnlyList<string>? Alias = null,
    IReadOnlyList<KnowledgeExternalReferenceInput>? ExternalReferences = null);
