using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.ScorecardsDashboards.Queries;

public sealed record GetScorecardsDashboardsAuditMetadataQuery(Guid Id) : IRequest<Response<ScorecardsDashboardsAuditMetadataDto>>;
