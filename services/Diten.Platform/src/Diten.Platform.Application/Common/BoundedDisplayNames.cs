using Diten.Platform.Application.Contracts;

namespace Diten.Platform.Application.Common;

/// <summary>
/// ATT-FIX1 / ATT-FIX2 — the ONE bounded way to ask AuthService for names, for every read and write that shows a
/// person: names are decoration on an answer that must still come. An AuthService that accepts the connection and
/// then hangs (the "service started before mongod" state, 30 s per query) costs a caller at most <see cref="Timeout"/>;
/// past it the caller answers without names ("unknown user"), exactly as when AuthService is down.
///
/// <para>Bounded twice: a linked token cancels the request, and <c>Task.WhenAny</c> stops waiting even for a resolver
/// that ignores its token. An empty id is never asked about; nothing to ask asks nothing.</para>
/// </summary>
public static class BoundedDisplayNames
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public static async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        IUserDisplayNameResolver resolver, IEnumerable<Guid> userIds, CancellationToken ct)
        => (await BoundAsync(userIds, (wanted, token) => resolver.ResolveAsync(wanted, token), ct))?.Value
           ?? new Dictionary<Guid, string>();

    /// <summary>The checked form: a timeout or a failure is an INCOMPLETE answer (never a complete one with gaps).</summary>
    public static async Task<DisplayNameResolution> ResolveCheckedAsync(
        IUserDisplayNameChecker checker, IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var wanted = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (wanted.Count == 0) { return new DisplayNameResolution(new Dictionary<Guid, string>(), Complete: true); }
        return (await BoundAsync(wanted, (ids, token) => checker.ResolveCheckedAsync(ids, token), ct))?.Value
               ?? new DisplayNameResolution(new Dictionary<Guid, string>(), Complete: false);
    }

    /// <summary>Null when the call timed out or failed; otherwise the answer.</summary>
    private static async Task<Answer<T>?> BoundAsync<T>(
        IEnumerable<Guid> userIds, Func<IReadOnlyCollection<Guid>, CancellationToken, Task<T>> ask, CancellationToken ct)
    {
        var wanted = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (wanted.Count == 0) { return null; }

        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bounded.CancelAfter(Timeout);
        var asking = ask(wanted, bounded.Token);
        var finished = await Task.WhenAny(asking, Task.Delay(Timeout, ct));
        ct.ThrowIfCancellationRequested();
        if (finished != asking)
        {
            bounded.Cancel();
            _ = asking.ContinueWith(t => _ = t.Exception, TaskScheduler.Default);   // observed, never thrown
            return null;
        }

        try
        {
            return new Answer<T>(await asking);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return null;
        }
    }

    private sealed record Answer<T>(T Value);
}
