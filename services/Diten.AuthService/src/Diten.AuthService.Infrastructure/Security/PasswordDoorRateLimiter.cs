using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

namespace Diten.AuthService.Infrastructure.Security;

/// <summary>
/// BL-529 FIX2 — THE ANONYMOUS PLATFORM PASSWORD DOORS ARE RATE-LIMITED: <c>POST api/platform-auth/forgot-password</c>
/// and <c>POST api/platform-auth/reset-password</c> (the set-password link). Both open without a tenant since BL-529, and
/// both can be asked by anyone, endlessly — a flood of "forgot password" against one administrator is also how a stale
/// write would be aimed at a reset.
/// <para>Fixed windows. The client is <see cref="ClientAddressResolver"/>'s answer (never a header the client wrote on its
/// own), and the per-client counts apply only while clients can be told apart (<see cref="ClientIdentity.Identified"/>).
/// "Forgot password" counts per client and per e-mail address. The set-password link counts ONLY attempts whose link did
/// not match: per client and per (client, address) when clients are told apart, otherwise per address bucket (FIX6) — see
/// the two methods. Every key is a SHA-256, never stored or logged in clear. The ASP.NET rate-limiting middleware is not
/// used because its partition key cannot read the request body; the same <see cref="PartitionedRateLimiter"/> primitives
/// are used here instead.</para>
/// </summary>
public sealed class PasswordDoorRateLimiter : IDisposable
{
    /// <summary>The stable code a refused request carries (429).</summary>
    public const string TooManyRequestsCode = Application.Common.AuthRefusalCodes.TooManyRequests;

    public const int DefaultPerAddressLimit = 5;
    public const int DefaultPerClientLimit = 30;
    // WP-USERS-ROLES-CLOSE-FIX1 FIX3 items 2–3 — the sign-in doors per client: higher than the link doors' 30, because one
    // office behind one NAT address is one "client" here; the per-ACCOUNT lockout (5) still caps the guesses at an account.
    // FIX4 item 4 — counted BEFORE verification, every attempt (a success too): high, because an office behind one NAT
    // address is one client and its colleagues sign in all morning (FIX3's 100 locked such an office out).
    public const int DefaultLoginPerClientLimit = 300;
    // FIX4 item 4 — only the WRONG attempts, per client, counted AFTER verification: the guessing brake (30 per window; the
    // per-account lockout caps any one account at 5 on top).
    public const int DefaultLoginFailuresPerClientLimit = 30;
    // When clients CANNOT be told apart there is no per-address count on the sign-in doors (a junk request for a victim's
    // address would lock the victim out). FIX4 item 1 — the coarse ceiling is per TENANT on the tenant doors (FIX3's one
    // bucket for everybody let ~5 junk requests a second close every tenant's sign-in), per door on the platform door: 300
    // a minute lets a 3,000-person tenant sign in within ten minutes behind a single proxy; the CPU is guarded separately
    // (PasswordWorkGate, FIX5 addendum), so one tenant's flood takes its own minute, not the others'.
    public const int DefaultUnidentifiedPerMinute = 300;
    public static readonly TimeSpan UnidentifiedWindow = TimeSpan.FromMinutes(1);
    // FIX4 item 1 — the set-password link doors carry no tenant before the store is read (the link resolves it), so without
    // client identity they count per ADDRESS bucket — but 60 a minute, not FIX2's 5 a quarter-hour: holding one person's link
    // shut takes a request a second, sustained, while a flood of random addresses spreads over 4096 buckets.
    public const int DefaultUnidentifiedLinkPerMinute = 60;
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(15);

    /// <summary>BL-529 FIX6 — how many per-address buckets the set-password link has while clients cannot be told apart.</summary>
    public const int UnidentifiedLinkBuckets = 4096;

    private readonly PartitionedRateLimiter<string> _perAddress;
    private readonly PartitionedRateLimiter<string> _perClient;
    private readonly PartitionedRateLimiter<string> _loginPerClient;
    // FIX5 addendum (small item) — the wrong-attempt count keeps its window's start, so a refusal says the time LEFT.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, FailureWindow> _loginFailures = new();
    private readonly object _failuresLock = new();
    private const int FailureWindowsBeforePrune = 4096;
    private readonly PartitionedRateLimiter<string> _unidentified;
    private readonly PartitionedRateLimiter<string> _linkUnidentified;

    public PasswordDoorRateLimiter()
        : this(DefaultPerAddressLimit, DefaultPerClientLimit, DefaultWindow)
    {
    }

    public PasswordDoorRateLimiter(int perAddressLimit, int perClientLimit, TimeSpan window)
    {
        Window = window;
        _perAddress = Fixed(perAddressLimit, window);
        _perClient = Fixed(perClientLimit, window);
        _loginPerClient = Fixed(DefaultLoginPerClientLimit, window);
        _unidentified = Fixed(DefaultUnidentifiedPerMinute, UnidentifiedWindow);
        _linkUnidentified = Fixed(DefaultUnidentifiedLinkPerMinute, UnidentifiedWindow);
    }

    public TimeSpan Window { get; }

    /// <summary>
    /// "Forgot password": per client (when clients can be told apart), then per e-mail address — the target: a flood of
    /// mails at one administrator is capped whoever sends it. True = go on.
    /// <para>FIX5 — when clients can NOT be told apart there is no per-client count (it would be one bucket for everybody),
    /// and the per-address partitions then grow with the addresses asked. They are NOT bucketed here: a shared bucket would
    /// let junk for other addresses cap mails to a real one. Each key is a fixed-length hash; the number of partitions is
    /// not capped.</para>
    /// </summary>
    /// <param name="countPerClient">REQUIRED (FIX5) — the caller states whether the client is told apart
    /// (<see cref="ClientIdentity.Identified"/>); no default can quietly make a global bucket.</param>
    /// <para>WP-USERS-ROLES-CLOSE-FIX1 FIX4 item 2 — a client behind an UNLISTED forwarder (or one that sends a made-up
    /// X-Forwarded-For from outside the list) is no longer "identified": without the per-client count, a stream of fresh
    /// addresses would meet no limit at all (each one a store read). The door's coarse ceiling bounds it; the per-address
    /// count still caps the mails to any one address.</para>
    public bool TryAcquireForgotPassword(string client, string? email, bool countPerClient)
        => (countPerClient
               ? TryAcquire(_perClient, $"forgot-password|client|{client}")
               : TryAcquire(_unidentified, "forgot-password|platform|all-clients"))
           && TryAcquire(_perAddress, $"forgot-password|address|{Normalize(email)}");

    /// <summary>
    /// BL-529 FIX5 — the set-password link counts ONLY attempts whose link did not match. The link is 64 random bytes: no
    /// count protects it, and any count junk could raise would only be a lever to lock the owner out — so a request that
    /// carries the valid link is never counted and never refused (the caller compares the link FIRST). Invalid attempts are
    /// counted per client and per (client, address) when clients are told apart. Otherwise per address, folded into
    /// <see cref="UnidentifiedLinkBuckets"/> fixed buckets (FIX6): every junk address would else be a new partition, and a
    /// shared bucket is harmless here because the owner's valid link skips the count. True = answer as usual (400);
    /// false = 429.
    /// </summary>
    public bool TryCountInvalidLinkAttempt(string client, string? email, bool countPerClient)
        => countPerClient
            ? TryAcquire(_perClient, $"reset-password|client|{client}")
              && TryAcquire(_perAddress, $"reset-password|client-address|{client}|{Normalize(email)}")
            : TryAcquire(_perAddress, $"reset-password|address-bucket|{AddressBucket(email)}");

    /// <summary>WP-USERS-ROLES-CLOSE-FIX1 FIX2 item 4 — the TENANT set-password link's two doors (the page's read-only check
    /// and the redemption POST): EVERY request is counted, before anything is read, with its own prefix (never shares a count
    /// with the platform door's "reset-password|…" keys, nor the check with the redemption) — the valid link's too; the link's
    /// 64 random bytes, not this count, are what keep it from being guessed. Per client when clients can be told apart.
    /// FIX3 item 3 — when they CANNOT, no per-address count any more (5 junk requests for a victim's address locked the
    /// victim's real link out for 15 minutes, again and again): the coarse ceiling for everybody instead. True = go on;
    /// false = 429.
    /// FIX5 addendum (small item) — the answer carries the window that refused: the per-minute address bucket says 60 s, not
    /// the per-client 15 minutes.</summary>
    public DoorVerdict TryAcquireTenantLinkDoor(TenantLinkDoor door, string client, string? email, bool countPerClient)
    {
        var prefix = door == TenantLinkDoor.Check ? "tenant-link-check" : "tenant-link-set";
        return countPerClient
            ? (TryAcquire(_perClient, $"{prefix}|client|{client}") ? DoorVerdict.Go : new(false, Window))
            : (TryAcquire(_linkUnidentified, $"{prefix}|address-bucket|{AddressBucket(email)}")
                ? DoorVerdict.Go
                : new(false, UnidentifiedWindow));
    }

    /// <summary>A door's answer: go on, or 429 with the length of the window that refused (FIX4 item 4: Retry-After says
    /// THAT window — 60 s for a per-minute ceiling, never the 15-minute one).</summary>
    public readonly record struct DoorVerdict(bool Allowed, TimeSpan RetryAfter)
    {
        public static DoorVerdict Go => new(true, TimeSpan.Zero);
    }

    /// <summary>WP-USERS-ROLES-CLOSE-FIX1 FIX3 items 2 and 5, FIX4 items 1 and 4 — the sign-in doors (tenant, platform, the
    /// second factor) and self-registration are counted BEFORE the request is validated or the store is read. Per client when
    /// clients can be told apart: every attempt against <see cref="DefaultLoginPerClientLimit"/>, and refused at once while
    /// the client's WRONG attempts (<see cref="RecordLoginFailure"/>) are used up. Otherwise the coarse ceiling, per tenant on
    /// the tenant doors (the tenant the request names — no read), per door on the platform door — never per address.</summary>
    public DoorVerdict TryAcquireLoginDoor(LoginDoor door, string client, bool countPerClient, Guid? tenantId)
    {
        var prefix = Prefix(door);
        if (countPerClient)
        {
            if (!TryAcquire(_loginPerClient, $"{prefix}|client|{client}")) return new(false, Window);
            return FailuresLeftFor(prefix, client) is { } left ? new(false, left) : DoorVerdict.Go;
        }

        var scope = door == LoginDoor.Platform ? "platform" : $"tenant:{(tenantId is { } id ? id.ToString("N") : "none")}";
        return TryAcquire(_unidentified, $"{prefix}|{scope}|all-clients") ? DoorVerdict.Go : new(false, UnidentifiedWindow);
    }

    /// <summary>FIX4 item 4 — a WRONG attempt (the door answered 401), counted per client AFTER verification: a success never
    /// uses this count up. Without client identity there is no client to count.</summary>
    public void RecordLoginFailure(LoginDoor door, string client)
    {
        var key = Key($"{Prefix(door)}|failures|client|{client}");
        var now = DateTime.UtcNow;
        lock (_failuresLock)
        {
            if (_loginFailures.Count >= FailureWindowsBeforePrune)
            {
                foreach (var ended in _loginFailures.Where(entry => entry.Value.EndsAt <= now).Select(entry => entry.Key).ToList())
                    _loginFailures.TryRemove(ended, out _);
            }

            _loginFailures[key] = _loginFailures.TryGetValue(key, out var open) && open.EndsAt > now
                ? open with { Count = open.Count + 1 }
                : new FailureWindow(now + Window, 1);
        }
    }

    /// <summary>FIX5 addendum (small item) — the time left in the client's wrong-attempt window when it is used up, else
    /// null.</summary>
    private TimeSpan? FailuresLeftFor(string prefix, string client)
    {
        var now = DateTime.UtcNow;
        return _loginFailures.TryGetValue(Key($"{prefix}|failures|client|{client}"), out var open)
               && open.EndsAt > now && open.Count >= DefaultLoginFailuresPerClientLimit
            ? open.EndsAt - now
            : null;
    }

    private readonly record struct FailureWindow(DateTime EndsAt, int Count);

    private static string Prefix(LoginDoor door) => door switch
    {
        LoginDoor.Tenant => "tenant-login",
        LoginDoor.Platform => "platform-login",
        LoginDoor.Mfa => "tenant-mfa",
        _ => "tenant-register"
    };

    public enum LoginDoor { Tenant, Platform, Register, Mfa }

    public enum TenantLinkDoor { Check, Redeem }

    /// <summary>FIX6 — the bucket an address falls in while clients cannot be told apart (0 … buckets − 1).</summary>
    public static int AddressBucket(string? email)
        => (int)(BitConverter.ToUInt32(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(email))), 0) % UnidentifiedLinkBuckets);

    public void Dispose()
    {
        _perAddress.Dispose();
        _perClient.Dispose();
        _loginPerClient.Dispose();
        _unidentified.Dispose();
        _linkUnidentified.Dispose();
    }

    private static PartitionedRateLimiter<string> Fixed(int permits, TimeSpan window) =>
        PartitionedRateLimiter.Create<string, string>(key => RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, permits),
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    private static string Normalize(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    // Every partition key is a fixed-length SHA-256: no caller-chosen string is kept, whatever its length.
    private static bool TryAcquire(PartitionedRateLimiter<string> limiter, string key)
    {
        using var lease = limiter.AttemptAcquire(Key(key));
        return lease.IsAcquired;
    }

    private static string Key(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
}
