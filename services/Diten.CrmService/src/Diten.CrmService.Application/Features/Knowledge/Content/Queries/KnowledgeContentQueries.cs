using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Content.Queries;

/// <summary>Lists knowledge content for the tenant. Archived rows are included by default so history stays visible.
/// Filters are applied in memory (DateTimeOffset effective window is not a server-side sort key).</summary>
public sealed record ListKnowledgeContentQuery(
    string? ContentType = null,
    string? ContentStatus = null,
    Guid? SubjectId = null,
    Guid? TopicId = null,
    Guid? AudienceProfileId = null,
    string? LanguageCode = null,
    Guid? BrandId = null,
    Guid? ProductId = null,
    Guid? CampaignId = null,
    DateTimeOffset? EffectiveAt = null,
    string? Search = null,
    bool IncludeArchived = true) : IRequest<Response<KnowledgeContentListDto>>;

public sealed record GetKnowledgeContentQuery(Guid ContentId) : IRequest<Response<KnowledgeContentDto>>;
