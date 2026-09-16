using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Commands;

public sealed record VerifyParticipantAccessCommand(Guid Id, VerifyParticipantAccessRequest Request)
    : IRequest<Response<VerifiedParticipantAccessDto>>;
