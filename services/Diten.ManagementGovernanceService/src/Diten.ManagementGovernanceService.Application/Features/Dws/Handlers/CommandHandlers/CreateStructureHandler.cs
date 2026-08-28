using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using DwsAuthorizationManifest = Diten.ManagementGovernanceService.Application.Modules.Dws.DwsAuthorizationManifest;
using MediatR;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Handlers.CommandHandlers;

public sealed class CreateStructureHandler(
    IDwsFunctionalValidator<CreateStructureCommand> validator,
    IFu16DwsFunctionalAuthorization authorization,
    IMod0117DwsContextValidator contexts,
    IDwsFunctionalCommandPort commands)
    : IRequestHandler<CreateStructureCommand, Response<CreateStructureResult>>
{
    public Task<Response<CreateStructureResult>> Handle(CreateStructureCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DwsFunctionalResponse.ExecuteAsync(async () =>
        {
            validator.Validate(request);
            var authorizationSnapshot = await authorization.AuthorizeAsync(request.Context, DwsFunctionalAuthorizationBinding.ModuleCode, DwsFunctionalAuthorizationBinding.ModuleEntitlementCode, nameof(CreateStructureCommand), DwsAuthorizationManifest.RequireExact(nameof(CreateStructureCommand)), cancellationToken);
            var contextSnapshot = await contexts.ValidateAsync(request.Context, request.Request.ExternalContextReference, cancellationToken);
            await contexts.RevalidateAsync(request.Context, request.Request.ExternalContextReference, contextSnapshot, cancellationToken);
            await authorization.RevalidateAsync(request.Context, authorizationSnapshot, cancellationToken);
            return await commands.CreateStructureAsync(request.Request, request.Context, cancellationToken);
        }, 201);
    }
}
