using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateProfiles.Commands;

public sealed record EvaluateCandidateProfileCommand(Guid Id, EvaluateCandidateProfileRequest Request)
    : IRequest<Response<CandidateProfileEvaluationDto>>;
