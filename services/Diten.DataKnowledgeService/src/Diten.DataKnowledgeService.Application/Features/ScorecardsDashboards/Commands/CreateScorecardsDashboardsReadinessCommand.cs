using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards.Commands;

public sealed record CreateScorecardsDashboardsReadinessCommand(ScorecardsDashboardsReadinessCreateRequest Request) : IRequest<Response<Guid>>;
