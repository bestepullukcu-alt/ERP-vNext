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
    public static async Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        IUserDisplayNameResolver resolver, IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var wanted = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        return wanted.Count == 0 ? new Dictionary<Guid, string>() : await resolver.ResolveAsync(wanted, ct);
    }

    public static string? NameOf(IReadOnlyDictionary<Guid, string> names, Guid userId) =>
        names.TryGetValue(userId, out var name) ? name : null;
}
