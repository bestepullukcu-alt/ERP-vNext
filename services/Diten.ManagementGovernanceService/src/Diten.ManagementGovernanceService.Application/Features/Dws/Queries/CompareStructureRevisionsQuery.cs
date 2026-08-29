using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Queries;

public sealed record CompareStructureRevisionsQuery(Guid StructureDefinitionId, int LeftRevisionNumber, int RightRevisionNumber, DwsTrustedActorContext Context)
    : IRequest<Response<StructureComparisonDto>>;
