using System.Text.RegularExpressions;

namespace Diten.Platform.Application.Features.PayrollSources;

public static partial class PayrollSensitiveValueGuard
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
        "private_key",
        "credential",
        "bank",
        "iban",
        "swift",
        "routingnumber",
        "taxid",
        "tax_id",
        "ssn",
        "nationalid",
        "payslip"
    ];

    private static readonly string[] PayloadMarkers =
    [
        "{",
        "}",
        "\"employee\"",
        "\"payroll\"",
        "\"gross\"",
        "\"net\"",
        "\"earnings\"",
        "\"deductions\"",
        "<payslip",
        "<payroll",
        "raw-payload"
    ];

    public static bool LooksLikeRawSecret(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return SensitiveMarkers.Any(normalized.Contains)
               || JwtRegex().IsMatch(value)
               || normalized.StartsWith("basic ", StringComparison.Ordinal);
    }

    public static bool LooksLikeRawPayrollPayload(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return PayloadMarkers.Any(normalized.Contains)
               || normalized.Contains("grosspay", StringComparison.Ordinal)
               || normalized.Contains("netpay", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+", RegexOptions.Compiled)]
    private static partial Regex JwtRegex();
}
