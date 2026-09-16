using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateCareerPassport.Commands;

public sealed record EvaluateCandidateCareerPassportReadinessCommand(Guid Id) : IRequest<Response<CandidateCareerPassportReadinessDto>>;
