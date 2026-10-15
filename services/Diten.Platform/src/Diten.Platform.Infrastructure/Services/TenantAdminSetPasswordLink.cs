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

    /// <summary>BL-454 stage D FIX1 K2 — outside Development the link must be https: a set-password token over plain http
    /// can be read on the way.</summary>
    public const string ReasonRootNotHttps = "INVITE_LINK_ROOT_NOT_HTTPS";

    /// <summary>
    /// Whether a link may be sent from this server at all — asked BEFORE AuthService is (FIX1 2): a refused root must not
    /// cost an existing administrator their password and sessions for a mail that will not leave.
    /// </summary>
    public static string? RefusalFor(string? frontendBaseUrl, bool isDevelopment)
    {
        var root = (frontendBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (!Uri.TryCreate(root, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return ReasonRootMissing;
        }

        if (isDevelopment)
        {
            return null;
        }

        if (IsLoopback(uri))
        {
            return ReasonRootLoopback;
        }

        return uri.Scheme == Uri.UriSchemeHttps ? null : ReasonRootNotHttps;
    }

    /// <summary>The link, or the named reason no link may be sent from this server.</summary>
    public static (string? Url, string? RefusalCode) Build(string? frontendBaseUrl, bool isDevelopment, string email, string token)
    {
        if (RefusalFor(frontendBaseUrl, isDevelopment) is { } refusal)
        {
            return (null, refusal);
        }

        var root = frontendBaseUrl!.Trim().TrimEnd('/');
        return ($"{root}/account/set-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}", null);
    }

    private static bool IsLoopback(Uri uri)
    {
        // K2 — "localhost." (the fully qualified spelling, trailing dot) is the same machine.
        var host = uri.Host.TrimEnd('.');
        if (uri.IsLoopback
            || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // K11 — the unspecified addresses (0.0.0.0, ::) are no public address either.
        return IPAddress.TryParse(host.Trim('[', ']'), out var address)
               && (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any));
    }
}
