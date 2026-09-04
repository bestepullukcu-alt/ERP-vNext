using Diten.ManagementGovernanceService.Application.Features.Dws.Queries;
using DwsAuthorizationManifest = Diten.ManagementGovernanceService.Application.Modules.Dws.DwsAuthorizationManifest;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Handlers.QueryHandlers;

public sealed class CompareStructureRevisionsHandler(IDwsFunctionalValidator<CompareStructureRevisionsQuery> validator, IFu16DwsFunctionalAuthorization authorization, IMod0117DwsContextValidator contexts, IDwsStructureVisibilityPort visibility, IDwsFunctionalQueryPort queries)
    : IRequestHandler<CompareStructureRevisionsQuery, Response<StructureComparisonDto>>
{
    public Task<Response<StructureComparisonDto>> Handle(CompareStructureRevisionsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DwsFunctionalResponse.ExecuteAsync(async () => { validator.Validate(request); var security = await DwsExistingStructureSecurity.CaptureAsync(request.StructureDefinitionId, request.Context, nameof(CompareStructureRevisionsQuery), DwsAuthorizationManifest.RequireExact(nameof(CompareStructureRevisionsQuery)), authorization, contexts, visibility, cancellationToken); var result = await queries.CompareStructureRevisionsAsync(request.StructureDefinitionId, request.LeftRevisionNumber, request.RightRevisionNumber, request.Context, cancellationToken); await DwsExistingStructureSecurity.RevalidateAsync(request.Context, security, authorization, contexts, visibility, cancellationToken); return result; });
    }
}
