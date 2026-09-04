using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Validators;

public sealed class AddStructureNodeValidator : IDwsFunctionalValidator<AddStructureNodeCommand>
{
    public void Validate(AddStructureNodeCommand command)
    {
        ArgumentNullException.ThrowIfNull(command); command.Context.RequireCommand();
        DwsFunctionalValidation.IdentityVersion(command.Request.StructureDefinitionId, command.Request.ExpectedRevisionVersion);
        if (command.Request.ParentLogicalNodeId == Guid.Empty || command.Request.SiblingOrder < 0) throw new DwsValidationException(DwsErrors.InvalidStructure);
        _ = DwsText.Required(command.Request.Code, 100); _ = DwsText.Required(command.Request.Title, 300); _ = DwsText.Optional(command.Request.Description, 4000);
    }
}
