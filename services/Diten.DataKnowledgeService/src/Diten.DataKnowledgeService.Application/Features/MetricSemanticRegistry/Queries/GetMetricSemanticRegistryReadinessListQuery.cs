using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry.Queries;

public sealed record GetMetricSemanticRegistryReadinessListQuery : IRequest<Response<IReadOnlyList<MetricSemanticRegistryReadinessListItemDto>>>;
