using System.Net;

namespace Diten.Web.Controllers;

public static class ProxyAuthFailure
{
    public const string PlatformLoginPath = "/platform/login";

    public static bool IsAuthFailure(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.Unauthorized;

    public static bool IsForbidden(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.Forbidden;

    public static void ClearAuthCookies(HttpResponse response)
    {
        foreach (var path in new[] { "/", "/account", "/Account", "/platform", "/Platform", "/api" })
        {
            response.Cookies.Delete("access_token", BuildDeleteOptions(response, path));
            response.Cookies.Delete("refresh_token", BuildDeleteOptions(response, path));
        }
    }

    public static object PlatformLoginPayload() => new
    {
        success = false,
        redirectUrl = PlatformLoginPath,
        errors = new[] { "Authentication is no longer valid. Please sign in again." }
    };

    private static CookieOptions BuildDeleteOptions(HttpResponse response, string path)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = response.HttpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = path
        };
    }
}
