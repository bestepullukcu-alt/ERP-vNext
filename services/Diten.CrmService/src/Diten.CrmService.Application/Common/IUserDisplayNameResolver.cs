namespace Diten.CrmService.Application.Common;

/// <summary>
/// Resolves a set of user ids to their DISPLAY NAMES only (WP-SEG-DETAILS6). The single question is "what is this
/// person called" — never their email, role or any other profile field. The implementation is the S2S seam onto
/// AuthService's <c>internal/users/display-names</c> endpoint; it is tenant-scoped server-side and FAIL-CLOSED: any
/// id it cannot resolve is simply absent from the result, so a caller shows a date without a name rather than a raw id.
/// <para>Contract: ONE bulk call for the whole id set. A caller collects its distinct, non-empty ids and asks once.</para>
/// </summary>
public interface IUserDisplayNameResolver
{
    /// <summary>Maps the supplied user ids to display names. Unknown / unreachable / cross-tenant ids are omitted from
    /// the dictionary (never fabricated). Empty input returns an empty map without any network call.</summary>
    Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// WP-VW-W3-FIX (F5) — the <c>displayNames</c> map of a read DTO (id "D" → name), as the objection response detail
/// builds it: the distinct GUID-shaped ids asked in ONE call; null when there is no resolver, no id or the call fails
/// (fail-open — a name is display only; the screen falls back to a short id).
/// </summary>
public static class UserDisplayNames
{
    public static async Task<IReadOnlyDictionary<string, string>?> MapAsync(
        IUserDisplayNameResolver? resolver, IEnumerable<string?> ids, CancellationToken ct)
    {
        var guids = ids.Select(v => Guid.TryParse(v, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct().ToList();
        if (resolver is null || guids.Count == 0)
        {
            return null;
        }

        try
        {
            var resolved = await resolver.ResolveAsync(guids, ct);
            return resolved.ToDictionary(kv => kv.Key.ToString("D"), kv => kv.Value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }
}

/// <summary>Default seam used by tests and non-HTTP hosts: resolves nothing, so provenance names stay absent.</summary>
public sealed class NullUserDisplayNameResolver : IUserDisplayNameResolver
{
    public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
}
