using Microsoft.AspNetCore.Http;

namespace Diten.Web.Services.Auth;

public static class AuthTokenCookies
{
    public const string AccessTokenCookie = "access_token";
    public const string RefreshTokenCookie = "refresh_token";

    private const int MaxCookieChunkSize = 3800;
    private const int MaxChunksToDelete = 20;
    private static readonly string[] CookiePaths = ["/", "/account", "/Account", "/platform", "/Platform", "/api"];

    /*
     * ⚠ THE BUFFER IS CONSULTED BEFORE THE COOKIE, AND THE ORDER IS THE POINT. When TokenBridge refreshes
     * mid-request it writes new cookies to the RESPONSE; HttpRequest.Cookies still holds what the browser
     * sent, which is now the expired token. Reading the request first would hand every caller a token that
     * is known — right here, in this process, this millisecond — to be stale.
     *
     * HttpRequest carries HttpContext, so this is done without changing the signature: all 57 call sites
     * across 53 files get the fresh token without one of them being edited.
     *
     * ⚠ KNOWN GAP, STATED RATHER THAN HIDDEN: six places index Request.Cookies["access_token"] directly
     * instead of coming through here, and they still read the stale value. They also do not support chunked
     * cookies, which is a second defect in the same six lines. Measured and recorded as backlog, not fixed
     * in this round.
     */
    public static string? GetAccessToken(HttpRequest request)
        => RefreshedTokens.AccessToken(request.HttpContext) ?? GetChunkedCookie(request, AccessTokenCookie);

    public static string? GetRefreshToken(HttpRequest request)
        => RefreshedTokens.RefreshToken(request.HttpContext) ?? GetChunkedCookie(request, RefreshTokenCookie);

    public static bool TryGet(HttpRequest request, string cookieName, out string value)
    {
        var refreshed = cookieName switch
        {
            AccessTokenCookie => RefreshedTokens.AccessToken(request.HttpContext),
            RefreshTokenCookie => RefreshedTokens.RefreshToken(request.HttpContext),
            _ => null
        };

        value = refreshed ?? GetChunkedCookie(request, cookieName) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    public static void Append(HttpResponse response, string cookieName, string value, CookieOptions options)
    {
        if (value.Length <= MaxCookieChunkSize)
        {
            response.Cookies.Append(cookieName, value, options);
            return;
        }

        var chunkCount = (int)Math.Ceiling(value.Length / (double)MaxCookieChunkSize);
        response.Cookies.Append(cookieName, $"chunks-{chunkCount}", options);

        for (var index = 0; index < chunkCount; index++)
        {
            var chunk = value.Substring(
                index * MaxCookieChunkSize,
                Math.Min(MaxCookieChunkSize, value.Length - index * MaxCookieChunkSize));
            response.Cookies.Append($"{cookieName}C{index + 1}", chunk, options);
        }
    }

    public static void ClearTokens(HttpResponse response)
    {
        foreach (var path in CookiePaths)
        {
            Delete(response, AccessTokenCookie, path);
            Delete(response, RefreshTokenCookie, path);
        }
    }

    private static void Delete(HttpResponse response, string cookieName, string path)
    {
        var options = BuildDeleteOptions(response, path);
        response.Cookies.Delete(cookieName, options);

        for (var index = 1; index <= MaxChunksToDelete; index++)
        {
            response.Cookies.Delete($"{cookieName}C{index}", options);
        }
    }

    private static string? GetChunkedCookie(HttpRequest request, string cookieName)
    {
        if (!request.Cookies.TryGetValue(cookieName, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!value.StartsWith("chunks-", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (!int.TryParse(value["chunks-".Length..], out var chunkCount) || chunkCount <= 0)
        {
            return null;
        }

        var chunks = new string[chunkCount];
        for (var index = 1; index <= chunkCount; index++)
        {
            if (!request.Cookies.TryGetValue($"{cookieName}C{index}", out var chunk) || string.IsNullOrEmpty(chunk))
            {
                return null;
            }

            chunks[index - 1] = chunk;
        }

        return string.Concat(chunks);
    }

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
