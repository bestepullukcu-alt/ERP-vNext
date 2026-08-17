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
        Diten.Web.Services.Auth.AuthTokenCookies.ClearTokens(response);
    }

    public static object PlatformLoginPayload() => new
    {
        success = false,
        redirectUrl = PlatformLoginPath,
        errors = new[] { "Authentication is no longer valid. Please sign in again." }
    };

}
