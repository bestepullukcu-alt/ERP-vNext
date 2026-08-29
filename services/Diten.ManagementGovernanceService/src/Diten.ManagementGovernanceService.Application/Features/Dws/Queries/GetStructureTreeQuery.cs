using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Queries;

public sealed record GetStructureTreeQuery(Guid StructureDefinitionId, int? RevisionNumber, DwsTrustedActorContext Context)
    : IRequest<Response<StructureTreeDto>>;
