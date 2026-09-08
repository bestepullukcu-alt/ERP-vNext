using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.MetricDefinitionsOwnership.Commands;

public sealed record CreateMetricDefinitionsOwnershipReadinessCommand(MetricDefinitionsOwnershipReadinessCreateRequest Request) : IRequest<Response<Guid>>;
