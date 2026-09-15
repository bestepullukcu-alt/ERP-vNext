using Diten.HumanCapitalService.Application.Common;
using MediatR;

namespace Diten.HumanCapitalService.Application.Features.CandidatePipeline.Queries;

public sealed record GetCandidatePipelineReadinessListQuery : IRequest<Response<IReadOnlyList<CandidatePipelineReadinessListItemDto>>>;
