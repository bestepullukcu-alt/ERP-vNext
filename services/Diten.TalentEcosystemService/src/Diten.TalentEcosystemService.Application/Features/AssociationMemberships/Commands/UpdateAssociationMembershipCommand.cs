using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.AssociationMemberships.Commands;

public sealed record UpdateAssociationMembershipCommand(Guid Id, AssociationMembershipRegistryRequest Request)
    : IRequest<Response<AssociationMembershipRegistryDto>>;
