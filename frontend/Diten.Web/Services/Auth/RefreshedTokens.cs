using Microsoft.AspNetCore.Http;

namespace Diten.Web.Services.Auth;

/// <summary>
/// The tokens <see cref="TokenBridge"/> obtained DURING this request, carried where the rest of the request
/// can actually reach them.
///
/// ⚠ WHY THIS EXISTS — THE SHAPE OF THE BUG. When the bridge refreshes, it writes the new tokens to
/// <c>HttpResponse.Cookies</c>. That is a header on the way OUT; it does not change
/// <c>HttpRequest.Cookies</c>, which is a parsed snapshot of what the browser sent on the way IN. Everything
/// downstream — 57 call sites across 53 files — reads the request. So for the whole of the request in which
/// the refresh happened, every consumer read the token that had just been replaced. Measured 2026-08-27:
/// there is not one line in this repository that writes to <c>Request.Cookies</c>, and there was no other
/// route by which the new value could be seen. The refresh was invisible until the NEXT request.
///
/// <para><b>Why HttpContext.Items and not something cleverer.</b> Two designs were considered:</para>
/// <list type="bullet">
///   <item>Replacing <c>Request.Cookies</c> with a wrapping <c>IRequestCookieCollection</c>. It would fix
///   every reader including the six that index <c>Request.Cookies["access_token"]</c> directly — but it
///   makes a request lie about what the browser sent, which is a thing many other people read for other
///   reasons, and there is no precedent for it anywhere in this codebase.</item>
///   <item>This: a per-request buffer, consulted by the ONE accessor everybody already goes through.</item>
/// </list>
/// <para>The second was chosen because <c>HttpRequest</c> exposes <c>HttpContext</c>, so
/// <see cref="AuthTokenCookies.GetAccessToken"/> can consult the buffer without changing its signature —
/// 57 call sites fixed by one edit, and none of them touched. The cost is honest and stated: a reader that
/// bypasses <c>AuthTokenCookies</c> and indexes the cookie collection itself still sees the stale value.
/// Six places do that today; they are measured and recorded in the backlog rather than quietly left.</para>
///
/// <para>Items is the right lifetime: it is per-request, it is already how the bridge hands the refreshed
/// <c>ClaimsPrincipal</c> downstream, and it cannot leak into another request.</para>
/// </summary>
public static class RefreshedTokens
{
    private const string AccessTokenKey = "Diten.Auth.RefreshedAccessToken";
    private const string RefreshTokenKey = "Diten.Auth.RefreshedRefreshToken";

    /// <summary>
    /// Records the tokens obtained by a refresh in this request. Call it wherever the new cookies are
    /// written, and at the same moment — the two must never disagree.
    /// </summary>
    public static void Record(HttpContext context, string accessToken, string refreshToken)
    {
        context.Items[AccessTokenKey] = accessToken;
        context.Items[RefreshTokenKey] = refreshToken;
    }

    /// <summary>The access token this request refreshed, or null if it did not refresh.</summary>
    public static string? AccessToken(HttpContext? context)
        => context?.Items.TryGetValue(AccessTokenKey, out var value) == true ? value as string : null;

    /// <summary>The refresh token this request refreshed, or null if it did not refresh.</summary>
    public static string? RefreshToken(HttpContext? context)
        => context?.Items.TryGetValue(RefreshTokenKey, out var value) == true ? value as string : null;

    /// <summary>
    /// Whether a refresh happened in THIS request.
    ///
    /// ⚠ THIS IS WHAT SEPARATES TWO 401s THAT LOOK IDENTICAL. A 401 from downstream normally means "the
    /// session is over, clear the cookies". A 401 that arrives AFTER a refresh in the same request means
    /// something completely different: a caller went out with the token that had just been replaced. Clearing
    /// cookies on that one destroys a session that is perfectly valid — and destroys it on the same response
    /// the new cookies were written to, so the refresh never reaches the browser at all.
    /// </summary>
    public static bool RefreshedInThisRequest(HttpContext? context) => AccessToken(context) is not null;
}
