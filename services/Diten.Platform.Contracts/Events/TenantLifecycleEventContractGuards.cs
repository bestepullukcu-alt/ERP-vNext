using System.Text.RegularExpressions;

namespace Diten.Platform.Contracts.Events;

internal static partial class TenantLifecycleEventContractGuards
{
    public static Guid RequireTenantId(Guid tenantId)
    {
        return tenantId == Guid.Empty
            ? throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId))
            : tenantId;
    }

    public static DateTimeOffset RequireUtc(DateTimeOffset value, string parameterName)
    {
        return value.Offset == TimeSpan.Zero
            ? value
            : throw new ArgumentException($"{parameterName} must be UTC.", parameterName);
    }

    public static string RequireText(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} cannot be empty.", parameterName);
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : throw new ArgumentException($"{parameterName} cannot exceed {maxLength} characters.", parameterName);
    }

    public static string? OptionalText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    public static IReadOnlyList<string> RequireSteps(IReadOnlyList<string> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var normalized = steps
            .Select(step => RequireText(step, nameof(steps), 128))
            .ToArray();

        return normalized.Length == 0
            ? throw new ArgumentException("Steps must contain at least one item.", nameof(steps))
            : normalized;
    }

    public static string RedactSensitiveError(string error)
    {
        var normalized = RequireText(error, nameof(error), 2000);
        return SensitiveValuePattern().Replace(normalized, match => $"{match.Groups[1].Value}=[REDACTED]");
    }

    [GeneratedRegex(@"(?i)\b(password|pwd|token|secret|connection[-_ ]?string|apikey|api[-_ ]?key)\b\s*[:=]\s*[^;\s,]+")]
    private static partial Regex SensitiveValuePattern();
}
