using System.Text.RegularExpressions;

namespace Diten.Platform.Application.Features.PayrollIntegrationGovernance;

public static partial class PayrollIntegrationGovernanceSensitiveValueGuard
{
    private static readonly string[] SensitiveMarkers =
    [
        "password",
        "passwd",
        "pwd=",
        "secret",
        "client_secret",
        "token",
        "bearer ",
        "authorization",
        "apikey",
        "api_key",
        "credential",
        "private_key",
        "payload",
        "raw",
        "payslip",
        "bank",
        "iban",
        "swift",
        "tax",
        "ssn",
        "national id",
        "biometric",
        "fingerprint",
        "faceprint",
        "geolocation",
        "latitude",
        "longitude",
        "gps",
        "gross",
        "netpay",
        "salary_amount",
        "payroll calculation",
        "adapter",
        "webhook",
        "provider client"
    ];

    private static readonly string[] PayloadMarkers =
    [
        "{",
        "}",
        "\"payroll\"",
        "\"attendance\"",
        "\"hris\"",
        "\"payload\"",
        "<payroll",
        "<attendance",
        "<hris"
    ];

    public static bool LooksUnsafe(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return SensitiveMarkers.Any(normalized.Contains)
            || PayloadMarkers.Any(normalized.Contains)
            || JwtRegex().IsMatch(value)
            || normalized.StartsWith("basic ", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+", RegexOptions.Compiled)]
    private static partial Regex JwtRegex();
}
