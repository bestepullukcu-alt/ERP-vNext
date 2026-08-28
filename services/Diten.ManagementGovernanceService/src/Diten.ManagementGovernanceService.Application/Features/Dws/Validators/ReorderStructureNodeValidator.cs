using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Validators;

public sealed class ReorderStructureNodeValidator : IDwsFunctionalValidator<ReorderStructureNodeCommand>
{
    public void Validate(ReorderStructureNodeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command); command.Context.RequireCommand();
        DwsFunctionalValidation.IdentityVersion(command.Request.StructureDefinitionId, command.Request.ExpectedRevisionVersion);
        DwsFunctionalValidation.Identity(command.Request.LogicalNodeId);
        if (command.Request.SiblingOrder < 0) throw new DwsValidationException(DwsErrors.InvalidStructure);
    }
}
