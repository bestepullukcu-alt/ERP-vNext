using System.Text.RegularExpressions;

namespace Diten.Platform.Application.Features.PersonReferenceDirectory;

internal static partial class PersonReferenceSensitiveValueGuard
{
    public static bool LooksForbidden(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        return LooksLikeRawPayload(candidate)
               || RawSecretPattern().IsMatch(candidate)
               || PiiHeavyPattern().IsMatch(candidate)
               || PayrollAndProviderPattern().IsMatch(candidate)
               || JwtPattern().IsMatch(candidate)
               || PemPattern().IsMatch(candidate);
    }

    private static bool LooksLikeRawPayload(string candidate) =>
        (candidate.StartsWith('{') && candidate.EndsWith('}'))
        || (candidate.StartsWith('[') && candidate.EndsWith(']'))
        || candidate.Contains("<worker>", StringComparison.OrdinalIgnoreCase)
        || candidate.Contains("<employee>", StringComparison.OrdinalIgnoreCase)
        || candidate.Contains("<person>", StringComparison.OrdinalIgnoreCase)
        || candidate.Contains("<payroll>", StringComparison.OrdinalIgnoreCase)
        || candidate.Contains("<candidate>", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex("(password|passwd|secret|token|apikey|api_key|client_secret|refresh_token|access_token|credential|bearer\\s+)", RegexOptions.IgnoreCase)]
    private static partial Regex RawSecretPattern();

    [GeneratedRegex("(date_of_birth|dob|birthdate|national_id|ssn|social_security|passport|home_address|street_address|bank|iban|swift|tax|payslip|salary|compensation|biometric|fingerprint|faceprint|geolocation|latitude|longitude)", RegexOptions.IgnoreCase)]
    private static partial Regex PiiHeavyPattern();

    [GeneratedRegex("(payroll|time_attendance|attendance|timesheet|provider_adapter|adapter_client|webhook|background_sync|candidate|talent_profile|requisition)", RegexOptions.IgnoreCase)]
    private static partial Regex PayrollAndProviderPattern();

    [GeneratedRegex("^[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+$")]
    private static partial Regex JwtPattern();

    [GeneratedRegex("-----BEGIN [A-Z ]+PRIVATE KEY-----", RegexOptions.IgnoreCase)]
    private static partial Regex PemPattern();
}
