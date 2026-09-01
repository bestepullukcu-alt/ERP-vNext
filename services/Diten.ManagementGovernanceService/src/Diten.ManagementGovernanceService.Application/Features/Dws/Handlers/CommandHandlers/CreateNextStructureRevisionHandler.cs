using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using DwsAuthorizationManifest = Diten.ManagementGovernanceService.Application.Modules.Dws.DwsAuthorizationManifest;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Handlers.CommandHandlers;

public sealed class CreateNextStructureRevisionHandler(IDwsFunctionalValidator<CreateNextStructureRevisionCommand> validator, IFu16DwsFunctionalAuthorization authorization, IMod0117DwsContextValidator contexts, IDwsStructureVisibilityPort visibility, IDwsFunctionalCommandPort commands)
    : IRequestHandler<CreateNextStructureRevisionCommand, Response<CreateNextStructureRevisionResult>>
{
    public Task<Response<CreateNextStructureRevisionResult>> Handle(CreateNextStructureRevisionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DwsFunctionalResponse.ExecuteAsync(async () => { validator.Validate(request); var security = await DwsExistingStructureSecurity.CaptureAsync(request.Request.StructureDefinitionId, request.Context, nameof(CreateNextStructureRevisionCommand), DwsAuthorizationManifest.RequireExact(nameof(CreateNextStructureRevisionCommand)), authorization, contexts, visibility, cancellationToken); await DwsExistingStructureSecurity.RevalidateAsync(request.Context, security, authorization, contexts, visibility, cancellationToken); return await commands.CreateNextStructureRevisionAsync(request.Request, request.Context, cancellationToken); });
    }
}
