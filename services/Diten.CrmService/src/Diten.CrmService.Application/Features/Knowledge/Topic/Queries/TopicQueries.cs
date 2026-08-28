using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Topic.Queries;

/// <summary>Lists topics for the tenant, optionally scoped to one subject. Archived rows are included by default.</summary>
public sealed record ListTopicsQuery(
    Guid? SubjectId = null,
    string? Status = null,
    string? Search = null,
    bool IncludeArchived = true) : IRequest<Response<TopicListDto>>;

public sealed record GetTopicQuery(Guid TopicId) : IRequest<Response<TopicDto>>;
