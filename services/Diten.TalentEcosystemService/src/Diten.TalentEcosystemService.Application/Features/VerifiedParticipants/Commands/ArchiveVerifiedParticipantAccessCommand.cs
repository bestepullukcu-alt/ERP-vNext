using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Commands;

public sealed record ArchiveVerifiedParticipantAccessCommand(Guid Id) : IRequest<Response<NoContent>>;
