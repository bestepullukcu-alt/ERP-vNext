using Diten.ManagementGovernanceService.Application.Features.Dws.Queries;
using DwsAuthorizationManifest = Diten.ManagementGovernanceService.Application.Modules.Dws.DwsAuthorizationManifest;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Handlers.QueryHandlers;

public sealed class CompareStructureBaselinesHandler(IDwsFunctionalValidator<CompareStructureBaselinesQuery> validator, IFu16DwsFunctionalAuthorization authorization, IMod0117DwsContextValidator contexts, IDwsStructureVisibilityPort visibility, IDwsFunctionalQueryPort queries)
    : IRequestHandler<CompareStructureBaselinesQuery, Response<BaselineComparisonDto>>
{
    public Task<Response<BaselineComparisonDto>> Handle(CompareStructureBaselinesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DwsFunctionalResponse.ExecuteAsync(async () => { validator.Validate(request); var security = await DwsExistingStructureSecurity.CaptureAsync(request.StructureDefinitionId, request.Context, nameof(CompareStructureBaselinesQuery), DwsAuthorizationManifest.RequireExact(nameof(CompareStructureBaselinesQuery)), authorization, contexts, visibility, cancellationToken); var result = await queries.CompareStructureBaselinesAsync(request.StructureDefinitionId, request.LeftBaselineNumber, request.RightBaselineNumber, request.Context, cancellationToken); await DwsExistingStructureSecurity.RevalidateAsync(request.Context, security, authorization, contexts, visibility, cancellationToken); return result; });
    }
}
