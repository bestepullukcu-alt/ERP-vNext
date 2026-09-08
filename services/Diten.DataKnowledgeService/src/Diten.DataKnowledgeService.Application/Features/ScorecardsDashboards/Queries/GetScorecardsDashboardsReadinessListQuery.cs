using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards.Queries;

public sealed record GetScorecardsDashboardsReadinessListQuery : IRequest<Response<IReadOnlyList<ScorecardsDashboardsReadinessListItemDto>>>;
