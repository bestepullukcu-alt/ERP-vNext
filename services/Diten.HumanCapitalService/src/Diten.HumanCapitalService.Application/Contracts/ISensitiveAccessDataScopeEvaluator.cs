using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Application.Contracts;

public interface ISensitiveAccessDataScopeEvaluator
{
    Task<SensitiveAccessDataScopeResult> EvaluateAsync(
        Guid tenantId,
        EmployeeProfileProjection projection,
        CancellationToken ct);
}

public sealed record SensitiveAccessDataScopeResult(
    bool IsAvailable,
    bool IsInScope,
    string ReasonCode)
{
    public static SensitiveAccessDataScopeResult Allowed() => new(true, true, "DataScopeMatched");
    public static SensitiveAccessDataScopeResult OutOfScope() => new(true, false, "DataScopeDenied");
    public static SensitiveAccessDataScopeResult ContractUnavailable() => new(false, false, "DataScopeContractUnavailable");
}
