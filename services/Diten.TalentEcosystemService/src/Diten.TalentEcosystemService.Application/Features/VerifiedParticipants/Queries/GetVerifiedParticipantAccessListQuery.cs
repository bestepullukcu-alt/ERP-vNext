using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.VerifiedParticipants.Queries;

public sealed record GetVerifiedParticipantAccessListQuery : IRequest<Response<IReadOnlyList<VerifiedParticipantAccessListItemDto>>>;
