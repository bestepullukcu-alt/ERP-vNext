using Diten.ManagementGovernanceService.Application.Features.Dws.Queries;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Validators;

public sealed class ValidateStructureValidator : IDwsFunctionalValidator<ValidateStructureQuery>
{
    public void Validate(ValidateStructureQuery query)
    {
        ArgumentNullException.ThrowIfNull(query); query.Context.RequireQuery(); DwsFunctionalValidation.Identity(query.StructureDefinitionId); DwsFunctionalValidation.OptionalPositive(query.RevisionNumber);
    }
}
