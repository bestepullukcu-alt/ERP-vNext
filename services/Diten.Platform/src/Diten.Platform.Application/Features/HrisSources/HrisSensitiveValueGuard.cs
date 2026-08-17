using System.Text.RegularExpressions;

namespace Diten.Platform.Application.Features.HrisSources;

internal static partial class HrisSensitiveValueGuard
{
    public static bool LooksLikeRawSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        return RawSecretPattern().IsMatch(candidate)
               || JwtPattern().IsMatch(candidate)
               || PemPattern().IsMatch(candidate);
    }

    public static bool LooksLikeRawPayload(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        return (candidate.StartsWith('{') && candidate.EndsWith('}'))
               || (candidate.StartsWith('[') && candidate.EndsWith(']'))
               || candidate.Contains("<worker>", StringComparison.OrdinalIgnoreCase)
               || candidate.Contains("<employee>", StringComparison.OrdinalIgnoreCase)
               || candidate.Contains("<person>", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("(password|passwd|secret|token|apikey|api_key|client_secret|refresh_token|access_token|bearer\\s+)", RegexOptions.IgnoreCase)]
    private static partial Regex RawSecretPattern();

    [GeneratedRegex("^[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+$")]
    private static partial Regex JwtPattern();

    [GeneratedRegex("-----BEGIN [A-Z ]+PRIVATE KEY-----", RegexOptions.IgnoreCase)]
    private static partial Regex PemPattern();
}
