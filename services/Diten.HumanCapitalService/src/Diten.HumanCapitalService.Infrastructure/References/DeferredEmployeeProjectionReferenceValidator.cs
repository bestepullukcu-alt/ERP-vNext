using Diten.HumanCapitalService.Application.Contracts;

namespace Diten.HumanCapitalService.Infrastructure.References;

public sealed class DeferredEmployeeProjectionReferenceValidator : IEmployeeProjectionReferenceValidator
{
    public Task<ReferenceValidationResult> ValidateHrisSourceProfileAsync(Guid tenantId, Guid hrisSourceProfileId, CancellationToken ct)
    {
        _ = tenantId;
        _ = hrisSourceProfileId;
        _ = ct;
        return Task.FromResult(ReferenceValidationResult.ContractUnavailable());
    }

    public Task<ReferenceValidationResult> ValidatePersonReferenceAsync(Guid tenantId, Guid personReferenceId, CancellationToken ct)
    {
        _ = tenantId;
        _ = personReferenceId;
        _ = ct;
        return Task.FromResult(ReferenceValidationResult.ContractUnavailable());
    }
}
