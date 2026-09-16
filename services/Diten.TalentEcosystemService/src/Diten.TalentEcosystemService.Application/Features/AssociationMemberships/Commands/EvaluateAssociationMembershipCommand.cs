using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.AssociationMemberships.Commands;

public sealed record EvaluateAssociationMembershipCommand(Guid Id, EvaluateAssociationMembershipRequest Request)
    : IRequest<Response<AssociationMembershipEvaluationDto>>;
