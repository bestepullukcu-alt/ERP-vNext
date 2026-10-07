using System.Net;

namespace Diten.Platform.Infrastructure.Services;

/// <summary>
/// BL-454 slice 2 stage D — the address in the tenant administrator's invitation: the frontend's set-password page,
/// where BL-529's door (api/users/set-password) redeems the one-time token. Its root comes from configuration
/// (<c>AuthService:FrontendBaseUrl</c>), whose default is <c>http://localhost:5001</c> (slice 1 N2). Outside Development a
/// missing or loopback root is REFUSED, by name: a mail whose button points at the reader's own machine is worse than
/// none, because the administrator cannot get in and nobody is told why.
/// </summary>
public static class TenantAdminSetPasswordLink
{
    public const string ReasonRootMissing = "INVITE_LINK_ROOT_MISSING";
    public const string ReasonRootLoopback = "INVITE_LINK_ROOT_LOOPBACK";

    /// <summary>The link, or the named reason no link may be sent from this server.</summary>
    public static (string? Url, string? RefusalCode) Build(string? frontendBaseUrl, bool isDevelopment, string email, string token)
    {
        var root = (frontendBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (!Uri.TryCreate(root, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return (null, ReasonRootMissing);
        }

        if (!isDevelopment && IsLoopback(uri))
        {
            return (null, ReasonRootLoopback);
        }

        return ($"{root}/account/set-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}", null);
    }

    private static bool IsLoopback(Uri uri)
    {
        if (uri.IsLoopback
            || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address);
    }
}
