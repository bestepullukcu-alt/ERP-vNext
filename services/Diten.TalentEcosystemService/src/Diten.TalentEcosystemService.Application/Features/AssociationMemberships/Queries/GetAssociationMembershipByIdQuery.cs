using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.AssociationMemberships.Queries;

public sealed record GetAssociationMembershipByIdQuery(Guid Id) : IRequest<Response<AssociationMembershipRegistryDto>>;
