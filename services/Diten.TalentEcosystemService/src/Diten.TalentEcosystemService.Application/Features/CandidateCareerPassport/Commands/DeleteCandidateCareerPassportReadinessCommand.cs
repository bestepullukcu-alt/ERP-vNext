using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateCareerPassport.Commands;

public sealed record DeleteCandidateCareerPassportReadinessCommand(Guid Id) : IRequest<Response<bool>>;
