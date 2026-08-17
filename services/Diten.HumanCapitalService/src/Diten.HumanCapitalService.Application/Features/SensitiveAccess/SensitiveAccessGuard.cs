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

    public static bool HasPermission(IReadOnlyCollection<string> permissions, string permission) =>
        permissions.Any(value => string.Equals(value, permission, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<string> ValidatePolicyVersion(string? sourcePolicyVersion)
    {
        if (string.IsNullOrWhiteSpace(sourcePolicyVersion))
        {
            return ["SourcePolicyVersion is required."];
        }

        var normalized = Normalize(sourcePolicyVersion);
        return ForbiddenMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal))
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
