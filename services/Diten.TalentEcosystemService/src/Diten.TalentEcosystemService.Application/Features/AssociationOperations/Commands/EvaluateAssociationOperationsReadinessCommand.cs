using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.AssociationOperations.Commands;

public sealed record EvaluateAssociationOperationsReadinessCommand(Guid Id) : IRequest<Response<AssociationOperationsReadinessDto>>;
