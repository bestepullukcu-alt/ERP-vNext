namespace Diten.SupplyChainService.Domain.Features.CapacityPlans;
public sealed record CapacityMutation<T>(T? Value, int StatusCode, string? ErrorCode = null, bool Replay = false);
public interface ICapacityRepository
{
    Task<CapacityMutation<CapacityPlan>> CreatePlanAsync(CapacityScope scope, string key, string fingerprint, Guid correlation, CapacityPlan plan, CancellationToken ct);
    Task<CapacityMutation<CapacityScenario>> CreateScenarioAsync(CapacityScope scope, Guid planId, string key, string fingerprint, Guid correlation, CapacityScenario scenario, CancellationToken ct);
    Task<CapacityMutation<CapacityEvaluation>> EvaluateAsync(CapacityScope scope, Guid planId, Guid scenarioId, string key, string fingerprint, Guid correlation, CapacityEvaluation evaluation, CancellationToken ct);
    Task<CapacityPlan?> GetPlanAsync(CapacityScope scope, Guid id, CancellationToken ct);
    Task<CapacityScenario?> GetScenarioAsync(CapacityScope scope, Guid planId, Guid id, CancellationToken ct);
    Task<CapacityEvaluation?> GetEvaluationAsync(CapacityScope scope, Guid planId, Guid id, CancellationToken ct);
}

public sealed class CapacityReadUnavailableException : Exception { }
