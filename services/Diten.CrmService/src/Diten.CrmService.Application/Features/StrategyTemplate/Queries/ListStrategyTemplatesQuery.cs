using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Queries;

/// <summary>The template grid. <c>SegmentId</c> answers the reverse question — "which plays bind this segment?" —
/// without exposing a single member of it.</summary>
public sealed record ListStrategyTemplatesQuery(
    string? TemplateStatus,
    string? SubjectType,
    string? BusinessUnitId,
    string? TemplateCode,
    Guid? SegmentId,
    string? Search,
    bool IncludeArchived) : IRequest<Response<StrategyTemplateListDto>>;
