using System.Text.RegularExpressions;

namespace Diten.Platform.Application.Features.TimeAttendanceProviders;

public static partial class TimeAttendanceSensitiveValueGuard
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
        "biometric",
        "fingerprint",
        "faceprint",
        "facial",
        "geolocation",
        "geo_lat",
        "geo_long",
        "latitude",
        "longitude",
        "gps",
        "payroll",
        "gross",
        "netpay",
        "payslip",
        "bank",
        "tax"
    ];

    private static readonly string[] PayloadMarkers =
    [
        "{",
        "}",
        "\"time\"",
        "\"attendance\"",
        "\"clock\"",
        "\"employee\"",
        "\"payload\"",
        "<time",
        "<attendance",
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

    public static bool LooksLikeRawPayload(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return PayloadMarkers.Any(normalized.Contains);
    }

    [GeneratedRegex(@"eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+", RegexOptions.Compiled)]
    private static partial Regex JwtRegex();
}
