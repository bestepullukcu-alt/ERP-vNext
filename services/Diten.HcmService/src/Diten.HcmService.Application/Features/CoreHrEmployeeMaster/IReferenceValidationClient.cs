namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster;

public interface IReferenceValidationClient
{
    Task<ReferenceValidationItem> ValidatePersonAsync(string? personId, CancellationToken cancellationToken);
    Task<ReferenceValidationItem> ValidateOrganizationUnitAsync(string? organizationUnitId, CancellationToken cancellationToken);
    Task<ReferenceValidationItem> ValidatePositionAsync(string? positionId, CancellationToken cancellationToken);
    Task<ReferenceValidationItem> ValidateLegalEntityAsync(string? legalEntityId, CancellationToken cancellationToken);
}
