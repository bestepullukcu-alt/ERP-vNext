using Diten.ManagementGovernanceService.Application.Features.Dws.Commands;
using Diten.ManagementGovernanceService.Domain.Modules.Dws;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Validators;

public sealed class CreateNextStructureRevisionValidator : IDwsFunctionalValidator<CreateNextStructureRevisionCommand>
{
    public void Validate(CreateNextStructureRevisionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command); command.Context.RequireCommand();
        DwsFunctionalValidation.IdentityVersion(command.Request.StructureDefinitionId, command.Request.ExpectedDefinitionVersion);
        if ((command.Request.SourceRevisionNumber is null) == (command.Request.SourceBaselineNumber is null) || command.Request.SourceRevisionNumber is <= 0 || command.Request.SourceBaselineNumber is <= 0)
            throw new DwsValidationException(DwsErrors.InvalidRequest);
    }
}
