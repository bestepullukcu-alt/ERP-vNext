using Diten.Web.Controllers;
using Diten.Web.Services.Auth;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.Web.Tests.Auth;

/*
 * THE REFRESHED TOKEN HAS TO BE VISIBLE IN THE REQUEST THAT REFRESHED IT.
 *
 * WHY THIS FILE EXISTS. TokenBridge refreshes the token and writes the new value to HttpResponse.Cookies —
 * a header on the way OUT. HttpRequest.Cookies is what the browser sent on the way IN, and nothing in this
 * repository writes to it (measured 2026-08-27: zero occurrences). So the 57 call sites that read the token
 * spent the rest of that request using the value that had just been replaced, and the refresh only took
 * effect on the NEXT request.
 *
 * The codebase already knew, but had only solved it inside the bridge — see TokenBridgeTests, the test named
 * "Pass_2_does_not_undo_the_refresh_even_though_the_request_still_holds_the_old_token". That name is the
 * whole defect written down and left in place.
 */
public class RefreshedTokenVisibilityTests
{
    private const string Stale = "STALE-ACCESS-TOKEN";
    private const string Fresh = "FRESH-ACCESS-TOKEN";

    private static DefaultHttpContext RequestCarrying(string accessToken)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{AuthTokenCookies.AccessTokenCookie}={accessToken}";
        return context;
    }

    [Fact]
    public void Without_a_refresh_the_cookie_is_still_the_answer()
    {
        // Non-vacuity: the buffer must not shadow the normal path, or every test below would pass for the
        // wrong reason and the ordinary request would read nothing.
        var context = RequestCarrying(Stale);

        Assert.Equal(Stale, AuthTokenCookies.GetAccessToken(context.Request));
        Assert.False(RefreshedTokens.RefreshedInThisRequest(context));
    }

    [Fact]
    public void After_a_refresh_the_reader_returns_the_new_token_not_the_one_the_browser_sent()
    {
        /*
         * MUTATION GUARD: delete the RefreshedTokens.Record call in TokenBridge, or the buffer lookup in
         * AuthTokenCookies.GetAccessToken, and this goes red with the stale value. Note what the request
         * still holds — the OLD token, unchanged, exactly as in production. That is not incidental to the
         * test, it is the condition being tested.
         */
        var context = RequestCarrying(Stale);
        RefreshedTokens.Record(context, Fresh, "FRESH-REFRESH-TOKEN");

        Assert.Equal(Fresh, AuthTokenCookies.GetAccessToken(context.Request));
        Assert.Equal(Stale, context.Request.Cookies[AuthTokenCookies.AccessTokenCookie]);
    }

    [Fact]
    public void Every_accessor_agrees_after_a_refresh()
    {
        // TryGet is a separate entry point and was a separate way to read the stale value.
        var context = RequestCarrying(Stale);
        context.Request.Headers.Cookie =
            $"{AuthTokenCookies.AccessTokenCookie}={Stale}; {AuthTokenCookies.RefreshTokenCookie}=STALE-REFRESH";
        RefreshedTokens.Record(context, Fresh, "FRESH-REFRESH-TOKEN");

        Assert.Equal(Fresh, AuthTokenCookies.GetAccessToken(context.Request));
        Assert.Equal("FRESH-REFRESH-TOKEN", AuthTokenCookies.GetRefreshToken(context.Request));

        Assert.True(AuthTokenCookies.TryGet(context.Request, AuthTokenCookies.AccessTokenCookie, out var viaTryGet));
        Assert.Equal(Fresh, viaTryGet);
    }

    [Fact]
    public void A_cookie_that_is_not_a_token_is_untouched_by_the_buffer()
    {
        // TryGet takes a cookie NAME. The buffer must answer only for the two it actually holds.
        var context = RequestCarrying(Stale);
        context.Request.Headers.Cookie = $"{AuthTokenCookies.AccessTokenCookie}={Stale}; tenant_hint=acme";
        RefreshedTokens.Record(context, Fresh, "FRESH-REFRESH-TOKEN");

        Assert.True(AuthTokenCookies.TryGet(context.Request, "tenant_hint", out var hint));
        Assert.Equal("acme", hint);
    }

    // ── THE SECOND LINE OF DEFENCE ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_401_after_a_refresh_does_not_destroy_the_session()
    {
        /*
         * ⚠ THE LOGOUT BUG, IN ONE TEST. Fifteen proxy controllers clear the auth cookies on a 401. When the
         * bridge has just refreshed, they clear them on the SAME HttpResponse the new cookies were written
         * to — and Response.Cookies.Delete does not sit alongside the earlier Append, it REMOVES it (pinned
         * in CookieOverwriteMeasurementTests). So the browser never receives the new token, arrives at the
         * next request with nothing, and the user is signed out for real.
         *
         * MUTATION GUARD: remove the RefreshedInThisRequest check from ProxyAuthFailure.ClearAuthCookies and
         * this goes red — the fresh token disappears from the response headers.
         */
        var context = new DefaultHttpContext();
        new AuthCookieService().WriteTokens(context.Response, Fresh, "FRESH-REFRESH-TOKEN", DateTime.UtcNow.AddDays(7));
        RefreshedTokens.Record(context, Fresh, "FRESH-REFRESH-TOKEN");

        /*
         * ⚠ THE ASSERTION IS "NOTHING CHANGED", NOT "NO EXPIRY HEADERS EXIST". A freshly written response
         * ALREADY carries expiry headers: WriteTokens calls ClearTokens first, so the five non-root cookie
         * paths (/account, /platform, /api …) are deleted before the real cookie is appended at "/". Asserting
         * the absence of "expires=1970" therefore fails on a perfectly healthy response — it did, the first
         * time this test was written. Comparing the headers before and after is exact: the guard must be a
         * no-op, and that is provable without knowing which expiry headers are legitimate.
         */
        var before = context.Response.Headers.SetCookie.ToString();

        ProxyAuthFailure.ClearAuthCookies(context.Response);

        var after = context.Response.Headers.SetCookie.ToString();
        Assert.Equal(before, after);
        Assert.Contains(Fresh, after);
        Assert.Contains("FRESH-REFRESH-TOKEN", after);
    }

    [Fact]
    public void A_401_WITHOUT_a_refresh_still_ends_the_session()
    {
        /*
         * ⚠ THE OTHER HALF, AND IT MATTERS AS MUCH. If the guard above simply stopped clearing cookies, a
         * genuinely expired session would keep its cookies forever and the user would loop through failing
         * requests instead of being sent to sign in. The rule is not "never clear" — it is "a 401 that
         * follows a refresh is a different event from a 401 that does not".
         */
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"{AuthTokenCookies.AccessTokenCookie}={Stale}";

        ProxyAuthFailure.ClearAuthCookies(context.Response);

        Assert.Contains("expires=Thu, 01 Jan 1970", context.Response.Headers.SetCookie.ToString());
    }
}
