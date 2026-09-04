using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Queries;

public sealed record CompareStructureBaselinesQuery(Guid StructureDefinitionId, int LeftBaselineNumber, int RightBaselineNumber, DwsTrustedActorContext Context)
    : IRequest<Response<BaselineComparisonDto>>;
