using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Validators;

public sealed class CreateStructureValidator : IDwsFunctionalValidator<CreateStructureCommand>
{
    public void Validate(CreateStructureCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Context.RequireCommand();
        _ = command.Request.ExternalContextReference ?? throw new DwsValidationException(DwsErrors.InvalidContextReference);
        _ = new StructuralMetadata(command.Request.Name, command.Request.Description);
    }
}
