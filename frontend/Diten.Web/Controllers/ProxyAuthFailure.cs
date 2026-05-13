using System.Net;

namespace Diten.Web.Controllers;

public static class ProxyAuthFailure
{
    public const string PlatformLoginPath = "/platform/login";

    public static bool IsAuthFailure(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

    public static void ClearAuthCookies(HttpResponse response)
    {
        response.Cookies.Delete("access_token");
        response.Cookies.Delete("refresh_token");
    }

    public static object PlatformLoginPayload() => new
    {
        success = false,
        redirectUrl = PlatformLoginPath,
        errors = new[] { "Authentication is no longer valid. Please sign in again." }
    };
}
