using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateProfiles.Commands;

public sealed record UpdateCandidateProfileCommand(Guid Id, CandidateProfileRequest Request) : IRequest<Response<NoContent>>;
