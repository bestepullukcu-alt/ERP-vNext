using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Localization;

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

    // ── The governance proxies' answer (Roles, Role Permissions, User Roles) ─────────────────────────────────
    //
    // WP-ROLES-CLOSE-01 — what a governance proxy hands the screen when an operation is refused:
    // { success:false, errors, errorCode, errorCodes, local:true }. errorCodes = every stable code of the refusal
    // (read by Read above — the ONE reader of a refused hop); errors = ONE sentence of this application's own, already
    // localized. ⚠ NOTHING FROM UPSTREAM REACHES THE BROWSER: AuthService's sentence and an exception's message (which
    // names internal hosts) go to the server log. A coded 4xx is a business refusal (Information); anything else —
    // no code, or a 5xx — is not expected (Warning).

    /// <summary>The gateway answered with a failure.</summary>
    public static async Task<object> ReadAsync(HttpResponseMessage response, IStringLocalizer<SharedResource> localizer, ILogger logger)
    {
        var raw = await response.Content.ReadAsStringAsync();
        var refusal = Read(raw);
        var codes = refusal.Codes.Select(c => c.Code).Distinct(StringComparer.Ordinal).ToList();
        var status = (int)response.StatusCode;
        var level = codes.Count > 0 && status is >= 400 and < 500 ? LogLevel.Information : LogLevel.Warning;
        logger.Log(level,
            "Gateway refused {Method} {Path} with {StatusCode}. Codes=[{Codes}] Uncoded={Uncoded} Upstream=[{Upstream}]",
            response.RequestMessage?.Method.Method,
            response.RequestMessage?.RequestUri?.AbsolutePath,
            status,
            string.Join(", ", codes),
            refusal.HasUncoded,
            string.Join(" | ", UpstreamSentences(raw)));

        var own = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => localizer["Unauthorized"].Value,
            HttpStatusCode.Forbidden => localizer["AccessDenied"].Value,
            _ => localizer["GatewayError"].Value
        };
        return Answer(own, codes);
    }

    /// <summary>The call itself failed (gateway unreachable, timeout, a body that could not be read).</summary>
    public static object Failure(Exception exception, IStringLocalizer<SharedResource> localizer, ILogger logger, string operation)
    {
        logger.LogError(exception, "Governance proxy call failed: {Operation}.", operation);
        return Answer(localizer["GatewayError"].Value, []);
    }

    /// <summary>The posted form breaks rules this application checks itself; one stable code per broken rule.</summary>
    public static object Invalid(IReadOnlyList<string> codes, IStringLocalizer<SharedResource> localizer)
        => Answer(localizer["ValidationFailed"].Value, codes);

    /// <summary>A sentence this application produced itself (already in the reader's language).</summary>
    public static object Local(string sentence) => Answer(sentence, []);

    private static object Answer(string ownSentence, IReadOnlyList<string> codes)
        => new
        {
            success = false,
            errors = new List<string> { ownSentence },
            errorCode = codes.Count > 0 ? codes[0] : null,
            errorCodes = codes,
            local = true
        };

    /// <summary>The service's own sentences, for the server log only.</summary>
    private static IReadOnlyList<string> UpstreamSentences(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return [];
            if (doc.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                return errors.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList();
            }

            return doc.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String ? [detail.GetString()!] : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
