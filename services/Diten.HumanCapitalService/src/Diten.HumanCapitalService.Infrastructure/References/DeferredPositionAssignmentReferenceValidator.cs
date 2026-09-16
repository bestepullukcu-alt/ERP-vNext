using Diten.HumanCapitalService.Application.Contracts;

namespace Diten.HumanCapitalService.Infrastructure.References;

public sealed class DeferredPositionAssignmentReferenceValidator : IPositionAssignmentReferenceValidator
{
    public Task<ReferenceValidationResult> ValidatePersonReferenceAsync(Guid tenantId, Guid personReferenceId, CancellationToken ct)
    {
        _ = tenantId;
        _ = personReferenceId;
        _ = ct;
        return Task.FromResult(ReferenceValidationResult.ContractUnavailable());
    }

    public Task<ReferenceValidationResult> ValidateOrganizationUnitAsync(Guid tenantId, Guid organizationUnitId, CancellationToken ct)
    {
        _ = tenantId;
        _ = organizationUnitId;
        _ = ct;
        return Task.FromResult(ReferenceValidationResult.ContractUnavailable());
    }

    public Task<ReferenceValidationResult> ValidatePositionAsync(Guid tenantId, Guid positionId, CancellationToken ct)
    {
        _ = tenantId;
        _ = positionId;
        _ = ct;
        return Task.FromResult(ReferenceValidationResult.ContractUnavailable());
    }
}
