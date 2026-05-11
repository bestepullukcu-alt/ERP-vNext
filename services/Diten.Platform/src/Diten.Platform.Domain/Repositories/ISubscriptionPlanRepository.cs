using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

public interface ISubscriptionPlanRepository
{
    Task<SubscriptionPlan> CreateAsync(SubscriptionPlan plan, CancellationToken ct = default);
    Task<SubscriptionPlan?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SubscriptionPlan?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task<SubscriptionPlan?> GetActiveDefaultAsync(Guid? excludeId = null, CancellationToken ct = default);
    Task UpdateAsync(SubscriptionPlan plan, CancellationToken ct = default);
    Task<(IReadOnlyList<SubscriptionPlan> Items, long TotalCount)> QueryAsync(SubscriptionPlansQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<SubscriptionPlan>> GetActiveAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SubscriptionPlan>> GetByIncludedModuleKeyAsync(string moduleKey, CancellationToken ct = default);
    Task<SubscriptionPlanSummary> GetSummaryAsync(CancellationToken ct = default);
}

public sealed record SubscriptionPlansQuery(
    string? Search,
    bool? IsActive,
    bool? IsTrialPlan,
    int Page,
    int PageSize,
    string Sort);

public sealed record SubscriptionPlanSummary(
    long Total,
    long Active,
    long TrialPlans,
    long PaidPlans);

