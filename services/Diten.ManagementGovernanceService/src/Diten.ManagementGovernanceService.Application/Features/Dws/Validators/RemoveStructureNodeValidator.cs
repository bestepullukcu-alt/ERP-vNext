using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Validators;

public sealed class RemoveStructureNodeValidator : IDwsFunctionalValidator<RemoveStructureNodeCommand>
{
    public void Validate(RemoveStructureNodeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command); command.Context.RequireCommand();
        DwsFunctionalValidation.IdentityVersion(command.Request.StructureDefinitionId, command.Request.ExpectedRevisionVersion);
        DwsFunctionalValidation.Identity(command.Request.LogicalNodeId);
    }
}
