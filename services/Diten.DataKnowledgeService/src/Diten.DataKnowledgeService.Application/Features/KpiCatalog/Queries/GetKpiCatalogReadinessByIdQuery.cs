using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.KpiCatalog.Queries;

public sealed record GetKpiCatalogReadinessByIdQuery(Guid Id) : IRequest<Response<KpiCatalogReadinessDto>>;
