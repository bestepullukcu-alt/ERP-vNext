using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Validators;

public sealed class CreateStructureBaselineValidator : IDwsFunctionalValidator<CreateStructureBaselineCommand>
{
    public void Validate(CreateStructureBaselineCommand command)
    {
        ArgumentNullException.ThrowIfNull(command); command.Context.RequireCommand();
        DwsFunctionalValidation.IdentityVersion(command.Request.StructureDefinitionId, command.Request.ExpectedRevisionVersion);
    }
}
