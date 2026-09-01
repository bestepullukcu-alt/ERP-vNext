using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using DwsAuthorizationManifest = Diten.ManagementGovernanceService.Application.Modules.Dws.DwsAuthorizationManifest;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Handlers.CommandHandlers;

public sealed class AddStructuralDependencyHandler(IDwsFunctionalValidator<AddStructuralDependencyCommand> validator, IFu16DwsFunctionalAuthorization authorization, IMod0117DwsContextValidator contexts, IDwsStructureVisibilityPort visibility, IDwsFunctionalCommandPort commands)
    : IRequestHandler<AddStructuralDependencyCommand, Response<AddStructuralDependencyResult>>
{
    public Task<Response<AddStructuralDependencyResult>> Handle(AddStructuralDependencyCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DwsFunctionalResponse.ExecuteAsync(async () => { validator.Validate(request); var security = await DwsExistingStructureSecurity.CaptureAsync(request.Request.StructureDefinitionId, request.Context, nameof(AddStructuralDependencyCommand), DwsAuthorizationManifest.RequireExact(nameof(AddStructuralDependencyCommand)), authorization, contexts, visibility, cancellationToken); await DwsExistingStructureSecurity.RevalidateAsync(request.Context, security, authorization, contexts, visibility, cancellationToken); return await commands.AddStructuralDependencyAsync(request.Request, request.Context, cancellationToken); });
    }
}
