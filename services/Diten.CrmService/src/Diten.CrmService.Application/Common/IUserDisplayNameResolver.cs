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

/// <summary>Default seam used by tests and non-HTTP hosts: resolves nothing, so provenance names stay absent.</summary>
public sealed class NullUserDisplayNameResolver : IUserDisplayNameResolver
{
    public Task<IReadOnlyDictionary<Guid, string>> ResolveAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
}
