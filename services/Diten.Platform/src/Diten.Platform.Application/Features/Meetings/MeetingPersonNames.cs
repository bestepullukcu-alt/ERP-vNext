using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Features.Meetings;

/// <summary>One person a meeting record names, with the name the tenant's directory gives them (null = not resolved).</summary>
public sealed record MeetingPersonNameDto(Guid UserId, string? DisplayName);

/// <summary>
/// BL-531 (WP-MEETINGS-ATTENDEE-SEARCH-01) — the people a meeting / series / report row ALREADY names come back with
/// their names in that same read, so a screen never downloads the whole directory just to put a name on an id.
///
/// <para>One batched call per read through <see cref="IUserDisplayNameResolver"/> (never one per row); the resolver
/// takes the tenant from the server-side context and AuthService scopes it again, so another tenant's user never
/// comes back as a name. An id it cannot resolve stays null — the screen says "unknown user", never the id.</para>
/// </summary>
public static class MeetingPersonNames
{
    /// <summary>ATT-FIX1 — names are decoration on a read that must still answer: an AuthService that accepts the
    /// connection and then hangs (the "service started before mongod" state, 30 s per query) must not hold the
    /// meeting, the minutes, the list, the report or the series form for 30–100 s. Past this, the read answers
    /// without names ("unknown user"), exactly as when AuthService is down.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public static async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        IUserDisplayNameResolver resolver, IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var wanted = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (wanted.Count == 0) { return new Dictionary<Guid, string>(); }

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bounded.CancelAfter(Timeout);
        var resolving = resolver.ResolveAsync(wanted, bounded.Token);
        // WhenAny as well as the token: a resolver that ignores its token still cannot hold the read.
        var finished = await Task.WhenAny(resolving, Task.Delay(Timeout, ct));
        ct.ThrowIfCancellationRequested();
        if (finished != resolving)
        {
            bounded.Cancel();
            _ = resolving.ContinueWith(t => _ = t.Exception, TaskScheduler.Default);   // observed, never thrown
            return new Dictionary<Guid, string>();
        }

        try
        {
            return await resolving;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return new Dictionary<Guid, string>();   // best effort by contract: no names, the read still answers
        }
    }

    public static string? NameOf(IReadOnlyDictionary<Guid, string> names, Guid userId) =>
        names.TryGetValue(userId, out var name) ? name : null;
}
