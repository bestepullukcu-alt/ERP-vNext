using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Validators;

public sealed class MoveStructureNodeValidator : IDwsFunctionalValidator<MoveStructureNodeCommand>
{
    public void Validate(MoveStructureNodeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command); command.Context.RequireCommand();
        DwsFunctionalValidation.IdentityVersion(command.Request.StructureDefinitionId, command.Request.ExpectedRevisionVersion);
        DwsFunctionalValidation.Identity(command.Request.LogicalNodeId);
        if (command.Request.NewParentLogicalNodeId == Guid.Empty || command.Request.NewParentLogicalNodeId == command.Request.LogicalNodeId || command.Request.NewSiblingOrder < 0) throw new DwsValidationException(DwsErrors.InvalidStructure);
    }
}
