namespace Diten.HumanCapitalService.Application.Contracts;

public interface IEmployeeProjectionReferenceValidator
{
    Task<ReferenceValidationResult> ValidateHrisSourceProfileAsync(Guid tenantId, Guid hrisSourceProfileId, CancellationToken ct);
    Task<ReferenceValidationResult> ValidatePersonReferenceAsync(Guid tenantId, Guid personReferenceId, CancellationToken ct);
}

public sealed record ReferenceValidationResult(bool IsAvailable, bool IsValid)
{
    public static ReferenceValidationResult Valid() => new(true, true);
    public static ReferenceValidationResult NotFound() => new(true, false);
    public static ReferenceValidationResult ContractUnavailable() => new(false, false);
}
