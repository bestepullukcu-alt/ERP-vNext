using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Queries;

public sealed record GetVerifiedParticipantAccessByIdQuery(Guid Id) : IRequest<Response<VerifiedParticipantAccessDto>>;
