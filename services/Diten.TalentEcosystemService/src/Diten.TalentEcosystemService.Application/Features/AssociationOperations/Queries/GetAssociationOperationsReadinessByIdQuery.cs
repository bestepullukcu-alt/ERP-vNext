using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.AssociationOperations.Queries;

public sealed record GetAssociationOperationsReadinessByIdQuery(Guid Id) : IRequest<Response<AssociationOperationsReadinessDto>>;
