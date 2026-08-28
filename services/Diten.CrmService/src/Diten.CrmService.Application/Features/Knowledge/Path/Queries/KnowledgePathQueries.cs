using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Path.Queries;

/// <summary>Lists KnowledgePaths for the tenant. <c>effectiveAt</c> filters to paths effective at the instant (in-memory;
/// the effective window is a DateTimeOffset BSON array, never a server-side key). The Steps array is projected OUT of
/// the list rows (large-document guard); only the active/required counters and the unresolved-content flag are carried.</summary>
public sealed record ListKnowledgePathsQuery(
    Guid? SubjectId = null,
    Guid? TopicId = null,
    Guid? AudienceProfileId = null,
    string? Language = null,
    string? Status = null,
    DateTimeOffset? EffectiveAt = null,
    string? PathCode = null,
    string? Search = null,
    bool IncludeArchived = true) : IRequest<Response<KnowledgePathListDto>>;

/// <summary>Returns the path with its embedded steps (resolved content). <c>effectiveAt</c> drives latest-published
/// resolution; defaults to now.</summary>
public sealed record GetKnowledgePathQuery(
    Guid PathId, DateTimeOffset? EffectiveAt = null) : IRequest<Response<KnowledgePathDto>>;

/// <summary>Returns the ordered embedded steps of a path (StepOrder → StepCode, resolved content). Archived steps
/// included only when <c>includeArchived</c>.</summary>
public sealed record GetKnowledgePathStepsQuery(
    Guid PathId, bool IncludeArchived = false, DateTimeOffset? EffectiveAt = null)
    : IRequest<Response<KnowledgePathStepListDto>>;
