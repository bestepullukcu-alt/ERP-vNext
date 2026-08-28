using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using DwsAuthorizationManifest = Diten.ManagementGovernanceService.Application.Modules.Dws.DwsAuthorizationManifest;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Handlers.CommandHandlers;

public sealed class RemoveStructuralDependencyHandler(IDwsFunctionalValidator<RemoveStructuralDependencyCommand> validator, IFu16DwsFunctionalAuthorization authorization, IMod0117DwsContextValidator contexts, IDwsStructureVisibilityPort visibility, IDwsFunctionalCommandPort commands)
    : IRequestHandler<RemoveStructuralDependencyCommand, Response<RemoveStructuralDependencyResult>>
{
    public Task<Response<RemoveStructuralDependencyResult>> Handle(RemoveStructuralDependencyCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DwsFunctionalResponse.ExecuteAsync(async () => { validator.Validate(request); var security = await DwsExistingStructureSecurity.CaptureAsync(request.Request.StructureDefinitionId, request.Context, nameof(RemoveStructuralDependencyCommand), DwsAuthorizationManifest.RequireExact(nameof(RemoveStructuralDependencyCommand)), authorization, contexts, visibility, cancellationToken); await DwsExistingStructureSecurity.RevalidateAsync(request.Context, security, authorization, contexts, visibility, cancellationToken); return await commands.RemoveStructuralDependencyAsync(request.Request, request.Context, cancellationToken); });
    }
}
