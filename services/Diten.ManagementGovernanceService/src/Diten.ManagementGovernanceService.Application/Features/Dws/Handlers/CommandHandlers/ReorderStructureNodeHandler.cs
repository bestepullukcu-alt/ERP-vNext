using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using DwsAuthorizationManifest = Diten.ManagementGovernanceService.Application.Modules.Dws.DwsAuthorizationManifest;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Handlers.CommandHandlers;

public sealed class ReorderStructureNodeHandler(IDwsFunctionalValidator<ReorderStructureNodeCommand> validator, IFu16DwsFunctionalAuthorization authorization, IMod0117DwsContextValidator contexts, IDwsStructureVisibilityPort visibility, IDwsFunctionalCommandPort commands)
    : IRequestHandler<ReorderStructureNodeCommand, Response<ReorderStructureNodeResult>>
{
    public Task<Response<ReorderStructureNodeResult>> Handle(ReorderStructureNodeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DwsFunctionalResponse.ExecuteAsync(async () => { validator.Validate(request); var security = await DwsExistingStructureSecurity.CaptureAsync(request.Request.StructureDefinitionId, request.Context, nameof(ReorderStructureNodeCommand), DwsAuthorizationManifest.RequireExact(nameof(ReorderStructureNodeCommand)), authorization, contexts, visibility, cancellationToken); await DwsExistingStructureSecurity.RevalidateAsync(request.Context, security, authorization, contexts, visibility, cancellationToken); return await commands.ReorderStructureNodeAsync(request.Request, request.Context, cancellationToken); });
    }
}
