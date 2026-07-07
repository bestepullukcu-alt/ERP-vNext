using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster;

internal static class EmployeeDraftPayloadGuard
{
    private static readonly string[] GovernmentIdentifierKeys =
    [
        "government_id",
        "governmentid",
        "national_id",
        "nationalid",
        "ssn",
        "social_security",
        "tax_id",
        "taxid",
        "passport_number",
        "passportnumber"
    ];

    private static readonly string[] SensitiveKeys =
    [
        "government",
        "national",
        "ssn",
        "social_security",
        "tax",
        "passport",
        "birth",
        "salary",
        "bank",
        "iban"
    ];

    public static bool ContainsGovernmentIdentifier(Dictionary<string, JsonElement>? payload)
        => payload is not null && payload.Any(pair => IsGovernmentIdentifierKey(pair.Key) || ContainsGovernmentIdentifier(pair.Value));

    public static bool IsSensitiveKey(string key)
        => SensitiveKeys.Any(marker => key.Replace("-", "_", StringComparison.Ordinal).Contains(marker, StringComparison.OrdinalIgnoreCase));

    public static string HashIdempotencyKey(string idempotencyKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(idempotencyKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static Dictionary<string, object?> NormalizePayload(Dictionary<string, JsonElement> payload)
    {
        var normalized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in payload)
        {
            normalized[pair.Key] = NormalizeValue(pair.Value);
        }

        return normalized;
    }

    public static Dictionary<string, object?> NormalizeOptionalPayload(Dictionary<string, JsonElement>? payload)
        => payload is null ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) : NormalizePayload(payload);

    private static bool ContainsGovernmentIdentifier(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                if (IsGovernmentIdentifierKey(property.Name) || ContainsGovernmentIdentifier(property.Value))
                {
                    return true;
                }
            }
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray().Any(ContainsGovernmentIdentifier);
        }

        return false;
    }

    private static bool IsGovernmentIdentifierKey(string key)
        => GovernmentIdentifierKeys.Any(marker => key.Replace("-", "_", StringComparison.Ordinal).Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static object? NormalizeValue(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            JsonValueKind.Object => value.EnumerateObject()
                .ToDictionary(property => property.Name, property => NormalizeValue(property.Value), StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => value.EnumerateArray().Select(NormalizeValue).ToArray(),
            _ => value.GetRawText()
        };
}
