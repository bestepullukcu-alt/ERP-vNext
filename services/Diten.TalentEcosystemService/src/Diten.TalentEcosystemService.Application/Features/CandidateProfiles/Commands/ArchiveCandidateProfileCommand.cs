using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateProfiles.Commands;

public sealed record ArchiveCandidateProfileCommand(Guid Id) : IRequest<Response<NoContent>>;
