using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry.Queries;

public sealed record GetMetricSemanticRegistryReadinessByIdQuery(Guid Id) : IRequest<Response<MetricSemanticRegistryReadinessDto>>;
