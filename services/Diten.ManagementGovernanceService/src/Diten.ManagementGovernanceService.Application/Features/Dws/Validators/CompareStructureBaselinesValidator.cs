using Diten.ManagementGovernanceService.Application.Features.Dws.Queries;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Validators;

public sealed class CompareStructureBaselinesValidator : IDwsFunctionalValidator<CompareStructureBaselinesQuery>
{
    public void Validate(CompareStructureBaselinesQuery query)
    {
        ArgumentNullException.ThrowIfNull(query); query.Context.RequireQuery(); DwsFunctionalValidation.Identity(query.StructureDefinitionId); DwsFunctionalValidation.PositiveDistinctPair(query.LeftBaselineNumber, query.RightBaselineNumber);
    }
}
