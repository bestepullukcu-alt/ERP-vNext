using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Commands;

public sealed record EvaluateVerifiedParticipantAccessCommand(Guid Id, EvaluateVerifiedParticipantAccessRequest Request)
    : IRequest<Response<VerifiedParticipantEvaluationDto>>;
