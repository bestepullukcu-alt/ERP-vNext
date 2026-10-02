using System.Text.Json;

namespace Diten.Web.Services.Auth;

/// <summary>One stable code of a refused call, with the string params the service attached (e.g. minLength).</summary>
public sealed record GatewayErrorCode(string Code, IReadOnlyDictionary<string, string>? Params);

/// <summary>
/// WP-USERS-ERROR-CODES-01 — what a refused AuthService call says in a form the Web can localize: EVERY code it
/// carries, and how many failures it reported in all. Both wire shapes are read the same way: the Response envelope
/// ({ errors: [...], errorCodes: [...] }) and a validator's body ({ title, detail, errorCodes: [...] }).
/// The service's own sentences are never returned from here — they are English and belong in the server log.
/// </summary>
public sealed record GatewayRefusal(IReadOnlyList<GatewayErrorCode> Codes, int FailureCount)
{
    /// <summary>True when at least one reported failure has no code — it must not be dropped in silence.</summary>
    public bool HasUncoded => FailureCount > Codes.Count;

    public static GatewayRefusal Read(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new GatewayRefusal([], 1);
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new GatewayRefusal([], 1);
            }

            var codes = new List<GatewayErrorCode>();
            if (root.TryGetProperty("errorCodes", out var entries) && entries.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object
                        || !entry.TryGetProperty("code", out var code)
                        || code.ValueKind != JsonValueKind.String
                        || string.IsNullOrWhiteSpace(code.GetString()))
                    {
                        continue;
                    }

                    Dictionary<string, string>? parameters = null;
                    if (entry.TryGetProperty("params", out var ps) && ps.ValueKind == JsonValueKind.Object)
                    {
                        parameters = ps.EnumerateObject()
                            .Where(p => p.Value.ValueKind == JsonValueKind.String)
                            .ToDictionary(p => p.Name, p => p.Value.GetString()!, StringComparer.Ordinal);
                    }

                    codes.Add(new GatewayErrorCode(code.GetString()!, parameters is { Count: > 0 } ? parameters : null));
                }
            }

            return new GatewayRefusal(codes, Math.Max(CountFailures(root), codes.Count == 0 ? 1 : 0));
        }
        catch (JsonException)
        {
            return new GatewayRefusal([], 1);
        }
    }

    // A validator's body lists one "-- Property: sentence Severity: Error" per failure in `detail`; the envelope
    // carries one entry per refusal in `errors`. (A handler-thrown validation joins its sentences into ONE envelope
    // error, so there the count can only under-report — never invent a failure.)
    private static int CountFailures(JsonElement root)
    {
        if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
        {
            var text = detail.GetString() ?? string.Empty;
            var count = 0;
            for (var at = text.IndexOf(" Severity: ", StringComparison.Ordinal); at >= 0; at = text.IndexOf(" Severity: ", at + 1, StringComparison.Ordinal))
            {
                count++;
            }

            return count;
        }

        return root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array ? errors.GetArrayLength() : 0;
    }
}
