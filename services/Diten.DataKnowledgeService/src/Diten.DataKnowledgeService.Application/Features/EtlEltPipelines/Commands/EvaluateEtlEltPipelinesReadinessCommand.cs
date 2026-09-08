using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.EtlEltPipelines.Commands;

public sealed record EvaluateEtlEltPipelinesReadinessCommand(Guid Id) : IRequest<Response<EtlEltPipelinesReadinessDto>>;
