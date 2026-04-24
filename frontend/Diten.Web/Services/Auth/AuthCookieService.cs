using Microsoft.AspNetCore.Http;

namespace Diten.Web.Services.Auth;

public sealed class AuthCookieService : IAuthCookieService
{
    private const string AccessTokenCookie = "access_token";
    private const string RefreshTokenCookie = "refresh_token";

    public void WriteTokens(HttpResponse response, string accessToken, string refreshToken, DateTime refreshExpiresAtUtc)
    {
        var cookieOptions = BuildCookieOptions(response, refreshExpiresAtUtc);

        response.Cookies.Append(AccessTokenCookie, accessToken, cookieOptions);
        response.Cookies.Append(RefreshTokenCookie, refreshToken, cookieOptions);
    }

    public void ClearTokens(HttpResponse response)
    {
        response.Cookies.Delete(AccessTokenCookie, BuildDeleteOptions(response));
        response.Cookies.Delete(RefreshTokenCookie, BuildDeleteOptions(response));
    }

    private static CookieOptions BuildCookieOptions(HttpResponse response, DateTime refreshExpiresAtUtc)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = response.HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = new DateTimeOffset(DateTime.SpecifyKind(refreshExpiresAtUtc, DateTimeKind.Utc))
        };
    }

    private static CookieOptions BuildDeleteOptions(HttpResponse response)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = response.HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        };
    }
}
