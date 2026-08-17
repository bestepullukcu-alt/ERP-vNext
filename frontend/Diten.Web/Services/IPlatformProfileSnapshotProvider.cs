namespace Diten.Web.Services;

public interface IPlatformProfileSnapshotProvider
{
    /// <summary>
    /// Returns the current platform admin's profile snapshot for layout rendering.
    /// Source-of-truth is the Platform service <c>PlatformAdministrator</c> record (live API),
    /// not the JWT cookie. This eliminates stale display-name drift between profile/settings
    /// pages and other Platform shell pages after the user updates their display name.
    /// </summary>
    /// <remarks>
    /// Implementation MUST:
    /// 1) Cache per request (HttpContext.Items) so multiple layout fragments don't fan-out.
    /// 2) Cache per user (IMemoryCache, short TTL) so each navigation doesn't hammer the API.
    /// 3) Fall back to JWT claims on API failure so the page still renders.
    /// </remarks>
    Task<PlatformProfileSnapshot> GetCurrentAsync(CancellationToken ct = default);

    /// <summary>
    /// Invalidates the cached profile for the current user. Called after a successful
    /// PSS-009 Profile/Settings update so the next page render fetches fresh data.
    /// </summary>
    void InvalidateCurrent();
}
