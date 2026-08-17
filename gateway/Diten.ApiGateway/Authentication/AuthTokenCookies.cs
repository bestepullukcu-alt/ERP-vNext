namespace Diten.ApiGateway.Authentication;

public static class AuthTokenCookies
{
    private const string AccessTokenCookie = "access_token";

    public static string? GetAccessToken(HttpRequest request) => GetChunkedCookie(request, AccessTokenCookie);

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
}
