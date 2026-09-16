using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.AssociationMemberships.Commands;

public sealed record ArchiveAssociationMembershipCommand(Guid Id) : IRequest<Response<NoContent>>;
