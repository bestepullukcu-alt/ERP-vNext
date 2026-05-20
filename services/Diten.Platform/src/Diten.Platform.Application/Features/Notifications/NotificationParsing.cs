using System.Text.RegularExpressions;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.Notifications;

public static partial class NotificationParsing
{
    public static string NormalizeTemplateKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    public static string NormalizeLocale(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "en" : value.Trim().ToLowerInvariant();

    public static string NormalizeEmail(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    public static bool TryParseProvider(string? value, out MessagingProviderCode provider) =>
        Enum.TryParse(value, ignoreCase: true, out provider) && Enum.IsDefined(provider);

    public static bool TryParseChannel(string? value, out NotificationChannelCode channel) =>
        Enum.TryParse(value, ignoreCase: true, out channel) && Enum.IsDefined(channel);

    public static bool TryParseTemplateStatus(string? value, out NotificationTemplateStatus status) =>
        Enum.TryParse(value, ignoreCase: true, out status) && Enum.IsDefined(status);

    public static bool TryParseVariableType(string? value, out TemplateVariableType type) =>
        Enum.TryParse(value, ignoreCase: true, out type) && Enum.IsDefined(type);

    public static bool TryParseFallbackPolicy(string? value, out NotificationFallbackPolicy policy) =>
        Enum.TryParse(value, ignoreCase: true, out policy) && Enum.IsDefined(policy);

    public static bool IsValidTemplateKey(string? value) =>
        !string.IsNullOrWhiteSpace(value) && TemplateKeyRegex().IsMatch(value.Trim());

    public static bool IsValidVariableName(string? value) =>
        !string.IsNullOrWhiteSpace(value) && VariableNameRegex().IsMatch(value.Trim());

    public static bool LooksLikeRawSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        return candidate.Contains(' ')
               || candidate.Contains('=')
               || candidate.StartsWith("sk-", StringComparison.OrdinalIgnoreCase)
               || candidate.StartsWith("SG.", StringComparison.OrdinalIgnoreCase)
               || candidate.Contains("password", StringComparison.OrdinalIgnoreCase)
               || candidate.Contains("apikey", StringComparison.OrdinalIgnoreCase)
               || candidate.Contains("api_key", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("^[a-z0-9]+(\\.[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex TemplateKeyRegex();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_.]*$", RegexOptions.CultureInvariant)]
    private static partial Regex VariableNameRegex();
}
