using Diten.Web.Services.Auth;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Diten.Web.Tests.Auth;

/*
 * THE MECHANISM, MEASURED RATHER THAN REMEMBERED.
 *
 * Everything in this round rests on one claim about ASP.NET Core: that Response.Cookies.Delete(name) does not
 * merely add an expiry header, it REMOVES an earlier Append of the same cookie from the response headers. If
 * that were false, a proxy endpoint clearing cookies after the bridge refreshed them would leave both headers
 * and the browser would keep the fresh value; the whole "the refresh is thrown away" story would be wrong.
 *
 * So it is pinned here, against a real HttpResponse, before anything is built on top of it.
 */
public class CookieOverwriteMeasurementTests
{
    [Fact]
    public void Delete_removes_an_Append_that_happened_earlier_in_the_same_response()
    {
        var context = new DefaultHttpContext();

        context.Response.Cookies.Append(
            AuthTokenCookies.AccessTokenCookie,
            "FRESH-TOKEN",
            new CookieOptions { Path = "/" });

        var afterAppend = context.Response.Headers.SetCookie.ToString();
        Assert.Contains("FRESH-TOKEN", afterAppend);

        context.Response.Cookies.Delete(
            AuthTokenCookies.AccessTokenCookie,
            new CookieOptions { Path = "/" });

        var afterDelete = context.Response.Headers.SetCookie.ToString();

        /*
         * ⚠ THIS IS THE WHOLE DEFECT IN ONE ASSERTION. The freshly issued token is GONE from the response —
         * not shadowed, not ordered after, gone — and what remains is an expiry instruction. The browser is
         * told to forget the cookie it was about to be given, so the next request arrives with nothing and
         * the user is signed out for real.
         */
        Assert.DoesNotContain("FRESH-TOKEN", afterDelete);
        Assert.Contains("expires=Thu, 01 Jan 1970", afterDelete);
    }

    [Fact]
    public void The_same_holds_for_the_real_write_and_clear_helpers()
    {
        // Not a synthetic Append/Delete pair: the actual pair the bridge and the proxy endpoints use.
        var context = new DefaultHttpContext();
        var cookies = new AuthCookieService();

        cookies.WriteTokens(context.Response, "FRESH-ACCESS", "FRESH-REFRESH", DateTime.UtcNow.AddDays(7));
        Assert.Contains("FRESH-ACCESS", context.Response.Headers.SetCookie.ToString());

        AuthTokenCookies.ClearTokens(context.Response);

        Assert.DoesNotContain("FRESH-ACCESS", context.Response.Headers.SetCookie.ToString());
        Assert.DoesNotContain("FRESH-REFRESH", context.Response.Headers.SetCookie.ToString());
    }
}
