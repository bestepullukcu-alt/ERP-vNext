using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.AssociationOperations.Queries;

public sealed record GetAssociationOperationsReadinessListQuery : IRequest<Response<IReadOnlyList<AssociationOperationsReadinessListItemDto>>>;
