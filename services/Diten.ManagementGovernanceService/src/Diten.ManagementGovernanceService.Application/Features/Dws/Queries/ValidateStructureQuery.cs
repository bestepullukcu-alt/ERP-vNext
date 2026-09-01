using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Queries;

public sealed record ValidateStructureQuery(Guid StructureDefinitionId, int? RevisionNumber, DwsTrustedActorContext Context)
    : IRequest<Response<StructureValidationDto>>;
