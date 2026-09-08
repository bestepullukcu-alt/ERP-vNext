using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards.Commands;

public sealed record EvaluateScorecardsDashboardsReadinessCommand(Guid Id) : IRequest<Response<ScorecardsDashboardsReadinessDto>>;
