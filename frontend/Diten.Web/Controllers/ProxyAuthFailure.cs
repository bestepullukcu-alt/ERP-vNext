using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diten.Web.Controllers;

public static class ProxyAuthFailure
{
    public const string PlatformLoginPath = "/platform/login";

    public static bool IsAuthFailure(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.Unauthorized;

    public static bool IsForbidden(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.Forbidden;

    /// <summary>
    /// Ends the session by clearing the auth cookies — UNLESS this request refreshed them.
    ///
    /// <para>⚠ TWO 401s THAT LOOK IDENTICAL AND MEAN OPPOSITE THINGS. Normally a 401 from downstream means
    /// the session is over and the cookies should go. But when TokenBridge refreshed mid-request, a 401 means
    /// something else entirely: a caller went out carrying the token that had just been replaced. The session
    /// is fine. Clearing here destroys it — and destroys it on the SAME HttpResponse the new cookies were
    /// just written to.</para>
    ///
    /// <para>⚠ AND THAT IS NOT A RACE OR AN ORDERING SUBTLETY, IT IS TOTAL. Measured 2026-08-27 against a
    /// real HttpResponse: <c>Cookies.Delete</c> does not add an expiry alongside the earlier
    /// <c>Cookies.Append</c>, it REMOVES the Append from the response headers. So the freshly issued token
    /// never reaches the browser at all; the next request arrives with nothing, and the user is signed out
    /// for real. Fifteen proxy controllers call this, and every one of them could throw away a good refresh
    /// — which is the most likely explanation for the long-running "the data pages log me out" reports.</para>
    ///
    /// <para>The primary fix is that a refreshed request no longer sends the stale token downstream at all,
    /// so this 401 should not occur. This is the second line: if it occurs anyway, the session survives.</para>
    /// </summary>
    public static void ClearAuthCookies(HttpResponse response)
    {
        if (Diten.Web.Services.Auth.RefreshedTokens.RefreshedInThisRequest(response.HttpContext))
        {
            /*
             * Loud on purpose. Reaching here means the refresh worked but something downstream was still
             * called with the old token — a consumer that does not read through AuthTokenCookies, or a token
             * captured before the bridge ran. The session is kept; the fact that it happened must not be.
             */
            // ⚠ RequestServices CAN BE NULL — not a theoretical worry: a DefaultHttpContext has none, and
            // this method must never be the thing that throws. Keeping the session is the job; logging is
            // the commentary, and commentary that can crash the request is worse than no commentary.
            response.HttpContext.RequestServices?
                .GetService<ILoggerFactory>()
                ?.CreateLogger(typeof(ProxyAuthFailure))
                .LogWarning(
                    "Downstream returned 401 AFTER this request refreshed its token. Auth cookies were NOT "
                    + "cleared — the session is valid and clearing them here would discard the refresh that "
                    + "was written to this same response. Path {Path}.",
                    response.HttpContext.Request.Path.Value);

            return;
        }

        Diten.Web.Services.Auth.AuthTokenCookies.ClearTokens(response);
    }

    public static object PlatformLoginPayload() => new
    {
        success = false,
        redirectUrl = PlatformLoginPath,
        errors = new[] { "Authentication is no longer valid. Please sign in again." }
    };

}
