using Diten.TalentEcosystemService.Application.Common;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.AssociationOperations.Queries;

public sealed record GetAssociationOperationsAuditMetadataQuery(Guid Id) : IRequest<Response<AssociationOperationsAuditMetadataDto>>;
