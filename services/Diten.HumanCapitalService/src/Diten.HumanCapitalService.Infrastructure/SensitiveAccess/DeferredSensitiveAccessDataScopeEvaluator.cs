using Diten.HumanCapitalService.Application.Contracts;
using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Infrastructure.SensitiveAccess;

public sealed class DeferredSensitiveAccessDataScopeEvaluator : ISensitiveAccessDataScopeEvaluator
{
    public Task<SensitiveAccessDataScopeResult> EvaluateAsync(
        Guid tenantId,
        EmployeeProfileProjection projection,
        CancellationToken ct)
    {
        _ = tenantId;
        _ = projection;
        _ = ct;
        return Task.FromResult(SensitiveAccessDataScopeResult.ContractUnavailable());
    }
}
