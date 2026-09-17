using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CandidatePipeline.Commands;

public sealed record DeleteCandidatePipelineReadinessCommand(Guid Id) : IRequest<Response<bool>>;
