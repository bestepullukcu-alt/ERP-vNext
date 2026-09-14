namespace Diten.HumanCapitalService.Application.Contracts;

public interface IPositionAssignmentReferenceValidator
{
    Task<ReferenceValidationResult> ValidatePersonReferenceAsync(Guid tenantId, Guid personReferenceId, CancellationToken ct);
    Task<ReferenceValidationResult> ValidateOrganizationUnitAsync(Guid tenantId, Guid organizationUnitId, CancellationToken ct);
    Task<ReferenceValidationResult> ValidatePositionAsync(Guid tenantId, Guid positionId, CancellationToken ct);
}
