using Microsoft.AspNetCore.Http;

namespace Diten.Web.Services.Auth;

/// <summary>
/// WP-VW-W6 W6-FIX-a (G-13) — the way back through the sign-in page. An e-mail link (an approval link, an order, a visit
/// report) names its tenant (<c>?tenantId=</c>); opened signed out, the cookie challenge sends the reader to the sign-in
/// page, which must sign in to THAT tenant (login.js reads <c>?tenantId=</c>), or the reader lands in the wrong one. After
/// sign-in the reader returns to the page asked for — only ever a page of this site (no open redirect).
/// </summary>
public static class LoginReturn
{
    /// <summary>
    /// The sign-in address with the request's <c>tenantId</c> carried over — only a single, valid, non-empty guid (written
    /// in its "D" form, so nothing of the raw query travels); otherwise the address unchanged (today's behaviour).
    /// </summary>
    public static string WithTenant(string redirectUri, HttpRequest request)
    {
        if (!request.Query.TryGetValue("tenantId", out var values) || values.Count != 1
            || !Guid.TryParse(values[0], out var tenantId) || tenantId == Guid.Empty)
        {
            return redirectUri;
        }

        return redirectUri + (redirectUri.Contains('?') ? "&" : "?") + "tenantId=" + tenantId.ToString("D");
    }

    /// <summary>
    /// A page of this site: a path starting with one "/" — never "//" or "/\" (a browser reads both as another host) — and
    /// no control characters. An absolute address, a scheme or a protocol-relative one is not.
    /// </summary>
    public static bool IsLocal(string? url)
        => !string.IsNullOrWhiteSpace(url) && !url.Any(char.IsControl)
           && url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));
}
