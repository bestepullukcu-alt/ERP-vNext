using Diten.ManagementGovernanceService.Application.Features.Dws.Queries;

namespace Diten.ManagementGovernanceService.Application.Features.Dws.Validators;

public sealed class CompareStructureRevisionsValidator : IDwsFunctionalValidator<CompareStructureRevisionsQuery>
{
    public void Validate(CompareStructureRevisionsQuery query)
    {
        ArgumentNullException.ThrowIfNull(query); query.Context.RequireQuery(); DwsFunctionalValidation.Identity(query.StructureDefinitionId); DwsFunctionalValidation.PositiveDistinctPair(query.LeftRevisionNumber, query.RightRevisionNumber);
    }
}
