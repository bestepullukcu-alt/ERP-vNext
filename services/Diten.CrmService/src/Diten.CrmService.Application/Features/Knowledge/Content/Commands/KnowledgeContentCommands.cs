using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Content.Commands;

/// <summary>
/// MOD-0162 FU02 knowledge content write surface. <c>TenantId</c> is NEVER accepted from the payload (server-resolved
/// from the JWT claim). There is deliberately NO delete command — closing content is
/// <see cref="ArchiveKnowledgeContentCommand"/> (soft lifecycle), so content history stays readable.
/// </summary>
public sealed record CreateKnowledgeContentCommand(
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
    IReadOnlyList<KnowledgeExternalReferenceInput>? ExternalReferences = null) : IRequest<Response<Guid>>;

/// <summary>Full replace of the mutable fields of content. <c>ContentCode</c> is immutable (rename goes through
/// <c>ContentTitle</c>). Archived content cannot be updated.</summary>
public sealed record UpdateKnowledgeContentCommand(
    Guid ContentId,
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
    IReadOnlyList<KnowledgeExternalReferenceInput>? ExternalReferences = null) : IRequest<Response<bool>>;

/// <summary>Archives content (ArchivedAt/By stamped, status → archived). Still readable; accepts no update afterwards.</summary>
public sealed record ArchiveKnowledgeContentCommand(Guid ContentId) : IRequest<Response<bool>>;
