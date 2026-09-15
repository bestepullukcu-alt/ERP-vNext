using System.Text.RegularExpressions;

namespace Diten.HumanCapitalService.Application.Features.SensitiveAccess;

public static class SensitiveAccessGuard
{
    public const string OwnerKey = "hcm.sensitive-access";
    public const string EmployeeProjectionReadPermission = "hcm.employee-projections.read";
    public const string ReadPermission = "hcm.sensitive-access.read";
    public const string ManagePermission = "hcm.sensitive-access.manage";
    public const string ReviewPermission = "hcm.sensitive-access.review";
    public const string AuditReadPermission = "hcm.sensitive-access.audit.read";

    private static readonly string[] ForbiddenMarkers =
    [
        "rawpayload",
        "raw_payload",
        "providerresponse",
        "provider_response",
        "credential",
        "access_token",
        "refresh_token",
        "secret",
        "password",
        "payroll",
        "bank",
        "tax",
        "payslip",
        "biometric",
        "geolocation",
        "nationalid",
        "national_id",
        "dateofbirth",
        "dob",
        "homeaddress",
        "home_address"
    ];

    // Word/token-boundary matcher: markers only match as standalone tokens, so legitimate
    // words such as "taxonomy" (contains "tax") or "scorecard" (contains "score") are NOT
    // falsely rejected, while real markers ("tax", "ssn", "salary", ...) still match. Tested
    // against the RAW value. Underscore is a regex word character, so snake_case markers match.
    private static readonly Regex ForbiddenMarkerRegex = new(
        @"\b(" + string.Join("|", ForbiddenMarkers.Select(Regex.Escape)) + @")\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool HasPermission(IReadOnlyCollection<string> permissions, string permission) =>
        permissions.Any(value => string.Equals(value, permission, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<string> ValidatePolicyVersion(string? sourcePolicyVersion)
    {
        if (string.IsNullOrWhiteSpace(sourcePolicyVersion))
        {
            return ["SourcePolicyVersion is required."];
        }

        return ForbiddenMarkerRegex.IsMatch(sourcePolicyVersion)
            ? ["Sensitive access policy metadata cannot contain raw payload, credential, payroll, bank, tax, payslip, biometric, geolocation, national ID, DOB, home address, or PII-heavy markers."]
            : [];
    }

    private static string Normalize(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        var index = 0;

        foreach (var current in value)
        {
            if (char.IsLetterOrDigit(current) || current == '_')
            {
                buffer[index++] = char.ToLowerInvariant(current);
            }
        }

        return new string(buffer[..index]);
    }
}
