using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Validators;

public sealed class UpdateStructureMetadataValidator : IDwsFunctionalValidator<UpdateStructureMetadataCommand>
{
    public void Validate(UpdateStructureMetadataCommand command)
    {
        ArgumentNullException.ThrowIfNull(command); command.Context.RequireCommand();
        DwsFunctionalValidation.IdentityVersion(command.Request.StructureDefinitionId, command.Request.ExpectedRevisionVersion);
        _ = new StructuralMetadata(command.Request.Name, command.Request.Description);
    }
}
