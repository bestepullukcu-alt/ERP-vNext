using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.MetricSemanticRegistry.Commands;

public sealed record CreateMetricSemanticRegistryReadinessCommand(MetricSemanticRegistryReadinessCreateRequest Request) : IRequest<Response<Guid>>;
