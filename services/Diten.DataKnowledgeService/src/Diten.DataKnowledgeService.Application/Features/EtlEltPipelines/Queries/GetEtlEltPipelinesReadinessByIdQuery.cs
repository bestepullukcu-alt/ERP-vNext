using Diten.DataKnowledgeService.Application.Common;
using MediatR;

namespace Diten.DataKnowledgeService.Application.Features.EtlEltPipelines.Queries;

public sealed record GetEtlEltPipelinesReadinessByIdQuery(Guid Id) : IRequest<Response<EtlEltPipelinesReadinessDto>>;
