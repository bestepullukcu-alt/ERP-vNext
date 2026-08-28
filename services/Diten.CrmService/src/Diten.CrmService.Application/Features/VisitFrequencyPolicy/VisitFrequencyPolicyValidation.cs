using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy;

/// <summary>
/// MOD-0165 FU03 structural validation. Every rule returns an error string (400 message) or null. The frequency
/// vocabulary is validated in-domain against the <c>Frequency*</c> constants — it is structural, not tenant
/// vocabulary, so it never fails open on an unpublished MOD-0048 set.
/// </summary>
public static class VisitFrequencyPolicyValidation
{
    /// <summary>Allowed PeriodType values per FrequencyType. cycle-based ⇒ cycle; weekly ⇒ week; monthly ⇒ month;
    /// biweekly ⇒ week OR custom (decision: a fortnight is expressed as a 2-week window or an explicit custom period);
    /// custom ⇒ any period.</summary>
    private static readonly IReadOnlyDictionary<string, string[]> AllowedPeriods = new Dictionary<string, string[]>
    {
        [FrequencyType.Weekly] = new[] { FrequencyPeriodType.Week },
        [FrequencyType.Biweekly] = new[] { FrequencyPeriodType.Week, FrequencyPeriodType.Custom },
        [FrequencyType.Monthly] = new[] { FrequencyPeriodType.Month },
        [FrequencyType.CycleBased] = new[] { FrequencyPeriodType.Cycle },
        [FrequencyType.Custom] = FrequencyPeriodType.All.ToArray(),
    };

    public static string? ValidateTargetType(string? targetType)
        => FrequencyTargetType.IsValid(targetType)
            ? null
            : $"TargetType must be one of: {string.Join(", ", FrequencyTargetType.All)}.";

    public static string? ValidateTargetId(Guid targetId)
        => targetId == Guid.Empty ? "TargetId is required and cannot be empty." : null;

    public static string? ValidateFrequencyType(string? frequencyType)
        => FrequencyType.IsValid(frequencyType)
            ? null
            : $"FrequencyType must be one of: {string.Join(", ", FrequencyType.All)}.";

    public static string? ValidatePeriodType(string? periodType)
        => FrequencyPeriodType.IsValid(periodType)
            ? null
            : $"PeriodType must be one of: {string.Join(", ", FrequencyPeriodType.All)}.";

    public static string? ValidateSource(string? source)
        => FrequencySource.IsValid(source)
            ? null
            : $"Source must be one of: {string.Join(", ", FrequencySource.All)}.";

    /// <summary>Status is optional on write (defaults to draft). When supplied it must be a lifecycle value.</summary>
    public static string? ValidateStatusValue(string? status)
        => string.IsNullOrWhiteSpace(status) || FrequencyPolicyStatus.IsValid(status)
            ? null
            : $"Status must be one of: {string.Join(", ", FrequencyPolicyStatus.All)}. A policy is never hard-deleted.";

    public static string? ValidateRequiredVisitCount(int requiredVisitCount)
        => requiredVisitCount <= 0 ? "RequiredVisitCount must be greater than zero." : null;

    public static string? ValidatePriority(int priority)
        => priority < 1 ? "Priority is required and must be a positive number (smaller wins)." : null;

    public static string? ValidateEffectiveRange(DateTimeOffset effectiveFrom, DateTimeOffset? effectiveTo)
        => effectiveTo is { } to && to < effectiveFrom
            ? "EffectiveTo cannot be earlier than EffectiveFrom."
            : null;

    /// <summary>The FrequencyType × PeriodType combination must be one of the allowed pairs.</summary>
    public static string? ValidateFrequencyPeriodCombination(string frequencyType, string periodType)
    {
        var freq = FrequencyType.Normalize(frequencyType);
        var period = FrequencyPeriodType.Normalize(periodType);
        if (!AllowedPeriods.TryGetValue(freq, out var allowed) || !allowed.Contains(period))
        {
            return $"PeriodType '{period}' is not valid for FrequencyType '{freq}'. " +
                   $"Allowed: {string.Join(", ", allowed ?? Array.Empty<string>())}.";
        }

        return null;
    }

    /// <summary>cycle-based frequency (and any policy whose period is a cycle) needs a CycleId or CyclePeriodId.</summary>
    public static string? ValidateCycleContext(string frequencyType, string periodType, Guid? cycleId, Guid? cyclePeriodId)
    {
        var freq = FrequencyType.Normalize(frequencyType);
        var period = FrequencyPeriodType.Normalize(periodType);
        var needsCycle = freq == FrequencyType.CycleBased || period == FrequencyPeriodType.Cycle;
        if (needsCycle && cycleId is null && cyclePeriodId is null)
        {
            return "cycle-based frequency requires a CycleId or CyclePeriodId.";
        }

        return null;
    }

    /// <summary>A campaign-period policy — and any campaign-sourced policy — needs a CampaignId (provenance decision).</summary>
    public static string? ValidateCampaignContext(string periodType, string source, Guid? campaignId)
    {
        var period = FrequencyPeriodType.Normalize(periodType);
        if (period == FrequencyPeriodType.CampaignPeriod && campaignId is null)
        {
            return "campaign-period frequency requires a CampaignId.";
        }

        if (FrequencySource.Normalize(source) == FrequencySource.Campaign && campaignId is null)
        {
            return "Source 'campaign' requires a CampaignId for provenance.";
        }

        return null;
    }

    /// <summary>A segmentation-sourced policy needs a SegmentId (provenance decision).</summary>
    public static string? ValidateSegmentContext(string source, Guid? segmentId)
        => FrequencySource.Normalize(source) == FrequencySource.Segmentation && segmentId is null
            ? "Source 'segmentation' requires a SegmentId for provenance."
            : null;

    /// <summary>A custom FrequencyType must carry Notes (controlled validation for the free-form case).</summary>
    public static string? ValidateCustom(string frequencyType, string? notes)
        => FrequencyType.Normalize(frequencyType) == FrequencyType.Custom
           && string.IsNullOrWhiteSpace(notes)
            ? "A 'custom' FrequencyType requires Notes describing the custom cadence."
            : null;
}
