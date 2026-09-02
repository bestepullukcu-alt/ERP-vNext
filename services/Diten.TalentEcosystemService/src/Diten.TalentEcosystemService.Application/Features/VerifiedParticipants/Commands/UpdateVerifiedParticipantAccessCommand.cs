using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Commands;

public sealed record UpdateVerifiedParticipantAccessCommand(Guid Id, VerifiedParticipantAccessRequest Request)
    : IRequest<Response<VerifiedParticipantAccessDto>>;
