using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Validators;

public sealed class RemoveStructuralDependencyValidator : IDwsFunctionalValidator<RemoveStructuralDependencyCommand>
{
    public void Validate(RemoveStructuralDependencyCommand command)
    {
        ArgumentNullException.ThrowIfNull(command); command.Context.RequireCommand();
        DwsFunctionalValidation.Dependency(command.Request.StructureDefinitionId, command.Request.FromLogicalNodeId, command.Request.ToLogicalNodeId, command.Request.ExpectedRevisionVersion);
    }
}
