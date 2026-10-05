using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

    public ClientAddressResolver(IEnumerable<IPAddress> trustedProxies, bool trustLoopback)
    {
        _trustedProxies = trustedProxies.Select(Normalize).ToHashSet();
        _trustLoopback = trustLoopback;
    }

    /// <summary>
    /// BL-529 FIX4 — whether this resolver can tell end users apart. Outside Development with no trusted proxy configured
    /// every request's peer is the Web server or the gateway, never the person: a per-client limit keyed on it would be ONE
    /// bucket for everybody (anyone could lock every user's door). The per-client limit is then switched off — the per-address
    /// limits still hold — and the host warns once at start (<see cref="ClientAddressStartupWarning"/>).
    /// </summary>
    public bool IdentifiesClients => _trustedProxies.Count > 0 || _trustLoopback;

    /// <summary>
    /// BL-529 FIX4 — reads and VALIDATES the configured list once, at registration: an entry that is not an IP address (a
    /// CIDR range, a typo) stops the start with a message naming it, instead of a 500 on the first password request.
    /// </summary>
    public static ClientAddressResolver FromConfiguration(IConfiguration configuration, IHostEnvironment environment)
    {
        var entries = configuration.GetSection(TrustedProxiesKey).Get<string[]>() ?? [];
        var parsed = new List<IPAddress>(entries.Length);
        foreach (var entry in entries)
        {
            if (!IPAddress.TryParse((entry ?? string.Empty).Trim(), out var address))
            {
                throw new InvalidOperationException(
                    $"{TrustedProxiesKey} contains '{entry}', which is not an IP address. List each proxy's address on its own " +
                    "(ranges such as CIDR are not supported).");
            }

            parsed.Add(address);
        }

        return new ClientAddressResolver(parsed, trustLoopback: environment.IsDevelopment() && parsed.Count == 0);
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

/// <summary>
/// BL-529 FIX4 — says ONCE, when the host starts, that the per-client limit of the anonymous password doors is off because
/// no trusted proxy is configured (see <see cref="ClientAddressResolver.IdentifiesClients"/>).
/// </summary>
public sealed class ClientAddressStartupWarning(ClientAddressResolver resolver, ILogger<ClientAddressStartupWarning> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!resolver.IdentifiesClients)
        {
            logger.LogWarning(
                "{Key} is empty: the per-client rate limit of the anonymous platform password doors is OFF (the per-address limits " +
                "still apply). List the Web server's and the gateway's addresses to switch it on.",
                ClientAddressResolver.TrustedProxiesKey);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
