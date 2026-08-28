using Diten.ManagementGovernanceService.Application.Features.Dws.Queries;
using DwsAuthorizationManifest = Diten.ManagementGovernanceService.Application.Modules.Dws.DwsAuthorizationManifest;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Handlers.QueryHandlers;

public sealed class GetStructureByIdHandler(IDwsFunctionalValidator<GetStructureByIdQuery> validator, IFu16DwsFunctionalAuthorization authorization, IMod0117DwsContextValidator contexts, IDwsStructureVisibilityPort visibility, IDwsFunctionalQueryPort queries)
    : IRequestHandler<GetStructureByIdQuery, Response<StructureSummaryDto>>
{
    public Task<Response<StructureSummaryDto>> Handle(GetStructureByIdQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DwsFunctionalResponse.ExecuteAsync(async () => { validator.Validate(request); var security = await DwsExistingStructureSecurity.CaptureAsync(request.StructureDefinitionId, request.Context, nameof(GetStructureByIdQuery), DwsAuthorizationManifest.RequireExact(nameof(GetStructureByIdQuery)), authorization, contexts, visibility, cancellationToken); var result = await queries.GetStructureByIdAsync(request.StructureDefinitionId, request.Context, cancellationToken); await DwsExistingStructureSecurity.RevalidateAsync(request.Context, security, authorization, contexts, visibility, cancellationToken); return result; });
    }
}
