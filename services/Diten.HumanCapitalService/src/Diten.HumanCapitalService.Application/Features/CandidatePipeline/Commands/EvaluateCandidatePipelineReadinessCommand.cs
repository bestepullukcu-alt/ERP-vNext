using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CandidatePipeline.Commands;

public sealed record EvaluateCandidatePipelineReadinessCommand(Guid Id) : IRequest<Response<CandidatePipelineReadinessDto>>;
