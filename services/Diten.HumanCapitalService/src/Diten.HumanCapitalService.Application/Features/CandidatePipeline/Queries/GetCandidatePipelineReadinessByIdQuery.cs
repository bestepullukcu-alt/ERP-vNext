using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CandidatePipeline.Queries;

public sealed record GetCandidatePipelineReadinessByIdQuery(Guid Id) : IRequest<Response<CandidatePipelineReadinessDto>>;
