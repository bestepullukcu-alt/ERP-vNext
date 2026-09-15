using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateProfiles.Commands;

public sealed record CreateCandidateProfileCommand(CandidateProfileRequest Request) : IRequest<Response<Guid>>;
