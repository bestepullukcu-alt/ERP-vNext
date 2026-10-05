using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Diten.AuthService.Infrastructure.Security;

/// <summary>
/// BL-529 FIX3 — WHO IS ASKING, for the rate limits of the anonymous password doors. The client is the connection's own
/// peer (<c>Connection.RemoteIpAddress</c>). <c>X-Forwarded-For</c> is read ONLY when that peer is a configured trusted
/// proxy (<c>ClientAddress:TrustedProxies</c>; in Development, loopback when none is configured), and then the standard
/// way: from the right, skipping trusted proxies, the first address that is not one of them. The leftmost entry — the one
/// a client writes itself — is never believed on its own, so a fresh header per request cannot buy a fresh allowance.
/// </summary>
public sealed class ClientAddressResolver
{
    public const string TrustedProxiesKey = "ClientAddress:TrustedProxies";

    private readonly HashSet<IPAddress> _trustedProxies;
    private readonly bool _trustLoopback;

    public ClientAddressResolver(IConfiguration configuration, IHostEnvironment environment)
        : this(
            (configuration.GetSection(TrustedProxiesKey).Get<string[]>() ?? []).Select(IPAddress.Parse),
            trustLoopback: environment.IsDevelopment()
                           && (configuration.GetSection(TrustedProxiesKey).Get<string[]>() ?? []).Length == 0)
    {
    }

    public ClientAddressResolver(IEnumerable<IPAddress> trustedProxies, bool trustLoopback)
    {
        _trustedProxies = trustedProxies.Select(Normalize).ToHashSet();
        _trustLoopback = trustLoopback;
    }

    public string Resolve(HttpContext context)
    {
        var peer = context.Connection.RemoteIpAddress;
        if (peer is null)
        {
            return "unknown";
        }

        if (!IsTrusted(peer))
        {
            return Normalize(peer).ToString();
        }

        // The peer is a proxy we trust: walk the forwarded chain from the right, past every trusted proxy.
        var chain = context.Request.Headers["X-Forwarded-For"]
            .SelectMany(value => (value ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToArray();
        for (var i = chain.Length - 1; i >= 0; i--)
        {
            if (!IPAddress.TryParse(chain[i], out var hop))
            {
                return "invalid"; // an unparsable hop below the trusted ones: one shared key, never a fresh one per value
            }

            if (!IsTrusted(hop))
            {
                return Normalize(hop).ToString();
            }
        }

        return Normalize(peer).ToString(); // only trusted proxies on the way: the request came from them
    }

    private bool IsTrusted(IPAddress address)
    {
        var normalized = Normalize(address);
        return _trustedProxies.Contains(normalized) || (_trustLoopback && IPAddress.IsLoopback(normalized));
    }

    private static IPAddress Normalize(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}
