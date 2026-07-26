namespace Diten.Platform.Application.Contracts;

/// <summary>
/// Resolves AuthService user ids to human display names (MOD-0024 pack §K6.4).
///
/// <para>Platform stores user IDENTITIES (a task's assignee, a position assignment's holder) but no names —
/// MOD-0288's <c>PersonReference</c> has no user id, so it cannot supply them either. This seam is the one place
/// that crosses to AuthService for them.</para>
///
/// <para>Two rules every implementation must honour:</para>
/// <list type="number">
/// <item><b>Batch, never per user.</b> Callers pass the whole set; an implementation that issues one request per
/// id turns a list of 50 people into 50 round trips.</item>
/// <item><b>Best effort — never throw.</b> A name is decoration on top of an id. If AuthService is unreachable
/// the caller must still return its rows with names simply absent, so an assignee picker degrades to
/// "position — unit" instead of failing outright.</item>
/// </list>
///
/// <para>An id missing from the result means "not resolved" (unknown, other tenant, or auth unreachable) and the
/// caller should omit the name rather than substitute the id — a GUID is never shown as a person.</para>
/// </summary>
public interface IUserDisplayNameResolver
{
    Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken ct = default);
}
