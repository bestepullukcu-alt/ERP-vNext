using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.DoctorInsights;

/// <summary>WP-VW-W4 (W4-BE-c, §2.7) — the doctor insights keys (English descriptions in the Auth catalog).</summary>
public static class DoctorInsightsPermissions
{
    /// <summary>The profile / potentials / indication rows of the doctors the caller may see (own + team, §2.4).</summary>
    public const string Read = "crm.doctor-insights.read";

    /// <summary>Edit a doctor profile by hand (the report writes through the recorder).</summary>
    public const string Manage = "crm.doctor-insights.manage";

    /// <summary>Every doctor of the tenant (management, test).</summary>
    public const string ReadAll = "crm.doctor-insights.read-all";

    /// <summary>Marketing: the class × share × opportunity counts, never a doctor's identity.</summary>
    public const string ReadAggregate = "crm.doctor-insights.read-aggregate";

    /// <summary>The per-product potential thresholds (the settings page).</summary>
    public const string ThresholdsManage = "crm.doctor-insights.thresholds.manage";

    public static readonly IReadOnlyList<string> All = new[] { Read, Manage, ReadAll, ReadAggregate, ThresholdsManage };
}

/// <summary>
/// WP-VW-W4 (W4-BE-c, §2.3) — THE opportunity matrix (single source; Web repeats it only to preview):
/// <code>
///            none / occasional   half           mostly / always
///   high     opportunity         grow           keep
///   medium   grow                grow           keep
///   low      low_priority        low_priority   keep
/// </code>
/// No class (no number) or no share ⇒ null (no label).
/// </summary>
public static class OpportunityRules
{
    public static string? Classify(string? potentialClass, string? shareBucket)
    {
        if (string.IsNullOrWhiteSpace(potentialClass) || string.IsNullOrWhiteSpace(shareBucket)) return null;
        var share = shareBucket.Trim().ToLowerInvariant();
        if (!ShareBuckets.All.Contains(share)) return null;
        if (share is ShareBuckets.Mostly or ShareBuckets.Always) return OpportunityCodes.Keep;

        var low = share is ShareBuckets.None or ShareBuckets.Occasional;
        return potentialClass.Trim().ToLowerInvariant() switch
        {
            PotentialClasses.High => low ? OpportunityCodes.Opportunity : OpportunityCodes.Grow,
            PotentialClasses.Medium => OpportunityCodes.Grow,
            PotentialClasses.Low => OpportunityCodes.LowPriority,
            _ => null
        };
    }

    /// <summary>The planning order: opportunity, grow, keep, low priority, none.</summary>
    public static int Rank(string? opportunity) => opportunity switch
    {
        OpportunityCodes.Opportunity => 0,
        OpportunityCodes.Grow => 1,
        OpportunityCodes.Keep => 2,
        OpportunityCodes.LowPriority => 3,
        _ => 4
    };
}

/// <summary>The effective thresholds of one product and where they come from.</summary>
public sealed record EffectiveThreshold(int LowBelow, int HighAbove, string Source);

public static class ThresholdSources
{
    public const string Product = "product";
    public const string TenantDefault = "default";
    public const string BuiltIn = "built_in";
}

/// <summary>
/// WP-VW-W4 (W4-BE-c, §2.2, K-W4) — the potential rules: the class from the indication total × the product's thresholds,
/// the total rule (indications ≤ the doctor's monthly patients) and the three-month rule (<c>isDue</c>).
/// </summary>
public static class PotentialRules
{
    /// <summary>total &lt; LowBelow ⇒ low; total &gt; HighAbove ⇒ high; else medium. No total ⇒ null.</summary>
    public static string? Classify(int? total, EffectiveThreshold threshold)
        => total is not { } t ? null
            : t < threshold.LowBelow ? PotentialClasses.Low
            : t > threshold.HighAbove ? PotentialClasses.High
            : PotentialClasses.Medium;

    /// <summary>The indications may not exceed the doctor's total (only when both are known).</summary>
    public static bool ExceedsTotal(int? indicationTotal, int? doctorTotal)
        => indicationTotal is { } i && doctorTotal is { } d && i > d;

    /// <summary>
    /// The numbers are asked again when they are OLDER than 90 days (UTC days; 90 days old is still current, 91 is due)
    /// or were never given.
    /// </summary>
    public static bool IsDue(DateTimeOffset? asOf, DateTimeOffset now)
        => asOf is not { } at
           || DateOnly.FromDateTime(now.UtcDateTime).DayNumber - DateOnly.FromDateTime(at.UtcDateTime).DayNumber > DoctorInsightsLimits.DueAfterDays;

    /// <summary>0 &lt; LowBelow ≤ HighAbove (and both within the patient bound).</summary>
    public static bool ValidThreshold(int lowBelow, int highAbove)
        => lowBelow > 0 && lowBelow <= highAbove && highAbove <= DoctorInsightsLimits.MaxMonthlyPatients;

    /// <summary>The product's own document, else the tenant default document, else 10 / 30.</summary>
    public static EffectiveThreshold Resolve(IReadOnlyList<ProductPotentialThreshold> stored, Guid productId)
    {
        var own = stored.FirstOrDefault(t => t.GlobalProductId == productId && !t.IsDeleted);
        if (own is not null) return new EffectiveThreshold(own.LowBelow, own.HighAbove, ThresholdSources.Product);
        var tenant = stored.FirstOrDefault(t => t.GlobalProductId is null && !t.IsDeleted);
        return tenant is not null
            ? new EffectiveThreshold(tenant.LowBelow, tenant.HighAbove, ThresholdSources.TenantDefault)
            : new EffectiveThreshold(ProductPotentialThresholdDefaults.LowBelow, ProductPotentialThresholdDefaults.HighAbove, ThresholdSources.BuiltIn);
    }
}

/// <summary>
/// WP-VW-W4 (W4-BE-c, §2.2) — the Concept Graph type codes of the indication and the patient profile nodes. They are
/// TENANT data (seed <c>CT-IND</c>, <c>CT-PROF</c>), so they come from configuration
/// (<c>Crm:DoctorInsights:ConceptTypes:Indication</c> / <c>:Profile</c>).
/// </summary>
public interface IDoctorInsightsSettings
{
    string IndicationConceptTypeCode { get; }
    string ProfileConceptTypeCode { get; }
}

public static class DoctorInsightsDefaults
{
    public const string IndicationConceptTypeCode = "CT-IND";
    public const string ProfileConceptTypeCode = "CT-PROF";
}

public sealed class DefaultDoctorInsightsSettings : IDoctorInsightsSettings
{
    public string IndicationConceptTypeCode => DoctorInsightsDefaults.IndicationConceptTypeCode;
    public string ProfileConceptTypeCode => DoctorInsightsDefaults.ProfileConceptTypeCode;
}
