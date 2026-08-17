using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities;

public sealed class SubscriptionPlan : GlobalEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }

    public decimal? PriceMonthly { get; set; }
    public decimal? PriceYearly { get; set; }
    public string? Currency { get; set; }

    public bool IsTrialPlan { get; set; }
    public int? TrialDurationDays { get; set; }

    // TBD schema: keep structured (document-like) without forcing a separate quota engine in MVP.
    public Dictionary<string, decimal>? DefaultQuotas { get; set; }

    public IReadOnlyList<string> IncludedFeatures { get; set; } = [];
    public IReadOnlyList<string> IncludedModuleKeys { get; set; } = [];
}

