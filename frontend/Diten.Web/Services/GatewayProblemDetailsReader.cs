using System.Text.Json;

namespace Diten.Web.Services;

/// <summary>
/// BL-398 (WP-PSS-MOD0024-FOLLOWUPS-02) — the ONE reader for ASP.NET's OWN 400, extracted from
/// <c>TaskFieldDefinitionsController</c>, where it first shipped, into a shared helper five Task-screen
/// controllers now call.
///
/// <para><b>Why this exists at all.</b> Platform's envelope (<c>GatewayResponse&lt;T&gt;.Errors</c>) is not what
/// the gateway answers with when a request body cannot even be BOUND (a null for a non-nullable field, a wrong
/// type) — that never reaches a handler, so ASP.NET's own model-binding pipeline answers first, with a
/// ProblemDetails body whose <c>errors</c> is a field → messages dictionary. A controller that only knows the
/// envelope shape falls through to printing that JSON verbatim on the page.</para>
///
/// <para><b>Why a shared reader and not five copies.</b> <c>TaskFieldDefinitionsController</c> had this rule;
/// <c>TaskTypesController</c>, <c>TaskChecklistTemplatesController</c>, <c>TaskRecurrenceRulesController</c> and
/// <c>TaskTemplatesController</c> did not — each fell through to the raw-body fallback for exactly this shape.
/// A sixth Task-form screen written by copying one of the four would have copied the gap, not the fix.</para>
///
/// <para>What this does NOT decide: the caller's own reason-code-to-sentence map (unchanged, per controller) and
/// the final "nothing at all could be read" fallback message (each controller already has its own localized
/// <c>GatewayError</c> key; this helper does not carry a localizer).</para>
/// </summary>
public static class GatewayProblemDetailsReader
{
    /// <summary>
    /// True when <paramref name="raw"/> is a ProblemDetails body (an <c>errors</c> object, or <c>title</c> +
    /// <c>status</c>), with <paramref name="errors"/> holding its field messages — DISTINCT, non-empty strings
    /// only. A ProblemDetails with no usable field messages (a plain 500, an <c>errors</c> object with nothing
    /// stringy in it) still returns true with an EMPTY list: the caller knows the shape was recognised and
    /// substitutes its own generic sentence, rather than this helper inventing text on their behalf.
    /// </summary>
    public static bool TryReadErrors(string? raw, out List<string> errors)
    {
        errors = [];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var hasFieldErrors = root.TryGetProperty("errors", out var fieldErrors)
                && fieldErrors.ValueKind == JsonValueKind.Object;
            var isProblemDetails = hasFieldErrors
                || (root.TryGetProperty("title", out _) && root.TryGetProperty("status", out _));
            if (!isProblemDetails)
            {
                return false;
            }

            if (hasFieldErrors)
            {
                errors = fieldErrors.EnumerateObject()
                    .Where(field => field.Value.ValueKind == JsonValueKind.Array)
                    .SelectMany(field => field.Value.EnumerateArray())
                    .Where(message => message.ValueKind == JsonValueKind.String)
                    .Select(message => message.GetString())
                    .Where(message => !string.IsNullOrWhiteSpace(message))
                    .Select(message => message!)
                    .Distinct()
                    .ToList();
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
