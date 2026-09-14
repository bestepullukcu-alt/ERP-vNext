using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.AssociationOperations.Commands;

public sealed record CreateAssociationOperationsReadinessCommand(AssociationOperationsReadinessCreateRequest Request) : IRequest<Response<Guid>>;
