using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Commands;

public sealed record EvaluateCandidateDisputeReadinessCommand(Guid Id, EvaluateCandidateDisputeReadinessRequest Request)
    : IRequest<Response<CandidateDisputeEvaluationDto>>;
