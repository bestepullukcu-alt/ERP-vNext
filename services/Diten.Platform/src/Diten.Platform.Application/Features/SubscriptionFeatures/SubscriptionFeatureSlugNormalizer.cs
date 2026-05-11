using System.Text.RegularExpressions;

namespace Diten.Platform.Application.Features.SubscriptionFeatures;

public static class SubscriptionFeatureSlugNormalizer
{
    private static readonly Regex SeparatorPattern = new(@"[\s_]+", RegexOptions.Compiled);
    private static readonly Regex InvalidPattern = new(@"[^a-z0-9-]", RegexOptions.Compiled);
    private static readonly Regex RepeatedDashPattern = new(@"-+", RegexOptions.Compiled);

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant();
        normalized = SeparatorPattern.Replace(normalized, "-");
        normalized = InvalidPattern.Replace(normalized, "-");
        normalized = RepeatedDashPattern.Replace(normalized, "-");
        return normalized.Trim('-');
    }
}
