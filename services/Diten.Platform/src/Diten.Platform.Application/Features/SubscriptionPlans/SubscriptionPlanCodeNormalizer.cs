using System.Text;
using System.Text.RegularExpressions;

namespace Diten.Platform.Application.Features.SubscriptionPlans;

public static class SubscriptionPlanCodeNormalizer
{
    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return string.Empty;
        }

        var trimmed = code.Trim();
        var sb = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            sb.Append(ch switch
            {
                ' ' or '_' => '-',
                _ => ch
            });
        }

        var normalized = sb.ToString();
        normalized = Regex.Replace(normalized, @"-+", "-");
        normalized = normalized.Trim('-');
        return normalized.ToUpperInvariant();
    }
}

