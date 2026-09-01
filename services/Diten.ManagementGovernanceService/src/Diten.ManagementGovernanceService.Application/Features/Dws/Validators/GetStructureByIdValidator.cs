using Diten.ManagementGovernanceService.Application.Features.Dws.Queries;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Validators;

public sealed class GetStructureByIdValidator : IDwsFunctionalValidator<GetStructureByIdQuery>
{
    public void Validate(GetStructureByIdQuery query)
    {
        ArgumentNullException.ThrowIfNull(query); query.Context.RequireQuery(); DwsFunctionalValidation.Identity(query.StructureDefinitionId);
    }
}
