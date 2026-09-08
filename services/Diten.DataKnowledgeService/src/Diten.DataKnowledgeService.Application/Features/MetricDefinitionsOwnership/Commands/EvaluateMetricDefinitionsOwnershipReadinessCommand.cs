using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.MetricDefinitionsOwnership.Commands;

public sealed record EvaluateMetricDefinitionsOwnershipReadinessCommand(Guid Id) : IRequest<Response<MetricDefinitionsOwnershipReadinessDto>>;
