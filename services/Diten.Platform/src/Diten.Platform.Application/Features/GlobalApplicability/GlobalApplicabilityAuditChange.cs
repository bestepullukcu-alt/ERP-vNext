using System.Globalization;
using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Application.Features.GlobalApplicability;

/// <summary>
/// WP-PLATFORM-AUDIT-INTX-01 FIX1 (AUD-001 standard field 10) — what a plan or catalogue update actually changed. A
/// record that only said "Update, GlobalApplicabilityVersion = n" could not tell a typo fix from a monthly price going
/// from 10 to 1000.
///
/// <para><b>What is recorded.</b> The NAMES of every changed field (<see cref="ChangedFields"/>), and the value before
/// and after for the scalar ones — prices, currency, flags, status, codes. Free-text fields (a name, a display name, a
/// description) are recorded by NAME only: their words are not the audit trail's to copy. Code lists (a plan's
/// modules, features and default quotas) are codes, not prose, and are recorded as one sorted, comma-joined value.</para>
/// </summary>
public sealed record GlobalApplicabilityAuditChange(
    IReadOnlyList<string> ChangedFields,
    IReadOnlyDictionary<string, object?> Before,
    IReadOnlyDictionary<string, object?> After)
{
    /// <summary>Fields whose words are free text: their change is named, their content never copied.</summary>
    public static IReadOnlySet<string> TextFields { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "Name", "DisplayName", "Description" };

    /// <summary>The change between two snapshots taken with <see cref="StateOf(SubscriptionPlan)"/> or <see cref="StateOf(ModuleCatalogItem)"/>.</summary>
    public static GlobalApplicabilityAuditChange Between(
        IReadOnlyDictionary<string, object?> before,
        IReadOnlyDictionary<string, object?> after)
    {
        var changed = before.Keys.Union(after.Keys)
            .Where(key => !Equals(before.GetValueOrDefault(key), after.GetValueOrDefault(key)))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();
        var scalars = changed.Where(key => !TextFields.Contains(key)).ToList();
        return new GlobalApplicabilityAuditChange(
            changed,
            scalars.ToDictionary(key => key, key => before.GetValueOrDefault(key)),
            scalars.ToDictionary(key => key, key => after.GetValueOrDefault(key)));
    }

    public static IReadOnlyDictionary<string, object?> StateOf(SubscriptionPlan plan) => new Dictionary<string, object?>
    {
        ["Code"] = plan.Code,
        ["Name"] = plan.Name,
        ["Description"] = plan.Description,
        ["IsActive"] = plan.IsActive,
        ["IsDefault"] = plan.IsDefault,
        ["SortOrder"] = plan.SortOrder,
        ["PriceMonthly"] = plan.PriceMonthly,
        ["PriceYearly"] = plan.PriceYearly,
        ["Currency"] = plan.Currency,
        ["IsTrialPlan"] = plan.IsTrialPlan,
        ["TrialDurationDays"] = plan.TrialDurationDays,
        ["DefaultQuotas"] = Joined(plan.DefaultQuotas?.Select(pair => $"{pair.Key}={pair.Value.ToString(CultureInfo.InvariantCulture)}")),
        ["IncludedFeatures"] = Joined(plan.IncludedFeatures),
        ["IncludedModuleKeys"] = Joined(plan.IncludedModuleKeys)
    };

    public static IReadOnlyDictionary<string, object?> StateOf(ModuleCatalogItem item) => new Dictionary<string, object?>
    {
        ["ModuleCode"] = item.ModuleCode,
        ["ModuleName"] = item.ModuleName,
        ["DisplayName"] = item.DisplayName,
        ["Description"] = item.Description,
        ["Domain"] = item.Domain,
        ["Service"] = item.Service,
        ["Status"] = item.Status.ToString(),
        ["ModuleVersion"] = item.ModuleVersion,
        ["IsCoreModule"] = item.IsCoreModule,
        ["IsTenantAssignable"] = item.IsTenantAssignable,
        ["IsBaseline"] = item.IsBaseline,
        ["SortOrder"] = item.SortOrder,
        ["Icon"] = item.Icon,
        ["Origin"] = item.Origin.ToString()
    };

    private static string Joined(IEnumerable<string>? values) =>
        values is null ? string.Empty : string.Join(",", values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
}
