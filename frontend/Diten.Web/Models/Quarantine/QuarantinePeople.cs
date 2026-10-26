using System.Text.Json;
using System.Text.Json.Nodes;

namespace Diten.Web.Models.Quarantine;

/// <summary>
/// FU-0175-Q5-02 (WP-0174-ITEM) — display names for the people on a quarantine page (creator, review starter, history actor,
/// decider, approver, approver candidates). No contract change and no new service endpoint: QUARANTINE-BUNDLE's <c>UserRef</c>
/// already has an optional <c>name</c>, and the Web fills it the way this controller fills product / location names
/// (<c>MasterLabelResolver</c>: one server-side pass per page call, with the CALLER's token). The source is AuthService's existing
/// per-id label read <c>GET /api/users/{id}/display-label</c> (key <c>auth.users.lookup</c>, tenant-scoped, a missing user and
/// another tenant's user are the same 404) — the label-only read, so no e-mail or other profile data reaches the page.
/// An id that does not resolve (no key, 404, AuthService down) keeps <c>name</c> empty; the page then shows the short id with
/// "unknown user" (QuarantineUi.person) — never a guessed name, never an error for the page.
/// </summary>
public static class QuarantinePeople
{
    /// <summary>At most this many distinct ids are resolved per page call (one label read each).</summary>
    public const int MaxIds = 40;

    public const string CandidatePrefix = "user:";

    /// <summary>Every <c>{ "userId": "&lt;uuid&gt;" }</c> object id and every <c>"user:&lt;uuid&gt;"</c> candidate in the trees, distinct, in order.</summary>
    public static IReadOnlyList<string> UserIds(params JsonNode?[] roots)
    {
        var ids = new List<string>();
        void Add(string? value)
        {
            if (Guid.TryParse(value, out var id) && !ids.Contains(id.ToString("D"))) ids.Add(id.ToString("D"));
        }

        void Walk(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject obj:
                    if (obj["userId"] is JsonValue user && user.TryGetValue<string>(out var userId)) Add(userId);
                    foreach (var (_, child) in obj) Walk(child);
                    break;
                case JsonArray array:
                    foreach (var child in array) Walk(child);
                    break;
                case JsonValue value when value.TryGetValue<string>(out var text) && text.StartsWith(CandidatePrefix, StringComparison.Ordinal):
                    Add(text[CandidatePrefix.Length..]);
                    break;
            }
        }

        foreach (var root in roots) Walk(root);
        return ids;
    }

    /// <summary>One label read per id (≤ <see cref="MaxIds"/>); only a 200 with a non-empty <c>displayLabel</c> counts.</summary>
    public static async Task<IReadOnlyDictionary<string, string>> ResolveAsync(
        IEnumerable<string> ids, Func<string, CancellationToken, Task<(int Status, string Body)>> get, CancellationToken ct)
    {
        var distinct = ids.Where(id => Guid.TryParse(id, out _)).Select(id => Guid.Parse(id).ToString("D")).Distinct().Take(MaxIds).ToList();
        var answers = await Task.WhenAll(distinct.Select(async id =>
        {
            var (status, body) = await get($"/api/users/{id}/display-label", ct);
            return (Id: id, Label: status == 200 ? Label(body) : null);
        }));
        return answers.Where(a => !string.IsNullOrWhiteSpace(a.Label)).ToDictionary(a => a.Id, a => a.Label!, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Sets <c>name</c> on every <c>{ userId }</c> object whose id resolved and whose name is empty (a service name wins).</summary>
    public static void Fill(IReadOnlyDictionary<string, string> names, params JsonNode?[] roots)
    {
        void Walk(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject obj:
                    if (obj["userId"] is JsonValue user && user.TryGetValue<string>(out var userId)
                        && string.IsNullOrWhiteSpace((string?)obj["name"]) && names.TryGetValue(userId, out var name))
                    {
                        obj["name"] = name;
                    }

                    foreach (var (_, child) in obj.ToList()) Walk(child);
                    break;
                case JsonArray array:
                    foreach (var child in array) Walk(child);
                    break;
            }
        }

        foreach (var root in roots) Walk(root);
    }

    private static string? Label(string body)
    {
        try
        {
            // AuthService envelope { data: { userId, displayLabel, labelState } }; "Unnamed" users answer displayLabel null.
            var data = JsonNode.Parse(body)?["data"];
            return (string?)data?["labelState"] == "Named" ? (string?)data?["displayLabel"] : null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    // ── WP-MVP1-FU-CLEAN1 (FU-0177-PLAN-OWNER): the shared person PICKER ──────────────────────────────────────────────────────────

    /// <summary>Rows one picker search asks for (AuthService allows 1–50).</summary>
    public const int PickerLimit = 20;

    /// <summary>Longest search text sent (AuthService refuses more than 100).</summary>
    public const int MaxSearchLength = 100;

    /// <summary>
    /// The picker search: AuthService's existing <c>GET /api/users/lookup</c> (key <c>auth.users.lookup</c>, the same key as the label read;
    /// ACTIVE users of the caller's tenant only, label only — no e-mail, no roles). The path carries ONLY the search and the limit: the tenant
    /// is the caller's token's (the Web never names one), so a query string cannot widen the search to another tenant.
    /// </summary>
    public static string SearchPath(string? search)
    {
        var text = (search ?? string.Empty).Trim();
        if (text.Length > MaxSearchLength) text = text[..MaxSearchLength];
        return $"/api/users/lookup?search={Uri.EscapeDataString(text)}&limit={PickerLimit}";
    }

    /// <summary>The picker options of a lookup answer (envelope <c>{ data: [ { userId, displayLabel } ] }</c>): uuid ids with a label, distinct, in order.</summary>
    public static JsonArray Options(string body)
    {
        var items = new JsonArray();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (JsonNode.Parse(body)?["data"] is not JsonArray data) return items;
            foreach (var row in data.OfType<JsonObject>())
            {
                if (!Guid.TryParse((string?)row["userId"], out var id) || (string?)row["displayLabel"] is not { } label || string.IsNullOrWhiteSpace(label)) continue;
                if (seen.Add(id.ToString("D"))) items.Add(new JsonObject { ["value"] = id.ToString("D"), ["text"] = label.Trim() });
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return new JsonArray();
        }

        return items;
    }
}
