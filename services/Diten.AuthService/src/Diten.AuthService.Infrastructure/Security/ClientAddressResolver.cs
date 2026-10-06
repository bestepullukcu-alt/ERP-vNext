using System.Collections.Concurrent;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Infrastructure.Security;

/// <summary>
/// BL-529 FIX3 — WHO IS ASKING, for the rate limits of the anonymous password doors. The client is the connection's own
/// peer (<c>Connection.RemoteIpAddress</c>). <c>X-Forwarded-For</c> is read ONLY when that peer is a configured trusted
/// proxy (<c>ClientAddress:TrustedProxies</c>; appsettings.Development.json lists loopback), and then the standard
/// way: from the right, skipping trusted proxies, the first address that is not one of them. The leftmost entry — the one
/// a client writes itself — is never believed on its own, so a fresh header per request cannot buy a fresh allowance.
/// </summary>
public sealed class ClientAddressResolver
{
    public const string TrustedProxiesKey = "ClientAddress:TrustedProxies";

    private readonly HashSet<IPAddress> _trustedProxies;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<IPAddress, byte> _warnedPeers = new();

    /// <param name="logger">FIX5 — for the one-time warning about an untrusted peer that forwards for others.</param>
    public ClientAddressResolver(IEnumerable<IPAddress> trustedProxies, ILogger? logger = null)
    {
        _trustedProxies = trustedProxies.Select(Normalize).ToHashSet();
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// BL-529 FIX4/FIX5 — whether a trusted proxy is configured at all. Without one, every request's peer is the Web
    /// server or the gateway, never the person: a per-client limit keyed on it would be ONE bucket for everybody. The host
    /// warns once at start (<see cref="ClientAddressStartupWarning"/>). FIX5 — there is no Development fallback any more:
    /// appsettings.Development.json lists loopback itself.
    /// </summary>
    public bool IdentifiesClients => _trustedProxies.Count > 0;

    /// <summary>
    /// BL-529 FIX4 — reads and VALIDATES the configured list once, at registration: an entry that is not an IP address (a
    /// CIDR range, a typo) stops the start with a message naming it, instead of a 500 on the first password request. FIX5 —
    /// the entry must read back exactly as written: IPAddress.TryParse also accepts shorthand ("10.0.1" is 10.0.0.1),
    /// which would trust an address nobody meant.
    /// </summary>
    public static IReadOnlyList<IPAddress> ParseTrustedProxies(IConfiguration configuration)
    {
        var entries = configuration.GetSection(TrustedProxiesKey).Get<string[]>() ?? [];
        var parsed = new List<IPAddress>(entries.Length);
        foreach (var entry in entries)
        {
            var written = (entry ?? string.Empty).Trim();
            if (!IPAddress.TryParse(written, out var address)
                || !string.Equals(address.ToString(), written, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"{TrustedProxiesKey} contains '{entry}', which is not an IP address written in full. List each proxy's " +
                    "address on its own, in full (ranges such as CIDR and shorthand such as 10.0.1 are not supported).");
            }

            parsed.Add(address);
        }

        return parsed;
    }

    /// <summary>
    /// Who is asking. <see cref="ClientIdentity.Identified"/> is false when the client cannot be told apart from everyone
    /// else: no trusted proxy configured, or no peer address at all (FIX5 — "unknown" would be one shared key).
    /// </summary>
    public ClientIdentity Identify(HttpContext context)
    {
        var peer = context.Connection.RemoteIpAddress;
        if (peer is null || !IdentifiesClients)
        {
            return new ClientIdentity(peer is null ? "unknown" : Normalize(peer).ToString(), Identified: false);
        }

        return new ClientIdentity(Resolve(context), Identified: true);
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
            // FIX5 — a peer outside the list that forwards for others is a proxy nobody listed: every client behind it shares
            // its bucket. Said once per peer, so the list can be completed.
            if (_trustedProxies.Count > 0 && context.Request.Headers.ContainsKey("X-Forwarded-For")
                && _warnedPeers.TryAdd(Normalize(peer), 0))
            {
                _logger.LogWarning(
                    "A peer that is not in {Key} sent X-Forwarded-For; its clients share one rate-limit bucket. Add the proxy's " +
                    "address to the list if it is one of ours.",
                    TrustedProxiesKey);
            }

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

    private bool IsTrusted(IPAddress address) => _trustedProxies.Contains(Normalize(address));

    private static IPAddress Normalize(IPAddress address) => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
}

/// <summary>BL-529 FIX5 — the client key, and whether it tells this client apart from everyone else.</summary>
public sealed record ClientIdentity(string Key, bool Identified);

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
