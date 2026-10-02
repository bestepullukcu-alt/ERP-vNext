using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Localization;

namespace Diten.Web.Services.Governance;

/// <summary>
/// WP-ROLES-CLOSE-01 — what a governance proxy (Roles, Role Permissions, User Roles) hands the screen when the gateway
/// refuses: <c>{ success:false, errors, errorCode, local }</c>.
/// <list type="bullet">
/// <item><c>errorCode</c> — the first stable code of the Response envelope's <c>errorCodes</c>; the screen maps it to
/// a resx key in the reader's language. <c>errors</c> then keeps AuthService's English sentence (console, logs).</item>
/// <item>No code — AuthService's sentence is NOT handed over (it is English, whatever the reader's language):
/// <c>errors</c> carries this application's own localized sentence (signed out / not permitted / gateway error) and
/// <c>local</c> is true, which tells the screen the sentence is safe to show.</item>
/// </list>
/// </summary>
public static class GatewayRefusal
{
    public static async Task<object> ReadAsync(HttpResponseMessage response, IStringLocalizer<SharedResource> localizer)
    {
        var (code, errors) = await ReadEnvelopeAsync(response);
        if (code is not null)
        {
            return new { success = false, errors, errorCode = (string?)code, local = false };
        }

        var own = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => localizer["Unauthorized"].Value,
            HttpStatusCode.Forbidden => localizer["AccessDenied"].Value,
            _ => localizer["GatewayError"].Value
        };
        return new { success = false, errors = new List<string> { own }, errorCode = (string?)null, local = true };
    }

    /// <summary>A sentence this application produced itself (already in the reader's language).</summary>
    public static object Local(string sentence)
        => new { success = false, errors = new List<string> { sentence }, errorCode = (string?)null, local = true };

    private static async Task<(string? Code, List<string> Errors)> ReadEnvelopeAsync(HttpResponseMessage response)
    {
        var errors = new List<string>();
        try
        {
            var raw = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(raw)) return (null, errors);
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return (null, errors);

            if (root.TryGetProperty("errors", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                errors.AddRange(list.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(e.GetString()))
                    .Select(e => e.GetString()!));
            }

            if (root.TryGetProperty("errorCodes", out var codes) && codes.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in codes.EnumerateArray())
                {
                    if (entry.ValueKind == JsonValueKind.Object
                        && entry.TryGetProperty("code", out var code)
                        && code.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(code.GetString()))
                    {
                        return (code.GetString(), errors);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Not an envelope (a proxy page, an empty body): there is no code to hand over.
        }

        return (null, errors);
    }
}
