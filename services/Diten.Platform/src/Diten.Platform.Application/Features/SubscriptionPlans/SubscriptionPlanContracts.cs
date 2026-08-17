namespace Diten.Platform.Application.Features.SubscriptionPlans;

public sealed record SubscriptionPlanDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    bool IsDefault,
    int SortOrder,
    decimal? PriceMonthly,
    decimal? PriceYearly,
    string? Currency,
    bool IsTrialPlan,
    int? TrialDurationDays,
    IReadOnlyDictionary<string, decimal>? DefaultQuotas,
    IReadOnlyList<string> IncludedFeatures,
    IReadOnlyList<string> IncludedModuleKeys,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record SubscriptionPlanListItemDto(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    bool IsDefault,
    int SortOrder,
    decimal? PriceMonthly,
    decimal? PriceYearly,
    string? Currency,
    bool IsTrialPlan,
    int? TrialDurationDays,
    IReadOnlyDictionary<string, decimal>? DefaultQuotas,
    IReadOnlyList<string> IncludedFeatures,
    IReadOnlyList<string> IncludedModuleKeys,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record SubscriptionPlanSummaryDto(
    long TotalPlans,
    long ActivePlans,
    long TrialPlans,
    long PaidPlans);

public sealed record CreateSubscriptionPlanRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    bool IsDefault,
    int? SortOrder,
    decimal? PriceMonthly,
    decimal? PriceYearly,
    string? Currency,
    bool IsTrialPlan,
    int? TrialDurationDays,
    IReadOnlyDictionary<string, decimal>? DefaultQuotas,
    IReadOnlyList<string>? IncludedFeatures,
    IReadOnlyList<string>? IncludedModuleKeys);

public sealed record UpdateSubscriptionPlanRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    bool IsDefault,
    int? SortOrder,
    decimal? PriceMonthly,
    decimal? PriceYearly,
    string? Currency,
    bool IsTrialPlan,
    int? TrialDurationDays,
    IReadOnlyDictionary<string, decimal>? DefaultQuotas,
    IReadOnlyList<string>? IncludedFeatures,
    IReadOnlyList<string>? IncludedModuleKeys);

public sealed record SubscriptionPlanFilterRequest(
    string? Search,
    bool? IsActive,
    bool? IsTrialPlan,
    int Page = 1,
    int PageSize = 20,
    string Sort = "sortOrder");
