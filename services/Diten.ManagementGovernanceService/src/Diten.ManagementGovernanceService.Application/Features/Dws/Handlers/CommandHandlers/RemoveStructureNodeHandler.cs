using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using DwsAuthorizationManifest = Diten.ManagementGovernanceService.Application.Modules.Dws.DwsAuthorizationManifest;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Handlers.CommandHandlers;

public sealed class RemoveStructureNodeHandler(IDwsFunctionalValidator<RemoveStructureNodeCommand> validator, IFu16DwsFunctionalAuthorization authorization, IMod0117DwsContextValidator contexts, IDwsStructureVisibilityPort visibility, IDwsFunctionalCommandPort commands)
    : IRequestHandler<RemoveStructureNodeCommand, Response<RemoveStructureNodeResult>>
{
    public Task<Response<RemoveStructureNodeResult>> Handle(RemoveStructureNodeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DwsFunctionalResponse.ExecuteAsync(async () => { validator.Validate(request); var security = await DwsExistingStructureSecurity.CaptureAsync(request.Request.StructureDefinitionId, request.Context, nameof(RemoveStructureNodeCommand), DwsAuthorizationManifest.RequireExact(nameof(RemoveStructureNodeCommand)), authorization, contexts, visibility, cancellationToken); await DwsExistingStructureSecurity.RevalidateAsync(request.Context, security, authorization, contexts, visibility, cancellationToken); return await commands.RemoveStructureNodeAsync(request.Request, request.Context, cancellationToken); });
    }
}
