using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.MetricDefinitionsOwnership.Commands;

public sealed record DeleteMetricDefinitionsOwnershipReadinessCommand(Guid Id) : IRequest<Response<bool>>;
