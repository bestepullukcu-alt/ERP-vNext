using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Services.Governance;

/// <summary>
/// WP-ROLES-CLOSE-01 — what a governance proxy (Roles, Role Permissions, User Roles) hands the screen when an
/// operation is refused: <c>{ success:false, errors, errorCode, errorCodes, local:true }</c>.
/// <list type="bullet">
/// <item><c>errorCodes</c> — the stable codes of the refusal (AuthService's Response envelope, or the form's own
/// rules); <c>errorCode</c> is the first one. The screen maps each to a resx key in the reader's language.</item>
/// <item><c>errors</c> — ONE sentence of this application's own, already localized (signed out / not permitted /
/// gateway error / check the form): what the screen says when it has no sentence for the code.</item>
/// </list>
/// ⚠ NOTHING FROM UPSTREAM REACHES THE BROWSER. AuthService's sentence is English whatever the reader's language, and an
/// exception's message names internal hosts (the gateway address). Both are written to the server log and stay there.
/// </summary>
public static class GatewayRefusal
{
    /// <summary>The gateway answered with a failure.</summary>
    public static async Task<object> ReadAsync(HttpResponseMessage response, IStringLocalizer<SharedResource> localizer, ILogger logger)
    {
        var (codes, upstream) = await ReadEnvelopeAsync(response);
        // A coded 4xx is a business refusal — the product working as designed (a name already taken, a locked grant):
        // Information. Anything else — no code, or a 5xx — is not expected: Warning.
        var status = (int)response.StatusCode;
        var level = codes.Count > 0 && status is >= 400 and < 500 ? LogLevel.Information : LogLevel.Warning;
        logger.Log(level,
            "Gateway refused {Method} {Path} with {StatusCode}. Codes=[{Codes}] Upstream=[{Upstream}]",
            response.RequestMessage?.Method.Method,
            response.RequestMessage?.RequestUri?.AbsolutePath,
            (int)response.StatusCode,
            string.Join(", ", codes),
            string.Join(" | ", upstream));

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

    private static async Task<(List<string> Codes, List<string> Upstream)> ReadEnvelopeAsync(HttpResponseMessage response)
    {
        var codes = new List<string>();
        var upstream = new List<string>();
        try
        {
            var raw = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(raw)) return (codes, upstream);
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return (codes, upstream);

            if (root.TryGetProperty("errors", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                upstream.AddRange(list.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(e.GetString()))
                    .Select(e => e.GetString()!));
            }

            if (root.TryGetProperty("errorCodes", out var entries) && entries.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    if (entry.ValueKind == JsonValueKind.Object
                        && entry.TryGetProperty("code", out var code)
                        && code.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(code.GetString())
                        && !codes.Contains(code.GetString()!))
                    {
                        codes.Add(code.GetString()!);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not an envelope (a proxy page, an empty body): there is no code to hand over.
        }

        return (codes, upstream);
    }
}
