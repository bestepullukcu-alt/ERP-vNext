using Diten.ManagementGovernanceService.Application.Features.Dws.Queries;
using DwsAuthorizationManifest = Diten.ManagementGovernanceService.Application.Modules.Dws.DwsAuthorizationManifest;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Handlers.QueryHandlers;

public sealed class ValidateStructureHandler(IDwsFunctionalValidator<ValidateStructureQuery> validator, IFu16DwsFunctionalAuthorization authorization, IMod0117DwsContextValidator contexts, IDwsStructureVisibilityPort visibility, IDwsFunctionalQueryPort queries)
    : IRequestHandler<ValidateStructureQuery, Response<StructureValidationDto>>
{
    public Task<Response<StructureValidationDto>> Handle(ValidateStructureQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DwsFunctionalResponse.ExecuteAsync(async () => { validator.Validate(request); var security = await DwsExistingStructureSecurity.CaptureAsync(request.StructureDefinitionId, request.Context, nameof(ValidateStructureQuery), DwsAuthorizationManifest.RequireExact(nameof(ValidateStructureQuery)), authorization, contexts, visibility, cancellationToken); var result = await queries.ValidateStructureAsync(request.StructureDefinitionId, request.RevisionNumber, request.Context, cancellationToken); await DwsExistingStructureSecurity.RevalidateAsync(request.Context, security, authorization, contexts, visibility, cancellationToken); return result; });
    }
}
