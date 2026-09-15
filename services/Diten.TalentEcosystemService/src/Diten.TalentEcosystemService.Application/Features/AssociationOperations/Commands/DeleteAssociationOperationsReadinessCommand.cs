using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.AssociationOperations.Commands;

public sealed record DeleteAssociationOperationsReadinessCommand(Guid Id) : IRequest<Response<bool>>;
