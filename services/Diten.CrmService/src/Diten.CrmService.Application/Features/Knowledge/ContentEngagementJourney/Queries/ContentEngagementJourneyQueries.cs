using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney.Queries;

/// <summary>Lists ContentEngagementJourneys for the tenant. <c>effectiveAt</c> filters to journeys effective at the
/// instant (in-memory; the effective window is a DateTimeOffset BSON array, never a server-side key). The Stages array
/// is projected OUT of the list rows (large-document guard); only the active/required counters and the
/// unresolved-path flag are carried. No query parameter recommends, scores or selects a journey.</summary>
public sealed record ListContentEngagementJourneysQuery(
    Guid? SubjectId = null,
    Guid? TopicId = null,
    Guid? AudienceProfileId = null,
    string? Language = null,
    string? Status = null,
    DateTimeOffset? EffectiveAt = null,
    string? JourneyCode = null,
    Guid? KnowledgePathId = null,
    string? Search = null,
    bool IncludeArchived = true) : IRequest<Response<ContentEngagementJourneyListDto>>;

/// <summary>Returns the journey with its embedded stages (resolved path). <c>effectiveAt</c> drives latest-published
/// resolution; defaults to now.</summary>
public sealed record GetContentEngagementJourneyQuery(
    Guid JourneyId, DateTimeOffset? EffectiveAt = null) : IRequest<Response<ContentEngagementJourneyDto>>;

/// <summary>Returns the ordered embedded stages of a journey (StageOrder → StageCode, resolved path). Archived stages
/// included only when <c>includeArchived</c>.</summary>
public sealed record GetContentEngagementJourneyStagesQuery(
    Guid JourneyId, bool IncludeArchived = false, DateTimeOffset? EffectiveAt = null)
    : IRequest<Response<ContentEngagementJourneyStageListDto>>;
