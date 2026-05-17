using System.Text.RegularExpressions;

namespace Diten.Platform.Common.Observability;

public static partial class SensitiveDataRedactor
{
    public static string Redact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var redacted = EmailPattern().Replace(value, "[REDACTED_EMAIL]");
        redacted = PhonePattern().Replace(redacted, "[REDACTED_PHONE]");
        redacted = JwtPattern().Replace(redacted, "[REDACTED_JWT]");
        redacted = MongoConnectionStringPattern().Replace(redacted, "mongodb://[REDACTED]");
        redacted = AngleCredentialPattern().Replace(redacted, "<[REDACTED]>");
        redacted = ConnectionStringPattern().Replace(redacted, "$1=[REDACTED]");
        redacted = SecretValuePattern().Replace(redacted, "$1=[REDACTED]");
        return redacted;
    }

    public static bool IsSensitivePropertyName(string propertyName)
    {
        return SensitivePropertyPattern().IsMatch(propertyName);
    }

    [GeneratedRegex(@"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"(?<![A-Za-z0-9])\+?\d[\d\s().-]{7,}\d(?![A-Za-z0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"eyJ[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+", RegexOptions.CultureInvariant)]
    private static partial Regex JwtPattern();

    [GeneratedRegex(@"mongodb(?:\+srv)?://[^\s,'""}]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MongoConnectionStringPattern();

    [GeneratedRegex(@"<(?i:username|password|token|secret|api[_-]?key)>", RegexOptions.CultureInvariant)]
    private static partial Regex AngleCredentialPattern();

    [GeneratedRegex(@"(?i)\b(password|pwd|token|api[_-]?key|secret|client[_-]?secret|connectionstring)\s*=\s*[^;,\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex ConnectionStringPattern();

    [GeneratedRegex(@"(?i)\b(password|pwd|token|api[_-]?key|secret|client[_-]?secret|authorization|otp|jwt|bearer|refresh[_-]?token|access[_-]?token)\b\s*[:=]\s*[""']?[^,""'\s}]+", RegexOptions.CultureInvariant)]
    private static partial Regex SecretValuePattern();

    [GeneratedRegex(@"(?i)(password|pwd|token|api[_-]?key|secret|authorization|otp|jwt|bearer|refresh|access|connection|string|email|phone)")]
    private static partial Regex SensitivePropertyPattern();
}
