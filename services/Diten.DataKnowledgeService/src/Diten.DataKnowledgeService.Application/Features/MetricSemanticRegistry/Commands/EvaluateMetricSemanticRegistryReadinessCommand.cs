using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry.Commands;

public sealed record EvaluateMetricSemanticRegistryReadinessCommand(Guid Id) : IRequest<Response<MetricSemanticRegistryReadinessDto>>;
