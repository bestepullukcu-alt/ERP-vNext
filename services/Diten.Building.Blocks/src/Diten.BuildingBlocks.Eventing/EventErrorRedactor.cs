using System.Text.RegularExpressions;

namespace Diten.BuildingBlocks.Eventing;

public static partial class EventErrorRedactor
{
    [GeneratedRegex("(?i)(password|token|secret|credential|connectionstring|api[_-]?key|private[_-]?key)\\s*[:=]\\s*[^\\s;,\"]+")]
    private static partial Regex SensitiveValueRegex();

    public static string RedactAndTruncate(string? value, int maxLength = 4000)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var redacted = SensitiveValueRegex().Replace(value, "$1=[REDACTED]");
        return redacted.Length <= maxLength ? redacted : redacted[..maxLength];
    }
}
