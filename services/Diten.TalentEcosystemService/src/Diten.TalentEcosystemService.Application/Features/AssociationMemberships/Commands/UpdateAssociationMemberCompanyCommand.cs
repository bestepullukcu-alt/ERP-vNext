using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.AssociationMemberships.Commands;

public sealed record UpdateAssociationMemberCompanyCommand(Guid Id, AssociationMemberCompanyRequest Request)
    : IRequest<Response<AssociationMembershipRegistryDto>>;
