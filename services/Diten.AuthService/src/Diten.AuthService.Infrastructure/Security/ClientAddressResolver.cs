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

    /// <summary>BL-529 FIX6 — how many distinct peers are warned about; past it, one "limit reached" line and silence.</summary>
    public const int MaxWarnedPeers = 256;

    private readonly HashSet<IPAddress> _trustedProxies;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<IPAddress, byte> _warnedPeers = new();
    private int _warnLimitReported;

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
    /// which would trust an address nobody meant. FIX6 — "as written" is the address's canonical form (IPv6 compressed,
    /// e.g. "::ffff:10.0.0.1"), which the message now says.
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
                    $"{TrustedProxiesKey} contains '{entry}', which is not an IP address in its canonical form. List each " +
                    "proxy's address on its own, written as the address reads back (IPv4 as four numbers, IPv6 compressed); " +
                    "ranges such as CIDR and shorthand such as 10.0.1 are not supported.");
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
            // its bucket. Said once per peer, so the list can be completed. FIX6 — the line names the peer, and the record of
            // warned peers is capped (anyone can send the header).
            if (_trustedProxies.Count > 0 && context.Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                WarnAboutUnlistedForwarder(Normalize(peer));
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

    /// <summary>FIX6 — how many peers have been warned about (never more than <see cref="MaxWarnedPeers"/>).</summary>
    public int WarnedPeerCount => _warnedPeers.Count;

    /// <summary>FIX7 item 3 — TEST SEAM: runs inside the lock, after a new peer was found to fit and before it is added, so a
    /// test can hold one thread there and see whether another gets in (it must not). Never set in production.</summary>
    internal Action? AfterRoomFoundUnderLock { get; set; }

    private void WarnAboutUnlistedForwarder(IPAddress peer)
    {
        // FIX7 — no unlocked "already named?" shortcut: the one check is the one under the lock (an unlisted forwarder is
        // rare; the lock costs nothing that matters, and two checks hid each other from the tests).
        bool named = false;
        bool limitJustReached = false;
        lock (_warnedPeers)
        {
            // FIX7 item 3 — the limit line is for a NEW peer that cannot be added. A peer already named (another thread named
            // it a moment ago) is neither named again nor counted as "past the limit": at 255 two requests from the same
            // unlisted peer used to spend the one limit line while only 256 peers had been seen.
            if (_warnedPeers.ContainsKey(peer))
            {
                return;
            }

            if (_warnedPeers.Count < MaxWarnedPeers)
            {
                AfterRoomFoundUnderLock?.Invoke();
                named = _warnedPeers.TryAdd(peer, 0);
            }
            else if (_warnLimitReported == 0)
            {
                _warnLimitReported = 1;
                limitJustReached = true;
            }
        }

        if (named)
        {
            _logger.LogWarning(
                "Peer {Peer} is not in {Key} but sent X-Forwarded-For; its clients share one rate-limit bucket. Add that " +
                "address to the list if it is one of our proxies.",
                peer.ToString(), TrustedProxiesKey);
        }
        else if (limitJustReached)
        {
            _logger.LogWarning(
                "More than {Max} peers outside {Key} sent X-Forwarded-For; no further peers are named until Auth restarts.",
                MaxWarnedPeers, TrustedProxiesKey);
        }
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
