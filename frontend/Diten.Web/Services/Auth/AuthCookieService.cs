using Microsoft.AspNetCore.Http;

namespace Diten.Web.Services.Auth;

public sealed class AuthCookieService : IAuthCookieService
{
    public void WriteTokens(HttpResponse response, string accessToken, string refreshToken, DateTime refreshExpiresAtUtc)
    {
        ClearTokens(response);
        var cookieOptions = BuildCookieOptions(response, refreshExpiresAtUtc);

        AuthTokenCookies.Append(response, AuthTokenCookies.AccessTokenCookie, accessToken, cookieOptions);
        AuthTokenCookies.Append(response, AuthTokenCookies.RefreshTokenCookie, refreshToken, cookieOptions);
    }

    public void ClearTokens(HttpResponse response)
    {
        AuthTokenCookies.ClearTokens(response);
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

}
